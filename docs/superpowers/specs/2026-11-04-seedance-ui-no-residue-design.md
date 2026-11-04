# Seedance UI 导入：不在工程里留代码、不在 Prefab 上挂脚本

日期：2026-11-04　　前置设计：`2026-10-21-seedance-ui-to-unity-design.md`

## 背景与目的

现在的导入流程会把 C# 包 `SeedanceUI`（Runtime + Editor + Editor.Cli）拷进目标工程 `Assets/SeedanceUI/` 并一直留着；生成的 Prefab 上，根节点挂 `SeedanceUiRoot`，每个导入节点挂 `SeedanceUiElement`。这两个脚本只为重导做 diff（找回节点对应哪个元素、分辨手加 / 被挪走的节点），对游戏运行没有任何作用。

用户希望：

1. **工程里不驻留 Seedance 代码**：导入流程结束后把整个 SDK 从工程里删掉。
2. **Prefab 上不挂 Seedance 脚本**：diff 所需的结构信息改存到 Prefab 之外的一个记录文件里。

## 目标

- 导入（含截图）结束后，目标工程里没有任何 Seedance 的 `.cs` / `.asmdef`；不论导入成功还是失败都删。
- 生成 / 重导后的 Prefab 上只有 Unity 自带组件（RectTransform、Image、Button、Toggle、TextMeshProUGUI 等）。
- 重导行为与现在完全一致（规则见前置设计与 `SKILL.md`：只更新 Sprite / 位置 / 尺寸；文字、同级顺序、显隐、已有组件归程序；父级只在程序没挪过时跟上游改；产物里没了的设 inactive；手加节点不碰；missing 占位补图后补组件；type 变化只报告）。
- 记录文件随工程提交，团队里其他人拿到工程也能重导。

## 非目标

- 不兼容旧格式：已经挂着 `SeedanceUiRoot` / `SeedanceUiElement` 的 Prefab 不做迁移（只有测试工程用过，用户确认）。
- 不改 ui-maker 产物格式、不改 report 字段、不改导入规则本身。

## 反目标（技术上能跑但算失败）

- 记录文件被推包 / 删包误删。
- 程序改了节点名或挪了层级后，重导认不出节点、建出重复节点。
- 记录文件进了游戏包（放 Resources 下或被引用）。

## 设计（用户决定的部分）

### 记录文件的位置与命名

- 统一放在 **`Assets/SeedanceUI/`** 下，不放 Resources。
- 名字按 Prefab 路径镜像：`Assets/UI/Generated/CharacterPanel.prefab` → `Assets/SeedanceUI/UI/Generated/CharacterPanel.json`（去掉开头的 `Assets/`，扩展名换成 `.json`）。
- 随工程提交。

### SDK 的安装位置（因上一条而变）

`Assets/SeedanceUI/` 从此只放记录文件，长期保留。SDK 改装到临时目录 **`Assets/SeedanceUiImporter/`**，`install_package.sh` 的 rsync `--delete` 只作用在这个目录上。

- **GUID 全部换新**：`SeedanceUiImporter.meta` 和包内每个文件 / 目录的 `.meta` 都重新生成一次 GUID（之后照旧固定不改），避免和旧版装在 `Assets/SeedanceUI/` 下的副本冲突。包从此每次用完就删，GUID 不再承担「Prefab 上组件引用」的作用，但固定下来能让推包结果稳定。
- `Assets/SeedanceUI.meta`（记录目录）由 Unity 自动生成，脚本不管。
- 旧版残留：`install_package.sh` 推包前，若 `Assets/SeedanceUI/` 下有 `Editor/`、`Editor.Cli/`、`Runtime/` 子目录（旧 SDK），连同其 `.meta` 删掉，只留记录文件。这是一次性清理，不算兼容旧格式（旧 Prefab 照样不迁移）；测试工程现在就是这个状态。

## 设计（我的判断，用户可改）

### 记录文件内容

纯 JSON（Unity 里是 TextAsset，没有任何脚本依赖），由 Editor 侧用 `JsonUtility` 读写。大致：

```json
{
  "formatVersion": 1,
  "prefabGuid": "…",
  "designWidth": 1920, "designHeight": 1080, "layoutVersion": 1,
  "elements": [
    { "elementId": "e7", "fileId": 1234567890123, "sourceName": "升级", "type": "button", "layoutParentId": "e1" }
  ]
}
```

字段与现在两个标记组件一一对应；`layoutParentId` 挂在根下仍为 `"<root>"`。

### 节点怎么对上：按 Prefab 内的 local fileID

- 不按节点名、不按层级路径——程序改名、挪层级都不影响。
- 读：`LoadPrefabContents` 之前，先遍历 Prefab 资源里的 GameObject，用 `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` 得到 fileID → 该对象在层级里的 sibling-index 路径；再按同一路径在 contents 里找到对应对象。得到 `elementId → GameObject`。
- 写：`SaveAsPrefabAsset` 会保留已有对象的 fileID；保存后重新加载资源，读出新建节点的 fileID，再写回记录。新建节点保存前在内存里按对象引用记着，保存后按同样的路径方式对回资源对象。
- 记录里有、但 Prefab 里找不到对应 fileID 的元素（程序删掉了节点）：按现在的行为当作新元素重建（`SKILL.md` 的「要重建文字就删掉那个节点再导」依赖这一点）。
- Prefab 里有、记录里没有的节点 = 手加的，完全不碰。程序 Ctrl+D 复制出来的节点 fileID 是新的，也算手加的。
- 嵌套进来的别的 Seedance Prefab：它们的对象不在本记录里，自然不碰（替代现在「只认归属本根的标记」的判断）。

