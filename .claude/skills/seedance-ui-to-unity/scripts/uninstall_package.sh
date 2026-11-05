#!/usr/bin/env bash
# 删掉 install_package.sh 推进工程的 Assets/SeedanceUiImporter/ 与它的 .meta。Assets/SeedanceUI/（导入记录）不碰。
# 没装过也算成功。只删文件；让 Editor 刷新由调用方用 unity 命令做。
# 用法：uninstall_package.sh <unity 工程路径>
set -euo pipefail
project="${1:-}"
if [ -z "$project" ] || [ ! -d "$project/Assets" ]; then
  echo "用法：uninstall_package.sh <unity 工程路径>（该路径下必须有 Assets/）" >&2
  exit 1
fi
dst="$project/Assets/SeedanceUiImporter"
if [ ! -e "$dst" ] && [ ! -e "$dst.meta" ]; then
  echo "没有安装，跳过"
  exit 0
fi
rm -rf "$dst" "$dst.meta"
echo "已删除 $dst/"
