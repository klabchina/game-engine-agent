# seedance-3dmodel-to-unity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新 skill `seedance-3dmodel-to-unity`：把 Tripo / 混元的带骨骼 FBX 产物导进 Unity 工程，生成挂好 Animator（Controller 含全部动画）的角色 Prefab，支持保留程序改动的重导，并提供 3DViewer 展示场景。

**Architecture:** 照搬 `seedance-ui-to-unity` 的形态——一份临时推进工程的 Editor C# 包 `SeedanceModelImporter`（`Editor/` 导入逻辑 + `Editor.Cli/` 注册 `unity command`），skill 流程推包 → `model_import` → `model_capture` → 删包。导入流水线：`ModelSource`（读产物）→ `TextureWriter`（按用途固定命名）→ `FbxWriter`（拷 FBX + ModelImporter 设置）→ `MaterialBuilder`（URP Lit）→ `ControllerBuilder` → `ModelPrefabBuilder` → `ViewerScene`，导入记录由 `ModelRecordStore` 读写。与 UI 包互不引用。

**Tech Stack:** Unity 6（测试工程 6000.0.62f1，URP 17），C#，ModelImporter / AnimatorController / URP Lit，`com.unity.pipeline`（0.8 与 ≥0.9），NUnit EditMode 测试，bash。

**Spec:** `docs/superpowers/specs/2026-11-04-seedance-3dmodel-to-unity-design.md`

## Global Constraints

- 只支持 Unity 6（`m_EditorVersion: 6000.`）。骨骼一律 Generic，不做 Humanoid / Avatar 重定向。
- 包临时目录 `Assets/SeedanceModelImporter/`，导入记录 `Assets/SeedanceModel/<Prefab 路径去掉 Assets/，扩展名换 .json>`。
- 程序集 `Seedance.ModelImport.Editor` / `Seedance.ModelImport.Editor.Cli` / 测试 `Seedance.ModelImport.Tests`，命名空间 `Seedance.ModelImport.*`；**不引用** `Seedance.UiImport.*`；所有 .meta GUID 新生成（`gen_meta.py`），之后固定。
- Prefab 与 Controller 上不挂任何 `Seedance.*` 组件 / 资源；包不含 Runtime 程序集。
- 命令：pipeline ≥0.9 `seedance model_import` / `seedance model_capture`；0.8 `seedance_model_import` / `seedance_model_capture`（versionDefines `SEEDANCE_HAS_PIPELINE` / `SEEDANCE_PIPELINE_GROUPS`，同 UI 包）。
- `PREFAB_NAME` 只能 `[A-Za-z0-9_-]`；为空取 `IMPORT_DIR` 最后一级。
- 导入后目录：`<IMPORT_DIR>/<P>.fbx`、`Textures/<P>_<BaseColor|Normal|Metallic>.<ext>`（认不出的保留原名）、`Materials/<P>.mat`（多槽 `<P>_<槽名>.mat`）、`<P>.controller`、`<P>.prefab`。
- clip 名去掉 `Armature|` 前缀；首次循环规则：名字含 `idle` / `walk` / `run`（不分大小写）循环；根运动原地播：XZ 位移与 Y 旋转 Bake Into Pose、竖直保留、Apply Root Motion 关。
- 默认状态 `idle`，没有则第一个；不加切换线与参数。
- 高度在 0.1 m ~ 10 m 之外加 warning。URP 未装报中文错误。
- 所有给用户看的错误是中文 `ModelImportException`；记录损坏 / Prefab 在但无记录 / Play Mode / 产物没有或多于一个 FBX / Prefab 名非法：报错时工程里什么都没写。
- 展示场景 `Assets/Product/Scene/3DViewer.unity`，Additive 打开，不碰用户当前场景。
- 文档、注释、提交信息用中文，提交前缀 `feat:` / `fix:` / `docs:` / `refactor:`。

## Review Focus

