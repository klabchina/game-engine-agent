using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Tests
{
    public class TextureWriterTests
    {
        const string P = "T";
        static string TexDir => ModelTestPaths.OutDir + "/Textures";
        string scratch;

        [SetUp]
        public void SetUp()
        {
            scratch = Path.Combine(Path.GetTempPath(), "seedance-tex-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ModelTestPaths.OutDir);
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }

        /// <summary>把 Tripo 的三张贴图拷到临时目录，文件名前缀换成 prefix（模拟 Tripo 每次生成 UUID 都不同）</summary>
        List<string> TripoTextures(string prefix)
        {
            var dir = Path.Combine(scratch, prefix);
            Directory.CreateDirectory(dir);
            return Directory.GetFiles(Path.Combine(ModelTestPaths.TripoDir, "Textures")).Select(f =>
            {
                var dest = Path.Combine(dir, Path.GetFileName(f).Replace("8a5defa9-ef49-4463-afdb-705a60445708", prefix));
                File.Copy(f, dest);
                return dest;
            }).OrderBy(p => p).ToList();
        }

        static string[] Files() =>
            Directory.GetFiles(TexDir).Where(f => !f.EndsWith(".meta")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        static Dictionary<TextureRole, string> Guids(TextureWriteResult r) =>
            r.byRole.ToDictionary(kv => kv.Key, kv => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kv.Value)));

        /// <summary>写一张 4x4 纯色、不带 alpha 的 PNG（RGB24 编出来的 PNG 颜色类型是 RGB）</summary>
        string SinglePng(string name, Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGB24, false);
            t.SetPixels(Enumerable.Repeat(c, 16).ToArray());
            var path = Path.Combine(scratch, name);
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            return path;
        }

        [Test]
        public void Write_Tripo_FixedNames()
        {
            var r = TextureWriter.Write(TripoTextures("uuidA"), ModelTestPaths.OutDir, P, null);
            CollectionAssert.AreEqual(new[] { "T_BaseColor.jpg", "T_Metallic.png", "T_Normal.jpg" }, Files());
            Assert.AreEqual(3, r.byRole.Count);
            var normal = (TextureImporter)AssetImporter.GetAtPath(TexDir + "/T_Normal.jpg");
            Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
            var metal = (TextureImporter)AssetImporter.GetAtPath(TexDir + "/T_Metallic.png");
            Assert.IsFalse(metal.sRGBTexture);
            CollectionAssert.IsEmpty(r.orphanTextures);
            CollectionAssert.IsEmpty(r.warnings);
        }

        [Test]
        public void Write_Metallic_ConvertedFromGltfOrm()
        {
            // Tripo 的金属度图是 glTF ORM：R=AO、G=粗糙度、B=金属度；URP 要 R=金属度、A=光滑度
            var src = SinglePng("x_Metallic.png", new Color(1f, 0.8f, 0.2f, 1f));
            TextureWriter.Write(new[] { src, SinglePng("x_BaseColor.png", Color.white) }, ModelTestPaths.OutDir, P, null);
            var bytes = File.ReadAllBytes(TexDir + "/T_Metallic.png");
            var t = new Texture2D(2, 2);
            t.LoadImage(bytes);
            var px = t.GetPixel(1, 1);
            UnityEngine.Object.DestroyImmediate(t);
            Assert.AreEqual(0.2f, px.r, 0.01f, "R = 原 B（金属度）");
            Assert.AreEqual(0.2f, px.a, 0.01f, "A = 1 - 原 G（光滑度）");
        }

        [Test]
        public void Write_Again_NewUuid_NoExtraFiles()
        {
            var first = TextureWriter.Write(TripoTextures("uuidA"), ModelTestPaths.OutDir, P, null);
            var guids = Guids(first);
            var second = TextureWriter.Write(TripoTextures("uuidB"), ModelTestPaths.OutDir, P, guids);
            CollectionAssert.AreEqual(new[] { "T_BaseColor.jpg", "T_Metallic.png", "T_Normal.jpg" }, Files());
            CollectionAssert.AreEquivalent(guids, Guids(second));
            CollectionAssert.IsEmpty(second.orphanTextures);
        }

        [Test]
        public void Write_ExtensionChanged_ReplacesFixedFile()
        {
            var first = TextureWriter.Write(new[] { SinglePng("a_BaseColor.jpg", Color.red) }, ModelTestPaths.OutDir, P, null);
            // 名字是 .jpg 但内容是 png——Unity 按内容识别，够用来测扩展名替换
            var second = TextureWriter.Write(new[] { SinglePng("b_BaseColor.png", Color.blue) }, ModelTestPaths.OutDir, P, Guids(first));
            CollectionAssert.AreEqual(new[] { "T_BaseColor.png" }, Files());
            Assert.AreEqual(TexDir + "/T_BaseColor.png", AssetDatabase.GetAssetPath(second.byRole[TextureRole.BaseColor]));
            CollectionAssert.IsEmpty(second.orphanTextures);
        }

        [Test]
        public void Write_RoleGone_ReportsOrphan()
        {
            var first = TextureWriter.Write(TripoTextures("uuidA"), ModelTestPaths.OutDir, P, null);
            var second = TextureWriter.Write(new[] { SinglePng("texture_20250901.png", Color.gray) }, ModelTestPaths.OutDir, P, Guids(first));
            CollectionAssert.AreEquivalent(new[] { TexDir + "/T_Normal.jpg", TexDir + "/T_Metallic.png" }, second.orphanTextures);
            Assert.IsTrue(File.Exists(TexDir + "/T_Normal.jpg"), "孤儿贴图不删");
            Assert.AreEqual(1, second.byRole.Count);
            Assert.AreEqual(TexDir + "/T_BaseColor.png", AssetDatabase.GetAssetPath(second.byRole[TextureRole.BaseColor]));
        }

        [Test]
        public void Write_Unknown_KeepsNameAndWarns()
        {
            var r = TextureWriter.Write(new[] { SinglePng("a_BaseColor.png", Color.red), SinglePng("b_AO.png", Color.white) }, ModelTestPaths.OutDir, P, null);
            CollectionAssert.AreEqual(new[] { "T_BaseColor.png", "b_AO.png" }, Files());
            Assert.AreEqual(1, r.warnings.Count);
            StringAssert.Contains("b_AO.png", r.warnings[0]);
        }
    }
}
