#!/usr/bin/env bash
# seedance-ui-to-unreal 的离线单测（不需要开编辑器）。用法：run-tests.sh [测试模块名，如 test_layout]
# 退出码：0 全过，非 0 有失败。
set -uo pipefail
DIR="$(cd "$(dirname "$0")" && (pwd -W 2>/dev/null || pwd))"
EDITOR_SRC="$DIR/../../.claude/skills/seedance-ui-to-unreal/editor"
PY=python
"$PY" -c "" 2>/dev/null || PY=python3
cd "$DIR/Tests/SeedanceUI"
export PYTHONPATH="$EDITOR_SRC"
export PYTHONIOENCODING=utf-8
if [ -n "${1:-}" ]; then
  "$PY" -m unittest -v "$1"
else
  "$PY" -m unittest discover -s . -p 'test_*.py' -v
fi
