// 展示场景 Assets/Product/Scene/3DViewer.unity：首次导入时创建（地面、方向光、相机），之后每次导入把 Prefab 实例放到原点，
// 只让本次的那个显示（判断依据：实例的源 Prefab 有导入记录；程序手放的东西不碰），相机按它取景。
// 以 Additive 打开、改完保存后关掉，不动用户当前打开的场景。Play 后模型从 Controller 的默认状态（idle）开始播。

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seedance.ModelImport.Editor
{
    public static class ViewerScene
    {
        public const string ScenePath = "Assets/Product/Scene/3DViewer.unity";
        internal static readonly Func<Scene, string, bool> DefaultSaveScene = (scene, path) => EditorSceneManager.SaveScene(scene, path);
        /// <summary>测试替换用</summary>
        internal static Func<Scene, string, bool> SaveScene = DefaultSaveScene;
        const string EmptySceneYaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

        /// <summary>
        /// 把模型放进展示场景并保存。展示场景只是方便看，出任何问题都只记 warning、不让导入失败（Prefab 和记录这时都已经写好了）。
        /// 用户自己开着这个场景且有没保存的修改时：照样放好模型，但不保存（否则会把用户的修改一起存掉），留给用户自己存。
        /// </summary>
        public static void Show(GameObject prefabAsset, ModelImportReport report)
        {
            var scene = default(Scene);
            var openedHere = false;
            try
            {
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : CreateScene();
                    openedHere = true;
                }
                var userEdits = !openedHere && scene.isDirty;

                GameObject instance = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (!IsImportedModel(go)) continue;
                    var isThis = PrefabUtility.GetCorrespondingObjectFromSource(go) == prefabAsset;
                    if (isThis && instance == null) instance = go;
                    go.SetActive(isThis);
                }
                if (instance == null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
                    instance.transform.position = Vector3.zero;
                }
                instance.SetActive(true);

                var cam = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Camera>(true)).FirstOrDefault(c => c != null);
                if (cam != null) ViewFraming.Frame(cam, ViewFraming.Bounds(instance));

                if (userEdits)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    report.warnings.Add($"展示场景 {ScenePath} 开着且有没保存的修改，模型已经放进去了但没保存（免得把你的修改一起存掉），需要的话自己保存");
                }
                else if (!SaveScene(scene, ScenePath))
                {
                    report.warnings.Add($"展示场景 {ScenePath} 保存失败，没更新；Prefab 和导入记录都已经好了，不影响使用");
                    return;
                }
                report.scenePath = ScenePath;
            }
            catch (Exception ex)
            {
                report.warnings.Add($"展示场景 {ScenePath} 没更新成（{ex.Message}）；Prefab 和导入记录都已经好了，不影响使用");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool IsImportedModel(GameObject go)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) return false;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (source == null) return false;
            var path = AssetDatabase.GetAssetPath(source);
            return ModelRecordStore.FindByGuid(AssetDatabase.AssetPathToGUID(path), path, out _) != null;
        }

        static Scene CreateScene()
        {
            // 有未保存的未命名场景时 NewScene(Additive) 会抛异常，所以先落一个空场景文件再以 Additive 打开（同 UI 的展示场景）
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            File.WriteAllText(ScenePath, EmptySceneYaml);
            AssetDatabase.ImportAsset(ScenePath);
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (active.IsValid()) SceneManager.SetActiveScene(active);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            SceneManager.MoveGameObjectToScene(ground, scene);

            var lightGo = new GameObject("Directional Light", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            SceneManager.MoveGameObjectToScene(lightGo, scene);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.GetComponent<Camera>().fieldOfView = 40f;
            SceneManager.MoveGameObjectToScene(camGo, scene);
            return scene;
        }
    }
}
