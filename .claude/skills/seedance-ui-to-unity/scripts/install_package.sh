#!/usr/bin/env bash
# 把 SeedanceUiImporter 包源码同步到（有 rsync 用 rsync，没有就用 cp + find） Unity 工程的 Assets/SeedanceUiImporter/（含固定 GUID 的 .meta）。
# 这是临时目录：导入流程结束后由 uninstall_package.sh 删掉。Assets/SeedanceUI/ 存的是导入记录，这里不碰，
# 只清掉旧版装在 Assets/SeedanceUI/ 下的 SDK（Editor/、Editor.Cli/、Runtime/），否则会和新包的程序集重名。
# 用法：install_package.sh <unity 工程路径>
set -euo pipefail
project="${1:-}"
if [ -z "$project" ] || [ ! -d "$project/Assets" ]; then
  echo "用法：install_package.sh <unity 工程路径>（该路径下必须有 Assets/）" >&2
  exit 1
fi
src="$(cd "$(dirname "$0")/.." && pwd)/SeedanceUiImporter/"
dst="$project/Assets/SeedanceUiImporter"
for d in Editor Editor.Cli Runtime; do
  rm -rf "$project/Assets/SeedanceUI/$d" "$project/Assets/SeedanceUI/$d.meta"
done
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
cp "$(dirname "$src")/SeedanceUiImporter.meta" "$project/Assets/SeedanceUiImporter.meta"
echo "已同步到 $project/Assets/SeedanceUiImporter/"
