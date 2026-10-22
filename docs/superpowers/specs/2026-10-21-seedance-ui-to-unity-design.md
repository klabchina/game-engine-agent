# Seedance ui-maker 产物 → Unity Prefab：设计

> 2026-10-21。上游管线：`/Users/li-j/project/agent-ai/art-seedance-frameanimation`（ui-maker，文档 `docs/ui-maker/`，产物写出见 `src/server/uiMakerExport.ts`）。

## 意图

ui-maker 已经能把一套 UI 切成透明 PNG + `layout.json`，但进引擎还得人工一块块摆。要一个 **Claude skill**：给它一份 ui-maker 产物和一个 Unity 工程，它通过 Unity CLI 驱动 Editor，把切图导入并搭成一个**原样还原预览图**的 UGUI Prefab；产物更新后再跑一次，只刷新图片和位置，不毁掉程序已经挂上去的东西。

这是 README「UI 界面创建工作流」那条线的 Unity 一端：Seedance 切图 → 切图 + JSON → 本 agent（Unity CLI）→ Prefab。

## 目标

- 一条命令（skill 调用）把一个 ui-maker 任务变成一个可用的 UGUI Prefab，拖进场景就和 `preview.png` 一致。
- 封装成 skill，日后直接复用，不用重新摸索。
- 可重复执行：再导一次只更新图片 / 位置 / 文字，保留手加的组件与引用。

## 非目标（这一版不做）

- 自适应锚点（按屏幕边缘拉伸 / 贴边）。一律锚在父节点左上，只保证设计分辨率下像素对齐。
- 九宫格（产物里没有 border 信息）。
- UI Toolkit、Unreal UMG。
- 交互逻辑（按钮点了干什么）。
- 修改上游 ui-maker 管线或它的 `layout.json` 格式。

## 反目标（技术上能跑、但算失败）

- 重导一次就把程序挂的脚本、Inspector 里连好的引用冲掉。
- 重导后资源 GUID 变化，导致别处对 Sprite / Prefab 的引用断掉。
- 中文显示成方块。
- 拖进场景后与 `preview.png` 有肉眼可见的错位、上下颠倒或缩放。

## 输入（skill 参数）

| 参数 | 必填 | 说明 |
|---|---|---|
| 产物路径 | 是 | ui-maker 任务的 `out/` 目录，或 `ui.zip` |
| Unity 工程路径 | 是 | 目标工程根目录（含 `Assets/`） |
| 导入目录 | 是 | 工程内路径，如 `Assets/UI/Generated/<任务名>/`，切图、字体、Prefab 都放这下面 |
| Prefab 名 | 否 | ASCII（字母、数字、`_`、`-`），不合法就报错。不传 → 用导入目录的最后一级名 |
| TMP 字体 | 否 | 工程里已有的中文 TMP Font Asset 路径。不传 → 自动生成一份动态 TMP 字体（见下） |

## 上游产物格式（已核实，以 `uiMakerExport.ts` 为准）

```
out/
  layout.json
  slices/<名字>.png          # 每「款」一张透明 PNG；同款同尺寸元素共用
  # background.png 不在 out/ 里：只在 ui.zip 和 <任务>/input/ 下。它是用户上传的游戏场景截图，不进 Prefab
  preview.png / preview-notext.png / mockup.png   # 仅供对照，不导入
  ui.zip                     # 上面这些的打包
```

`layout.json`（`version: 1`）：

- `resolution: { width, height }`：设计分辨率（2560×1440 / 1440×2560 / 3840×2160 / 2160×3840）。
- `background`: `"background.png"` 或 `null`。
- `elements[]`，数组顺序 = 叠放顺序（`order` 同值）：
  - 出图元素（type ∈ `panel button slot icon bar tab frame image`）：`id`、`name`、`type`、`image`（相对路径）、`rect`（切图实际位置，含几像素留边）、`drawnRect`（用户画的框）、`parent`（父元素 id 或 null）、`genLayer`、`order`、`sameAs`（共用切图的主元素 id 或 null）。
  - 没产出的出图元素：`missing: true`、`rect: null`，仍有 `drawnRect`。
  - 文字元素（type `text`）：`rect`（即画的框）、`text: { content, fontSize, color, align: left|center|right, bold }`、`parent`、`order`。
- 坐标：设计分辨率像素，**原点左上、y 向下**；所有 rect 都是**画布绝对坐标**（不是相对父元素）。
- `name` 是中文且任务内唯一；`id`（`e1`、`e2`…）任务内稳定、不复用。

## 已定的做法

