// EDOPro strings.conf 읽기: 시스템 문구(효과 선택지·안내 문구), 승리 이유, 카운터 이름, 카드군 이름.
// 형식: "!system 1150 문구", "!victory 0x10 문구", "!counter 0x1 문구", "!setname 0x1 문구". 다른 줄은 무시한다.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DuelRoguelike.Engine.Data
{
    public sealed class SystemStrings
    {
        readonly Dictionary<uint, string> _system = new Dictionary<uint, string>();
        readonly Dictionary<uint, string> _victory = new Dictionary<uint, string>();
        readonly Dictionary<uint, string> _counter = new Dictionary<uint, string>();
        readonly Dictionary<uint, string> _setName = new Dictionary<uint, string>();

        public int Count => _system.Count + _victory.Count + _counter.Count + _setName.Count;

        /// <summary>파일 하나를 읽어 더한다. 같은 번호가 이미 있으면 나중에 읽은 쪽으로 바꾼다.</summary>
        public void Load(string path)
        {
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length < 2 || line[0] != '!')
                    continue;
                int first = line.IndexOf(' ');
                if (first < 0) continue;
                int second = line.IndexOf(' ', first + 1);
                if (second < 0) continue;
                string key = line.Substring(0, first);
                if (!TryParseId(line.Substring(first + 1, second - first - 1), out uint id))
                    continue;
                string text = line.Substring(second + 1);
                switch (key)
                {
                    case "!system": _system[id] = text; break;
                    case "!victory": _victory[id] = text; break;
                    case "!counter": _counter[id] = text; break;
                    case "!setname": _setName[id] = text; break;
                }
            }
        }

        /// <summary>시스템 문구. 없으면 null.</summary>
        public string System(uint id) => _system.TryGetValue(id, out var s) ? s : null;

        public string Victory(uint id) => _victory.TryGetValue(id, out var s) ? s : null;

        public string Counter(uint id) => _counter.TryGetValue(id, out var s) ? s : null;

        public string SetName(uint id) => _setName.TryGetValue(id, out var s) ? s : null;

        static bool TryParseId(string s, out uint id)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X"))
                return uint.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
            return uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }
    }
}
