# game-engine-agent

## 目标

- [ ] 通过 Unity CLI 测试场景性能指标，并分析出原因
- [x] 通过 Unity CLI 直接创建 UI 界面（skill：`.claude/skills/seedance-ui-to-unity/`）
- [x] 通过 Unity CLI 把 Tripo / 混元的绑骨 FBX 做成带 Animator 的角色 Prefab（skill：`.claude/skills/seedance-3dmodel-to-unity/`）
- [ ] 通过 Unreal 命令行测试关卡性能指标，并分析出原因
- [x] 通过 Unreal（unreal-mcp）直接创建 UI 界面（UMG）（skill：`.claude/skills/seedance-ui-to-unreal/`）

### UI 界面创建工作流

```
Seedance 美术管线：UI 整体切图
        ↓
导出切图 + 对应结构的 JSON
        ↓
本 agent（通过 Unity CLI）
        ↓
自动导入为 Unity 场景 or Prefab
```

1. **Seedance 美术管线**：对 UI 整体稿进行切图
2. **导出**：切图资源，以及描述界面层级结构的 JSON
3. **本 agent**：读取切图与 JSON，通过 Unity CLI 驱动 Editor
4. **Unity**：自动导入，生成场景或 Prefab

#### 安装 skill（插件市场）

需要能访问本仓库（GitHub `klabchina/game-engine-agent`）。在 Claude Code 里：

```
/plugin marketplace add klabchina/game-engine-agent
/plugin install seedance-ui-to-unity@game-engine-agent
/plugin install seedance-3dmodel-to-unity@game-engine-agent
/plugin install seedance-ui-to-unreal@game-engine-agent
```

更新：`/plugin marketplace update game-engine-agent`。机器上要有 Unity CLI（`unity`）和 Unity 6 Editor；目标工程不用提前准备。

实现：skill `seedance-ui-to-unity`（`.claude/skills/seedance-ui-to-unity/SKILL.md`），对 Claude 说「把这份 ui-maker 产物导入 Unity 工程 X」即可。设计见 `docs/superpowers/specs/2026-10-21-seedance-ui-to-unity-design.md`，开发 / 测试工程在 `unity/SeedanceUiExample/`（跑测试：`bash unity/SeedanceUiExample/run-tests.sh`，先关掉该工程的 Editor）。

Unreal：skill `seedance-ui-to-unreal`（`.claude/skills/seedance-ui-to-unreal/SKILL.md`），对 Claude 说「把这份 ui-maker 产物导入 UE 工程 X 做成 UMG」即可，生成 Widget Blueprint。需要 UE 5.8 编辑器开着目标工程、启用 ModelContextProtocol 与 AllToolsets 插件（MCP 服务器地址写在工程的 `.mcp.json`），本机有 Python 3。设计见 `docs/superpowers/specs/2026-11-10-seedance-ui-to-unreal-design.md`，开发 / 测试工程在 `unreal/SeedanceExample/`（离线单测 `bash unreal/SeedanceExample/run-tests.sh`；端到端 `bash unreal/SeedanceExample/run-e2e.sh`，要开着该工程的编辑器）。

模型导入：skill `seedance-3dmodel-to-unity`（`.claude/skills/seedance-3dmodel-to-unity/SKILL.md`），对 Claude 说「把这个 Tripo / 混元的 FBX 产物导入 Unity 工程 X」即可。设计见 `docs/superpowers/specs/2026-11-04-seedance-3dmodel-to-unity-design.md`，测试与 UI 共用 `unity/SeedanceUiExample/`。

## 可用 Unity Editor 工具 —— 160 个

Unity CLI 参考文档:<https://docs.unity.com/zh-cn/unity-cli/unity-cli-reference>

来源:`unity list`(Unity CLI `1.0.0-beta.13`,经 `com.unity.pipeline` 0.8.0-exp.1 连接 Editor)。
`unity command` 的 tag 汇总为 47 个分组。主要类别:

| 类别 | 代表工具 |
|---|---|
| 场景 (9) | `open_scene` `save_scene` `get_scene_hierarchy` `list_open_scenes` `set_active_scene` |
| GameObject (16) | `create_gameobject(s)` `find_gameobjects` `set_transform` `set_parent` `rename_gameobject` |
| 组件 (6) | `add_component` `remove_component` `get/set_component_properties` `get_serialized_fields` |
| Prefab (7) | `create_prefab` `create_prefab_variant` `instantiate_prefab` `apply/revert_prefab_overrides` `unpack_prefab` |
| 脚本 (14) | `create_script` `attach_script` `eval` `eval_file` `run_script` `recompile` `recompile_status` |
| 资源 (12) | `find_assets` `create_asset` `import_asset` `move/copy/rename/delete_asset` `get/set_import_settings` |
| 动画 (14) | `create_animator_controller` `add_animator_layer/parameter/state/transition` `create_timeline` `add_timeline_track/clip` |
| 烘焙 (17) | `bake_lighting` `bake_navmesh` `bake_navmesh_surfaces` `bake_occlusion_culling` + 各自 status/cancel |
| 构建 (10) | `build` `build_status` `get/set_build_settings` `list_build_profiles` `list_build_targets` `switch_build_target` |
| 可观测 (8) | `console` `console_status` `clear_console` `get_performance_stats` `audit` `report_evals` |
| 设置 (10) | `get/set_` × `player` `graphics` `quality` `physics` `audio` `input` `time` `lighting` `navmesh` `tags_layers` |
| 捕获/运行时 (10) | `capture_game_view` `capture_scene_view` `screenshot` `editor_play/pause/stop/focus` `simulate_key` `simulate_pointer` `set_timescale` |
| 包管理 (6) | `package_list` `package_add` `package_remove` `package_search` `package_resolve` `package_status` |
| 测试 (4) | `run_tests` `list_tests` `test_status` `cancel_tests` |

> 注意:其中大部分是**写操作**(`set_*` / `create_*` / `delete_*` / `bake_*` / `build` / `eval`)。
> 只读查询工具包括 `editor_status` `list_open_scenes` `console` `console_status` `recompile_status`
> `get_*` 系列 `find_*` 系列 `list_*` 系列。
