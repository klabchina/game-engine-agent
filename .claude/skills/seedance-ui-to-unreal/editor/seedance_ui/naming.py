# 进 UE 的资产名、控件名一律 ASCII（上游元素名是中文，中文名存在导入记录的 sourceName）。
# 容器 <type>_<id>（CanvasPanel），显示控件 <type>_<id>_<控件>；贴图 T_<type>_<id>，同款元素（sameAs）用主元素的。

import re

from .errors import UiImportError

WBP_NAME_RE = re.compile(r"^[A-Za-z0-9_-]+$")
RECORD_ROOT = "/Game/SeedanceKlab/UI"

# type → (显示控件后缀, UMG 类路径)；没列出的类型按 Image
VIEW_KINDS = {
    "button": ("Button", "/Script/UMG.Button"),
    "tab": ("CheckBox", "/Script/UMG.CheckBox"),
    "bar": ("ProgressBar", "/Script/UMG.ProgressBar"),
    "text": ("Text", "/Script/UMG.TextBlock"),
}
IMAGE_KIND = ("Image", "/Script/UMG.Image")
CONTAINER_CLASS = "/Script/UMG.CanvasPanel"


def is_valid_wbp_name(s):
    return bool(s) and bool(WBP_NAME_RE.match(s))


def wbp_path(import_dir, wbp_name):
    d = (import_dir or "").replace("\\", "/").rstrip("/")
    if not d.startswith("/Game/"):
        raise UiImportError("导入目录必须以 /Game/ 开头：%s（在 Git Bash 里调用要加 MSYS2_ARG_CONV_EXCL='/Game/'）" % import_dir)
    name = wbp_name or d.rsplit("/", 1)[-1]
    if not is_valid_wbp_name(name):
        raise UiImportError("Widget Blueprint 名只能用英文字母、数字、_、-：%s%s"
                            % (name, "" if wbp_name else "（取自导入目录名，可以另传 WBP_NAME）"))
    return "%s/WBP_%s" % (d, name)


def texture_folder(import_dir):
    return import_dir.replace("\\", "/").rstrip("/") + "/Textures"


def texture_name(doc, el):
    master = doc["by_id"][el["same_as"]] if el["same_as"] else el
    return "T_%s_%s" % (master["type"], master["id"])


def container_name(el):
    return "%s_%s" % (el["type"], el["id"])


def view_class(etype):
    return VIEW_KINDS.get(etype, IMAGE_KIND)[1]


def view_name(el):
    if el["missing"]:
        return None
    return "%s_%s" % (container_name(el), VIEW_KINDS.get(el["type"], IMAGE_KIND)[0])


def record_file_path(wbp):
    """/Game/UI/X/WBP_X → /Game/SeedanceKlab/UI/UI/X/WBP_X.json（交给 AssetTools.read_file / write_file）"""
    return "%s/%s.json" % (RECORD_ROOT, wbp[len("/Game/"):])
