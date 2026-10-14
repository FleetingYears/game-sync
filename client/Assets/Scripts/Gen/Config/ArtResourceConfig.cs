// 此文件由导表工具自动生成，请勿手动修改。
using System;
using System.Collections.Generic;
using System.IO;

namespace GameSync.Config
{
    [Serializable]
    public sealed class ArtResourceConfig
    {
        /// <summary>
        /// 对应二进制配置路径，相对仓库根目录。
        /// </summary>
        public const string BytesPath = "client/Res/Config/art_resource.bytes";

        /// <summary>
        /// 资源唯一 ID
        /// </summary>
        public string Id { get; private set; } = string.Empty;

        /// <summary>
        /// Sprite 或 Prefab
        /// </summary>
        public ArtResourceType Type { get; private set; }

        /// <summary>
        /// Assets/ 开头的路径，不含后缀
        /// </summary>
        public string AssetPath { get; private set; } = string.Empty;

        /// <summary>
        /// 维护说明
        /// </summary>
        public string Remark { get; private set; } = string.Empty;

        private static readonly object _sync = new object();
        private static Dictionary<string, ArtResourceConfig> _index;
        private static IReadOnlyList<ArtResourceConfig> _all;

        /// <summary>是否已成功加载过配置。</summary>
        public static bool IsLoaded
        {
            get
            {
                lock (_sync)
                {
                    return _index != null;
                }
            }
        }

        private static void EnsureLoaded()
        {
            if (_index == null)
            {
                throw new InvalidOperationException("配置尚未加载：ArtResourceConfig");
            }
        }

        /// <summary>按仓库根目录与 BytesPath 读取文件，再加载配置。</summary>
        public static void LoadFromFile(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("必须提供仓库根目录。", nameof(projectRoot));
            }
            Load(File.ReadAllBytes(Path.Combine(projectRoot, BytesPath)));
        }

        /// <summary>加载二进制；全部校验成功后替换旧配置，失败保留旧配置。</summary>
        public static void Load(byte[] bytes)
        {
            using (var reader = new ConfigBinaryReader(bytes))
            {
                int count = reader.ReadHeader("Client", "art_resource", 4, 0, 16);
                reader.ExpectField("id", 1, 0, "", new string[] {  }, new int[] {  });
                reader.ExpectField("type", 7, 0, "ArtResourceType", new string[] { "Prefab", "Sprite" }, new int[] { 2, 1 });
                reader.ExpectField("asset_path", 1, 0, "", new string[] {  }, new int[] {  });
                reader.ExpectField("remark", 1, 2, "", new string[] {  }, new int[] {  });
                reader.ValidateRows(count, 16);
                var rows = new ArtResourceConfig[count];
                var index = new Dictionary<string, ArtResourceConfig>(count, StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    var row = new ArtResourceConfig();
                    row.Id = reader.ReadString();
                    row.Type = (ArtResourceType)reader.ReadEnum(new int[] { 2, 1 });
                    row.AssetPath = reader.ReadString();
                    row.Remark = reader.ReadString();
                    if (row.Id.Length == 0)
                    {
                        throw new InvalidDataException("必填字符串为空：Id");
                    }
                    if (row.Id != row.Id.Trim())
                    {
                        throw new InvalidDataException("字符串 Key 含首尾空白。");
                    }
                    if (row.AssetPath.Length == 0)
                    {
                        throw new InvalidDataException("必填字符串为空：AssetPath");
                    }
                    if (index.ContainsKey(row.Id))
                    {
                        throw new InvalidDataException("配置存在重复 Key。");
                    }
                    index.Add(row.Id, row);
                    rows[i] = row;
                }
                reader.EnsureEnd();
                var all = Array.AsReadOnly(rows);
                lock (_sync)
                {
                    _index = index;
                    _all = all;
                }
            }
        }

        /// <summary>按 Key 获取配置；未找到时抛出 KeyNotFoundException。</summary>
        public static ArtResourceConfig Get(string key)
        {
            lock (_sync)
            {
                EnsureLoaded();
                return _index[key];
            }
        }

        /// <summary>按 Key 查询，返回是否找到；找不到时配置为 null。</summary>
        public static bool TryGet(string key, out ArtResourceConfig config)
        {
            lock (_sync)
            {
                EnsureLoaded();
                return _index.TryGetValue(key, out config);
            }
        }

        /// <summary>获取全部配置的只读列表，顺序与导出的主键排序一致。</summary>
        public static IReadOnlyList<ArtResourceConfig> GetAll()
        {
            lock (_sync)
            {
                EnsureLoaded();
                return _all;
            }
        }

        /// <summary>查找首个满足条件的配置；没有匹配项时返回 null。</summary>
        public static ArtResourceConfig Find(Predicate<ArtResourceConfig> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
            var rows = GetAll();
            foreach (var row in rows)
            {
                if (predicate(row))
                {
                    return row;
                }
            }
            return null;
        }

        /// <summary>获取全部满足条件的配置，返回只读列表。</summary>
        public static IReadOnlyList<ArtResourceConfig> FindAll(Predicate<ArtResourceConfig> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
            var result = new List<ArtResourceConfig>();
            foreach (var row in GetAll())
            {
                if (predicate(row))
                {
                    result.Add(row);
                }
            }
            return result.AsReadOnly();
        }
    }
}
