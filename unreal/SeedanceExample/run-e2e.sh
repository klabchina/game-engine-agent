#!/usr/bin/env bash
# seedance-ui-to-unreal 端到端测试：SeedanceExample 的编辑器要开着（MCP 在 .mcp.json 的地址上）。
# 用法：run-e2e.sh [截图输出目录]（默认 Saved/SeedanceE2E）。约 3～4 分钟。退出码 0 全过。
set -uo pipefail
DIR="$(cd "$(dirname "$0")" && (pwd -W 2>/dev/null || pwd))"
PY=python
"$PY" -c "" 2>/dev/null || PY=python3
export MSYS_NO_PATHCONV=1 PYTHONIOENCODING=utf-8
"$PY" "$DIR/Tests/SeedanceUI/e2e_check.py" "$@"
