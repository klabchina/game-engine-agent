# Seedance ui-maker 产物 → Unity Prefab 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 一个 Claude skill：给它 ui-maker 产物（`out/` 或 `ui.zip`）+ Unity 工程 + 导入目录，它驱动运行中的 Editor 把切图导入、搭成 UGUI Prefab、放进展示场景；重导只刷新 Sprite / 位置 / 尺寸。

**Architecture:** 导入逻辑是一份 Unity Editor C# 包 `SeedanceUI`（Runtime 标记组件 + Editor 导入器 + 一个 `[CliCommand]` 入口），源码放在 skill 目录里，由脚本拷进目标工程的 `Assets/SeedanceUI/`。skill 的 `SKILL.md` 负责前置检查、拷包、取字体、调用 `unity command seedance_ui_import`、截图对照。开发和测试用本仓库里的一个 Unity 工程 `unity/SeedanceUiDev`，EditMode 测试跑 `unity test`。

**Tech Stack:** Unity 6000.0.62f1、UGUI（`com.unity.ugui` 2.x，内含 TextMeshPro）、Unity Test Framework（NUnit）、Unity CLI 1.0.0-beta.13 + `com.unity.pipeline`、bash。

**Spec:** `docs/superpowers/specs/2026-10-21-seedance-ui-to-unity-design.md`（执行者先读它）

## Global Constraints

- 只支持 Unity 6000.x（pipeline 包要求 Unity 6）；开发工程用 6000.0.62f1。
- 工程里新产生的资源名、节点名一律 ASCII：Sprite `<type>_<sameAs ?? id>.png`（type 取被引用的那个元素的 type），节点 `<type>_<id>`，Prefab `<Prefab 名>.prefab`（`^[A-Za-z0-9_-]+$`），字体 `<ASCII 化文件名>_SDF.asset`，展示场景 `Assets/Product/Scene/UIShowcase.unity`。
- 目录：`<导入目录>/<Prefab 名>.prefab`、`<导入目录>/Sprites/`、`<导入目录>/Fonts/`；导入目录必须以 `Assets/` 开头。
- Prefab 根节点：普通 RectTransform，四边拉伸（anchorMin (0,0)、anchorMax (1,1)、offset 0），**不带** Canvas / CanvasScaler，**不带背景**（忽略 background.png）。
- 子节点 anchorMin = anchorMax = pivot = (0,1)；`sizeDelta = (w,h)`；`anchoredPosition = (x - 父.x, -(y - 父.y))`；父为空时父 rect 视为 (0,0)。出图元素用 `rect`，`missing` 元素用 `drawnRect`。
- 重导只更新 **Sprite、位置、尺寸**；文字、父节点、sibling 顺序、组件只在首次建节点时设置。产物里已没有的元素设 inactive、不删；没有 `SeedanceUiElement` 的节点完全不碰；type 变了只报告。
- 资源按固定路径覆盖写入，不删再建（保住 GUID）；不再用到的 Sprite 不删，只报告。
- 不手改 `.prefab` / `.meta` 文本，一律走 AssetDatabase / PrefabUtility。
- 绝不在测试或 skill 里调用任何生成服务。

## Review Focus

1. **程序改过的 Prefab 重导**：节点被挪进别的容器、文字被改、手加了组件 —— 期望全部保留，被挪走的节点不更新位置并报告。Task 8 的 `Update_MovedNode_KeepsParentAndReports`、`Update_KeepsManualTextAndComponents`。
2. **只开新 Input System 的工程**：展示场景的 EventSystem 用了 `StandaloneInputModule` 会每帧报错。期望按工程设置选模块。Task 9 的 `EventSystemModule_FollowsInputHandling`。
3. **竖屏 1440×2560 产物**：现有真实产物全是横屏。期望 CanvasScaler Match = 1，坐标照样对齐。Task 3 的 `Match_PortraitIsOne`、Task 9 的 `Create_PortraitScene`。
4. **上游改了名 / 同款主元素换了**：Sprite 文件名随 id 变化，旧 Sprite 留在工程里。期望不删、报告里列出。Task 5 的 `Write_ReportsOrphansWithoutDeleting`。
5. **用户 Editor 里开着一个没保存的场景**：导入不能把它关掉或丢改动。期望展示场景以 Additive 打开、改完保存后关掉，用户当前场景原样。Task 9 的 `Show_DoesNotTouchUserScene`。

---

## 文件结构

```
.claude/skills/seedance-ui-to-unity/
  SKILL.md                                   # skill 入口（Task 11）
  scripts/install_package.sh                 # 把 SeedanceUI/ 拷进工程 Assets/SeedanceUI/（Task 1）
  scripts/fetch_font.sh                      # 下载默认中文字体到缓存（Task 6）
  SeedanceUI/                                # 拷进目标工程的包源码（含手写 .meta，GUID 固定）
    Runtime/Seedance.UiImport.Runtime.asmdef
    Runtime/SeedanceUiElement.cs             # 节点标记组件
    Runtime/SeedanceUiRoot.cs                # 根节点来源信息
    Editor/Seedance.UiImport.Editor.asmdef
    Editor/LayoutModel.cs                    # layout.json DTO + 解析（Task 2）
    Editor/UiNaming.cs                       # ASCII 命名（Task 3）
    Editor/UiGeometry.cs                     # 坐标换算（Task 3）
    Editor/UiSource.cs                       # 打开 out/ 或 ui.zip（Task 4）
    Editor/SpriteAssetWriter.cs              # 切图落盘 + 导入设置（Task 5）
    Editor/FontResolver.cs                   # TMP 字体（Task 6）
    Editor/PrefabBuilder.cs                  # 新建 / 更新 Prefab（Task 7、8）
    Editor/ShowcaseScene.cs                  # 展示场景（Task 9）
    Editor/ImportTypes.cs                    # ImportRequest / ImportReport / UiImportException（Task 2 建，后续追加字段）
    Editor/SeedanceUiImporter.cs             # 编排（Task 10）
    Editor.Cli/Seedance.UiImport.Editor.Cli.asmdef   # 引用 Unity.Pipeline
    Editor.Cli/SeedanceUiImportCommand.cs    # [CliCommand("seedance_ui_import")]（Task 10）
unity/SeedanceUiDev/                         # 开发 / 测试用 Unity 工程（入库，Library 等忽略）
  run-tests.sh                               # 拷包 + unity test（Task 1）
  TestFixtures/char-panel/                   # 真实产物 uim-mvaex9q1 的 layout.json + slices/
  TestFixtures/char-panel.zip                # 同上打包
  Assets/Tests/SeedanceUI/Seedance.UiImport.Tests.asmdef + *Tests.cs
```

