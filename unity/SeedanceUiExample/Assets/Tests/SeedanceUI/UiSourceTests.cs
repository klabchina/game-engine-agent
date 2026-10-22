using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;

namespace Seedance.UiImport.Tests
{
    public class UiSourceTests
    {
        static string[] Slices(string root) =>
            Directory.GetFiles(Path.Combine(root, "slices")).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        [Test]
        public void Open_Directory()
        {
            using var src = UiSource.Open(TestPaths.CharPanelDir);
            Assert.IsTrue(File.Exists(src.LayoutJsonPath));
            Assert.IsTrue(File.Exists(Path.Combine(src.RootDir, "slices/属性条_1.png")));
        }

        [Test]
        public void Open_Zip_SameFilesAsDirectory()
        {
            string root;
            using (var src = UiSource.Open(TestPaths.CharPanelZip))
            {
                root = src.RootDir;
                Assert.IsTrue(File.Exists(src.LayoutJsonPath));
                CollectionAssert.AreEqual(Slices(TestPaths.CharPanelDir), Slices(root));
            }
            Assert.IsFalse(Directory.Exists(root));
            Assert.IsTrue(Directory.Exists(TestPaths.CharPanelDir), "目录来源不能被删");
        }

        [Test]
        public void Open_RejectsMissingLayout()
        {
            var empty = Path.Combine(Path.GetTempPath(), "seedance-empty-" + System.Guid.NewGuid());
            Directory.CreateDirectory(empty);
            try { Assert.Throws<UiImportException>(() => UiSource.Open(empty)); }
            finally { Directory.Delete(empty, true); }
        }

        [Test]
        public void Open_RejectsNonexistentPath()
        {
            Assert.Throws<UiImportException>(() => UiSource.Open("/nonexistent/seedance/out"));
        }
    }
}
