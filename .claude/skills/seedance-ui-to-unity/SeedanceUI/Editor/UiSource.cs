// 打开一份 ui-maker 产物：任务的 out/ 目录，或 ui.zip（解到工程 Temp/ 下，用完删掉）。

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    public sealed class UiSource : IDisposable
    {
        public string RootDir { get; }
        public string LayoutJsonPath => Path.Combine(RootDir, "layout.json");
        readonly bool ownsDir;

        UiSource(string rootDir, bool ownsDir)
        {
            RootDir = rootDir;
            this.ownsDir = ownsDir;
        }

        public static UiSource Open(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new UiImportException("没有给产物路径");
            UiSource src;
            if (Directory.Exists(path)) src = new UiSource(Path.GetFullPath(path), false);
            else if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) src = Extract(path);
            else throw new UiImportException($"产物路径不存在，或不是目录也不是 .zip：{path}");

            if (!File.Exists(src.LayoutJsonPath))
            {
                src.Dispose();
                throw new UiImportException($"产物里没有 layout.json：{path}");
            }
            return src;
        }

        static UiSource Extract(string zipPath)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/SeedanceUiImport", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(dir);
            var src = new UiSource(dir, true);
            try
            {
                using var stream = File.OpenRead(zipPath);
                // ui.zip 里有的条目没打 UTF-8 标志（macOS zip、JSZip 都可能），统一按 UTF-8 解文件名
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8);
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // 目录条目
                    var dest = Path.GetFullPath(Path.Combine(dir, entry.FullName));
                    if (Path.IsPathRooted(entry.FullName) || !dest.StartsWith(dir + Path.DirectorySeparatorChar))
                        throw new UiImportException($"ui.zip 里有越出目录的条目：{entry.FullName}");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    entry.ExtractToFile(dest, true);
                }
            }
            catch (InvalidDataException ex)
            {
                src.Dispose();
                throw new UiImportException($"ui.zip 读不了：{ex.Message}");
            }
            catch
            {
                src.Dispose();
                throw;
            }
            return src;
        }

        public void Dispose()
        {
            if (ownsDir && Directory.Exists(RootDir)) Directory.Delete(RootDir, true);
        }
    }
}
