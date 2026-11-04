# seedance-3dmodel-to-unity：带骨骼的 FBX 产物 → Unity 角色 Prefab

日期：2026-11-04　　参照：`2026-10-21-seedance-ui-to-unity-design.md`、`2026-11-04-seedance-ui-no-residue-design.md`（同一套“临时推包 + 记录文件”做法）

## 背景与目的

Seedance 的 3D 流程会用 Tripo 或混元生成模型、自动绑骨并加上动作，最后导出成一个 FBX 产物（zip，解开是一个目录）。现在要把它放进 Unity 工程，得手动完成以下几步：拖进 FBX、调整导入设置、建材质并连上贴图、建 AnimatorController 并逐个拖入动画、拼 Prefab。

新建一个 skill **`seedance-3dmodel-to-unity`**，和 `seedance-ui-to-unity` 并列，作为单独的插件发布。它把上面这些步骤一次做完：产出一个角色 Prefab，Prefab 上挂好 Animator，Animator 绑的 Controller 里已经包含 FBX 中的全部动画。上游重新生成后可以重导，程序加的东西保留。

不和 UI skill 合并（用户已确认）：两者的触发词、输入、所用的 Unity 子系统（ModelImporter / 材质 / Animator 对比 UGUI / TMP）几乎没有交集。合在一起会互相误触发，也会把两套版本和测试绑在一起。

## 目标

- 输入 Tripo 或混元的 FBX 产物（zip，或解开后的目录），产出一个能直接拖进场景的角色 Prefab：
  - 模型本体：材质已建好，贴图已连上。
  - 根节点挂 Animator，绑定自动生成的 AnimatorController。
  - Controller 里每段 FBX 动画对应一个状态。
- 导入后工程里不留任何 Seedance 代码，Prefab 上不挂 Seedance 脚本（与 UI skill 相同）。
- 可以重导：只更新模型和动画，程序加的东西保留（规则见下文“重导”）。
- 提供展示场景 `3DViewer`，打开 Play 就能看到角色播放 idle。

## 非目标

- 不做 Humanoid / Avatar：骨骼一律按 Generic 处理，只播 FBX 自带的动画（用户确认；以后要做动画重定向再改）。
- 不在 Controller 里连状态之间的切换线、不加参数：怎么切换由程序决定。
- 不统一两家的骨骼命名。
- 不处理不带骨骼的静态模型，也不处理 glb / obj 等其他格式。

## 反目标（技术上能跑但算失败）

- 重导把程序加在 Prefab 上的组件、骨骼下的挂点，或 Controller 里的切换线、参数、额外状态弄丢了。
- 重导后 Prefab 里出现第二份模型。
- 模型躺倒、大了或小了 100 倍，汇报里却没有任何提示。
- 导入结束后工程里还留着 Seedance 的 `.cs` / `.asmdef`。

## 产物格式（来自两份样例）

样例：`~/Downloads/rig-2026-11-03-m1ctb3-fbx`（Tripo）、`~/Downloads/hyrig-2026-11-02-ohuuje-fbx`（混元）。两份产物来自同一条转换流程（FBX 内部引用的贴图路径都指向 `seedance-fbx-*` 临时目录）。

```
<name>/                 （zip 解开后，或用户直接给的目录）
  <name>.fbx            二进制 FBX，骨骼根 Armature，动画名 "Armature|<clip>"
  Textures/
    *.jpg / *.png
```

| | Tripo | 混元 |
|---|---|---|
| 动画 | `Armature\|idle` / `walk` / `sit` | `Armature\|idle` / `walk` |
| 贴图 | `…_BaseColor.jpg`、`…_Normal_Bake.jpg`、`…_Metallic-tripo_.png` | 一张 `texture_20250901.png` |
| 骨骼名 | `Root`、`Spine01`… | `Hips`、`Spine`、`Spine1`… |
| 大小 | FBX 6.7MB + 贴图约 7MB | FBX 39MB + 贴图 16MB |

zip 的根目录可能直接是 `<name>.fbx`，也可能多套一层目录：建造者要两种都认。产物里应当恰好有一个 `.fbx`，没有或多于一个就报中文错误。

## 设计（用户决定的部分）

