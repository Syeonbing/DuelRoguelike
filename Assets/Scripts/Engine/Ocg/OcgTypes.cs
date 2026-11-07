// ocgcore의 상태 코드, 듀얼 플래그, 메시지 종류.
// 원본: External/ygopro-core/ocgapi_types.h, ocgapi_constants.h (AGPL-3.0-or-later)
using System;

namespace DuelRoguelike.Engine.Ocg
{
    public enum OcgDuelCreationStatus
    {
        Success = 0,
        NoOutput = 1,
        NotCreated = 2,
        NullDataReader = 3,
        NullScriptReader = 4,
        IncompatibleLuaApi = 5,
        NullRngSeed = 6,
    }

    public enum OcgDuelStatus
    {
        End = 0,       // 듀얼 종료
        Awaiting = 1,  // 플레이어 응답 필요
        Continue = 2,
    }

    public enum OcgLogType
    {
        Error = 0,
        FromScript = 1,
        ForDebug = 2,
        Undefined = 3,
    }

    // DUEL_* 플래그 (ocgapi_constants.h)
    [Flags]
    public enum OcgDuelFlags : ulong
    {
        None = 0,
        TestMode = 0x01,
        AttackFirstTurn = 0x02,
        UseTrapsInNewChain = 0x04,
        SixStepBattleStep = 0x08,
        PseudoShuffle = 0x10,
        TriggerWhenPrivateKnowledge = 0x20,
        SimpleAi = 0x40,
        Relay = 0x80,
        OcgObsoleteIgnition = 0x100,
        FirstTurnDraw = 0x200,
        OneFaceupField = 0x400,
        PZone = 0x800,
        SeparatePZone = 0x1000,
        EMZone = 0x2000,
        FsxMMZone = 0x4000,
        TrapMonstersNotUseZone = 0x8000,
        ReturnToDeckTriggers = 0x10000,
        TriggerOnlyInLocation = 0x20000,
        SpSummonOnceOldNegate = 0x40000,
        CannotSummonOathOld = 0x80000,
        NoStandbyPhase = 0x100000,
        NoMainPhase2 = 0x200000,
        ThreeColumnsField = 0x400000,
        DrawUntil5 = 0x800000,
        NoHandLimit = 0x1000000,
        UnlimitedSummons = 0x2000000,
        InvertedQuickPriority = 0x4000000,
        EquipNotSentIfMissingTarget = 0x8000000,
        ZeroAtkDestroyed = 0x10000000,
        StoreAttackReplays = 0x20000000,
        SingleChainInDamageSubstep = 0x40000000,
        CanReposIfNonSumPlayer = 0x80000000,
        TcgSegocNonPublic = 0x100000000,
        TcgSegocFirstTrigger = 0x200000000,
        TcgFastEffectIgnition = 0x400000000,
        ExtraDeckRitual = 0x800000000,
        NormalSummonFaceupDef = 0x1000000000,

        // DUEL_MODE_MR5 (현재 마스터 룰)
        ModeMR5 = PZone | EMZone | FsxMMZone | TrapMonstersNotUseZone | TriggerOnlyInLocation,
    }

    // TYPE_* 카드 종류 (ocgapi_constants.h)
    [Flags]
    public enum OcgCardType : uint
    {
        None = 0,
        Monster = 0x1,
        Spell = 0x2,
        Trap = 0x4,
        Normal = 0x10,
        Effect = 0x20,
        Fusion = 0x40,
        Ritual = 0x80,
        TrapMonster = 0x100,
        Spirit = 0x200,
        Union = 0x400,
        Gemini = 0x800,
        Tuner = 0x1000,
        Synchro = 0x2000,
        Token = 0x4000,
        Maximum = 0x8000,
        QuickPlay = 0x10000,
        Continuous = 0x20000,
        Equip = 0x40000,
        Field = 0x80000,
        Counter = 0x100000,
        Flip = 0x200000,
        Toon = 0x400000,
        Xyz = 0x800000,
        Pendulum = 0x1000000,
        SpSummon = 0x2000000,
        Link = 0x4000000,
    }

    // LOCATION_* 카드 위치 (ocgapi_constants.h)
    [Flags]
    public enum OcgLocation : uint
    {
        None = 0,
        Deck = 0x01,
        Hand = 0x02,
        MonsterZone = 0x04,
        SpellTrapZone = 0x08,
        Grave = 0x10,
        Removed = 0x20,
        Extra = 0x40,
        Overlay = 0x80,
        OnField = MonsterZone | SpellTrapZone,
    }

    // POS_* 표시 형식 (ocgapi_constants.h)
    [Flags]
    public enum OcgPosition : uint
    {
        None = 0,
        FaceUpAttack = 0x1,
        FaceDownAttack = 0x2,
        FaceUpDefense = 0x4,
        FaceDownDefense = 0x8,
        FaceUp = FaceUpAttack | FaceUpDefense,
        FaceDown = FaceDownAttack | FaceDownDefense,
        Attack = FaceUpAttack | FaceDownAttack,
        Defense = FaceUpDefense | FaceDownDefense,
    }

    // PHASE_* 페이즈 (ocgapi_constants.h)
    [Flags]
    public enum OcgPhase : ushort
    {
        None = 0,
        Draw = 0x01,
        Standby = 0x02,
        Main1 = 0x04,
        BattleStart = 0x08,
        BattleStep = 0x10,
        Damage = 0x20,
        DamageCalculation = 0x40,
        Battle = 0x80,
        Main2 = 0x100,
        End = 0x200,
    }

