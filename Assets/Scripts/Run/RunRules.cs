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

        /// <summary>코인 토스에서 플레이어가 이길 확률 (퍼센트). 아이템·보스·이벤트가 바꿀 수 있다 (RunState.CoinTossWinPercent).</summary>
        public const int CoinTossWinPercent = 50;

        /// <summary>상대(AI)가 코인 토스에 이겼을 때 선공을 고를 확률 (퍼센트). 규칙 결정 문서: AI는 대부분 선공을 고른다.</summary>
        public const int AiChoosesFirstPercent = 90;

        /// <summary>이벤트 성향 가중치 (EventTendency 순서: 긍정 35 : 무성향 15 : 부정 50, 규칙 결정 문서). 그 성향에 나올 이벤트가 없으면 남은 성향에서 고른다.</summary>
        public static readonly int[] EventTendencyWeights = { 35, 15, 50 };

        /// <summary>조건이 걸린 선택지를 지금 고를 수 있는 이벤트는 가중치를 이만큼 곱한다 (임시 값).</summary>
        public const int EventConditionMultiplier = 2;

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
        // 2026-11-11 재화 세션에서 정함 (docs/ECONOMY.md): 층대가 오를 때 칩 획득과 칩교환소 가격이 같은 비율(층대마다 기본값의 +50%)로 오른다.
        // 그래서 어느 층대에서든 같은 판수로 같은 상품을 사고, 앞 층에서 모아 둔 칩은 뒤로 갈수록 값이 떨어진다.
        // 기본값과 비율은 패치로 바꿀 수 있게 여기에만 둔다. 칩교환소 가격은 진열할 때 저장 파일에 적히므로, 바꿔도 이미 진열된 칩교환소는 그대로다.

        /// <summary>층대 = (층 − 1) ÷ 10, 상한 없음. 1~10층 0, 11~20층 1, 21~30층 2 … (보스 층 10, 20 …은 그 묶음의 끝이라 앞 층대).</summary>
        public static int ChipBand(int floor) => Math.Max(0, floor - 1) / BossFloorInterval;

        /// <summary>전투 층 승리 칩 = WinChipsBase + WinChipsPerBand × 층대 (1~10층 20, 11~20층 30, 21~30층 40 …). 지면 칩을 잃지 않는다.</summary>
        public const int WinChipsBase = 20;
        public const int WinChipsPerBand = 10;

        /// <summary>보스 승리 칩 = 그 층의 승리 칩 × 이 값 ÷ 100 (250%: 10층 50, 20층 75, 30층 100 …). 승리 칩 대신 받는다.</summary>
        public const int BossChipsPercent = 250;

        /// <summary>이 층의 듀얼에서 이기면 받는 칩. boss는 보스 층(10층 단위) 승리.</summary>
        public static int ChipsForWin(int floor, bool boss)
        {
            int win = WinChipsBase + WinChipsPerBand * ChipBand(floor);
            return boss ? win * BossChipsPercent / 100 : win;
        }

        /// <summary>칩교환소 가격의 층대당 상승분 (기본가의 %). 승리 칩과 같은 비율이다 (WinChipsPerBand ÷ WinChipsBase = 50%).</summary>
        public const int ShopPricePercentPerBand = 50;

        /// <summary>칩교환소 상품의 1~10층 기본가 (칩). 순서는 ShopProduct와 같다. 아이템 카드는 카드마다 (ItemCard.Price).</summary>
        static readonly int[] ShopBasePrices =
        {
            30,  // 봉인 대상 교체: 같은 레어도, 새로 봉인할 카드는 무작위
            50,  // 봉인 대상 교체: 같은 레어도, 새로 봉인할 카드를 직접 고름
            50,  // 봉인 대상 교체: 레어도 상관없음, 무작위
            80,  // 봉인 대상 교체: 레어도 상관없음, 직접 고름
            150, // 봉인 해제 (매우 귀하게)
        };

        /// <summary>칩교환소에 봉인 해제가 나올 확률(%) (임시, 등장 빈도는 미정). 봉인 대상 교체는 늘 나온다.</summary>
        public const int UnsealOfferPercent = 50;

        /// <summary>봉인 상품의 1~10층 기본가.</summary>
        public static int ShopBasePrice(ShopProduct product) => ShopBasePrices[(int)product];

        /// <summary>기본가를 이 층의 가격으로: 기본가 × (100 + 50 × 층대) ÷ 100 (1~10층 ×1, 11~20층 ×1.5, 21~30층 ×2 …). 나머지는 버린다.</summary>
        public static int ScaledShopPrice(int basePrice, int floor) => basePrice * (100 + ShopPricePercentPerBand * ChipBand(floor)) / 100;

        /// <summary>이 층 칩교환소의 봉인 상품 가격. 아이템 카드는 ScaledShopPrice(ItemCard.Price, floor).</summary>
        public static int ShopPrice(ShopProduct product, int floor) => ScaledShopPrice(ShopBasePrice(product), floor);

        /// <summary>상점 덱: 전체 장수 한도 (같은 카드명은 MaxCopies장). 규칙 결정 문서.</summary>
        public const int MaxShopDeck = 10;

        /// <summary>탑 미리보기: 아이템 없이 바로 위 몇 층의 종류가 보이는지. 규칙 결정 문서: 1층.</summary>
        public const int BasePreviewFloors = 1;

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

        // ---------------- 골드 (영구 재화) 가격 ----------------
        // 2026-11-11 재화 세션에서 정함 ("느림" 가격대, docs/ECONOMY.md).
        // 골드 상점(GameSave)이 쓰는 것: 사이드 덱 영구 확장, 런 시작 때의 임시 확장, 컬렉션 분해. 팩·스트럭처 덱·코스트 완화는 가격만 있다.
        // 패치로 바꿀 수 있게 여기에만 둔다 (가격은 저장 파일에 적지 않는다).

        /// <summary>팩 1개 (5장 기준, 팩 구성은 미정).</summary>
        public const int PackPrice = 80;

        /// <summary>스트럭처 덱 1개.</summary>
        public const int StructureDeckPrice = 320;

        /// <summary>사이드 덱 시작 한도를 골드로 영구히 늘릴 수 있는 최대 장수.</summary>
        public const int PermanentSideDeckMax = 10;

        /// <summary>사이드 덱 영구 확장 가격 = 이 값 × n (n번째 확장, 1부터).</summary>
        public const int SideDeckExpansionStep = 50;

        /// <summary>
        /// 사이드 덱 시작 한도를 n번째로 늘리는 가격 (영구). n은 1(4장째) ~ 7(10장째): 50, 100 … 350, 모두 합 1400.
        /// </summary>
        public static int SideDeckExpansionPrice(int nth)
        {
            if (nth < 1 || nth > PermanentSideDeckMax - StartingSideDeckMax)
                throw new ArgumentOutOfRangeException(nameof(nth), nth, $"사이드 덱 영구 확장은 1~{PermanentSideDeckMax - StartingSideDeckMax}번째입니다.");
            return SideDeckExpansionStep * nth;
        }

        /// <summary>런을 시작할 때 골드로 사는 사이드 덱 임시 한도: 최대 장수와 1장 가격 (그 런에서만, 고정가).</summary>
        public const int RunSideDeckExtraMax = 5;
        public const int RunSideDeckSlotPrice = 8;

        /// <summary>런을 시작할 때 사이드 덱에 넣을 수 있는 가장 큰 장수 (영구 확장을 다 사고 임시 칸도 다 산 때).</summary>
        public const int StartingSideDeckCap = PermanentSideDeckMax + RunSideDeckExtraMax;

        /// <summary>런을 시작할 때 사이드 덱 임시 칸 slots장의 값.</summary>
        public static int RunSideDeckSlotsPrice(int slots)
        {
            if (slots < 0 || slots > RunSideDeckExtraMax)
                throw new ArgumentOutOfRangeException(nameof(slots), slots, $"사이드 덱 임시 칸은 0~{RunSideDeckExtraMax}장입니다.");
            return RunSideDeckSlotPrice * slots;
        }

        /// <summary>컬렉션 분해: 카드 ID마다 이 장수는 남긴다 (넘는 분만 분해한다).</summary>
        public const int DisassemblyKeep = 3;

        /// <summary>투입 코스트 제한 임시 완화 1점의 가격 (상한 없음).</summary>
        public const int CostReliefPricePerPoint = 8;

        /// <summary>
        /// 이번 런에서 이미 boughtThisRun점을 완화한 뒤 points점을 더 완화하는 가격. 지금은 고정가라 boughtThisRun을 쓰지 않는다.
        /// 한 런 안에서 누진으로 바꿀 때는 여기만 고친다 (예: n번째 1점 = CostReliefPricePerPoint × n).
        /// </summary>
        public static int CostReliefPrice(int boughtThisRun, int points)
        {
            if (boughtThisRun < 0 || points < 0)
                throw new ArgumentOutOfRangeException(nameof(points), points, "완화한 점수는 0 이상입니다.");
            return CostReliefPricePerPoint * points;
        }

        /// <summary>
        /// 봉인 장수 = 이 값 + 연패 횟수 + 층 ÷ SealFloorStep (버림). 패배마다 연패와 상관없이 더 봉인하는 장수.
        /// 2026-11-13 사용자 결정 (플레이 소감 "패배 패널티가 조금 더 늘었으면"): 0 → 1.
        /// </summary>
        public const int LossSealBase = 1;

        /// <summary>봉인 장수의 층 항: 층 ÷ 이 값 (버림).</summary>
        public const int SealFloorStep = 10;

        /// <summary>패배할 때마다 엑스트라 덱이 1장 봉인될 확률(%). 성공하면 실패할 때까지 다시 굴린다.</summary>
        public const int ExtraSealPercent = 20;

        /// <summary>메인 덱 최소 장수. 이보다 적어지면 사이드 덱에서 보충하고, 못 하면 런이 끝난다.</summary>
        public const int MinMainDeck = DeckRecipe.MinMainDeck;
        public const int MaxMainDeck = DeckRecipe.MaxMainDeck;
        public const int MaxExtraDeck = DeckRecipe.MaxExtraDeck;

        /// <summary>같은 카드명은 메인·엑스트라 덱에 이 장수까지 (카드 DB의 alias 기준). 사이드 덱에는 제한이 없다.</summary>
        public const int MaxCopies = DeckRecipe.MaxCopies;

        /// <summary>
        /// 런을 시작할 때 사이드 덱에 넣을 수 있는 장수의 처음 값. 골드로 영구히 늘린 한도는 GameSave.SideDeckLimit(최대 PermanentSideDeckMax)이고,
        /// 런마다 임시 칸을 RunSideDeckExtraMax장까지 더 산다 (RunSetup.SideDeckLimit = 영구 한도 + 산 임시 칸).
        /// </summary>
        public const int StartingSideDeckMax = 3;

        /// <summary>승리 보상 후보 수. 상대 덱에서 뽑힌 등급의 서로 다른 카드를 이만큼 보여 주고, 모자란 자리는 골드로 채운다.</summary>
        public const int RewardChoices = 3;

        /// <summary>
        /// 승리 보상에서 카드 대신 주는 골드 (등급 순서 N, R, SR, UR, FR): 보상 후보의 빈 자리, 뽑힌 등급의 카드가 상대 덱에 없을 때.
        /// 2026-11-11 정함 (docs/ECONOMY.md). 이 표는 저장 파일을 읽을 때 런 골드를 다시 계산하는 데도 쓰므로, 바꾸면 진행 중인 런의 골드도 바뀐다.
        /// </summary>
        static readonly int[] GoldByRarity = { 1, 2, 4, 8, 16 };

        /// <summary>
        /// 컬렉션 분해 1장의 골드 (등급 순서 N, R, SR, UR, FR). 카드 ID마다 DisassemblyKeep(3)장을 넘는 분만 분해한다 (GameSave.Disassemble).
        /// 2026-11-11 정함 (docs/ECONOMY.md): N~UR은 보상 표와 같고, FR만 특별히 높다 (팩 2개 값).
        /// </summary>
        static readonly int[] DisassemblyGoldByRarity = { 1, 2, 4, 8, 160 };

        public static bool IsBossFloor(int floor) => floor % BossFloorInterval == 0;

        /// <summary>
        /// 패배했을 때 메인 덱에서 봉인할 장수 = 1 + 연패 횟수 + 층 ÷ 10. losingStreak는 이번 패배를 포함한 연패 횟수 (첫 패배면 1).
        /// 예: 1~9층 첫 패배 2장, 2연패째 3장, 25층 3연패째 6장.
        /// </summary>
        public static int MainSealCount(int losingStreak, int floor) => LossSealBase + losingStreak + floor / SealFloorStep;

        /// <summary>엑스트라 덱 봉인 장수: 20%가 성공할 때마다 1장, 실패하거나 엑스트라 덱이 비면 멈춘다.</summary>
        public static int RollExtraSealCount(RunRandom random, int available)
        {
            int count = 0;
            while (count < available && random.Next(100) < ExtraSealPercent)
                count++;
            return count;
        }

        /// <summary>승리 보상에서 이 등급의 카드 1장 대신 주는 골드 (빈 자리·등급 대체).</summary>
        public static int GoldFor(Rarity rarity) => GoldByRarity[(int)rarity];

        /// <summary>이 등급의 카드 1장을 분해하면 받는 골드.</summary>
        public static int DisassemblyGoldFor(Rarity rarity) => DisassemblyGoldByRarity[(int)rarity];
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
