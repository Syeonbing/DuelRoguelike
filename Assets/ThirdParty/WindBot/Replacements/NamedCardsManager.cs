// WindBot Ignite의 YGOSharp.OCGWrapper/NamedCardsManager.cs를 대신한다 (AGPL-3.0-or-later, 원본: https://github.com/ProjectIgnis/windbot).
// 원본은 Mono.Data.Sqlite로 cards.cdb를 직접 읽는다. 이 프로젝트는 카드 DB를 이미 엔진 계층(CardDatabase)에서 읽으므로,
// 같은 공개 API(GetCard, GetAllCards)를 두고 카드는 바깥에서 AddCard로 넣는다.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace YGOSharp.OCGWrapper
{
    public static class NamedCardsManager
    {
        private static readonly Dictionary<long, NamedCard> _cards = new Dictionary<long, NamedCard>();
        private static List<NamedCard> _all;

        /// <summary>들어 있는 카드 수.</summary>
        public static int Count => _cards.Count;

        /// <summary>모든 카드를 지운다.</summary>
        public static void Clear()
        {
            _cards.Clear();
            _all = null;
        }

        /// <summary>
        /// 카드 한 장을 넣는다. 값은 cards.cdb의 datas 표와 같은 형식이다.
        /// levelInfo: 하위 8비트 레벨/랭크, 16~23비트 오른쪽 스케일, 24~31비트 왼쪽 스케일.
        /// 링크 몬스터는 defense 자리에 링크 마커를 넣는다 (DB와 같다).
        /// </summary>
        public static void AddCard(int id, int ot, int alias, ulong setcode, uint type, uint levelInfo,
            ulong race, uint attribute, int attack, int defense, string name, string description)
        {
            var record = new ValuesRecord(new object[]
            {
                id, ot, alias, (long)setcode, (long)type, (long)levelInfo, (long)race, (long)attribute, attack, defense,
                name ?? string.Empty, description ?? string.Empty,
            });
            _cards[id] = new NamedCard(record);
            _all = null;
        }

        internal static NamedCard GetCard(long id)
        {
            NamedCard card;
            return _cards.TryGetValue(id, out card) ? card : null;
        }

        public static IList<NamedCard> GetAllCards()
        {
            if (_all == null)
                _all = _cards.Values.ToList();
            return _all;
        }

        /// <summary>Card·NamedCard 생성자가 받는 IDataRecord를 값 배열로 흉내 낸다.</summary>
        private sealed class ValuesRecord : IDataRecord
        {
            private readonly object[] _values;

            public ValuesRecord(object[] values) { _values = values; }

            public int FieldCount => _values.Length;
            public object this[int i] => _values[i];
            public object this[string name] => throw new NotSupportedException();
            public bool GetBoolean(int i) => Convert.ToBoolean(_values[i]);
            public byte GetByte(int i) => Convert.ToByte(_values[i]);
            public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public char GetChar(int i) => Convert.ToChar(_values[i]);
            public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public IDataReader GetData(int i) => throw new NotSupportedException();
            public string GetDataTypeName(int i) => _values[i].GetType().Name;
            public DateTime GetDateTime(int i) => Convert.ToDateTime(_values[i]);
            public decimal GetDecimal(int i) => Convert.ToDecimal(_values[i]);
            public double GetDouble(int i) => Convert.ToDouble(_values[i]);
            public Type GetFieldType(int i) => _values[i].GetType();
            public float GetFloat(int i) => Convert.ToSingle(_values[i]);
            public Guid GetGuid(int i) => throw new NotSupportedException();
            public short GetInt16(int i) => Convert.ToInt16(_values[i]);
            public int GetInt32(int i) => Convert.ToInt32(_values[i]);
            public long GetInt64(int i) => Convert.ToInt64(_values[i]);
            public string GetName(int i) => i.ToString();
            public int GetOrdinal(string name) => throw new NotSupportedException();
            public string GetString(int i) => (string)_values[i];
            public object GetValue(int i) => _values[i];
            public int GetValues(object[] values)
            {
                int n = Math.Min(values.Length, _values.Length);
                Array.Copy(_values, values, n);
                return n;
            }
            public bool IsDBNull(int i) => _values[i] == null;
        }
    }
}