    // REASON_* 카드가 움직인 이유 (ocgapi_constants.h)
    [Flags]
    public enum OcgReason : uint
    {
        None = 0,
        Destroy = 0x1,
        Release = 0x2,
        Temporary = 0x4,
        Material = 0x8,
        Summon = 0x10,
        Battle = 0x20,
        Effect = 0x40,
        Cost = 0x80,
        Adjust = 0x100,
        LostTarget = 0x200,
        Rule = 0x400,
        SpSummon = 0x800,
        DisSummon = 0x1000,
        Flip = 0x2000,
        Discard = 0x4000,
        ReverseDamage = 0x8000,
        ReverseRecover = 0x10000,
        Return = 0x20000,
        Fusion = 0x40000,
        Synchro = 0x80000,
        Ritual = 0x100000,
        Xyz = 0x200000,
        Replace = 0x1000000,
        Draw = 0x2000000,
        Redirect = 0x4000000,
        Link = 0x10000000,
    }

    // HINT_* (MSG_HINT의 종류)
    public enum OcgHintType : byte
    {
        Event = 1,
        Message = 2,
        SelectMessage = 3,
        OptionSelected = 4,
        Effect = 5,
        Race = 6,
        Attribute = 7,
        Code = 8,
        Number = 9,
        Card = 10,
        Zone = 11,
    }

    // QUERY_* 카드 상태 조회 항목 (ocgapi_constants.h)
    [Flags]
    public enum OcgQuery : uint
    {
        None = 0,
        Code = 0x1,
        Position = 0x2,
        Alias = 0x4,
        Type = 0x8,
        Level = 0x10,
        Rank = 0x20,
        Attribute = 0x40,
        Race = 0x80,
        Attack = 0x100,
        Defense = 0x200,
        BaseAttack = 0x400,
        BaseDefense = 0x800,
        Reason = 0x1000,
        ReasonCard = 0x2000,
        EquipCard = 0x4000,
        TargetCard = 0x8000,
        OverlayCard = 0x10000,
        Counters = 0x20000,
        Owner = 0x40000,
        Status = 0x80000,
        IsPublic = 0x100000,
        LeftScale = 0x200000,
        RightScale = 0x400000,
        Link = 0x800000,
        IsHidden = 0x1000000,
        Cover = 0x2000000,
        End = 0x80000000,
    }

    // ATTRIBUTE_* 속성
    [Flags]
    public enum OcgAttribute : uint
    {
        None = 0,
        Earth = 0x01,
        Water = 0x02,
        Fire = 0x04,
        Wind = 0x08,
        Light = 0x10,
        Dark = 0x20,
        Divine = 0x40,
    }

    public static class OcgConstants
    {
        /// <summary>MSG_WIN의 승자 값이 이것이면 무승부.</summary>
        public const byte PlayerNone = 2;

        /// <summary>MSG_WIN 이유: LP 0</summary>
        public const byte WinReasonLp = 1;

        /// <summary>MSG_WIN 이유: 덱에서 드로우하지 못함</summary>
        public const byte WinReasonDeckOut = 2;
    }

    // MSG_* 메시지 종류 (ocgapi_constants.h)
    public enum OcgMessageType : byte
    {
        Retry = 1,
        Hint = 2,
        Waiting = 3,
        Start = 4,
        Win = 5,
        UpdateData = 6,
        UpdateCard = 7,
        RequestDeck = 8,
        SelectBattleCmd = 10,
        SelectIdleCmd = 11,
        SelectEffectYn = 12,
        SelectYesNo = 13,
        SelectOption = 14,
        SelectCard = 15,
        SelectChain = 16,
        SelectPlace = 18,
        SelectPosition = 19,
        SelectTribute = 20,
        SortChain = 21,
        SelectCounter = 22,
        SelectSum = 23,
        SelectDisfield = 24,
        SortCard = 25,
        SelectUnselectCard = 26,
        ConfirmDecktop = 30,
        ConfirmCards = 31,
        ShuffleDeck = 32,
        ShuffleHand = 33,
        RefreshDeck = 34,
        SwapGraveDeck = 35,
        ShuffleSetCard = 36,
        ReverseDeck = 37,
        DeckTop = 38,
        ShuffleExtra = 39,
        NewTurn = 40,
        NewPhase = 41,
        ConfirmExtratop = 42,
        Move = 50,
        PosChange = 53,
        Set = 54,
        Swap = 55,
        FieldDisabled = 56,
        Summoning = 60,
        Summoned = 61,
        SpSummoning = 62,
        SpSummoned = 63,
        FlipSummoning = 64,
        FlipSummoned = 65,
        Chaining = 70,
        Chained = 71,
        ChainSolving = 72,
        ChainSolved = 73,
        ChainEnd = 74,
        ChainNegated = 75,
        ChainDisabled = 76,
        CardSelected = 80,
        RandomSelected = 81,
        BecomeTarget = 83,
        Draw = 90,
        Damage = 91,
        Recover = 92,
        Equip = 93,
        LpUpdate = 94,
        Unequip = 95,
        CardTarget = 96,
        CancelTarget = 97,
        PayLpCost = 100,
        AddCounter = 101,
        RemoveCounter = 102,
        Attack = 110,
        Battle = 111,
        AttackDisabled = 112,
        DamageStepStart = 113,
        DamageStepEnd = 114,
        MissedEffect = 120,
        BeChainTarget = 121,
        CreateRelation = 122,
        ReleaseRelation = 123,
        TossCoin = 130,
        TossDice = 131,
        RockPaperScissors = 132,
        HandRes = 133,
        AnnounceRace = 140,
        AnnounceAttrib = 141,
        AnnounceCard = 142,
        AnnounceNumber = 143,
        CardHint = 160,
        TagSwap = 161,
        ReloadField = 162,
        AiName = 163,
        ShowHint = 164,
        PlayerHint = 165,
        MatchKill = 170,
        CustomMsg = 180,
        RemoveCards = 190,
    }
}