命名空间：Runtime `Seedance.UiImport`，Editor `Seedance.UiImport.Editor`，测试 `Seedance.UiImport.Tests`。

**跑测试**（每个 Task 都用）：先关掉 `unity/SeedanceUiDev` 的 Editor（`unity test` 走 batch mode，工程被打开时会锁住），然后
`bash unity/SeedanceUiDev/run-tests.sh <测试类名>` —— 退出码 0 = 全过，8 = 有失败，6 = 没跑出结果（编译错误等，看 `unity/SeedanceUiDev/Logs/`）。

---

### Task 1: 开发工程、包骨架、拷包与跑测脚本

**Files:**
- Create: `unity/SeedanceUiDev/`（Unity 工程）、`unity/SeedanceUiDev/.gitignore`、`unity/SeedanceUiDev/run-tests.sh`
- Create: `.claude/skills/seedance-ui-to-unity/scripts/install_package.sh`
- Create: `.claude/skills/seedance-ui-to-unity/SeedanceUI/Runtime/{Seedance.UiImport.Runtime.asmdef,SeedanceUiElement.cs,SeedanceUiRoot.cs}` 及各自 `.meta`、目录 `.meta`
- Create: `.claude/skills/seedance-ui-to-unity/SeedanceUI/Editor/Seedance.UiImport.Editor.asmdef`（+ `.meta`）
- Create: `unity/SeedanceUiDev/TestFixtures/char-panel/`、`char-panel.zip`
- Test: `unity/SeedanceUiDev/Assets/Tests/SeedanceUI/{Seedance.UiImport.Tests.asmdef,RuntimeComponentTests.cs}`

**Interfaces:**
- Produces:
  - `public class SeedanceUiElement : MonoBehaviour { public string elementId; public string sourceName; public string type; }`
  - `public class SeedanceUiRoot : MonoBehaviour { public int designWidth; public int designHeight; public int layoutVersion; }`
  - `install_package.sh <unity 工程路径>`：`rsync -a --delete` 把 `SeedanceUI/` 同步到 `<工程>/Assets/SeedanceUI/`；工程路径不存在或没有 `Assets/` 时退出码 1。
  - `run-tests.sh [filter]`：调 install_package.sh，再 `unity test "$DIR" --mode EditMode --output "$DIR/test-results.xml" ${filter:+--filter "Seedance.UiImport.Tests.$filter"}`，原样传回退出码。

- [ ] **Step 1: 建工程**

```bash
unity projects new SeedanceUiDev --path unity --editor-version 6000.0.62f1 --template com.unity.template.urp-blank
unity pipeline install --project-path unity/SeedanceUiDev
```
确认 `unity/SeedanceUiDev/Packages/manifest.json` 里有 `com.unity.ugui`、`com.unity.test-framework`、`com.unity.pipeline`、`com.unity.inputsystem`。删掉 `unity/.gitkeep`。

- [ ] **Step 2: `.gitignore`**：`Library/ Temp/ Logs/ obj/ UserSettings/ Build/ *.csproj *.sln test-results.xml`，以及 `Assets/SeedanceUI/`、`Assets/SeedanceUI.meta`（由 install 脚本生成，源在 skill 目录）。

- [ ] **Step 3: 包骨架**
  - Runtime asmdef 名 `Seedance.UiImport.Runtime`，无引用。
  - Editor asmdef 名 `Seedance.UiImport.Editor`，`includePlatforms: ["Editor"]`，引用 `Seedance.UiImport.Runtime`、`UnityEngine.UI`、`Unity.TextMeshPro`、`Unity.TextMeshPro.Editor`。
  - 每个 `.cs` / `.asmdef` / 目录手写 `.meta`，GUID 用 `python3 -c "import uuid;print(uuid.uuid4().hex)"` 生成一次后固定。`.cs` 用 MonoImporter 格式（`fileFormatVersion: 2`、`guid`、`MonoImporter: {externalObjects: {}, serializedVersion: 2, defaultReferences: [], executionOrder: 0, icon: {instanceID: 0}, userData: , assetBundleName: , assetBundleVariant: }`），asmdef 用 `AssemblyDefinitionImporter`，目录用 `folderAsset: yes` + `DefaultImporter`。**`SeedanceUiElement.cs` / `SeedanceUiRoot.cs` 的 GUID 一经提交永不再改**——Prefab 靠它找脚本。

