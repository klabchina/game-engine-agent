#!/usr/bin/env bash
# install_package.sh / uninstall_package.sh 的测试：有 rsync、没有 rsync（只给最基本的命令）两种环境。
# 推包后 Assets/SeedanceUiImporter/ 要和源码完全一致、旧文件被删、旧版装在 Assets/SeedanceUI/ 下的 SDK 被清掉、记录文件不动；
# 删包后 SDK 目录与 meta 都没了、记录文件还在，重复删包也成功。
# 用法：bash unity/SeedanceUiExample/tools/test_install_package.sh
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
skill="$here/../../../.claude/skills/seedance-ui-to-unity"
src="$skill/SeedanceUiImporter"
mskill="$here/../../../.claude/skills/seedance-3dmodel-to-unity"
msrc="$mskill/SeedanceModelImporter"
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
  local proj="$tmp/$1" d
  # 旧版 SDK（装在 Assets/SeedanceUI/ 下）+ 一份记录文件
  # 旧 SDK 目录以各自的 asmdef 为特征
  mkdir -p "$proj/Assets/SeedanceUI/Editor" "$proj/Assets/SeedanceUI/Editor.Cli" "$proj/Assets/SeedanceUI/Runtime"
  echo old > "$proj/Assets/SeedanceUI/Editor/Seedance.UiImport.Editor.asmdef"
  echo old > "$proj/Assets/SeedanceUI/Editor.Cli/Seedance.UiImport.Editor.Cli.asmdef"
  echo old > "$proj/Assets/SeedanceUI/Runtime/Seedance.UiImport.Runtime.asmdef"
  for d in Editor Editor.Cli Runtime; do
    echo old > "$proj/Assets/SeedanceUI/$d/X.cs"
    echo old > "$proj/Assets/SeedanceUI/$d.meta"
  done
  mkdir -p "$proj/Assets/SeedanceUI/UI"
  echo '{}' > "$proj/Assets/SeedanceUI/UI/A.json"
  # 另一个工程：Prefab 在 Assets/Runtime/、Assets/Editor/ 下，记录镜像到 Assets/SeedanceUI/Runtime/、Editor/，不能被当成旧 SDK 删掉
  local rproj="$tmp/$1-records"
  mkdir -p "$rproj/Assets/SeedanceUI/Runtime/UI" "$rproj/Assets/SeedanceUI/Editor"
  echo '{}' > "$rproj/Assets/SeedanceUI/Runtime/UI/X.json"
  echo '{}' > "$rproj/Assets/SeedanceUI/Editor/Y.json"
  if ! PATH="$2" bash "$skill/scripts/install_package.sh" "$rproj" >/dev/null 2>"$tmp/$1.err"; then
    echo "FAIL $1: install（记录工程）退出非 0：$(cat "$tmp/$1.err")"; fail=1; return
  fi
  if [ ! -f "$rproj/Assets/SeedanceUI/Runtime/UI/X.json" ] || [ ! -f "$rproj/Assets/SeedanceUI/Editor/Y.json" ]; then
    echo "FAIL $1: install 把 Assets/SeedanceUI/Runtime|Editor 下的记录当旧 SDK 删了"; fail=1; return
  fi
  # 新目录里的陈旧文件
  mkdir -p "$proj/Assets/SeedanceUiImporter/Old"
  echo stale > "$proj/Assets/SeedanceUiImporter/Old/Stale.cs"

  if ! PATH="$2" bash "$skill/scripts/install_package.sh" "$proj" >/dev/null 2>"$tmp/$1.err"; then
    echo "FAIL $1: install 退出非 0：$(cat "$tmp/$1.err")"; fail=1; return
  fi
  if ! diff -r "$src" "$proj/Assets/SeedanceUiImporter" >/dev/null; then echo "FAIL $1: 内容和源码不一致"; diff -rq "$src" "$proj/Assets/SeedanceUiImporter" | head -5; fail=1; return; fi
  if ! cmp -s "$skill/SeedanceUiImporter.meta" "$proj/Assets/SeedanceUiImporter.meta"; then echo "FAIL $1: 包根目录 meta 不对"; fail=1; return; fi
  for d in Editor Editor.Cli Runtime; do
    if [ -e "$proj/Assets/SeedanceUI/$d" ] || [ -e "$proj/Assets/SeedanceUI/$d.meta" ]; then echo "FAIL $1: 旧 SDK $d 没清掉"; fail=1; return; fi
  done
  if [ ! -f "$proj/Assets/SeedanceUI/UI/A.json" ]; then echo "FAIL $1: install 删了记录文件"; fail=1; return; fi

  local i
  for i in 1 2; do
    if ! PATH="$2" bash "$skill/scripts/uninstall_package.sh" "$proj" >/dev/null 2>"$tmp/$1.err"; then
      echo "FAIL $1: 第 $i 次 uninstall 退出非 0：$(cat "$tmp/$1.err")"; fail=1; return
    fi
  done
  if [ -e "$proj/Assets/SeedanceUiImporter" ] || [ -e "$proj/Assets/SeedanceUiImporter.meta" ]; then echo "FAIL $1: uninstall 没删干净"; fail=1; return; fi
  if [ ! -f "$proj/Assets/SeedanceUI/UI/A.json" ]; then echo "FAIL $1: uninstall 删了记录文件"; fail=1; return; fi

  # 模型包：和 UI 包同时推进同一个工程，各自完整；删模型包不碰 UI 包和模型记录
  local both="$tmp/$1-both"
  mkdir -p "$both/Assets/SeedanceModel"
  echo '{}' > "$both/Assets/SeedanceModel/X.json"
  for s in "$skill" "$mskill"; do
    if ! PATH="$2" bash "$s/scripts/install_package.sh" "$both" >/dev/null 2>"$tmp/$1.err"; then
      echo "FAIL $1: install（$(basename "$s")）退出非 0：$(cat "$tmp/$1.err")"; fail=1; return
    fi
  done
  if ! diff -r "$src" "$both/Assets/SeedanceUiImporter" >/dev/null; then echo "FAIL $1: 两包共存时 UI 包内容不对"; fail=1; return; fi
  if ! diff -r "$msrc" "$both/Assets/SeedanceModelImporter" >/dev/null; then echo "FAIL $1: 模型包内容和源码不一致"; fail=1; return; fi
  if ! cmp -s "$mskill/SeedanceModelImporter.meta" "$both/Assets/SeedanceModelImporter.meta"; then echo "FAIL $1: 模型包根目录 meta 不对"; fail=1; return; fi
  for i in 1 2; do
    if ! PATH="$2" bash "$mskill/scripts/uninstall_package.sh" "$both" >/dev/null 2>"$tmp/$1.err"; then
      echo "FAIL $1: 第 $i 次模型包 uninstall 退出非 0：$(cat "$tmp/$1.err")"; fail=1; return
    fi
  done
  if [ -e "$both/Assets/SeedanceModelImporter" ] || [ -e "$both/Assets/SeedanceModelImporter.meta" ]; then echo "FAIL $1: 模型包 uninstall 没删干净"; fail=1; return; fi
  if [ ! -d "$both/Assets/SeedanceUiImporter" ]; then echo "FAIL $1: 删模型包把 UI 包删了"; fail=1; return; fi
  if [ ! -f "$both/Assets/SeedanceModel/X.json" ]; then echo "FAIL $1: 删模型包把模型记录删了"; fail=1; return; fi
  echo "ok $1"
}

check with-rsync "$PATH"
check without-rsync "$nobin"
exit $fail