1. **Tripo → 混元重导到同一个 Prefab**（clip 少了 `sit`、骨骼全换名、只剩一张贴图）：Prefab 保存成功，`missingClips=[sit]`、`removedBones` 非空、法线 / 金属度槽清空，程序挂在根上的组件还在。→ Task 8 测试 `Reimport_DifferentSource_ReportsAndKeepsRootComponents`（用 Tripo FBX + 构造的单贴图产物，并把记录里的骨骼 / clip 名改出差异来模拟）；Task 10 用两份真实样例手动跑一遍。
2. **同一用途贴图扩展名变了**（`_BaseColor.jpg` → `.png`）：旧的固定名文件删掉、新的建上、材质指向新的、`Textures/` 里没有两份 BaseColor。→ Task 3 测试 `Write_ExtensionChanged_ReplacesFixedFile`。
3. **SOURCE 路径含中文 / 空格，zip 多套一层目录**：照常导入。→ Task 2 测试 `Open_ZipWithWrapperDir_FindsFbx`、`Open_PathWithSpacesAndChinese`。
4. **UI 包与模型包同时在工程里**：两包都编译、`unity list` 两组命令都在；删掉一个不影响另一个。→ Task 1 的 `test_install_package.sh` 用例 + 测试工程本身同时装两包跑全量测试。
5. **重导时 Prefab 被程序加了骨骼下挂点、clip 上加了动画事件**：重导后都在。→ Task 8 测试 `Reimport_KeepsBoneAttachmentAndClipEvents`。

---

### Task 1: 包骨架、推包 / 删包脚本、测试工程接线、测试素材

**Files:**
- Create: `.claude/skills/seedance-3dmodel-to-unity/SeedanceModelImporter/Editor/Seedance.ModelImport.Editor.asmdef`、`AssemblyInfo.cs`、`ModelImportTypes.cs`
- Create: `.claude/skills/seedance-3dmodel-to-unity/SeedanceModelImporter/Editor.Cli/Seedance.ModelImport.Editor.Cli.asmdef`
- Create: `.claude/skills/seedance-3dmodel-to-unity/SeedanceModelImporter.meta` 与包内全部 .meta（`python3 unity/SeedanceUiExample/tools/gen_meta.py .claude/skills/seedance-3dmodel-to-unity/SeedanceModelImporter`）
- Create: `.claude/skills/seedance-3dmodel-to-unity/scripts/install_package.sh`、`uninstall_package.sh`
- Create: `unity/SeedanceUiExample/Assets/Tests/SeedanceModel/Seedance.ModelImport.Tests.asmdef`、`ModelTestPaths.cs`
- Create: `unity/SeedanceUiExample/TestFixtures/rig-tripo/`（拷 `~/Downloads/rig-2026-11-03-m1ctb3-fbx` 全部内容，去掉 `.DS_Store`）、`rig-tripo.zip`（同内容，zip 根直接是文件）
- Modify: `unity/SeedanceUiExample/run-tests.sh`、`.gitignore`、`tools/test_install_package.sh`

**Interfaces:**
- Produces:
  - `public class ModelImportException : Exception`（ctor `(string message)`）
  - `[Serializable] public class ModelImportRequest { public string source, importDir, prefabName; }`
  - `[Serializable] public class ModelImportReport { public string prefabPath, controllerPath, scenePath; public float heightMeters; public List<string> clips, createdStates, updatedStates, missingClips, removedBones, orphanTextures, warnings; }`（`clips` 项格式 `"idle (loop)"` / `"sit"`）
  - `ModelTestPaths.Fixtures`、`TripoDir`、`TripoZip`、`const string OutDir = "Assets/_SeedanceModelTestOut"`、`const string RecordOutDir = "Assets/SeedanceModel/_SeedanceModelTestOut"`
  - `run-tests.sh <filter>`：filter 以 `Seedance.` 开头时原样传，否则仍加 `Seedance.UiImport.Tests.` 前缀