- [ ] **Step 4: 测试 asmdef 与夹具**
  - `Seedance.UiImport.Tests`：Editor only，`defineConstraints: ["UNITY_INCLUDE_TESTS"]`，引用 Runtime、Editor、`UnityEngine.UI`、`Unity.TextMeshPro`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner`，`overrideReferences: true` + `precompiledReferences: ["nunit.framework.dll"]`。
  - 从 `/Users/li-j/project/agent-ai/art-seedance-frameanimation/data/ui-maker-tasks/uim-mvaex9q1-938c26/out/` 拷 `layout.json` 和 `slices/` 到 `TestFixtures/char-panel/`（不进 `Assets/`，Unity 不导入）；`cd TestFixtures/char-panel && zip -r ../char-panel.zip layout.json slices`。

- [ ] **Step 5: 写失败测试** `RuntimeComponentTests.AddMarker_KeepsFields`

```csharp
var go = new GameObject("n");
var m = go.AddComponent<SeedanceUiElement>();
m.elementId = "e3"; m.sourceName = "角色属性"; m.type = "text";
Assert.AreEqual("e3", go.GetComponent<SeedanceUiElement>().elementId);
Object.DestroyImmediate(go);
```

- [ ] **Step 6: 跑** `bash unity/SeedanceUiDev/run-tests.sh RuntimeComponentTests` → 组件还没写时退出码 6（编译失败）。

- [ ] **Step 7: 写两个 MonoBehaviour**（`[DisallowMultipleComponent]`，字段即 Interfaces 所列）。

- [ ] **Step 8: 跑** 同上 → 退出码 0。再确认 GUID 没被 Unity 改：`grep guid .claude/skills/seedance-ui-to-unity/SeedanceUI/Runtime/SeedanceUiElement.cs.meta unity/SeedanceUiDev/Assets/SeedanceUI/Runtime/SeedanceUiElement.cs.meta` 两行相同。

- [ ] **Step 9: Commit** `feat: 搭建 SeedanceUI 包骨架与开发工程`

---

### Task 2: layout.json 模型与解析

**Files:**
- Create: `SeedanceUI/Editor/LayoutModel.cs`、`SeedanceUI/Editor/ImportTypes.cs`
- Test: `Assets/Tests/SeedanceUI/LayoutParserTests.cs`

**Interfaces:**
- Produces（`Seedance.UiImport.Editor`，全部 `[Serializable]`，公开字段，供 JsonUtility）：
  - `class UiRect { int x, y, w, h; }`、`class UiResolution { int width, height; }`
  - `class UiText { string content; float fontSize; string color; string align; bool bold; }`
  - `class UiLayoutElement { string id, name, type, image, parent, sameAs; UiRect rect, drawnRect; int order; bool missing; UiText text; bool IsText => type == "text"; UiRect EffectiveRect => missing ? drawnRect : rect; }`
  - `class UiLayoutDoc { int version; UiResolution resolution; List<UiLayoutElement> elements; UiLayoutElement Find(string id); }`
  - `static class UiLayoutParser { static UiLayoutDoc Parse(string json); }`
  - `class UiImportException : Exception`（ImportTypes.cs）

JsonUtility 会把 `null` 对象填成默认实例、`null` 字符串可能变成 `""`，所以 `Parse` 解析后统一归一化：空字符串的 `parent` / `sameAs` / `image` → `null`；非 text 元素 `text = null`；`missing` 元素 `rect = null`；text 元素 `drawnRect = rect`。

- [ ] **Step 1: 写失败测试**（夹具读 `Path.Combine(Application.dataPath, "../TestFixtures/char-panel/layout.json")`）
  - `Parse_RealLayout`：`version == 1`；`resolution` 2560×1440；65 个元素；`e1.type == "panel"`、`e1.rect` = (254,143,2056,1156)、`e1.parent == null`；`e14.sameAs == "e11"`、`e14.image == "slices/属性条_1.png"`；`e3.IsText`、`e3.text.content == "角色属性"`、`fontSize == 64`、`color == "#ffe9b0"`、`align == "center"`、`bold`；`e1.text == null`。
  - `Parse_MissingElement`：内联 JSON 一个 `{"id":"e1","name":"a","type":"icon","missing":true,"rect":null,"drawnRect":{"x":1,"y":2,"w":3,"h":4},"parent":null,"order":0,"sameAs":null}` → `rect == null`、`EffectiveRect.x == 1`。
  - `Parse_Rejects`：`version: 2`、缺 `resolution`、重复 id、`parent` 指向不存在的 id，各自抛 `UiImportException`，消息里带出问题字段 / id。

- [ ] **Step 2: 跑** `run-tests.sh LayoutParserTests` → 退出码 6（类型未定义）。
- [ ] **Step 3: 实现** `LayoutModel.cs` / `ImportTypes.cs`（此 Task 只放 `UiImportException`）。
- [ ] **Step 4: 跑** → 0。
- [ ] **Step 5: Commit** `feat: 解析 ui-maker layout.json`

---

### Task 3: ASCII 命名与坐标换算

**Files:**
- Create: `SeedanceUI/Editor/UiNaming.cs`、`SeedanceUI/Editor/UiGeometry.cs`
- Test: `Assets/Tests/SeedanceUI/UiNamingTests.cs`、`UiGeometryTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `UiLayoutDoc` / `UiLayoutElement` / `UiRect`。
- Produces:
  - `static class UiNaming { string SpriteFileName(UiLayoutDoc doc, UiLayoutElement e); string NodeName(UiLayoutElement e); bool IsValidPrefabName(string s); string FontAssetName(string fontFilePath); string ToAscii(string s); }`
  - `struct LocalPlacement { Vector2 anchoredPosition; Vector2 size; }`
  - `static class UiGeometry { LocalPlacement Place(UiRect self, UiRect parentOrNull); void Apply(RectTransform rt, LocalPlacement p); void Stretch(RectTransform rt); float MatchFor(int width, int height); }`

