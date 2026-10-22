#!/usr/bin/env bash
# 拷包 + 跑 EditMode 测试。用法：run-tests.sh [测试类名]
# Editor 日志写到 Logs/test-editor.log（编译错误看这里）。
# 跑之前先关掉本工程的 Editor（unity test 走 batch mode，工程被打开时会被锁住）。
set -uo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
filter="${1:-}"
bash "$DIR/../../.claude/skills/seedance-ui-to-unity/scripts/install_package.sh" "$DIR" >/dev/null || exit 1
unity --no-banner test "$DIR" --mode EditMode --output "$DIR/test-results.xml" ${filter:+--filter "Seedance.UiImport.Tests.$filter"} -- -logFile "$DIR/Logs/test-editor.log"
