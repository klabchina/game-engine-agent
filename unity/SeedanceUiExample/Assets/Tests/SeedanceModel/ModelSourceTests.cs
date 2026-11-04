using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using Seedance.ModelImport.Editor;

namespace Seedance.ModelImport.Tests
{
    public class ModelSourceTests
    {
        string scratch;

        [SetUp]
        public void SetUp()
        {
            scratch = Path.Combine(Path.GetTempPath(), "seedance-model-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }

        static void Touch(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
        }

        static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var dest = Path.Combine(to, Path.GetRelativePath(from, f));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest);
            }
        }

        [Test]
        public void Open_Dir_FindsFbxAndTextures()
        {
            using var src = ModelSource.Open(ModelTestPaths.TripoDir);
            StringAssert.EndsWith(ModelTestPaths.TripoFbxName, src.FbxPath);
            Assert.AreEqual(3, src.TexturePaths.Count);
        }

        [Test]
        public void Open_Zip_SameAsDir()
        {
            string fbx;
            using (var src = ModelSource.Open(ModelTestPaths.TripoZip))
            {
                fbx = src.FbxPath;
                StringAssert.EndsWith(ModelTestPaths.TripoFbxName, fbx);
                Assert.AreEqual(3, src.TexturePaths.Count);
                Assert.IsTrue(File.Exists(fbx));
            }
            Assert.IsFalse(File.Exists(fbx), "zip 解出来的临时文件要删掉");
            Assert.IsTrue(Directory.Exists(ModelTestPaths.TripoDir), "目录来源不能被删");
        }

        [Test]
        public void Open_ZipWithWrapperDir_FindsFbx()
        {
            var dir = Path.Combine(scratch, "content");
            Touch(Path.Combine(dir, "wrap/x.fbx"));
            Touch(Path.Combine(dir, "wrap/Textures/a_BaseColor.png"));
            Touch(Path.Combine(dir, "__MACOSX/wrap/._x.fbx"));
            var zip = Path.Combine(scratch, "w.zip");
            ZipFile.CreateFromDirectory(dir, zip);
            using var src = ModelSource.Open(zip);
            StringAssert.EndsWith("x.fbx", src.FbxPath);
            Assert.AreEqual(1, src.TexturePaths.Count);
            StringAssert.EndsWith("a_BaseColor.png", src.TexturePaths[0]);
        }

        [Test]
        public void Open_PathWithSpacesAndChinese()
        {
            var dir = Path.Combine(scratch, "模型 产物");
            CopyDir(ModelTestPaths.TripoDir, dir);
            using var src = ModelSource.Open(dir);
            StringAssert.EndsWith(ModelTestPaths.TripoFbxName, src.FbxPath);
            Assert.AreEqual(3, src.TexturePaths.Count);
        }

        [Test]
        public void Open_NoTexturesDir_EmptyList()
        {
            Touch(Path.Combine(scratch, "x.fbx"));
            using var src = ModelSource.Open(scratch);
            Assert.AreEqual(0, src.TexturePaths.Count);
        }

        [Test]
        public void Open_NoFbx_Throws()
        {
            Touch(Path.Combine(scratch, "Textures/a.png"));
            var ex = Assert.Throws<ModelImportException>(() => ModelSource.Open(scratch));
            StringAssert.Contains("没有找到 .fbx", ex.Message);
        }

        [Test]
        public void Open_TwoFbx_Throws()
        {
            Touch(Path.Combine(scratch, "a.fbx"));
            Touch(Path.Combine(scratch, "sub/b.FBX"));
            var ex = Assert.Throws<ModelImportException>(() => ModelSource.Open(scratch));
            StringAssert.Contains("有多个 .fbx", ex.Message);
        }

        [Test]
        public void Open_Missing_Throws()
        {
            var ex = Assert.Throws<ModelImportException>(() => ModelSource.Open(Path.Combine(scratch, "nope.zip")));
            StringAssert.Contains("不存在", ex.Message);
        }
    }
}