规则：`SpriteFileName` = `$"{master.type}_{master.id}.png"`，`master = e.sameAs != null ? doc.Find(e.sameAs) : e`。`ToAscii` 把 `[^A-Za-z0-9_-]` 换成 `_`、连续 `_` 合并、去首尾 `_`，结果为空时返回 `"Font"`。`FontAssetName` = `ToAscii(无扩展名的文件名) + "_SDF"`。`MatchFor`：宽 ≥ 高返回 0，否则 1。`Apply` 设 anchorMin = anchorMax = pivot = (0,1)、sizeDelta、anchoredPosition。`Stretch` 设 anchorMin (0,0)、anchorMax (1,1)、offsetMin = offsetMax = 0。

- [ ] **Step 1: 写失败测试**
  - `SpriteFileName_UsesMaster`（真实夹具）：`e1 → "panel_e1.png"`，`e14 → "bar_e11.png"`，`e26 → "panel_e8.png"`，`e27 → "tab_e9.png"`。
  - `NodeName`：`e3 → "text_e3"`。
  - `IsValidPrefabName`：`"CharacterPanel"`、`"ui_01-a"` 为真；`"角色"`、`"a b"`、`""`、`"a/b"` 为假。
  - `FontAssetName`：`"/x/NotoSansCJKsc-Regular.otf" → "NotoSansCJKsc-Regular_SDF"`，`"/x/思源 黑体.ttf" → "Font_SDF"`。
  - `Place_RootChild`：self (254,143,2056,1156)、parent null → pos (254,-143)、size (2056,1156)。
  - `Place_Nested`：self (2213,134,105,106)、parent (254,143,2056,1156) → pos (1959, 9)。
  - `Match_LandscapeIsZero`：2560×1440 → 0；`Match_PortraitIsOne`：1440×2560 → 1。
  - `Apply_SetsTopLeftAnchors`：Apply 后 `anchorMin == anchorMax == pivot == (0,1)`。
- [ ] **Step 2: 跑** `run-tests.sh UiNamingTests` 与 `UiGeometryTests` → 6。
- [ ] **Step 3: 实现。**
- [ ] **Step 4: 跑** → 0。
- [ ] **Step 5: Commit** `feat: ASCII 命名与 RectTransform 坐标换算`

---

### Task 4: 打开产物（目录或 zip）

**Files:**
- Create: `SeedanceUI/Editor/UiSource.cs`
- Test: `Assets/Tests/SeedanceUI/UiSourceTests.cs`

**Interfaces:**
- Produces: `sealed class UiSource : IDisposable { string RootDir { get; } string LayoutJsonPath { get; } static UiSource Open(string path); }`

`Open`：目录 → `RootDir = path`；`.zip` → 用 `System.IO.Compression.ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8)` 解到 `<工程>/Temp/SeedanceUiImport/<Guid>/`，`Dispose` 时删掉（目录来源不删）。根下没有 `layout.json`、路径不存在、不是目录也不是 `.zip`，都抛 `UiImportException`。解压时拒绝含 `..` 或绝对路径的条目（抛异常）。

- [ ] **Step 1: 写失败测试**
  - `Open_Directory`：`LayoutJsonPath` 存在，`RootDir/slices/属性条_1.png` 存在。
  - `Open_Zip_SameFilesAsDirectory`：zip 与目录列出的 `slices/` 文件名集合相同（中文名正确解码）；`Dispose` 后 `RootDir` 不存在。
  - `Open_RejectsMissingLayout`：空的临时目录 → 抛 `UiImportException`。
- [ ] **Step 2: 跑** `run-tests.sh UiSourceTests` → 6。
- [ ] **Step 3: 实现。**
- [ ] **Step 4: 跑** → 0。
- [ ] **Step 5: Commit** `feat: 支持从 out 目录或 ui.zip 读取产物`

---

### Task 5: 切图落盘与 Sprite 导入

**Files:**
- Create: `SeedanceUI/Editor/SpriteAssetWriter.cs`
- Modify: `SeedanceUI/Editor/ImportTypes.cs`（加 `SpriteWriteResult`）
- Test: `Assets/Tests/SeedanceUI/SpriteAssetWriterTests.cs`

**Interfaces:**
- Consumes: `UiLayoutDoc`、`UiNaming.SpriteFileName`、`UiSource.RootDir`。
- Produces:
  - `class SpriteWriteResult { Dictionary<string, Sprite> byElementId; List<string> orphanSprites; List<string> warnings; }`（`orphanSprites` 是资源路径）
  - `static class SpriteAssetWriter { SpriteWriteResult Write(UiLayoutDoc doc, string sourceRoot, string importDir); }`

做法：对每个非 text、非 missing 的元素，`dst = <importDir>/Sprites/<SpriteFileName>`。同一个 dst 只写一次（`File.Copy(src, dst, overwrite: true)`，不先删）；两个元素映射到同一个 dst、但 `image` 不同时记一条 warning，用先写的那张。写完 `AssetDatabase.ImportAsset`，再设 TextureImporter：`textureType = Sprite`、`spriteImportMode = Single`、`mipmapEnabled = false`、`alphaIsTransparency = true`、`wrapMode = Clamp`、`maxTextureSize = 最长边向上取 2 的幂，夹在 [32, 16384]`，`SaveAndReimport`。`orphanSprites` = `Sprites/` 下本次没写到的 `.png`，**不删**。

