// 3DViewer 相机与离屏截图共用的取景：从模型正面偏右上方看过去，把整个包围盒框进画面。

using System.Linq;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    static class ViewFraming
    {
        /// <summary>激活的 Renderer 合起来的包围盒；没有 Renderer 时是以根为中心的 1 m 盒</summary>
        public static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
            if (rs.Length == 0) return new Bounds(go.transform.position + Vector3.up * 0.5f, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static void Frame(Camera cam, Bounds b)
        {
            var dir = Quaternion.Euler(-12f, 20f, 0f) * Vector3.forward; // 正面（+Z）偏右 20°、略高
            var radius = Mathf.Max(b.extents.magnitude, 0.1f);
            var dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            cam.transform.position = b.center + dir * dist;
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
            cam.farClipPlane = dist + radius * 4f + 100f;
        }
    }
}
