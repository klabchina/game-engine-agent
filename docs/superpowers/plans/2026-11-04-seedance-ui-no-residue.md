# Seedance UI 导入：不留代码、不挂脚本 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 导入完成后目标工程里不留 Seedance 代码，生成的 Prefab 上不挂 Seedance 脚本；重导所需的结构信息改存到 `Assets/SeedanceUI/` 下的 JSON 记录文件。

**Architecture:** SDK 改装到临时目录 `Assets/SeedanceUiImporter/`，skill 在流程最后（成功或失败）删掉它。`Runtime/` 程序集（`SeedanceUiRoot` / `SeedanceUiElement`）整个删除；新增 `UiRecord`（数据）+ `UiRecordStore`（定位 / 读写记录）+ `PrefabNodeIds`（Prefab 内 local fileID ↔ 节点对象）。`PrefabBuilder` / `ShowcaseScene` / `ui_capture` 改为读写记录。

**Tech Stack:** Unity 6（6000.0.62f1）、UGUI、TextMeshPro、`com.unity.pipeline`（0.8 / ≥0.9）、NUnit EditMode 测试、bash。

**Spec:** `docs/superpowers/specs/2026-11-04-seedance-ui-no-residue-design.md`（前置：`docs/superpowers/specs/2026-10-21-seedance-ui-to-unity-design.md`）

## Global Constraints

