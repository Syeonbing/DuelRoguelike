// 카드 위치와 엔진 메시지 바이너리 읽기.
using System;
using System.IO;
using System.Text;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Duel
{
    /// <summary>
    /// 카드 위치. 엔진의 loc_info와 같다.
    /// 위치에 Overlay가 섞여 있으면 엑시즈 소재이고, 이때 Sequence는 소재를 가진 몬스터의 자리, Position 칸은 소재 순번이다.
    /// </summary>
    public readonly struct CardLocation : IEquatable<CardLocation>
    {
        public readonly byte Controller;
        public readonly OcgLocation Location;
        public readonly uint Sequence;
        public readonly OcgPosition Position;

        public CardLocation(byte controller, OcgLocation location, uint sequence, OcgPosition position = OcgPosition.None)
        {
            Controller = controller;
            Location = location;
            Sequence = sequence;
            Position = position;
        }

        public bool IsEmpty => Location == OcgLocation.None;
        public bool IsOverlay => (Location & OcgLocation.Overlay) != 0;
        public bool IsOnField => (Location & OcgLocation.OnField) != 0 && !IsOverlay;

        /// <summary>표시 형식을 뺀 자리만 비교한다.</summary>
        public bool SamePlace(CardLocation other) =>
            Controller == other.Controller && Location == other.Location && Sequence == other.Sequence;

        public bool Equals(CardLocation other) => SamePlace(other) && Position == other.Position;
        public override bool Equals(object obj) => obj is CardLocation other && Equals(other);
        public override int GetHashCode() => (Controller << 24) ^ ((int)Location << 16) ^ (int)Sequence ^ ((int)Position << 8);
        public override string ToString() => $"P{Controller}:{Location}[{Sequence}]({Position})";
    }

    /// <summary>카드 코드와 위치.</summary>
    public readonly struct CardRef
    {
        public readonly uint Code;
        public readonly CardLocation Location;

        public CardRef(uint code, CardLocation location)
        {
            Code = code;
            Location = location;
        }
    }

    /// <summary>엔진 메시지·조회 결과(리틀 엔디언)를 앞에서부터 읽는다.</summary>
    internal sealed class MessageReader
    {
        readonly byte[] _data;
        int _pos;

        public MessageReader(byte[] data, int offset = 0)
        {
            _data = data ?? Array.Empty<byte>();
            _pos = offset;
        }

        public int Position => _pos;
        public int Remaining => _data.Length - _pos;

        public byte U8() { Need(1); return _data[_pos++]; }
        public bool Bool() => U8() != 0;
        public ushort U16() { Need(2); ushort v = BitConverter.ToUInt16(_data, _pos); _pos += 2; return v; }
        public uint U32() { Need(4); uint v = BitConverter.ToUInt32(_data, _pos); _pos += 4; return v; }
        public int I32() { Need(4); int v = BitConverter.ToInt32(_data, _pos); _pos += 4; return v; }
        public ulong U64() { Need(8); ulong v = BitConverter.ToUInt64(_data, _pos); _pos += 8; return v; }

        public void Skip(int count) { Need(count); _pos += count; }

        public byte[] Bytes(int count)
        {
            Need(count);
            var result = new byte[count];
            Buffer.BlockCopy(_data, _pos, result, 0, count);
            _pos += count;
            return result;
        }

        /// <summary>loc_info 10바이트: [컨트롤러 u8][위치 u8][순번 u32][표시 형식 u32]</summary>
        public CardLocation Location()
        {
            byte con = U8();
            var loc = (OcgLocation)U8();
            uint seq = U32();
            var pos = (OcgPosition)U32();
            return new CardLocation(con, loc, seq, pos);
        }

        /// <summary>6바이트: [컨트롤러 u8][위치 u8][순번 u32]</summary>
        public CardLocation LocationU32Seq() => new CardLocation(U8(), (OcgLocation)U8(), U32());

        /// <summary>3바이트: [컨트롤러 u8][위치 u8][순번 u8]</summary>
        public CardLocation LocationU8Seq() => new CardLocation(U8(), (OcgLocation)U8(), U8());

        /// <summary>9바이트: [컨트롤러 u8][위치 u32][순번 u32] (MSG_SORT_CARD)</summary>
        public CardLocation LocationU32Loc() => new CardLocation(U8(), (OcgLocation)U32(), U32());

        void Need(int count)
        {
            if (_pos + count > _data.Length)
                throw new InvalidDataException($"메시지가 예상보다 짧습니다 (위치 {_pos}, 필요 {count}바이트, 전체 {_data.Length}바이트).");
        }
    }

    /// <summary>엔진에 보낼 응답(리틀 엔디언)을 만든다.</summary>
    internal sealed class ResponseWriter
    {
        readonly System.Collections.Generic.List<byte> _bytes = new System.Collections.Generic.List<byte>();

        public ResponseWriter I8(sbyte v) { _bytes.Add(unchecked((byte)v)); return this; }
        public ResponseWriter U8(byte v) { _bytes.Add(v); return this; }
        public ResponseWriter U16(ushort v) { _bytes.AddRange(BitConverter.GetBytes(v)); return this; }
        public ResponseWriter I32(int v) { _bytes.AddRange(BitConverter.GetBytes(v)); return this; }
        public ResponseWriter U32(uint v) { _bytes.AddRange(BitConverter.GetBytes(v)); return this; }
        public ResponseWriter U64(ulong v) { _bytes.AddRange(BitConverter.GetBytes(v)); return this; }
        public byte[] ToArray() => _bytes.ToArray();

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (byte b in _bytes) sb.Append(b.ToString("X2")).Append(' ');
            return sb.ToString().TrimEnd();
        }
    }
}
