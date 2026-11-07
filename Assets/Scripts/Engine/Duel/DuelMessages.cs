// 엔진 메시지를 해석한 결과. 바이너리 형식은 External/ygopro-core의 메시지 작성 코드(new_message)를 따른다.
// 화면·로그는 이 객체만 보고, 바이너리는 DuelMessageParser만 다룬다.
using System.Collections.Generic;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Duel
{
    public abstract class DuelMessage
    {
        public OcgMessageType Type;
    }

    /// <summary>플레이어의 응답이 필요한 메시지 (MSG_SELECT_*, MSG_ANNOUNCE_* 등).</summary>
    public abstract class DuelRequest : DuelMessage
    {
        /// <summary>응답할 플레이어 (0 또는 1).</summary>
        public byte Player;
    }

    /// <summary>해석하지 않는 메시지. 본문만 보관한다.</summary>
    public sealed class RawMessage : DuelMessage
    {
        public byte[] Body;
    }

    /// <summary>본문이 없는 메시지 (MSG_RETRY, MSG_CHAIN_END, MSG_ATTACK_DISABLED, MSG_DAMAGE_STEP_START/END 등).</summary>
    public sealed class SimpleMessage : DuelMessage { }

    // ---------------- 진행 ----------------

    public sealed class HintMessage : DuelMessage
    {
        public OcgHintType HintType;
        public byte Player;
        public ulong Data;
    }

    public sealed class WinMessage : DuelMessage
    {
        /// <summary>승자. OcgConstants.PlayerNone이면 무승부.</summary>
        public byte Winner;
        public byte Reason;
    }

    public sealed class NewTurnMessage : DuelMessage
    {
        public byte Player;
    }

    public sealed class NewPhaseMessage : DuelMessage
    {
        public OcgPhase Phase;
    }

    // ---------------- 카드 이동 ----------------

    public sealed class DrawMessage : DuelMessage
    {
        public byte Player;
        public List<uint> Codes = new List<uint>();
    }

    public sealed class MoveMessage : DuelMessage
    {
        public uint Code;
        public CardLocation From;
        public CardLocation To;
        public OcgReason Reason;
    }

    public enum SummonKind { Normal, Special, Flip }

    /// <summary>MSG_SUMMONING / MSG_SPSUMMONING / MSG_FLIPSUMMONING</summary>
    public sealed class SummoningMessage : DuelMessage
    {
        public SummonKind Kind;
        public uint Code;
        public CardLocation Location;
    }

    /// <summary>MSG_SUMMONED / MSG_SPSUMMONED / MSG_FLIPSUMMONED</summary>
    public sealed class SummonedMessage : DuelMessage
    {
        public SummonKind Kind;
    }

    public sealed class SetMessage : DuelMessage
    {
        public uint Code;
        public CardLocation Location;
    }

    public sealed class PositionChangeMessage : DuelMessage
    {
        public uint Code;
        public CardLocation Location;
        public OcgPosition Previous;
        public OcgPosition Current;
    }

    public sealed class SwapMessage : DuelMessage
    {
        public CardRef First;
        public CardRef Second;
    }

    public sealed class ShuffleMessage : DuelMessage
    {
        /// <summary>MSG_SHUFFLE_DECK / MSG_SHUFFLE_HAND / MSG_SHUFFLE_EXTRA</summary>
        public byte Player;
        public List<uint> Codes = new List<uint>();
    }

    /// <summary>MSG_CONFIRM_CARDS: Player에게 카드를 보여 준다.</summary>
    public sealed class ConfirmCardsMessage : DuelMessage
    {
        public byte Player;
        public List<CardRef> Cards = new List<CardRef>();
    }

    /// <summary>위치 목록만 있는 메시지 (MSG_BECOME_TARGET, MSG_CARD_SELECTED, MSG_RANDOM_SELECTED).</summary>
    public sealed class CardListMessage : DuelMessage
    {
        public byte Player;
        public List<CardLocation> Cards = new List<CardLocation>();
    }

    /// <summary>카드 두 장의 관계 (MSG_EQUIP, MSG_CARD_TARGET, MSG_CANCEL_TARGET).</summary>
    public sealed class CardPairMessage : DuelMessage
    {
        public CardLocation Card;
        public CardLocation Target;
    }

    // ---------------- 체인 ----------------

    public sealed class ChainingMessage : DuelMessage
    {
        public uint Code;
        public CardLocation Location;
        public byte TriggerController;
        public OcgLocation TriggerLocation;
        public uint TriggerSequence;
        public ulong Description;
        public int ChainCount;
    }

    /// <summary>MSG_CHAINED / MSG_CHAIN_SOLVING / MSG_CHAIN_SOLVED / MSG_CHAIN_NEGATED / MSG_CHAIN_DISABLED</summary>
    public sealed class ChainEventMessage : DuelMessage
    {
        public int ChainCount;
    }

    // ---------------- LP ----------------

    public enum LpChangeKind { Damage, Recover, PayCost, Set }

    /// <summary>MSG_DAMAGE / MSG_RECOVER / MSG_PAY_LPCOST / MSG_LPUPDATE</summary>
    public sealed class LpChangeMessage : DuelMessage
    {
        public LpChangeKind Kind;
        public byte Player;
        /// <summary>Set이면 새 LP, 나머지는 바뀐 양.</summary>
        public uint Amount;
    }

    // ---------------- 전투 ----------------

    public sealed class AttackMessage : DuelMessage
    {
        public CardLocation Attacker;
        /// <summary>비어 있으면 직접 공격.</summary>
        public CardLocation Target;
        public bool IsDirect => Target.IsEmpty;
    }

    public sealed class BattleMessage : DuelMessage
    {
        public CardLocation Attacker;
        public int AttackerAttack;
        public int AttackerDefense;
        public CardLocation Target;
        public int TargetAttack;
        public int TargetDefense;
    }

    // ---------------- 기타 ----------------

    /// <summary>MSG_TOSS_COIN / MSG_TOSS_DICE</summary>
    public sealed class TossMessage : DuelMessage
    {
        public byte Player;
        public List<byte> Results = new List<byte>();
    }

    /// <summary>MSG_CARD_HINT</summary>
    public sealed class CardHintMessage : DuelMessage
    {
        public CardLocation Card;
        public byte HintType;
        public ulong Value;
    }

    /// <summary>MSG_PLAYER_HINT</summary>
    public sealed class PlayerHintMessage : DuelMessage
    {
        public byte Player;
        public byte HintType;
        public ulong Value;
    }

    /// <summary>MSG_MISSED_EFFECT: 타이밍을 놓친 효과.</summary>
    public sealed class MissedEffectMessage : DuelMessage
    {
        public CardLocation Card;
        public uint Code;
    }

    /// <summary>MSG_ADD_COUNTER / MSG_REMOVE_COUNTER</summary>
    public sealed class CounterMessage : DuelMessage
    {
        public ushort CounterType;
        public CardLocation Card;
        public ushort Count;
    }

    // ================ 응답이 필요한 메시지 ================

    /// <summary>발동할 수 있는 효과 하나.</summary>
    public sealed class EffectOption
    {
        public uint Code;
        public CardLocation Location;
        public ulong Description;
        public byte ClientMode;
    }

    public sealed class SelectIdleCmdRequest : DuelRequest
    {
        public List<CardRef> Summonable = new List<CardRef>();
        public List<CardRef> SpecialSummonable = new List<CardRef>();
        public List<CardRef> Repositionable = new List<CardRef>();
        public List<CardRef> MonsterSetable = new List<CardRef>();
        public List<CardRef> SpellSetable = new List<CardRef>();
        public List<EffectOption> Activatable = new List<EffectOption>();
        public bool CanBattlePhase;
        public bool CanEndPhase;
        public bool CanShuffle;
    }

    public sealed class AttackOption
    {
        public uint Code;
        public CardLocation Location;
        public bool CanDirectAttack;
    }

    public sealed class SelectBattleCmdRequest : DuelRequest
    {
        public List<EffectOption> Activatable = new List<EffectOption>();
        public List<AttackOption> Attackers = new List<AttackOption>();
        public bool CanMain2;
        public bool CanEndPhase;
    }

    public sealed class SelectEffectYesNoRequest : DuelRequest
    {
        public uint Code;
        public CardLocation Location;
        public ulong Description;
    }

    public sealed class SelectYesNoRequest : DuelRequest
    {
        public ulong Description;
    }

    public sealed class SelectOptionRequest : DuelRequest
    {
        public List<ulong> Options = new List<ulong>();
    }

    public sealed class SelectCardRequest : DuelRequest
    {
        public bool Cancelable;
        public int Min;
        public int Max;
        public List<CardRef> Cards = new List<CardRef>();
    }

    public sealed class TributeOption
    {
        public uint Code;
        public CardLocation Location;
        /// <summary>이 카드 한 장이 몇 장분의 릴리스가 되는지.</summary>
        public int ReleaseParam;
    }

    public sealed class SelectTributeRequest : DuelRequest
    {
        public bool Cancelable;
        public int Min;
        public int Max;
        public List<TributeOption> Cards = new List<TributeOption>();
    }

    public sealed class SelectUnselectCardRequest : DuelRequest
    {
        public bool Finishable;
        public bool Cancelable;
        public int Min;
        public int Max;
        public List<CardRef> Selectable = new List<CardRef>();
        public List<CardRef> Unselectable = new List<CardRef>();
    }

    public sealed class SelectChainRequest : DuelRequest
    {
        public byte SpecialCount;
        public bool Forced;
        public uint HintTiming;
        public uint OpponentHintTiming;
        public List<EffectOption> Chains = new List<EffectOption>();
    }

    /// <summary>MSG_SELECT_PLACE / MSG_SELECT_DISFIELD. Flag의 켜진 비트는 고를 수 없는 칸이다.</summary>
    public sealed class SelectPlaceRequest : DuelRequest
    {
        public int Count;
        public uint Flag;
        public bool IsDisableField;
    }

    public sealed class SelectPositionRequest : DuelRequest
    {
        public uint Code;
        public OcgPosition Positions;
    }

    public sealed class CounterOption
    {
        public uint Code;
        public CardLocation Location;
        public int Count;
    }

    public sealed class SelectCounterRequest : DuelRequest
    {
        public ushort CounterType;
        public int Count;
        public List<CounterOption> Cards = new List<CounterOption>();
    }

    public sealed class SumOption
    {
        public uint Code;
        public CardLocation Location;
        /// <summary>하위 16비트와 상위 16비트에 값이 하나씩 (상위는 0일 수 있다).</summary>
        public uint Param;
    }

    public sealed class SelectSumRequest : DuelRequest
    {
        /// <summary>true면 합이 정확히 Target이어야 하고 장수는 Min~Max. false면 Target 이상(불필요한 카드 없이).</summary>
        public bool Exact;
        public int Target;
        public int Min;
        public int Max;
        public List<SumOption> MustSelect = new List<SumOption>();
        public List<SumOption> Selectable = new List<SumOption>();
    }

    /// <summary>MSG_SORT_CARD / MSG_SORT_CHAIN</summary>
    public sealed class SortCardRequest : DuelRequest
    {
        public List<CardRef> Cards = new List<CardRef>();
    }

    public sealed class AnnounceRaceRequest : DuelRequest
    {
        public int Count;
        public ulong Available;
    }

    public sealed class AnnounceAttributeRequest : DuelRequest
    {
        public int Count;
        public uint Available;
    }

    public sealed class AnnounceCardRequest : DuelRequest
    {
        /// <summary>선언할 수 있는 카드 조건 (엔진의 선언 조건 연산 코드).</summary>
        public List<ulong> Opcodes = new List<ulong>();
    }

    public sealed class AnnounceNumberRequest : DuelRequest
    {
        public List<ulong> Options = new List<ulong>();
    }

    public sealed class RockPaperScissorsRequest : DuelRequest { }
}
