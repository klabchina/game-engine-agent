# Seedance ui-maker 产物 → Unreal UMG：设计

> 2026-11-10。Unity 侧对应设计：`2026-10-21-seedance-ui-to-unity-design.md`、`2026-11-04-seedance-ui-no-residue-design.md`。上游产物格式以前者「上游产物格式」一节为准，这里不重复。

## 意图

ui-maker 能把一套 UI 切成透明 PNG + `layout.json`，Unity 侧已经有 skill 把它导成 UGUI Prefab。现在要 Unreal 这一端：给一份 ui-maker 产物和一个 UE 工程，skill 通过 unreal-mcp 驱动**运行中的编辑器**，把切图导入并搭成一个**原样还原预览图**的 **UMG Widget Blueprint**；产物更新后再跑一次，只刷新贴图、位置、尺寸，不毁掉程序加上去的东西。

这是 README 目标「通过 Unreal 命令行直接创建 UI 界面（UMG）」。

## 目标

- 一次 skill 调用把一个 ui-maker 任务变成可用的 Widget Blueprint，按设计分辨率显示时与 `preview.png` 一致。
- 支持重导：规则与 Unity 版一致（只更新贴图 / 位置 / 尺寸，保留程序的改动）。
- 工程里不留 Seedance 代码或插件；只多出贴图、Widget Blueprint 和导入记录 JSON。

## 非目标（这一版不做）

- 自适应锚点（贴边、拉伸）。整块 UI 按设计分辨率等比缩放、居中。
- 九宫格、交互逻辑、展示关卡。
- 自动导入字体文件（MCP 没有导入字体的工具）。
- 修改 ui-maker 管线或 `layout.json` 格式。

## 反目标（技术上能跑、但算失败）

- 重导把程序加的控件、蓝图变量绑定、事件图表冲掉。
- 重导后贴图资源路径变化，导致引用断掉。
- 中文显示成方块。
- 与 `preview.png` 有肉眼可见的错位、缩放或叠放顺序错误。
- 导入中途失败，工程里留下一半新一半旧的资源。

## 已定的做法

**由用户定的：**
- 产出 UMG Widget Blueprint；第一版就同时支持首次导入与重导。
- 工程里不留代码：导入逻辑放在 skill 目录，经 MCP 的沙箱 Python（`ProgrammaticToolset.execute_tool_script`）执行。
- 执行方案：skill 里的沙箱脚本 + 本地 python3 启动器（只用标准库）直接调 MCP HTTP 接口。
- 字体：默认引擎字体（中文走引擎自带后备字体）；传了工程里的字体资产就用它。
- 不做展示关卡，用设计器截图与 `preview.png` 对照。
- 第 1～3 节的结构、重导、流程设计（2026-11-10 逐节确认）。

**我的判断（用户可推翻）：** 下文没有标注「用户定」的细节。

## 第 1 节：UMG 结构

### 资产

- Widget Blueprint：`<导入目录>/WBP_<名字>`，父类 `UserWidget`。名字只能用 ASCII 字母、数字、`_`、`-`；不传就取导入目录最后一级（那一级不合法就报错，要求传名字）。

### 根结构（用户定）

```
Root            CanvasPanel
└ ScaleBox      ScaleBox（Stretch = ScaleToFit），铺满 Root
  └ SizeBox     SizeBox，宽高 = 设计分辨率
    └ Design    CanvasPanel，所有元素放这里
```

作用相当于 Unity 的 CanvasScaler：元素坐标直接用设计像素，与工程 DPI 曲线无关；换屏幕比例时整块居中留边。根结构的四个控件也记入导入记录（见第 2 节），重导时按名字找回，程序改了它们的属性不覆盖。

### 每个元素两层（用户定）

- **容器**：`CanvasPanel`，名 `<type>_<id>`（如 `button_e62`）。负责位置、尺寸，子元素挂在它下面。可见性 `SelfHitTestInvisible`。
- **显示控件**：容器的第一个子节点，锚点四边拉伸铺满容器，名 `<type>_<id>_<控件>`（如 `button_e62_Button`、`text_e63_Text`、`panel_e1_Image`）。
- 子元素的容器排在显示控件之后，按 `layout.json` 数组顺序。

### type → 显示控件（用户定）

