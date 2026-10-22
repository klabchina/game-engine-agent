#!/usr/bin/env python3
"""给 SeedanceUI 包源码里还没有 .meta 的文件 / 目录补一份，GUID 随机生成一次后随源码提交、永不再改。
用法：python3 gen_meta.py <SeedanceUI 目录>"""
import sys, uuid
from pathlib import Path

MONO = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
ASMDEF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def template(p: Path) -> str:
    if p.is_dir():
        return FOLDER
    if p.suffix == ".cs":
        return MONO
    if p.suffix == ".asmdef":
        return ASMDEF
    raise SystemExit(f"不认识的文件类型：{p}")

root = Path(sys.argv[1]).resolve()
for p in [root, *sorted(root.rglob("*"))]:
    if p.suffix == ".meta" or p.name == ".DS_Store":
        continue
    meta = p.with_name(p.name + ".meta")
    if not meta.exists():
        meta.write_text(template(p).format(guid=uuid.uuid4().hex))
        print("生成", meta)
