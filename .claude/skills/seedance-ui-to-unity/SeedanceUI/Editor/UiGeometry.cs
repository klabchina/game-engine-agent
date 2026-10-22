// layout.json 的画布绝对坐标（左上原点、y 向下）→ RectTransform。
// 嵌套元素锚在父元素左上，pivot 也在左上，所以 anchoredPosition 就是相对父元素左上角的偏移（y 取负）。
// 直接挂在 Prefab 根下的元素（通常是整块面板）锚点和 pivot 都在中心，相对根居中，换屏幕比例时不会偏到一边。

using UnityEngine;

namespace Seedance.UiImport.Editor
{
    public struct LocalPlacement
    {
        public Vector2 anchoredPosition;
        public Vector2 size;
        public Vector2 anchor;
        public Vector2 pivot;
    }

    public static class UiGeometry
    {
        static readonly Vector2 TopLeft = new Vector2(0, 1);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        /// <summary>parentOrNull 为 null 表示挂在根下，父的左上角就是画布原点</summary>
        public static LocalPlacement Place(UiRect self, UiRect parentOrNull)
        {
            int px = parentOrNull?.x ?? 0, py = parentOrNull?.y ?? 0;
            return new LocalPlacement
            {
                anchoredPosition = new Vector2(self.x - px, -(self.y - py)),
                size = new Vector2(self.w, self.h),
                anchor = TopLeft,
                pivot = TopLeft,
            };
        }

        /// <summary>根下的元素：中心相对画布中心的偏移</summary>
        public static LocalPlacement PlaceCentered(UiRect self, int canvasWidth, int canvasHeight)
        {
            return new LocalPlacement
            {
                anchoredPosition = new Vector2(self.x + self.w / 2f - canvasWidth / 2f, -(self.y + self.h / 2f - canvasHeight / 2f)),
                size = new Vector2(self.w, self.h),
                anchor = Center,
                pivot = Center,
            };
        }

        public static void Apply(RectTransform rt, LocalPlacement p)
        {
            rt.anchorMin = p.anchor;
            rt.anchorMax = p.anchor;
            rt.pivot = p.pivot;
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