- [ ] **Step 1: 写脚本测试**：在 `test_install_package.sh` 的 `check()` 里加：对同一工程先后跑两个 skill 的 `install_package.sh`，断言 `Assets/SeedanceUiImporter/` 与 `Assets/SeedanceModelImporter/` 都与各自源码 `diff -r` 一致；跑模型包 `uninstall_package.sh` 两次都退出 0，之后模型包目录与 meta 消失、UI 包目录仍在、`Assets/SeedanceModel/X.json` 仍在。
- [ ] **Step 2: 跑** `bash unity/SeedanceUiExample/tools/test_install_package.sh`。Expected: FAIL（模型包脚本不存在）。
- [ ] **Step 3: 实现**：两个脚本按 UI 包脚本改目录名（`SeedanceModelImporter`），**没有**旧 SDK 清理段；asmdef 内容同 UI 包（Editor 包不引用 UGUI/TMP，引用 `Unity.RenderPipelines.Universal.Runtime` 只在需要时由 Task 6 加）；`AssemblyInfo.cs` 对 `Seedance.ModelImport.Tests` 开 InternalsVisibleTo；`ModelImportTypes.cs` 写上面的类型；gen_meta 补 meta；测试 asmdef 仿 UI 测试 asmdef，引用 `Seedance.ModelImport.Editor`；`run-tests.sh` 同时推两包、改 filter 规则；`.gitignore` 加 `Assets/SeedanceModelImporter/`、`.meta`、`Assets/SeedanceModel/`、`.meta`；拷测试素材。
- [ ] **Step 4: 跑** `bash unity/SeedanceUiExample/tools/test_install_package.sh` → Expected: `ok with-rsync`、`ok without-rsync`；再关 Editor 跑 `bash unity/SeedanceUiExample/run-tests.sh` → Expected: 退出 0，UI 测试全过（两包共存能编译）。
- [ ] **Step 5: Commit** `feat: seedance-3dmodel-to-unity 包骨架、推包删包脚本与测试接线`

### Task 2: ModelSource（读产物）

**Files:** Create `Editor/ModelSource.cs`；Test `Assets/Tests/SeedanceModel/ModelSourceTests.cs`

**Interfaces:**
- Produces: `public sealed class ModelSource : IDisposable { public string FbxPath { get; } public IReadOnlyList<string> TexturePaths { get; } public static ModelSource Open(string path); }` —— zip 解到 `Temp/` 外的系统临时目录，Dispose 删；`TexturePaths` 为 FBX 同级 `Textures/` 下 `.png/.jpg/.jpeg/.tga`（按文件名排序），没有 `Textures/` 时为空列表。

- [ ] **Step 1: 写测试**：
  - `Open_Dir_FindsFbxAndTextures`：TripoDir → `FbxPath` 以 `rig-2026-11-03-m1ctb3.fbx` 结尾，`TexturePaths.Count == 3`
  - `Open_Zip_SameAsDir`：TripoZip → 同上，Dispose 后解压目录不存在
  - `Open_ZipWithWrapperDir_FindsFbx`：测试里现做一个 zip，内含 `wrap/x.fbx` + `wrap/Textures/a_BaseColor.png` → 找到
  - `Open_PathWithSpacesAndChinese`：把 TripoDir 拷到临时目录 `模型 产物/` 下 → 能打开
  - `Open_NoFbx_Throws` / `Open_TwoFbx_Throws`：`ModelImportException`，消息含“没有找到 .fbx” / “有多个 .fbx”
  - `Open_Missing_Throws`：消息含“不存在”
- [ ] **Step 2: 跑** `bash unity/SeedanceUiExample/run-tests.sh Seedance.ModelImport.Tests.ModelSourceTests` → Expected: 编译失败 / FAIL（ModelSource 未定义）
- [ ] **Step 3: 实现** `ModelSource`（解压用 `System.IO.Compression.ZipFile`；可参考 UI 包 `UiSource` 写法，复制不引用）；忽略 `__MACOSX/` 与 `.DS_Store`。
- [ ] **Step 4: 跑** 同 Step 2 → Expected: 全 PASS
- [ ] **Step 5: Commit** `feat: 模型产物读取（目录 / zip）`