- skill 名 `seedance-3dmodel-to-unity`，单独作为一个 skill / 插件，不并入 UI skill。
- 导入后生成 Prefab，根上挂 Animator，绑定的 Controller 里有 FBX 中的全部动画。
- 骨骼用 Generic。
- 状态之间不连切换线，由程序驱动。
- 根运动：原地播。首次导入时 clip 的水平位移（XZ）和朝向（Y 旋转）都烘焙进姿势（Bake Into Pose），竖直位移按原样保留在姿势里；Animator 的 Apply Root Motion 关闭。角色的移动由程序代码负责。重导时不改已有 clip 的这些设置（随“clip 设置整份保留”）。
- 支持重导：程序加的东西保留，只更新模型和动画。
- 有展示场景，名字叫 3DViewer。
- 和 UI skill 一样：推包，导入，删包；Prefab 不挂脚本；导入记录放在工程里。

## 设计（我的判断，用户可改）

### 参数

| 参数 | 必填 | 说明 |
|---|---|---|
| `SOURCE` | 是 | 产物 zip 或解开后的目录，绝对路径 |
| `PROJECT` | 是 | Unity 工程根目录，绝对路径 |
| `IMPORT_DIR` | 是 | 工程内目录，以 `Assets/` 开头，如 `Assets/Characters/Knight` |
| `PREFAB_NAME` | 否 | 只能用英文字母、数字、`_`、`-`；不给时取 `IMPORT_DIR` 最后一级（那一级是中文就必须给） |

只支持 Unity 6，前置检查（unity CLI、`com.unity.pipeline`）与 UI skill 相同。

### 导入后的工程目录

```
IMPORT_DIR/
  <PREFAB_NAME>.fbx              从产物拷入，改名为 Prefab 名（ASCII）
  Textures/<PREFAB_NAME>_<用途>.<原扩展名>   按用途改成固定名：BaseColor / Normal / Metallic（认不出用途的保留原名）
  Materials/<PREFAB_NAME>.mat    每个 FBX 材质槽对应一份，槽位有多个时用 <PREFAB_NAME>_<槽名>.mat
  <PREFAB_NAME>.controller
  <PREFAB_NAME>.prefab
```

### FBX 导入设置（ModelImporter）

- 骨骼类型 Generic，Avatar 从这个模型自己生成。
- 动画切片：FBX 里的每个 take 对应一个 clip，clip 名去掉 `Armature|` 前缀（例如 `idle`）。如果去掉前缀后重名，或者出现非 ASCII 字符，就保留原名并写进 warning。
- 循环：只在首次导入时设置。名字是 `idle` / `walk` / `run`（不区分大小写，可带前后缀，如 `walk_fast`）的设为循环，其余不循环。之后归程序改；重导时不改已有 clip 的循环设置，新出现的 clip 照这个规则设置。
- 朝向和缩放：开启 Bake Axis Conversion，并按文件单位换算。导入后计算模型包围盒的高度，写进汇报（例如“高 1.82 m”）。高度在 0.1 m ~ 10 m 之外时额外给一条 warning（“可能缩放不对”）。
- 不用 FBX 自带的材质，材质由下一节建好后通过 remap 指定给各个材质槽。

### 材质与贴图

- 按文件名认贴图的用途（不区分大小写）：
  - 含 `basecolor` / `albedo` / `diffuse`：底色。
  - 含 `normal`：法线，导入类型设为 Normal map。
  - 含 `metallic`：金属度。
  - 只有一张贴图，且名字里没有以上关键词：当底色（混元就是这种）。
  - 其余认不出来的：保留文件，写进 warning。
- 材质用 URP Lit。如果工程没装 URP，报中文错误停下（建造者可改成退回 Standard，但要在汇报里写明）。
- Tripo 的 `…_Metallic-tripo_.png` 可能是 glTF 那种打包格式（G 通道是粗糙度，B 通道是金属度），而 URP 需要 R 通道是金属度、A 通道是光滑度。建造者先确认实际通道布局；如果确实需要转换，就在 `Textures/` 里另外生成一张 `<原名>_URP.png` 给材质用，原图保留。
- 多个材质槽：两份样例都只有一个槽。遇到多个槽时，全部用同一组贴图，写一条 warning。

### Prefab 结构

```
<PREFAB_NAME>            (根：空物体，Animator，Controller = <PREFAB_NAME>.controller，Avatar = FBX 生成的)
  └─ <PREFAB_NAME>_Model (FBX 模型的嵌套 Prefab 实例：Armature、SkinnedMeshRenderer…)
```

- Animator 挂在根上，作用于子节点里的骨骼（Animator 按相对路径找骨骼，Avatar 的根是 Model 节点，路径要能对上）。具体用哪种结构由建造者实测决定，也可以把 Animator 挂在 Model 节点上，条件是重导时程序挂在根上的东西不丢。
- 不加 Collider、Rigidbody 或任何脚本。

