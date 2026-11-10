// 로그라이크 진행 수치 (규칙 결정 문서 2026-11-10: "탑 구조", "런 진행", "LP 규칙", "승리 보상", "봉인", "재화", "칩교환소").
// 수치를 바꿀 때는 여기만 고친다. 정하지 않은 값은 임시 기본값이고 주석에 "(임시)"라고 적는다.
using System;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public static class RunRules
    {
        /// <summary>런 시작 LP이자 전부 회복했을 때의 LP. 상대는 늘 이 LP로 시작한다.</summary>
        public const int FullLp = 8000;

        /// <summary>듀얼에서 이기면 남은 LP에 더하는 값 (보스 제외). 더한 LP는 FullLp를 넘을 수 있다.</summary>
        public const int WinLpBonus = 1000;

        /// <summary>
        /// 이 층의 배수(10, 20, 30…)는 보스 층이다 (탑 구조 10층 묶음의 끝, Tower). 보스를 이기면 LP를 FullLp로 맞춘다 (보너스 대신, 넘쳤어도 낮춘다).
        /// 보스 보상: 보통 승리 보상에 더해 봉인된 카드 1장을 골라 봉인 해제, 봉인된 카드가 없으면 보상 후보를 한 번 더.
        /// 보스 상대 덱은 아직 따로 없다 (일반 전투 층처럼 무작위).
        /// </summary>
        public const int BossFloorInterval = 10;

        /// <summary>
        /// 층이 확률로 이벤트 층이 될 확률(%). 칸은 10층 묶음 안의 자리(끝자리) 0~10, 0은 쓰지 않는다.
        /// 1~3층 10%, 4층 30%, 7~8층 5%. 5층 칩교환소, 6층 이벤트(반드시), 9층 휴식, 10층 보스는 정해져 있다.
        /// </summary>
        internal static readonly int[] RandomEventPercent = { 0, 10, 10, 10, 30, 0, 0, 5, 5, 0, 0 };

        // ---------------- 칩 (런 재화) ----------------
        // 획득량과 가격은 규칙 결정 문서에서 아직 정하지 않았다 ("미정"). 아래 값은 모두 임시 값이다.

        /// <summary>전투 층 듀얼에서 이기면 받는 칩 (임시). 지면 칩을 잃지 않는다.</summary>
        public const int ChipsPerWin = 20;

        /// <summary>보스를 이기면 받는 칩 (임시, ChipsPerWin 대신).</summary>
        public const int ChipsPerBossWin = 50;

        /// <summary>칩교환소 상품 가격 (임시). 순서는 ShopProduct와 같다.</summary>
        static readonly int[] ShopPrices =
        {
            30,  // 봉인 대상 교체: 같은 레어도, 새로 봉인할 카드는 무작위
            50,  // 봉인 대상 교체: 같은 레어도, 새로 봉인할 카드를 직접 고름
            50,  // 봉인 대상 교체: 레어도 상관없음, 무작위
            80,  // 봉인 대상 교체: 레어도 상관없음, 직접 고름
            150, // 봉인 해제 (매우 귀하게)
        };

        /// <summary>칩교환소에 봉인 해제가 나올 확률(%) (임시, 등장 빈도는 미정). 봉인 대상 교체는 늘 나온다.</summary>
        public const int UnsealOfferPercent = 50;

        public static int ShopPrice(ShopProduct product) => ShopPrices[(int)product];

        // ---------------- 런 종료 골드 정산 ----------------

        /// <summary>정산의 "런 동안 얻은 칩 ÷ 이 값" 항.</summary>
        public const int SettlementEarnedChipsDivisor = 100;

        /// <summary>정산의 "종료 때 가진 칩 ÷ 이 값" 항.</summary>
        public const int SettlementHeldChipsDivisor = 10;

        /// <summary>10층 단위 보너스: 10층을 클리어했으면 +1, 20층 +2, 30층 +3 …을 모두 더한다. floor는 런이 끝난 층.</summary>
        public static int SettlementFloorBonus(int floor)
        {
            int cleared = Math.Max(0, floor - 1) / BossFloorInterval;
            return cleared * (cleared + 1) / 2;
        }

        /// <summary>런 종료 골드 = 도달 층 + 10층 단위 보너스 + (얻은 칩 ÷ 100) + (가진 칩 ÷ 10). 나머지는 버린다.</summary>
        public static int SettlementGold(int floor, int chipsEarned, int chipsHeld) =>
            floor + SettlementFloorBonus(floor) + chipsEarned / SettlementEarnedChipsDivisor + chipsHeld / SettlementHeldChipsDivisor;

        /// <summary>봉인 장수 = 연패 횟수 + 층 ÷ 이 값 (버림).</summary>
        public const int SealFloorStep = 10;

        /// <summary>패배할 때마다 엑스트라 덱이 1장 봉인될 확률(%). 성공하면 실패할 때까지 다시 굴린다.</summary>
        public const int ExtraSealPercent = 20;

        /// <summary>메인 덱 최소 장수. 이보다 적어지면 사이드 덱에서 보충하고, 못 하면 런이 끝난다.</summary>
        public const int MinMainDeck = DeckRecipe.MinMainDeck;
        public const int MaxMainDeck = DeckRecipe.MaxMainDeck;
        public const int MaxExtraDeck = DeckRecipe.MaxExtraDeck;

        /// <summary>같은 카드명은 메인·엑스트라 덱에 이 장수까지 (카드 DB의 alias 기준). 사이드 덱에는 제한이 없다.</summary>
        public const int MaxCopies = DeckRecipe.MaxCopies;

        /// <summary>런을 시작할 때 사이드 덱에 넣을 수 있는 장수. 골드로 늘리는 것(영구 10장, 런마다 +5장)은 아직 없다.</summary>
        public const int StartingSideDeckMax = 3;

        /// <summary>승리 보상 후보 수. 상대 덱에서 뽑힌 등급의 서로 다른 카드를 이만큼 보여 주고, 모자란 자리는 골드로 채운다.</summary>
        public const int RewardChoices = 3;

        /// <summary>
        /// 카드 1장을 골드로 바꿀 때의 값 (등급 순서 N, R, SR, UR, FR). 임시 값: 환산표는 아직 정하지 않았다.
        /// 보상 후보의 빈 자리, 뽑힌 등급의 카드가 상대 덱에 없을 때, (나중에) 컬렉션 분해에 같은 표를 쓴다.
        /// </summary>
        static readonly int[] GoldByRarity = { 1, 2, 4, 8, 16 };

        public static bool IsBossFloor(int floor) => floor % BossFloorInterval == 0;

        /// <summary>패배했을 때 메인 덱에서 봉인할 장수. losingStreak는 이번 패배를 포함한 연패 횟수 (첫 패배면 1).</summary>
        public static int MainSealCount(int losingStreak, int floor) => losingStreak + floor / SealFloorStep;

        /// <summary>엑스트라 덱 봉인 장수: 20%가 성공할 때마다 1장, 실패하거나 엑스트라 덱이 비면 멈춘다.</summary>
        public static int RollExtraSealCount(RunRandom random, int available)
        {
            int count = 0;
            while (count < available && random.Next(100) < ExtraSealPercent)
                count++;
            return count;
        }

        /// <summary>이 등급의 카드 1장 값의 골드 (임시 환산표).</summary>
        public static int GoldFor(Rarity rarity) => GoldByRarity[(int)rarity];
    }

    /// <summary>승리 보상 등급 확률표 (규칙 결정 문서 "승리 보상"). 값은 % 정수이고, 열 순서는 Rarity와 같다.</summary>
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