### Task 3: 贴图用途识别与写入

**Files:** Create `Editor/TextureRoles.cs`、`Editor/TextureWriter.cs`；Test `TextureRolesTests.cs`、`TextureWriterTests.cs`

**Interfaces:**
- Produces:
  - `public enum TextureRole { Unknown, BaseColor, Normal, Metallic }`
  - `public static Dictionary<string, TextureRole> TextureRoles.Classify(IReadOnlyList<string> fileNames)` —— 规则见 Global Constraints / spec；同一用途多张时取第一张，其余标 Unknown
  - `public class TextureWriteResult { public Dictionary<TextureRole, Texture2D> byRole; public List<string> orphanTextures; public List<string> warnings; }`
  - `public static TextureWriteResult TextureWriter.Write(IReadOnlyList<string> sourcePaths, string importDir, string prefabName, IReadOnlyDictionary<TextureRole,string> previousGuids)` —— 写到 `<importDir>/Textures/<P>_<Role>.<ext>`；Normal 设 `TextureImporterType.NormalMap`；Metallic 设 sRGB 关；扩展名变了删旧的固定名文件（按 previousGuids 找）；本次没有的用途：旧文件进 `orphanTextures`（不删）；Unknown 原名拷入并 warning

- [ ] **Step 1: 写测试**：
  - `Classify_Tripo`：三个 Tripo 文件名 → BaseColor / Normal / Metallic
  - `Classify_SingleUnsuffixed_IsBaseColor`：`["texture_20250901.png"]` → BaseColor
  - `Classify_Unknown`：`["a_BaseColor.png","b_AO.png"]` → b 为 Unknown
  - `Write_Tripo_FixedNames`：Textures/ 下恰好 `T_BaseColor.jpg`、`T_Normal.jpg`、`T_Metallic.png`（或 Step 3 定下的转换后名字），Normal 的 importer 类型为 NormalMap
  - `Write_Again_NewUuid_NoExtraFiles`：同样三张改了 UUID 前缀再写一次 → 文件数不变，GUID 不变
  - `Write_ExtensionChanged_ReplacesFixedFile`：BaseColor 先 .jpg 后 .png → 只剩 `T_BaseColor.png`
  - `Write_RoleGone_ReportsOrphan`：先写三张，再只写一张无后缀 → `orphanTextures` 含 Normal、Metallic 路径，文件仍在
- [ ] **Step 2: 跑** `run-tests.sh Seedance.ModelImport.Tests.TextureRolesTests` 与 `...TextureWriterTests` → Expected: FAIL
- [ ] **Step 3: 先确认 Tripo 金属度贴图的通道**：用 `Texture2D` 读 `…_Metallic-tripo_.png`，统计 R/G/B/A 各通道均值与方差，写进 `TextureWriter` 顶部注释。若是 glTF 打包（G=粗糙度、B=金属度），`Write` 生成 `<P>_Metallic.png`：R=B 通道，A=1−G，原图不拷；若已是 R=金属度，原样拷。然后实现两个类。
- [ ] **Step 4: 跑** 同 Step 2 → Expected: PASS
- [ ] **Step 5: Commit** `feat: 贴图按用途固定命名写入`

### Task 4: 导入记录

**Files:** Create `Editor/ModelRecord.cs`、`Editor/ModelRecordStore.cs`；Test `ModelRecordStoreTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] public class ModelRecord { public int formatVersion = 1; public string prefabGuid, fbxGuid, source; public List<string> clips; public List<RoleGuid> textures; public List<string> materials; public List<string> bones; }`、`[Serializable] public class RoleGuid { public string role, guid; }`
  - `ModelRecordStore`：与 UI 包 `UiRecordStore` 同名同语义的静态方法 `Root = "Assets/SeedanceModel"`、`RecordPathFor`、`FindByGuid`、`Save`、`ReleaseMirrorPath`、`ValidateAll`、`EnsureWritable`；错误消息把“Seedance UI”换成“Seedance 模型”，记录目录换成 `Assets/SeedanceModel/`