### 记录与 Prefab 的对应

- **以 GUID 为准，路径只是快捷入口**：先看镜像路径上的记录，其 `prefabGuid` 等于当前 Prefab GUID 才用；否则扫 `Assets/SeedanceUI/` 下所有记录按 GUID 找。找到的记录不在镜像路径上（程序把 Prefab 挪 / 改名了）就在本次保存时搬过去（删旧文件）。
- Prefab 不存在 → 走首次导入。若镜像路径上已有旧记录：其 `prefabGuid` 仍指向一个存在的 Prefab（那个 Prefab 被挪走了还没重导过），先把旧记录搬到那个 Prefab 的镜像路径，再写新记录；否则直接覆盖。这样记录不会因为「挪走后原路径又导入新 UI」而丢失。
- 记录存在但解析失败 → 报中文错误停下，不写任何东西（不要默默当首次导入，那会建出一套重复节点）。
- Prefab 存在但没有任何记录 → 同样报错停下，提示「记录丢失；删掉这个 Prefab 再导入会按首次导入重建（手加内容会丢）」。不加强制首次导入的请求字段。
- 写记录：先写到同目录临时文件再改名替换，避免写一半。Prefab 已保存而记录没写成（崩溃 / 被杀）的极端情况，按上一条报错与手动恢复处理。

### 其他读标记组件的地方

- `ui_capture`：设计分辨率从记录读，替代 `SeedanceUiRoot`。
- 展示场景 `ShowcaseScene`：判断 Canvas 下哪些子节点是导入的 UI，改为「它的源 Prefab 有记录」（用 GUID 查记录）。
- `Runtime/` 程序集整个删掉；`UiNaming` 等注释里提到 `sourceName` 存在组件上的地方改为存在记录里。

### 删除 SDK 的时机与方式

- skill 流程：推包 → 等编译 → `ui_prepare` → `ui_import` → `ui_capture` → **删包**。
- 中途任何一步失败（包括编译错误、命令报错），也执行删包，再汇报失败。
- 删包由 skill 在 shell 里做：`rm -rf "$PROJECT/Assets/SeedanceUiImporter" "$PROJECT/Assets/SeedanceUiImporter.meta"`，然后让 Editor 刷新资源并等到就绪（`unity status --until-ready`），确认工程无编译错误。可以封装成 `scripts/uninstall_package.sh`。
- 删包后的 Editor 刷新 / 编译出错按 unity-cli 规则如实报告，不重试。
- Editor 连不上、超时、在 Safe Mode 时：照样删文件，不等 Editor 就绪，汇报里说明「包已删，Editor 需要你手动刷新 / 重开」。
- 删包后的编译检查只看是否还有 Seedance 相关的错误；工程原本就有的、和 Seedance 无关的编译错误照实列出，但不算本次导入失败。
- `uninstall_package.sh` 只负责删文件（目录不存在也算成功，退出码 0）；刷新、等就绪由 skill 用 `unity` 命令单独做。

### 测试工程

- `unity/SeedanceUiExample/` 的 `.gitignore`：忽略 `Assets/SeedanceUiImporter/`，**不再**忽略 `Assets/SeedanceUI/`（测试工程里的记录文件是否提交由建造者看情况，手动导入产物原本就在 ignore 里，对应记录也可以一起 ignore）。
- `run-tests.sh`：测试需要包在工程里才能编译，跑测试前推包；跑完后不删（测试工程是开发环境）。
- 删掉 `RuntimeComponentTests`；`PrefabBuildTests` / `PrefabUpdateTests` / `ShowcaseSceneTests` 里对标记组件的断言改为对记录文件的断言，并新增：
  - Prefab 上没有任何 `Seedance.*` 组件；
  - 改节点名、挪层级后重导仍认得出；
  - 删掉节点后重导会重建；复制出来的节点不碰；
  - Prefab 挪路径后按 GUID 找回记录并搬迁；
  - 记录损坏 / 缺失时报错且不写文件；
  - `install_package.sh` 不碰 `Assets/SeedanceUI/`，`uninstall_package.sh` 删干净。
- `test_install_package.sh` 跟着改目录。

### 需要同步更新的文档

`SKILL.md`（流程、删包步骤、「手动删了 Runtime」那条 Common Mistake 改为记录文件相关）、`docs/engine/unity.md`、前置设计里引用标记组件的段落加注「已被本设计取代」。

## 留给建造者

- fileID ↔ contents 对象的具体映射实现（路径法之外有更直接的 API 也可以用，满足「改名 / 挪层级不影响」即可）。
- 让 Editor 在删包后刷新的具体命令（`unity` 的哪条命令能触发 AssetDatabase Refresh）。
- 记录 JSON 的精确字段名与格式（可读、稳定排序即可）。
