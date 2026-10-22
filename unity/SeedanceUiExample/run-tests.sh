#!/usr/bin/env bash
# 拷包 + 跑 EditMode 测试。用法：run-tests.sh [测试类名]
# Editor 日志写到 Logs/test-editor.log（编译错误看这里）。
# 跑之前先关掉本工程的 Editor（unity test 走 batch mode，工程被打开时会被锁住）。
# 退出码：unity test 的退出码（0 全过 / 8 有失败 / 6 没跑出结果）；全过但有用例被跳过时为 9。
set -uo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
SKILL="$DIR/../../.claude/skills/seedance-ui-to-unity"
filter="${1:-}"
bash "$SKILL/scripts/install_package.sh" "$DIR" >/dev/null || exit 1
# 字体相关的测试依赖默认字体缓存，没有就先下载；下载失败照常跑，由下面的跳过检查报出来
bash "$SKILL/scripts/fetch_font.sh" >/dev/null 2>&1 || echo "警告：默认字体下载失败，依赖字体的测试会被跳过" >&2
unity --no-banner test "$DIR" --mode EditMode --output "$DIR/test-results.xml" ${filter:+--filter "Seedance.UiImport.Tests.$filter"} -- -logFile "$DIR/Logs/test-editor.log"
code=$?
[ $code -ne 0 ] && exit $code
skipped=$(python3 "$DIR/tools/failures.py" "$DIR/test-results.xml" | head -1 | sed -n "s/.*'skipped': '\([0-9]*\)'.*/\1/p")
if [ "${skipped:-0}" != "0" ]; then
  python3 "$DIR/tools/failures.py" "$DIR/test-results.xml" | grep '^SKIP' >&2
  echo "有 $skipped 个用例被跳过，不算全过" >&2
  exit 9
fi
exit 0
