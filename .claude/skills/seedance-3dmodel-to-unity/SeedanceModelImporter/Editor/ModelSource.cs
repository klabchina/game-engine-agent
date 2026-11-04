// 打开一份带骨骼的 FBX 产物：解开后的目录，或 zip（解到系统临时目录，不进工程，用完删掉）。
// 产物里应恰好有一个 .fbx（可以多套一层目录）；贴图在 FBX 同级的 Textures/ 下。

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Seedance.ModelImport.Editor
{
    public sealed class ModelSource : IDisposable
    {
        static readonly string[] TextureExts = { ".png", ".jpg", ".jpeg", ".tga" };

        public string FbxPath { get; }
        public IReadOnlyList<string> TexturePaths { get; }
        readonly string ownedDir;

        ModelSource(string fbxPath, IReadOnlyList<string> textures, string ownedDir)
        {
            FbxPath = fbxPath;
            TexturePaths = textures;
            this.ownedDir = ownedDir;
        }

        public static ModelSource Open(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ModelImportException("没有给产物路径");
            if (Directory.Exists(path)) return FromDir(Path.GetFullPath(path), null);
            if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Extract(path);
                try { return FromDir(dir, dir); }
                catch { Directory.Delete(dir, true); throw; }
            }
            throw new ModelImportException($"产物路径不存在，或不是目录也不是 .zip：{path}");
        }

        static bool Ignored(string root, string file)
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            return rel.StartsWith("__MACOSX/") || Path.GetFileName(file).StartsWith("._") || Path.GetFileName(file) == ".DS_Store";
        }

        static ModelSource FromDir(string root, string ownedDir)
        {
            var fbxs = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && !Ignored(root, f))
                .ToList();
            if (fbxs.Count == 0) throw new ModelImportException($"产物里没有找到 .fbx：{root}");
            if (fbxs.Count > 1)
                throw new ModelImportException($"产物里有多个 .fbx，不知道导哪个：{string.Join("、", fbxs.Select(f => Path.GetRelativePath(root, f)))}");
            var fbx = fbxs[0];
            var texDir = Path.Combine(Path.GetDirectoryName(fbx), "Textures");
            var textures = Directory.Exists(texDir)
                ? Directory.GetFiles(texDir)
                    .Where(f => TextureExts.Contains(Path.GetExtension(f).ToLowerInvariant()) && !Ignored(root, f))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                    .ToList()
                : new List<string>();
            return new ModelSource(fbx, textures, ownedDir);
        }

        static string Extract(string zipPath)
        {
            var dir = Path.Combine(Path.GetTempPath(), "SeedanceModelImport", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                using var stream = File.OpenRead(zipPath);
                // 有的 zip 条目没打 UTF-8 标志（macOS zip 等），统一按 UTF-8 解文件名
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8);
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // 目录条目
                    var dest = Path.GetFullPath(Path.Combine(dir, entry.FullName));
                    if (Path.IsPathRooted(entry.FullName) || !dest.StartsWith(dir + Path.DirectorySeparatorChar))
                        throw new ModelImportException($"zip 里有越出目录的条目：{entry.FullName}");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    entry.ExtractToFile(dest, true);
                }
            }
            catch (InvalidDataException ex)
            {
                Directory.Delete(dir, true);
                throw new ModelImportException($"zip 读不了：{ex.Message}");
            }
            catch
            {
                Directory.Delete(dir, true);
                throw;
            }
            return dir;
        }

        public void Dispose()
        {
            if (ownedDir != null && Directory.Exists(ownedDir)) Directory.Delete(ownedDir, true);
        }
    }
}
