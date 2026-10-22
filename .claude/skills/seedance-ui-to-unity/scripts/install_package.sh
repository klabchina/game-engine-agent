#!/usr/bin/env bash
# 把 SeedanceUI 包源码同步到（有 rsync 用 rsync，没有就用 cp + find） Unity 工程的 Assets/SeedanceUI/（含固定 GUID 的 .meta）。
# 用法：install_package.sh <unity 工程路径>
set -euo pipefail
project="${1:-}"
if [ -z "$project" ] || [ ! -d "$project/Assets" ]; then
  echo "用法：install_package.sh <unity 工程路径>（该路径下必须有 Assets/）" >&2
  exit 1
fi
src="$(cd "$(dirname "$0")/.." && pwd)/SeedanceUI/"
dst="$project/Assets/SeedanceUI"
if command -v rsync >/dev/null 2>&1; then
  rsync -a --delete "$src" "$dst/"
else
  # 没有 rsync（如 Windows 的 Git Bash）：先删掉目标里源码已经没有的文件 / 目录，再整体拷过去
  mkdir -p "$dst"
  (cd "$dst" && find . -mindepth 1 -depth) | while IFS= read -r rel; do
    [ -e "$src$rel" ] || rm -rf "$dst/$rel"
  done
  cp -R "$src". "$dst/"
fi
# 目录本身的 meta，GUID 固定
cp "$(dirname "$src")/SeedanceUI.meta" "$project/Assets/SeedanceUI.meta"
echo "已同步到 $project/Assets/SeedanceUI/"
