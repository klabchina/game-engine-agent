---
name: seedance-ui-to-unity
description: Use when importing a Seedance ui-maker UI (the layout.json + slices/ output folder, or ui.zip) into a Unity project as a UGUI prefab, or re-importing an updated export into an existing prefab. Triggers on 导入 UI、UI 切图进 Unity、ui-maker 产物、ui.zip、做成 Prefab、重导 / 更新 UI、Seedance UI to Unity.
---

# Seedance UI → Unity Prefab

## Overview

ui-maker 的产物（透明切图 + `layout.json`）由一份 Unity Editor C# 包 `SeedanceUI` 解析成 UGUI Prefab。本 skill 负责：把包推进目标工程 → 在运行中的 Editor 里调一次 `unity command seedance ui_import` → 离屏截图与 `preview.png` 对照 → 汇报。

- 节点、Sprite 一律 ASCII 命名（`button_e7`、`slot_e2.png`），中文名存在节点的 `SeedanceUiElement.sourceName`。
- 重导只更新 **Sprite、位置、尺寸**；文字、同级顺序、已有组件、显隐归程序，不碰。父节点只在程序没挪过时跟着上游改。产物里没了的元素设 inactive，不删。
- 同一个字体在工程里只生成一份 TMP 字体资产，后导入的 UI 共用它。
- Prefab 根不带 Canvas；工程里会有一个展示场景 `Assets/Product/Scene/UIShowcase.unity`（带 Canvas，首次导入时创建）。

`SKILL_DIR` = 本文件所在目录。下面所有 `unity` 命令都带 `--no-banner --project-path "$PROJECT"`——本机常同时开着别的工程的 Editor。

## 参数（缺必填项就问用户）

| 参数 | 必填 | 说明 |
|---|---|---|
| `SOURCE` | 是 | 任务的 `out/` 目录或 `ui.zip`，绝对路径 |
| `PROJECT` | 是 | Unity 工程根目录，绝对路径 |
| `IMPORT_DIR` | 是 | 工程内目录，`Assets/` 开头，如 `Assets/UI/Generated/CharacterPanel` |
| `PREFAB_NAME` | 否 | 只能用英文字母、数字、`_`、`-`；不给就取 `IMPORT_DIR` 最后一级（那一级是中文就必须给） |
| `TMP_FONT` | 否 | 工程里现成的中文 TMP Font Asset 路径 |

## 步骤

1. **前置检查**
   - `command -v unity`
   - `head -1 "$PROJECT/ProjectSettings/ProjectVersion.txt"` 必须是 `m_EditorVersion: 6000.` 开头，否则停下告诉用户只支持 Unity 6。
   - `grep -q com.unity.pipeline "$PROJECT/Packages/manifest.json"`，没有就 `unity pipeline install --project-path "$PROJECT"`。
2. **推包**：`bash "$SKILL_DIR/scripts/install_package.sh" "$PROJECT"`
3. **Editor**：看 `unity status --format json` 里有没有 `project` 等于 `$PROJECT` 的实例。
   - 没有：`unity open "$PROJECT"`，再 `unity status --until-ready --project-path "$PROJECT"`。刚打开的 Editor 会自己编译，**不要**马上 `recompile`（会返回 503 Server Busy）。
   - 已经开着：`unity recompile --project-path "$PROJECT"`，让它编译刚推进去的包。
   - 最后看 `unity list --project-path "$PROJECT"` 定命令写法（没有就再 `unity status --until-ready` 后重查一次）：
     - 有 `ui_import`（分组 `seedance`，pipeline ≥ 0.9）：下文写法照用。
     - 有 `seedance_ui_import`（pipeline 0.8 不支持分组）：下文的 `command seedance ui_xxx` 一律换成 `command seedance_ui_xxx`。