**由用户定的：**
- 产出 **UGUI Prefab**（Image / Button 等，根节点不带 Canvas），文字用 **TextMeshPro**。
- 工程里的资源名、节点名一律 **ASCII**（见「命名」）。
- 工程路径、导入目录每次由参数指定。
- 字体：传了用传的；没传自动生成动态 TMP 字体。
- 重导：**只更新图片和位置，节点按元素 `id` 对上**，其余（手加组件、引用、手改的属性）保留。

**设计中的我的判断（用户可推翻）：**

Prefab 结构
- 根节点：**普通 RectTransform，不带 Canvas / CanvasScaler**（用户定）。四边拉伸铺满父节点（anchor (0,0)-(1,1)，offset 0），放进参考分辨率 = 设计分辨率的 Canvas 下即像素对齐。根名 = Prefab 名。
- **不带背景**（用户定）：background.png 是游戏场景截图，进 Prefab 会盖住真实画面。导入时忽略 `ui.zip` 里的 background.png。
- **展示场景**（用户定）：导入时在工程的 `Assets/Product/Scene/` 下准备一个带 Canvas + CanvasScaler（Scale With Screen Size，参考分辨率 = 设计分辨率）+ GraphicRaycaster + EventSystem 的场景，供展示 Prefab；已存在就不新建。
  - **每个工程一个场景**（用户定：一个项目的设计分辨率和横竖屏是固定的）。首次创建时 CanvasScaler 参考分辨率取本次 `layout.json` 的分辨率，Match 横屏 0 / 竖屏 1。
  - 之后导入的 UI 分辨率与场景不一致：不改场景，在报告里警告（我的判断）。
  - 每次导入：场景里没有这个 Prefab 的实例就实例化一个放到 Canvas 下；本次导入的那个设为 active，其他实例设为 inactive，方便直接看（我的判断）。保存场景。
  - 场景文件：`Assets/Product/Scene/UIShowcase.unity`（我的判断）。
- 层级按 `parent` 嵌套；同级按 `order` 排 sibling 顺序（数组顺序即叠放顺序，越后越靠上）。
- RectTransform：anchor 与 pivot 都设在父节点左上 `(0,1)`，`sizeDelta = (w,h)`，`anchoredPosition = (x - 父.x, -(y - 父.y))`。父的 x/y 取父元素自己落地后的 rect；根下元素相对画布 (0,0)。出图元素用 `rect`，`missing` 元素用 `drawnRect`。

类型 → 组件
| type | 组件 |
|---|---|
| panel / slot / icon / frame / image | Image |
| button | Image + Button（targetGraphic = 自身 Image） |
| tab | Image + Toggle（targetGraphic = 自身 Image） |
| bar | Image（Type = Filled，Horizontal，fillAmount = 1） |
| text | TextMeshProUGUI（content、fontSize、color、对齐、bold；不出图，用引擎字体排） |
| missing 元素 | 空 RectTransform 占位（按 drawnRect），并在报告里列出 |

文字元素不加 raycastTarget；出图元素的 raycastTarget 只给 button / tab 开（我的判断，避免整块面板吞点击）。

资源导入
- 切图拷到 `<导入目录>/Sprites/`，按「命名」改成 ASCII 文件名；同款元素共用同一个 Sprite（`image` 字段相同的元素用同一张）。
- TextureImporter：Sprite (2D and UI)、Single、关 mipmap、alphaIsTransparency、Clamp、`maxTextureSize` ≥ 图片最长边向上取 2 的幂（背景 2560 就要 4096，否则被压缩缩小）。压缩格式用 Unity 默认。
- **GUID 稳定**：资源按固定路径覆盖写入（拷文件覆盖、不删再建），Unity 保留原 `.meta` 的 GUID，Prefab 与外部引用不断。因为文件名由元素 id 推出，ui-maker 里改元素名不影响 Unity 里的文件名。
- 旧 Sprite：本次产物不再用到的 Sprite（比如同款分组变了、主元素换了）**不删**，在报告里列出，由人决定（我的判断）。
- 读 `ui.zip` 时要按 UTF-8 解文件名：zip 里有的条目没打 UTF-8 标志。