- [ ] **Step 1: 写测试**：把 UI 测试 `UiRecordStoreTests` 复制改名为 `ModelRecordStoreTests`，类型换成 `ModelRecord`、路径换成本包常量（覆盖：镜像路径、按 GUID 找、损坏报错、重复 GUID 报错、原子写不留 .tmp、只读报错、大小写改名、ReleaseMirrorPath）。
- [ ] **Step 2: 跑** `run-tests.sh Seedance.ModelImport.Tests.ModelRecordStoreTests` → Expected: FAIL
- [ ] **Step 3: 实现**：复制 `UiRecordStore.cs` / `UiRecord.cs` 改名改类型（去掉 `RootParentId`、`DesignSize`）。
- [ ] **Step 4: 跑** → Expected: PASS
- [ ] **Step 5: Commit** `feat: 模型导入记录读写`

### Task 5: FBX 写入与 ModelImporter 设置

**Files:** Create `Editor/FbxWriter.cs`；Test `FbxWriterTests.cs`

**Interfaces:**
- Produces:
  - `public class FbxWriteResult { public string fbxPath; public List<string> clipNames; public List<string> bones; public float heightMeters; public List<string> warnings; }`
  - `public static FbxWriteResult FbxWriter.Write(string sourceFbx, string importDir, string prefabName, bool firstImport)` —— 拷到 `<importDir>/<P>.fbx`（覆盖时 .meta 不动）；设 Generic、`avatarSetup = CreateFromThisModel`、Bake Axis Conversion、`materialImportMode = None`（材质由 Task 6 remap）；clip：首次按 `importedTakeInfos` 建 `clipAnimations`（去前缀、循环规则、`lockRootPositionXZ/lockRootRotation = true`、`keepOriginalPositionY = true`），非首次按名字保留已有 `ModelImporterClipAnimation` 整份只更新 `firstFrame/lastFrame`，新 take 按首次规则加，消失的 take 去掉；`SaveAndReimport`；高度 = 实例化后所有 Renderer 包围盒合并的 `size.y`；骨骼 = `SkinnedMeshRenderer.bones` 名字去重排序
  - `public static void FbxWriter.RemapMaterials(string fbxPath, IReadOnlyDictionary<string, Material> bySlot)`、`public static IReadOnlyList<string> FbxWriter.MaterialSlots(string fbxPath)`

- [ ] **Step 1: 写测试**（Tripo 素材）：
  - `First_ClipsNamedAndLooped`：`clipNames == [idle, sit, walk]`（按 FBX 里的顺序断言集合相等即可）；idle、walk `loopTime`，sit 否
  - `First_RootMotionInPlace`：三段 clip 的 `lockRootPositionXZ`、`lockRootRotation`、`keepOriginalPositionY` 都为 true
  - `First_GenericAndUpright`：`animationType == Generic`；高度在 0.5 ~ 3 m；实例的包围盒 `size.y` 是三轴最大值或接近（站立，不是躺倒）——用两份样例实测后据实写死阈值
  - `Reimport_KeepsClipCustomizations`：首次后把 sit 改成循环、给 idle 加一个 `AnimationEvent` 写回 importer，再 `Write(firstImport:false)` → sit 仍循环、idle 事件还在
  - `Reimport_GuidStable`：重写前后 FBX GUID 相同
  - `Bones_Listed`：`bones` 含 `Root`、`Spine01`
- [ ] **Step 2: 跑** `run-tests.sh Seedance.ModelImport.Tests.FbxWriterTests` → Expected: FAIL
- [ ] **Step 3: 实现**；轴向 / 单位组合用 Tripo 与混元原件各导一次实测（混元原件不进仓库，手动拷进测试工程试完删掉），结论写进 `FbxWriter` 注释。
- [ ] **Step 4: 跑** → Expected: PASS
- [ ] **Step 5: Commit** `feat: FBX 导入设置（Generic、动画切片、原地播、高度）`