- [ ] **Step 1: 写失败测试**（`importDir = "Assets/_SeedanceUiTestOut"`，`[TearDown]` 用 `AssetDatabase.DeleteAsset` 清掉）
  - `Write_NamesAndShares`：`Sprites/` 下正好 8 个文件：`panel_e1 button_e2 frame_e4 button_e6 panel_e8 tab_e9 bar_e11 button_e62`（`.png`）；`byElementId["e14"] == byElementId["e11"]`；text 元素不在字典里。
  - `Write_ImporterSettings`：`panel_e1.png` 的 importer 是 Sprite / Single / 无 mipmap / alphaIsTransparency；`maxTextureSize == 4096`（面板 2056 宽）。
  - `Write_Twice_KeepsGuid`：写两次，`AssetDatabase.AssetPathToGUID` 不变。
  - `Write_ReportsOrphansWithoutDeleting`：先在 `Sprites/` 放一张 `panel_e99.png`，Write 后它仍存在，并出现在 `orphanSprites`。
- [ ] **Step 2: 跑** `run-tests.sh SpriteAssetWriterTests` → 6。
- [ ] **Step 3: 实现。**
- [ ] **Step 4: 跑** → 0。
- [ ] **Step 5: Commit** `feat: 切图按 ASCII 名导入为 Sprite`

---

### Task 6: TMP 字体与默认字体下载

**Files:**
- Create: `SeedanceUI/Editor/FontResolver.cs`、`.claude/skills/seedance-ui-to-unity/scripts/fetch_font.sh`
- Test: `Assets/Tests/SeedanceUI/FontResolverTests.cs`

**Interfaces:**
- Produces:
  - `static class FontResolver { TMP_FontAsset Resolve(string tmpFontAssetPath, string fontFilePath, string importDir); void EnsureTmpEssentials(); }`
  - `fetch_font.sh`：把 `https://github.com/notofonts/noto-cjk/raw/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf` 下载到 `${XDG_CACHE_HOME:-$HOME/.cache}/seedance-ui-to-unity/NotoSansCJKsc-Regular.otf`（已存在且大于 1MB 就跳过），stdout 只打印这个绝对路径，失败退出码非 0。字体是 SIL OFL 1.1，可随游戏分发。

`Resolve`：
- `tmpFontAssetPath` 非空 → `LoadAssetAtPath<TMP_FontAsset>`，为 null 抛 `UiImportException`。
- 否则 `fontFilePath` 必须存在（不存在抛异常）。拷到 `<importDir>/Fonts/<ToAscii(文件名)><扩展名>`（已存在不覆盖），导入；`<importDir>/Fonts/<FontAssetName>.asset` 已存在就加载返回。不存在时用 `TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic)` 创建，`AssetDatabase.CreateAsset` 后**必须把 `atlasTextures[0]` 和 `material` 用 `AddObjectToAsset` 挂成子资源**，再 `SaveAssets`。不挂的话，重开 Editor 后字体丢贴图，文字消失。
- `EnsureTmpEssentials`：`TMP_Settings` 资源找不到时导入 TMP Essential Resources（`TMP_PackageResourceImporter.ImportResources(true, false, false)`；这个 API 在 6000.0 的 ugui 包里若不可见，就改为 `AssetDatabase.ImportPackage(<com.unity.ugui 包内 "TMP Essential Resources.unitypackage">, false)`）。

- [ ] **Step 1: 写失败测试**（默认字体用 `fetch_font.sh` 的缓存路径；文件不存在就 `Assert.Ignore("先跑 fetch_font.sh")`）
  - `Resolve_ExistingAsset`：传入测试里先建好的 TMP 字体资产路径 → 返回同一个对象。
  - `Resolve_BadAssetPath_Throws`。
  - `Resolve_FromFontFile_CreatesPersistentAsset`：生成 `Fonts/NotoSansCJKsc-Regular_SDF.asset`；`AssetDatabase.LoadAllAssetsAtPath` 里有 `Texture2D` 和 `Material`；`atlasPopulationMode == Dynamic`；`HasCharacters("角色属性")` 为真（动态字体会按需加字）。
  - `Resolve_Twice_Reuses`：第二次调用返回的资产 GUID 与第一次相同。
- [ ] **Step 2: 跑** `bash .claude/skills/seedance-ui-to-unity/scripts/fetch_font.sh` 得到路径；`run-tests.sh FontResolverTests` → 6。
- [ ] **Step 3: 实现。**
- [ ] **Step 4: 跑** → 0（不能有 Ignored）。
- [ ] **Step 5: Commit** `feat: TMP 字体解析与默认中文字体`

---

### Task 7: 新建 Prefab

**Files:**
- Create: `SeedanceUI/Editor/PrefabBuilder.cs`
- Modify: `SeedanceUI/Editor/ImportTypes.cs`（加 `ImportReport`）
- Test: `Assets/Tests/SeedanceUI/PrefabBuildTests.cs`

**Interfaces:**
- Consumes: Task 2/3/5/6 的产物。
- Produces:
  - `[Serializable] class ImportReport { string prefabPath; string scenePath; List<string> created, updated, deactivated, missing, typeChanged, moved, orphanSprites, warnings; }`，列表项格式为 `"<id> <name>"`（如 `"e3 角色属性"`），全部初始化为空列表。
  - `static class PrefabBuilder { GameObject BuildOrUpdate(UiLayoutDoc doc, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font, string prefabPath, ImportReport report); }`，返回保存后的 Prefab 资产。Prefab 名取 `prefabPath` 的文件名。

新建流程：根 `GameObject(<Prefab 名>, RectTransform)` + `UiGeometry.Stretch` + `SeedanceUiRoot`（设计分辨率、`layoutVersion = doc.version`）。**两遍**：先按数组顺序给每个元素建节点（名 `NodeName`、挂 `SeedanceUiElement`，`sourceName = e.name`），再按数组顺序挂到父节点（`parent` 为 null 挂根）并 `SetAsLastSibling`，所以同级顺序 = 数组顺序。之后 `UiGeometry.Apply(Place(e.EffectiveRect, parent?.EffectiveRect))`。组件：

