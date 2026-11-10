using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Tests
{
    public class MaterialBuilderTests
    {
        const string P = "Knight";
        static string Dir => ModelTestPaths.OutDir;
        static readonly string[] OneSlot = { "tripo_node_x_material" };

        [TearDown]
        public void TearDown()
        {
            MaterialBuilder.FindShader = MaterialBuilder.DefaultFindShader;
            AssetDatabase.DeleteAsset(ModelTestPaths.OutDir);
        }

        static Texture2D Tex(string name)
        {
            Directory.CreateDirectory(Dir);
            var t = new Texture2D(4, 4, TextureFormat.RGB24, false);
            var path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string Guid(Texture t) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t));

        static Dictionary<TextureRole, Texture2D> Three() => new Dictionary<TextureRole, Texture2D>
        {
            [TextureRole.BaseColor] = Tex("base"), [TextureRole.Normal] = Tex("normal"), [TextureRole.Metallic] = Tex("metal"),
        };

        static Dictionary<TextureRole, string> Guids(Dictionary<TextureRole, Texture2D> d) => d.ToDictionary(kv => kv.Key, kv => Guid(kv.Value));

        [Test]
        public void First_LitWithThreeMaps()
        {
            var tex = Three();
            var warnings = new List<string>();
            var bySlot = MaterialBuilder.BuildOrUpdate(Dir, P, OneSlot, tex, null, warnings);
            var m = bySlot[OneSlot[0]];
            Assert.AreEqual($"{Dir}/Materials/{P}.mat", AssetDatabase.GetAssetPath(m));
            Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name);
            Assert.AreEqual(Guid(tex[TextureRole.BaseColor]), Guid(m.GetTexture("_BaseMap")));
            Assert.AreEqual(Guid(tex[TextureRole.Normal]), Guid(m.GetTexture("_BumpMap")));
            Assert.AreEqual(Guid(tex[TextureRole.Metallic]), Guid(m.GetTexture("_MetallicGlossMap")));
            Assert.IsTrue(m.IsKeywordEnabled("_NORMALMAP"));
            Assert.IsTrue(m.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
            CollectionAssert.IsEmpty(warnings);
        }

        [Test]
        public void Update_KeepsColorAndSmoothness()
        {
            var tex = Three();
            var m = MaterialBuilder.BuildOrUpdate(Dir, P, OneSlot, tex, null, new List<string>())[OneSlot[0]];
            m.SetColor("_BaseColor", Color.red);
            m.SetFloat("_Smoothness", 0.123f);
            AssetDatabase.SaveAssets();
            var m2 = MaterialBuilder.BuildOrUpdate(Dir, P, OneSlot, tex, Guids(tex), new List<string>())[OneSlot[0]];
            Assert.AreEqual(AssetDatabase.GetAssetPath(m), AssetDatabase.GetAssetPath(m2));
            Assert.AreEqual(Color.red, m2.GetColor("_BaseColor"));
            Assert.AreEqual(0.123f, m2.GetFloat("_Smoothness"), 1e-4f);
        }

        [Test]
        public void Update_RoleGone_ClearsOnlyRecordedSlot()
        {
            var tex = Three();
            var m = MaterialBuilder.BuildOrUpdate(Dir, P, OneSlot, tex, null, new List<string>())[OneSlot[0]];
            var mine = Tex("programmer_normal");
            m.SetTexture("_BumpMap", mine); // 程序自己换了法线
            AssetDatabase.SaveAssets();
            var only = new Dictionary<TextureRole, Texture2D> { [TextureRole.BaseColor] = tex[TextureRole.BaseColor] };
            var m2 = MaterialBuilder.BuildOrUpdate(Dir, P, OneSlot, only, Guids(tex), new List<string>())[OneSlot[0]];
            Assert.AreEqual(Guid(mine), Guid(m2.GetTexture("_BumpMap")), "程序换上的贴图不碰");
            Assert.IsNull(m2.GetTexture("_MetallicGlossMap"), "导入写的、这次没有的用途要清空");
            Assert.IsFalse(m2.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
        }

        [Test]
        public void MultipleSlots_OneMaterialEachAndWarn()
        {
            var warnings = new List<string>();
            var bySlot = MaterialBuilder.BuildOrUpdate(Dir, P, new[] { "Body", "Hair" }, Three(), null, warnings);
            Assert.AreEqual($"{Dir}/Materials/{P}_Body.mat", AssetDatabase.GetAssetPath(bySlot["Body"]));
            Assert.AreEqual($"{Dir}/Materials/{P}_Hair.mat", AssetDatabase.GetAssetPath(bySlot["Hair"]));
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void NoUrp_Throws()
        {
            MaterialBuilder.FindShader = _ => null;
            var ex = Assert.Throws<ModelImportException>(() => MaterialBuilder.BuildOrUpdate(Dir, P, OneSlot, Three(), null, new List<string>()));
            StringAssert.Contains("URP", ex.Message);
            Assert.IsFalse(Directory.Exists($"{Dir}/Materials") && Directory.GetFiles($"{Dir}/Materials", "*.mat").Any());
        }
    }
}
