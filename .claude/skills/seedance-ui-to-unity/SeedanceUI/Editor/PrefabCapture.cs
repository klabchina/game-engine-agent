// 把 Prefab 放进一个预览场景（不进 Hierarchy、不影响用户打开的场景），按设计分辨率离屏渲染成 PNG，
// 供 skill 和 ui-maker 的 preview.png 并排对照。比截 Game 视图稳：尺寸不受 Game 窗口大小、焦点影响。

using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Seedance.UiImport.Editor
{
    public static class PrefabCapture
    {
        public static void Capture(GameObject prefabAsset, int width, int height, string pngPath)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                var camGo = new GameObject("CaptureCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.GetComponent<Camera>();
                cam.scene = scene;
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = ShowcaseScene.BackgroundColor;
                cam.orthographic = true;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.targetTexture = rt;

                var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(width, height);
                scaler.matchWidthOrHeight = UiGeometry.MatchFor(width, height);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
                inst.transform.SetParent(canvasGo.transform, false);
                inst.SetActive(true);

                Canvas.ForceUpdateCanvases();
                foreach (var t in inst.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var dir = Path.GetDirectoryName(Path.GetFullPath(pngPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
                RenderTexture.ReleaseTemporary(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
