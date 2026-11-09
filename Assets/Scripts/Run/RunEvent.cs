// 런에서 일어난 일 하나 (보상, 봉인, LP 변화, 보충 등). 순서대로 RunState.Events에 쌓인다.
// 화면은 이것으로 층 사이 결과·로그를 보여 주고, 화면 밖 시험은 이것으로 규칙이 맞게 적용됐는지 다시 계산한다.
// 저장 파일에는 종류 이름으로 적는다 (순서를 바꿔도 되지만 이름은 바꾸지 않는다. 바꾸면 RunStateSave에서 옛 이름을 읽어 준다).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public enum RunEventKind
    {
        /// <summary>DeckId: 시작 덱, MainCount·ExtraCount·SideCount: 시작 장수</summary>
        RunStarted,
        /// <summary>DeckId: 상대 덱, Lp: 나의 시작 LP</summary>
        DuelStarted,
        /// <summary>Lp: 듀얼이 끝났을 때의 LP (보너스 전)</summary>
        DuelWon,
        /// <summary>Result: 패배 또는 무승부(패배로 처리), Count: 이번 패배를 포함한 연패 횟수</summary>
        DuelLost,
        /// <summary>듀얼 도중에 게임이 꺼져(저장 파일에 듀얼 중으로 남아) 그 듀얼을 패배로 처리함. 바로 뒤에 DuelLost가 온다.</summary>
        DuelForfeited,
        /// <summary>Rarity: 뽑힌 등급, Codes: 보상 후보 카드 (서로 다른 카드), Count: 빈 자리 하나를 고르면 받는 골드 (빈 자리가 없으면 0)</summary>
        RewardOffered,
        /// <summary>Code, Rarity: 보상 후보에서 고른 카드 (사이드 덱과 컬렉션으로)</summary>
        RewardGained,
        /// <summary>Rarity, Count: 보상 후보의 빈 자리를 골라 받은 골드</summary>
        RewardGold,
        /// <summary>Rarity, Count: 뽑힌 등급의 카드가 상대 덱에 없어 대신 받은 골드</summary>
        RewardSubstituted,
        /// <summary>Code, Pile: 봉인된 카드와 원래 있던 곳 (메인·엑스트라 덱)</summary>
        CardSealed,
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

    /// <summary>LP가 바뀐 까닭 (듀얼 밖에서).</summary>
    public enum LpRestoreReason
    {
        /// <summary>보스 층(10층마다) 듀얼 직전에 FullLp로 맞춤 (9층 휴식이 생길 때까지 임시)</summary>
        BossFloor,
        /// <summary>패배: FullLp로 회복 (FullLp보다 높으면 그대로)</summary>
        Loss,
        /// <summary>승리: 남은 LP + WinLpBonus</summary>
        Win,
        /// <summary>보스 승리: FullLp로 맞춤</summary>
        BossCleared,
    }

    public sealed class RunEvent
    {
        public RunEventKind Kind { get; internal set; }

        /// <summary>이 일이 속한 듀얼 번호 (1부터). 듀얼 사이의 일(보충·교환·보상 고르기)은 마지막으로 끝난 듀얼, 런 시작은 0.</summary>
        public int DuelNumber { get; internal set; }

        /// <summary>이 일이 생긴 층 (승리와 승리 보상은 이긴 층).</summary>
        public int Floor { get; internal set; }

        public uint Code { get; internal set; }
        public IReadOnlyList<uint> Codes { get; internal set; } = Array.Empty<uint>();
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
                case RunEventKind.DuelForfeited:
                    return "듀얼 도중에 게임이 꺼져 패배로 처리";
                case RunEventKind.RewardOffered:
                    return $"보상 후보 [{Rarity}] {string.Join(", ", Codes.Select(cardName))}" +
                           (Count > 0 ? $" · 빈 자리 {RunRules.RewardChoices - Codes.Count}곳 (골드 {Count})" : "");
                case RunEventKind.RewardGained:
                    return $"획득 [{Rarity}] {cardName(Code)} → 사이드 덱";
                case RunEventKind.RewardGold:
                    return $"보상으로 골드 {Count} (빈 자리를 고름)";
                case RunEventKind.RewardSubstituted:
                    return $"[{Rarity}] 뽑힘: 상대 덱에 {Rarity} 카드가 없어 골드 {Count}";
                case RunEventKind.CardSealed:
                    return $"봉인 ({PileName(Pile)}): {cardName(Code)}";
                case RunEventKind.LpRestored:
                    return $"LP {PreviousLp} → {Lp} ({LpReasonText(LpReason)})";
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

        static string LpReasonText(LpRestoreReason reason)
        {
            switch (reason)
            {
                case LpRestoreReason.BossFloor: return "보스 층 직전";
                case LpRestoreReason.Loss: return "패배, 회복";
                case LpRestoreReason.Win: return $"승리 +{RunRules.WinLpBonus}";
                case LpRestoreReason.BossCleared: return "보스 격파";
                default: return reason.ToString();
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
