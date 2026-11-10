// 把角色 Prefab 放进预览场景（不进 Hierarchy、不影响用户打开的场景），摆成默认状态动画（idle）的第 0 帧，
// 从正面偏右上方离屏渲染成 PNG，给 skill 拿来给用户看。

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seedance.ModelImport.Editor
{
    public static class ModelCapture
    {
        public static readonly Color BackgroundColor = new Color(0.18f, 0.18f, 0.2f);

        public static void Capture(GameObject prefabAsset, int width, int height, string pngPath)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                var lightGo = new GameObject("Light", typeof(Light));
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(40f, 160f, 0f); // 从相机这一侧照过来

                var camGo = new GameObject("CaptureCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.GetComponent<Camera>();
                cam.scene = scene;
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = BackgroundColor;
                cam.fieldOfView = 30f;
                cam.targetTexture = rt;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
                inst.transform.position = Vector3.zero;
                inst.SetActive(true);
                PoseDefaultClip(inst);
                ViewFraming.Frame(cam, ViewFraming.Bounds(inst));
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

        /// <summary>摆成 Controller 默认状态那段动画的第 0 帧（没有 Animator / 动画就保持绑定姿势）</summary>
        static void PoseDefaultClip(GameObject inst)
        {
            var animator = inst.GetComponentInChildren<Animator>(true);
            var controller = animator == null ? null : animator.runtimeAnimatorController as AnimatorController;
            var clip = controller == null || controller.layers.Length == 0
                ? null
                : controller.layers[0].stateMachine.defaultState?.motion as AnimationClip;
            if (clip == null) clip = controller?.animationClips.FirstOrDefault();
            if (clip != null) clip.SampleAnimation(animator.gameObject, 0f);
        }
    }
}
