using NUnit.Framework;
using Seedance.ModelImport.Editor;

namespace Seedance.ModelImport.Tests
{
    public class TextureRolesTests
    {
        [Test]
        public void Classify_Tripo()
        {
            var r = TextureRoles.Classify(new[]
            {
                "tripo_node_8a5d_BaseColor.jpg", "tripo_node_8a5d_Normal_Bake.jpg", "tripo_node_8a5d_Metallic-tripo_.png",
            });
            Assert.AreEqual(TextureRole.BaseColor, r["tripo_node_8a5d_BaseColor.jpg"]);
            Assert.AreEqual(TextureRole.Normal, r["tripo_node_8a5d_Normal_Bake.jpg"]);
            Assert.AreEqual(TextureRole.Metallic, r["tripo_node_8a5d_Metallic-tripo_.png"]);
        }

        [Test]
        public void Classify_SingleUnsuffixed_IsBaseColor()
        {
            Assert.AreEqual(TextureRole.BaseColor, TextureRoles.Classify(new[] { "texture_20250901.png" })["texture_20250901.png"]);
        }

        [Test]
        public void Classify_Unknown()
        {
            var r = TextureRoles.Classify(new[] { "a_BaseColor.png", "b_AO.png" });
            Assert.AreEqual(TextureRole.BaseColor, r["a_BaseColor.png"]);
            Assert.AreEqual(TextureRole.Unknown, r["b_AO.png"]);
        }

        [Test]
        public void Classify_Synonyms_CaseInsensitive()
        {
            var r = TextureRoles.Classify(new[] { "x_ALBEDO.png", "y_normal.png" });
            Assert.AreEqual(TextureRole.BaseColor, r["x_ALBEDO.png"]);
            Assert.AreEqual(TextureRole.Normal, r["y_normal.png"]);
            Assert.AreEqual(TextureRole.BaseColor, TextureRoles.Classify(new[] { "m_Diffuse.jpg", "n.png" })["m_Diffuse.jpg"]);
        }

        [Test]
        public void Classify_DuplicateRole_SecondUnknown()
        {
            var r = TextureRoles.Classify(new[] { "a_BaseColor.png", "b_BaseColor.png" });
            Assert.AreEqual(TextureRole.BaseColor, r["a_BaseColor.png"]);
            Assert.AreEqual(TextureRole.Unknown, r["b_BaseColor.png"]);
        }
    }
}
