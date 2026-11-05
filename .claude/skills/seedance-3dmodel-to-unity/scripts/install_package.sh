#!/usr/bin/env bash
# 把 SeedanceModelImporter 包源码同步到（有 rsync 用 rsync，没有就用 cp + find） Unity 工程的 Assets/SeedanceModelImporter/（含固定 GUID 的 .meta）。
# 这是临时目录：导入流程结束后由 uninstall_package.sh 删掉。导入记录在 Assets/SeedanceKlab/Model/，这里不碰；
# 旧版的记录目录 Assets/SeedanceModel/ 会先迁过去。
# 与 seedance-ui-to-unity 的包互不影响，可以同时在工程里。
# 用法：install_package.sh <unity 工程路径>
set -euo pipefail
project="${1:-}"
if [ -z "$project" ] || [ ! -d "$project/Assets" ]; then
  echo "用法：install_package.sh <unity 工程路径>（该路径下必须有 Assets/）" >&2
  exit 1
fi
# 导入记录目录从旧位置 Assets/<旧名>/ 迁到 Assets/SeedanceKlab/<子目录>/（连同 .meta，GUID 不变）。
# 旧目录里没有记录就直接删；新目录已有记录而旧目录也有，说明旧记录多半是从版本库回来的，拒绝并让用户手动合并。
migrate_records() { # $1 = 旧目录名，$2 = SeedanceKlab 下的子目录名
  local old="$project/Assets/$1" new="$project/Assets/SeedanceKlab/$2"
  [ -d "$old" ] || return 0
  if [ -z "$(find "$old" -type f ! -name '*.meta' -print -quit)" ]; then
    rm -rf "$old" "$old.meta"
    return 0
  fi
  if [ -d "$new" ] && [ -n "$(find "$new" -type f ! -name '*.meta' -print -quit)" ]; then
    echo "Assets/$1/ 和 Assets/SeedanceKlab/$2/ 里都有导入记录（旧目录多半是从版本库回来的）。请把 Assets/$1/ 下的记录连同 .meta 手动并进 Assets/SeedanceKlab/$2/、删掉 Assets/$1/ 后再导入" >&2
    exit 1
  fi
  rm -rf "$new" "$new.meta"
  mkdir -p "$project/Assets/SeedanceKlab"
  mv "$old" "$new"
  if [ -e "$old.meta" ]; then mv "$old.meta" "$new.meta"; fi
  echo "导入记录已从 Assets/$1/ 迁到 Assets/SeedanceKlab/$2/"
}
src="$(cd "$(dirname "$0")/.." && pwd)/SeedanceModelImporter/"
dst="$project/Assets/SeedanceModelImporter"
migrate_records SeedanceModel Model
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
cp "$(dirname "$src")/SeedanceModelImporter.meta" "$project/Assets/SeedanceModelImporter.meta"
echo "已同步到 $project/Assets/SeedanceModelImporter/"
