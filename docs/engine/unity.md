# Unity

## 环境

- 只支持 Unity 6（测试工程 `unity/SeedanceUiExample/` 为 6000.0.62f1，URP）。
- 通过 Unity CLI（`unity`）驱动运行中的 Editor，Editor 侧需装 `com.unity.pipeline`（兼容 0.8 与 ≥0.9）。
- 可用的 Editor 工具清单见 `README.md`；CLI 参考：<https://docs.unity.com/zh-cn/unity-cli/unity-cli-reference>。

## Unity CLI 规则

- 本机常同时开着多个 Editor，每条 `unity` 命令都带 `--project-path`，加 `--no-banner`。
- 刚 `unity open` 的 Editor 会自己编译，不要马上 `recompile`（会 503）；用 `unity status --until-ready` 等它就绪。
- 出错（超时、Safe Mode、连不上）如实报告，不要循环重试、换别的 Editor，也不要手改 `.prefab` / `.meta` / `.unity` 文本。

## seedance-ui-to-unity

把 Seedance ui-maker 产物（`layout.json` + `slices/`，或 `ui.zip`）导入成 UGUI Prefab。

| 路径 | 内容 |
|---|---|
| `.claude/skills/seedance-ui-to-unity/SKILL.md` | 导入步骤（用户可见行为以这里为准） |
| `.claude/skills/seedance-ui-to-unity/SeedanceUiImporter/` | **C# 包源码唯一真源**：`Editor/`（导入逻辑、记录读写）、`Editor.Cli/`（`unity command` 注册） |
| `.claude/skills/seedance-ui-to-unity/scripts/install_package.sh` | 把包同步到目标工程 `Assets/SeedanceUiImporter/`（rsync，缺 rsync 时 cp+find 降级） |
| `.claude/skills/seedance-ui-to-unity/scripts/uninstall_package.sh` | 导入结束后删掉 `Assets/SeedanceUiImporter/`（工程里不留 Seedance 代码） |
| `.claude/skills/seedance-ui-to-unity/scripts/fetch_font.sh` | 下载默认中文字体 Noto Sans CJK SC 到 `~/.cache/seedance-ui-to-unity/` |
| `unity/SeedanceUiExample/Assets/Tests/SeedanceUI/` | EditMode 测试（`Seedance.UiImport.Tests`） |
| `unity/SeedanceUiExample/TestFixtures/` | 测试用产物（`char-panel/`、`char-panel.zip`） |
| `unity/SeedanceUiExample/tools/` | `failures.py`（解析测试结果）、`gen_meta.py`（补 .meta）、`test_install_package.sh` |
| `docs/superpowers/specs/2026-10-21-seedance-ui-to-unity-design.md` | 设计 |
| `docs/superpowers/specs/2026-11-04-seedance-ui-no-residue-design.md` | 不留代码、不挂脚本、记录文件（取代前者里标记组件的部分） |

### 约定

- **只改 skill 目录里的源码**。`unity/SeedanceUiExample/Assets/SeedanceUiImporter/` 是 `install_package.sh` 拷进去的副本（已 gitignore），改了会被下次同步覆盖。测试工程的测试依赖这个包，所以测试工程里不删包。
- **Prefab 上不挂 Seedance 组件**：重导所需的结构信息存在目标工程 `Assets/SeedanceUI/<Prefab 路径去掉 Assets/>.json`，节点按 Prefab 内 local fileID 对应，记录按 Prefab GUID 查找。导入代码不能引入任何运行时（非 Editor）程序集。
- **.meta 的 GUID 固定**：随源码提交，不随意改（推包结果稳定）。新增文件后跑 `python3 unity/SeedanceUiExample/tools/gen_meta.py .claude/skills/seedance-ui-to-unity/SeedanceUiImporter` 补 meta。
- **pipeline 版本兼容**：`Editor.Cli` 的 asmdef 用 versionDefines 定义 `SEEDANCE_HAS_PIPELINE` / `SEEDANCE_PIPELINE_GROUPS`；≥0.9 注册分组命令 `seedance ui_import`，0.8 注册 `seedance_ui_import`，新增命令两种写法都要加。没装 pipeline 时 `Editor.Cli` 不编译，其余代码不能依赖它。
- 导入规则（ASCII 命名、重导只更新 Sprite/位置/尺寸、不删节点只设 inactive、字体共用等）见 `SKILL.md` 与 spec，改行为时两处一起更新。
- 拒绝在 Play Mode 下导入。