4. **TMP 基础资源**：
   ```bash
   unity --no-banner command seedance ui_prepare --project-path "$PROJECT" --result-only
   ```
   返回 `"importing"` 就 `unity --no-banner status --until-ready --project-path "$PROJECT"` 后再调，直到 `"ready"`（最多 5 次）。
5. **字体**：没给 `TMP_FONT`、而 `layout.json` 里有 `"type": "text"` 时（zip 用 `unzip -p "$SOURCE" layout.json` 看），`FONT_FILE=$(bash "$SKILL_DIR/scripts/fetch_font.sh")`，并告诉用户默认用的是 Noto Sans CJK SC（SIL OFL，可随游戏分发）。
6. **导入**：把请求写进临时 JSON（有 scratchpad 就放 scratchpad），字段 `source` `importDir` `prefabName` `tmpFont` `fontFile`，没有的留空串。
   ```bash
   unity --no-banner command seedance ui_import --request "$REQ" --project-path "$PROJECT" --result-only --timeout 600
   ```
   结果是一个 JSON 字符串（ImportReport），再解析一层。
7. **截图对照**：
   ```bash
   unity --no-banner command seedance ui_capture --prefab "<report.prefabPath>" --out "<临时目录>/capture.png" --project-path "$PROJECT" --result-only
   ```
   用 Read 打开 `capture.png` 和产物的 `preview.png`（zip 就 `unzip -p` 出来）给用户并排看。截图是深灰底、`preview.png` 带场景背景，对照 UI 本身；文字因字体不同会有细微出入。
8. **汇报**：Prefab 路径、展示场景路径，以及 report 里非空的 `created` / `updated` / `deactivated` / `missing` / `moved` / `typeChanged` / `orphanSprites` / `warnings`（各是什么意思见下表）。

## Report 字段

| 字段 | 含义 | 要用户做什么 |
|---|---|---|
| `created` / `updated` | 新建 / 刷新了 Sprite 与位置的节点 | — |
| `deactivated` | 产物里没了，节点已设 inactive | 确认不要了再手动删 |
| `missing` | 上游没出图，只建了占位 | 回 ui-maker 重出这一层 |
| `moved` | 节点被程序挪到别的父节点下：只更新了尺寸，位置没动 | 需要的话手动对齐 |
| `typeChanged` | 类型和首次导入时不同，组件没改 | 手动加 / 删 Button 等组件 |
| `orphanSprites` | `Sprites/` 下本次没用到的图（没删） | 确认没被引用后删 |

## 出错时

- 照 unity-cli skill 的规则：超时、Editor 进 Safe Mode（`unity pipeline list` 的 Safe Mode 列）、连不上，如实报告做了什么、失败在哪，**不要**循环重试、换别的 Editor、或手改 `.prefab` / `.meta` / `.unity` 文本。
- 报「Editor 正处于 Play Mode」：请用户退出 Play Mode（或 `unity command editor.playmode stop`，先问用户）后重跑；这时什么都还没写进工程。
- `ui_import` 抛的中文错误消息就是给用户看的（路径不对、Prefab 名非 ASCII、缺字体、layout.json 不合法），原样转述并问怎么改。
- 编译错误：看 `unity recompile --project-path "$PROJECT"` 的输出；`Assets/SeedanceUI/` 由本 skill 管理，别在工程里改它，改 `$SKILL_DIR/SeedanceUI/` 再推包。

## Common Mistakes

| 错误 | 正确做法 |
|---|---|
| 不带 `--project-path`，命令打到了别的工程的 Editor | 每条 `unity` 命令都带 |
| 期待重导刷新文字内容 | 文字只在首次建节点时写入；要重建文字就删掉那个节点再导 |
| 手动删了 `Assets/SeedanceUI/Runtime` | Prefab 上的标记组件会变 Missing Script，重导也认不出节点；重新推包 |
| 一个工程里导入横屏和竖屏两套 UI | 展示场景按首次导入的分辨率建，另一种只会得到 warning |
