# 坐标、字号、颜色换算。容器锚在父容器左上，偏移 = 自己的 rect 减父元素的 rect；根下元素相对设计画布原点。

import math

DEFAULT_FONT_PX = 36
PX_TO_UMG = 0.75  # UMG 字号是 96 DPI 下的点：点 = 像素 × 72 / 96
WHITE = (1.0, 1.0, 1.0, 1.0)


def slot_offsets(el, parent_el):
    x, y, w, h = el["rect"]
    px, py = (parent_el["rect"][0], parent_el["rect"][1]) if parent_el else (0, 0)
    return (x - px, y - py, w, h)


def font_size(px):
    if isinstance(px, (int, float)) and px > 0:
        return (float(px) * PX_TO_UMG, None)
    return (DEFAULT_FONT_PX * PX_TO_UMG, "没有字号，用了 %d 像素" % DEFAULT_FONT_PX)


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else math.pow((c + 0.055) / 1.055, 2.4)


def parse_color(s):
    """#RGB / #RRGGBB / #RRGGBBAA → (线性 r, g, b, a)。解析不了返回白色和一条警告"""
    h = s.strip().lstrip("#") if isinstance(s, str) else ""
    if len(h) == 3:
        h = "".join(ch * 2 for ch in h)
    if len(h) not in (6, 8) or any(ch not in "0123456789abcdefABCDEF" for ch in h):
        return (WHITE, "颜色 %s 解析不了，用了白色" % s)
    vals = [int(h[i:i + 2], 16) / 255.0 for i in range(0, len(h), 2)]
    r, g, b = (_srgb_to_linear(v) for v in vals[:3])
    a = vals[3] if len(vals) == 4 else 1.0
    return ((r, g, b, a), None)


def text_anchor(align):
    """TextBlock 在容器里的锚点 / Alignment：水平按对齐，垂直居中"""
    return ({"left": 0.0, "right": 1.0}.get(align, 0.5), 0.5)


def justification(align):
    return {"left": "Left", "right": "Right"}.get(align, "Center")
