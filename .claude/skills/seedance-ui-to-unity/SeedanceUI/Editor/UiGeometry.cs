// layout.json 的画布绝对坐标（左上原点、y 向下）→ RectTransform。
// 子节点一律锚在父节点左上，pivot 也在左上，所以 anchoredPosition 就是相对父元素左上角的偏移（y 取负）。

using UnityEngine;

namespace Seedance.UiImport.Editor
{
    public struct LocalPlacement
    {
        public Vector2 anchoredPosition;
        public Vector2 size;
    }

    public static class UiGeometry
    {
        static readonly Vector2 TopLeft = new Vector2(0, 1);

        /// <summary>parentOrNull 为 null 表示挂在根下，父的左上角就是画布原点</summary>
        public static LocalPlacement Place(UiRect self, UiRect parentOrNull)
        {
            int px = parentOrNull?.x ?? 0, py = parentOrNull?.y ?? 0;
            return new LocalPlacement
            {
                anchoredPosition = new Vector2(self.x - px, -(self.y - py)),
                size = new Vector2(self.w, self.h),
            };
        }

        public static void Apply(RectTransform rt, LocalPlacement p)
        {
            rt.anchorMin = TopLeft;
            rt.anchorMax = TopLeft;
            rt.pivot = TopLeft;
            rt.sizeDelta = p.size;
            rt.anchoredPosition = p.anchoredPosition;
        }

        /// <summary>Prefab 根：四边拉伸铺满父节点（展示场景里就是参考分辨率等于设计分辨率的 Canvas）</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>CanvasScaler 的 matchWidthOrHeight：横屏按宽、竖屏按高</summary>
        public static float MatchFor(int width, int height) => width >= height ? 0f : 1f;
    }
}
