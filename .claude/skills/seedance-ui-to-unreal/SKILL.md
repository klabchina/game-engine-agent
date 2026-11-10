---
name: seedance-ui-to-unreal
description: Use when importing a Seedance ui-maker UI (the layout.json + slices/ output folder, or ui.zip) into an Unreal Engine project as a UMG Widget Blueprint, or re-importing an updated export into an existing Widget Blueprint. Triggers on 导入 UI 到 UE / Unreal / 虚幻、UMG、Widget Blueprint、WBP、ui-maker 产物、ui.zip、重导 / 更新 UI、Seedance UI to Unreal.
---

# Seedance UI → Unreal UMG

## Overview

ui-maker 的产物（透明切图 + `layout.json`）经 unreal-mcp 导成 UMG Widget Blueprint。导入逻辑是本 skill 目录下的 Python 包 `editor/seedance_ui/`，由本地启动器 `scripts/run_import.py` 拼成一个脚本交给编辑器的 MCP 沙箱执行——**工程里不留任何 Seedance 代码**，只多出贴图、Widget Blueprint 和导入记录 JSON。

- 结构：`Root` → `ScaleBox`（等比缩放到屏幕）→ `SizeBox`（设计分辨率）→ `Design`，元素都在 `Design` 里，坐标就是设计像素。
- 每个元素两层：容器 `CanvasPanel`（`button_e62`，管位置尺寸、挂子元素）+ 显示控件（`button_e62_Button`，铺满容器）。panel / slot / icon / frame / image → Image，button → Button，tab → CheckBox，bar → ProgressBar，text → TextBlock；没出图的（missing）只有容器占位。
- 资源与控件一律 ASCII 命名，贴图 `<导入目录>/Textures/T_<type>_<id>`；中文名存在导入记录的 `sourceName`。
- 导入记录：`<工程>/Content/SeedanceKlab/UI/<WBP 路径去掉 /Game/>.json`，随工程提交（UE 不会把 .json 当资产，不进包）。
- 重导只更新 **贴图、位置、尺寸**；文字、同级顺序、显隐、程序加的控件和蓝图都不碰。按控件名认节点：容器名或显示控件名有一个没改就认得出（新名字写回记录）。产物里没了的元素设 `Collapsed`，不删。
- 切图内容没变（SHA-256 相同）就不动贴图；变了且只被本 WBP 引用 → 删掉同名重导；还被别的资产引用 → 不替换，进 `warnings`。
- 字体默认引擎字体（中文走引擎自带后备字体）；可传工程里的字体资产。

`SKILL_DIR` = 本文件所在目录。

## 参数（缺必填项就问用户）

| 参数 | 必填 | 说明 |
|---|---|---|
| `SOURCE` | 是 | 任务的 `out/` 目录或 `ui.zip`，绝对路径 |
| `PROJECT` | 是 | UE 工程目录（含 `.uproject`、`.mcp.json`），绝对路径 |
| `IMPORT_DIR` | 是 | `/Game/` 开头，如 `/Game/UI/Generated/CharacterPanel` |
| `WBP_NAME` | 否 | 只能英文字母、数字、`_`、`-`；资产名为 `WBP_<WBP_NAME>`；不给取 `IMPORT_DIR` 最后一级（那一级是中文就必须给） |
| `FONT` | 否 | 工程里现成的字体资产路径，如 `/Game/Fonts/NotoSansSC` |

## 步骤

1. **前置检查**
   - Python：`python -c ""`（Windows 上 `python3` 常是应用商店占位，退出码 49），不行再试 `python3`；记为 `$PY`。
   - `$PROJECT/.mcp.json` 存在（没有就请用户在编辑器控制台跑 `ModelContextProtocol.GenerateClientConfig ClaudeCode`）。
   - 编辑器开着这个工程、MCP 服务器起着：启动器会自己检查并给出中文提示；没起来就请用户在控制台敲 `ModelContextProtocol.StartServer <端口>`。