### Task 6: 材质

**Files:** Create `Editor/MaterialBuilder.cs`；Modify `Editor/Seedance.ModelImport.Editor.asmdef`（如需引用 URP）；Test `MaterialBuilderTests.cs`

**Interfaces:**
- Consumes: `TextureWriteResult.byRole`（Task 3）、`ModelRecord.textures`（Task 4）、`FbxWriter.MaterialSlots/RemapMaterials`（Task 5）
- Produces: `public static Dictionary<string, Material> MaterialBuilder.BuildOrUpdate(string importDir, string prefabName, IReadOnlyList<string> slots, IReadOnlyDictionary<TextureRole, Texture2D> byRole, IReadOnlyDictionary<TextureRole, string> recordedGuids, List<string> warnings)` —— URP Lit（`Shader.Find("Universal Render Pipeline/Lit")` 为空 → `ModelImportException("工程没有装 URP …")`）；首次新建；已存在只改 `_BaseMap` / `_BumpMap`（并开 `_NORMALMAP` 关键字）/ `_MetallicGlossMap`（并开 `_METALLICSPECGLOSSMAP`）；本次没有的用途：槽位当前贴图 GUID == recordedGuids 中该用途 → 清空，否则不碰；多槽 warning

- [ ] **Step 1: 写测试**：
  - `First_LitWithThreeMaps`
  - `Update_KeepsColorAndSmoothness`：首次后改 `_BaseColor` 与 `_Smoothness`，再 BuildOrUpdate → 两值不变
  - `Update_RoleGone_ClearsOnlyRecordedSlot`：首次三张；程序把 `_BumpMap` 换成另一张贴图；再只给 BaseColor → `_BumpMap` 仍是程序那张，`_MetallicGlossMap` 为空
  - `NoUrp_Throws`：用测试钩子 `internal static Func<string, Shader> FindShader` 返回 null → 抛错且没有新建 .mat
- [ ] **Step 2: 跑** `run-tests.sh Seedance.ModelImport.Tests.MaterialBuilderTests` → Expected: FAIL
- [ ] **Step 3: 实现**
- [ ] **Step 4: 跑** → Expected: PASS
- [ ] **Step 5: Commit** `feat: URP 材质建立与重导更新`

### Task 7: AnimatorController

**Files:** Create `Editor/ControllerBuilder.cs`；Test `ControllerBuilderTests.cs`

**Interfaces:**
- Produces: `public static AnimatorController ControllerBuilder.BuildOrUpdate(string controllerPath, IReadOnlyList<AnimationClip> clips, IReadOnlyList<string> recordedClips, ModelImportReport report)` —— 首次：Base Layer 每 clip 一个状态，默认 idle 否则第一个；之后：按状态名对应，记录里有的状态换 motion（`updatedStates`），新 clip 加状态（`createdStates`），记录里有而本次没有的 clip：状态留着、motion 置 null、进 `missingClips`；不在记录里的状态、切换线、参数、其他 layer、默认状态都不碰

- [ ] **Step 1: 写测试**（clips 用测试里 `new AnimationClip()` 存成 .anim 资产即可，不依赖 FBX）：
  - `First_StatesAndDefault`：`[walk, idle, sit]` → 3 个状态，默认 idle，无 transition、无参数
  - `First_NoIdle_DefaultFirst`
  - `Update_KeepsUserTransitionsParamsStates`：首次后加参数 `Speed`、idle→walk 切换线、自建状态 `Attack`、第二层；再 Update → 全在
  - `Update_NewClip_AddsState` / `Update_ClipGone_KeepsStateNullMotion`：report 字段正确
  - `Update_ReplacesMotion`：同名新 clip 对象 → 状态 motion 指向新的
