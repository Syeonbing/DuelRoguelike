// 듀얼 상태. 카드 배치·LP·체인은 엔진 조회(OCG_DuelQueryField, OCG_DuelQueryLocation)로 다시 읽어 맞추고,
// 턴·페이즈·승패는 메시지로 따라간다. 화면은 이 상태만 보고 그린다.
using System.Collections.Generic;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Duel
{
    /// <summary>필드·패·묘지 등에 있는 카드 한 장의 현재 상태 (조회 결과).</summary>
    public sealed class CardInfo
    {
        public CardLocation Location;
        public uint Code;
        /// <summary>현재 카드명 기준 코드 (이름이 바뀌는 효과 반영).</summary>
        public uint Alias;
        public OcgCardType Type;
        public uint Level;
        public uint Rank;
        public OcgAttribute Attribute;
        public ulong Race;
        public int Attack;
        public int Defense;
        public int BaseAttack;
        public int BaseDefense;
        public byte Owner;
        /// <summary>양쪽 플레이어가 볼 수 있는 카드인지 (앞면, 공개 상태 등).</summary>
        public bool IsPublic;
        public bool IsHidden;
        public CardLocation EquipTarget;
        public List<uint> OverlayCodes = new List<uint>();
        /// <summary>카운터: (종류, 개수)</summary>
        public List<(ushort Type, ushort Count)> Counters = new List<(ushort, ushort)>();

        public OcgPosition Position => Location.Position;
        public bool IsFaceUp => (Position & OcgPosition.FaceUp) != 0;
        public bool IsMonster => (Type & OcgCardType.Monster) != 0;
    }

    public sealed class PlayerState
    {
        public const int MonsterZoneCount = 7;   // 0~4 메인 몬스터 존, 5~6 엑스트라 몬스터 존(이 게임은 쓰지 않음)
        public const int SpellZoneCount = 8;     // 0~4 마법·함정 존, 5 필드 존, 6~7 펜듈럼 존

        public int Lp;
        public int DeckCount;
        public int ExtraCount;
        public readonly CardInfo[] MonsterZones = new CardInfo[MonsterZoneCount];
        public readonly CardInfo[] SpellZones = new CardInfo[SpellZoneCount];
        public readonly List<CardInfo> Hand = new List<CardInfo>();
        public readonly List<CardInfo> Grave = new List<CardInfo>();
        public readonly List<CardInfo> Removed = new List<CardInfo>();
        public readonly List<CardInfo> Extra = new List<CardInfo>();
    }

    public sealed class ChainLink
    {
        public uint Code;
        public CardLocation Location;
        public byte TriggerController;
        public OcgLocation TriggerLocation;
        public uint TriggerSequence;
        public ulong Description;
    }

    public sealed class DuelState
    {
        const OcgQuery CardQuery =
            OcgQuery.Code | OcgQuery.Position | OcgQuery.Alias | OcgQuery.Type | OcgQuery.Level | OcgQuery.Rank |
            OcgQuery.Attribute | OcgQuery.Race | OcgQuery.Attack | OcgQuery.Defense | OcgQuery.BaseAttack |
            OcgQuery.BaseDefense | OcgQuery.EquipCard | OcgQuery.OverlayCard | OcgQuery.Counters | OcgQuery.Owner |
            OcgQuery.IsPublic | OcgQuery.IsHidden;

        public readonly PlayerState[] Players = { new PlayerState(), new PlayerState() };
        public readonly List<ChainLink> Chain = new List<ChainLink>();

        /// <summary>1부터 센다. 듀얼 시작 전에는 0.</summary>
        public int Turn;
        public byte TurnPlayer;
        public OcgPhase Phase;

        public bool IsFinished;
        /// <summary>승자. OcgConstants.PlayerNone이면 무승부.</summary>
        public byte Winner = OcgConstants.PlayerNone;
        public byte WinReason;

        public DuelState(uint startingLp)
        {
            Players[0].Lp = Players[1].Lp = (int)startingLp;
        }

        /// <summary>
        /// 새로 나타난 카드(덱에서 나온 카드 등)의 종류·레벨·공격력 등을 채울 카드 DB. 없으면 코드만 둔다.
        /// 엔진 조회(Refresh)가 메시지 묶음이 끝날 때 다시 맞추므로, 그 사이에 잠깐 보일 값이다.
        /// </summary>
        public Data.CardDatabase Database;

        /// <summary>
        /// 메시지로 알 수 있는 것(턴, 페이즈, LP, 승패)을 바로 반영한다. 카드 이동·표시 형식·체인도 메시지마다 바로 옮겨 두어
        /// 화면이 메시지 하나씩 보여 줄 때 배치가 그 단계와 맞게 한다. 정확한 값은 묶음이 끝날 때 Refresh가 엔진 조회로 다시 맞춘다.
        /// </summary>
        public void Apply(DuelMessage message)
        {
            switch (message)
            {
                case MoveMessage m:
                    ApplyMove(m);
                    break;
                case PositionChangeMessage m when !m.Location.IsOverlay:
                {
                    var card = Find(m.Location);
                    if (card != null)
                        card.Location = new CardLocation(m.Location.Controller, m.Location.Location, m.Location.Sequence, m.Current);
                    break;
                }
                case ChainingMessage m when m.ChainCount == Chain.Count + 1:
                    Chain.Add(new ChainLink
                    {
                        Code = m.Code,
                        Location = m.Location,
                        TriggerController = m.TriggerController,
                        TriggerLocation = m.TriggerLocation,
                        TriggerSequence = m.TriggerSequence,
                        Description = m.Description,
                    });
                    break;
                case SimpleMessage m when m.Type == OcgMessageType.ChainEnd:
                    Chain.Clear();
                    break;
                case DrawMessage m when m.Player < 2:
                {
                    // 드로우는 MSG_MOVE 없이 MSG_DRAW로만 온다. 덱 장수를 줄이고 패 끝에 붙인다 (뒷면으로: 상대 패를 드러내지 않는다).
                    var p = Players[m.Player];
                    p.DeckCount = System.Math.Max(0, p.DeckCount - m.Codes.Count);
                    foreach (uint code in m.Codes)
                    {
                        var card = NewCard(code);
                        card.Owner = m.Player;
                        card.Location = new CardLocation(m.Player, OcgLocation.Hand, (uint)p.Hand.Count, OcgPosition.FaceDownDefense);
                        p.Hand.Add(card);
                    }
                    break;
                }
                case ShuffleMessage m when m.Player < 2 && m.Codes.Count > 0
                                           && (m.Type == OcgMessageType.ShuffleHand || m.Type == OcgMessageType.ShuffleExtra):
                    Reorder(m.Type == OcgMessageType.ShuffleHand ? Players[m.Player].Hand : Players[m.Player].Extra, m.Codes);
                    break;
                case SummoningMessage m when m.Kind == SummonKind.Flip && !m.Location.IsOverlay:
                {
                    // 반전 소환은 MSG_POS_CHANGE 없이 온다: 앞면 공격 표시가 된다.
                    var card = Find(m.Location);
                    if (card != null)
                        card.Location = new CardLocation(m.Location.Controller, m.Location.Location, m.Location.Sequence, OcgPosition.FaceUpAttack);
                    break;
                }
                case NewTurnMessage m:
                    Turn++;
                    TurnPlayer = m.Player;
                    break;
                case NewPhaseMessage m:
                    Phase = m.Phase;
                    break;
                case LpChangeMessage m when m.Player < 2:
                {
                    var p = Players[m.Player];
                    switch (m.Kind)
                    {
                        case LpChangeKind.Damage:
                        case LpChangeKind.PayCost:
                            p.Lp -= (int)m.Amount; // 엔진처럼 0 아래로도 내려간다 (화면은 0으로 보인다)
                            break;
                        case LpChangeKind.Recover:
                            p.Lp += (int)m.Amount;
                            break;
                        case LpChangeKind.Set:
                            p.Lp = (int)m.Amount;
                            break;
                    }
                    break;
                }
                case WinMessage m:
                    IsFinished = true;
                    Winner = m.Winner;
                    WinReason = m.Reason;
                    break;
            }
        }

        // ---- 메시지로 카드 옮기기 (Refresh 전까지의 근사치. 칸·순번이 어긋나면 그냥 넘긴다) ----

        void ApplyMove(MoveMessage m)
        {
            CardInfo card = m.From.IsOverlay || m.From.Controller > 1 ? null : Take(m.From);
            if (m.To.IsOverlay || m.To.Controller > 1 || m.To.Location == OcgLocation.None)
                return;
            if (card == null)
                card = NewCard(m.Code);
            else if (m.Code != 0)
                card.Code = m.Code;
            card.Location = m.To;
            Put(card, m.To);
        }

        CardInfo NewCard(uint code)
        {
            var card = new CardInfo { Code = code };
            if (code != 0 && Database != null && Database.TryGet(code, out var record))
            {
                card.Type = record.Type;
                card.Level = record.Data.Level & 0xff;
                card.Attribute = (OcgAttribute)record.Data.Attribute;
                card.Race = record.Data.Race;
                card.Attack = card.BaseAttack = record.Data.Attack;
                card.Defense = card.BaseDefense = record.Data.Defense;
            }
            return card;
        }

        /// <summary>그 자리의 카드를 빼낸다. 덱이면 장수만 줄인다.</summary>
        CardInfo Take(CardLocation loc)
        {
            var p = Players[loc.Controller];
            int seq = (int)loc.Sequence;
            switch (loc.Location)
            {
                case OcgLocation.Deck:
                    p.DeckCount = System.Math.Max(0, p.DeckCount - 1);
                    return null;
                case OcgLocation.MonsterZone:
                    return TakeZone(p.MonsterZones, seq);
                case OcgLocation.SpellTrapZone:
                    return TakeZone(p.SpellZones, seq);
                case OcgLocation.Extra:
                    p.ExtraCount = System.Math.Max(0, p.ExtraCount - 1);
                    return TakeList(p.Extra, seq);
                default:
                    var list = ListOf(p, loc.Location);
                    return list != null ? TakeList(list, seq) : null;
            }
        }

        void Put(CardInfo card, CardLocation loc)
        {
            var p = Players[loc.Controller];
            int seq = (int)loc.Sequence;
            switch (loc.Location)
            {
                case OcgLocation.Deck:
                    p.DeckCount++;
                    return;
                case OcgLocation.MonsterZone:
                    if (seq < p.MonsterZones.Length) p.MonsterZones[seq] = card;
                    return;
                case OcgLocation.SpellTrapZone:
                    if (seq < p.SpellZones.Length) p.SpellZones[seq] = card;
                    return;
                case OcgLocation.Extra:
                    p.ExtraCount++;
                    PutList(p.Extra, card, seq);
                    return;
                default:
                    var list = ListOf(p, loc.Location);
                    if (list != null) PutList(list, card, seq);
                    return;
            }
        }

        static List<CardInfo> ListOf(PlayerState p, OcgLocation location)
        {
            switch (location)
            {
                case OcgLocation.Hand: return p.Hand;
                case OcgLocation.Grave: return p.Grave;
                case OcgLocation.Removed: return p.Removed;
                default: return null;
            }
        }

        static CardInfo TakeZone(CardInfo[] zones, int seq)
        {
            if (seq >= zones.Length) return null;
            var card = zones[seq];
            zones[seq] = null;
            return card;
        }

        static CardInfo TakeList(List<CardInfo> list, int seq)
        {
            if (seq >= list.Count) return null;
            var card = list[seq];
            list.RemoveAt(seq);
            Renumber(list);
            return card;
        }

        static void PutList(List<CardInfo> list, CardInfo card, int seq)
        {
            list.Insert(System.Math.Min(seq, list.Count), card);
            Renumber(list);
        }

        /// <summary>목록(패·묘지·제외·엑스트라)의 순번을 자리에 맞게 다시 매긴다.</summary>
        /// <summary>섞은 뒤 엔진이 알려 준 코드 순서대로 다시 놓는다. 같은 코드가 여럿이면 앞에서부터 쓴다.</summary>
        void Reorder(List<CardInfo> list, List<uint> codes)
        {
            if (list.Count != codes.Count)
                return; // 장수가 다르면 Refresh에 맡긴다
            var left = new List<CardInfo>(list);
            list.Clear();
            foreach (uint code in codes)
            {
                int i = left.FindIndex(c => c.Code == code);
                if (i < 0)
                    i = 0; // 코드를 모르는 카드(0) 등: 남은 것 중 앞의 것
                list.Add(left[i]);
                left.RemoveAt(i);
            }
            Renumber(list);
        }

        static void Renumber(List<CardInfo> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i].Location;
                if (l.Sequence != i)
                    list[i].Location = new CardLocation(l.Controller, l.Location, (uint)i, l.Position);
            }
        }

        /// <summary>엔진에 조회해 카드 배치, LP, 덱 장수, 체인을 다시 맞춘다.</summary>
        /// <summary>
        /// 메시지로 바로 반영하는 정보(LP, 장수, 카드 자리·코드·표시 형식, 체인)를 줄마다 글로 적는다.
        /// 배치 끝에 엔진 조회(Refresh) 전후를 비교해 메시지 반영이 맞는지 점검하는 데 쓴다.
        /// </summary>
        public string[] Snapshot()
        {
            var lines = new List<string>();
            for (int p = 0; p < 2; p++)
            {
                var player = Players[p];
                lines.Add($"P{p} LP {player.Lp}, 덱 {player.DeckCount}, 엑스트라 {player.ExtraCount}");
                lines.Add($"P{p} 몬스터 존: {Zones(player.MonsterZones)}");
                lines.Add($"P{p} 마법·함정 존: {Zones(player.SpellZones)}");
                lines.Add($"P{p} 패: {Codes(player.Hand, false)}");
                lines.Add($"P{p} 묘지: {Codes(player.Grave, false)}");
                lines.Add($"P{p} 제외: {Codes(player.Removed, true)}");
                lines.Add($"P{p} 엑스트라: {Codes(player.Extra, true)}");
            }
            lines.Add("체인: " + string.Join(" ", Chain.ConvertAll(c => c.Code.ToString())));
            return lines.ToArray();
        }

        static string Zones(CardInfo[] zones) =>
            string.Join(" ", System.Array.ConvertAll(zones, c => c == null ? "-" : $"{c.Code}/{(int)c.Position:X}"));

        static string Codes(List<CardInfo> cards, bool withFace) =>
            string.Join(" ", cards.ConvertAll(c => withFace ? $"{c.Code}{(c.IsFaceUp ? "" : "(뒷면)")}" : c.Code.ToString()));

        public void Refresh(OcgDuel duel)
        {
            ReadField(duel.QueryField());
            for (byte p = 0; p < 2; p++)
            {
                var player = Players[p];
                ReadZone(duel.QueryLocation(p, OcgLocation.MonsterZone, CardQuery), p, OcgLocation.MonsterZone, player.MonsterZones);
                ReadZone(duel.QueryLocation(p, OcgLocation.SpellTrapZone, CardQuery), p, OcgLocation.SpellTrapZone, player.SpellZones);
                ReadList(duel.QueryLocation(p, OcgLocation.Hand, CardQuery), p, OcgLocation.Hand, player.Hand);
                ReadList(duel.QueryLocation(p, OcgLocation.Grave, CardQuery), p, OcgLocation.Grave, player.Grave);
                ReadList(duel.QueryLocation(p, OcgLocation.Removed, CardQuery), p, OcgLocation.Removed, player.Removed);
                ReadList(duel.QueryLocation(p, OcgLocation.Extra, CardQuery), p, OcgLocation.Extra, player.Extra);
            }
        }

        /// <summary>위치로 카드를 찾는다. 없으면 null.</summary>
        public CardInfo Find(CardLocation location)
        {
            if (location.Controller > 1 || location.IsOverlay)
                return null;
            var p = Players[location.Controller];
            int seq = (int)location.Sequence;
            switch (location.Location)
            {
                case OcgLocation.MonsterZone: return seq < p.MonsterZones.Length ? p.MonsterZones[seq] : null;
                case OcgLocation.SpellTrapZone: return seq < p.SpellZones.Length ? p.SpellZones[seq] : null;
                case OcgLocation.Hand: return seq < p.Hand.Count ? p.Hand[seq] : null;
                case OcgLocation.Grave: return seq < p.Grave.Count ? p.Grave[seq] : null;
                case OcgLocation.Removed: return seq < p.Removed.Count ? p.Removed[seq] : null;
                case OcgLocation.Extra: return seq < p.Extra.Count ? p.Extra[seq] : null;
                default: return null;
            }
        }

        // OCG_DuelQueryField: [옵션 u32] + 플레이어마다 ([LP u32] [몬스터 존 7칸] [마법·함정 존 8칸] [덱·패·묘지·제외·엑스트라 장수, 엑스트라 앞면 펜듈럼 수 u32])
        //                     + [체인 수 u32] + 체인마다 ([코드 u32][loc_info][발동 컨트롤러 u8][발동 위치 u8][발동 순번 u32][설명 u64])
        // 칸마다 [있음 u8] 이고, 있으면 [표시 형식 u8][엑시즈 소재 수 u32]가 이어진다.
        void ReadField(byte[] data)
        {
            var r = new MessageReader(data);
            r.U32();
            for (int p = 0; p < 2; p++)
            {
                var player = Players[p];
                player.Lp = (int)r.U32();
                for (int i = 0; i < PlayerState.MonsterZoneCount + PlayerState.SpellZoneCount; i++)
                {
                    if (r.U8() != 0)
                    {
                        r.U8();
                        r.U32();
                    }
                }
                player.DeckCount = (int)r.U32();
                r.U32(); // 패 (목록 조회로 따로 읽는다)
                r.U32(); // 묘지
                r.U32(); // 제외
                player.ExtraCount = (int)r.U32();
                r.U32(); // 엑스트라 덱 앞면 펜듈럼 수
            }
            var engineChain = new List<ChainLink>();
            uint chainCount = r.U32();
            for (uint i = 0; i < chainCount; i++)
            {
                engineChain.Add(new ChainLink
                {
                    Code = r.U32(),
                    Location = r.Location(),
                    TriggerController = r.U8(),
                    TriggerLocation = (OcgLocation)r.U8(),
                    TriggerSequence = r.U32(),
                    Description = r.U64(),
                });
            }
            // 엔진은 처리가 끝난 체인을 바로 빼지만, 화면은 체인이 끝날 때(MSG_CHAIN_END)까지 "처리됨"으로 남겨 둔다.
            // 엔진 체인이 지금 체인의 앞부분과 같으면 (뒤쪽이 처리된 것뿐이면) 그대로 둔다.
            bool resolvedOnly = engineChain.Count < Chain.Count;
            for (int i = 0; resolvedOnly && i < engineChain.Count; i++)
                resolvedOnly = engineChain[i].Code == Chain[i].Code;
            if (!resolvedOnly)
            {
                Chain.Clear();
                Chain.AddRange(engineChain);
            }
        }

        static void ReadZone(byte[] data, byte controller, OcgLocation location, CardInfo[] zones)
        {
            var cards = ReadCards(data, controller, location);
            for (int i = 0; i < zones.Length; i++)
                zones[i] = i < cards.Count ? cards[i] : null;
        }

        static void ReadList(byte[] data, byte controller, OcgLocation location, List<CardInfo> list)
        {
            list.Clear();
            foreach (var card in ReadCards(data, controller, location))
                if (card != null)
                    list.Add(card);
        }

        // OCG_DuelQueryLocation: [전체 길이 u32] + 칸마다 (빈 칸이면 [0 u16], 카드면 항목 반복: [크기 u16][항목 u32][값 (크기-4)바이트], QUERY_END로 끝)
        static List<CardInfo> ReadCards(byte[] data, byte controller, OcgLocation location)
        {
            var result = new List<CardInfo>();
            if (data.Length < 4)
                return result;
            var r = new MessageReader(data);
            r.U32();
            uint sequence = 0;
            while (r.Remaining >= 2)
            {
                ushort size = r.U16();
                if (size == 0)
                {
                    result.Add(null);
                    sequence++;
                    continue;
                }
                var card = new CardInfo();
                var position = OcgPosition.None;
                while (true)
                {
                    var flag = (OcgQuery)r.U32();
                    int valueSize = size - 4;
                    if (flag == OcgQuery.End)
                        break;
                    var v = new MessageReader(r.Bytes(valueSize));
                    switch (flag)
                    {
                        case OcgQuery.Code: card.Code = v.U32(); break;
                        case OcgQuery.Position: position = (OcgPosition)v.U32(); break;
                        case OcgQuery.Alias: card.Alias = v.U32(); break;
                        case OcgQuery.Type: card.Type = (OcgCardType)v.U32(); break;
                        case OcgQuery.Level: card.Level = v.U32(); break;
                        case OcgQuery.Rank: card.Rank = v.U32(); break;
                        case OcgQuery.Attribute: card.Attribute = (OcgAttribute)v.U32(); break;
                        case OcgQuery.Race: card.Race = v.U64(); break;
                        case OcgQuery.Attack: card.Attack = v.I32(); break;
                        case OcgQuery.Defense: card.Defense = v.I32(); break;
                        case OcgQuery.BaseAttack: card.BaseAttack = v.I32(); break;
                        case OcgQuery.BaseDefense: card.BaseDefense = v.I32(); break;
                        case OcgQuery.Owner: card.Owner = v.U8(); break;
                        case OcgQuery.IsPublic: card.IsPublic = v.U8() != 0; break;
                        case OcgQuery.IsHidden: card.IsHidden = v.U8() != 0; break;
                        case OcgQuery.EquipCard:
                            card.EquipTarget = v.Location();
                            break;
                        case OcgQuery.OverlayCard:
                        {
                            uint n = v.U32();
                            for (uint i = 0; i < n; i++)
                                card.OverlayCodes.Add(v.U32());
                            break;
                        }
                        case OcgQuery.Counters:
                        {
                            uint n = v.U32();
                            for (uint i = 0; i < n; i++)
                            {
                                uint packed = v.U32();
                                card.Counters.Add(((ushort)(packed & 0xffff), (ushort)(packed >> 16)));
                            }
                            break;
                        }
                    }
                    size = r.U16();
                }
                card.Location = new CardLocation(controller, location, sequence, position);
                result.Add(card);
                sequence++;
            }
            return result;
        }
    }
}
