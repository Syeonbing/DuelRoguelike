// Project Ignis CardScripts 폴더에서 Lua 스크립트를 찾아 읽는다.
// 엔진은 c12345678.lua(카드), proc_fusion.lua(공용 처리) 같은 파일 이름으로 스크립트를 요청한다.
using System;
using System.Collections.Generic;
using System.IO;

namespace DuelRoguelike.Engine.Data
{
    public sealed class CardScriptSource
    {
        /// <summary>듀얼을 만든 직후 직접 불러와야 하는 기본 스크립트 (EDOPro와 같은 순서).</summary>
        public static readonly string[] StartupScripts = { "constant.lua", "utility.lua" };

        /// <summary>
        /// 기본으로 찾는 하위 폴더. 앞에 있는 폴더가 우선이다.
        /// 루트(공용 스크립트) → official(공식 카드) → pre-errata(에라타 이전 카드) → unofficial.
        /// unofficial은 utility.lua가 항상 불러오는 proc_unofficial.lua 때문에 넣는다 (EDOPro도 이 폴더를 찾는다).
        /// 카드 DB에 비공식 카드가 없으므로 비공식 카드 스크립트는 요청되지 않는다.
        /// </summary>
        public static readonly string[] DefaultFolders = { "", "official", "pre-errata", "unofficial" };

        readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Root { get; }

        /// <summary>찾을 수 있는 스크립트 파일 수.</summary>
        public int Count => _paths.Count;

        public CardScriptSource(string root, IEnumerable<string> folders = null)
        {
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"CardScripts 폴더가 없습니다: {root}");
            Root = root;

            foreach (string folder in folders ?? DefaultFolders)
            {
                string dir = string.IsNullOrEmpty(folder) ? root : Path.Combine(root, folder);
                if (!Directory.Exists(dir))
                    continue;
                foreach (string path in Directory.GetFiles(dir, "*.lua", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(path);
                    if (!_paths.ContainsKey(name))
                        _paths.Add(name, path);
                }
            }
        }

        public bool TryGetPath(string name, out string path) =>
            _paths.TryGetValue(Path.GetFileName(name), out path);

        /// <summary>스크립트 내용을 읽는다. 맨 앞의 UTF-8 BOM은 뺀다. 없으면 null.</summary>
        public byte[] Read(string name)
        {
            if (!TryGetPath(name, out string path))
                return null;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                var trimmed = new byte[bytes.Length - 3];
                Buffer.BlockCopy(bytes, 3, trimmed, 0, trimmed.Length);
                return trimmed;
            }
            return bytes;
        }
    }
}
