// 로그라이크 진행 수치 (설계 문서 "로그라이크 진행", "보상 확률표 (초안)").
// 수치를 바꿀 때는 여기만 고친다. 정하지 않은 값은 임시 기본값이고 주석에 그렇게 적는다.
using System;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public static class RunRules
    {
        /// <summary>시작 LP이자 전부 회복했을 때의 LP. 상대는 늘 이 LP로 시작한다.</summary>
        public const int FullLp = 8000;

        /// <summary>
        /// 이 층의 배수(10, 20, 30…)는 보스전 층이고, 그 듀얼 직전에 LP를 전부 회복한다 (임시 기본값, 2026-11-09 사용자 결정).
        /// 회복은 LP가 FullLp보다 낮을 때만 FullLp로 올리고, 다이안 켓 등으로 넘긴 LP는 그대로 둔다.
        /// 보스 자체(상대 구성·보상)는 아직 없다.
        /// </summary>
        public const int BossFloorInterval = 10;

        /// <summary>기절 장수 = 연패 횟수 + 층 ÷ 이 값 (버림).</summary>
        public const int FaintFloorStep = 10;

        /// <summary>패배할 때마다 엑스트라 덱이 1장 기절할 확률(%). 성공하면 실패할 때까지 다시 굴린다.</summary>
        public const int ExtraFaintPercent = 20;

        /// <summary>메인 덱 최소 장수. 이보다 적어지면 사이드 덱에서 보충하고, 못 하면 런이 끝난다.</summary>
        public const int MinMainDeck = DeckRecipe.MinMainDeck;
        public const int MaxMainDeck = DeckRecipe.MaxMainDeck;
        public const int MaxExtraDeck = DeckRecipe.MaxExtraDeck;
        public const int MaxCopies = DeckRecipe.MaxCopies;

        /// <summary>런을 시작할 때 사이드 덱에 넣을 수 있는 장수 (재화로 늘리는 것은 아직 없다).</summary>
        public const int StartingSideDeckMax = 10;

        public static bool IsBossFloor(int floor) => floor % BossFloorInterval == 0;

        /// <summary>패배했을 때 메인 덱에서 기절할 장수. losingStreak는 이번 패배를 포함한 연패 횟수 (첫 패배면 1).</summary>
        public static int MainFaintCount(int losingStreak, int floor) => losingStreak + floor / FaintFloorStep;

        /// <summary>엑스트라 덱 기절 장수: 20%가 성공할 때마다 1장, 실패하거나 엑스트라 덱이 비면 멈춘다.</summary>
        public static int RollExtraFaintCount(RunRandom random, int available)
        {
            int count = 0;
            while (count < available && random.Next(100) < ExtraFaintPercent)
                count++;
            return count;
        }
    }

    /// <summary>승리 보상 등급 확률표 (설계 문서 "보상 확률표 (초안)"). 값은 % 정수이고, 열 순서는 Rarity와 같다.</summary>
    public static class RewardTable
    {
        static readonly int[][] Rows =
        {
            new[] { 70, 20, 9, 1, 0 },   // 1~9층
            new[] { 65, 22, 11, 2, 0 },  // 10~19층
            new[] { 60, 24, 13, 3, 0 },  // 20~29층
            new[] { 55, 26, 15, 4, 0 },  // 30~39층
            new[] { 50, 28, 17, 5, 0 },  // 40~49층 (50층 최종 보스도 보상이 정해질 때까지 임시로 이 줄)
            new[] { 46, 29, 18, 6, 1 },  // 51~59층
            new[] { 43, 30, 19, 7, 1 },  // 60~69층
            new[] { 40, 31, 20, 8, 1 },  // 70~79층
            new[] { 37, 32, 21, 9, 1 },  // 80~89층
            new[] { 34, 33, 22, 10, 1 }, // 90~99층, 101층 이후 무한 모드
        };

        static readonly int[] HiddenBoss = { 0, 0, 0, 0, 100 }; // 100층 히든 보스: FR 확정

        /// <summary>층의 등급별 확률(%) 복사본. 합은 100.</summary>
        public static int[] Percentages(int floor) => (int[])Row(floor).Clone();

        static int[] Row(int floor)
        {
            if (floor < 1)
                throw new ArgumentOutOfRangeException(nameof(floor), floor, "층은 1부터입니다.");
            if (floor < 50)
                return Rows[floor / 10];
            if (floor == 50)
                return Rows[4];
            if (floor < 100)
                return Rows[5 + (floor - 50) / 10];
            if (floor == 100)
                return HiddenBoss;
            return Rows[9];
        }

        /// <summary>층의 확률표로 등급 하나를 뽑는다.</summary>
        public static Rarity Roll(int floor, RunRandom random)
        {
            int[] row = Row(floor);
            int roll = random.Next(100);
            for (int i = 0; i < row.Length; i++)
            {
                if (roll < row[i])
                    return (Rarity)i;
                roll -= row[i];
            }
            throw new InvalidOperationException($"{floor}층 확률표의 합이 100이 아닙니다.");
        }
    }
}