### AnimatorController

- 一层 Base Layer，每个 clip 对应一个状态，状态名等于 clip 名。
- 默认状态：名为 `idle` 的那个；没有 idle 就用第一个。
- 不加切换线，不加参数。

### 重导

判定为重导的条件：`IMPORT_DIR/<PREFAB_NAME>.prefab` 已经存在，并且有对应的导入记录。

- **FBX**：原地覆盖文件，`.meta` / GUID 不变，ModelImporter 设置沿用。Prefab 里的嵌套模型实例随之更新。
- **clip 设置**（我的判断）：按 clip 名对应，已有 clip 的 `ModelImporterClipAnimation` 整份保留（循环、根运动、动画事件、mask、曲线等程序改过的都不动），只把帧范围更新成新 FBX 里该 take 的范围；新 take 照首次导入的规则新建；不再有的 take 从切片里去掉（对应状态见 Controller 一条）。
- **贴图**（我的判断）：Tripo 的贴图名里带每次都会变的 UUID，所以不按原名对应，而按用途对应：本次认出的用途覆盖到固定名 `<PREFAB_NAME>_<用途>.<扩展名>`（GUID 不变；扩展名变了就删旧的、建新的，并在汇报里说明）。认不出用途的贴图保留原名拷入，并写 warning。
- **材质**：沿用首次导入时建的那份，只把贴图槽重新指向本次认出来的贴图。颜色、光滑度等其他参数不碰。本次没认出某个用途时（例如从 Tripo 换成混元，没有法线贴图）：该槽当前指向的正是导入记录里登记的那张，就清空槽位，旧文件列进 `orphanTextures`（不删）；槽位指向的是程序自己换上的贴图，就不碰。
- **Controller**：
  - 新 clip：加成新状态。
  - 已有状态（导入记录里登记过的，按状态名对应）：motion 换成新 FBX 里同名的 clip。
  - FBX 里不再有的 clip：对应状态保留，motion 置空，列进 `missingClips`。
  - 程序加的切换线、参数、额外状态、额外的 layer 都不碰；默认状态也不改。
- **Prefab**：只在根上没有 Animator 时补一个；有就只确认 Controller 和 Avatar 指向正确，其余全部不碰。程序在根上、在骨骼下加的东西都保留。
- **骨骼改名**：新旧骨骼的名字集合有差异时，把消失的骨骼名列进 `removedBones`。如果程序在已消失的骨骼下挂过东西，提醒用户检查；不自动修复。

### 导入记录

- 位置：`Assets/SeedanceModel/<Prefab 路径去掉 Assets/>.json`，随工程提交。
- 内容（`JsonUtility`）：`formatVersion`、`prefabGuid`、`fbxGuid`、`source`（`"tripo"` / `"hunyuan"` / `"unknown"`，只作参考）、`clips`（导入生成的 clip / 状态名）、`textures`（用途 → 贴图 GUID）、`materials`（材质 GUID）、`bones`（骨骼名列表，用于比较骨骼改名）。
- 按 Prefab GUID 查找，找到但不在镜像路径上就搬过去；记录损坏、Prefab 存在却没有记录时，报中文错误停下，不写任何文件；写入时先写临时文件再改名替换（原子写）。这些和 UI skill 的规则一致。实现上可以参考 UI 包里的 `UiRecordStore`，复制一份改名使用，**两个包之间不互相引用**（各自临时推包、各自删包）。

### 3DViewer 展示场景

- `Assets/Product/Scene/3DViewer.unity`，首次导入时创建。
- 场景里有：一块地面（Plane，默认材质）、一盏方向光、一个 Main Camera（斜着朝下看，按模型包围盒自动取景）。
- 每次导入把 Prefab 实例放到原点；Viewer 里其他导入的模型实例设为 inactive（按“源 Prefab 有导入记录”判断）。程序手放进去的物体不碰。已经有同一 Prefab 的实例就复用，不重复放。
- 用 Additive 方式打开、保存、关闭，不切换、不修改用户当前打开的场景（和 UI 的 ShowcaseScene 相同）。
- 打开 Play 后，模型从 Controller 的默认状态 idle 开始播放。

### 截图对照

- 新增一条命令：离屏渲染 Prefab 的 idle 第 0 帧（没有 idle 就用默认状态的 clip），正面略偏一点，深灰底，输出 PNG。
- 产物里没有预览图，所以只给用户看截图本身，配上汇报里的高度。