- [ ] **Step 2: 跑** `run-tests.sh Seedance.ModelImport.Tests.ControllerBuilderTests` → Expected: FAIL
- [ ] **Step 3: 实现**
- [ ] **Step 4: 跑** → Expected: PASS
- [ ] **Step 5: Commit** `feat: AnimatorController 生成与重导合并`

### Task 8: Prefab 与导入总流程

**Files:** Create `Editor/ModelPrefabBuilder.cs`、`Editor/SeedanceModelImporter.cs`；Test `ModelImporterTests.cs`、`ModelReimportTests.cs`

**Interfaces:**
- Consumes: Task 2–7 全部
- Produces:
  - `public static GameObject ModelPrefabBuilder.BuildOrUpdate(string prefabPath, GameObject fbxModel, AnimatorController controller, Avatar avatar)` —— 首次：根空物体 + 子节点 `<P>_Model`（FBX 的嵌套 Prefab 实例）；Animator 挂哪一层按实测（spec“留给建造者”），`applyRootMotion = false`；非首次：只在缺 Animator 时补，确认 controller / avatar 指向正确，其余不碰
  - `public static ModelImportReport SeedanceModelImporter.Run(ModelImportRequest req)`；`internal static Func<bool> IsPlaying`（测试钩子，同 UI 包）
  - 顺序：Play Mode 检查 → 参数校验 → `ModelRecordStore.ValidateAll` + Prefab 在则必须有记录且可写 → `ModelSource.Open` → 贴图 → FBX → 材质 + remap → Controller → Prefab → 记录（`ReleaseMirrorPath` / `Save`）→ ViewerScene（Task 9 前先留调用点，Task 9 接上）→ `SaveAssets`；`removedBones` = 记录 bones − 本次 bones；高度 warning

- [ ] **Step 1: 写测试**：
  - `Run_Tripo_FirstImport`：目录结构符合 Global Constraints；Prefab 根上 Animator 的 controller 为 `<P>.controller`、avatar 非空、`applyRootMotion == false`；Prefab 上没有 `Seedance.` 开头的组件类型；记录存在且 `clips` 3 个；`report.heightMeters > 0`
  - `Run_PlayMode_Throws_WritesNothing`、`Run_BadPrefabName_Throws_WritesNothing`、`Run_PrefabWithoutRecord_Throws_WritesNothing`、`Run_BrokenRecordElsewhere_Throws_WritesNothing`：`OutDir` 下无新文件
  - `Reimport_KeepsBoneAttachmentAndClipEvents`：首次后在 Prefab 根加 `BoxCollider`、在骨骼 `Spine01` 下加子物体 `Weapon`、给 idle clip 加事件；同产物再导 → 三者都在；Prefab 内 SkinnedMeshRenderer 数与首次相同（没有第二份模型）
  - `Reimport_DifferentSource_ReportsAndKeepsRootComponents`：首次 Tripo；改记录里 `bones` 加一个 `OldBone`、`clips` 加一个 `jump` 后（模拟上游变化），再用“Tripo FBX + 只有一张无后缀贴图”的构造产物重导 → `removedBones` 含 `OldBone`、`missingClips` 含 `jump`、材质法线槽为空、根上 `BoxCollider` 仍在
- [ ] **Step 2: 跑** `run-tests.sh Seedance.ModelImport.Tests.ModelImporterTests` 与 `...ModelReimportTests` → Expected: FAIL
- [ ] **Step 3: 实现**
- [ ] **Step 4: 跑** → Expected: PASS；再跑全量 `run-tests.sh` → Expected: 退出 0
- [ ] **Step 5: Commit** `feat: 模型 Prefab 组装与导入总流程`

### Task 9: 3DViewer 场景、截图、CLI 命令

**Files:** Create `Editor/ViewerScene.cs`、`Editor/ModelCapture.cs`、`Editor.Cli/SeedanceModelImportCommand.cs`；Modify `Editor/SeedanceModelImporter.cs`（接上 ViewerScene）；Test `ViewerSceneTests.cs`、`ModelCaptureTests.cs`