id 对应与重导
- 每个由导入器建的节点挂一个标记组件 `SeedanceUiElement { string elementId; string sourceName; string type; }`（runtime 脚本，随 skill 装进工程；`sourceName` 存 ui-maker 里的中文名，方便对照）。根节点挂 `SeedanceUiRoot`，记设计分辨率等来源信息。
- **同一个 Prefab 怎么认**：按路径 `<导入目录>/<Prefab 名>.prefab`。存在就走重导，不存在就新建（我的判断）。`layout.json` 里没有任务 id，所以不靠任务 id 认；复制出来的任务导进同一路径就是覆盖更新。
- 重导时：打开已有 Prefab（`PrefabUtility.LoadPrefabContents`），按 `elementId` 找节点：
  - 已有：**只更新 Sprite、位置、尺寸**（用户定）。文字内容 / 样式、父节点、sibling 顺序、组件只在首次建节点时设置，之后归程序，重导不碰。
  - type 变了（如 panel → button）：不增删组件，只在报告里提示。
  - 新增元素：新建节点。
  - 产物里已没有的元素：**不删**，设为 inactive，并在报告里列出（我的判断：删掉可能连带丢掉手挂的东西）。
  - 没有标记组件的节点（手加的）：完全不碰。
- 保存回同一路径（`SaveAsPrefabAsset`），Prefab GUID 不变。

字体
- 传了字体：直接引用。
- 没传：拷一个中文字体文件进 `<导入目录>/Fonts/`，用 `TMP_FontAsset.CreateFontAsset`（Dynamic，atlas 默认）生成 Font Asset，所有文字元素引用它；已存在则复用不重建。字体文件来源见「留给实现者」。
- 工程没装 TMP / 没导入 TMP Essential Resources 时，skill 先装好再继续。

执行方式
- 通过 Unity CLI 驱动**运行中的 Editor**（`unity status` / `unity command eval` 或自定义 `[CliCommand]`），需要工程装 `com.unity.pipeline`（Unity 6.0+）；没装就 `unity pipeline install`，Editor 没开就 `unity open`。
- 导入逻辑写成工程里的 **Editor C# 脚本**（随 skill 分发，装到 `Assets/SeedanceUI/Editor/` + `Assets/SeedanceUI/Runtime/`），skill 负责：拷脚本 → 等编译 → 调用导入入口 → 读回报告。不手改 `.prefab` / `.meta` 文本。
- 支持的 Unity 版本：6000.x（本机已装 6000.0.62f1、6000.3.16f1）。2021.3 不支持（pipeline 包要求 Unity 6）。

报告（skill 结束时给用户）
- 新建 / 更新 / 失活的元素数与名单，missing 元素，Prefab 路径。
- 在展示场景里截一张图（`capture_game_view`），与 `preview.png` 并排给用户目测（我的判断：这是验证「原样还原」的唯一可靠方式；截图没有背景，对照时看 UI 本身）。

Skill 本身
- 放在本仓库 `.claude/skills/seedance-ui-to-unity/`（入库，团队共用）：`SKILL.md` + C# 源文件 + 必要的小脚本（如解 zip）。
- `SKILL.md` 写清参数、前置检查（CLI、pipeline 包、TMP）、步骤、失败时怎么办（遵循 unity-cli skill 的规则：超时 / Safe Mode 要报告，不偷偷绕过）。

## 命名（用户定：转 ASCII）

上游的元素名和切图文件名是用户在 ui-maker 里起的中文（`面板.png`、`格子_1.png`），进 Unity 一律换成 ASCII。规则（具体格式是我的判断）：

| 对象 | 规则 | 例子 |
|---|---|---|
| Sprite 文件 | `<type>_<元素 id>.png`；元素 id 取 `sameAs` 指向的那个元素；`sameAs` 为空就用自己的 id。所以同款元素自然落到同一张 | `panel_e1.png`、`slot_e2.png` |
| 节点名 | `<type>_<元素 id>` | `button_e7`、`text_e12` |
| Prefab 文件 | `<Prefab 名>.prefab`，见输入参数 | `CharacterPanel.prefab` |
| 字体资产 | `<字体文件名>_SDF.asset`，文件名非 ASCII 时转成 ASCII | `NotoSansSC_SDF.asset` |
| 展示场景 | `UIShowcase.unity` | |

- 中文名不丢：存在每个节点的 `SeedanceUiElement.sourceName` 里。
- 目录结构：`<导入目录>/<Prefab 名>.prefab`、`<导入目录>/Sprites/`、`<导入目录>/Fonts/`（没传字体时）。

## 留给实现者

- 导入入口是 `unity command eval` 调静态方法，还是注册 `[CliCommand]`；参数怎么传（JSON 文件路径最稳）。
- 没传字体时的默认字体文件来源（本机系统字体 / skill 自带一份开源字体），以及 license 提示文案。
- C# 里解析 `layout.json` 的方式（JsonUtility 对 null / 可选字段的处理）。
- 测试怎么做：至少用 `data/ui-maker-tasks/` 下现有 3 个任务的真实产物跑一遍新建 + 重导，验证 GUID 不变、手加组件保留、截图与 preview 对齐。