2. **导入**（Git Bash 里**必须**加 `MSYS2_ARG_CONV_EXCL="/Game/"`，否则 `/Game/...` 会被改写成 `C:/Program Files/Git/Game/...`；不要用 `MSYS_NO_PATHCONV=1`，那会连脚本路径一起不转换）：
   ```bash
   MSYS2_ARG_CONV_EXCL="/Game/" $PY "$SKILL_DIR/scripts/run_import.py" \
     --source "$SOURCE" --project "$PROJECT" --import-dir "$IMPORT_DIR" \
     [--wbp-name "$WBP_NAME"] [--font "$FONT"] --capture "<临时目录>/capture.png"
   ```
   首次导入 65 个元素约 2 分钟，重导约 1 分钟；用 `timeout` ≥ 600000。stdout 是 ImportReport JSON，错误在 stderr。退出码：0 成功，1 产物 / 参数 / 记录问题或编辑器里报错，2 连接 / 环境问题。
3. **截图对照**：用 Read 打开 `capture.png`（WBP 编辑器整窗截图，设计器里显示的是缩小后的设计画布）和产物的 `preview.png`（zip 就 `unzip -p "$SOURCE" preview.png > <临时目录>/preview.png`），并排给用户看。截图是深灰底、preview 带场景背景，对照 UI 本身。
4. **汇报**：Widget Blueprint 路径、导入记录路径，以及 report 里非空的字段（见下表）。

## Report 字段

| 字段 | 含义 | 要用户做什么 |
|---|---|---|
| `created` / `updated` | 新建 / 刷新了贴图与位置的元素 | — |
| `rebuilt` | 容器和显示控件的名字都找不到，按新元素重建了 | 确认不是程序把两个名字都改了（那样会多出一份） |
| `deactivated` | 产物里没了，容器已设 Collapsed | 确认不要了再手动删 |
| `missing` | 上游没出图，只有容器占位 | 回 ui-maker 重出这一层 |
| `moved` | 被程序挪到别的父节点下：只更新了尺寸，位置没动 | 需要的话手动对齐 |
| `typeChanged` | 类型和首次导入时不同，控件没换 | 手动调整（如 Image 换 Button） |
| `orphanTextures` | `Textures/` 下本次没用到的贴图（没删） | 确认没被引用后删 |
| `warnings` | 字号缺失、颜色解析失败、缺 Bold 字形、贴图因外部引用没替换、隐藏的元素又出现了等 | 按内容处理 |

## 出错时

- 连不上、超时、编辑器在 PIE：如实报告做了什么、失败在哪，**不要**循环重试、换别的编辑器、或手改 `.uasset`。
- 「编辑器打开的不是这个工程」：`.mcp.json` 的端口被别的工程的编辑器占着，请用户确认。
- 中文错误消息（导入目录、WBP 名非法、layout.json 不合法、缺切图、记录损坏 / 缺失、资产被锁）是给用户看的，原样转述并问怎么改。这些检查都在写任何东西之前，报错时工程里什么都没变。
- 「已存在但找不到它的导入记录」「有导入记录但 Widget Blueprint 不在了」：能从版本库恢复最好；否则删掉 WBP（和记录）再导入会按首次导入重建，**程序加的内容会丢**，先问用户。
- 「导入记录是只读的」「不能编辑」：多半是版本管理没 checkout，请用户 checkout 后重跑。
- 「Widget Blueprint 已保存，但导入记录没写成」：**不要直接重导**（会把新控件再建一份）；排除原因后从版本库恢复 WBP 再导。
- 编辑器里的脚本报错（stderr 是 `line N: ...` 加 Traceback）：照实贴给用户；这时 WBP 没有保存，编辑器里可能有未保存的改动，请用户不要保存、关掉 WBP 编辑器后重试或回报问题。

## Common Mistakes

| 错误 | 正确做法 |
|---|---|
| Git Bash 里不加 `MSYS2_ARG_CONV_EXCL="/Game/"` | `/Game/...` 会被改写，启动器会拒绝；加上再跑 |
| 程序把容器和显示控件**都**改了名 | 至少保留一个原名（通常只改显示控件名做蓝图变量），否则重导会重建一份 |
| 挪了 / 改名了 Widget Blueprint，没挪导入记录 | 记录按 WBP 路径对应，一起挪到 `Content/SeedanceKlab/UI/` 下的新镜像路径 |
| 期待重导刷新文字内容 | 文字只在首次建节点时写入；要重建就删掉那个元素的容器再导 |
| 手改 `Content/SeedanceKlab/UI/` 下的记录 | 重导会报错停下；从版本库恢复 |
| 改名 `Design` 控件 | 找不到它会报错停下；程序自己加的控件放在任意位置都不会被碰 |
