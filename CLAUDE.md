# CLAUDE.md

用 Claude 通过命令行驱动游戏引擎（Unity / Unreal）的 skill 集合，以插件市场形式发布。目标与进度见 `README.md`。

## 目录

| 路径 | 内容 |
|---|---|
| `.claude/skills/<skill>/` | 各 skill 本体，也是插件市场里对应插件的根目录 |
| `.claude-plugin/marketplace.json` | 插件市场清单（改了插件记得同步 `version`） |
| `unity/` `unreal/` | 各引擎的开发 / 测试工程 |
| `docs/engine/` | 引擎相关的开发说明 |
| `docs/superpowers/` | 设计（specs）与实现计划（plans） |

## 引擎文档

动到某个引擎相关的代码前，先读对应文档：

- Unity：`docs/engine/unity.md`（CLI 规则、seedance-ui-to-unity 与 seedance-3dmodel-to-unity 的约定与测试）
- Unreal：`docs/engine/unreal.md`（MCP 规则与工具速查、seedance-ui-to-unreal 的约定与测试）。开发 / 测试工程 `unreal/SeedanceExample/`；`unreal/` 下的二进制资源走 Git LFS，clone 前要装 Git LFS

## 约定

- 文档、注释、提交信息用中文；提交前缀 `feat:` / `fix:` / `refactor:` / `docs:`。