**Interfaces:**
- Produces:
  - `public static class ViewerScene { public const string ScenePath = "Assets/Product/Scene/3DViewer.unity"; public static void Show(GameObject prefabAsset, ModelImportReport report); }` —— 首次建场景：Plane 地面、Directional Light、Main Camera；放实例到原点（已有同 Prefab 实例则复用），其他“源 Prefab 有记录”的实例设 inactive；相机按当前实例包围盒取景；Additive 打开 / 保存 / 关闭
  - `public static void ModelCapture.Capture(GameObject prefabAsset, int width, int height, string pngPath)` —— 离屏实例化、采样默认状态 clip 第 0 帧（`AnimationClip.SampleAnimation`）、正面偏 20° 取景、深灰底
  - CLI：`model_import --request <json>` → report JSON；`model_capture --prefab <path> --out <png>` → PNG 绝对路径（1280×720）

- [ ] **Step 1: 写测试**（仿 UI 的 `ShowcaseSceneTests` / `PrefabCaptureTests`）：
  - `Show_CreatesSceneWithGroundLightCamera`、`Show_TwoModels_OnlyLatestActive`、`Show_SamePrefabTwice_OneInstance`、`Show_IgnoresManualObject`、`Show_DoesNotTouchUserScene`
  - `Capture_WritesPng_NotAllBackground`：输出 PNG 存在，且中心区域像素不全是背景色
- [ ] **Step 2: 跑** `run-tests.sh Seedance.ModelImport.Tests.ViewerSceneTests` 与 `...ModelCaptureTests` → Expected: FAIL
- [ ] **Step 3: 实现**（CLI 文件照抄 UI 的 `SeedanceUiImportCommand` 结构）
- [ ] **Step 4: 跑** → Expected: PASS；全量 `run-tests.sh` → Expected: 退出 0
- [ ] **Step 5: Commit** `feat: 3DViewer 展示场景、离屏截图与 unity 命令`

### Task 10: SKILL.md、文档、插件清单、端到端

**Files:** Create `.claude/skills/seedance-3dmodel-to-unity/SKILL.md`；Modify `.claude-plugin/marketplace.json`（新插件 1.0.0，keywords `unity` `fbx` `animator` `prefab` `tripo` `hunyuan` `seedance` `unity-cli`）、`docs/engine/unity.md`（新增一节：路径表、约定、测试命令）、`README.md`（如有 skill 列表则加一行）

- [ ] **Step 1: 写 SKILL.md**：结构照 UI skill（Overview / 参数 / 步骤 1–8：前置检查、推包、Editor、导入、截图、删包、汇报 / Report 字段表 / 出错时 / Common Mistakes）；description 触发词：导入模型、FBX 进 Unity、Tripo、混元、绑骨模型、做成角色 Prefab、Animator、重导模型。不含 TMP / 字体步骤。
- [ ] **Step 2: 端到端**（Ruling 同 UI：在测试工程的干净副本上，副本去掉 `Assets/Tests/`，以便验证删包后无编译错误）：
  - 用 Tripo zip 首次导入到 `Assets/Characters/Knight` → `model_capture` 截图并 Read 查看：站立、有贴图
  - 在 Editor 里给 Prefab 根加 BoxCollider（`unity command add_component` 等），再用混元原件目录重导到同一位置 → report 中 `missingClips` 含 sit、`removedBones` 非空；BoxCollider 仍在
  - 删包 + `unity recompile` → `unity list` 无 seedance 命令，无编译错误，`grep -c Seedance` Prefab / Controller 为 0
  - 打开 3DViewer 看场景存在（不进 Play）
- [ ] **Step 3: 跑** 全量 `bash unity/SeedanceUiExample/run-tests.sh` 与 `bash unity/SeedanceUiExample/tools/test_install_package.sh` → Expected: 都退出 0
- [ ] **Step 4: Commit** `docs: seedance-3dmodel-to-unity 的 SKILL.md、文档与插件清单`
