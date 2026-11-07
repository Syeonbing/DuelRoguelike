// OCG_DuelQueryLocation 결과를 카드 단위로 나누고 다시 합친다.
// 형식: [뒤따르는 길이 u32] + 칸마다 (빈 칸이면 [0 u16], 카드면 항목 반복: [크기 u16][항목 u32][값 (크기-4)바이트], QUERY_END 항목으로 끝).
using System;
using System.Collections.Generic;
using System.IO;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.AI
{
    internal sealed class QueryCard
    {
        /// <summary>QUERY_END를 뺀 항목들 (순서 유지).</summary>
        public readonly List<KeyValuePair<OcgQuery, byte[]>> Fields = new List<KeyValuePair<OcgQuery, byte[]>>();

        public byte[] Get(OcgQuery flag)
        {
            foreach (var field in Fields)
                if (field.Key == flag)
                    return field.Value;
            return null;
        }

        public OcgPosition Position
        {
            get
            {
                var value = Get(OcgQuery.Position);
                return value != null && value.Length >= 4 ? (OcgPosition)BitConverter.ToUInt32(value, 0) : OcgPosition.None;
            }
        }

        public bool IsPublic
        {
            get
            {
                var value = Get(OcgQuery.IsPublic);
                return value != null && value.Length >= 1 && value[0] != 0;
            }
        }

        /// <summary>keep에 든 항목만 남긴다.</summary>
        public void Keep(OcgQuery keep) => Fields.RemoveAll(f => (f.Key & keep) == 0);
    }

    internal static class QueryBuffer
    {
        /// <summary>빈 칸은 null로 넣는다.</summary>
        public static List<QueryCard> Parse(byte[] data)
        {
            var cards = new List<QueryCard>();
            if (data == null || data.Length < 4)
                return cards;
            int pos = 4;
            while (data.Length - pos >= 2)
            {
                int size = BitConverter.ToUInt16(data, pos);
                pos += 2;
                if (size == 0)
                {
                    cards.Add(null);
                    continue;
                }
                var card = new QueryCard();
                while (true)
                {
                    if (size < 4 || pos + size > data.Length)
                        throw new InvalidDataException($"조회 결과의 항목 크기가 잘못됐습니다: {size} (위치 {pos})");
                    var flag = (OcgQuery)BitConverter.ToUInt32(data, pos);
                    var value = new byte[size - 4];
                    Buffer.BlockCopy(data, pos + 4, value, 0, value.Length);
                    pos += size;
                    if (flag == OcgQuery.End)
                        break;
                    card.Fields.Add(new KeyValuePair<OcgQuery, byte[]>(flag, value));
                    if (data.Length - pos < 2)
                        throw new InvalidDataException("조회 결과가 QUERY_END 없이 끝났습니다.");
                    size = BitConverter.ToUInt16(data, pos);
                    pos += 2;
                }
                cards.Add(card);
            }
            return cards;
        }

        public static byte[] Build(IList<QueryCard> cards)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(0u);
                foreach (var card in cards)
                {
                    if (card == null)
                    {
                        w.Write((ushort)0);
                        continue;
                    }
                    foreach (var field in card.Fields)
                    {
                        w.Write((ushort)(field.Value.Length + 4));
                        w.Write((uint)field.Key);
                        w.Write(field.Value);
                    }
                    w.Write((ushort)4);
                    w.Write((uint)OcgQuery.End);
                }
                w.Flush();
                var result = stream.ToArray();
                BitConverter.GetBytes((uint)(result.Length - 4)).CopyTo(result, 0);
                return result;
            }
        }
    }
}
