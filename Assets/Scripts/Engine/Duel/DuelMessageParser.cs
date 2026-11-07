// 엔진 메시지 바이너리 → DuelMessage. 형식은 ygopro-core(C API 11.0)의 new_message 작성 코드를 따른다.
using System.Collections.Generic;
using System.IO;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Duel
{
    public static class DuelMessageParser
    {
        /// <summary>메시지 하나를 해석한다. 모르는 메시지는 RawMessage, 형식이 맞지 않으면 InvalidDataException.</summary>
        public static DuelMessage Parse(OcgMessage message)
        {
            var r = new MessageReader(message.Body);
            DuelMessage result = ParseBody(message.Type, r, message.Body);
            result.Type = message.Type;
            return result;
        }

        static DuelMessage ParseBody(OcgMessageType type, MessageReader r, byte[] body)
        {
            switch (type)
            {
                // ---- 본문 없음 ----
                case OcgMessageType.Retry:
                case OcgMessageType.ChainEnd:
                case OcgMessageType.AttackDisabled:
                case OcgMessageType.DamageStepStart:
                case OcgMessageType.DamageStepEnd:
                case OcgMessageType.ReverseDeck:
                    return new SimpleMessage();

                case OcgMessageType.Summoned:
                    return new SummonedMessage { Kind = SummonKind.Normal };
                case OcgMessageType.SpSummoned:
                    return new SummonedMessage { Kind = SummonKind.Special };
                case OcgMessageType.FlipSummoned:
                    return new SummonedMessage { Kind = SummonKind.Flip };

                // ---- 진행 ----
                case OcgMessageType.Hint:
                    return new HintMessage { HintType = (OcgHintType)r.U8(), Player = r.U8(), Data = r.U64() };
                case OcgMessageType.Win:
                    return new WinMessage { Winner = r.U8(), Reason = r.U8() };
                case OcgMessageType.NewTurn:
                    return new NewTurnMessage { Player = r.U8() };
                case OcgMessageType.NewPhase:
                    return new NewPhaseMessage { Phase = (OcgPhase)r.U16() };

                // ---- 카드 이동 ----
                case OcgMessageType.Draw:
                {
                    var m = new DrawMessage { Player = r.U8() };
                    uint count = r.U32();
                    for (uint i = 0; i < count; i++)
                    {
                        m.Codes.Add(r.U32());
                        r.U32(); // 표시 형식
                    }
                    return m;
                }
                case OcgMessageType.Move:
                    return new MoveMessage { Code = r.U32(), From = r.Location(), To = r.Location(), Reason = (OcgReason)r.U32() };
                case OcgMessageType.Summoning:
                    return new SummoningMessage { Kind = SummonKind.Normal, Code = r.U32(), Location = r.Location() };
                case OcgMessageType.SpSummoning:
                    return new SummoningMessage { Kind = SummonKind.Special, Code = r.U32(), Location = r.Location() };
                case OcgMessageType.FlipSummoning:
                    return new SummoningMessage { Kind = SummonKind.Flip, Code = r.U32(), Location = r.Location() };
                case OcgMessageType.Set:
                    return new SetMessage { Code = r.U32(), Location = r.Location() };
                case OcgMessageType.PosChange:
                {
                    uint code = r.U32();
                    var loc = r.LocationU8Seq();
                    var prev = (OcgPosition)r.U8();
                    var cur = (OcgPosition)r.U8();
                    return new PositionChangeMessage { Code = code, Location = loc, Previous = prev, Current = cur };
                }
                case OcgMessageType.Swap:
                {
                    var first = new CardRef(r.U32(), r.Location());
                    var second = new CardRef(r.U32(), r.Location());
                    return new SwapMessage { First = first, Second = second };
                }
                case OcgMessageType.ShuffleDeck:
                    return new ShuffleMessage { Player = r.U8() };
                case OcgMessageType.ShuffleHand:
                case OcgMessageType.ShuffleExtra:
                {
                    var m = new ShuffleMessage { Player = r.U8() };
                    uint count = r.U32();
                    for (uint i = 0; i < count; i++)
                        m.Codes.Add(r.U32());
                    return m;
                }
                case OcgMessageType.ConfirmCards:
                case OcgMessageType.ConfirmDecktop:
                case OcgMessageType.ConfirmExtratop:
                {
                    var m = new ConfirmCardsMessage { Player = r.U8() };
                    uint count = r.U32();
                    for (uint i = 0; i < count; i++)
                    {
                        uint code = r.U32();
                        m.Cards.Add(new CardRef(code, r.LocationU32Seq()));
                    }
                    return m;
                }
                case OcgMessageType.BecomeTarget:
                case OcgMessageType.CardSelected:
                case OcgMessageType.RemoveCards:
                    return ReadLocationList(new CardListMessage { Player = OcgConstants.PlayerNone }, r);
                case OcgMessageType.RandomSelected:
                    return ReadLocationList(new CardListMessage { Player = r.U8() }, r);
                case OcgMessageType.Equip:
                case OcgMessageType.CardTarget:
                case OcgMessageType.CancelTarget:
                    return new CardPairMessage { Card = r.Location(), Target = r.Location() };

                // ---- 체인 ----
                case OcgMessageType.Chaining:
                    return new ChainingMessage
                    {
                        Code = r.U32(),
                        Location = r.Location(),
                        TriggerController = r.U8(),
                        TriggerLocation = (OcgLocation)r.U8(),
                        TriggerSequence = r.U32(),
                        Description = r.U64(),
                        ChainCount = (int)r.U32(),
                    };
                case OcgMessageType.Chained:
                case OcgMessageType.ChainSolving:
                case OcgMessageType.ChainSolved:
                case OcgMessageType.ChainNegated:
                case OcgMessageType.ChainDisabled:
                    return new ChainEventMessage { ChainCount = r.U8() };

                // ---- LP ----
                case OcgMessageType.Damage:
                    return new LpChangeMessage { Kind = LpChangeKind.Damage, Player = r.U8(), Amount = r.U32() };
                case OcgMessageType.Recover:
                    return new LpChangeMessage { Kind = LpChangeKind.Recover, Player = r.U8(), Amount = r.U32() };
                case OcgMessageType.PayLpCost:
                    return new LpChangeMessage { Kind = LpChangeKind.PayCost, Player = r.U8(), Amount = r.U32() };
                case OcgMessageType.LpUpdate:
                    return new LpChangeMessage { Kind = LpChangeKind.Set, Player = r.U8(), Amount = r.U32() };

                // ---- 전투 ----
                case OcgMessageType.Attack:
                    return new AttackMessage { Attacker = r.Location(), Target = r.Location() };
                case OcgMessageType.Battle:
                {
                    var m = new BattleMessage { Attacker = r.Location(), AttackerAttack = r.I32(), AttackerDefense = r.I32() };
                    r.U8();
                    m.Target = r.Location();
                    m.TargetAttack = r.I32();
                    m.TargetDefense = r.I32();
                    r.U8();
                    return m;
                }

                // ---- 기타 ----
                case OcgMessageType.TossCoin:
                case OcgMessageType.TossDice:
                {
                    var m = new TossMessage { Player = r.U8() };
                    int count = r.U8();
                    for (int i = 0; i < count; i++)
                        m.Results.Add(r.U8());
                    return m;
                }
                case OcgMessageType.CardHint:
                    return new CardHintMessage { Card = r.Location(), HintType = r.U8(), Value = r.U64() };
                case OcgMessageType.PlayerHint:
                    return new PlayerHintMessage { Player = r.U8(), HintType = r.U8(), Value = r.U64() };
                case OcgMessageType.MissedEffect:
                    return new MissedEffectMessage { Card = r.Location(), Code = r.U32() };
                case OcgMessageType.AddCounter:
                case OcgMessageType.RemoveCounter:
                {
                    ushort counterType = r.U16();
                    var loc = r.LocationU8Seq();
                    return new CounterMessage { CounterType = counterType, Card = loc, Count = r.U16() };
                }

                // ---- 응답이 필요한 메시지 ----
                case OcgMessageType.SelectIdleCmd:
                {
                    var m = new SelectIdleCmdRequest { Player = r.U8() };
                    ReadCardList(m.Summonable, r, LocFormat.U32Seq);
                    ReadCardList(m.SpecialSummonable, r, LocFormat.U32Seq);
                    ReadCardList(m.Repositionable, r, LocFormat.U8Seq);
                    ReadCardList(m.MonsterSetable, r, LocFormat.U32Seq);
                    ReadCardList(m.SpellSetable, r, LocFormat.U32Seq);
                    ReadEffectList(m.Activatable, r, LocFormat.U32Seq);
                    m.CanBattlePhase = r.Bool();
                    m.CanEndPhase = r.Bool();
                    m.CanShuffle = r.Bool();
                    return m;
                }
                case OcgMessageType.SelectBattleCmd:
                {
                    var m = new SelectBattleCmdRequest { Player = r.U8() };
                    ReadEffectList(m.Activatable, r, LocFormat.U32Seq);
                    uint count = r.U32();
                    for (uint i = 0; i < count; i++)
                    {
                        uint code = r.U32();
                        var loc = r.LocationU8Seq();
                        m.Attackers.Add(new AttackOption { Code = code, Location = loc, CanDirectAttack = r.Bool() });
                    }
                    m.CanMain2 = r.Bool();
                    m.CanEndPhase = r.Bool();
                    return m;
                }
                case OcgMessageType.SelectEffectYn:
                    return new SelectEffectYesNoRequest { Player = r.U8(), Code = r.U32(), Location = r.Location(), Description = r.U64() };
                case OcgMessageType.SelectYesNo:
                    return new SelectYesNoRequest { Player = r.U8(), Description = r.U64() };
                case OcgMessageType.SelectOption:
                {
                    var m = new SelectOptionRequest { Player = r.U8() };
                    int count = r.U8();
                    for (int i = 0; i < count; i++)
                        m.Options.Add(r.U64());
                    return m;
                }
                case OcgMessageType.SelectCard:
                {
                    var m = new SelectCardRequest { Player = r.U8(), Cancelable = r.Bool(), Min = (int)r.U32(), Max = (int)r.U32() };
                    ReadCardList(m.Cards, r, LocFormat.Full);
                    return m;
                }
                case OcgMessageType.SelectTribute:
                {
                    var m = new SelectTributeRequest { Player = r.U8(), Cancelable = r.Bool(), Min = (int)r.U32(), Max = (int)r.U32() };
                    uint count = r.U32();
                    for (uint i = 0; i < count; i++)
                    {
                        uint code = r.U32();
                        var loc = r.LocationU32Seq();
                        m.Cards.Add(new TributeOption { Code = code, Location = loc, ReleaseParam = r.U8() });
                    }
                    return m;
                }
                case OcgMessageType.SelectUnselectCard:
                {
                    var m = new SelectUnselectCardRequest
                    {
                        Player = r.U8(), Finishable = r.Bool(), Cancelable = r.Bool(), Min = (int)r.U32(), Max = (int)r.U32(),
                    };
                    ReadCardList(m.Selectable, r, LocFormat.Full);
                    ReadCardList(m.Unselectable, r, LocFormat.Full);
                    return m;
                }
                case OcgMessageType.SelectChain:
                {
                    var m = new SelectChainRequest
                    {
                        Player = r.U8(), SpecialCount = r.U8(), Forced = r.Bool(), HintTiming = r.U32(), OpponentHintTiming = r.U32(),
                    };
                    ReadEffectList(m.Chains, r, LocFormat.Full);
                    return m;
                }
                case OcgMessageType.SelectPlace:
                case OcgMessageType.SelectDisfield:
                    return new SelectPlaceRequest
                    {
                        Player = r.U8(), Count = r.U8(), Flag = r.U32(), IsDisableField = type == OcgMessageType.SelectDisfield,
                    };
                case OcgMessageType.SelectPosition:
                    return new SelectPositionRequest { Player = r.U8(), Code = r.U32(), Positions = (OcgPosition)r.U8() };
                case OcgMessageType.SelectCounter:
                {
                    var m = new SelectCounterRequest { Player = r.U8(), CounterType = r.U16(), Count = r.U16() };
                    uint count = r.U32();
                    for (uint i = 0; i < count; i++)
                    {
                        uint code = r.U32();
                        var loc = r.LocationU8Seq();
                        m.Cards.Add(new CounterOption { Code = code, Location = loc, Count = r.U16() });
                    }
                    return m;
                }
                case OcgMessageType.SelectSum:
                {
                    var m = new SelectSumRequest { Player = r.U8() };
                    m.Exact = r.U8() == 0;
                    m.Target = (int)r.U32();
                    m.Min = (int)r.U32();
                    m.Max = (int)r.U32();
                    ReadSumList(m.MustSelect, r);
                    ReadSumList(m.Selectable, r);
                    return m;
                }
                case OcgMessageType.SortCard:
                case OcgMessageType.SortChain:
                {
                    var m = new SortCardRequest { Player = r.U8() };
                    ReadCardList(m.Cards, r, LocFormat.U32Loc);
                    return m;
                }
                case OcgMessageType.AnnounceRace:
                    return new AnnounceRaceRequest { Player = r.U8(), Count = r.U8(), Available = r.U64() };
                case OcgMessageType.AnnounceAttrib:
                    return new AnnounceAttributeRequest { Player = r.U8(), Count = r.U8(), Available = r.U32() };
                case OcgMessageType.AnnounceCard:
                {
                    var m = new AnnounceCardRequest { Player = r.U8() };
                    int count = r.U8();
                    for (int i = 0; i < count; i++)
                        m.Opcodes.Add(r.U64());
                    return m;
                }
                case OcgMessageType.AnnounceNumber:
                {
                    var m = new AnnounceNumberRequest { Player = r.U8() };
                    int count = r.U8();
                    for (int i = 0; i < count; i++)
                        m.Options.Add(r.U64());
                    return m;
                }
                case OcgMessageType.RockPaperScissors:
                    return new RockPaperScissorsRequest { Player = r.U8() };

                default:
                    return new RawMessage { Body = body };
            }
        }

        enum LocFormat { Full, U32Seq, U8Seq, U32Loc }

        static CardLocation ReadLoc(MessageReader r, LocFormat format)
        {
            switch (format)
            {
                case LocFormat.Full: return r.Location();
                case LocFormat.U32Seq: return r.LocationU32Seq();
                case LocFormat.U8Seq: return r.LocationU8Seq();
                case LocFormat.U32Loc: return r.LocationU32Loc();
                default: throw new InvalidDataException(format.ToString());
            }
        }

        static void ReadCardList(List<CardRef> list, MessageReader r, LocFormat format)
        {
            uint count = r.U32();
            for (uint i = 0; i < count; i++)
            {
                uint code = r.U32();
                list.Add(new CardRef(code, ReadLoc(r, format)));
            }
        }

        static void ReadEffectList(List<EffectOption> list, MessageReader r, LocFormat format)
        {
            uint count = r.U32();
            for (uint i = 0; i < count; i++)
            {
                uint code = r.U32();
                var loc = ReadLoc(r, format);
                list.Add(new EffectOption { Code = code, Location = loc, Description = r.U64(), ClientMode = r.U8() });
            }
        }

        static void ReadSumList(List<SumOption> list, MessageReader r)
        {
            uint count = r.U32();
            for (uint i = 0; i < count; i++)
            {
                uint code = r.U32();
                var loc = r.Location();
                list.Add(new SumOption { Code = code, Location = loc, Param = r.U32() });
            }
        }

        static CardListMessage ReadLocationList(CardListMessage m, MessageReader r)
        {
            uint count = r.U32();
            for (uint i = 0; i < count; i++)
                m.Cards.Add(r.Location());
            return m;
        }
    }
}
