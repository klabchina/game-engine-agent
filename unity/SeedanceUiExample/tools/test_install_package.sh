#!/usr/bin/env bash
# install_package.sh 的测试：有 rsync、没有 rsync（只给最基本的命令）两种环境，结果都要和源码完全一致，旧文件要被删掉。
# 用法：bash unity/SeedanceUiExample/tools/test_install_package.sh
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
skill="$here/../../../.claude/skills/seedance-ui-to-unity"
src="$skill/SeedanceUI"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
fail=0

# 没有 rsync 的 PATH：只放脚本可能用到的基础命令
nobin="$tmp/bin"
mkdir -p "$nobin"
for c in bash sh cp rm mkdir find dirname cd pwd cmp echo cat mv ls test sed tr; do
  p="$(command -v "$c" 2>/dev/null)" && [ -x "$p" ] && ln -sf "$p" "$nobin/$c"
done

check() { # $1 = 名字，$2 = PATH
  local proj="$tmp/$1"
  mkdir -p "$proj/Assets/SeedanceUI/Old"
  echo stale > "$proj/Assets/SeedanceUI/Old/Stale.cs"
  echo stale > "$proj/Assets/SeedanceUI/Editor.meta"
  if ! PATH="$2" bash "$skill/scripts/install_package.sh" "$proj" >/dev/null 2>"$tmp/$1.err"; then
    echo "FAIL $1: 脚本退出非 0：$(cat "$tmp/$1.err")"; fail=1; return
  fi
  if ! diff -r "$src" "$proj/Assets/SeedanceUI" >/dev/null; then echo "FAIL $1: 内容和源码不一致"; diff -rq "$src" "$proj/Assets/SeedanceUI" | head -5; fail=1; return; fi
  if ! cmp -s "$skill/SeedanceUI.meta" "$proj/Assets/SeedanceUI.meta"; then echo "FAIL $1: 包根目录 meta 不对"; fail=1; return; fi
  echo "ok $1"
}

check with-rsync "$PATH"
check without-rsync "$nobin"
exit $fail
