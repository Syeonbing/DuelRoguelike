// 저장 파일의 줄 단위 텍스트 형식 (GameSave, RunStateSave가 쓴다). rarity.txt처럼 사람이 열어 볼 수 있다.
// 한 줄 = 키 + 값들 (공백으로 나눔). # 뒤는 주석이고 빈 줄은 건너뛴다. 이벤트 줄의 값은 "이름=값" 꼴이다.
// 형식이 틀리면 FormatException (파일 이름과 줄 번호 포함).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DuelRoguelike.Run
{
    internal readonly struct SaveLine
    {
        public readonly int Number;
        public readonly string Key;
        public readonly string[] Values;

        public SaveLine(int number, string key, string[] values)
        {
            Number = number;
            Key = key;
            Values = values;
        }
    }

    internal sealed class SaveReader
    {
        readonly string _source;

        public SaveReader(string source) => _source = source;

        public static List<SaveLine> Split(string text)
        {
            var lines = new List<SaveLine>();
            string[] raw = text.Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i];
                int comment = line.IndexOf('#');
                if (comment >= 0)
                    line = line.Substring(0, comment);
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;
                lines.Add(new SaveLine(i + 1, parts[0], parts.Skip(1).ToArray()));
            }
            return lines;
        }

        public FormatException Error(SaveLine line, string message) =>
            new FormatException($"{_source} {line.Number}번째 줄 ({line.Key}): {message}");

        public FormatException Error(string message) => new FormatException($"{_source}: {message}");

        public void ExpectCount(SaveLine line, int count)
        {
            if (line.Values.Length != count)
                throw Error(line, $"값이 {count}개여야 하는데 {line.Values.Length}개");
        }

        public void ExpectAtLeast(SaveLine line, int count)
        {
            if (line.Values.Length < count)
                throw Error(line, $"값이 {count}개 이상이어야 하는데 {line.Values.Length}개");
        }

        public int Int(SaveLine line, string text, int min = int.MinValue)
        {
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                throw Error(line, $"\"{text}\"는 정수가 아님");
            if (value < min)
                throw Error(line, $"{value}: {min} 이상이어야 함");
            return value;
        }

        public ulong ULong(SaveLine line, string text)
        {
            if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value))
                throw Error(line, $"\"{text}\"는 0 이상의 정수가 아님");
            return value;
        }

        public uint Code(SaveLine line, string text)
        {
            if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) || value == 0)
                throw Error(line, $"\"{text}\"는 카드 코드가 아님");
            return value;
        }

        public List<uint> Codes(SaveLine line, int skip = 0) => line.Values.Skip(skip).Select(v => Code(line, v)).ToList();

        public T Enum<T>(SaveLine line, string text) where T : struct
        {
            // 숫자로 적힌 값("3")은 받지 않는다 (이름만).
            if (text.Length == 0 || char.IsDigit(text[0]) || text[0] == '-'
                || !System.Enum.TryParse(text, false, out T value) || !System.Enum.IsDefined(typeof(T), value))
                throw Error(line, $"\"{text}\"는 {typeof(T).Name} 값이 아님");
            return value;
        }

        /// <summary>한 번만 나와야 하는 키들을 모은다. 두 번 나오면 FormatException.</summary>
        public Dictionary<string, SaveLine> Unique(IEnumerable<SaveLine> lines, ICollection<string> allowed, ICollection<string> repeatable)
        {
            var map = new Dictionary<string, SaveLine>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                if (repeatable.Contains(line.Key))
                    continue;
                if (!allowed.Contains(line.Key))
                    throw Error(line, "모르는 키");
                if (map.ContainsKey(line.Key))
                    throw Error(line, $"{map[line.Key].Number}번째 줄에 이미 있음");
                map[line.Key] = line;
            }
            return map;
        }

        public SaveLine Required(Dictionary<string, SaveLine> map, string key)
        {
            if (!map.TryGetValue(key, out var line))
                throw Error($"\"{key}\" 줄이 없음");
            return line;
        }
    }

    internal sealed class SaveWriter
    {
        readonly StringBuilder _text = new StringBuilder();

        public void Comment(string text) => _text.Append("# ").Append(text).Append('\n');

        public void Line(string key, params object[] values) => Line(key, (IEnumerable<object>)values);

        public void Line(string key, IEnumerable<object> values)
        {
            _text.Append(key);
            foreach (object value in values)
            {
                string s = Format(value);
                if (s.Length == 0 || s.Any(char.IsWhiteSpace) || s.Contains('#'))
                    throw new InvalidOperationException($"저장 값 \"{s}\"에 공백이나 #이 있어 쓸 수 없습니다 ({key}).");
                _text.Append(' ').Append(s);
            }
            _text.Append('\n');
        }

        public void Codes(string key, IEnumerable<uint> codes) => Line(key, codes.Cast<object>());

        static string Format(object value) => value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";

        public override string ToString() => _text.ToString();
    }

    /// <summary>
    /// 공백이 든 글(레시피 이름 등)을 값 하나로 쓰기: %, #, =, 공백 문자만 %XX(UTF-16 코드 두 자리 16진수, 넘으면 %uXXXX)로 바꾼다.
    /// 한글은 그대로 두어 파일을 열어 읽을 수 있다. 빈 글은 "%"로 쓴다.
    /// </summary>
    internal static class SaveEscape
    {
        public static string Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "%";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '%' || c == '#' || c == '=' || char.IsWhiteSpace(c) || char.IsControl(c))
                    sb.Append(c <= 0xff ? $"%{(int)c:X2}" : $"%u{(int)c:X4}");
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Decode(string value, SaveReader r, SaveLine line)
        {
            if (value == "%")
                return "";
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c != '%')
                {
                    sb.Append(c);
                    continue;
                }
                bool wide = i + 1 < value.Length && value[i + 1] == 'u';
                int start = i + (wide ? 2 : 1), digits = wide ? 4 : 2;
                if (start + digits > value.Length ||
                    !int.TryParse(value.Substring(start, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                    throw r.Error(line, $"\"{value}\"의 % 표기가 틀림");
                sb.Append((char)code);
                i = start + digits - 1;
            }
            return sb.ToString();
        }
    }
}
