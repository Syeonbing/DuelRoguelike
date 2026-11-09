// 런에서 일어난 일 하나 (획득, 기절, LP 회복, 보충 등). 순서대로 RunState.Events에 쌓인다.
// 화면은 이것으로 층 사이 결과·로그를 보여 주고, 화면 밖 시험은 이것으로 규칙이 맞게 적용됐는지 다시 계산한다.
using System;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public enum RunEventKind
    {
        /// <summary>DeckId: 시작 덱, MainCount·ExtraCount·SideCount: 시작 장수</summary>
        RunStarted,
        /// <summary>DeckId: 상대 덱, Lp: 나의 시작 LP</summary>
        DuelStarted,
        /// <summary>Lp: 남은 LP (다음 듀얼로 이어 간다)</summary>
        DuelWon,
        /// <summary>Result: 패배 또는 무승부(패배로 처리), Count: 이번 패배를 포함한 연패 횟수</summary>
        DuelLost,
        /// <summary>Code, Rarity: 상대 덱에서 얻어 사이드 덱에 넣은 카드</summary>
        RewardGained,
        /// <summary>Rarity: 뽑힌 등급의 카드가 상대 덱에 없어 포인트로 대체</summary>
        RewardSubstituted,
        /// <summary>Code, Pile: 기절한 카드와 원래 있던 곳 (메인·엑스트라 덱)</summary>
        CardFainted,
        /// <summary>PreviousLp → Lp, LpReason</summary>
        LpRestored,
        /// <summary>Count: 메인 덱을 최소 장수로 채우려면 사이드 덱에서 보충해야 하는 장수</summary>
        RefillRequired,
        /// <summary>Code, Pile: 사이드 덱에서 옮겨 간 곳. IsRefill이면 보충</summary>
        MovedToDeck,
        /// <summary>Code, Pile: 사이드 덱으로 옮기기 전에 있던 곳</summary>
        MovedToSide,
        /// <summary>EndReason</summary>
        RunEnded,
    }

    /// <summary>카드가 있는 곳 (런 기준).</summary>
    public enum RunPile
    {
        Main,
        Extra,
        Side,
    }

    public enum LpRestoreReason
    {
        /// <summary>보스 층(10층마다) 듀얼 직전</summary>
        BossFloor,
        /// <summary>패배</summary>
        Loss,
    }

    public sealed class RunEvent
    {
        public RunEventKind Kind { get; internal set; }

        /// <summary>이 일이 속한 듀얼 번호 (1부터). 듀얼 사이의 일(보충·교환)은 마지막으로 끝난 듀얼, 런 시작은 0.</summary>
        public int DuelNumber { get; internal set; }

        /// <summary>이 일이 생긴 층 (승리 보상은 이긴 층).</summary>
        public int Floor { get; internal set; }

        public uint Code { get; internal set; }
        public Rarity Rarity { get; internal set; }
        public RunPile Pile { get; internal set; }
        public string DeckId { get; internal set; }
        public DuelResult Result { get; internal set; }
        public int Lp { get; internal set; }
        public int PreviousLp { get; internal set; }
        public int Count { get; internal set; }
        public int MainCount { get; internal set; }
        public int ExtraCount { get; internal set; }
        public int SideCount { get; internal set; }
        public LpRestoreReason LpReason { get; internal set; }
        public RunEndReason EndReason { get; internal set; }
        public bool IsRefill { get; internal set; }

        /// <summary>한국어 한 줄 설명. cardName은 카드 코드 → 표시할 카드명.</summary>
        public string Describe(Func<uint, string> cardName)
        {
            switch (Kind)
            {
                case RunEventKind.RunStarted:
                    return $"런 시작: 시작 덱 {DeckName(DeckId)} (메인 {MainCount}장, 엑스트라 {ExtraCount}장, 사이드 {SideCount}장)";
                case RunEventKind.DuelStarted:
                    return $"{Floor}층 듀얼 {DuelNumber}{(RunRules.IsBossFloor(Floor) ? " (보스 층)" : "")}: 상대 {DeckName(DeckId)}, 나의 LP {Lp}";
                case RunEventKind.DuelWon:
                    return $"승리 (남은 LP {Lp})";
                case RunEventKind.DuelLost:
                    return $"{(Result == DuelResult.Draw ? "무승부 (패배로 처리)" : "패배")}, {Count}연패";
                case RunEventKind.RewardGained:
                    return $"획득 [{Rarity}] {cardName(Code)} → 사이드 덱";
                case RunEventKind.RewardSubstituted:
                    return $"[{Rarity}] 뽑힘: 상대 덱에 {Rarity} 카드가 없어 포인트로 대체";
                case RunEventKind.CardFainted:
                    return $"기절 ({PileName(Pile)}): {cardName(Code)}";
                case RunEventKind.LpRestored:
                    return $"LP 회복 {PreviousLp} → {Lp} ({(LpReason == LpRestoreReason.BossFloor ? "보스 층 직전" : "패배")})";
                case RunEventKind.RefillRequired:
                    return $"메인 덱을 {RunRules.MinMainDeck}장으로 채우려면 사이드 덱에서 {Count}장 보충해야 함";
                case RunEventKind.MovedToDeck:
                    return $"{(IsRefill ? "보충" : "교환")}: {cardName(Code)} 사이드 덱 → {PileName(Pile)}";
                case RunEventKind.MovedToSide:
                    return $"교환: {cardName(Code)} {PileName(Pile)} → 사이드 덱";
                case RunEventKind.RunEnded:
                    return EndReason == RunEndReason.Abandoned ? "런 종료: 포기"
                        : $"런 종료: 메인 덱을 {RunRules.MinMainDeck}장으로 채울 수 없음";
                default:
                    return Kind.ToString();
            }
        }

        /// <summary>스타터 덱이면 표시 이름, 아니면 Id 그대로.</summary>
        static string DeckName(string id) => StarterDecks.Find(id ?? "")?.Name ?? id;

        public static string PileName(RunPile pile) =>
            pile == RunPile.Main ? "메인 덱" : pile == RunPile.Extra ? "엑스트라 덱" : "사이드 덱";

        /// <summary>듀얼 번호·층과 설명 (카드는 코드로). 같은 시드로 다시 돌린 런과 비교할 때도 쓴다.</summary>
        public override string ToString() => $"#{DuelNumber} {Floor}층 {Describe(code => code.ToString())}";
    }
}
