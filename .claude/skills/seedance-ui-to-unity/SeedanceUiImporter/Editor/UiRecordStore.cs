// 导入记录的位置与读写。记录统一放在 Assets/SeedanceUI/ 下，按 Prefab 路径镜像命名；
// 但认记录以 Prefab 的 GUID 为准——程序挪了 / 改名了 Prefab 也找得回来，下次保存时搬到新的镜像路径。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    public static class UiRecordStore
    {
        public const string Root = "Assets/SeedanceUI";
        public const string RootParentId = "<root>";

        /// <summary>Assets/UI/X.prefab → Assets/SeedanceUI/UI/X.json</summary>
        public static string RecordPathFor(string prefabPath)
        {
            var p = prefabPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/")) throw new UiImportException($"Prefab 路径必须在 Assets/ 下：{prefabPath}");
            var rel = p.Substring("Assets/".Length);
            var ext = Path.GetExtension(rel);
            if (ext.Length > 0) rel = rel.Substring(0, rel.Length - ext.Length);
            return $"{Root}/{rel}.json";
        }

        /// <summary>
        /// 找 prefabGuid 对应的记录。没有返回 null（recordPath 也为 null）。
        /// 任何一份记录解析不了、或多份记录指向同一个 Prefab，都抛 UiImportException——宁可停下，也不要当成首次导入建出一套重复节点。
        /// </summary>
        public static UiRecord FindByGuid(string prefabGuid, string prefabPath, out string recordPath)
        {
            recordPath = null;
            var hits = new List<(string path, UiRecord record)>();
            foreach (var path in AllRecordPaths())
            {
                var r = Read(path);
                if (r.prefabGuid == prefabGuid) hits.Add((path, r));
            }
            if (hits.Count == 0) return null;
            if (hits.Count > 1)
                throw new UiImportException($"有多份导入记录指向同一个 Prefab（{prefabPath}）：{string.Join("、", hits.Select(h => h.path))}。只保留正确的那份再导入");
            recordPath = hits[0].path;
            return hits[0].record;
        }

        /// <summary>写到 prefabPath 的镜像路径（先写 .tmp 再替换，Unity 不导入 .tmp）；previousPath 是别处的旧记录就删掉</summary>
        public static void Save(string prefabPath, UiRecord record, string previousPath)
        {
            var path = RecordPathFor(prefabPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(record, true));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
            AssetDatabase.ImportAsset(path);
            if (!string.IsNullOrEmpty(previousPath) && Normalize(previousPath) != path) Delete(previousPath);
        }

        /// <summary>
        /// 首次导入前调用：镜像路径上已有记录、而它属于一个被挪到别处的 Prefab（还没重导过），先把记录搬到那个 Prefab 的镜像路径，
        /// 免得被这次的新记录覆盖掉。记录指向的 Prefab 已经不存在，就留着让新记录覆盖。
        /// </summary>
        public static void ReleaseMirrorPath(string prefabPath)
        {
            var path = RecordPathFor(prefabPath);
            if (!File.Exists(path)) return;
            var r = Read(path);
            var owner = AssetDatabase.GUIDToAssetPath(r.prefabGuid);
            if (string.IsNullOrEmpty(owner) || !File.Exists(owner) || Normalize(owner) == Normalize(prefabPath)) return;
            Save(owner, r, path);
        }

        static IEnumerable<string> AllRecordPaths() =>
            Directory.Exists(Root)
                ? Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories).Select(Normalize).OrderBy(p => p, StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        static UiRecord Read(string path)
        {
            UiRecord r = null;
            try { r = JsonUtility.FromJson<UiRecord>(File.ReadAllText(path)); }
            catch (Exception) { /* 下面统一报 */ }
            if (r == null || string.IsNullOrEmpty(r.prefabGuid))
                throw new UiImportException($"导入记录损坏，读不出来：{path}。从版本库恢复它，或删掉它和对应的 Prefab 再导入");
            return r;
        }

        static void Delete(string path)
        {
            if (!AssetDatabase.DeleteAsset(path))
            {
                File.Delete(path);
                File.Delete(path + ".meta");
            }
        }

        static string Normalize(string p) => p.Replace('\\', '/');
    }
}