| type | 组件 |
|---|---|
| panel / slot / icon / frame / image | `Image`（sprite，`raycastTarget = false`） |
| button | `Image`（`raycastTarget = true`）+ `Button`（`targetGraphic` = 该 Image） |
| tab | `Image`（`raycastTarget = true`）+ `Toggle`（`targetGraphic` = 该 Image，`graphic = null`） |
| bar | `Image`（`type = Filled`、`fillMethod = Horizontal`、`fillOrigin = Left`、`fillAmount = 1`，`raycastTarget = false`） |
| text | `TextMeshProUGUI`：text、font、fontSize、color（`ColorUtility.TryParseHtmlString`，失败用白色并记 warning）、`alignment` = `Left` / `Center` / `Right`（TMP 这三个值本身就是垂直居中，和上游预览一致）、`fontStyle = Bold`（bold 时）、`textWrappingMode = NoWrap`、`overflowMode = Overflow`、`raycastTarget = false` |
| missing | 只有 RectTransform + 标记，加进 `report.missing` |

每个新建节点加进 `report.created`。`PrefabUtility.SaveAsPrefabAsset` 后 `DestroyImmediate` 临时根。

- [ ] **Step 1: 写失败测试**（用真实夹具走 Task 5 / 6 得到 sprites 与 font；`prefabPath = "Assets/_SeedanceUiTestOut/CharacterPanel.prefab"`）
  - `Build_Hierarchy`：根名 `CharacterPanel`，根 anchorMin (0,0)、anchorMax (1,1)、无 `Canvas` 组件；`button_e2` 的父是 `panel_e1`；`text_e13` 的父是 `bar_e11`；`panel_e1` 下子节点顺序依次为 `button_e2, text_e3, frame_e4, button_e6, panel_e8, panel_e26, panel_e44, button_e62, button_e64`。
  - `Build_Positions`：`button_e2` 的 `anchoredPosition == (1959, 9)`、`sizeDelta == (105,106)`；`text_e3`（rect 1030,170,500,90，父 e1）→ (776, -27)。
  - `Build_Components`：`button_e2` 有 Button 且 `targetGraphic` 是自身 Image、`raycastTarget` 为真；`tab_e9` 有 Toggle；`bar_e11` 的 Image 是 Filled；`panel_e1` 的 `raycastTarget` 为假；`bar_e14.sprite == bar_e11.sprite`。
  - `Build_Text`：`text_e3.text == "角色属性"`、`fontSize == 64`、颜色 #ffe9b0、`alignment == Center`、Bold；`text_e12.alignment == Left`；`text_e13.alignment == Right`。
  - `Build_Markers`：65 个 `SeedanceUiElement`，`elementId` 与 doc 一一对应，`text_e3` 的 `sourceName == "角色属性"`；`report.created.Count == 65`。
  - `Build_MissingPlaceholder`：内联 doc 里一个 missing icon → 节点只有 RectTransform + 标记，位置取 drawnRect，`report.missing` 含它。
- [ ] **Step 2: 跑** `run-tests.sh PrefabBuildTests` → 6。
- [ ] **Step 3: 实现。**
- [ ] **Step 4: 跑** → 0。
- [ ] **Step 5: Commit** `feat: 按 layout.json 新建 UGUI Prefab`

---

### Task 8: 重导更新 Prefab

**Files:**
- Modify: `SeedanceUI/Editor/PrefabBuilder.cs`
- Test: `Assets/Tests/SeedanceUI/PrefabUpdateTests.cs`

**Interfaces:**
- Consumes / Produces: 同 Task 7 的 `BuildOrUpdate`；`prefabPath` 已存在时走更新。

更新流程：`PrefabUtility.LoadPrefabContents`，按 `SeedanceUiElement.elementId` 建索引。对 doc 里每个元素：
- **已有节点**：有 Image 且 sprites 里有它 → 换 sprite。位置：节点当前父节点就是 layout 父节点（父为 null 时是根）→ `Apply(Place(...))` 并记 `updated`；否则（被程序挪走）**不改位置和尺寸**，记 `moved`。`marker.type != e.type` → 记 `typeChanged`，组件不动、marker.type 也不改。文字内容 / 样式、父节点、sibling、`activeSelf` 一律不碰。
- **新元素**：按 Task 7 的规则建，挂到 layout 父节点（找不到就挂根），`SetAsLastSibling`，记 `created`。
- **doc 里已没有的节点**：`SetActive(false)`，记 `deactivated`。
- 没有标记组件的节点不碰。
- `SaveAsPrefabAsset` 回同一路径，`UnloadPrefabContents`。

- [ ] **Step 1: 写失败测试**（先用夹具 Build 一次，再改 Prefab 或 doc，再 BuildOrUpdate）
  - `Update_KeepsGuid`：两次之间 Prefab 的 `AssetPathToGUID` 不变。
  - `Update_MovesAndResprites`：doc 里把 e2 的 rect.x +10 → `button_e2.anchoredPosition.x == 1969`，`report.updated` 含 `e2`。
  - `Update_KeepsManualTextAndComponents`：先在 Prefab 里把 `text_e3.text` 改成 `"loc_key"`、给 `panel_e1` 加 `AudioSource`、在根下加一个无标记的 `Custom` 节点，重导后三者都在、文字仍是 `"loc_key"`。
  - `Update_MovedNode_KeepsParentAndReports`：把 `button_e2` 挪到 `Custom` 下，重导后父仍是 `Custom`、`anchoredPosition` 不变，`report.moved` 含 `e2`。
  - `Update_RemovedElement_Deactivated`：doc 删掉 e64 → `button_e64` 还在且 `activeSelf == false`，`report.deactivated` 含 `e64`。
  - `Update_NewElement_Created`：doc 追加一个 `missing: true` 的 `e66` icon（父 e1，drawnRect 任意）→ `icon_e66` 在 `panel_e1` 下，`report.created == ["e66 …"]`（只有一项）。
  - `Update_TypeChanged_Reported`：doc 里 e8 改成 `button` → `panel_e8` 没有 Button，`report.typeChanged` 含 `e8`。
