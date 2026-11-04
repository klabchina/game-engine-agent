// 建 / 合并角色的 AnimatorController：Base Layer 里每段动画一个同名状态，默认状态 idle（没有就第一个），不加切换线和参数。
// 重导时只动导入记录里登记过的状态（换 motion）和这次新出现的动画（加状态）；
// 程序加的切换线、参数、状态、layer、默认状态都不碰。记录里有、这次 FBX 里没有的：状态留着、motion 置空、报 missingClips。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    public static class ControllerBuilder
    {
        public static AnimatorController BuildOrUpdate(string controllerPath, IReadOnlyList<AnimationClip> clips,
            IReadOnlyList<string> recordedClips, ModelImportReport report)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var first = controller == null;
            if (first)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }
            var sm = controller.layers[0].stateMachine;
            var recorded = new HashSet<string>(recordedClips ?? new string[0]);
            AnimatorState State(string name) => sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

            foreach (var clip in clips)
            {
                var state = State(clip.name);
                if (state == null)
                {
                    state = sm.AddState(clip.name);
                    state.motion = clip;
                    report.createdStates.Add(clip.name);
                }
                else if (recorded.Contains(clip.name))
                {
                    state.motion = clip;
                    report.updatedStates.Add(clip.name);
                }
                else
                {
                    report.warnings.Add($"Controller 里已有程序建的状态 {clip.name}，没改它；新动画 {clip.name} 没接进去，需要的话手动挂上");
                }
            }

            var present = new HashSet<string>(clips.Select(c => c.name));
            foreach (var name in recorded.Where(n => !present.Contains(n)).OrderBy(n => n, System.StringComparer.Ordinal))
            {
                var state = State(name);
                if (state == null) continue; // 程序删掉了这个状态
                state.motion = null;
                report.missingClips.Add(name);
            }

            if (first)
                sm.defaultState = State("idle") ?? (clips.Count > 0 ? State(clips[0].name) : null);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