| type | 显示控件 | 设置 |
|---|---|---|
| panel / slot / icon / frame / image | Image | Brush = 贴图，ImageSize = 贴图尺寸；`HitTestInvisible` |
| button | Button | Normal / Hovered / Pressed 三种 Brush 都用贴图；Hovered 着色 1.1 倍亮、Pressed 0.85 倍暗（我的判断） |
| tab | CheckBox（`ToggleButton` 样式） | 未选中 / 选中各状态 Brush 都用贴图，着色规则同 Button |
| bar | ProgressBar | FillImage = 贴图、BackgroundImage 不画、从左往右、Percent = 1 |
| text | TextBlock | 见「文字」；`HitTestInvisible` |
| missing 元素 | 无显示控件，只有容器占位 | 报告 `missing` |

### 位置与层级

- 容器的 CanvasPanelSlot：Anchors (0,0)-(0,0)、Alignment (0,0)、`bAutoSize = false`；Offsets = (x − 父.x, y − 父.y, w, h)。父为根时父.x = 父.y = 0。出图元素用 `rect`，missing 元素用 `drawnRect`。
- 同级顺序 = 数组顺序（越后越靠上），靠 CanvasPanel 子节点顺序表达，ZOrder 保持 0。
- 只有 Button / CheckBox 接受点击（和 Unity 版的 raycastTarget 规则一致）。

### 文字

- TextBlock 在容器里：水平按 `align` 锚在左 / 中 / 右，垂直居中（Slot 锚点与 Alignment 取 (0|0.5|1, 0.5)，`bAutoSize = true`）。
- 字号：ui-maker 是像素，UMG 字号是 96 DPI 下的点，`UMG 字号 = 像素 × 0.75`。没有字号：用 27（= 36 像素）并警告。
- 颜色：`#RRGGBB` / `#RRGGBBAA` 转 sRGB → 线性颜色；解析不了用白色并警告。
- 字体：传了 `FONT` 用它；否则用引擎默认字体。粗体用字体里的 `Bold` 字形，字体没有就用默认字形并警告。
- 不自动换行。

## 第 2 节：资源、导入记录与重导

### 贴图

- 路径：`<导入目录>/Textures/T_<type>_<id>`。`sameAs` 非空时用主元素的 type 与 id，同款元素共用一张。名字由 id 推出，ui-maker 里改元素名不换资源。
- 设置：`CompressionSettings = UserInterface2D`、`LODGroup = UI`、`MipGenSettings = NoMipmaps`。
- 重导时贴图怎么更新（2026-11-10 V1 验证后由用户定）：MCP 的 `TextureTools.import_file` 遇到同名资产直接报错，不能原地覆盖；`AssetTools.delete` 会强删并把内存里所有引用清空。所以：
  - 启动器算每张切图文件的 SHA-256，记录里存「贴图名 → 哈希」。哈希没变：不碰贴图。
  - 哈希变了（或贴图存在但记录里没有它的哈希）：查引用者（`AssetTools.get_referencers`）。只有本 Widget Blueprint 引用（或没人引用）→ 删除、同名重新导入、重设导入设置，并给所有用它的显示控件重设笔刷；还有别的资产引用 → 不动这张贴图，`warnings` 里列出「T_xxx 被 <资产> 引用，切图已更新但没有替换，请手动处理」，记录里保留旧哈希（下次重导还会提示）。
  - 引用永远不会被悄悄清空。
- 本次产物用不到的贴图不删，报告 `orphanTextures`。

### 导入记录

- 位置：`<工程>/Content/SeedanceKlab/UI/<WBP 路径去掉 /Game/>.json`，如 `/Game/UI/Generated/X/WBP_X` → `Content/SeedanceKlab/UI/UI/Generated/X/WBP_X.json`（用户定）。与 Unity 的 `Assets/SeedanceKlab/UI/` 对应。
- 用 MCP `AssetTools.write_file` / `read_file` 读写。UE 不把 `.json` 当资产，不进游戏包；随工程提交。
- 内容（字段名由实现定，可读、稳定排序）：

```json
{
  "formatVersion": 1,
  "widgetBlueprint": "/Game/UI/Generated/X/WBP_X",
  "designWidth": 2560, "designHeight": 1440, "layoutVersion": 1,
  "rootWidgets": { "root": "Root", "scaleBox": "ScaleBox", "sizeBox": "SizeBox", "design": "Design" },
  "textures": { "T_button_e62": "<切图文件 SHA-256>" },
  "elements": [
    { "elementId": "e62", "container": "button_e62", "view": "button_e62_Button",
      "sourceName": "升级", "type": "button", "layoutParentId": "e1" }
  ]
}
```