### skill 流程与 Editor 包

- 流程照抄 UI skill：前置检查 → 推包（`Assets/SeedanceModelImporter/`）→ 等编译 → `model_import` → `model_capture` → **删包**（成功、失败都删）→ 汇报。
- 命令注册：pipeline ≥ 0.9 用分组 `seedance model_import` / `seedance model_capture`，0.8 用 `seedance_model_import` / `seedance_model_capture`（asmdef 的 versionDefines 做法与 UI 包相同）。
- 两个包可能先后推进同一个工程（例如先导 UI 再导模型），所以 asmdef 名、命名空间、.meta GUID 都不能和 UI 包冲突：程序集叫 `Seedance.ModelImport.Editor` / `.Editor.Cli`，命名空间 `Seedance.ModelImport.*`，GUID 全部新生成。
- 拒绝在 Play Mode 下导入。
- zip 先解到工程外的临时目录再拷入，不在 `Assets/` 里解压。

### Report 字段

`prefabPath`、`controllerPath`、`scenePath`、`heightMeters`、`clips`（本次的 clip 名及循环设置）、`createdStates` / `updatedStates`、`missingClips`、`removedBones`、`orphanTextures`、`warnings`。

### 仓库内的落点

- skill 本体：`.claude/skills/seedance-3dmodel-to-unity/`（`SKILL.md`、`SeedanceModelImporter/` 的 C# 源码、`scripts/install_package.sh`、`scripts/uninstall_package.sh`）。
- `.claude-plugin/marketplace.json` 加入新插件，版本 1.0.0。
- 测试沿用 `unity/SeedanceUiExample` 工程：EditMode 测试放在 `Assets/Tests/SeedanceModel/`（独立的测试 asmdef），`run-tests.sh` 同时推两个包。
- 测试素材：Tripo 那份（约 14MB）放进 `unity/SeedanceUiExample/TestFixtures/rig-tripo/`。混元那份 55MB 太大，不进仓库；混元特有的“只有一张无后缀贴图”的情况，用 Tripo 的 FBX 配一张改了名的贴图来构造测试，混元原件只用于手动端到端验证。
- 文档：`docs/engine/unity.md` 增加本 skill 一节；根 `CLAUDE.md` 的目录表不用改。

## 测试要点

- 首次导入（Tripo 样例）：
  - Prefab 根上有 Animator，Controller 有 3 个状态，默认状态为 idle。
  - idle、walk 循环，sit 不循环。
  - 材质是 URP Lit，三张贴图都连上了。
  - 高度在合理范围内。
  - Prefab 上没有任何 `Seedance.*` 组件。
- 只有一张无后缀贴图（构造的混元情况）：作为底色连上。
- 重导：
  - 程序在根上加的组件、在骨骼下加的挂点、在 Controller 里加的切换线和参数都还在。
  - 改过的循环设置、clip 上加的动画事件、材质颜色都没有被覆盖。
  - Tripo 重导（贴图 UUID 变了）：`Textures/` 里不多出新文件，材质仍然连着固定名贴图。
  - Tripo 换成只有一张贴图的产物：法线、金属度槽被清空，旧文件列进 `orphanTextures`。
  - 新 FBX 少了一段 clip 时，对应状态保留、motion 为空，并出现在 `missingClips`。
  - 多了一段 clip 时，新增对应状态。
  - Prefab 里没有出现第二份模型。
- 产物里没有 FBX 或有多个 FBX、zip 多套一层目录、`PREFAB_NAME` 非 ASCII、Play Mode：报中文错误，且工程里什么都没写。
- 记录丢失或损坏：报错，不写任何文件。
- 3DViewer：
  - 第二次导入别的模型后，只有最新的那个是 active。
  - 程序手放的物体不受影响。
  - 不碰用户当前打开的场景。
- 推包与删包：和 UI 包同时存在时能编译通过；删掉模型包不影响 UI 包，反过来也一样。

## 留给建造者

- Animator 挂在根上还是 Model 节点上（以重导后程序加的东西不丢、动画正常播放为准）。
- Bake Axis Conversion 和单位换算的具体设置组合（用两份样例实测：站立、朝 +Z、高度合理）。
- Tripo 金属度贴图的实际通道布局，以及是否需要转换。
- 自动取景相机的具体参数，以及截图的角度。
- clip 切片的具体 API（`ModelImporter.clipAnimations` 或 `defaultClipAnimations`）。
