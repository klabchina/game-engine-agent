"""测试数据：TestFixtures 下的两个 ui-maker 产物。"""

import json
import os
import zipfile

FIXTURES = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "TestFixtures"))


def zip_path(name):
    """name: ui-popup / ui-status"""
    return os.path.join(FIXTURES, name + ".zip")


def layout_text(name):
    with zipfile.ZipFile(zip_path(name)) as z:
        return z.read("layout.json").decode("utf-8")


def layout_dict(name):
    return json.loads(layout_text(name))