- 读到不认识的 `formatVersion` 就报错停下。
- 记录与 Widget Blueprint 的对应以记录里的 `widgetBlueprint` 路径为准；程序挪 / 改名了 Widget Blueprint，需要连同记录一起挪（UE 没有可用的稳定 GUID 查找，这一版不做按 GUID 找回，写进 SKILL.md）。

### 重导怎么认节点（用户定）

UMG 控件没有 Unity fileID 那样的稳定 id，只能按控件名认：

1. 按记录里的容器名找。
2. 找不到，按记录里的显示控件名找，取它的父控件当容器。
3. 都找不到：当作被程序删掉，按新元素重建，报告 `rebuilt`。

找到后，把容器 / 显示控件的当前名字写回记录（程序为蓝图变量改名不影响下次重导）。**限制**：容器和显示控件都被改名的节点会被重建出一份重复的——写进 SKILL.md。

### 重导规则（与 Unity 版一致）

- 只更新**贴图、位置、尺寸**。文字内容与样式、同级顺序、显隐、程序加的控件与蓝图图表都不碰。
- 父节点：容器当前的父还是记录里上次的 layout 父 → 程序没挪过，跟着上游改父级并按数组顺序插入；否则算程序挪走，只更新尺寸、不动位置，报告 `moved`。
- 产物里没了的元素：容器设 `Collapsed`，不删，报告 `deactivated`；之后产物里又出现，恢复为 `SelfHitTestInvisible` 并报告。
- type 变了：不换显示控件，报告 `typeChanged`。
- missing 占位：上游补出图后，补上显示控件。判断依据是**记录里标记的「首次导入时为 missing」**，不看容器里有没有控件（避免把程序有意删掉的显示控件加回来）。
- 记录里没有的控件（手加的）：完全不碰。

### 先检查后写入

脚本在写任何东西之前完成全部检查，任何一项不过就抛中文错误、什么都不写：

- 编辑器不在 PIE；
- `layout.json` 合法（version、resolution、id 唯一、parent / sameAs 存在、每个元素有位置）；
- 所有要用的切图文件都存在；
- `FONT` 给了就必须能加载为字体；
- 已有 Widget Blueprint 时记录必须存在且能解析；有记录但 Widget Blueprint 不在了 → 报错（要么恢复，要么删记录按首次导入）；
- 要写的 Widget Blueprint、已存在的贴图都能编辑（版本控制没锁：`AssetTools.can_edit_asset`）。

写入阶段出错：Widget Blueprint 未保存前出错，不保存；已保存而记录没写成，报「Widget Blueprint 已保存，但导入记录没写成，不要直接重导，先从版本库恢复」。

## 第 3 节：执行流程、skill 结构与测试

### 目录

```
.claude/skills/seedance-ui-to-unreal/
  SKILL.md
  editor/ui_import.py      # 沙箱脚本：全部导入逻辑，只用 json / re / math，经 execute_tool 调 MCP 工具
  scripts/run_import.py    # 本地启动器：python3 标准库
```

沙箱脚本结构：纯逻辑（解析、几何、命名、重导对比生成「操作列表」）与工具层（包装 `execute_tool` 的少数函数）分开，便于离线测试。

### 参数

| 参数 | 必填 | 说明 |
|---|---|---|
| `SOURCE` | 是 | 产物 `out/` 目录或 `ui.zip`，绝对路径 |
| `PROJECT` | 是 | UE 工程目录（含 `.uproject`） |
| `IMPORT_DIR` | 是 | `/Game/` 开头，如 `/Game/UI/Generated/CharacterPanel` |
| `WBP_NAME` | 否 | ASCII；不给取 `IMPORT_DIR` 最后一级，资产名为 `WBP_<WBP_NAME>` |
| `FONT` | 否 | 工程里的字体资产路径 |

### 流程

