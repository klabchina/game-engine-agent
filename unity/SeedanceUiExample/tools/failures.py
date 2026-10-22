#!/usr/bin/env python3
"""打印 test-results.xml 的汇总与失败用例（名字 + 消息 + 栈顶几行）。用法：failures.py [xml]"""
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1] if len(sys.argv) > 1 else "unity/SeedanceUiExample/test-results.xml").getroot()
print({k: root.get(k) for k in ("total", "passed", "failed", "skipped")})
for tc in root.iter("test-case"):
    if tc.get("result") == "Failed":
        msg = (tc.findtext("failure/message") or "").strip()
        st = (tc.findtext("failure/stack-trace") or "").strip().splitlines()[:4]
        print("FAIL", tc.get("name"), "::", msg[:500])
        for l in st: print("   ", l.strip())
    elif tc.get("result") == "Skipped":
        print("SKIP", tc.get("name"), (tc.findtext("reason/message") or "").strip())
