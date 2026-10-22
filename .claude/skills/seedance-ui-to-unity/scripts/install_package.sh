#!/usr/bin/env bash
# 把 SeedanceUI 包源码同步到 Unity 工程的 Assets/SeedanceUI/（含固定 GUID 的 .meta）。
# 用法：install_package.sh <unity 工程路径>
set -euo pipefail
project="${1:-}"
if [ -z "$project" ] || [ ! -d "$project/Assets" ]; then
  echo "用法：install_package.sh <unity 工程路径>（该路径下必须有 Assets/）" >&2
  exit 1
fi
src="$(cd "$(dirname "$0")/.." && pwd)/SeedanceUI/"
rsync -a --delete "$src" "$project/Assets/SeedanceUI/"
# 目录本身的 meta，GUID 固定
cp "$(dirname "$src")/SeedanceUI.meta" "$project/Assets/SeedanceUI.meta"
echo "已同步到 $project/Assets/SeedanceUI/"
