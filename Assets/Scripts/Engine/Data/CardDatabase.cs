// Project Ignis BabelCDB 형식의 카드 DB(.cdb)를 읽는다.
// 열 해석은 EDOPro의 gframe/data_manager.cpp와 같게 맞췄다.
using System.Collections.Generic;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Data
{
    /// <summary>카드 DB 한 장. 텍스트는 .cdb에 들어 있는 원문(영문)이다.</summary>
    public sealed class CardRecord
    {
        public OcgCardData Data;
        public uint Ot;
        public ulong Category;
        public string Name;
        public string Description;

        /// <summary>효과 선택지·힌트 문구 str1~str16. 없는 칸은 null.</summary>
        public string[] Strings;

        public OcgCardType Type => (OcgCardType)Data.Type;
    }

    public sealed class CardDatabase
    {
        const string SelectAll =
            "SELECT datas.id, datas.ot, datas.alias, datas.setcode, datas.type, datas.atk, datas.def, " +
            "datas.level, datas.race, datas.attribute, datas.category, texts.name, texts.desc, " +
            "texts.str1, texts.str2, texts.str3, texts.str4, texts.str5, texts.str6, texts.str7, texts.str8, " +
            "texts.str9, texts.str10, texts.str11, texts.str12, texts.str13, texts.str14, texts.str15, texts.str16 " +
            "FROM datas, texts WHERE texts.id = datas.id";

        readonly Dictionary<uint, CardRecord> _cards = new Dictionary<uint, CardRecord>();

        public int Count => _cards.Count;

        /// <summary>sqlite3.dll의 SQLite 버전.</summary>
        public static string SqliteVersion => SqliteReader.Version;

        /// <summary>.cdb 파일 하나를 읽어 더한다. 같은 카드 코드가 이미 있으면 나중에 읽은 쪽으로 바꾼다.</summary>
        /// <returns>이 파일에서 읽은 카드 수</returns>
        public int Load(string cdbPath)
        {
            int loaded = 0;
            using (var db = new SqliteReader(cdbPath))
            {
                db.Query(SelectAll, row =>
                {
                    var record = ReadRow(row);
                    _cards[record.Data.Code] = record;
                    loaded++;
                });
            }
            return loaded;
        }

        public bool TryGet(uint code, out CardRecord record) => _cards.TryGetValue(code, out record);

        /// <summary>엔진의 카드 데이터 요청(OcgDuelOptions.CardSource)에 그대로 넘길 수 있다.</summary>
        public OcgCardData? GetCardData(uint code) =>
            _cards.TryGetValue(code, out var record) ? record.Data : (OcgCardData?)null;

        /// <summary>카드명. DB에 없으면 코드 숫자를 돌려준다.</summary>
        public string GetName(uint code) =>
            _cards.TryGetValue(code, out var record) && !string.IsNullOrEmpty(record.Name) ? record.Name : code.ToString();

        static CardRecord ReadRow(SqliteRow row)
        {
            var data = new OcgCardData
            {
                Code = (uint)row.GetInt64(0),
                Alias = (uint)row.GetInt64(2),
                SetCodes = UnpackSetCodes((ulong)row.GetInt64(3)),
                Type = (uint)row.GetInt64(4),
                Attack = row.GetInt32(5),
                Defense = row.GetInt32(6),
                Race = (ulong)row.GetInt64(8),
                Attribute = (uint)row.GetInt64(9),
            };

            // 링크 몬스터는 def 칸에 링크 마커가 들어 있다.
            if ((data.Type & (uint)OcgCardType.Link) != 0)
            {
                data.LinkMarker = (uint)data.Defense;
                data.Defense = 0;
            }

            // level 칸: 하위 8비트 = 레벨/랭크, 16~23비트 = 오른쪽 스케일, 24~31비트 = 왼쪽 스케일
            int level = row.GetInt32(7);
            data.Level = level < 0 ? unchecked((uint)-(level & 0xff)) : (uint)(level & 0xff);
            data.LeftScale = (uint)((level >> 24) & 0xff);
            data.RightScale = (uint)((level >> 16) & 0xff);

            var strings = new string[16];
            for (int i = 0; i < strings.Length; i++)
                strings[i] = NullIfEmpty(row.GetText(13 + i));

            return new CardRecord
            {
                Data = data,
                Ot = (uint)row.GetInt64(1),
                Category = (ulong)row.GetInt64(10),
                Name = row.GetText(11),
                Description = row.GetText(12),
                Strings = strings,
            };
        }

        // setcode 칸: 16비트 카드군 코드 최대 4개를 64비트 하나에 묶어 둔다.
        static ushort[] UnpackSetCodes(ulong packed)
        {
            var list = new List<ushort>(4);
            for (int i = 0; i < 4; i++)
            {
                ushort setCode = (ushort)((packed >> (i * 16)) & 0xffff);
                if (setCode != 0)
                    list.Add(setCode);
            }
            return list.Count == 0 ? null : list.ToArray();
        }

        static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
    }
}