1. **前置检查**：`python3` 可用；从 `$PROJECT/.mcp.json` 读 MCP 地址并能连通；编辑器打开的就是 `$PROJECT`（启动器经 MCP 读当前工程名与 `.uproject` 比对，具体用哪个工具由实现定）。
2. **解压**：启动器把产物解到 `$PROJECT/Saved/SeedanceUiImport/<随机目录>/`（zip 文件名按 UTF-8 解，拒绝越出目录的条目），流程结束（成功或失败）都删。
3. **执行**：启动器读 `editor/ui_import.py`，在开头嵌入请求（参数、`layout.json` 内容、切图绝对路径），经 MCP HTTP 调一次 `execute_tool_script`；脚本依次：检查 → 导入贴图 → 建 / 更新 Widget Blueprint → 编译 → 保存 → 写记录 → 返回 ImportReport JSON。启动器打印报告；脚本抛的错误原样打印、退出码非 0。
4. **截图对照**：打开 Widget Blueprint 设计器截图，与 `preview.png` 并排给用户看（**待验证 V2**：用 SlateInspector 截设计器画布，还是整窗截图后裁剪）。
5. **汇报**：Widget Blueprint 路径，以及报告里非空的字段。

### ImportReport 字段

| 字段 | 含义 | 要用户做什么 |
|---|---|---|
| `created` / `updated` | 新建 / 刷新了贴图与位置的元素 | — |
| `rebuilt` | 两个控件名都找不到，按新元素重建 | 确认不是改名造成的重复 |
| `deactivated` | 产物里没了，容器已 Collapsed | 确认不要了再手动删 |
| `missing` | 上游没出图，只有容器占位 | 回 ui-maker 重出这一层 |
| `moved` | 被程序挪到别的父节点下，只更新了尺寸 | 需要的话手动对齐 |
| `typeChanged` | 类型和首次导入时不同，控件没换 | 手动调整 |
| `orphanTextures` | `Textures/` 下本次没用到的贴图 | 确认没被引用后删 |
| `warnings` | 字号缺失、颜色解析失败、缺 Bold 字形等 | — |

列表项格式与 Unity 版相同：`"<id> <ui-maker 里的名字>"`。

### 出错时

照 Unity 版规则：超时、连不上、编辑器在 PIE，如实报告做了什么、失败在哪；不循环重试、不换别的编辑器、不手改 `.uasset`。脚本的中文错误原样转述并问用户怎么改。

### 测试

- **离线单测**：`unreal/SeedanceExample/Tests/SeedanceUI/`，pytest。直接 import 沙箱脚本，用假的 `execute_tool`（内存里模拟控件树、贴图、文件）。覆盖：layout 解析与校验、坐标与字号换算、命名、重导（改名找回、被挪走、删除重建、手加不碰、missing 补图、deactivate 后恢复）、先检查后写入、记录读写。不需要开编辑器。
- **端到端**：`unreal/SeedanceExample/run-e2e.sh`，编辑器开着时用两个真实产物导入到 `/Game/Tests/SeedanceUI/`，经 MCP 读回控件树断言结构、坐标、贴图引用；再改名一个显示控件、手加一个控件、重导，确认都保留且没有重复节点。跑完删除 `/Game/Tests/SeedanceUI/` 和对应记录。
- **测试数据**：`ui-popup.zip`、`ui-status.zip` 放到 `unreal/SeedanceExample/TestFixtures/`；`.gitattributes` 给 `unreal/**/*.zip` 加 LFS。

### 同步更新

- 新增 `docs/engine/unreal.md`（MCP 规则、本 skill 约定与测试）；CLAUDE.md 的 Unreal 一行指向它。
- `.claude-plugin/marketplace.json` 新增插件 `seedance-ui-to-unreal` 1.0.0。
- 实现完成后勾 README 的 UMG 目标。

## 实现第一步要验证的事（结论不符就回来改设计）

- **V1 贴图原地覆盖**：已验证——不能原地覆盖，改为「第 2 节 · 贴图」里的哈希 + 引用检查方案。
- **V2 截图**：怎么拿到设计分辨率下干净的设计器画布截图。
- **V3 属性写入**：CanvasPanelSlot 的 LayoutData、Button / CheckBox 的 WidgetStyle、ProgressBar 的 WidgetStyle 能用 `ObjectTools.set_properties` 写进去，编译、保存后保持。
- **V4 HTTP 调用**：本地启动器经 MCP HTTP 调 `call_tool` → `execute_tool_script` 的会话握手与超时；一次脚本执行能否在合理时间（目标 < 2 分钟）内处理 65 个元素。
- **V5 中文**：默认引擎字体下中文不出方块。

## 留给实现者

- 沙箱里 `execute_tool` 的工具名写法与返回结构（以 `get_execution_environment` 和各工具 output schema 为准）。
- 判断编辑器当前工程的具体工具。
- 记录 JSON 的精确字段名。
- 截图与 `preview.png` 对照时的缩放方式。
