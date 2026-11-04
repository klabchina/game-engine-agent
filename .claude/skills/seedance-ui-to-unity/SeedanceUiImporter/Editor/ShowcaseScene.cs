// 展示场景 Assets/Product/Scene/UIShowcase.unity：每个工程一个（一个项目的设计分辨率、横竖屏是固定的）。
// 首次导入时创建，Canvas 的参考分辨率取这次的设计分辨率；之后每次导入把 Prefab 实例放进去，只让本次的那个显示。
// 以 Additive 打开、改完保存后关掉，不动用户当前打开的场景。

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Seedance.UiImport.Editor
{
    public static class ShowcaseScene
    {
        public const string ScenePath = "Assets/Product/Scene/UIShowcase.unity";

        /// <summary>展示场景与离屏截图共用的底色（深灰，透明切图在上面看得清）</summary>
        public static readonly Color BackgroundColor = new Color(0.18f, 0.18f, 0.2f);

        /// <summary>没有任何对象的场景文件，Unity 能直接打开</summary>
        internal const string EmptySceneYaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

        public static void Show(GameObject prefabAsset, int designWidth, int designHeight, ImportReport report)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : CreateScene(designWidth, designHeight);
                openedHere = true;
            }
            try
            {
                var canvas = FindCanvas(scene);
                if (canvas == null) throw new UiImportException($"{ScenePath} 里找不到 Canvas");
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.referenceResolution != new Vector2(designWidth, designHeight))
                    report.warnings.Add($"展示场景的参考分辨率是 {scaler.referenceResolution.x}×{scaler.referenceResolution.y}，" +
                                        $"这套 UI 是 {designWidth}×{designHeight}，场景没改，显示会有缩放");

                GameObject instance = null;
                foreach (Transform child in canvas.transform)
                {
                    if (child.GetComponent<SeedanceUiRoot>() == null) continue;
                    var isThis = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefabAsset;
                    if (isThis && instance == null) instance = child.gameObject;
                    child.gameObject.SetActive(isThis);
                }
                if (instance == null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
                    instance.transform.SetParent(canvas.transform, false);
                }
                instance.SetActive(true);

                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new UiImportException($"保存展示场景失败：{ScenePath}");
                report.scenePath = ScenePath;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static Canvas FindCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<Canvas>(true);
                if (c != null && c.isRootCanvas) return c;
            }
            return null;
        }

        static Scene CreateScene(int designWidth, int designHeight)
        {
            // 编辑器里有未保存的未命名场景时，EditorSceneManager.NewScene(Additive) 会直接抛异常（用户 Editor 和测试运行器都常见），
            // 所以先落一个空场景文件再以 Additive 打开；保存时 Unity 会写回完整内容
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            File.WriteAllText(ScenePath, EmptySceneYaml);
            AssetDatabase.ImportAsset(ScenePath);
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (active.IsValid()) SceneManager.SetActiveScene(active);

            var cam = new GameObject("Main Camera", typeof(Camera));
            var camera = cam.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            SceneManager.MoveGameObjectToScene(cam, scene);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(designWidth, designHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UiGeometry.MatchFor(designWidth, designHeight);
            SceneManager.MoveGameObjectToScene(canvasGo, scene);

            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent(InputModuleType());
            SceneManager.MoveGameObjectToScene(es, scene);
            return scene;
        }

        /// <summary>
        /// 工程开了新 Input System 就用它的 UI 模块（只开新 Input System 时 StandaloneInputModule 会每帧报错），
        /// 否则用 StandaloneInputModule。用反射取类型，不让本包硬依赖 com.unity.inputsystem。
        /// </summary>
        static Type InputModuleType()
        {
#if ENABLE_INPUT_SYSTEM
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) return t;
#endif
            return typeof(StandaloneInputModule);
        }
    }
}