- 只支持 Unity 6；所有 `unity` 命令带 `--no-banner --project-path`。
- SDK 安装目录：`Assets/SeedanceUiImporter/`；记录根目录：`Assets/SeedanceUI/`（不放 Resources）。
- 记录路径 = `Assets/SeedanceUI/` + Prefab 路径去掉开头 `Assets/`，扩展名换 `.json`。例：`Assets/UI/Generated/CharacterPanel.prefab` → `Assets/SeedanceUI/UI/Generated/CharacterPanel.json`。
- 根下元素的 `layoutParentId` 为 `"<root>"`。记录 `formatVersion` = 1。
- 节点对应只按 Prefab 内 local fileID，不按名字 / 路径。
- 不兼容旧格式（挂着旧标记组件的 Prefab 不迁移）。
- 包内所有 `.meta` 换新 GUID 一次，之后固定不改；`Assets/SeedanceUI.meta` 由 Unity 自动生成。
- 用户可见消息（异常、脚本输出）用中文；提交信息中文，前缀 `feat:` / `fix:` / `refactor:` / `docs:`，结尾带 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`。
- 跑 Unity 测试前关掉测试工程的 Editor；测试命令 `bash unity/SeedanceUiExample/run-tests.sh [类名]`，退出码 0 才算过（9 = 有跳过，也不算过）。

## Review Focus

1. **两份记录的 `prefabGuid` 相同**（有人手动复制了记录文件）→ 按 GUID 查找时应报中文错误并列出两份路径，而不是随便挑一份。测试：Task 2 `Find_DuplicateGuid_Throws`。
2. **Prefab 路径里有中文 / 空格目录**（`Assets/UI 界面/X.prefab`）→ 记录路径照样镜像，读写正常。测试：Task 2 `RecordPath_NonAsciiAndSpaces`。
3. **导入在保存 Prefab 之前就失败**（layout 不合法、缺字体）→ 不能写出半份记录。测试：Task 4 `Run_FailsBeforeSave_WritesNoRecord`。
4. **程序把节点挪到自己手加的节点下面并改名** → 仍认得出、报 `moved`，不重建。测试：Task 4 `Update_RenamedAndReparentedUnderManualNode_Recognized`。
5. **导入命令报错后** skill 仍删包；删包后 `unity list` 里不再有 seedance 命令。验证：Task 5 手动端到端第 3 步。

---

## 文件结构

| 文件 | 动作 | 职责 |
|---|---|---|
| `.claude/skills/seedance-ui-to-unity/SeedanceUI/` → `SeedanceUiImporter/` | 改名（`git mv`） | 包源码；目录 meta 改为 `SeedanceUiImporter.meta` |
| `…/SeedanceUiImporter/Runtime/` | 删除 | 标记组件不再需要 |
| `…/SeedanceUiImporter/Editor/UiRecord.cs` | 新建 | 记录数据类 |
| `…/SeedanceUiImporter/Editor/UiRecordStore.cs` | 新建 | 记录路径、按 GUID 查找、原子写、搬迁 |
| `…/SeedanceUiImporter/Editor/PrefabNodeIds.cs` | 新建 | fileID ↔ 节点对象映射 |
| `…/Editor/PrefabBuilder.cs` `ShowcaseScene.cs` `UiNaming.cs` | 修改 | 改用记录 |
| `…/Editor.Cli/SeedanceUiImportCommand.cs` | 修改 | `ui_capture` 读记录 |
| `…/Editor/*.asmdef` `Editor.Cli/*.asmdef` | 修改 | 去掉 `Seedance.UiImport.Runtime` 引用 |
| `.claude/skills/seedance-ui-to-unity/scripts/install_package.sh` | 修改 | 装到 `Assets/SeedanceUiImporter/`，清旧 SDK |
| `.claude/skills/seedance-ui-to-unity/scripts/uninstall_package.sh` | 新建 | 删包 |
| `.claude/skills/seedance-ui-to-unity/SKILL.md` | 修改 | 流程加删包、文档更新 |
| `unity/SeedanceUiExample/.gitignore` `tools/test_install_package.sh` `tools/gen_meta.py`（用法注释） | 修改 | 跟新目录 |
| `unity/SeedanceUiExample/Assets/Tests/SeedanceUI/*` | 修改 / 新建 | 见各任务 |
| `docs/engine/unity.md`、旧 spec | 修改 | 文档同步 |

---

### Task 1: 包改名换 GUID、推包 / 删包脚本

**Files:**
- Rename: `.claude/skills/seedance-ui-to-unity/SeedanceUI/` → `.claude/skills/seedance-ui-to-unity/SeedanceUiImporter/`；删 `SeedanceUI.meta`
- Modify: `.claude/skills/seedance-ui-to-unity/scripts/install_package.sh`
- Create: `.claude/skills/seedance-ui-to-unity/scripts/uninstall_package.sh`
- Modify: `unity/SeedanceUiExample/tools/test_install_package.sh`、`unity/SeedanceUiExample/.gitignore`、`unity/SeedanceUiExample/tools/gen_meta.py`（仅用法注释）

**Interfaces:**
- Produces: `install_package.sh <工程路径>` → 同步到 `<工程>/Assets/SeedanceUiImporter/` + `SeedanceUiImporter.meta`；`uninstall_package.sh <工程路径>` → 删这两者，目录不存在也退出 0，stdout 打印「已删除 …」或「没有安装，跳过」。

- [ ] **Step 1: 改 `test_install_package.sh`（先让它失败）**

在 `check()` 里：
- 预置旧 SDK：`mkdir -p "$proj/Assets/SeedanceUI/Editor" "$proj/Assets/SeedanceUI/Runtime" "$proj/Assets/SeedanceUI/Editor.Cli"`，各放一个 `X.cs`，再放 `Editor.meta` `Runtime.meta` `Editor.Cli.meta`；放一份记录 `"$proj/Assets/SeedanceUI/UI/A.json"`。
- 预置 `"$proj/Assets/SeedanceUiImporter/Old/Stale.cs"`。
- 断言：`diff -r "$src" "$proj/Assets/SeedanceUiImporter"` 一致；`cmp "$skill/SeedanceUiImporter.meta" "$proj/Assets/SeedanceUiImporter.meta"`；`Assets/SeedanceUI/Editor`、`Runtime`、`Editor.Cli` 及其 `.meta` 都不存在；`Assets/SeedanceUI/UI/A.json` 仍在。
- 再跑 `uninstall_package.sh "$proj"`：退出 0，`Assets/SeedanceUiImporter` 与 `.meta` 不存在，`A.json` 仍在；再跑一次仍退出 0。

`src` 改为 `$skill/SeedanceUiImporter`。

- [ ] **Step 2: 跑测试确认失败**

Run: `bash unity/SeedanceUiExample/tools/test_install_package.sh`
Expected: `FAIL with-rsync …`，退出码 1

- [ ] **Step 3: 改名并换 GUID**

```bash
cd .claude/skills/seedance-ui-to-unity
git mv SeedanceUI SeedanceUiImporter
git rm SeedanceUI.meta
find SeedanceUiImporter -name '*.meta' -exec git rm -q {} +
python3 ../../../unity/SeedanceUiExample/tools/gen_meta.py SeedanceUiImporter
```
确认生成了 `SeedanceUiImporter.meta` 与包内每个文件 / 目录的新 meta。

- [ ] **Step 4: 改 `install_package.sh`、新建 `uninstall_package.sh`**

`install_package.sh`：`src` 指向 `SeedanceUiImporter/`，`dst="$project/Assets/SeedanceUiImporter"`，目录 meta 拷 `SeedanceUiImporter.meta`。同步前清旧 SDK：对 `Editor` `Editor.Cli` `Runtime` 三个名字，`rm -rf "$project/Assets/SeedanceUI/$d" "$project/Assets/SeedanceUI/$d.meta"`。只用 Step 1 里无 rsync PATH 已有的命令。
`uninstall_package.sh`：参数校验同 install；`rm -rf` 目录和 meta；`set -euo pipefail`。

- [ ] **Step 5: 跑脚本测试确认通过**

Run: `bash unity/SeedanceUiExample/tools/test_install_package.sh`
Expected: `ok with-rsync`、`ok without-rsync`，退出码 0

- [ ] **Step 6: 测试工程跟上**

`unity/SeedanceUiExample/.gitignore`：`Assets/SeedanceUI/` `Assets/SeedanceUI.meta` 两行改为 `Assets/SeedanceUiImporter/` `Assets/SeedanceUiImporter.meta`；另加 `Assets/SeedanceUI/UI/`、`Assets/SeedanceUI/Product/`（手动导入产物的记录，和产物一样不提交）。`run-tests.sh` 的 `SKILL` 路径不变（脚本名不变）。`gen_meta.py` 用法注释里的目录名改为 `SeedanceUiImporter`。

- [ ] **Step 7: 跑全部 Unity 测试（行为未变，应全过）**

Run: `bash unity/SeedanceUiExample/run-tests.sh`
Expected: 退出码 0。测试工程里旧的 `Assets/SeedanceUI/{Editor,Editor.Cli,Runtime}` 已被清掉、`Assets/SeedanceUiImporter/` 已出现。

- [ ] **Step 8: Commit**

```bash
git add -A .claude/skills/seedance-ui-to-unity unity/SeedanceUiExample/.gitignore unity/SeedanceUiExample/tools
git commit -m "refactor: SDK 改装到 Assets/SeedanceUiImporter 并换新 GUID，新增删包脚本"
```

---

### Task 2: 记录文件 `UiRecord` / `UiRecordStore`

**Files:**
- Create: `.claude/skills/seedance-ui-to-unity/SeedanceUiImporter/Editor/UiRecord.cs`、`UiRecordStore.cs`（各补 meta：`gen_meta.py`）
- Create: `unity/SeedanceUiExample/Assets/Tests/SeedanceUI/UiRecordStoreTests.cs`
- Modify: `unity/SeedanceUiExample/Assets/Tests/SeedanceUI/TestPaths.cs`

**Interfaces:**
- Produces（命名空间 `Seedance.UiImport.Editor`）：
  ```csharp
  [Serializable] public class UiRecordElement { public string elementId; public long fileId; public string sourceName; public string type; public string layoutParentId; }
  [Serializable] public class UiRecord { public int formatVersion = 1; public string prefabGuid; public int designWidth, designHeight, layoutVersion; public List<UiRecordElement> elements = new(); }
  public static class UiRecordStore {
      public const string Root = "Assets/SeedanceUI";
      public const string RootParentId = "<root>";
      public static string RecordPathFor(string prefabPath);
      /// 按 GUID 找；镜像路径上 prefabGuid 对得上优先，否则扫 Root 下所有 *.json。没有返回 null。解析失败 / 多份同 GUID 抛 UiImportException。recordPath 为找到的位置。
      public static UiRecord FindByGuid(string prefabGuid, string prefabPath, out string recordPath);
      /// 写到 RecordPathFor(prefabPath)：先写 "<path>.tmp" 再替换；previousPath 非空且不同则删掉它（含 .meta）。写完 AssetDatabase.ImportAsset。
      public static void Save(string prefabPath, UiRecord record, string previousPath);
      /// 首次导入前调用：镜像路径上已有记录、且它的 prefabGuid 指向一个存在于别处的 Prefab，就把它搬到那个 Prefab 的镜像路径。
      public static void ReleaseMirrorPath(string prefabPath);
  }
  ```
- `TestPaths` 新增 `public const string RecordOutDir = "Assets/SeedanceUI/_SeedanceUiTestOut";`（= `RecordPathFor` 对 `OutDir` 下 Prefab 的目录）。

- [ ] **Step 1: 写失败测试 `UiRecordStoreTests`**

TearDown：`AssetDatabase.DeleteAsset(TestPaths.OutDir); AssetDatabase.DeleteAsset(TestPaths.RecordOutDir);`。辅助 `MakePrefab(name)` 存一个空 RectTransform Prefab 到 `OutDir`，返回 GUID。

```csharp
[Test] public void RecordPath_Mirrors() =>
    Assert.AreEqual("Assets/SeedanceUI/UI/Generated/CharacterPanel.json", UiRecordStore.RecordPathFor("Assets/UI/Generated/CharacterPanel.prefab"));
[Test] public void RecordPath_NonAsciiAndSpaces() =>
    Assert.AreEqual("Assets/SeedanceUI/UI 界面/X.json", UiRecordStore.RecordPathFor("Assets/UI 界面/X.prefab"));
[Test] public void SaveThenFind_RoundTrip()  // Save 后 FindByGuid 拿回同样字段（含 fileId、"<root>"）；recordPath == RecordPathFor；目录下没有 .tmp 残留
[Test] public void Find_None_ReturnsNull()
[Test] public void Find_PrefabMoved_FindsByGuid()  // 记录存在旧镜像路径，用新 prefabPath 查 → 找到，recordPath 为旧路径
[Test] public void Save_WithPreviousPath_MovesRecord()  // 旧路径文件与 .meta 消失，新路径存在
[Test] public void Find_MirrorPathGuidMismatch_UsesGuid()  // 镜像路径上的记录属于别的 GUID，真正的记录在别处 → 返回别处那份
[Test] public void Find_Corrupt_Throws()  // 镜像路径写入 "{not json" → Assert.Throws<UiImportException>，消息含该路径
[Test] public void Find_DuplicateGuid_Throws()  // 两份记录同 GUID → 抛 UiImportException，消息含两份路径
[Test] public void ReleaseMirrorPath_MovesRecordOfMovedPrefab()  // Prefab A 从 OutDir/X.prefab 挪到 OutDir/Y.prefab（AssetDatabase.MoveAsset），记录仍在 X.json；ReleaseMirrorPath(OutDir/X.prefab) 后 X.json 不在、Y.json 在
[Test] public void ReleaseMirrorPath_StaleRecord_LeftForOverwrite()  // X.json 的 GUID 指向不存在的 Prefab → 不搬，X.json 仍在
```

- [ ] **Step 2: 跑测试确认失败**

Run: `bash unity/SeedanceUiExample/run-tests.sh UiRecordStoreTests`
Expected: 非 0（编译失败：`UiRecordStore` 不存在，看 `Logs/test-editor.log`）

- [ ] **Step 3: 实现 `UiRecord.cs`、`UiRecordStore.cs`**

用 `JsonUtility.ToJson(record, true)` / `FromJson`（异常与 `prefabGuid` 为空都算解析失败）；文件读写用 `System.IO`（相对 `Assets/` 的路径相对工程根即可）。`.tmp` 后缀 Unity 不导入。替换用 `File.Copy(tmp, path, true)` 后删 tmp（或 `File.Replace`，目标不存在时用 `File.Move`）。删旧路径用 `AssetDatabase.DeleteAsset`。`ReleaseMirrorPath` 用 `AssetDatabase.GUIDToAssetPath` 判断 Prefab 是否存在于别处。

- [ ] **Step 4: 跑测试确认通过**

Run: `bash unity/SeedanceUiExample/run-tests.sh UiRecordStoreTests`
Expected: 退出码 0

- [ ] **Step 5: Commit**

```bash
git commit -am "feat: Seedance UI 记录文件的读写与按 GUID 查找"   # 记得 git add 新文件与 meta
```

---

### Task 3: `PrefabNodeIds`（fileID ↔ 节点）

**Files:**
- Create: `.claude/skills/seedance-ui-to-unity/SeedanceUiImporter/Editor/PrefabNodeIds.cs`（+ meta）
- Create: `unity/SeedanceUiExample/Assets/Tests/SeedanceUI/PrefabNodeIdsTests.cs`

**Interfaces:**
- Produces：
  ```csharp
  public static class PrefabNodeIds {
      /// assetRoot：已保存的 Prefab 资源根；cloneRoot：结构完全相同的副本（LoadPrefabContents 的结果，或刚用来 SaveAsPrefabAsset 的临时根）。
      /// 按 sibling-index 路径把两边一一对上，返回 资源 GameObject 的 local fileID → 副本里的 GameObject。
      public static Dictionary<long, GameObject> Map(GameObject assetRoot, GameObject cloneRoot);
  }
  ```
  fileID 用 `AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetGo, out _, out long id)`。

- [ ] **Step 1: 写失败测试**

```csharp
[Test] public void Map_StableAcrossRenameAndReparent()
// 建 Prefab：Root/A/B。记下 B 的 fileID（Map(asset, contents) 反查）。
// LoadPrefabContents → 把 B 改名 "Renamed"、挪到 Root 下 → SaveAsPrefabAsset → Unload。
// 再 LoadPrefabContents，Map 后 map[idB].name == "Renamed" 且 parent == contents root。
[Test] public void Map_DuplicatedNodeGetsNewId()
// contents 里 Instantiate B 作为 B 的兄弟并保存；Map 后共 4 个 id，两份 B 的 id 不同，原 idB 仍对应原来那个（按 sibling 顺序在前的）。
[Test] public void Map_NewPrefabFromTempRoot()
// 临时根 SaveAsPrefabAsset 后 Map(asset, tempRoot)：每个临时对象都有 id 且互不相同。
```

- [ ] **Step 2: 跑测试确认失败**

Run: `bash unity/SeedanceUiExample/run-tests.sh PrefabNodeIdsTests`
Expected: 非 0（编译失败）

- [ ] **Step 3: 实现 `PrefabNodeIds.Map`**

递归同时遍历两棵树的 `transform.GetChild(i)`；两边子节点数不同直接抛 `InvalidOperationException`（调用方用法保证结构一致，不同即 bug）。拿不到 fileID 的对象跳过。

- [ ] **Step 4: 跑测试确认通过**

Run: `bash unity/SeedanceUiExample/run-tests.sh PrefabNodeIdsTests`
Expected: 退出码 0

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: 按 Prefab 内 fileID 找回节点"   # 先 git add 新文件与 meta
```

---

### Task 4: 导入改用记录，删除 Runtime 标记组件

**Files:**
- Modify: `…/SeedanceUiImporter/Editor/PrefabBuilder.cs`、`ShowcaseScene.cs`、`UiNaming.cs`（注释）、`SeedanceUiImporter.cs`（如需）
- Modify: `…/SeedanceUiImporter/Editor.Cli/SeedanceUiImportCommand.cs`
- Modify: `…/Editor/Seedance.UiImport.Editor.asmdef`、`…/Editor.Cli/Seedance.UiImport.Editor.Cli.asmdef`
- Delete: `…/SeedanceUiImporter/Runtime/`（含 meta）
- Modify tests: `Seedance.UiImport.Tests.asmdef`（去 Runtime 引用）、`PrefabBuildTests.cs`、`PrefabUpdateTests.cs`、`PrefabReviewFixTests.cs`、`ShowcaseSceneTests.cs`、`ImporterTests.cs`、`PrefabCaptureTests.cs`（TearDown 都加删 `TestPaths.RecordOutDir`）
- Delete test: `RuntimeComponentTests.cs`
- Create test: `RecordImportTests.cs`

**Interfaces:**
- Consumes: Task 2 `UiRecordStore` / `UiRecord`，Task 3 `PrefabNodeIds.Map`
- Produces: `PrefabBuilder.BuildOrUpdate(...)`、`ShowcaseScene.Show(...)` 签名不变。新增：
  - `public static void PrefabBuilder.EnsureRecordReadable(string prefabPath)`：Prefab 存在时 `FindByGuid`，为 null 或解析失败抛下面的 `UiImportException`；Prefab 不存在什么都不做。`SeedanceUiImporter.Run` 在 `SpriteAssetWriter.Write` **之前**调用它，保证记录有问题时一个文件都不写。
  - `public static (int width, int height) UiRecordStore.DesignSize(string prefabPath)`：没有记录抛 `UiImportException($"{prefabPath} 不是 Seedance UI 导入的 Prefab（Assets/SeedanceUI/ 下没有它的记录）")`。`ui_capture` 用它。

行为（对照现有实现逐条保留）：
- **Create**：先 `UiRecordStore.ReleaseMirrorPath(prefabPath)`；建节点时在内存里记 `GameObject → UiRecordElement`（替代 `AddComponent<SeedanceUiElement>`，`layoutParentId = e.parent ?? RootParentId`）；保存后 `Map(savedAsset, tempRoot)` 反查填 `fileId`；`UiRecordStore.Save(prefabPath, record, null)`；记录里写 `designWidth/Height`、`layoutVersion`、`prefabGuid`。
- **Update**：`FindByGuid(guid, prefabPath, out recPath)`；为 null 抛 `UiImportException($"{prefabPath} 已存在但找不到它的导入记录（Assets/SeedanceUI/ 下）。删掉这个 Prefab 再导入会按首次导入重建（手加的内容会丢）")`，且此前不写任何东西。`LoadPrefabContents` 后 `Map(asset, contents)`，按记录的 `fileId` 得到 `elementId → GameObject`；记录里有但 map 里没有的元素当新元素建。`marker.type` / `marker.layoutParentId` 改读写记录条目。去掉「只认归属本根的标记」那段（嵌套 Prefab 的对象本就不在记录里）。保存后再 `Map(savedAsset, contents)` 给新建节点填 `fileId`，`Save(prefabPath, record, recPath)`。
- 「deactivated」报告文本用记录里的 `elementId sourceName`。
- **ShowcaseScene**：判断 Canvas 子节点是否导入 UI：`PrefabUtility.GetCorrespondingObjectFromSource(child)` 非空，且其 GUID `UiRecordStore.FindByGuid(guid, path, out _) != null`。
- **ui_capture**：`FindByGuid` 取 `designWidth/Height`。

- [ ] **Step 1: 改测试（先失败）**

- `PrefabBuildTests.Build_Hierarchy`：`SeedanceUiRoot` 断言改为读记录：`Assert.AreEqual(2560, Record().designWidth)`。
- `Build_Markers` 改名 `Build_Record`：记录元素数 == layout 元素数；`e3` 的 `sourceName`、`type == "text"`；每个元素的 `fileId` 在 `PrefabNodeIds.Map(prefab, prefab)` 里能找到，且对应节点名为 `UiNaming.NodeName`。
- `Build_MissingPlaceholder`：`GetComponent<SeedanceUiElement>` 断言改为「记录里有该元素」。
- 新增 `Build_NoSeedanceComponents`：`prefab.GetComponentsInChildren<Component>(true)` 中没有任何类型的 `Namespace` 以 `"Seedance"` 开头。
- `PrefabUpdateTests.Update_MovesAndResprites`：`Count(m => m.elementId == "e2")` 改为记录里 `e2` 只有一条且对应节点存在；`Update_TypeChanged_Reported`：断言记录里 `e8` 的 `type` 仍是 `"panel"`。
- `ShowcaseSceneTests.MakePrefab`：去掉 `AddComponent<SeedanceUiRoot>`，保存后 `UiRecordStore.Save(path, new UiRecord{ prefabGuid = …, designWidth = 2560, designHeight = 1440 }, null)`；`GetComponentsInChildren<SeedanceUiRoot>` 改为 Canvas 的直接子节点。新增 `Show_IgnoresNonSeedanceChild`：Canvas 下手放一个没有记录的 Prefab 实例，Show 之后它的 active 不变。
- `PrefabCaptureTests` 若依赖 `SeedanceUiRoot` 一并改；新增 `DesignSize_NoRecord_Throws`（对一个没有记录的 Prefab 调 `UiRecordStore.DesignSize` → 抛 `UiImportException`，消息含「没有它的记录」）和 `DesignSize_FromRecord`（2560×1440）。
- `ImporterTests` 新增 `Run_ExistingPrefabWithoutRecord_WritesNothing`：先 `Run` 一次，删掉记录，再 `Run` → 抛 `UiImportException`；`Sprites/` 下文件的修改时间不变、Prefab 字节不变。
- 新建 `RecordImportTests`（SetUp `PrefabFixture.Build()`）：
  ```csharp
  [Test] public void Update_RenamedAndReparentedUnderManualNode_Recognized()
  // EditPrefab：新建手加节点 "Manual" 挂 panel_e1 下，把 button_e2 改名 "BtnLevelUp" 挪进 Manual。重导 →
  // report.moved 含 "e2 …"，created 不含 e2；Prefab 里叫 BtnLevelUp 的节点只有 1 个，没有新的 button_e2。
  [Test] public void Update_DeletedNode_Recreated()       // EditPrefab 删 text_e3 → 重导 → created 含 e3，text_e3 存在
  [Test] public void Update_DuplicatedNode_Untouched()    // EditPrefab 复制 button_e2 为 "Copy" 并挪到 (1,1) → 重导 → Copy 的 anchoredPosition 仍为 (1,1)，active
  [Test] public void Update_PrefabMoved_RecordFollows()   // AssetDatabase.MoveAsset 到 OutDir/Moved/CharacterPanel.prefab，用新路径重导 → 新镜像路径有记录、旧的没了、report.created 为空
  [Test] public void Update_NoRecord_ThrowsAndWritesNothing()  // 删记录 → 重导 Assert.Throws<UiImportException>；Prefab 文件字节不变、记录仍不存在
  [Test] public void Update_CorruptRecord_ThrowsAndWritesNothing() // 记录写 "{x" → 抛；Prefab 字节、记录内容都不变
  [Test] public void Create_AfterMovingOldPrefab_KeepsOldRecord() // MoveAsset 走旧 Prefab，再在原路径首次导入 → 两个 Prefab 各有自己的记录，各自 prefabGuid 正确
  ```
- `ImporterTests` 新增 `Run_FailsBeforeSave_WritesNoRecord`：用不合法 layout 的临时目录（或 `Run_Rejects_TextWithoutFont` 的情形）跑 `SeedanceUiImporter.Run`，抛异常后 `RecordPathFor(期望 prefabPath)` 不存在。
- 删除 `RuntimeComponentTests.cs`（含 meta），测试 asmdef 去掉 `Seedance.UiImport.Runtime`。

- [ ] **Step 2: 跑测试确认失败**

Run: `bash unity/SeedanceUiExample/run-tests.sh`
Expected: 非 0

- [ ] **Step 3: 删 Runtime，按上面「行为」改 `PrefabBuilder` / `ShowcaseScene` / `SeedanceUiImportCommand` / asmdef / 注释**

`PrefabBuilder` 里 `RootParentId` 改用 `UiRecordStore.RootParentId`。`UiNaming.cs` 首行注释改为「中文名存在记录文件的 sourceName 里」。

- [ ] **Step 4: 跑全部测试确认通过**

Run: `bash unity/SeedanceUiExample/run-tests.sh`
Expected: 退出码 0；再 `grep -rn "SeedanceUiRoot\|SeedanceUiElement" .claude unity/SeedanceUiExample/Assets/Tests` 无结果。

- [ ] **Step 5: Commit**

```bash
git add -A .claude/skills/seedance-ui-to-unity unity/SeedanceUiExample/Assets/Tests
git commit -m "feat: 重导结构信息改存 Assets/SeedanceUI 记录文件，Prefab 不再挂 Seedance 组件"
```

---

### Task 5: skill 流程加删包、文档同步、端到端验证

**Files:**
- Modify: `.claude/skills/seedance-ui-to-unity/SKILL.md`
- Modify: `docs/engine/unity.md`、`docs/superpowers/specs/2026-10-21-seedance-ui-to-unity-design.md`（标记组件相关段落前加「> 2026-11-04：已被 `2026-11-04-seedance-ui-no-residue-design.md` 取代——不再挂组件，改存记录文件」）
- Modify: `.claude-plugin/marketplace.json`（插件 `version` → `1.1.0`）

- [ ] **Step 1: 改 `SKILL.md`**

- Overview：去掉「中文名存在 `SeedanceUiElement.sourceName`」，改为「结构信息（元素 id、中文名、类型、父元素）存在 `Assets/SeedanceUI/<Prefab 路径>.json`，随工程提交；Prefab 上不挂 Seedance 脚本；导入结束后 SDK 从工程里删掉」。
- 步骤 2 推包说明目标目录 `Assets/SeedanceUiImporter/`。
- 新增步骤「删包」（在截图之后、汇报之前）：`bash "$SKILL_DIR/scripts/uninstall_package.sh" "$PROJECT"`，然后让 Editor 刷新并 `unity --no-banner status --until-ready --project-path "$PROJECT"`；`unity list` 里不应再有 seedance 命令。**步骤 2 之后任何一步失败都先执行删包再汇报**；Editor 连不上 / Safe Mode 时只删文件、不等，汇报里说「包已删，请手动刷新或重开 Editor」；删包后的编译错误只把 Seedance 相关的算失败，工程原有的照实列出。
  - 让 Editor 刷新的命令：实现者在测试工程上实测确定（候选 `unity recompile`，或 `unity command` 里的资源刷新类工具），写进 SKILL.md。
- 出错时：「`Assets/SeedanceUI/` 由本 skill 管理」改为「`Assets/SeedanceUiImporter/` 是临时目录，别在工程里改它」；新增「记录丢失 / 损坏」的处理（原样转述错误；删掉 Prefab 再导入会重建，手加内容会丢，先问用户）。
- Common Mistakes：删掉「手动删了 `Assets/SeedanceUI/Runtime`」，改为「手动删了 / 改坏了 `Assets/SeedanceUI/` 下的记录 → 重导会报错停下；从版本库恢复」。

- [ ] **Step 2: 改 `docs/engine/unity.md`**

路径表 `SeedanceUI/` → `SeedanceUiImporter/`，新增 `uninstall_package.sh`；约定里「只改 skill 目录里的源码」路径改为 `Assets/SeedanceUiImporter/`；新增一条「Prefab 不挂 Seedance 组件，结构信息在 `Assets/SeedanceUI/` 记录文件里（见 2026-11-04 spec）」；`gen_meta.py` 命令里的目录名改掉。

- [ ] **Step 3: 端到端手动验证（测试工程，按 SKILL.md 走）**

1. 关掉其他占用测试工程的进程，按 SKILL.md 用 `TestFixtures/char-panel.zip` 导入到 `Assets/UI/E2E`，`PREFAB_NAME=E2E`。
   Expected：report 正常；截图与 preview 对得上；之后 `ls Assets/SeedanceUiImporter` 不存在；`Assets/SeedanceUI/UI/E2E/E2E.json` 存在；`grep -c "Seedance" Assets/UI/E2E/E2E.prefab` 为 0；`unity list` 无 seedance 命令；`unity console` 无编译错误。
2. 再导一次同一产物：report 里 `created` 为空，`updated` 非空；结束后 SDK 同样已删。
3. 用一个不存在的 `SOURCE` 路径导入：`ui_import` 报中文错误，SDK 仍被删除，汇报里如实说明。
4. 清理：删 `Assets/UI/E2E`、`Assets/SeedanceUI/UI/E2E`。

- [ ] **Step 4: 全部测试再跑一遍**

Run: `bash unity/SeedanceUiExample/run-tests.sh && bash unity/SeedanceUiExample/tools/test_install_package.sh`
Expected: 两者退出码 0

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/seedance-ui-to-unity/SKILL.md docs .claude-plugin/marketplace.json
git commit -m "docs: skill 流程加删包，文档同步记录文件方案，插件版本 1.1.0"
```
