using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Seedance.ModelImport.Tests
{
    public class ControllerBuilderTests
    {
        static string CtrlPath => $"{ModelTestPaths.OutDir}/Knight.controller";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ModelTestPaths.OutDir);

        /// <summary>存一个空 clip；同名的新版本放进子目录 tag/（资源名跟文件名走，不能改文件名）</summary>
        static AnimationClip Clip(string name, string tag = "")
        {
            var dir = tag == "" ? ModelTestPaths.OutDir : $"{ModelTestPaths.OutDir}/{tag}";
            Directory.CreateDirectory(dir);
            AssetDatabase.ImportAsset(dir);
            var c = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(c, $"{dir}/{name}.anim");
            return c;
        }

        static List<AnimationClip> Clips(params string[] names) => names.Select(n => Clip(n)).ToList();

        static AnimatorState State(AnimatorController c, string name) =>
            c.layers[0].stateMachine.states.Select(s => s.state).SingleOrDefault(s => s.name == name);

        [Test]
        public void First_StatesAndDefault()
        {
            var report = new ModelImportReport();
            var c = ControllerBuilder.BuildOrUpdate(CtrlPath, Clips("walk", "idle", "sit"), null, report);
            Assert.AreEqual(CtrlPath, AssetDatabase.GetAssetPath(c));
            var sm = c.layers[0].stateMachine;
            CollectionAssert.AreEquivalent(new[] { "walk", "idle", "sit" }, sm.states.Select(s => s.state.name));
            Assert.AreEqual("idle", sm.defaultState.name);
            Assert.IsEmpty(c.parameters);
            Assert.IsTrue(sm.states.All(s => s.state.transitions.Length == 0));
            Assert.IsEmpty(sm.anyStateTransitions);
            Assert.AreEqual("walk", State(c, "walk").motion.name);
            CollectionAssert.AreEquivalent(new[] { "walk", "idle", "sit" }, report.createdStates);
        }

        [Test]
        public void First_NoIdle_DefaultFirst()
        {
            var c = ControllerBuilder.BuildOrUpdate(CtrlPath, Clips("walk", "sit"), null, new ModelImportReport());
            Assert.AreEqual("walk", c.layers[0].stateMachine.defaultState.name);
        }

        [Test]
        public void Update_KeepsUserTransitionsParamsStates()
        {
            var clips = Clips("idle", "walk");
            var c = ControllerBuilder.BuildOrUpdate(CtrlPath, clips, null, new ModelImportReport());
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var t = State(c, "idle").AddTransition(State(c, "walk"));
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            c.layers[0].stateMachine.AddState("Attack");
            c.AddLayer("Upper");
            c.layers[0].stateMachine.defaultState = State(c, "walk");
            AssetDatabase.SaveAssets();

            var c2 = ControllerBuilder.BuildOrUpdate(CtrlPath, clips, new[] { "idle", "walk" }, new ModelImportReport());
            Assert.AreEqual("Speed", c2.parameters.Single().name);
            Assert.AreEqual("walk", State(c2, "idle").transitions.Single().destinationState.name);
            Assert.IsNotNull(State(c2, "Attack"));
            Assert.AreEqual(2, c2.layers.Length);
            Assert.AreEqual("walk", c2.layers[0].stateMachine.defaultState.name, "默认状态归程序");
        }

        [Test]
        public void Update_NewClip_AddsState()
        {
            ControllerBuilder.BuildOrUpdate(CtrlPath, Clips("idle"), null, new ModelImportReport());
            var report = new ModelImportReport();
            var c = ControllerBuilder.BuildOrUpdate(CtrlPath, new List<AnimationClip> { Clip("idle", "2"), Clip("jump") }, new[] { "idle" }, report);
            Assert.IsNotNull(State(c, "jump"));
            CollectionAssert.AreEqual(new[] { "jump" }, report.createdStates);
            CollectionAssert.AreEqual(new[] { "idle" }, report.updatedStates);
        }

        [Test]
        public void Update_ClipGone_KeepsStateNullMotion()
        {
            ControllerBuilder.BuildOrUpdate(CtrlPath, Clips("idle", "sit"), null, new ModelImportReport());
            var report = new ModelImportReport();
            var c = ControllerBuilder.BuildOrUpdate(CtrlPath, new List<AnimationClip> { Clip("idle", "2") }, new[] { "idle", "sit" }, report);
            Assert.IsNotNull(State(c, "sit"));
            Assert.IsNull(State(c, "sit").motion);
            CollectionAssert.AreEqual(new[] { "sit" }, report.missingClips);
        }

        [Test]
        public void Update_ReplacesMotion()
        {
            ControllerBuilder.BuildOrUpdate(CtrlPath, Clips("idle"), null, new ModelImportReport());
            var fresh = Clip("idle", "2");
            var c = ControllerBuilder.BuildOrUpdate(CtrlPath, new List<AnimationClip> { fresh }, new[] { "idle" }, new ModelImportReport());
            Assert.AreEqual(AssetDatabase.GetAssetPath(fresh), AssetDatabase.GetAssetPath(State(c, "idle").motion));
        }

        [Test]
        public void Update_UnrecordedStateWithSameName_NotTouched()
        {
            // 程序自建了一个叫 jump 的状态（不在记录里）、挂了自己的动作；上游这次也出了 jump
            ControllerBuilder.BuildOrUpdate(CtrlPath, Clips("idle"), null, new ModelImportReport());
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            var mine = Clip("myjump");
            c.layers[0].stateMachine.AddState("jump").motion = mine;
            AssetDatabase.SaveAssets();
            var report = new ModelImportReport();
            var c2 = ControllerBuilder.BuildOrUpdate(CtrlPath, new List<AnimationClip> { Clip("idle", "2"), Clip("jump") }, new[] { "idle" }, report);
            Assert.AreEqual(1, c2.layers[0].stateMachine.states.Count(s => s.state.name == "jump"));
            Assert.AreEqual(AssetDatabase.GetAssetPath(mine), AssetDatabase.GetAssetPath(State(c2, "jump").motion));
            Assert.IsTrue(report.warnings.Any(w => w.Contains("jump")));
        }
    }
}
