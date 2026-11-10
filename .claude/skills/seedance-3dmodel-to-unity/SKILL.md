---
name: seedance-3dmodel-to-unity
description: Use when importing a rigged, animated FBX output from Tripo or Hunyuan (混元) — a zip or folder with one .fbx plus Textures/ — into a Unity project as a character prefab with an Animator whose controller already holds the FBX's animations, or re-importing an updated model into an existing prefab. Triggers on 导入模型、FBX 进 Unity、Tripo / 混元模型导入、绑骨模型、做成角色 Prefab、挂 Animator、重导 / 更新模型、Seedance 3D model to Unity.
---

# Seedance 3D 模型 → Unity 角色 Prefab

## Overview

Tripo / 混元生成并绑骨、带动作的 FBX 产物（zip 或解开的目录：一个 `.fbx` + `Textures/`）由一份 Unity Editor C# 包 `SeedanceModelImporter` 做成角色 Prefab。本 skill 负责：把包临时推进目标工程 → 在运行中的 Editor 里调一次 `unity command seedance model_import` → 离屏截图给用户看 → **把包删掉** → 汇报。

- 工程里不留 Seedance 代码：包只在导入期间待在 `Assets/SeedanceModelImporter/`，流程结束（成功或失败）都删掉。和 `seedance-ui-to-unity` 的包互不影响。
- Prefab 上不挂 Seedance 脚本。重导要用的信息（动画名、贴图 / 材质、骨骼名）存在 `Assets/SeedanceKlab/Model/<Prefab 路径去掉 Assets/>.json`，随工程提交（`seedance-ui-to-unity` 的记录在旁边的 `Assets/SeedanceKlab/UI/`）。

导出的东西：

```
IMPORT_DIR/
  <P>.fbx                       Generic 骨骼；动画去掉 "Armature|" 前缀（idle / walk / sit…）
  Textures/<P>_BaseColor|Normal|Metallic.<ext>   按用途固定命名（Tripo 的金属度图转成 URP 格式）
  Materials/<P>.mat             URP Lit
  <P>.controller                每段动画一个状态，默认 idle，不连切换线、不加参数
  <P>.prefab                    根（空物体，给程序挂东西）└─ <P>_Model（FBX 实例 + Animator）
```

- **原地播**：Animator 不开 Apply Root Motion；动画里的水平位移 / 转向不进姿势。角色移动由程序负责。
- 名字含 idle / walk / run 的动画首次设为循环，其余不循环；之后归程序。
- 两家 FBX 都带一个多余的 `Icosphere`（转换流程残留），首次导入时在 Prefab 里关掉。
- 重导只更新 **模型、动画、贴图**：程序加的组件 / 挂点、Controller 里的切换线 / 参数 / 状态、clip 上的事件和设置、材质参数都保留。
- 展示场景 `Assets/Product/Scene/3DViewer.unity`（地面 + 方向光 + 相机，首次导入时创建）：只显示最新导入的模型，Play 后播 idle。

`SKILL_DIR` = 本文件所在目录。下面所有 `unity` 命令都带 `--no-banner --project-path "$PROJECT"`——本机常同时开着别的工程的 Editor。

## 参数（缺必填项就问用户）

| 参数 | 必填 | 说明 |
|---|---|---|
| `SOURCE` | 是 | 产物 zip 或解开后的目录，绝对路径 |
| `PROJECT` | 是 | Unity 工程根目录，绝对路径 |
| `IMPORT_DIR` | 是 | 工程内目录，`Assets/` 开头，如 `Assets/Characters/Knight` |
| `PREFAB_NAME` | 否 | 只能用英文字母、数字、`_`、`-`；不给就取 `IMPORT_DIR` 最后一级（那一级是中文就必须给） |

## 步骤

1. **前置检查**
   - `command -v unity`
   - `head -1 "$PROJECT/ProjectSettings/ProjectVersion.txt"` 必须是 `m_EditorVersion: 6000.` 开头，否则停下告诉用户只支持 Unity 6。
   - `grep -q com.unity.render-pipelines.universal "$PROJECT/Packages/manifest.json"`，没有就停下告诉用户：材质用 URP Lit，工程得先装 URP。
   - `grep -q com.unity.pipeline "$PROJECT/Packages/manifest.json"`，没有就 `unity pipeline install --project-path "$PROJECT"`。
2. **推包**：`bash "$SKILL_DIR/scripts/install_package.sh" "$PROJECT"`（装到 `Assets/SeedanceModelImporter/`；旧版的记录目录 `Assets/SeedanceModel/` 会连同 .meta 迁到 `Assets/SeedanceKlab/Model/`）。**从这一步起，后面任何一步失败都先做第 6 步删包，再汇报。**
3. **Editor**：看 `unity status --format json` 里有没有 `project` 等于 `$PROJECT` 的实例。
   - 没有：`unity open "$PROJECT"`，再 `unity status --until-ready --project-path "$PROJECT"`。刚打开的 Editor 会自己编译，**不要**马上 `recompile`（会返回 503 Server Busy）。
   - 已经开着：`unity recompile --project-path "$PROJECT"`，让它编译刚推进去的包。
   - 最后看 `unity list --project-path "$PROJECT"` 定命令写法（没有就再 `unity status --until-ready` 后重查一次）：
     - 有 `model_import`（分组 `seedance`，pipeline ≥ 0.9）：下文写法照用。
     - 有 `seedance_model_import`（pipeline 0.8 不支持分组）：下文的 `command seedance model_xxx` 一律换成 `command seedance_model_xxx`。