### 测试

```bash
bash unity/SeedanceUiExample/run-tests.sh                   # 全部 EditMode 测试
bash unity/SeedanceUiExample/run-tests.sh UiGeometryTests   # 只跑某个 UI 测试类（模型测试用完整名，见下文）
bash unity/SeedanceUiExample/tools/test_install_package.sh  # 两个 skill 的推包 / 删包脚本测试
```

- 跑之前**先关掉该工程的 Editor**（batch mode 会被工程锁挡住）。
- 脚本会先推包、下字体，再跑 `unity test`。退出码：0 全过 / 8 有失败 / 6 没出结果 / 9 有用例被跳过（跳过视为失败）。
- 编译错误看 `unity/SeedanceUiExample/Logs/test-editor.log`；失败详情 `python3 unity/SeedanceUiExample/tools/failures.py`。

## seedance-3dmodel-to-unity

把 Tripo / 混元的绑骨 FBX 产物（zip 或目录：一个 `.fbx` + `Textures/`）做成挂好 Animator 的角色 Prefab。

| 路径 | 内容 |
|---|---|
| `.claude/skills/seedance-3dmodel-to-unity/SKILL.md` | 导入步骤（用户可见行为以这里为准） |
| `.claude/skills/seedance-3dmodel-to-unity/SeedanceModelImporter/` | **C# 包源码唯一真源**：`Editor/`（ModelSource、TextureWriter、FbxWriter、MaterialBuilder、ControllerBuilder、ModelPrefabBuilder、ViewerScene、ModelCapture、记录读写）、`Editor.Cli/`（`model_import` / `model_capture`） |
| `.claude/skills/seedance-3dmodel-to-unity/scripts/` | `install_package.sh` / `uninstall_package.sh`（目标工程 `Assets/SeedanceModelImporter/`） |
| `unity/SeedanceUiExample/Assets/Tests/SeedanceModel/` | EditMode 测试（`Seedance.ModelImport.Tests`） |
| `unity/SeedanceUiExample/TestFixtures/rig-tripo/`、`rig-tripo.zip` | Tripo 样例产物（混元样例 55 MB，不进仓库） |
| `docs/superpowers/specs/2026-11-04-seedance-3dmodel-to-unity-design.md` | 设计 |

### 约定

- 与 UI 包同样的规则：只改 skill 目录里的源码；Prefab / Controller 上不挂 Seedance 东西；记录在 `Assets/SeedanceModel/`；.meta GUID 固定（`gen_meta.py .claude/skills/seedance-3dmodel-to-unity/SeedanceModelImporter`）；pipeline 0.8 / ≥0.9 两种命令写法都要加。
- 两个包互不引用（程序集 `Seedance.ModelImport.*` / `Seedance.UiImport.*`），可以同时在一个工程里；测试工程两个都装。
- 实测结论写在 `FbxWriter.cs` / `TextureWriter.cs` 顶部注释：两家 FBX Y 朝上、fileScale 0.01、带一个残留 Icosphere；两家面朝方向不同（Tripo +X、混元 -Z），按左右骨骼算出后转 Model 子节点；Tripo 金属度图是 glTF ORM（要转）；样例动画本身已是原地动画。

### 测试

```bash
bash unity/SeedanceUiExample/run-tests.sh Seedance.ModelImport.Tests              # 只跑模型导入的测试
bash unity/SeedanceUiExample/run-tests.sh Seedance.ModelImport.Tests.FbxWriterTests
```