- [ ] **Step 2: 跑** `run-tests.sh PrefabUpdateTests` → 8（更新路径还不存在）。
- [ ] **Step 3: 实现。**
- [ ] **Step 4: 跑** `run-tests.sh PrefabUpdateTests` 和 `PrefabBuildTests` → 都是 0。
- [ ] **Step 5: Commit** `feat: 重导时按 elementId 只更新 Sprite 与位置`

---

### Task 9: 展示场景

**Files:**
- Create: `SeedanceUI/Editor/ShowcaseScene.cs`
- Test: `Assets/Tests/SeedanceUI/ShowcaseSceneTests.cs`

**Interfaces:**
- Produces: `static class ShowcaseScene { const string ScenePath = "Assets/Product/Scene/UIShowcase.unity"; void Show(GameObject prefabAsset, int designWidth, int designHeight, ImportReport report); }`，会设 `report.scenePath`。

做法：
- 记下调用前已打开的场景。展示场景已在其中 → 直接用；否则文件存在 → `EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)`；不存在 → `NewScene(EmptyScene, Additive)`，建：
  - `Main Camera`：Camera，`clearFlags = SolidColor`，深灰底；
  - `Canvas`：Canvas（ScreenSpaceOverlay）+ CanvasScaler（ScaleWithScreenSize，参考分辨率 = 设计分辨率，`matchWidthOrHeight = UiGeometry.MatchFor(...)`）+ GraphicRaycaster；
  - `EventSystem`：EventSystem + 输入模块。工程只开新 Input System 时（`PlayerSettings` 的 active input handling 不含旧 Input Manager），用反射加 `UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem`；否则加 `StandaloneInputModule`。
  
  然后建好 `Assets/Product/Scene/` 目录并保存到 ScenePath。
- Canvas 参考分辨率 ≠ 本次设计分辨率 → `report.warnings` 加一条，场景不改。
- Canvas 下没有这个 Prefab 的实例（`PrefabUtility.GetCorrespondingObjectFromSource(child) == prefabAsset`）→ `PrefabUtility.InstantiatePrefab` 挂到 Canvas 下。本次 Prefab 的实例 `SetActive(true)`，Canvas 下其他带 `SeedanceUiRoot` 的实例 `SetActive(false)`。
- 保存场景；如果是本函数打开的，`CloseScene(scene, true)`。不碰其他已打开的场景。

- [ ] **Step 1: 写失败测试**（`[TearDown]` 删 `Assets/Product`）
  - `Create_LandscapeScene`：Show 后文件存在；Canvas 是 Overlay，CanvasScaler 参考分辨率 (2560,1440)、`matchWidthOrHeight == 0`；有 EventSystem。
  - `Create_PortraitScene`：1440×2560 → `matchWidthOrHeight == 1`。
  - `EventSystemModule_FollowsInputHandling`：在本开发工程（URP 模板默认只开 Input System）里，模块类型名是 `InputSystemUIInputModule`。
  - `Show_TwoPrefabs_OnlyLatestActive`：先 Show A 再 Show B → Canvas 下 A、B 实例各一个，B active、A inactive；再 Show A → 仍各一个，A active。
  - `Show_ResolutionMismatch_Warns`：场景按 2560×1440 建好后再用 1440×2560 Show → `report.warnings.Count == 1`，参考分辨率仍是 (2560,1440)。
  - `Show_DoesNotTouchUserScene`：先新建一个未保存、改过的场景作为 active 场景，Show 后它仍是 active、仍是 dirty，`SceneManager.sceneCount` 与调用前相同。
- [ ] **Step 2: 跑** `run-tests.sh ShowcaseSceneTests` → 6。
- [ ] **Step 3: 实现。**
- [ ] **Step 4: 跑** → 0。
- [ ] **Step 5: Commit** `feat: 展示场景 UIShowcase`

---

### Task 10: 编排与 CLI 命令

**Files:**
- Create: `SeedanceUI/Editor/SeedanceUiImporter.cs`
- Create: `SeedanceUI/Editor.Cli/{Seedance.UiImport.Editor.Cli.asmdef,SeedanceUiImportCommand.cs}`（+ `.meta`）
- Modify: `SeedanceUI/Editor/ImportTypes.cs`（加 `ImportRequest`）
- Test: `Assets/Tests/SeedanceUI/ImporterTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] class ImportRequest { string source; string importDir; string prefabName; string tmpFont; string fontFile; }`
  - `static class SeedanceUiImporter { ImportReport Run(ImportRequest req); }`
  - CLI：`unity command seedance_ui_import --request <请求 JSON 的绝对路径>`，返回 `JsonUtility.ToJson(report, true)`。

`Run` 的顺序：校验（`importDir` 以 `Assets/` 开头，去掉结尾 `/`；`prefabName` 为空就取 `importDir` 最后一级；不合法抛 `UiImportException`）→ `UiSource.Open` → `Parse` → 建目录 → `SpriteAssetWriter.Write`（orphans、warnings 并进 report）→ 有 text 元素时 `FontResolver.EnsureTmpEssentials` + `Resolve`（没有 text 元素就不要求字体）→ `PrefabBuilder.BuildOrUpdate` → `ShowcaseScene.Show` → `AssetDatabase.SaveAssets` → 返回 report。

