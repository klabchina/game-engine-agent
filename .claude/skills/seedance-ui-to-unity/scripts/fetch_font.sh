#!/usr/bin/env bash
# 下载默认中文字体 Noto Sans CJK SC Regular（SIL OFL 1.1，可随游戏分发）到本机缓存。
# stdout 只打印字体文件的绝对路径；已下载过就直接打印。失败时退出码非 0。
set -euo pipefail
URL="https://github.com/notofonts/noto-cjk/raw/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf"
dir="${XDG_CACHE_HOME:-$HOME/.cache}/seedance-ui-to-unity"
file="$dir/NotoSansCJKsc-Regular.otf"
if [ -f "$file" ] && [ "$(wc -c < "$file")" -gt 1000000 ]; then
  echo "$file"
  exit 0
fi
mkdir -p "$dir"
tmp="$file.part"
curl -fL --retry 3 -o "$tmp" "$URL" >&2
if [ "$(wc -c < "$tmp")" -le 1000000 ]; then
  echo "下载的字体文件太小，可能不完整：$tmp" >&2
  exit 1
fi
mv "$tmp" "$file"
echo "$file"