4. **导入**：把请求写进临时 JSON（有 scratchpad 就放 scratchpad），字段 `source` `importDir` `prefabName`，没有的留空串。
   ```bash
   unity --no-banner command seedance model_import --request "$REQ" --project-path "$PROJECT" --result-only --timeout 600
   ```
   结果是一个 JSON 字符串（ModelImportReport），再解析一层。混元的 FBX 有几十 MB，导入要一两分钟。
5. **截图**：
   ```bash
   unity --no-banner command seedance model_capture --prefab "<report.prefabPath>" --out "<临时目录>/capture.png" --project-path "$PROJECT" --result-only
   ```
   用 Read 打开 `capture.png` 给用户看（idle 第一帧，深灰底）。产物里没有预览图，看站姿、朝向、贴图是否正常，配合 report 的高度判断缩放。
6. **删包**（成功、失败都做）：
   ```bash
   bash "$SKILL_DIR/scripts/uninstall_package.sh" "$PROJECT"
   unity --no-banner recompile --project-path "$PROJECT"
   unity --no-banner status --until-ready --project-path "$PROJECT"
   ```
   `recompile` 会让 Editor 刷新资源、发现包没了并重新编译。之后 `unity --no-banner list --project-path "$PROJECT"` 里不应再有 `model_import`。
   - Editor 连不上 / 超时 / Safe Mode：只删文件，不等；汇报里说「包已删，请手动刷新或重开 Editor」。
   - 删包后的编译错误：只把和 Seedance 有关的算本次失败；工程原有的、无关的照实列出。
7. **汇报**：Prefab、Controller、展示场景路径，`heightMeters`、`clips`，以及 report 里非空的其他字段（含义见下表）。

## Report 字段

| 字段 | 含义 | 要用户做什么 |
|---|---|---|
| `heightMeters` | 模型高度（米） | 不像角色该有的高度就检查缩放 |
| `clips` | 本次 FBX 里的动画，`(loop)` 表示循环 | — |
| `createdStates` / `updatedStates` | Controller 里新建 / 换了动作的状态 | — |
| `missingClips` | 上次有、这次 FBX 里没了的动画：状态留着、动作已清空 | 确认不要了再手动删状态 |
| `removedBones` | 上次有、这次没了的骨骼 | 程序挂在这些骨骼下的东西要检查 |
| `orphanTextures` | 本次没用到的贴图（没删） | 确认没被引用后删 |
| `warnings` | 多余的 Icosphere、认不出用途的贴图、多材质槽、高度异常、撞名状态、展示场景没保存（保存失败，或用户开着它且有没保存的修改）等 | 照提示处理；展示场景的问题不影响 Prefab |

## 出错时

- 照 unity-cli skill 的规则：超时、Editor 进 Safe Mode（`unity pipeline list` 的 Safe Mode 列）、连不上，如实报告做了什么、失败在哪，**不要**循环重试、换别的 Editor、或手改 `.prefab` / `.meta` / `.unity` / `.controller` 文本。
- 报「Editor 正处于 Play Mode」：请用户退出 Play Mode（或 `unity command editor.playmode stop`，先问用户）后重跑；这时什么都还没写进工程。
- `model_import` 抛的中文错误消息就是给用户看的（路径不对、产物里没有 / 有多个 FBX、Prefab 名非 ASCII、没装 URP），原样转述并问怎么改。
- 编译错误：看 `unity recompile --project-path "$PROJECT"` 的输出；`Assets/SeedanceModelImporter/` 是临时目录，别在工程里改它，改 `$SKILL_DIR/SeedanceModelImporter/` 再推包。
- 「找不到它的导入记录」「导入记录损坏」「多份导入记录指向同一个 Prefab」：原样转述。能从版本库恢复记录最好；否则删掉那个 Prefab 再导入会按首次导入重建，**程序加的东西会丢**，先问用户。这类错误发生时工程里什么都还没写。
- 「导入记录是只读的」：多半是版本管理（如 Perforce）没 checkout，请用户 checkout 那份记录后重跑；这时什么都还没写。
- 推包报「Assets/SeedanceModel/ 和 Assets/SeedanceKlab/Model/ 里都有导入记录」：旧版记录目录从版本库回来了（多半是别人用旧版导入后提交的）。原样转述，请用户把旧目录里的记录连同 .meta 并进新目录、删掉旧目录后重跑；这时什么都还没写，包也没推。
- 「Prefab 已保存，但导入记录没写成」：照报错排除原因后，从版本库恢复 Prefab 再导。

## Common Mistakes

| 错误 | 正确做法 |
|---|---|
| 不带 `--project-path`，命令打到了别的工程的 Editor | 每条 `unity` 命令都带 |
| 期待 Animator 里已经连好 idle → walk 的切换 | 不连；切换线、参数由程序加，重导不会碰 |
| 期待角色跟着 walk 动画往前走 | 原地播；移动由程序控制 |
| 手动删了 / 改坏了 `Assets/SeedanceKlab/Model/` 下的记录 | 重导会报错停下；从版本库恢复记录 |
| 导入失败后直接汇报，没删包 | 推包之后任何失败都先跑删包 |
| 把 UI 产物（layout.json + slices/）交给本 skill | 那是 `seedance-ui-to-unity` 的活 |