CLI asmdef：Editor only，引用 `Seedance.UiImport.Editor` 和 `Unity.Pipeline`。命令方法 `[CliCommand("seedance_ui_import", "Import a Seedance ui-maker output as a UGUI prefab", MainThreadRequired = true)] static string Import([CliArg("request", "Absolute path to the import request JSON")] string request)`。`UiImportException` 原样抛出，让 CLI 报错。

- [ ] **Step 1: 写失败测试**
  - `Run_Zip_EndToEnd`：`source` = `char-panel.zip`、`importDir = "Assets/_SeedanceUiTestOut/CharacterPanel/"`、`fontFile` = 缓存字体 → `report.prefabPath == "Assets/_SeedanceUiTestOut/CharacterPanel/CharacterPanel.prefab"` 且资产存在；`created.Count == 65`；`scenePath == ShowcaseScene.ScenePath`。
  - `Run_Twice_SecondIsUpdate`：第二次 `created` 为空、`updated.Count == 65`（text 元素也算位置更新）。
  - `Run_DefaultPrefabName`：不传 `prefabName` → Prefab 名 `CharacterPanel`。
  - `Run_Rejects`：`importDir = "Packages/x"`、`prefabName = "角色"`、有文字但 `tmpFont` 和 `fontFile` 都没给 → 分别抛 `UiImportException`。
- [ ] **Step 2: 跑** `run-tests.sh ImporterTests` → 6。
- [ ] **Step 3: 实现** `SeedanceUiImporter` 与 CLI 命令。
- [ ] **Step 4: 跑** `run-tests.sh` 不带 filter（全部）→ 0。
- [ ] **Step 5: 在 Editor 里手动验证 CLI**：`unity open unity/SeedanceUiDev`，`unity status --until-ready --project-path <绝对路径>`；`unity list --project-path …` 里能看到 `seedance_ui_import`；写一个请求 JSON 到 scratchpad，`unity command seedance_ui_import --request <它> --project-path …` 返回的 JSON 里 `prefabPath` 正确。然后关掉 Editor。
- [ ] **Step 6: Commit** `feat: seedance_ui_import 编排与 CLI 命令`

---

### Task 11: SKILL.md 与真实产物验收

**Files:**
- Create: `.claude/skills/seedance-ui-to-unity/SKILL.md`
- Modify: `README.md`（「UI 界面创建工作流」下加一行指向这个 skill，勾掉「通过 Unity CLI 直接创建 UI 界面」）

**SKILL.md 内容**（frontmatter `name: seedance-ui-to-unity`，description 写清触发场景：把 Seedance / ui-maker 的 UI 切图、`layout.json`、`ui.zip` 导入 Unity 做成 Prefab，或重导更新）：
1. **参数**：产物路径、Unity 工程路径、导入目录（必填）；Prefab 名、TMP 字体资产路径（可选）。缺必填项就问用户。
2. **前置检查**：`command -v unity`；`ProjectSettings/ProjectVersion.txt` 的版本以 `6000.` 开头，否则停下说明；`Packages/manifest.json` 没有 `com.unity.pipeline` 就 `unity pipeline install --project-path <工程>`。
3. **拷包**：`bash <skill>/scripts/install_package.sh <工程>`。
4. **字体**：没给 TMP 字体、而 `layout.json` 里有 text 元素时，跑 `fetch_font.sh` 拿路径作为 `fontFile`，并告诉用户默认用的是 Noto Sans CJK SC（SIL OFL）。
5. **Editor**：`unity status --format json` 里找这个工程。没开就 `unity open <工程>` 后 `unity status --until-ready --project-path <工程>`。拷包后 `unity command recompile --project-path …`，轮询 `recompile_status` 到 `completed`；用 `unity list` 确认有 `seedance_ui_import`。
6. **导入**：把请求 JSON 写进 scratchpad，跑 `unity command seedance_ui_import --request <路径> --project-path <工程>`。
7. **截图对照**：`open_scene` 打开 `Assets/Product/Scene/UIShowcase.unity`，用 `capture_game_view` 截图（参数以 `unity command --format json` 列出的为准），把截图和产物的 `preview.png` 都读出来给用户并排看。截图没有背景、`preview.png` 有背景，对照 UI 本身就行。
8. **报告**：Prefab 路径，新建 / 更新 / 隐藏 / missing / 被挪走 / 类型变化 / 孤儿 Sprite / 警告。
9. **失败处理**：照 unity-cli skill 的规则——超时、Safe Mode、连不上都如实报告，不重试循环、不手改 `.prefab` / `.meta` 文本。

- [ ] **Step 1: 写 SKILL.md**，按 superpowers:writing-skills 的要求自查 description 和结构。
- [ ] **Step 2: 真实产物验收**。新建一个空 scratch 工程（`unity projects new SeedanceUiAccept --path <scratchpad> --editor-version 6000.0.62f1 --template com.unity.template.urp-blank`），按 SKILL.md 从头走一遍，依次导入 3 个任务：`uim-mvaean9h-295a38/out/`（目录）、`uim-mvaex9q1-938c26/out/ui.zip`、`uim-mvaxo13p-7c4c12/out/ui.zip`，导入目录 `Assets/UI/Generated/<名>/`。
  - 期望：3 个 Prefab 都生成；每张截图与 `preview.png` 肉眼对齐，中文不是方块。
  - 再导一次 `uim-mvaex9q1`：report 的 `created` 为空，Prefab 和 Sprite 的 GUID 不变（导入前后 `grep guid` 对比 `.meta`）。
- [ ] **Step 3: 把截图和结论给用户看**，等用户确认后再收尾。
- [ ] **Step 4: Commit** `feat: seedance-ui-to-unity skill`
