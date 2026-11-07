// 응답이 필요한 메시지에 대한 응답 바이트. 형식은 ygopro-core playerop.cpp의 응답 해석 코드를 따른다.
using System;
using System.Collections.Generic;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Duel
{
    public static class DuelResponses
    {
        // ---- 메인 페이즈 (MSG_SELECT_IDLECMD): (순번 << 16) | 종류 ----
        public static byte[] Summon(int index) => Command(0, index);
        public static byte[] SpecialSummon(int index) => Command(1, index);
        public static byte[] Reposition(int index) => Command(2, index);
        public static byte[] MonsterSet(int index) => Command(3, index);
        public static byte[] SpellSet(int index) => Command(4, index);
        public static byte[] IdleActivate(int index) => Command(5, index);
        public static byte[] ToBattlePhase() => Command(6, 0);
        public static byte[] IdleToEndPhase() => Command(7, 0);
        public static byte[] ShuffleHand() => Command(8, 0);

        // ---- 배틀 페이즈 (MSG_SELECT_BATTLECMD) ----
        public static byte[] BattleActivate(int index) => Command(0, index);
        public static byte[] Attack(int index) => Command(1, index);
        public static byte[] ToMain2() => Command(2, 0);
        public static byte[] BattleToEndPhase() => Command(3, 0);

        static byte[] Command(int type, int index) => Int32((index << 16) | type);

        // ---- 예/아니오, 선택지, 체인, 표시 형식, 숫자 ----
        public static byte[] YesNo(bool yes) => Int32(yes ? 1 : 0);
        public static byte[] Option(int index) => Int32(index);
        public static byte[] Chain(int index) => Int32(index);
        public static byte[] NoChain() => Int32(-1);
        public static byte[] Position(OcgPosition position) => Int32((int)position);
        public static byte[] AnnounceNumber(int index) => Int32(index);
        public static byte[] AnnounceCard(uint code) => Int32(unchecked((int)code));
        public static byte[] AnnounceRace(ulong races) => new ResponseWriter().U64(races).ToArray();
        public static byte[] AnnounceAttribute(uint attributes) => new ResponseWriter().U32(attributes).ToArray();

        /// <summary>가위바위보: 1 = 가위, 2 = 바위, 3 = 보</summary>
        public static byte[] RockPaperScissors(int hand)
        {
            if (hand < 1 || hand > 3) throw new ArgumentOutOfRangeException(nameof(hand));
            return Int32(hand);
        }

        // ---- 카드 선택 (MSG_SELECT_CARD, MSG_SELECT_TRIBUTE, MSG_SELECT_SUM) ----

        /// <summary>요청 목록의 순번으로 카드를 고른다. 형식: [0 i32][장수 u32][순번 u32...]</summary>
        public static byte[] Cards(IReadOnlyList<int> indices)
        {
            var w = new ResponseWriter().I32(0).U32((uint)indices.Count);
            foreach (int i in indices)
                w.U32((uint)i);
            return w.ToArray();
        }

        /// <summary>카드 선택 취소 (취소할 수 있을 때만).</summary>
        public static byte[] CancelCards() => Int32(-1);

        // ---- MSG_SELECT_UNSELECT_CARD ----

        /// <summary>
        /// 카드 한 장을 고르거나 고른 것을 해제한다. index는 Selectable 다음에 Unselectable을 이어 붙인 목록의 순번이다.
        /// </summary>
        public static byte[] SelectUnselect(int index) => new ResponseWriter().I32(1).I32(index).ToArray();

        /// <summary>선택 끝내기 / 취소 (Finishable 또는 Cancelable일 때만).</summary>
        public static byte[] FinishSelectUnselect() => Int32(-1);

        // ---- MSG_SELECT_PLACE ----

        public readonly struct Place
        {
            public readonly byte Player;
            public readonly OcgLocation Location;
            public readonly byte Sequence;

            public Place(byte player, OcgLocation location, byte sequence)
            {
                Player = player;
                Location = location;
                Sequence = sequence;
            }
        }

        public static byte[] Places(IReadOnlyList<Place> places)
        {
            var w = new ResponseWriter();
            foreach (var p in places)
                w.U8(p.Player).U8((byte)p.Location).U8(p.Sequence);
            return w.ToArray();
        }

        /// <summary>
        /// 고를 수 있는 칸을 모두 돌려준다. 순서: 자기 몬스터 존, 자기 마법·함정 존, 상대 몬스터 존, 상대 마법·함정 존.
        /// Flag의 켜진 비트는 고를 수 없는 칸이다. 하위 16비트가 요청한 플레이어 쪽, 상위 16비트가 상대 쪽.
        /// </summary>
        public static List<Place> AvailablePlaces(SelectPlaceRequest request)
        {
            var result = new List<Place>();
            for (int side = 0; side < 2; side++)
            {
                byte player = (byte)(side == 0 ? request.Player : 1 - request.Player);
                int shift = side * 16;
                for (int seq = 0; seq < 7; seq++)
                    if ((request.Flag & (1u << (shift + seq))) == 0)
                        result.Add(new Place(player, OcgLocation.MonsterZone, (byte)seq));
                for (int seq = 0; seq < 8; seq++)
                    if ((request.Flag & (1u << (shift + 8 + seq))) == 0)
                        result.Add(new Place(player, OcgLocation.SpellTrapZone, (byte)seq));
            }
            return result;
        }

        // ---- MSG_SELECT_COUNTER: 카드마다 가져갈 카운터 수 (u16) ----
        public static byte[] Counters(IReadOnlyList<int> counts)
        {
            var w = new ResponseWriter();
            foreach (int c in counts)
                w.U16((ushort)c);
            return w.ToArray();
        }

        // ---- MSG_SORT_CARD / MSG_SORT_CHAIN ----

        /// <summary>엔진이 정한 순서를 그대로 쓴다.</summary>
        public static byte[] DefaultSort() => new ResponseWriter().I8(-1).ToArray();

        /// <summary>order[i] = i번째 카드의 새 순번.</summary>
        public static byte[] Sort(IReadOnlyList<int> order)
        {
            var w = new ResponseWriter();
            foreach (int o in order)
                w.I8((sbyte)o);
            return w.ToArray();
        }

        static byte[] Int32(int value) => BitConverter.GetBytes(value);
    }
}
