// 엔진 없이 런 규칙만 시험한다: 듀얼 결과를 정해 두고 런을 진행시켜 기대값과 비교한다 (tools/headless-run.ps1 첫 단계).
// 확률표·연쇄 확률은 난수를 많이 뽑아 비율을 본다. 규칙은 규칙 결정 문서(2026-11-10) 기준.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;
using DuelRoguelike.Run;

namespace DuelRoguelike.Dev
{
    public sealed class RunRuleSelfTest
    {
        /// <summary>블랙홀: 스타터 덱 4개에 1장씩이라 첫 컬렉션에 4장.</summary>
        const uint DarkHole = 53129443;

        readonly CardDatabase _db;
        readonly RarityTable _rarities;
        readonly List<string> _failures = new List<string>();
        int _checks;

        RunRuleSelfTest(CardDatabase db, RarityTable rarities)
        {
            _db = db;
            _rarities = rarities;
        }

        /// <summary>모든 시험을 돌려 실패 목록을 돌려준다 (비어 있으면 통과). checks에는 확인한 항목 수.</summary>
        public static List<string> Run(CardDatabase db, RarityTable rarities, out int checks)
        {
            var test = new RunRuleSelfTest(db, rarities);
            foreach (var (name, body) in new (string, Action)[]
                     {
                         ("확률표", test.RewardTableRows),
                         ("등급 뽑기 분포", test.RewardRollDistribution),
                         ("엑스트라 덱 연쇄 봉인", test.ExtraSealChain),
                         ("탑 구조", test.TowerStructure),
                         ("봉인 장수·LP·층 진행", test.ScriptedRun),
                         ("칩교환소", test.ChipExchangeShop),
                         ("보스 보상과 골드 정산", test.BossBonusAndSettlement),
                         ("코인 토스와 선후공", test.CoinTossAndTurnOrder),
                         ("이벤트 고르기·성향", test.EventPicking),
                         ("이벤트 선택지와 결과", test.EventChoices),
                         ("이벤트 전투", test.EventBattle),
                         ("이벤트 도감", test.EventCodexSave),
                         ("아이템 카드 목록", test.ItemCardList),
                         ("상점 덱과 아이템 적용", test.ShopDeckAndItems),
                         ("귀속과 봉인", test.BoundItemRelease),
                         ("런 종료와 보충", test.EndAndRefill),
                         ("사이드 덱 교환 규칙", test.ExchangeRules),
                         ("승리 보상 고르기", test.RewardChoice),
                         ("보상 골드 대체와 등급 표 바꾸기", test.RewardSubstitutionAndTableChange),
                         ("같은 카드명", test.SameNameCopies),
                         ("같은 시드 재현", test.Determinism),
                         ("런 시작 확인", test.SetupChecks),
                         ("등급 파일 읽기", test.RarityParsing),
                         ("런 포기", test.AbandonRun),
                         ("저장 파일 쓰고 읽기", test.SaveRoundTrip),
                         ("듀얼 도중 종료", test.ForfeitOnLoad),
                         ("저장 파일 형식 오류", test.SaveFormatErrors),
                         ("옛 저장 파일 (형식 1)", test.SaveFormatV1),
                         ("옛 저장 파일 (형식 2)", test.SaveFormatV2),
                         ("덱 레시피", test.DeckRecipes),
                         ("저장 파일 위치·깨진 파일", test.SaveFiles),
                     })
            {
                try
                {
                    body();
                }
                catch (Exception e)
                {
                    test._failures.Add($"{name}: 예외 {e.GetType().Name}: {e.Message}");
                }
            }
            checks = test._checks;
            return test._failures;
        }

        void Expect(bool condition, string message)
        {
            _checks++;
            if (!condition)
                _failures.Add(message);
        }

        void ExpectThrows<T>(Action action, string message) where T : Exception
        {
            _checks++;
            try
            {
                action();
                _failures.Add($"{message}: 예외가 나지 않음");
            }
            catch (T)
            {
            }
        }

        // --- 확률표 ---

        void RewardTableRows()
        {
            var cases = new (int Floor, int[] Row)[]
            {
                (1, new[] { 70, 20, 9, 1, 0 }), (9, new[] { 70, 20, 9, 1, 0 }),
                (10, new[] { 65, 22, 11, 2, 0 }), (19, new[] { 65, 22, 11, 2, 0 }),
                (25, new[] { 60, 24, 13, 3, 0 }), (39, new[] { 55, 26, 15, 4, 0 }),
                (49, new[] { 50, 28, 17, 5, 0 }), (50, new[] { 50, 28, 17, 5, 0 }),
                (51, new[] { 46, 29, 18, 6, 1 }), (59, new[] { 46, 29, 18, 6, 1 }),
                (60, new[] { 43, 30, 19, 7, 1 }), (75, new[] { 40, 31, 20, 8, 1 }),
                (89, new[] { 37, 32, 21, 9, 1 }), (90, new[] { 34, 33, 22, 10, 1 }),
                (99, new[] { 34, 33, 22, 10, 1 }), (100, new[] { 0, 0, 0, 0, 100 }),
                (101, new[] { 34, 33, 22, 10, 1 }), (500, new[] { 34, 33, 22, 10, 1 }),
            };
            foreach (var (floor, row) in cases)
                Expect(RewardTable.Percentages(floor).SequenceEqual(row), $"{floor}층 확률표: {string.Join("/", RewardTable.Percentages(floor))}");
            for (int floor = 1; floor <= 300; floor++)
                if (RewardTable.Percentages(floor).Sum() != 100)
                    Expect(false, $"{floor}층 확률 합이 100이 아님");
            ExpectThrows<ArgumentOutOfRangeException>(() => RewardTable.Percentages(0), "0층 확률표");
        }

        void RewardRollDistribution()
        {
            const int rolls = 200000;
            foreach (int floor in new[] { 1, 55 })
            {
                var counts = new int[RarityTable.RarityCount];
                var random = new RunRandom(12345 + (ulong)floor);
                for (int i = 0; i < rolls; i++)
                    counts[(int)RewardTable.Roll(floor, random)]++;
                int[] row = RewardTable.Percentages(floor);
                for (int r = 0; r < row.Length; r++)
                {
                    double percent = counts[r] * 100.0 / rolls;
                    Expect(Math.Abs(percent - row[r]) < 0.5, $"{floor}층 {(Rarity)r} 비율 {percent:F2}% (표 {row[r]}%)");
                }
            }
            var hidden = new RunRandom(7);
            Expect(Enumerable.Range(0, 1000).All(_ => RewardTable.Roll(100, hidden) == Rarity.FR), "100층은 FR 확정");
        }

        void ExtraSealChain()
        {
            const int trials = 200000;
            var random = new RunRandom(99);
            int atLeast1 = 0, atLeast2 = 0, atLeast3 = 0, total = 0;
            for (int i = 0; i < trials; i++)
            {
                int n = RunRules.RollExtraSealCount(random, 15);
                total += n;
                if (n >= 1) atLeast1++;
                if (n >= 2) atLeast2++;
                if (n >= 3) atLeast3++;
            }
            Expect(Math.Abs(atLeast1 * 100.0 / trials - 20) < 0.5, $"엑스트라 1장 이상 {atLeast1 * 100.0 / trials:F2}% (20%)");
            Expect(Math.Abs(atLeast2 * 100.0 / trials - 4) < 0.25, $"엑스트라 2장 이상 {atLeast2 * 100.0 / trials:F2}% (4%)");
            Expect(Math.Abs(atLeast3 * 100.0 / trials - 0.8) < 0.1, $"엑스트라 3장 이상 {atLeast3 * 100.0 / trials:F2}% (0.8%)");
            Expect(Math.Abs(total / (double)trials - 0.25) < 0.01, $"엑스트라 평균 {total / (double)trials:F3}장 (0.25장)");
            Expect(Enumerable.Range(0, 10000).All(_ => RunRules.RollExtraSealCount(random, 1) <= 1), "엑스트라 덱보다 많이 봉인하지 않음");
            Expect(RunRules.RollExtraSealCount(random, 0) == 0, "엑스트라 덱이 비면 0장");
        }

        /// <summary>탑 구조: 끝자리로 정해지는 층, 확률 이벤트 층의 비율, 최소 전투 층, 칩교환소 진열 확률.</summary>
        void TowerStructure()
        {
            Expect(Tower.Position(1) == 1 && Tower.Position(10) == 10 && Tower.Position(11) == 1 && Tower.Position(105) == 5 && Tower.BlockStart(25) == 21,
                "10층 묶음 안의 자리");
            Expect(Tower.FixedKind(5) == FloorKind.ChipExchange && Tower.FixedKind(16) == FloorKind.Event && Tower.FixedKind(29) == FloorKind.Rest
                   && Tower.FixedKind(50) == FloorKind.Boss && Tower.FixedKind(101) == FloorKind.Battle && Tower.FixedKind(8) == FloorKind.Battle,
                "끝자리 5 칩교환소, 6 이벤트, 9 휴식, 0 보스, 나머지 전투");
            ExpectThrows<ArgumentOutOfRangeException>(() => Tower.Position(0), "0층");

            const int blocks = 40000;
            var events = new int[11];
            var perBlock = new int[4];
            int badFixed = 0, noBattle = 0, mismatch = 0;
            for (int b = 0; b < blocks; b++)
            {
                ulong seed = 5000 + (ulong)b;
                int start = 1 + (b % 12) * 10;
                var kinds = Tower.Block(seed, start + b % 10);
                int count = 0;
                for (int p = 1; p <= 10; p++)
                {
                    var kind = kinds[p - 1];
                    bool random = Tower.RandomEventPercent(start + p - 1) > 0;
                    if (random ? kind != FloorKind.Battle && kind != FloorKind.Event : kind != Tower.FixedKind(start + p - 1))
                        badFixed++;
                    if (random && kind == FloorKind.Event)
                    {
                        events[p]++;
                        count++;
                    }
                }
                perBlock[Math.Min(3, count)]++;
                if (kinds.Take(4).All(k => k != FloorKind.Battle) || kinds.Skip(6).Take(2).All(k => k != FloorKind.Battle))
                    noBattle++;
                if (Tower.KindOf(seed, start + b % 10) != kinds[b % 10] || Tower.KindOf(seed, start + 3) != Tower.Block(seed, start)[3])
                    mismatch++;
            }
            Expect(badFixed == 0, $"정해진 층(5·6·9·10)이 바뀌거나 1~4·7~8층이 전투·이벤트가 아님: {badFixed}번");
            Expect(noBattle == 0, $"1~4층 또는 7~8층에 전투 층이 없는 묶음 {noBattle}개");
            Expect(mismatch == 0, $"같은 시드·층인데 종류가 다름 {mismatch}번");
            foreach (int p in new[] { 1, 2, 3, 4, 7, 8 })
            {
                double percent = events[p] * 100.0 / blocks;
                int expected = Tower.RandomEventPercent(p);
                Expect(Math.Abs(percent - expected) < 0.7, $"{p}층 이벤트 비율 {percent:F2}% (규칙 {expected}%)");
            }
            // 규칙 결정 문서의 시뮬레이션: 10층 묶음당 확률 이벤트 0개 46%, 1개 40%, 2개 12%, 3개 이상 2%.
            double[] share = perBlock.Select(n => n * 100.0 / blocks).ToArray();
            Expect(Math.Abs(share[0] - 46) < 1.2 && Math.Abs(share[1] - 40) < 1.2 && Math.Abs(share[2] - 12) < 1 && Math.Abs(share[3] - 2) < 0.6,
                $"묶음당 확률 이벤트 개수 0/1/2/3+: {string.Join(" / ", share.Select(s => s.ToString("F1")))}%");
            Expect(PlainSeed >= 42 && Enumerable.Range(1, 30).All(f => Tower.KindOf(PlainSeed, f) == Tower.FixedKind(f)), $"시험 기본 시드 {PlainSeed}");

            // 칩교환소 진열: 봉인 대상 교체 4종은 늘, 봉인 해제는 50%(임시). 같은 시드·층이면 같다.
            int unseal = 0;
            var itemSeen = new Dictionary<uint, int>();
            for (int floor = 5; floor < 5 + 10 * 4000; floor += 10)
            {
                var stock = ChipExchange.Stock(77, floor);
                if (stock.Count < 4 || stock.Take(4).Any(o => !ChipExchange.IsSwap(o.Product) || o.Price != RunRules.ShopPrice(o.Product) || o.Sold))
                {
                    Expect(false, $"{floor}층 칩교환소 진열이 이상함");
                    break;
                }
                var itemOffers = stock.Where(o => o.Product == ShopProduct.ItemCard).ToList();
                if (itemOffers.Count != 1 || stock.Last() != itemOffers[0] || itemOffers[0].Price != OfferPrice(itemOffers[0]) || itemOffers[0].Sold)
                {
                    Expect(false, $"{floor}층 칩교환소 아이템 카드 진열이 이상함");
                    break;
                }
                itemSeen[itemOffers[0].Card] = itemSeen.TryGetValue(itemOffers[0].Card, out int seenCount) ? seenCount + 1 : 1;
                if (stock.Any(o => o.Product == ShopProduct.Unseal))
                    unseal++;
            }
            Expect(Math.Abs(unseal / 40.0 - RunRules.UnsealOfferPercent) < 2.5, $"봉인 해제 진열 {unseal / 40.0:F1}% (임시 {RunRules.UnsealOfferPercent}%)");
            Expect(ItemCards.All.All(i => itemSeen.TryGetValue(i.Code, out int n) && Math.Abs(n / 40.0 - 100.0 / ItemCards.All.Count) < 2.5), $"아이템 카드 진열은 목록에서 고르게: {string.Join(", ", itemSeen.Select(p => $"{p.Key} {p.Value / 40.0:F1}%"))}");
            Expect(ChipExchange.Stock(77, 15).Select(o => o.Product).SequenceEqual(ChipExchange.Stock(77, 15).Select(o => o.Product)), "같은 시드·층이면 같은 진열");
        }

        /// <summary>칩교환소: 진열, 가격, 칩 모자람, 봉인 대상 교체(같은 레어도·완전, 무작위·직접), 봉인 해제, 한 번만, 저장·읽기, 교체 뒤 보충.</summary>
        void ChipExchangeShop()
        {
            // 1층 2연패로 봉인 5장 (2 + 3), 그리고 4번 이겨 5층 칩교환소에 칩 80.
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = PlainSeed, StartDeck = StarterDecks.Yugi }, _db, _rarities);
            Play(run, DuelResult.Loss, 0);
            Play(run, DuelResult.Loss, 0);
            for (int i = 0; i < 4; i++)
                Play(run, DuelResult.Win, 8000);
            int chips = 4 * RunRules.ChipsPerWin;
            Expect(run.Floor == 5 && run.FloorKind == FloorKind.ChipExchange && run.Chips == chips && run.SealedCount == 5 && run.MainDeck.Count == 45,
                $"5층 칩교환소: {run.Floor}층 {run.FloorKind}, 칩 {run.Chips}, 봉인 {run.SealedCount}, 메인 {run.MainDeck.Count}");
            var products = run.Shop.Select(o => o.Product).ToList();
            Expect(products.Take(4).SequenceEqual(new[] { ShopProduct.SwapSameRarity, ShopProduct.SwapSameRarityChosen, ShopProduct.SwapAny, ShopProduct.SwapAnyChosen })
                   && run.Shop.All(o => o.Price == OfferPrice(o) && !o.Sold) && run.Shop.Last().Product == ShopProduct.ItemCard, "진열: 봉인 대상 교체 4종 (+ 봉인 해제는 확률) + 아이템 카드 1장, 임시 가격");
            Expect(RunRules.ShopPrice(ShopProduct.SwapSameRarity) < RunRules.ShopPrice(ShopProduct.SwapSameRarityChosen)
                   && RunRules.ShopPrice(ShopProduct.SwapAny) < RunRules.ShopPrice(ShopProduct.SwapAnyChosen)
                   && RunRules.ShopPrice(ShopProduct.SwapAnyChosen) < RunRules.ShopPrice(ShopProduct.Unseal), "직접 고르는 교체가 더 비싸고, 봉인 해제가 가장 비쌈");
            if (products.Contains(ShopProduct.Unseal))
                Expect(!run.CanBuy(ShopProduct.Unseal, out string poor) && poor.Contains("칩이 모자람"), "칩이 모자라면 봉인 해제를 못 삼");

            Rarity? RarityOf(uint code) => _rarities.TryGet(code, out var r) ? r : (Rarity?)null;

            // 같은 레어도, 무작위 (30): 고른 봉인 카드는 사이드 덱으로, 같은 레어도의 메인 덱 카드 하나가 대신 봉인.
            Expect(run.ReleaseCandidates(ShopProduct.SwapSameRarity).Count > 0, "같은 레어도 교체로 풀 수 있는 카드가 있음");
            uint release = run.ReleaseCandidates(ShopProduct.SwapSameRarity)[0];
            int side = run.SideDeck.Count;
            int mark = run.Events.Count;
            ExpectThrows<ArgumentException>(() => run.BuySwap(ShopProduct.SwapSameRarity, release, run.MainDeck[0]), "무작위 교체에 새 봉인 카드를 줌");
            run.BuySwap(ShopProduct.SwapSameRarity, release);
            chips -= RunRules.ShopPrice(ShopProduct.SwapSameRarity);
            var sealedNow = run.Events.Skip(mark).Where(e => e.Kind == RunEventKind.CardSealed).ToList();
            Expect(run.Chips == chips && run.SealedCount == 5 && run.SideDeck.Count == side + 1 && run.SideDeck.Last() == release && run.MainDeck.Count == 44
                   && sealedNow.Count == 1 && RarityOf(sealedNow[0].Code) == RarityOf(release) && run.SealedMain.Contains(sealedNow[0].Code),
                $"같은 레어도 교체: 칩 {run.Chips}, 봉인 {run.SealedCount}, 사이드 {run.SideDeck.Count}, 메인 {run.MainDeck.Count}");
            var bought = run.Events.Skip(mark).ToList();
            Expect(bought.Count == 3 && bought[0].Kind == RunEventKind.ShopBought && bought[0].Product == ShopProduct.SwapSameRarity && bought[0].Count == 30 && bought[0].Chips == chips
                   && bought[1].Kind == RunEventKind.CardUnsealed && bought[1].Code == release && !bought[1].IsBonus && bought[2].Kind == RunEventKind.CardSealed,
                "교체 이벤트: 구매 → 봉인 해제 → 봉인");
            Expect(!run.CanBuy(ShopProduct.SwapSameRarity, out string again) && again.Contains("이미") && run.Shop[0].Sold, $"같은 상품은 한 번만 ({again})");
            Expect(!run.CanBuy(ShopProduct.SwapAnyChosen, out string costly) && costly.Contains("칩이 모자람"), $"칩 {run.Chips}로 완전 교체(직접)는 못 삼");

            // 같은 레어도, 직접 고름 (50): 새로 봉인할 카드를 고른다.
            uint release2 = run.ReleaseCandidates(ShopProduct.SwapSameRarityChosen)[0];
            var targets = run.NewSealCandidates(ShopProduct.SwapSameRarityChosen, release2);
            Expect(targets.Count > 0 && targets.All(c => RarityOf(c) == RarityOf(release2) && run.MainDeck.Contains(c)), "직접 고를 후보: 같은 레어도의 메인 덱 카드");
            ExpectThrows<InvalidOperationException>(() => run.BuySwap(ShopProduct.SwapSameRarityChosen, release2), "직접 고르는 교체에 새 봉인 카드 없음");
            Expect(run.Chips == chips, "실패한 구매는 칩을 쓰지 않음");
            uint target = targets.Last();
            run.BuySwap(ShopProduct.SwapSameRarityChosen, release2, target);
            chips -= RunRules.ShopPrice(ShopProduct.SwapSameRarityChosen);
            Expect(run.Chips == chips && chips == 0 && run.SealedMain.Contains(target) && run.SideDeck.Contains(release2) && run.MainDeck.Count == 43,
                $"직접 고른 교체: 칩 {run.Chips}, {target} 봉인");
            Expect(!run.CanBuy(ShopProduct.SwapAny, out string none) && none.Contains("칩이 모자람"), "칩 0");

            // 저장·읽기: 진열과 산 것이 그대로. 떠나면 진열이 사라진다.
            var loaded = GameSave.Parse(save.Write(), _db, _rarities);
            Expect(loaded.Write() == save.Write() && loaded.Run.Shop.Count(o => o.Sold) == 2 && loaded.Run.FloorKind == FloorKind.ChipExchange, "칩교환소 저장·읽기");
            run.LeaveFloor();
            Expect(run.Floor == 6 && run.Shop.Count == 0 && !run.CanBuy(ShopProduct.SwapAny, out string gone) && gone.Contains("칩교환소가 아님"), "칩교환소를 떠나면 살 수 없음");

            // 40장 덱: 교체하면 메인 덱이 39장이 되어 보충한다 (풀린 카드가 사이드 덱에 있어 채울 수 있다).
            var forty = new DeckRecipe("Test40", "시험 덱 40장", StarterDecks.Yugi.Entries.Take(40).ToList());
            var spare = StarterDecks.Yugi.Entries.Skip(40).Take(2).Select(e => e.Code).ToList();
            var thin = NewRun(forty, side: spare);
            Play(thin, DuelResult.Loss, 0);
            Play(thin, DuelResult.Win, 8000); // 보충(사이드 덱 2장) 뒤 1층 승리
            while (thin.Floor < 5)
                Play(thin, DuelResult.Win, 8000);
            Expect(thin.Floor == 5 && thin.MainDeck.Count == 40 && thin.SealedCount == 2 && thin.Chips == 80,
                $"40장 덱 5층: 메인 {thin.MainDeck.Count}, 사이드 {thin.SideDeck.Count}, 봉인 {thin.SealedCount}, 칩 {thin.Chips}");
            uint x = thin.SealedCards()[0];
            Expect(thin.CanBuy(ShopProduct.SwapAny, out string why) && thin.ReleaseCandidates(ShopProduct.SwapAny).SequenceEqual(thin.SealedCards()), $"40장 덱도 교체 가능 ({why})");
            thin.BuySwap(ShopProduct.SwapAny, x);
            Expect(thin.Phase == RunPhase.NeedsRefill && thin.RefillNeeded == 1 && thin.RefillCandidates().Contains(x), $"교체 뒤 보충: {thin.Phase}, {thin.RefillNeeded}장");
            Expect(!thin.CanLeaveFloor(out _) && !thin.CanBuy(ShopProduct.SwapAnyChosen, out _), "보충 중에는 떠나거나 살 수 없음");
            thin.MoveToDeck(x);
            Expect(thin.Phase == RunPhase.Ready && thin.MainDeck.Count == 40 && thin.CanLeaveFloor(out _), "보충하면 다시 칩교환소");

            // 봉인 해제 (150): 칩을 모아 진열된 칩교환소에서 산다.
            var rich = NewRun(StarterDecks.Yugi);
            Play(rich, DuelResult.Loss, 0);
            Play(rich, DuelResult.Loss, 0); // 봉인 5장: 10층 보스 보상으로 1장 풀려도 남는다
            while (rich.Phase != RunPhase.Ended && rich.Floor < 120 && !(rich.FloorKind == FloorKind.ChipExchange && rich.Shop.Any(o => o.Product == ShopProduct.Unseal) && rich.Chips >= 150))
                Play(rich, DuelResult.Win, 8000);
            bool reached = rich.FloorKind == FloorKind.ChipExchange && rich.CanBuy(ShopProduct.Unseal, out _);
            Expect(reached, $"봉인 해제를 살 칩교환소에 닿음 ({rich.Floor}층)");
            if (!reached)
                return;
            uint u = rich.SealedCards()[0];
            int unsealSide = rich.SideDeck.Count, unsealSealed = rich.SealedCount, unsealChips = rich.Chips;
            rich.BuyUnseal(u);
            Expect(rich.SideDeck.Count == unsealSide + 1 && rich.SealedCount == unsealSealed - 1 && rich.Chips == unsealChips - RunRules.ShopPrice(ShopProduct.Unseal)
                   && rich.Events.Last().Kind == RunEventKind.CardUnsealed && rich.Events.Last().Code == u, "봉인 해제: 사이드 덱으로, 칩 -150");
            ExpectThrows<InvalidOperationException>(() => rich.BuyUnseal(rich.SealedCards().FirstOrDefault()), "봉인 해제는 한 번만");
        }

        /// <summary>
        /// 코인 토스: 같은 런·듀얼이면 결과가 같고, 이긴 쪽이 선후공을 고르며 (상대는 대부분 선공), 선공이 엔진의 팀 0이다.
        /// 듀얼 시작 기록에 남고 저장 파일을 오간다. 형식 4(기록 없음) 파일도 읽는다.
        /// </summary>
        void CoinTossAndTurnOrder()
        {
            const int Seeds = 300;
            int won = 0, lost = 0, opponentFirst = 0;
            bool stable = true;
            (GameSave Save, RunState Run) playerWins = default, opponentWins = default;
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var save = GameSave.NewGame();
                var run = save.StartRun(new RunSetup { Seed = seed, StartDeck = StarterDecks.Yugi }, _db, _rarities);
                PassFloors(run);
                var toss = run.NextCoinToss();
                var again = run.NextCoinToss();
                stable &= again.PlayerWon == toss.PlayerWon && again.OpponentChoosesFirst == toss.OpponentChoosesFirst;
                if (toss.PlayerWon)
                {
                    won++;
                    if (playerWins.Run == null) playerWins = (save, run);
                }
                else
                {
                    lost++;
                    if (toss.OpponentChoosesFirst)
                    {
                        opponentFirst++;
                        if (opponentWins.Run == null) opponentWins = (save, run);
                    }
                }
            }
            Expect(stable, "같은 런·듀얼이면 코인 토스 결과가 같다");
            Expect(won > Seeds * 4 / 10 && won < Seeds * 6 / 10, $"코인 토스 승률 {won}/{Seeds} (기대 약 50%)");
            Expect(opponentFirst > lost * 8 / 10 && opponentFirst < lost, $"상대가 이겼을 때 선공을 고른 비율 {opponentFirst}/{lost} (기대 약 90%)");

            // 이기면 고른 대로: 후공을 고르면 나는 엔진의 팀 1 (덱·LP 자리가 바뀐다).
            var duel = playerWins.Run.BeginDuel(playerFirst: false);
            var options = duel.CreateSessionOptions();
            Expect(duel.Toss.PlayerWon && !duel.PlayerGoesFirst && duel.PlayerTeam == 1, "코인 토스에 이기고 후공을 고름");
            Expect(options.Decks[1].SequenceEqual(duel.PlayerDeck) && options.Decks[0].SequenceEqual(duel.OpponentCards)
                   && options.Team2.StartingLP == (uint)duel.PlayerLp && options.Team1.StartingLP == RunRules.FullLp,
                "후공이면 엔진의 팀 0이 상대, 팀 1이 나");
            var started = playerWins.Run.Events.Last(e => e.Kind == RunEventKind.DuelStarted);
            Expect(started.Coin == CoinTossWinner.Player && started.Turn == TurnOrder.Second, "듀얼 시작 기록: 코인 토스 승리, 후공");
            Expect(started.Describe(c => c.ToString()).EndsWith("코인 토스 승리 → 나의 후공"), "듀얼 시작 기록 문장");

            // 후공 듀얼 중에 꺼지면 다시 읽을 때 패배. 기록은 그대로.
            string text = playerWins.Save.Write();
            Expect(text.Contains(" coin=Player turn=Second"), "저장 파일에 코인 토스·선후공");
            var loaded = GameSave.Parse(text, _db, _rarities, "시험");
            Expect(loaded.ForfeitedDuel == 1 && loaded.Run.Events.Any(e => e.Kind == RunEventKind.DuelStarted && e.Turn == TurnOrder.Second),
                "후공 듀얼 중에 꺼짐 → 패배 처리, 선후공 기록은 남음");
            string v4 = Regex.Replace(text.Replace("format 5\n", "format 4\n"), @" coin=\w+ turn=\w+", "");
            var old = GameSave.Parse(v4, _db, _rarities, "시험");
            Expect(old.ForfeitedDuel == 1 && old.Run.Events.Where(e => e.Kind == RunEventKind.DuelStarted).All(e => e.Turn == TurnOrder.Unknown),
                "형식 4 파일 (코인 토스 기록 없음)도 읽음");

            // 상대가 이기면 플레이어가 무엇을 골랐든 상대가 정한다.
            var lostDuel = opponentWins.Run.BeginDuel(playerFirst: true);
            Expect(!lostDuel.Toss.PlayerWon && !lostDuel.PlayerGoesFirst && lostDuel.PlayerTeam == 1, "상대가 이겨 선공을 고르면 나는 후공");
            ExpectThrows<InvalidOperationException>(() => opponentWins.Run.NextCoinToss(), "듀얼 중에 코인 토스");
        }

        /// <summary>보스 보상(봉인된 카드가 없으면 보상 1장 더, 있으면 봉인 해제 고르기)과 런 종료 골드 정산.</summary>
        void BossBonusAndSettlement()
        {
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = PlainSeed, StartDeck = StarterDecks.Yugi }, _db, _rarities);
            while (run.Floor < 9)
                Play(run, DuelResult.Win, 8000);
            PassFloors(run);
            Expect(run.SealedCount == 0 && run.FloorKind == FloorKind.Boss && run.Chips == 6 * RunRules.ChipsPerWin, $"10층: 봉인 {run.SealedCount}, {run.FloorKind}, 칩 {run.Chips}");
            int mark = run.Events.Count;
            Begin(run);
            run.FinishDuel(DuelResult.Win, 3000);
            if (run.Phase == RunPhase.ChoosingReward && !run.PendingReward.IsBossBonus)
            {
                Expect(save.Write().Contains("\nboss-bonus\n") && RoundTrip(save) == save.Write(), "보스 승리 보상을 고르는 중: boss-bonus 저장·읽기");
                run.ChooseReward(0);
            }
            var bonus = run.Events.Skip(mark).Where(e => (e.Kind == RunEventKind.RewardOffered || e.Kind == RunEventKind.RewardSubstituted) && e.IsBonus).ToList();
            Expect(bonus.Count == 1 && bonus[0].Floor == 10, "봉인된 카드가 없으면 보스 보상으로 보상 1장 더");
            if (run.Phase == RunPhase.ChoosingReward)
            {
                string text = save.Write();
                Expect(run.PendingReward.IsBossBonus && text.Contains("\nreward-bonus\n") && !text.Contains("\nboss-bonus\n") && RoundTrip(save) == text,
                    "보스 보상 1장 더: reward-bonus 저장·읽기");
                Expect(run.Events.Last().Describe(c => "").StartsWith("보스 보상 1장 더"), "보스 보상 후보 로그");
                run.ChooseReward(0);
            }
            Expect(run.Floor == 11 && run.Phase == RunPhase.Ready && run.Chips == 6 * RunRules.ChipsPerWin + RunRules.ChipsPerBossWin
                   && !run.Events.Any(e => e.Kind == RunEventKind.UnsealOffered), $"보스 보상 뒤 11층: {run.Floor}층, LP {run.Lp}, 칩 {run.Chips}");

            // 봉인된 카드가 있으면 봉인 해제 고르기 (ChoosingUnseal): 저장·읽기, 고르기 전에는 듀얼·교환 불가.
            Play(run, DuelResult.Loss, 0);
            while (run.Floor < 19)
                Play(run, DuelResult.Win, 8000);
            PassFloors(run);
            Begin(run);
            run.FinishDuel(DuelResult.Win, 8000);
            if (run.Phase == RunPhase.ChoosingReward)
                run.ChooseReward(0);
            Expect(run.Phase == RunPhase.ChoosingUnseal && run.Floor == 20 && save.Write().Contains("\nphase ChoosingUnseal\n"), $"20층 보스: {run.Phase}");
            var loaded = GameSave.Parse(save.Write(), _db, _rarities);
            Expect(loaded.Write() == save.Write() && loaded.Run.Phase == RunPhase.ChoosingUnseal, "봉인 해제 고르는 중 저장·읽기");
            ExpectThrows<InvalidOperationException>(() => loaded.Run.BeginDuel(), "봉인 해제를 고르기 전에 듀얼");
            Expect(!loaded.Run.CanMoveToSide(loaded.Run.MainDeck[0], out string busy) && busy.Contains("봉인 해제"), $"봉인 해제를 고르기 전에 교환 ({busy})");
            uint code = loaded.Run.SealedCards().Last();
            loaded.Run.ChooseUnseal(code);
            Expect(loaded.Run.Floor == 21 && loaded.Run.SideDeck.Last() == code && loaded.Run.Phase == RunPhase.Ready, "읽은 런에서 봉인 해제 → 21층");

            // 정산 공식 (규칙 결정 문서의 예: 13층 14, 31층 37, 51층 66, 101층 156)
            Expect(RunRules.SettlementGold(13, 0, 0) == 14 && RunRules.SettlementGold(31, 0, 0) == 37 && RunRules.SettlementGold(51, 0, 0) == 66
                   && RunRules.SettlementGold(101, 0, 0) == 156 && RunRules.SettlementGold(1, 0, 0) == 1 && RunRules.SettlementGold(10, 0, 0) == 10
                   && RunRules.SettlementGold(11, 0, 0) == 12, "정산: 도달 층 + 10층 단위 보너스");
            Expect(RunRules.SettlementGold(5, 250, 35) == 5 + 2 + 3 && RunRules.SettlementGold(5, 99, 9) == 5, "정산: 얻은 칩÷100 + 가진 칩÷10 (버림)");

            // 봉인 해제를 고르는 중에 포기: 런 종료, 정산 이벤트, 누적 = 런 골드 + 정산.
            int before = save.Gold;
            save.AbandonRun();
            var settled = run.Events.Last();
            Expect(run.Phase == RunPhase.Ended && settled.Kind == RunEventKind.GoldSettled && settled.Count == run.SettlementGold && settled.Chips == run.Chips
                   && settled.ChipsEarned == run.ChipsEarned && run.SettlementGold == RunRules.SettlementGold(20, run.ChipsEarned, run.Chips),
                $"정산 이벤트: {settled.Describe(c => "")}");
            Expect(save.Gold == before + run.Gold + run.SettlementGold, $"누적 골드 {save.Gold} = {before} + 런 골드 {run.Gold} + 정산 {run.SettlementGold}");
        }

        // --- 정해 둔 결과로 런 진행 ---

        /// <summary>1~30층에 확률 이벤트 층이 없는 시드 (42부터 찾음). 층 번호를 정해 두고 보는 시험의 기본 시드.</summary>
        static readonly ulong PlainSeed = FindPlainSeed(42);

        static ulong FindPlainSeed(ulong from)
        {
            for (ulong seed = from; ; seed++)
                if (Enumerable.Range(1, 30).All(floor => Tower.KindOf(seed, floor) == Tower.FixedKind(floor)))
                    return seed;
        }

        RunState NewRun(DeckRecipe deck, ulong? seed = null, IReadOnlyList<uint> side = null, RarityTable rarities = null,
            CardCollection collection = null) =>
            RunState.Start(new RunSetup { Seed = seed ?? PlainSeed, StartDeck = deck, SideDeck = side ?? Array.Empty<uint>() },
                _db, rarities ?? _rarities, collection ?? CardCollection.FromRecipes(StarterDecks.All));

        // ---------------- 이벤트 ----------------

        /// <summary>듀얼은 모두 LP 8000으로 이기고 비전투 층은 떠나며, 아직 고르지 않은 이벤트 층까지 간다.</summary>
        RunState ToEventFloor(RunState run)
        {
            for (int guard = 0; guard < 60 && !(run.Phase == RunPhase.Ready && run.FloorKind == FloorKind.Event && !run.EventDone && !run.EventBattlePending); guard++)
            {
                if (run.Phase != RunPhase.Ready)
                    Choose(run);
                else if (Tower.IsBattle(run.FloorKind) || run.EventBattlePending)
                {
                    run.BeginDuel();
                    run.FinishDuel(DuelResult.Win, 8000);
                    Choose(run);
                }
                else if (run.FloorKind == FloorKind.Event && !run.EventDone)
                    run.ChooseEventOption(PassChoice(run));
                else
                    run.LeaveFloor();
            }
            Expect(run.FloorKind == FloorKind.Event && !run.EventDone && !run.EventBattlePending, $"이벤트 층까지 가기: {run.Floor}층 {run.FloorKind}");
            return run;
        }

        /// <summary>첫 이벤트 층의 성향 분포 (긍정 35 : 무성향 15 : 부정 50, 1~4층·6층 모두 세 성향이 있다), 덱 전용·한 번만 이벤트.</summary>
        void EventPicking()
        {
            var counts = new int[3];
            const int Runs = 400;
            for (int i = 0; i < Runs; i++)
            {
                var run = ToEventFloor(NewRun(StarterDecks.All[i % 4], seed: (ulong)(5000 + i)));
                counts[(int)run.CurrentEvent.Tendency]++;
                Expect(run.CurrentEvent.DeckId == null || run.CurrentEvent.DeckId == run.StartDeckId, $"덱 전용 이벤트 {run.CurrentEventId}가 {run.StartDeckId} 런에 나옴");
                Expect((run.CurrentEvent.Slots & RunState.SlotOf(run.Floor)) != 0, $"{run.Floor}층에 자리가 맞지 않는 이벤트 {run.CurrentEventId}");
            }
            double Share(int t) => 100.0 * counts[t] / Runs;
            Expect(Math.Abs(Share(0) - 35) < 7 && Math.Abs(Share(1) - 15) < 6 && Math.Abs(Share(2) - 50) < 7,
                $"성향 분포 긍정 {Share(0):0.#}% · 무성향 {Share(1):0.#}% · 부정 {Share(2):0.#}% (기대 35·15·50)");

            // 한 번만 나오는 이벤트는 같은 런에 다시 나오지 않는다: 카이바 런 여러 개를 40층까지 (백룡의 전설·은퇴한 듀얼리스트는 6층마다 후보).
            for (ulong seed = 80; seed < 86; seed++)
            {
                var kaiba = NewRun(StarterDecks.Kaiba, seed: seed);
                var seen = new List<string>();
                while (kaiba.Floor < 40 && kaiba.Phase != RunPhase.Ended)
                {
                    ToEventFloor(kaiba);
                    seen.Add(kaiba.CurrentEventId);
                    kaiba.ChooseEventOption(PassChoice(kaiba));
                }
                var once = seen.Where(id => !TowerEventList.Find(id).Repeatable).ToList();
                Expect(once.Count == once.Distinct().Count(), $"시드 {seed}: 한 번만 나오는 이벤트가 다시 나옴 ({string.Join(", ", seen)})");
            }
        }

        /// <summary>선택지 조건·잠김·강제·지나가기, 결과(칩·LP·골드·봉인·봉인 해제·잃기), 결과 뒤 층 진행과 보충.</summary>
        void EventChoices()
        {
            var run = ToEventFloor(NewRun(StarterDecks.Yugi, seed: 41));
            int floor = run.Floor, chips = run.Chips, lp = run.Lp;

            // 사건: 「확인」만, 그냥 지나가기는 안 됨. 칩 +40 뒤 다음 층.
            run.ReplaceEventForTest("lucky-coin");
            Expect(!run.CanChooseEventOption(-1, out _) && run.CanChooseEventOption(0, out _), "사건은 확인만 고를 수 있음");
            int mark = run.Events.Count;
            run.ChooseEventOption(0);
            Expect(run.Chips == chips + 40 && run.ChipsEarned >= 40 && run.Floor == floor + 1 && run.Lp == lp
                   && HasNew(run, mark, e => e.Kind == RunEventKind.EventResolved && e.EventId == "lucky-coin" && e.Count == 0)
                   && HasNew(run, mark, e => e.Kind == RunEventKind.ChipsGained && e.Count == 40),
                $"반짝이는 동전: 칩 {run.Chips} (기대 {chips + 40}), {run.Floor}층");
            ExpectThrows<InvalidOperationException>(() => run.ChooseEventOption(0), "다음 층이 이벤트 층이 아니거나 이미 고른 뒤에는 못 고름");

            // 소매치기: 가진 칩보다 많이 잃지 않는다. 낙석: LP는 1 아래로 내려가지 않는다.
            run = ToEventFloor(NewRun(StarterDecks.Yugi, seed: 41));
            run.ReplaceEventForTest("pickpocket");
            chips = run.Chips;
            mark = run.Events.Count;
            run.ChooseEventOption(0);
            Expect(run.Chips == Math.Max(0, chips - 50) && (chips == 0 || HasNew(run, mark, e => e.Kind == RunEventKind.ChipsLost && e.Count == Math.Min(50, chips))),
                $"소매치기: 칩 {chips} → {run.Chips}");

            // 통행세: 칩 60 미만이면 잠김. 함정 6장도 없으면 강제 선택지(봉인 2장)만, 그냥 지나가기는 안 됨.
            run = ToEventFloor(NewRun(StarterDecks.Yugi, seed: 41));
            run.ReplaceEventForTest("toll-gate");
            var toll = run.CurrentEvent;
            bool traps = run.Satisfies(toll.Choices[1], toll.Scope);
            Expect(run.CanChooseEventOption(0, out string reason) == (run.Chips >= 60) && (run.Chips >= 60 || reason.Contains("칩 60 이상")),
                $"통행세 칩 조건 (칩 {run.Chips}): {reason}");
            Expect(run.CanChooseEventOption(2, out _) == run.AllEventChoicesLocked && !run.CanChooseEventOption(-1, out _),
                "강제 선택지는 모두 잠겼을 때만, 강제 선택지가 있으면 그냥 지나가기 없음");
            if (run.AllEventChoicesLocked)
            {
                int main = run.MainDeck.Count;
                mark = run.Events.Count;
                run.ChooseEventOption(2);
                Expect(run.Events.Skip(mark).Count(e => e.Kind == RunEventKind.CardSealed && e.Pile == RunPile.Main) == 2
                       && run.MainDeck.Count == main - 2 && run.SealedMain.Count == 2 && run.EventDone == (run.Phase != RunPhase.Ready),
                    $"억지로 지나감: 봉인 2장 (메인 {main} → {run.MainDeck.Count}, {run.Phase})");
                if (run.Phase == RunPhase.NeedsRefill)
                    Expect(run.Floor == floor && run.EventDone && run.CanLeaveFloor(out _) == false, "보충할 때는 이벤트 층에 남음");
            }
            else
                Expect(traps || run.Chips >= 60, "모두 잠기지 않았는데 조건을 만족하는 선택지가 없음");

            // 저주받은 거울(덱만): 빛 속성 몬스터 3장 이상이면 칩, 아니면 그 선택지는 잠김. 거울을 깨면 1장 봉인.
            run = ToEventFloor(NewRun(StarterDecks.Yugi, seed: 41));
            run.ReplaceEventForTest("cursed-mirror");
            var mirror = run.CurrentEvent;
            int light = run.MainDeck.Concat(run.ExtraDeck).Count(c => mirror.Choices[0].Requires.Matches(c, _db));
            Expect(run.CanChooseEventOption(0, out _) == (light >= 3), $"빛 속성 몬스터 {light}장: 조건 선택지");
            Expect(RunState.ConditionText(mirror.Choices[0], mirror.Scope) == "빛 속성 몬스터 3장 이상 (덱)", "조건 글: " + RunState.ConditionText(mirror.Choices[0], mirror.Scope));

            // 갈림길: 칩 +100과 봉인 1장. 패배로 봉인된 카드가 있으면 용의 사당(드래곤족 2장, 카이바 덱)은 1장 봉인 해제, 없으면 칩 +60.
            var kaiba = ToEventFloor(NewRun(StarterDecks.Kaiba, seed: 43));
            kaiba.ReplaceEventForTest("dragon-shrine");
            Expect(kaiba.CanChooseEventOption(0, out _), "카이바 덱은 드래곤족 몬스터 2장 이상");
            chips = kaiba.Chips;
            kaiba.ChooseEventOption(0);
            Expect(kaiba.SealedCards().Count == 0 && kaiba.Chips == chips + 60, $"봉인된 카드가 없으면 칩 +60: {chips} → {kaiba.Chips}");

            kaiba = NewRun(StarterDecks.Kaiba, seed: 43);
            kaiba.BeginDuel();
            kaiba.FinishDuel(DuelResult.Loss, 0);
            ToEventFloor(kaiba);
            kaiba.ReplaceEventForTest("crossroads");
            int sealedBefore = kaiba.SealedCards().Count;
            chips = kaiba.Chips;
            kaiba.ChooseEventOption(0);
            Expect(kaiba.Chips == chips + 100 && kaiba.SealedCards().Count == sealedBefore + 1, $"험한 길: 칩 +100, 봉인 {sealedBefore} → {kaiba.SealedCards().Count}");
            if (kaiba.Phase == RunPhase.Ready)
            {
                ToEventFloor(kaiba);
                kaiba.ReplaceEventForTest("dragon-shrine");
                int side = kaiba.SideDeck.Count;
                sealedBefore = kaiba.SealedCards().Count;
                mark = kaiba.Events.Count;
                kaiba.ChooseEventOption(0);
                Expect(kaiba.SealedCards().Count == sealedBefore - 1 && kaiba.SideDeck.Count == side + 1
                       && HasNew(kaiba, mark, e => e.Kind == RunEventKind.CardUnsealed && !e.IsBonus), "용의 사당: 봉인 1장 해제 → 사이드 덱");
            }

            // 물물교환(덱만): 마법 5장 이상이면 마법 카드 1장을 잃고 칩 +120. 잃은 카드는 컬렉션에 남는다.
            run = ToEventFloor(NewRun(StarterDecks.Yugi, seed: 41));
            run.ReplaceEventForTest("barter");
            if (run.CanChooseEventOption(0, out _))
            {
                var collectionBefore = run.Collection.TotalCount;
                int main = run.MainDeck.Count;
                chips = run.Chips;
                mark = run.Events.Count;
                run.ChooseEventOption(0);
                var lost = run.Events.Skip(mark).FirstOrDefault(e => e.Kind == RunEventKind.CardLost);
                Expect(lost != null && _db.TryGet(lost.Code, out var rec) && (rec.Type & OcgCardType.Spell) != 0 && run.MainDeck.Count == main - 1
                       && run.Chips == chips + 120 && run.Collection.TotalCount == collectionBefore,
                    $"물물교환: 마법 카드 1장 잃음 ({lost?.Code}), 칩 {chips} → {run.Chips}");
                Expect(run.Phase == RunPhase.Ready ? run.Floor == floor + 1 : run.Floor == floor && run.EventDone,
                    $"잃은 뒤 메인 {run.MainDeck.Count}장: {run.Phase}, {run.Floor}층");
            }
            else
                Expect(false, "유희 덱에 마법 카드가 5장 미만이라 물물교환 시험을 못 함");
        }

        /// <summary>이벤트 전투: 고르면 그 층에서 듀얼을 기다리고(떠날 수 없음), 정해진 상대와 듀얼. 이기면 결과만(보상·칩 없음, LP +1000), 지면 봉인. 어느 쪽이든 다음 층. 저장도.</summary>
        void EventBattle()
        {
            foreach (bool win in new[] { true, false })
            {
                var save = GameSave.NewGame();
                var run = ToEventFloor(save.StartRun(new RunSetup { Seed = 61, StartDeck = StarterDecks.Yugi }, _db, _rarities));
                int floor = run.Floor, chips = run.Chips, earned = run.ChipsEarned, side = run.SideDeck.Count;
                run.ReplaceEventForTest("old-duelist");
                run.ChooseEventOption(0);
                Expect(run.EventBattlePending && run.PendingEventBattleIndex == 0 && !run.EventDone && run.Floor == floor && run.Phase == RunPhase.Ready,
                    "이벤트 전투를 고르면 그 층에서 기다림");
                Expect(!run.CanLeaveFloor(out _) && !run.CanChooseEventOption(1, out _), "이벤트 전투를 기다리는 동안 떠나거나 다시 고를 수 없음");
                string text = save.Write();
                Expect(text.Contains("\nfloor-event old-duelist battle 0\n") && RoundTrip(save) == text, "이벤트 전투를 기다리는 저장: floor-event … battle 0");

                var duel = run.BeginDuel();
                Expect(duel.IsEventBattle && duel.Opponent.Id == "Joey" && !duel.IsBossFloor, $"이벤트 전투 상대 {duel.Opponent.Id}");
                if (win)
                {
                    // 듀얼 중 저장을 읽으면 그 듀얼은 패배: 이벤트 전투도 일반 패배처럼 봉인되고 다음 층으로.
                    var forfeited = GameSave.Parse(save.Write(), _db, _rarities).Run;
                    Expect(!forfeited.EventBattlePending && forfeited.SealedCards().Count >= 1 && forfeited.LosingStreak == 1
                           && (forfeited.Phase != RunPhase.Ready || forfeited.Floor == floor + 1),
                        $"듀얼 중 저장 → 이벤트 전투 패배 처리: {forfeited.Floor}층, 봉인 {forfeited.SealedCards().Count}");

                    int mark = run.Events.Count;
                    run.FinishDuel(DuelResult.Win, 3000);
                    Expect(run.Floor == floor + 1 && !run.EventBattlePending && run.Lp == (run.FloorKind == FloorKind.Rest ? 8000 : 4000)
                           && run.Chips == chips + 50 && run.ChipsEarned == earned + 50 && run.SideDeck.Count == side + 1 && run.SideDeck.Contains(71625222u)
                           && run.Collection.Count(71625222) >= 1 && run.PendingReward == null
                           && !HasNew(run, mark, e => e.Kind == RunEventKind.RewardOffered || e.Kind == RunEventKind.RewardSubstituted),
                        $"이벤트 전투 승리: {run.Floor}층, LP {run.Lp}, 칩 {run.Chips} (기대 {chips + 50}), 사이드 {run.SideDeck.Count}");
                }
                else
                {
                    int main = run.MainDeck.Count;
                    run.FinishDuel(DuelResult.Loss, 0);
                    int expectSealed = Math.Min(2 + floor / 10, main); // 1 + 연패 1 + 층÷10
                    Expect(run.LosingStreak == 1 && run.SealedMain.Count == expectSealed && run.Chips == chips && run.SideDeck.Count == side
                           && (run.Phase != RunPhase.Ready || run.Floor == floor + 1 && run.Lp == 8000),
                        $"이벤트 전투 패배: 봉인 {run.SealedMain.Count} (기대 {expectSealed}), {run.Phase}, {run.Floor}층, LP {run.Lp}");
                    if (run.Phase == RunPhase.NeedsRefill)
                    {
                        text = save.Write();
                        Expect(text.Contains("\nfloor-event old-duelist done\n") && RoundTrip(save) == text, "보충 중인 이벤트 층 저장: floor-event … done");
                    }
                }
            }
        }

        /// <summary>이벤트 도감: 만난 이벤트·골라 본 선택지를 런을 넘어 모으고 저장한다. 모르는 Id도 버리지 않는다.</summary>
        void EventCodexSave()
        {
            var save = GameSave.NewGame();
            var run = ToEventFloor(save.StartRun(new RunSetup { Seed = 71, StartDeck = StarterDecks.Yugi }, _db, _rarities));
            run.ReplaceEventForTest("dragon-shrine");
            Expect(!save.Codex.Seen("dragon-shrine"), "런을 저장하기 전에는 도감에 없음");
            run.ChooseEventOption(2); // 지나간다
            string text = save.Write();
            Expect(save.Codex.Seen("dragon-shrine") && save.Codex.Chosen("dragon-shrine", 2) && !save.Codex.Chosen("dragon-shrine", 0)
                   && text.Contains("\nevent-seen dragon-shrine 2\n"), "저장하면 이번 런의 이벤트가 도감에 들어감");
            Expect(!save.Codex.IsComplete(TowerEventList.Find("dragon-shrine")), "지나간다만 골랐으면 도감 미완성");
            Expect(TowerEventList.Find("dragon-shrine").CodexChoices.SequenceEqual(new[] { 0, 1 }) && save.Codex.IsComplete(TowerEventList.QuietFloor) == save.Codex.Seen("quiet-floor"),
                "도감 선택지: 지나간다·결과 없는 선택지 제외 (고요한 층은 만나면 완성)");

            var loaded = GameSave.Parse(text + "event-seen gone-event 0 3\n", _db, _rarities);
            Expect(loaded.Codex.Chosen("dragon-shrine", 2) && loaded.Codex.Seen("gone-event") && loaded.Codex.Chosen("gone-event", 3),
                "도감 읽기 (모르는 이벤트 Id도 그대로)");
            loaded.Codex.MarkChosen("dragon-shrine", 0);
            loaded.Codex.MarkChosen("dragon-shrine", 1);
            Expect(loaded.Codex.IsComplete(TowerEventList.Find("dragon-shrine")), "도감 선택지를 모두 골라 보면 완성");
            Expect(loaded.Write().Contains("\nevent-seen gone-event 0 3\n") && RoundTrip(loaded) == loaded.Write(), "도감 저장 다시 읽기");
            loaded.AbandonRun();
            Expect(loaded.Run == null && loaded.Codex.Chosen("dragon-shrine", 2), "런을 정리해도 도감은 남음");
            Broken("format 5\ngold 0\nevent-seen\n", "이벤트 Id 없는 도감 줄");
            Broken("format 5\ngold 0\nevent-seen a -1\n", "도감 선택지 번호가 음수");
        }

        // ---------------- 상점 덱·아이템 ----------------

        const uint Telescope = 17092736; // 고대의 망원경 (카이바 덱에 1장)
        const uint Dice = 74137509;      // 천사의 주사위 (스타터 덱에 없음)

        static int OfferPrice(ShopOffer offer) =>
            offer.Product == ShopProduct.ItemCard ? ItemCards.All.First(i => i.Code == offer.Card).Price : RunRules.ShopPrice(offer.Product);

        /// <summary>1~4층이 모두 전투 층이고 5층 칩교환소의 아이템 카드가 item인 시드.</summary>
        static ulong SeedWithShopItem(uint item)
        {
            for (ulong seed = 1; ; seed++)
                if (Enumerable.Range(1, 4).All(f => Tower.KindOf(seed, f) == FloorKind.Battle) && ChipExchange.Stock(seed, 5).Last().Card == item)
                    return seed;
        }

        /// <summary>듀얼은 모두 LP 8000으로 이기고 다른 층은 지나가며, 칩교환소까지 간다.</summary>
        RunState ToShopFloor(RunState run)
        {
            for (int guard = 0; guard < 80 && !(run.Phase == RunPhase.Ready && run.FloorKind == FloorKind.ChipExchange); guard++)
            {
                if (run.Phase != RunPhase.Ready)
                    Choose(run);
                else if (Tower.IsBattle(run.FloorKind) || run.EventBattlePending)
                {
                    run.BeginDuel();
                    run.FinishDuel(DuelResult.Win, 8000);
                    Choose(run);
                }
                else if (run.FloorKind == FloorKind.Event && !run.EventDone)
                    run.ChooseEventOption(PassChoice(run));
                else
                    run.LeaveFloor();
            }
            Expect(run.Phase == RunPhase.Ready && run.FloorKind == FloorKind.ChipExchange, $"칩교환소까지 가기: {run.Floor}층 {run.FloorKind}");
            return run;
        }

        /// <summary>아이템 카드 목록: 카드 정보에 있고 공식 한글 카드명과 같다.</summary>
        void ItemCardList()
        {
            foreach (var item in ItemCards.All)
                Expect(_db.TryGet(item.Code, out _) && _db.GetName(item.Code) == item.Name && _db.NameCode(item.Code) == item.Code && item.Price > 0,
                    $"아이템 카드 {item.Code}: 카드 정보 이름 \"{_db.GetName(item.Code)}\" (목록 \"{item.Name}\")");
            Expect(ItemCards.All.Select(i => i.Code).Distinct().Count() == ItemCards.All.Count, "아이템 카드 목록에 같은 카드가 두 번 없음");
            Expect(ItemCards.Of(Telescope, _db)?.Effect == ItemEffectKind.PreviewFloors && ItemCards.Of(Telescope, _db).Bound
                   && ItemCards.Of(Dice, _db)?.Effect == ItemEffectKind.CoinTossWin && !ItemCards.Of(Dice, _db).Bound, "견본: 망원경(미리보기, 귀속), 주사위(코인 토스, 귀속 아님)");
            Expect(StarterDecks.Kaiba.ToCardList().Count(c => c == Telescope) == 1, "카이바 덱에 고대의 망원경 1장");
        }

        /// <summary>칩교환소의 아이템 카드 → 상점 덱, 상점 덱에서 적용, 저장·읽기와 형식 오류.</summary>
        void ShopDeckAndItems()
        {
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = SeedWithShopItem(Dice), StartDeck = StarterDecks.Kaiba }, _db, _rarities);
            uint plain = run.MainDeck.First(c => ItemCards.Of(c, _db) == null);
            Expect(run.PreviewFloors == RunRules.BasePreviewFloors && run.CoinTossWinPercent == RunRules.CoinTossWinPercent && run.ShopDeck.Count == 0,
                "아이템 없이 미리보기 1층, 코인 토스 50%, 상점 덱 0장");
            Expect(run.CanApplyItem(Telescope, RunPile.Main, out _) && !run.CanApplyItem(Telescope, RunPile.Shop, out string notShop) && notShop.Contains("없는 카드")
                   && !run.CanApplyItem(plain, RunPile.Main, out string notItem) && notItem.Contains("아이템 카드가 아님")
                   && !run.CanApplyItem(Telescope, RunPile.Side, out _), "적용할 수 있는 곳: 그 카드가 있는 메인 덱·상점 덱의 아이템 카드");

            run = ToShopFloor(run);
            var offer = run.Shop.Last();
            Expect(offer.Product == ShopProduct.ItemCard && offer.Card == Dice && offer.Price == 80 && run.Chips == 4 * RunRules.ChipsPerWin,
                $"5층 칩교환소: 아이템 카드 {offer.Card} 칩 {offer.Price}, 보유 칩 {run.Chips}");
            Expect(run.CanBuy(ShopProduct.ItemCard, out string why), $"아이템 카드를 살 수 있음 ({why})");
            int mark = run.Events.Count;
            run.BuyItem();
            Expect(run.ShopDeck.SequenceEqual(new[] { Dice }) && run.Chips == 0 && run.Collection.Count(Dice) == 0 && !run.SideDeck.Contains(Dice)
                   && HasNew(run, mark, e => e.Kind == RunEventKind.ShopBought && e.Product == ShopProduct.ItemCard && e.Code == Dice && e.Count == 80 && e.Chips == 0),
                $"아이템 카드 구매: 상점 덱 {run.ShopDeck.Count}장, 칩 {run.Chips}, 컬렉션에 안 들어감");
            Expect(!run.CanBuy(ShopProduct.ItemCard, out string sold) && sold.Contains("이미") && !run.CanMoveToDeck(Dice, out _) && !run.CanMoveToSide(Dice, out _),
                "한 번만 사고, 상점 덱 카드는 옮길 수 없음");
            Expect(run.HeldCards(HoldScope.All).Contains(Dice) && !run.HeldCards(HoldScope.DeckAndSide).Contains(Dice), "보유 범위: 상점 덱은 \"상점 덱 포함\"에서만");

            mark = run.Events.Count;
            run.ApplyItem(Dice, RunPile.Shop);
            Expect(run.CoinTossWinPercent == RunRules.CoinTossWinPercent + 20 && !run.IsBound(Dice) && run.IsApplied(RunPile.Shop, Dice)
                   && HasNew(run, mark, e => e.Kind == RunEventKind.ItemApplied && e.Code == Dice && e.Pile == RunPile.Shop && !e.IsBonus),
                $"상점 덱의 주사위 적용: 코인 토스 {run.CoinTossWinPercent}%");
            Expect(!run.CanApplyItem(Dice, RunPile.Shop, out string again) && again.Contains("이미"), "같은 덱에 두 번 적용하지 않음");
            run.ApplyItem(Telescope, RunPile.Main);
            Expect(run.PreviewFloors == RunRules.BasePreviewFloors + 1 && run.IsBound(Telescope) && !run.CanMoveToSide(Telescope, out string bound) && bound.Contains("귀속")
                   && run.Events.Last().Kind == RunEventKind.ItemApplied && run.Events.Last().IsBonus,
                $"메인 덱의 망원경 적용: 미리보기 {run.PreviewFloors}층, 귀속으로 사이드 덱에 못 뺌");

            string text = save.Write();
            Expect(text.Contains($"\nshop-deck {Dice}\n") && text.Contains($"\nitems Main:{Telescope} Shop:{Dice}\n") && text.Contains($" ItemCard:80:1:{Dice}\n")
                   && RoundTrip(save) == text, "상점 덱·적용한 아이템·아이템 상품 저장");
            var loaded = GameSave.Parse(text, _db, _rarities).Run;
            Expect(loaded.CoinTossWinPercent == run.CoinTossWinPercent && loaded.PreviewFloors == run.PreviewFloors && loaded.IsBound(Telescope), "읽은 런의 아이템 효과");

            Broken(text.Replace($"\nitems Main:{Telescope} Shop:{Dice}\n", $"\nitems Side:{Dice}\n"), "사이드 덱에 적용한 아이템");
            Broken(text.Replace($"\nitems Main:{Telescope} Shop:{Dice}\n", $"\nitems Main:{Dice}\n"), "메인 덱에 없는 아이템 적용");
            Broken(text.Replace($"\nitems Main:{Telescope} Shop:{Dice}\n", $"\nitems Main:{plain}\n"), "아이템 카드가 아닌 카드 적용");
            Broken(text.Replace($"\nitems Main:{Telescope} Shop:{Dice}\n", $"\nitems Main:{Telescope} Main:{Telescope}\n"), "같은 적용이 두 번");
            Broken(text.Replace($"\nshop-deck {Dice}\n", "\n"), "상점 덱 없이 상점 덱 적용");
            Broken(text.Replace($"\nshop-deck {Dice}\n", $"\nshop-deck {Dice} {Dice} {Dice} {Dice}\n"), "상점 덱에 같은 카드명 4장");
            Broken(text.Replace($"\nshop-deck {Dice}\n", $"\nshop-deck {Dice} {string.Join(" ", run.MainDeck.Take(RunRules.MaxShopDeck))}\n"), "상점 덱 11장");
            Broken(text.Replace($" ItemCard:80:1:{Dice}\n", " ItemCard:80:1\n"), "아이템 상품에 카드가 없음");
            Broken(text.Replace($" ItemCard:80:1:{Dice}\n", $" ItemCard:80:1:{plain}\n"), "아이템 상품이 아이템 카드가 아님");

            // 중첩되지 않는 효과: 망원경을 메인 덱과 상점 덱 양쪽에 적용해도 +1층.
            var both = ToShopFloor(NewRun(StarterDecks.Kaiba, SeedWithShopItem(Telescope)));
            both.BuyItem();
            both.ApplyItem(Telescope, RunPile.Main);
            both.ApplyItem(Telescope, RunPile.Shop);
            Expect(both.ShopDeck.SequenceEqual(new[] { Telescope }) && both.AppliedItems.Count == 2 && both.PreviewFloors == RunRules.BasePreviewFloors + 1,
                $"망원경 두 곳 적용: 미리보기 {both.PreviewFloors}층 (중첩 안 됨)");
        }

        /// <summary>귀속: 메인 덱에서 적용한 망원경은 봉인되면 적용·귀속이 풀린다.</summary>
        void BoundItemRelease()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var run = NewRun(StarterDecks.Kaiba, seed);
                run.ApplyItem(Telescope, RunPile.Main);
                while (run.Phase == RunPhase.Ready && run.MainDeck.Contains(Telescope))
                {
                    Begin(run);
                    if (run.Phase == RunPhase.InDuel)
                        run.FinishDuel(DuelResult.Loss, 0);
                }
                if (run.MainDeck.Contains(Telescope) || !run.SealedMain.Contains(Telescope))
                    continue;
                var released = run.Events.LastOrDefault(e => e.Kind == RunEventKind.ItemReleased);
                var sealedEvent = run.Events.LastOrDefault(e => e.Kind == RunEventKind.CardSealed && e.Code == Telescope);
                Expect(run.SealedMain.Contains(Telescope) && !run.IsApplied(RunPile.Main, Telescope) && !run.IsBound(Telescope) && run.PreviewFloors == RunRules.BasePreviewFloors
                       && released != null && released.Code == Telescope && released.Pile == RunPile.Main && sealedEvent != null
                       && run.Events.ToList().IndexOf(released) > run.Events.ToList().IndexOf(sealedEvent),
                    $"시드 {seed}: 망원경이 봉인되면 적용·귀속이 풀림");
                return;
            }
            Expect(false, "망원경이 봉인되는 시드를 찾지 못함");
        }

        /// <summary>승리 보상을 고르는 중이면 그 자리를 고른다. 보스 보상(보상 1장 더, 봉인 해제는 첫 봉인 카드)까지.</summary>
        static void Choose(RunState run, int slot = 0)
        {
            for (int i = 0; i < 2 && run.Phase == RunPhase.ChoosingReward; i++)
                run.ChooseReward(slot);
            if (run.Phase == RunPhase.ChoosingUnseal)
                run.ChooseUnseal(run.SealedCards()[0]);
        }

        /// <summary>
        /// 지금 층이 비전투 층이면 지나간다 (칩교환소·휴식은 떠나기, 이벤트는 고를 수 있는 첫 비전투 선택지 → 그냥 지나가기 → 강제·전투 선택지).
        /// Ready일 때만. 이벤트 전투밖에 없으면 그것을 고르고 멈춘다 (다음 BeginDuel이 이벤트 전투).
        /// </summary>
        static void PassFloors(RunState run)
        {
            while (run.Phase == RunPhase.Ready && !Tower.IsBattle(run.FloorKind) && !run.EventBattlePending)
            {
                if (run.FloorKind == FloorKind.Event && !run.EventDone)
                    run.ChooseEventOption(PassChoice(run));
                else
                    run.LeaveFloor();
            }
        }

        /// <summary>이벤트를 지나갈 선택지: 고를 수 있는 첫 비전투 선택지, 없으면 -1(지나가기), 그것도 안 되면 고를 수 있는 첫 선택지.</summary>
        internal static int PassChoice(RunState run)
        {
            var ev = run.CurrentEvent;
            var open = Enumerable.Range(0, ev.Choices.Count).Where(i => run.CanChooseEventOption(i, out _)).ToList();
            var calm = open.Where(i => !ev.Choices[i].IsBattle).ToList();
            if (calm.Count > 0)
                return calm[0];
            return run.CanChooseEventOption(-1, out _) || open.Count == 0 ? -1 : open[0];
        }

        /// <summary>비전투 층을 지나 다음 전투 층의 듀얼을 시작한다.</summary>
        static RunDuel Begin(RunState run)
        {
            PassFloors(run);
            // 지나간 이벤트의 결과로 봉인되어 메인 덱이 모자라면 보충(첫 후보)하고 남은 비전투 층을 마저 지나간다.
            while (run.Phase == RunPhase.NeedsRefill)
            {
                run.MoveToDeck(run.RefillCandidates()[0]);
                PassFloors(run);
            }
            return run.BeginDuel();
        }

        /// <summary>런 종료 이벤트 (없으면 null). 종료 뒤에는 골드 정산 이벤트가 하나 더 온다.</summary>
        /// <summary>마지막으로 고른 승리 보상 이벤트 (카드 또는 빈 자리 골드).</summary>
        static RunEvent LastReward(RunState run) => run.Events.Last(e => e.Kind == RunEventKind.RewardGained || e.Kind == RunEventKind.RewardGold);

        static RunEvent EndEvent(RunState run) => run.Events.LastOrDefault(e => e.Kind == RunEventKind.RunEnded);

        /// <summary>보상 고르기·보충이 남았으면 끝내고 (보충은 첫 후보), 비전투 층을 지나 듀얼 하나를 정한 결과로 끝낸다. 이기면 rewardSlot 자리를 고른다.</summary>
        static RunDuel Play(RunState run, DuelResult result, int lp, int rewardSlot = 0)
        {
            Choose(run, rewardSlot);
            while (run.Phase == RunPhase.NeedsRefill)
                run.MoveToDeck(run.RefillCandidates()[0]);
            var duel = Begin(run);
            run.FinishDuel(result, lp);
            Choose(run, rewardSlot);
            return duel;
        }

        static int CountNew(RunState run, int from, RunEventKind kind, RunPile? pile = null) =>
            run.Events.Skip(from).Count(e => e.Kind == kind && (pile == null || e.Pile == pile));

        static bool HasNew(RunState run, int from, Func<RunEvent, bool> match) => run.Events.Skip(from).Any(match);

        static bool IsLp(RunEvent e, LpRestoreReason reason, int previous, int lp) =>
            e.Kind == RunEventKind.LpRestored && e.LpReason == reason && e.PreviousLp == previous && e.Lp == lp;

        void ScriptedRun()
        {
            var run = NewRun(StarterDecks.Yugi);
            Expect(run.MainDeck.Count == 50 && run.ExtraDeck.Count == 0 && run.SideDeck.Count == 0 && run.Gold == 0, "유희 편 시작 장수");

            // 1층 2연패: 2장, 3장 봉인 (1 + 연패 + 층÷10). LP는 8000, 층은 그대로.
            int mark = run.Events.Count;
            Play(run, DuelResult.Loss, 0);
            Expect(run.LosingStreak == 1 && run.Floor == 1 && run.Lp == 8000 && run.MainDeck.Count == 48, $"1패 뒤: 연패 {run.LosingStreak}, {run.Floor}층, LP {run.Lp}, 메인 {run.MainDeck.Count}");
            Expect(CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main) == 2 && run.SealedMain.Count == 2, "1층 1연패는 2장 봉인");
            mark = run.Events.Count;
            Play(run, DuelResult.Draw, 0);
            Expect(run.LosingStreak == 2 && run.Draws == 1 && run.MainDeck.Count == 45, $"무승부는 패배로: 연패 {run.LosingStreak}, 메인 {run.MainDeck.Count}");
            Expect(CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main) == 3, "1층 2연패는 3장 봉인");

            // 승리: 연패 0, 남은 LP + 1000, 칩 +20, 보상 1개 (후보 또는 골드 대체). 보상을 다 받으면 다음 층.
            mark = run.Events.Count;
            Begin(run);
            run.FinishDuel(DuelResult.Win, 3000);
            Expect(run.LosingStreak == 0 && run.Lp == 4000 && run.Chips == RunRules.ChipsPerWin && run.ChipsEarned == RunRules.ChipsPerWin,
                $"승리 뒤: 연패 {run.LosingStreak}, LP {run.Lp} (3000 + 1000), 칩 {run.Chips}");
            Expect(HasNew(run, mark, e => e.Kind == RunEventKind.DuelWon && e.Lp == 3000) && HasNew(run, mark, e => IsLp(e, LpRestoreReason.Win, 3000, 4000)),
                "승리 이벤트(남은 LP 3000)와 LP +1000 이벤트");
            Expect(HasNew(run, mark, e => e.Kind == RunEventKind.ChipsGained && e.Count == RunRules.ChipsPerWin && e.Chips == RunRules.ChipsPerWin), "칩 이벤트");
            Expect(CountNew(run, mark, RunEventKind.RewardOffered) + CountNew(run, mark, RunEventKind.RewardSubstituted) == 1, "승리 보상 1개");
            Expect(run.Phase == RunPhase.ChoosingReward ? run.Floor == 1 : run.Floor == 2, $"보상을 고르기 전에는 이긴 층: {run.Phase}, {run.Floor}층");
            Choose(run);
            Expect(run.Floor == 2 && run.FloorKind == FloorKind.Battle && HasNew(run, mark, e => e.Kind == RunEventKind.FloorEntered && e.Floor == 2),
                $"보상을 받은 뒤 2층 전투 층: {run.Floor}층 {run.FloorKind}");
            var duel = Begin(run);
            Expect(duel.PlayerLp == 4000 && duel.CreateSessionOptions().Team1.StartingLP == 4000, "다음 층은 남은 LP + 1000으로 시작");
            run.FinishDuel(DuelResult.Win, 2000);
            Choose(run);

            // 4층까지: 남은 LP 1000으로 이기면 다음 층은 2000으로 시작한다.
            while (run.Floor < 5)
            {
                int floor = run.Floor;
                duel = Begin(run);
                Expect(duel.PlayerLp == (floor == 3 ? 3000 : 2000), $"{floor}층 시작 LP {duel.PlayerLp}");
                run.FinishDuel(DuelResult.Win, 1000);
                Choose(run);
            }

            // 5층 칩교환소 (듀얼 없음) → 6층 이벤트 (선택지를 골라야 지나감, 지금 이벤트는 아무 일도 없음) → 7층.
            int chips = 4 * RunRules.ChipsPerWin;
            Expect(run.Floor == 5 && run.FloorKind == FloorKind.ChipExchange && run.Shop.Count >= 4 && run.Chips == chips && run.Lp == 2000,
                $"5층 칩교환소: {run.Floor}층 {run.FloorKind}, 진열 {run.Shop.Count}, 칩 {run.Chips}");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "칩교환소에서 듀얼 시작");
            ExpectThrows<InvalidOperationException>(() => run.ChooseEventOption(0), "칩교환소에서 이벤트 선택지");
            run.LeaveFloor();
            Expect(run.Floor == 6 && run.FloorKind == FloorKind.Event && run.CurrentEvent != null && run.Shop.Count == 0,
                $"6층 이벤트: {run.Floor}층 {run.FloorKind} {run.CurrentEventId}");
            run.ReplaceEventForTest(TowerEventList.QuietFloor.Id);
            ExpectThrows<InvalidOperationException>(() => run.LeaveFloor(), "이벤트 층은 선택지로만 떠남");
            ExpectThrows<InvalidOperationException>(() => run.ChooseEventOption(1), "없는 선택지");
            ExpectThrows<InvalidOperationException>(() => run.ChooseEventOption(-1), "고를 수 있는 선택지가 있으면 그냥 지나갈 수 없음");
            mark = run.Events.Count;
            run.ChooseEventOption(0);
            Expect(run.Floor == 7 && run.FloorKind == FloorKind.Battle && run.Lp == 2000 && run.Chips == chips && run.CurrentEventId == null
                   && HasNew(run, mark, e => e.Kind == RunEventKind.EventResolved && e.EventId == TowerEventList.QuietFloor.Id),
                $"꽝 이벤트는 아무 일 없이 7층: {run.Floor}층, LP {run.Lp}, 칩 {run.Chips}");

            // 7~8층을 남은 LP 1000으로 이기면 9층 휴식에 들어가며 LP 2000 → 8000.
            Play(run, DuelResult.Win, 1000);
            mark = run.Events.Count;
            Play(run, DuelResult.Win, 1000);
            chips += 2 * RunRules.ChipsPerWin;
            Expect(run.Floor == 9 && run.FloorKind == FloorKind.Rest && run.Lp == 8000 && HasNew(run, mark, e => IsLp(e, LpRestoreReason.Rest, 2000, 8000)),
                $"9층 휴식: {run.Floor}층 {run.FloorKind}, LP {run.Lp}");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "휴식 층에서 듀얼 시작");
            run.LeaveFloor();

            // 10층(보스 층): 휴식으로 8000. 이기면 남은 LP와 상관없이 8000, 칩 +50, 보상 뒤에 봉인된 카드 1장 봉인 해제 (사이드 덱으로).
            mark = run.Events.Count;
            duel = Begin(run);
            Expect(duel.PlayerLp == 8000 && run.FloorKind == FloorKind.Boss && CountNew(run, mark, RunEventKind.LpRestored) == 0,
                $"10층 보스: 시작 LP {duel.PlayerLp}, {run.FloorKind}");
            mark = run.Events.Count;
            run.FinishDuel(DuelResult.Win, 2500);
            chips += RunRules.ChipsPerBossWin;
            Expect(run.Floor == 10 && run.Lp == 8000 && run.Chips == chips, $"10층 승리 뒤 8000: {run.Floor}층 LP {run.Lp}, 칩 {run.Chips} (기대 {chips})");
            Expect(HasNew(run, mark, e => IsLp(e, LpRestoreReason.BossCleared, 2500, 8000)) && CountNew(run, mark, RunEventKind.LpRestored) == 1,
                "보스 격파 LP 이벤트 (+1000은 없음)");
            if (run.Phase == RunPhase.ChoosingReward)
                run.ChooseReward(0);
            Expect(run.Phase == RunPhase.ChoosingUnseal && run.Floor == 10 && run.SealedCount == 5 && HasNew(run, mark, e => e.Kind == RunEventKind.UnsealOffered && e.Count == 5),
                $"보스 보상: 봉인 해제 고르기 ({run.Phase}, 봉인 {run.SealedCount}장)");
            ExpectThrows<InvalidOperationException>(() => run.ChooseUnseal(run.MainDeck[0]), "봉인되지 않은 카드를 봉인 해제");
            uint unsealed = run.SealedCards()[0];
            int sideBefore = run.SideDeck.Count;
            mark = run.Events.Count;
            run.ChooseUnseal(unsealed);
            Expect(run.Floor == 11 && run.Phase == RunPhase.Ready && run.SealedCount == 4 && run.SideDeck.Count == sideBefore + 1 && run.SideDeck.Last() == unsealed
                   && HasNew(run, mark, e => e.Kind == RunEventKind.CardUnsealed && e.Code == unsealed && e.IsBonus && e.Pile == RunPile.Main),
                $"보스 보상 봉인 해제 → 사이드 덱, 11층 ({run.Floor}층, 봉인 {run.SealedCount}장)");

            // 11층: 7500으로 이기면 8500 (8000을 넘을 수 있다). 12층은 8500으로 시작.
            Expect(Begin(run).PlayerLp == 8000, "11층은 8000 그대로");
            run.FinishDuel(DuelResult.Win, 7500);
            Choose(run);
            Expect(run.Lp == 8500, $"승리 LP는 8000을 넘을 수 있음: {run.Lp}");
            Expect(Begin(run).PlayerLp == 8500, "12층은 8500으로 시작");

            // 12층 패배: 1 + 연패 1 + 12÷10 = 3장, LP 8000으로 회복. 칩은 잃지 않는다.
            mark = run.Events.Count;
            chips = run.Chips;
            run.FinishDuel(DuelResult.Loss, 0);
            Expect(CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main) == 3 && run.Lp == 8000 && run.Chips == chips, "12층 1연패는 3장 봉인, LP 회복, 칩 그대로");

            // 19층 휴식: LP가 8000보다 높아도 8000으로 맞춘다. 20층(보스)은 8000으로 시작.
            while (run.Floor < 18)
                Play(run, DuelResult.Win, 7500);
            mark = run.Events.Count;
            Play(run, DuelResult.Win, 7500);
            Expect(run.Floor == 19 && run.Lp == 8000 && HasNew(run, mark, e => IsLp(e, LpRestoreReason.Rest, 8500, 8000)),
                $"19층 휴식: 8000보다 높아도 8000 ({run.Floor}층 LP {run.Lp})");
            Expect(Begin(run).PlayerLp == 8000 && run.Floor == 20, "20층 보스는 8000으로 시작");
            mark = run.Events.Count;
            run.FinishDuel(DuelResult.Win, 8000);
            Expect(run.Lp == 8000 && CountNew(run, mark, RunEventKind.LpRestored) == 0, "보스 층을 LP 8000으로 이기면 LP 이벤트 없음");
            Choose(run);

            // 24층 3연패: 4, 5, 6장 (1 + 연패 + 24÷10).
            while (run.Floor < 24)
                Play(run, DuelResult.Win, 5000);
            for (int streak = 1; streak <= 3; streak++)
            {
                if (run.Phase == RunPhase.Ended)
                    break;
                mark = run.Events.Count;
                int mainBefore = run.Phase == RunPhase.NeedsRefill ? 40 : run.MainDeck.Count;
                Play(run, DuelResult.Loss, 0);
                int sealedCount = CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main);
                Expect(sealedCount == Math.Min(streak + 3, mainBefore), $"24층 {streak}연패: {sealedCount}장 봉인 (기대 {streak + 3}장)");
            }
            Expect(RunRules.MainSealCount(3, 25) == 6 && RunRules.MainSealCount(1, 9) == 2 && RunRules.MainSealCount(2, 9) == 3 && RunRules.MainSealCount(1, 10) == 3,
                "봉인 장수 공식 (1 + 연패 + 층÷10)");
        }

        void EndAndRefill()
        {
            var forty = new DeckRecipe("Test40", "시험 덱 40장", StarterDecks.Yugi.Entries.Take(40).ToList());
            var spare = StarterDecks.Yugi.Entries.Skip(40).Take(3).Select(e => e.Code).ToList();

            // 사이드 덱이 비어 있으면 첫 패배로 런 종료.
            var run = NewRun(forty);
            Play(run, DuelResult.Loss, 0);
            Expect(run.Phase == RunPhase.Ended && run.EndReason == RunEndReason.DeckBelowMinimum && EndEvent(run) != null
                   && run.Events.Last().Kind == RunEventKind.GoldSettled,
                $"보충할 카드가 없으면 런 종료 ({run.Phase})");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "끝난 런에서 듀얼 시작");

            // 사이드 덱이 있으면 보충할 때까지 듀얼을 시작할 수 없고, 40장이 되면 Ready.
            // 1연패(2장 봉인)를 사이드 덱 3장 중 2장으로 한 장씩 채운다.
            run = NewRun(forty, side: spare);
            Play(run, DuelResult.Loss, 0);
            Expect(run.Phase == RunPhase.NeedsRefill && run.RefillNeeded == 2 && run.RefillCandidates().Count == 3,
                $"보충 필요: {run.Phase}, {run.RefillNeeded}장, 후보 {run.RefillCandidates().Count}");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "보충 전에 듀얼 시작");
            Expect(!run.CanMoveToSide(run.MainDeck[0], out _), "보충 중에는 사이드 덱으로 옮길 수 없음");
            run.MoveToDeck(spare[1]);
            Expect(run.Phase == RunPhase.NeedsRefill && run.RefillNeeded == 1 && run.SideDeck.Count == 2, "1장 보충 뒤 아직 1장 더");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "보충이 덜 끝났는데 듀얼 시작");
            run.MoveToDeck(run.RefillCandidates()[0]);
            Expect(run.Phase == RunPhase.Ready && run.MainDeck.Count == 40 && run.SideDeck.Count == 1
                   && run.Events.Last().Kind == RunEventKind.MovedToDeck && run.Events.Last().IsRefill, "보충 뒤 Ready");

            // 2연패(3장 봉인)인데 사이드 덱이 1장뿐이면 채울 수 없어 런 종료.
            Play(run, DuelResult.Loss, 0);
            Expect(run.Phase == RunPhase.Ended && run.EndReason == RunEndReason.DeckBelowMinimum, $"2연패(3장)에 사이드 덱 1장이면 런 종료 ({run.Phase})");

            // 봉인 장수가 메인 덱보다 많으면 전부 봉인 (여기서는 40장 런을 많이 지게 하지 않고 공식만 본다).
            Expect(RunRules.MainSealCount(50, 1) > 40, "봉인 장수에 상한 없음");
        }

        /// <summary>카이바 편에만 있는 메인 덱 카드 (유희 편과 카드명이 겹치지 않음), 서로 다른 카드로 n장.</summary>
        List<uint> KaibaOnly(int n)
        {
            var yugiNames = new HashSet<uint>(StarterDecks.Yugi.Entries.Select(e => _db.NameCode(e.Code)));
            return StarterDecks.Kaiba.Entries.Select(e => e.Code).Distinct()
                .Where(c => !yugiNames.Contains(_db.NameCode(c)) && !IsExtra(c)).Take(n).ToList();
        }

        void ExchangeRules()
        {
            var kaibaOnly = KaibaOnly(11);
            Expect(kaibaOnly.Count == 11, $"카이바 편에만 있는 카드 {kaibaOnly.Count}종 (11종 필요)");

            // 메인 덱 57장 + 시작 사이드 덱 3장: 모두 넣으면 60장.
            var fiftySeven = new DeckRecipe("Test57", "시험 덱 57장",
                StarterDecks.Yugi.Entries.Concat(kaibaOnly.Take(7).Select(c => (c, 1))).ToList());
            var toAdd = kaibaOnly.Skip(7).Take(3).ToList();
            var run = NewRun(fiftySeven, side: toAdd);
            foreach (uint code in toAdd)
                run.MoveToDeck(code);
            Expect(run.MainDeck.Count == 60 && run.SideDeck.Count == 0, $"메인 덱 60장까지 넣기 ({run.MainDeck.Count})");
            run.MoveToSide(toAdd[0]);
            Expect(run.MainDeck.Count == 59 && run.SideDeck.Count == 1 && run.Events.Last().Kind == RunEventKind.MovedToSide, "메인 → 사이드");
            run.MoveToDeck(toAdd[0]);
            Expect(!run.CanMoveToDeck(toAdd[0], out _), "사이드 덱에 없는 카드는 넣을 수 없음");

            // 메인 덱 60장이면 더 못 넣는다.
            var sixty = new DeckRecipe("Test60", "시험 덱 60장",
                StarterDecks.Yugi.Entries.Concat(kaibaOnly.Take(10).Select(c => (c, 1))).ToList());
            var full = NewRun(sixty, side: new[] { kaibaOnly[10] });
            Expect(full.MainDeck.Count == 60 && !full.CanMoveToDeck(kaibaOnly[10], out string fullReason) && fullReason.Contains("60"),
                "메인 덱 60장이면 더 넣을 수 없음");

            // 시작 덱 블랙홀 1장 + 사이드 덱 2장 = 같은 카드명 3장: 둘 다 넣을 수 있다 (4장째는 승리 보상 고르기 시험에서).
            run = NewRun(StarterDecks.Yugi, side: new[] { DarkHole, DarkHole });
            run.MoveToDeck(DarkHole);
            run.MoveToDeck(DarkHole);
            Expect(run.DeckCopies(DarkHole) == 3 && run.SideDeck.Count == 0, "블랙홀 덱에 3장");

            // 메인 덱 40장이면 사이드 덱으로 뺄 수 없다.
            var forty = new DeckRecipe("Test40", "시험 덱 40장", StarterDecks.Yugi.Entries.Take(40).ToList());
            run = NewRun(forty);
            Expect(!run.CanMoveToSide(run.MainDeck[0], out _), "메인 덱 40장에서 빼기");

            // 엑스트라 덱 카드(조이 편 융합 몬스터)는 엑스트라 덱으로 오가고, 보충에는 못 쓴다.
            run = NewRun(StarterDecks.Joey);
            uint fusion = run.ExtraDeck[0];
            Expect(run.ExtraDeck.Count == 2 && run.MainDeck.Count == 48 && run.IsExtraDeckCard(fusion), "조이 편 메인 48 + 엑스트라 2");
            run.MoveToSide(fusion);
            Expect(run.ExtraDeck.Count == 1 && run.SideDeck.Contains(fusion), "엑스트라 → 사이드");
            run.MoveToDeck(fusion);
            Expect(run.ExtraDeck.Count == 2 && run.Events.Last().Pile == RunPile.Extra, "사이드 → 엑스트라");

            // 듀얼 중에는 덱을 바꿀 수 없다.
            Begin(run);
            Expect(!run.CanMoveToSide(run.MainDeck[0], out _) && !run.CanMoveToDeck(fusion, out _), "듀얼 중 교환");
            ExpectThrows<InvalidOperationException>(() => run.MoveToSide(run.MainDeck[0]), "듀얼 중 교환 예외");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "듀얼 중 다시 시작");
            ExpectThrows<ArgumentOutOfRangeException>(() => run.FinishDuel(DuelResult.Win, 0), "LP 0으로 승리");
            run.FinishDuel(DuelResult.Win, 100);

            // 보충 중에는 엑스트라 덱 카드를 넣을 수 없다.
            var joey40 = new DeckRecipe("Joey40", "조이 편 40장 + 엑스트라 1장",
                StarterDecks.Joey.Entries.Where(e => !IsExtra(e.Code)).Take(40).Append(StarterDecks.Joey.Entries.First(e => IsExtra(e.Code))).ToList());
            uint joeyFusion = StarterDecks.Joey.Entries.First(e => IsExtra(e.Code)).Code;
            var joeySpares = StarterDecks.Joey.Entries.Where(e => !IsExtra(e.Code)).Skip(40).Take(2).Select(e => e.Code).ToList();
            run = NewRun(joey40, side: joeySpares);
            run.MoveToSide(joeyFusion); // 엑스트라 덱을 비워 봉인은 메인 덱에서만 (1연패 2장)
            Play(run, DuelResult.Loss, 0);
            if (run.Phase == RunPhase.NeedsRefill)
            {
                Expect(!run.CanMoveToDeck(joeyFusion, out _), "보충 중 엑스트라 덱 카드");
                Expect(run.RefillCandidates().SequenceEqual(joeySpares.Distinct()), "보충 후보는 메인 덱 카드만");
            }
            else
                Expect(false, $"조이 40장 1패 뒤 보충 필요 ({run.Phase})");
        }

        bool IsExtra(uint code) => _db.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

        /// <summary>승리해서 보상 후보가 나올 때까지 이긴다 (등급 대체로 골드만 받은 승리는 건너뜀). 30번 안에 안 나오면 null.</summary>
        static RewardOffer WinUntilOffer(RunState run)
        {
            for (int i = 0; i < 30; i++)
            {
                Begin(run);
                run.FinishDuel(DuelResult.Win, 8000);
                if (run.Phase == RunPhase.ChoosingReward)
                    return run.PendingReward;
            }
            return null;
        }

        void RewardChoice()
        {
            // 등급 표에 블랙홀(N)만 둔다: N이 뽑히면 후보는 블랙홀 1장 + 빈 자리 2곳(골드), 다른 등급은 상대 덱에 없어 골드로 대체.
            var onlyDarkHole = new RarityTable();
            onlyDarkHole.Set(DarkHole, Rarity.N);
            var run = NewRun(StarterDecks.Yugi, seed: 11, side: new[] { DarkHole, DarkHole }, rarities: onlyDarkHole);
            run.MoveToDeck(DarkHole);
            run.MoveToDeck(DarkHole);

            int mark = run.Events.Count;
            var offer = WinUntilOffer(run);
            if (offer == null)
            {
                Expect(false, "30번 이기는 동안 N(블랙홀) 후보가 나오지 않음");
                return;
            }
            var substituted = run.Events.Skip(mark).Where(e => e.Kind == RunEventKind.RewardSubstituted).ToList();
            Expect(substituted.All(e => e.Rarity != Rarity.N && e.Count == RunRules.GoldFor(e.Rarity)) && run.Gold == substituted.Sum(e => e.Count),
                "N이 아닌 등급은 골드로 대체 (등급별 골드)");
            Expect(offer.Rarity == Rarity.N && offer.Cards.SequenceEqual(new[] { DarkHole }) && offer.EmptySlots == 2
                   && offer.Gold == RunRules.GoldFor(Rarity.N) && offer.IsCard(0) && !offer.IsCard(1) && !offer.IsCard(2),
                $"후보: [{offer.Rarity}] {string.Join(",", offer.Cards)}, 빈 자리 {offer.EmptySlots}, 골드 {offer.Gold}");
            var offered = run.Events.Last(e => e.Kind == RunEventKind.RewardOffered);
            Expect(offered.Codes.SequenceEqual(offer.Cards) && offered.Count == offer.Gold && offered.DuelNumber == run.DuelsPlayed
                   && offered.Floor == run.Floor, "보상 후보 이벤트 (이긴 층)");
            Expect(run.Phase == RunPhase.ChoosingReward && run.Floor == offer.Floor, "보상을 고르기 전에는 이긴 층 그대로 (다 받으면 오름)");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "보상을 고르기 전에 듀얼 시작");
            Expect(!run.CanMoveToSide(run.MainDeck[0], out string why) && why.Contains("보상"), $"보상을 고르기 전에 교환 ({why})");
            ExpectThrows<ArgumentOutOfRangeException>(() => run.ChooseReward(RunRules.RewardChoices), "없는 자리");
            ExpectThrows<ArgumentOutOfRangeException>(() => run.ChooseReward(-1), "음수 자리");

            // 빈 자리를 고르면 골드.
            int gold = run.Gold;
            int side = run.SideDeck.Count;
            run.ChooseReward(1);
            Expect(run.Phase == RunPhase.Ready && run.PendingReward == null && run.Gold == gold + offer.Gold && run.SideDeck.Count == side
                   && LastReward(run).Kind == RunEventKind.RewardGold && LastReward(run).Count == offer.Gold && run.Floor == offer.Floor + 1, "빈 자리 → 골드, 다음 층");
            ExpectThrows<InvalidOperationException>(() => run.ChooseReward(0), "고른 뒤 다시 고르기");

            // 다음 블랙홀 후보에서 카드를 고른다: 사이드 덱과 컬렉션에 들어가고, 덱에 이미 3장이라 덱에는 넣을 수 없다.
            offer = WinUntilOffer(run);
            if (offer == null)
            {
                Expect(false, "두 번째 블랙홀 후보가 나오지 않음");
                return;
            }
            int owned = run.Collection.Count(DarkHole);
            gold = run.Gold;
            run.ChooseReward(0);
            Expect(run.SideDeck.Count(c => c == DarkHole) == 1 && run.Collection.Count(DarkHole) == owned + 1 && run.Gold == gold
                   && LastReward(run).Kind == RunEventKind.RewardGained && LastReward(run).Code == DarkHole, "카드 자리 → 사이드 덱과 컬렉션");
            Expect(!run.CanMoveToDeck(DarkHole, out string reason) && reason.Contains("같은 카드명"), $"같은 카드명은 덱에 3장까지 ({reason})");

            // 사이드 덱에는 같은 카드명 제한이 없다: 덱에서 하나 빼면 사이드 덱 2장 + 덱 2장 = 4장.
            run.MoveToSide(DarkHole);
            Expect(run.SideDeck.Count(c => c == DarkHole) == 2 && run.DeckCopies(DarkHole) == 2 && run.CanMoveToDeck(DarkHole, out _),
                "사이드 덱은 같은 카드명 제한 없음");
        }

        void RewardSubstitutionAndTableChange()
        {
            // 모든 카드를 SR로 둔 표: SR이 뽑히면 후보 3장(서로 다른 카드)에서 고르고, 다른 등급은 골드로 대체한다.
            var allSr = new RarityTable();
            foreach (var deck in StarterDecks.All)
                foreach (var (code, _) in deck.Entries)
                    allSr.Set(code, Rarity.SR);
            var run = NewRun(StarterDecks.Yugi, seed: 7, rarities: allSr);
            for (int i = 0; i < 40; i++)
                Play(run, DuelResult.Win, 8000);
            var offered = run.Events.Where(e => e.Kind == RunEventKind.RewardOffered).ToList();
            var gained = run.Events.Where(e => e.Kind == RunEventKind.RewardGained).ToList();
            var substituted = run.Events.Where(e => e.Kind == RunEventKind.RewardSubstituted).ToList();
            int bossWins = run.Events.Count(e => e.Kind == RunEventKind.DuelWon && e.Floor % 10 == 0);
            Expect(offered.Count(e => !e.IsBonus) + substituted.Count(e => !e.IsBonus) == 40 && bossWins > 0
                   && offered.Count(e => e.IsBonus) + substituted.Count(e => e.IsBonus) == bossWins, $"승리 40번, 보상 40개 + 보스 보상 {bossWins}개 (봉인된 카드가 없어 1장 더)");
            Expect(offered.All(e => e.Rarity == Rarity.SR && e.Codes.Count == 3 && e.Codes.Distinct().Count() == 3 && e.Count == 0)
                   && substituted.All(e => e.Rarity != Rarity.SR), "SR이면 서로 다른 후보 3장 (빈 자리 없음), 다른 등급은 대체");
            Expect(gained.Count == offered.Count && gained.All(e => e.Rarity == Rarity.SR), "후보마다 1장씩 얻음");
            Expect(substituted.Count > 0 && offered.Count > 0, $"후보 {offered.Count}, 대체 {substituted.Count}");
            Expect(substituted.All(e => e.Count == RunRules.GoldFor(e.Rarity)) && run.Gold == substituted.Sum(e => e.Count),
                $"대체 골드 = 등급별 골드, 런 골드 {run.Gold}");
            Expect(run.SideDeck.Count == gained.Count, "사이드 덱 장수 = 얻은 카드 수");
            Expect(gained.All(e => run.Collection.Count(e.Code) >= 2), "얻은 카드는 컬렉션에 더해짐");
            var opponentOf = run.Events.Where(e => e.Kind == RunEventKind.DuelStarted).ToDictionary(e => e.DuelNumber, e => StarterDecks.Find(e.DeckId));
            Expect(offered.All(e => e.Codes.All(c => opponentOf[e.DuelNumber].Entries.Any(x => x.Code == c))), "후보는 그 듀얼 상대 덱의 카드");

            // 표를 런 도중에 바꾸면 다음 보상부터 바로 반영된다 (런은 등급을 저장하지 않는다).
            foreach (var deck in StarterDecks.All)
                foreach (var (code, _) in deck.Entries)
                    allSr.Set(code, Rarity.N);
            int mark = run.Events.Count;
            for (int i = 0; i < 40; i++)
                Play(run, DuelResult.Win, 8000);
            var after = run.Events.Skip(mark).Where(e => e.Kind == RunEventKind.RewardGained).ToList();
            Expect(after.Count > 0 && after.All(e => e.Rarity == Rarity.N), "등급을 바꾼 뒤에는 바뀐 등급으로 보상");

            // 실제 등급 표: 모든 상대 덱에 N·R·SR·UR이 있으면 1~49층에서는 대체가 없다.
            var real = NewRun(StarterDecks.Kaiba, seed: 3);
            for (int i = 0; i < 30; i++)
                Play(real, DuelResult.Win, 8000);
            bool allPresent = StarterDecks.All.All(d => _rarities.CountByRarity(d).Take(4).All(n => n > 0));
            if (allPresent)
                Expect(!real.Events.Any(e => e.Kind == RunEventKind.RewardSubstituted), "모든 상대 덱에 N~UR이 있으면 31층까지 골드 대체 없음");
        }

        void SameNameCopies()
        {
            // 일러스트만 다른 ID: 카드명은 원래 카드로 세고, 덱에는 쓰지 않는다 (기본 ID를 쓴다).
            const uint darkMagician = 46986414;
            var alt = _db.Records.Select(rec => rec.Data.Code).FirstOrDefault(c => c != darkMagician && _db.NameCode(c) == darkMagician && _db.IsAlternativeArt(c));
            Expect(alt != 0, "블랙 매지션의 일러스트 ID가 카드 DB에 있음");
            if (alt != 0)
            {
                Expect(_db.NameCode(alt) == darkMagician && _db.NameCode(darkMagician) == darkMagician && !_db.IsAlternativeArt(darkMagician),
                    "일러스트 ID의 카드명은 블랙 매지션");
                var withAlt = new DeckRecipe("Alt", "일러스트 ID", StarterDecks.Yugi.Entries.Take(39).Append((alt, 1)).ToList());
                Expect(withAlt.Validate(_db).Any(p => p.Contains("일러스트")), "덱에 일러스트 ID를 쓰면 문제");
            }

            // 일러스트가 아닌데 카드명이 같은 두 ID (에라타 전·후, "이 카드명은 언제나 ~로 취급" 등): 합쳐서 3장까지.
            var yugiNames = new HashSet<uint>(StarterDecks.Yugi.Entries.Select(e => _db.NameCode(e.Code)));
            const OcgCardType mainKinds = OcgCardType.Monster | OcgCardType.Spell | OcgCardType.Trap;
            var pair = _db.Records.Where(rec => rec.Data.Alias != 0 && !_db.IsAlternativeArt(rec.Data.Code)
                                                && (rec.Type & mainKinds) != 0 && (rec.Type & OcgCardType.Token) == 0 && !DeckRecipe.IsExtraDeckType(rec.Type)
                                                && _db.TryGet(rec.Data.Alias, out var original) && original.Data.Alias == 0
                                                && (original.Type & OcgCardType.Token) == 0 && !DeckRecipe.IsExtraDeckType(original.Type)
                                                && !yugiNames.Contains(rec.Data.Alias))
                .Select(rec => (A: rec.Data.Alias, B: rec.Data.Code)).OrderBy(p => p.B).FirstOrDefault();
            if (!Expect2(pair.A != 0, "카드명이 같은 (일러스트가 아닌) 두 ID가 카드 DB에 있음"))
                return;
            var (a, b) = pair;
            Expect(_db.NameCode(b) == a && !_db.IsAlternativeArt(b), $"{b}의 카드명은 {a}");
            var baseCards = StarterDecks.Yugi.ToCardList().Take(47).GroupBy(c => c).Select(g => (g.Key, g.Count())).ToList();
            var twoAtwoB = new DeckRecipe("Names4", "같은 카드명 4장", baseCards.Append((a, 2)).Append((b, 2)).ToList());
            Expect(twoAtwoB.Validate(_db).Any(p => p.Contains("같은 카드명 4장")), "덱에 같은 카드명 4장 (ID 둘을 합쳐)");
            var twoA = new DeckRecipe("Names2", "같은 카드명 2장", baseCards.Append((a, 2)).ToList());
            Expect(twoA.Validate(_db).Count == 0, "같은 카드명 2장 덱은 문제없음");

            var collection = CardCollection.FromRecipes(StarterDecks.All);
            collection.Add(a, 3);
            collection.Add(b, 3);
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = twoA, SideDeck = new[] { b, b } }, _db, collection).Any(p => p.Contains("같은 카드명")),
                "시작 덱 2장 + 사이드 덱(다른 ID) 2장 = 같은 카드명 4장");
            var run = NewRun(twoA, side: new[] { b }, collection: collection);
            Expect(run.DeckCopies(a) == 2 && run.DeckCopies(b) == 2, $"덱 장수는 카드명으로 ({run.DeckCopies(a)}, {run.DeckCopies(b)})");
            run.MoveToDeck(b);
            Expect(run.DeckCopies(a) == 3 && run.DeckCopies(b) == 3 && run.MainDeck.Count(c => c == b) == 1, "다른 ID를 넣어도 같은 카드명으로 셈");
            run.MoveToSide(a);
            Expect(run.CanMoveToDeck(a, out _), "2장이면 다시 넣을 수 있음");
        }

        /// <summary>Expect와 같고, 결과를 돌려준다.</summary>
        bool Expect2(bool condition, string message)
        {
            Expect(condition, message);
            return condition;
        }

        void Determinism()
        {
            var script = new (DuelResult, int)[]
            {
                (DuelResult.Loss, 0), (DuelResult.Win, 5000), (DuelResult.Win, 3000), (DuelResult.Loss, 0), (DuelResult.Loss, 100),
                (DuelResult.Win, 8000), (DuelResult.Draw, 0), (DuelResult.Win, 1), (DuelResult.Win, 7000), (DuelResult.Loss, 0),
                (DuelResult.Win, 2000), (DuelResult.Win, 2000), (DuelResult.Win, 2000), (DuelResult.Win, 2000), (DuelResult.Loss, 0),
            };
            List<string> Events(ulong seed)
            {
                var run = NewRun(StarterDecks.Joey, seed);
                int step = 0;
                foreach (var (result, lp) in script)
                {
                    if (run.Phase == RunPhase.Ended)
                        break;
                    Play(run, result, lp, rewardSlot: step++ % RunRules.RewardChoices);
                }
                return run.Events.Select(e => e.ToString()).ToList();
            }
            var a = Events(1234);
            var b = Events(1234);
            var c = Events(1235);
            Expect(a.SequenceEqual(b), "같은 시드, 같은 결과면 이벤트가 같음");
            Expect(!a.SequenceEqual(c), "시드가 다르면 이벤트가 다름");

            var r1 = RunRandom.For(5, 3, RunRandomPurpose.Reward);
            var r2 = RunRandom.For(5, 3, RunRandomPurpose.Reward);
            r1.NextULong(); // 앞에서 더 뽑아도
            var r3 = RunRandom.For(5, 3, RunRandomPurpose.Reward);
            Expect(r2.NextULong() == r3.NextULong(), "같은 (시드, 듀얼, 용도)는 늘 같은 난수열");
            Expect(RunRandom.For(5, 3, RunRandomPurpose.Reward).NextULong() != RunRandom.For(5, 3, RunRandomPurpose.Seal).NextULong(), "용도가 다르면 다른 난수열");
            Expect(RunRandom.For(5, 3, RunRandomPurpose.Reward).NextULong() != RunRandom.For(5, 4, RunRandomPurpose.Reward).NextULong(), "듀얼 번호가 다르면 다른 난수열");
        }

        void SetupChecks()
        {
            var collection = CardCollection.FromRecipes(StarterDecks.All);
            var fourSide = KaibaOnly(4);
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi, SideDeck = fourSide }, _db, collection).Any(p => p.Contains("사이드 덱")),
                "시작 사이드 덱 4장");
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi, SideDeck = fourSide.Take(3).ToList() }, _db, collection).Count == 0,
                "시작 사이드 덱 3장");
            // 시작 덱(유희 편: 블랙홀 1장)과 사이드 덱을 합쳐 같은 카드명 3장까지.
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi, SideDeck = new[] { DarkHole, DarkHole } }, _db, collection).Count == 0,
                "시작 덱 1장 + 사이드 덱 2장 = 3장");
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi, SideDeck = new[] { DarkHole, DarkHole, DarkHole } }, _db, collection)
                    .Any(p => p.Contains("같은 카드명")), "시작 덱 1장 + 사이드 덱 3장 = 4장");
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi }, _db, CardCollection.FromRecipes(new[] { StarterDecks.Kaiba })).Count > 0,
                "컬렉션에 없는 카드로 시작");
            var thirtyNine = new DeckRecipe("Test39", "39장", StarterDecks.Yugi.Entries.Take(39).ToList());
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = thirtyNine }, _db, collection).Count > 0, "39장으로 시작");
            ExpectThrows<ArgumentException>(() => RunState.Start(new RunSetup { StartDeck = thirtyNine }, _db, _rarities, collection), "39장 런 시작");
            Expect(CardCollection.FromRecipes(StarterDecks.All).Count(DarkHole) == 4, "첫 컬렉션: 블랙홀 4장 (4덱 합)");
        }

        void RarityParsing()
        {
            var table = RarityTable.Parse("# 주석\n53129443 UR  # 블랙홀\n\n4206964\tsr\r\n  15025844 N\n");
            Expect(table.Count == 3 && table.TryGet(4206964, out var r) && r == Rarity.SR, "등급 파일 기본 형식 (탭·소문자·CRLF·주석)");
            ExpectThrows<FormatException>(() => RarityTable.Parse("53129443 XR"), "모르는 등급");
            ExpectThrows<FormatException>(() => RarityTable.Parse("53129443 3"), "숫자 등급");
            ExpectThrows<FormatException>(() => RarityTable.Parse("53129443 UR\n53129443 SR"), "같은 카드 두 번");
            ExpectThrows<FormatException>(() => RarityTable.Parse("블랙홀 UR"), "카드 코드가 아님");
            ExpectThrows<FormatException>(() => RarityTable.Parse("53129443 UR SR"), "칸이 셋");
            Expect(_rarities.Check(StarterDecks.All, _db).Count == 0, "실제 등급 파일 확인 (아래 등급 표 확인에서 자세히)");
        }

        // --- 런 포기와 저장 ---

        void AbandonRun()
        {
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = 7, StartDeck = StarterDecks.Kaiba }, _db, _rarities);
            ExpectThrows<InvalidOperationException>(() => save.StartRun(new RunSetup { Seed = 8, StartDeck = StarterDecks.Yugi }, _db, _rarities),
                "진행 중인 런이 있는데 새 런 시작");
            Begin(run);
            ExpectThrows<InvalidOperationException>(() => run.Abandon(), "듀얼 중 포기");
            run.FinishDuel(DuelResult.Win, 4000);
            Expect(run.Phase == RunPhase.ChoosingReward, $"1층 승리 뒤 보상 고르기 ({run.Phase})");
            int events = run.Events.Count;
            save.AbandonRun();
            Expect(run.Phase == RunPhase.Ended && run.EndReason == RunEndReason.Abandoned && run.Events.Count == events + 2 && run.PendingReward == null
                   && EndEvent(run).Describe(c => "") == "런 종료: 포기" && run.Events.Last().Kind == RunEventKind.GoldSettled,
                $"보상을 고르는 중에도 포기하면 런 종료 ({run.Phase}, {run.EndReason})");
            Expect(save.Run == null, "포기한 런은 저장에서 빠짐");
            int gained = run.Events.Count(e => e.Kind == RunEventKind.RewardGained);
            Expect(save.Collection.TotalCount == CardCollection.FromRecipes(StarterDecks.All).TotalCount + gained, "포기해도 얻은 카드는 컬렉션에 남음");
            Expect(run.SettlementGold == RunRules.SettlementGold(1, RunRules.ChipsPerWin, RunRules.ChipsPerWin) && run.SettlementGold > 0
                   && run.Events.Last().Count == run.SettlementGold, $"포기한 런도 골드 정산 ({run.SettlementGold})");
            Expect(save.Gold == run.Gold + run.SettlementGold, "포기한 런의 골드(보상 골드 + 정산)도 누적에 더함");
            var next = save.StartRun(new RunSetup { Seed = 8, StartDeck = StarterDecks.Yugi }, _db, _rarities);
            Expect(next.Phase == RunPhase.Ready && save.Run == next && save.LastStartDeckId == "Yugi", "포기한 뒤 새 런 시작");
        }

        string RoundTrip(GameSave save, RarityTable rarities = null) => GameSave.Parse(save.Write(), _db, rarities ?? _rarities, "시험").Write();

        /// <summary>저장 → 읽기 → 저장이 같은 글이고, 읽은 런을 이어 가면 원래 런과 똑같이 진행되는지.</summary>
        void SaveRoundTrip()
        {
            var fresh = GameSave.NewGame();
            Expect(RoundTrip(fresh) == fresh.Write(), "새 게임 저장 다시 읽기");
            Expect(GameSave.Parse(fresh.Write(), _db, _rarities).Run == null, "새 게임에는 진행 중인 런이 없음");

            var side = StarterDecks.Joey.Entries.Take(3).Select(e => e.Code).ToList();
            var script = new (DuelResult, int)[]
            {
                (DuelResult.Win, 5000), (DuelResult.Loss, 0), (DuelResult.Win, 3000), (DuelResult.Draw, 0), (DuelResult.Win, 8000),
                (DuelResult.Win, 1200), (DuelResult.Loss, -300), (DuelResult.Loss, 0), (DuelResult.Win, 9000), (DuelResult.Win, 400),
                (DuelResult.Win, 2000), (DuelResult.Win, 2000), (DuelResult.Loss, 0), (DuelResult.Win, 2000), (DuelResult.Win, 2000),
            };
            var original = GameSave.NewGame();
            original.StartRun(new RunSetup { Seed = 99, StartDeck = StarterDecks.Yugi, SideDeck = side }, _db, _rarities);
            var reloaded = GameSave.Parse(original.Write(), _db, _rarities, "시험");
            Expect(reloaded.LastStartDeckId == "Yugi" && reloaded.Run.SideDeck.SequenceEqual(side) && reloaded.Run.StartDeckName == StarterDecks.Yugi.Name
                   && reloaded.Recipes.Select(r => r.Id).SequenceEqual(StarterDecks.All.Select(d => d.Id)),
                "마지막 시작 덱, 런의 시작 사이드 덱·시작 덱 이름, 레시피 4개");
            int step = 0, mismatches = 0, choosingSaves = 0;
            foreach (var (result, lp) in script)
            {
                foreach (var save in new[] { original, reloaded })
                {
                    var run = save.Run;
                    if (run.Phase == RunPhase.Ended)
                        continue;
                    // 보상은 다음 걸음에서 고른다 (보상 고르는 중인 상태도 저장·읽기를 거친다). 자리는 걸음마다 바꿔 빈 자리도 섞는다.
                    Choose(run, step % RunRules.RewardChoices);
                    while (run.Phase == RunPhase.NeedsRefill)
                        run.MoveToDeck(run.RefillCandidates()[0]);
                    // 사이드 덱 교환도 섞는다: 사이드 덱 첫 카드를 넣을 수 있으면 넣고, 메인 덱 첫 카드를 뺀다.
                    if (run.SideDeck.Count > 0 && run.CanMoveToDeck(run.SideDeck[0], out _))
                        run.MoveToDeck(run.SideDeck[0]);
                    if (run.CanMoveToSide(run.MainDeck[0], out _))
                        run.MoveToSide(run.MainDeck[0]);
                    Begin(run);
                    run.FinishDuel(result, lp);
                }
                step++;
                if (original.Run.Phase == RunPhase.ChoosingReward)
                    choosingSaves++;
                if (original.Write() != reloaded.Write())
                    mismatches++;
                if (RoundTrip(original) != original.Write())
                    mismatches++;
                // 다음 걸음은 방금 저장한 것을 읽은 런으로 간다.
                reloaded = GameSave.Parse(reloaded.Write(), _db, _rarities, "시험");
            }
            Expect(mismatches == 0, $"저장하며 이어 간 런이 원래 런과 같음 (불일치 {mismatches}번 / {step}걸음)");
            Expect(choosingSaves > 0, "보상 고르는 중인 런도 저장·읽기를 거침");
            var a = original.Run.Events.Select(e => e.ToString()).ToList();
            var b = reloaded.Run.Events.Select(e => e.ToString()).ToList();
            Expect(a.SequenceEqual(b), "이벤트도 같음");
            var kinds = new HashSet<RunEventKind>(original.Run.Events.Select(e => e.Kind));
            Expect(kinds.Contains(RunEventKind.RewardOffered) && kinds.Contains(RunEventKind.RewardGained) && kinds.Contains(RunEventKind.CardSealed)
                   && kinds.Contains(RunEventKind.MovedToSide) && kinds.Contains(RunEventKind.LpRestored),
                "시험 런에 보상 후보·획득·봉인·교환·LP 변화가 모두 있음");
            Expect(reloaded.Collection.TotalCount == original.Collection.TotalCount && reloaded.Run.Gold == original.Run.Gold, "컬렉션 장수·골드 같음");

            // 끝난 런을 정리하면 골드가 누적에 더해지고 런이 빠진다.
            var ended = GameSave.NewGame();
            var forty = new DeckRecipe("Test40", "시험 덱 40장", StarterDecks.Yugi.Entries.Take(40).ToList());
            var endRun = ended.StartRun(new RunSetup { Seed = 5, StartDeck = forty }, _db, _rarities);
            Expect(!ended.CloseEndedRun(), "끝나지 않은 런은 정리하지 않음");
            Begin(endRun);
            endRun.FinishDuel(DuelResult.Loss, 0);
            Expect(endRun.Phase == RunPhase.Ended, "40장 덱 첫 패배로 런 종료");
            Expect(RoundTrip(ended) == ended.Write(), "끝난 런도 저장·읽기 (스타터 덱이 아닌 시작 덱 Id도 그대로)");
            int before = ended.Gold;
            Expect(ended.CloseEndedRun() && ended.Run == null && ended.Gold == before + endRun.Gold + endRun.SettlementGold && endRun.SettlementGold == 1,
                "끝난 런 정리: 골드(보상 골드 + 1층 정산 1) 누적, 런 빠짐");
            Expect(GameSave.Parse(ended.Write(), _db, _rarities).Gold == ended.Gold, "골드 저장·읽기");
        }

        /// <summary>듀얼 중(InDuel)으로 저장된 런을 읽으면 그 듀얼은 LP 0으로 진 것으로 처리된다.</summary>
        void ForfeitOnLoad()
        {
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = 21, StartDeck = StarterDecks.Kaiba }, _db, _rarities);
            Begin(run);
            run.FinishDuel(DuelResult.Win, 7600);
            Choose(run);
            Expect(run.Lp == 8600, $"1층을 7600으로 이겨 LP {run.Lp}");
            var duel = Begin(run);
            string text = save.Write();
            Expect(text.Contains("\nphase InDuel\n") && text.Contains("듀얼 중"), "듀얼 중에도 저장 (주석에 패배 처리 안내)");

            var loaded = GameSave.Parse(text, _db, _rarities, "시험");
            var r = loaded.Run;
            Expect(loaded.ForfeitedDuel == duel.DuelNumber, $"패배로 처리한 듀얼 {loaded.ForfeitedDuel} (기대 {duel.DuelNumber})");
            Expect(r.Phase == RunPhase.Ready && r.CurrentDuel == null && r.DuelsPlayed == 2 && r.Losses == 1 && r.LosingStreak == 1 && r.Floor == 2,
                $"읽은 런: {r.Phase}, 듀얼 {r.DuelsPlayed}, {r.Losses}패, 연패 {r.LosingStreak}, {r.Floor}층");
            Expect(r.Lp == 8000, $"LP 0으로 진 것으로 쳐서 8000 (시작 LP 8600을 남기지 않음): {r.Lp}");
            var forfeited = r.Events.Where(e => e.Kind == RunEventKind.DuelForfeited).ToList();
            Expect(forfeited.Count == 1 && forfeited[0].DuelNumber == 2
                   && r.Events.Count(e => e.Kind == RunEventKind.CardSealed && e.DuelNumber == 2 && e.Pile == RunPile.Main) == 2,
                "DuelForfeited 이벤트 1개, 2층 1연패 메인 덱 봉인 2장");

            // 규칙만으로 같은 듀얼을 LP 0으로 진 것과 같다 (DuelForfeited 이벤트만 더).
            run.FinishDuel(DuelResult.Loss, 0);
            var expected = run.Events.Select(e => e.ToString()).ToList();
            var got = r.Events.Where(e => e.Kind != RunEventKind.DuelForfeited).Select(e => e.ToString()).ToList();
            Expect(expected.SequenceEqual(got), "LP 0으로 진 것과 이벤트가 같음");
            Expect(r.SealedMain.SequenceEqual(run.SealedMain) && r.MainDeck.SequenceEqual(run.MainDeck), "봉인된 카드도 같음");

            // 패배로 처리한 런을 저장하고 다시 읽으면 더는 처리하지 않는다.
            var again = GameSave.Parse(loaded.Write(), _db, _rarities, "시험");
            Expect(again.ForfeitedDuel == 0 && again.Write() == loaded.Write(), "다시 읽으면 그대로");

            // 런 시작 직후 첫 듀얼 중에 꺼진 경우도 같다.
            var first = GameSave.NewGame();
            Begin(first.StartRun(new RunSetup { Seed = 22, StartDeck = StarterDecks.Joey }, _db, _rarities));
            var firstLoaded = GameSave.Parse(first.Write(), _db, _rarities, "시험");
            Expect(firstLoaded.ForfeitedDuel == 1 && firstLoaded.Run.Losses == 1 && firstLoaded.Run.Floor == 1 && firstLoaded.Run.Phase == RunPhase.Ready,
                "첫 듀얼 중에 꺼짐");
        }

        void Broken(string text, string what, RarityTable rarities = null) =>
            ExpectThrows<FormatException>(() => GameSave.Parse(text, _db, rarities ?? _rarities), what);

        void SaveFormatErrors()
        {
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = PlainSeed, StartDeck = StarterDecks.Pegasus }, _db, _rarities);
            Begin(run);
            run.FinishDuel(DuelResult.Win, 6000);
            string choosing = run.Phase == RunPhase.ChoosingReward ? save.Write() : null;
            Choose(run);
            string good = save.Write();
            Expect(GameSave.Parse(good, _db, _rarities).Run.Floor == 2, "정상 파일 읽기");
            Expect(GameSave.Parse(good.Replace("\n", "\r\n"), _db, _rarities).Write() == good, "CRLF 줄바꿈도 읽음");

            string Replace(string line, string with) => good.Replace("\n" + line + "\n", "\n" + with + "\n");
            Broken("", "빈 파일");
            Broken(good.Replace("format 5\n", ""), "format 줄 없음");
            Broken(good.Replace("format 5\n", "format 6\n"), "새 버전 형식");
            Broken(good.Replace("format 5\n", "format 5\nunknown 3\n"), "모르는 키");
            Broken(good.Replace("format 5\n", "format 5\nformat 5\n"), "같은 키 두 번");
            Broken(good.Replace("format 5\n", "format 3\n"), "형식 3에 형식 4의 줄 (floor-kind, chips)");
            // 형식 4: 지금 층 종류, 칩, 칩교환소 진열, 이벤트 층의 이벤트, 보스 보상
            string chipsLine = $"chips {run.Chips} {run.ChipsEarned}";
            Expect(run.Chips == RunRules.ChipsPerWin && good.Contains("\n" + chipsLine + "\n") && good.Contains("\nfloor-kind Battle\n"), "칩·층 종류 줄");
            Broken(Replace("floor-kind Battle", ""), "floor-kind 줄 없음");
            Broken(Replace("floor-kind Battle", "floor-kind Lobby"), "모르는 층 종류");
            Broken(Replace(chipsLine, "chips 30 20"), "가진 칩이 얻은 칩보다 많음");
            Broken(Replace(chipsLine, "chips -1 20"), "칩 음수");
            Broken(Replace(chipsLine, "chips 20"), "chips 칸 수");
            Broken(Replace("floor-kind Battle", "floor-kind ChipExchange"), "칩교환소인데 shop 줄 없음");
            Broken(good.Replace("\nend-run\n", "\nshop Unseal:150:0\nend-run\n"), "칩교환소가 아닌데 shop 줄");
            string shop = Replace("floor-kind Battle", "floor-kind ChipExchange\nshop SwapAny:50:0 Unseal:150:1");
            Expect(GameSave.Parse(shop, _db, _rarities).Run.Shop.Select(o => $"{o.Product}{o.Price}{o.Sold}").SequenceEqual(new[] { "SwapAny50False", "Unseal150True" }),
                "shop 줄 읽기 (진열·가격·산 것)");
            Broken(shop.Replace("Unseal:150:1", "Unseal:150:2"), "shop 샀는지가 0·1이 아님");
            Broken(shop.Replace("Unseal:150:1", "SwapAny:150:1"), "shop 같은 상품 두 번");
            Broken(shop.Replace("Unseal:150:1", "Lottery:1:0"), "shop 모르는 상품");
            Broken(shop.Replace("Unseal:150:1", "Unseal:150"), "shop 칸 꼴");
            Broken(Replace("floor-kind Battle", "floor-kind Event"), "이벤트 층인데 floor-event 줄 없음");
            Broken(Replace("floor-kind Battle", "floor-kind Event\nfloor-event nowhere"), "모르는 이벤트");
            Broken(good.Replace("\nend-run\n", "\nfloor-event quiet-floor\nend-run\n"), "이벤트 층이 아닌데 floor-event 줄");
            Expect(GameSave.Parse(Replace("floor-kind Battle", "floor-kind Event\nfloor-event quiet-floor"), _db, _rarities).Run.CurrentEvent == TowerEventList.QuietFloor,
                "floor-event 줄 읽기");
            Broken(good.Replace("\nend-run\n", "\nboss-bonus\nend-run\n"), "보스 보상을 고르는 중이 아닌데 boss-bonus");
            Broken(Replace("phase Ready", "phase ChoosingUnseal"), "봉인된 카드 없이 보스 보상 봉인 해제");
            Broken(Replace("floor-kind Battle", "floor-kind Rest").Replace("\nphase Ready\n", "\nphase InDuel\n"), "휴식 층에서 듀얼 중");
            Broken(good.Replace("event FloorEntered duel=0 floor=1", "event FloorEntered duel=0 floor=1 kind=Lobby"), "이벤트의 모르는 층 종류");
            Broken(good.Replace("event FloorEntered duel=0 floor=1", "event FloorEntered duel=0 floor=1 bonus=2"), "이벤트 bonus가 1이 아님");
            Broken(good.Replace("\nend-run\n", "\n"), "end-run 없음");
            Broken(Replace("floor 2", "floor 0"), "0층");
            Broken(Replace("floor 2", "floor 이층"), "층이 숫자가 아님");
            Broken(Replace("phase Ready", "phase ChoosingReward"), "보상을 고르는 중인데 reward 줄 없음");
            Broken(Replace("phase Ready", "phase 1"), "상태를 숫자로");
            Broken(Replace("phase Ready", "phase Ended"), "끝났는데 종료 이유 없음");
            Broken(Replace("wins 1", "wins 2"), "전적이 듀얼 수와 다름");
            Broken(Replace("opponents Yugi Kaiba Joey Pegasus", "opponents Yugi Nobody"), "모르는 상대 덱");
            Broken(Replace($"seed {PlainSeed}", ""), "seed 줄 없음");
            Broken(good.Replace("\nsealed-main\n", "\nsealed-main 99999999\n"), "카드 DB에 없는 카드");
            Broken(good.Replace("\nsealed-main\n", "\nfainted-main\n"), "형식 4에 fainted-main");
            Broken(good.Replace("event DuelWon duel=1", "event DuelWon duel=x"), "이벤트 칸 숫자 아님");
            Broken(good.Replace("event DuelWon duel=1", "event DuelWon wins=1 duel=1"), "모르는 이벤트 칸");
            Broken(good.Replace("event DuelWon duel=1 floor=1", "event DuelWon duel=1"), "이벤트에 floor 없음");
            Broken(good.Replace("format 5\ngold 0\n", "format 5\ngold -1\n"), "골드 음수");
            Broken(good.Replace("\nrecipe Kaiba ", "\nrecipe Yugi "), "레시피 Id 두 번");
            Broken(good.Replace("\nrecipe-side Joey", "\nrecipe-side Nobody"), "recipe 줄에 없는 레시피의 사이드 덱");
            Broken(Regex.Replace(good, @"\nrecipe-deck Pegasus [^\n]*", ""), "recipe-deck 줄 없음");
            Broken(good.Replace("\nrecipe-side Joey", "\nrecipe-side Joey\nrecipe-side Joey"), "recipe-side 두 번");
            Broken(good.Replace("\nrecipe Yugi ", "\nrecipe Yugi %2 "), "레시피 줄 칸 수");
            Broken(good.Replace("\nrecipe Yugi ", "\nrecipe Yugi %zz"), "이름의 % 표기 틀림");
            Broken(good.Replace("\nrecipe-deck Yugi ", "\nrecipe-deck Yugi 99999x "), "레시피 카드 코드가 숫자 아님");
            Broken("format 5\nrecipe-deck a 53129443\n", "recipe 없이 recipe-deck");
            Broken(good.Replace("format 5\n", "format 5\nlast-side-deck Yugi\n"), "형식 4에 last-side-deck");
            Broken("format 2\ncard 53129443 0\n", "컬렉션 0장");
            Broken("format 2\ncard 53129443 1\ncard 53129443 2\n", "컬렉션에 같은 카드 두 번");
            Broken("format 2\npoints 1 2 3 4 5\n", "형식 2에 points");
            Expect(GameSave.Parse("format 2\n# 주석\n\ncard 53129443 2\n", _db, _rarities).Collection.Count(53129443) == 2, "주석·빈 줄");

            // 보상 고르는 중(reward 줄): 후보까지 그대로 읽고, 맞지 않는 reward 줄은 거절한다.
            Expect(choosing != null, "1층 승리 뒤 보상 후보가 나옴");
            if (choosing == null)
                return;
            var choosingSave = GameSave.Parse(choosing, _db, _rarities);
            Expect(choosingSave.Write() == choosing && choosingSave.Run.Phase == RunPhase.ChoosingReward && choosingSave.Run.PendingReward != null,
                "보상 후보 저장·읽기");
            string rewardLine = choosing.Split('\n').First(l => l.StartsWith("reward ", StringComparison.Ordinal));
            var fields = rewardLine.Split(' ');
            Broken(choosing.Replace("\n" + rewardLine + "\n", "\n"), "reward 줄 없음");
            Broken(good.Replace("\nend-run\n", "\n" + rewardLine + "\nend-run\n"), "보상을 고르는 중이 아닌데 reward 줄");
            Broken(choosing.Replace(rewardLine, "reward 2 " + string.Join(" ", fields.Skip(2))), "보상의 듀얼 번호가 끝낸 듀얼과 다름");
            Broken(choosing.Replace(rewardLine, rewardLine + " " + fields[5]), "후보에 같은 카드 두 번 (또는 4장)");
            Broken(choosing.Replace(rewardLine, string.Join(" ", fields.Take(5)) + " 99999999"), "후보 카드가 카드 DB에 없음");
            Broken(choosing.Replace(rewardLine, string.Join(" ", fields.Take(5).Select((f, i) => i == 3 ? "XR" : f))), "보상 등급이 이상함");
            Broken(choosing.Replace(rewardLine, "reward 1 2 " + string.Join(" ", fields.Skip(3))), "보상의 층이 지금 층과 다름");
            Broken(good.Replace("\nend-run\n", "\nreward-bonus\nend-run\n"), "reward 줄 없이 reward-bonus");
            Broken(choosing.Replace("\nfloor-kind Battle\n", "\nfloor-kind ChipExchange\nshop Unseal:150:0\n"), "보상을 고르는 중인데 칩교환소");

            // 형식 3의 보상 고르는 중 런: 층이 이미 올라가 있었으므로 1 내려 읽는다. 칩은 0, 층은 전투 층.
            string v3Choosing = StripV4(choosing).Replace("\nformat ?\n", "\nformat 3\n").Replace("\nfloor 1\n", "\nfloor 2\n");
            var fromV3 = GameSave.Parse(v3Choosing, _db, _rarities);
            Expect(fromV3.Run.Floor == 1 && fromV3.Run.Phase == RunPhase.ChoosingReward && fromV3.Run.FloorKind == FloorKind.Battle && fromV3.Run.Chips == 0
                   && StripV4(fromV3.Write()) == StripV4(choosing),
                $"형식 3의 보상 고르는 중 런: {fromV3.Run.Floor}층 {fromV3.Run.Phase} " + FirstDifference(StripV4(choosing), StripV4(fromV3.Write())));
            fromV3.Run.ChooseReward(0);
            Expect(fromV3.Run.Floor == 2 && fromV3.Run.Phase == RunPhase.Ready, $"형식 3에서 읽은 런의 보상을 고르면 2층: {fromV3.Run.Floor}층");
            Broken(v3Choosing.Replace("\nfloor 2\n", "\nfloor 1\n"), "형식 3의 보상 고르는 중인데 1층");
        }

        /// <summary>형식 4에서 생긴 줄·이벤트·이벤트 칸을 지우고 format 줄을 "format ?"로 바꾼다 (옛 형식의 글을 만들거나 비교할 때).</summary>
        static string StripV4(string text)
        {
            text = Regex.Replace(text, @"\n(floor-kind|chips|shop|floor-event|boss-bonus|reward-bonus|event-seen)( [^\n]*)?(?=\n)", "");
            text = Regex.Replace(text, @"\n# 이벤트 도감 [^\n]*", "");
            text = Regex.Replace(text, @"\nevent (FloorEntered|ChipsGained|ShopBought|CardUnsealed|UnsealOffered|EventResolved|GoldSettled) [^\n]*", "");
            text = Regex.Replace(text, @" (kind|chips|chips-earned|product|event-id|bonus)=[^ \n]+", "");
            return Regex.Replace(text, @"\nformat \d+\n", "\nformat ?\n");
        }

        /// <summary>형식 1(2026-11-10 규칙 전) 저장 파일: 포인트는 골드로, 기절은 봉인으로 바꿔 읽는다.</summary>
        void SaveFormatV1()
        {
            var profile = GameSave.Parse("format 1\npoints 1 2 0 0 1\nlast-start-deck Yugi\n", _db, _rarities);
            int expectedGold = RunRules.GoldFor(Rarity.N) + 2 * RunRules.GoldFor(Rarity.R) + RunRules.GoldFor(Rarity.FR);
            Expect(profile.Gold == expectedGold && profile.LastStartDeckId == "Yugi", $"포인트 → 골드 {profile.Gold} (기대 {expectedGold})");
            Expect(profile.Write().StartsWith("# ", StringComparison.Ordinal) && profile.Write().Contains($"\nformat {GameSave.FormatVersion}\n") && !profile.Write().Contains("points")
                   && profile.Recipes.Count == StarterDecks.All.Count, "형식 1을 읽어 쓰면 지금 형식 (스타터 덱 레시피 4개)");
            Broken("format 1\npoints 1 2 3\n", "포인트 칸 수");
            Broken("format 1\ngold 3\n", "형식 1에 gold");

            // 모든 카드를 FR로 둔 표: 50층까지는 FR이 뽑히지 않아 승리마다 골드 대체 (형식 1의 포인트 대체와 같은 꼴).
            var allFr = new RarityTable();
            foreach (var deck in StarterDecks.All)
                foreach (var (code, _) in deck.Entries)
                    allFr.Set(code, Rarity.FR);
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = 4, StartDeck = StarterDecks.Joey }, _db, allFr);
            foreach (var (result, lp) in new[] { (DuelResult.Win, 5000), (DuelResult.Loss, 0), (DuelResult.Win, 3000), (DuelResult.Loss, 0), (DuelResult.Loss, 0) })
                Play(run, result, lp);
            Expect(run.Phase == RunPhase.Ready && run.Gold > 0 && run.SealedMain.Count > 0 && !run.Events.Any(e => e.Kind == RunEventKind.RewardOffered),
                $"시험 런: {run.Phase}, 골드 {run.Gold}, 봉인 {run.SealedMain.Count}");
            string v4 = save.Write();

            // 같은 런을 형식 1 글로 바꾼다.
            var substituted = new int[RarityTable.RarityCount];
            foreach (var e in run.Events.Where(e => e.Kind == RunEventKind.RewardSubstituted))
                substituted[(int)e.Rarity]++;
            // 형식 3·4에 새로 생긴 줄(레시피, 시작 덱 이름, 탑·칩)과 이벤트 칸(deck-name 등)은 형식 1에 없다.
            string v1 = Regex.Replace(StripV4(v4), @"\n(recipe|recipe-deck|recipe-side|start-deck-name) [^\n]*", "");
            v1 = Regex.Replace(v1, @" deck-name=[^ \n]+", "")
                .Replace("\nformat ?\ngold 0\n", "\nformat 1\npoints 0 0 0 0 0\n")
                .Replace($"\ngold {run.Gold}\n", $"\nsubstituted {string.Join(" ", substituted)}\n")
                .Replace("\nsealed-main", "\nfainted-main")
                .Replace("\nsealed-extra", "\nfainted-extra")
                .Replace("event CardSealed ", "event CardFainted ");
            v1 = Regex.Replace(v1, @"(event RewardSubstituted [^\n]*?) count=\d+", "$1");
            Expect(v1.Contains("\nformat 1\n") && v1.Contains("\nsubstituted ") && v1.Contains("event CardFainted ")
                   && !Regex.IsMatch(v1, @"event RewardSubstituted [^\n]*count="), "형식 1 글 만들기");
            var fromV1 = GameSave.Parse(v1, _db, allFr, "시험");
            Expect(StripV4(fromV1.Write()) == StripV4(v4) && fromV1.Run.Chips == 0,
                "형식 1을 읽어 지금 형식으로 쓰면 원래 글과 같음 (탑·칩 줄 빼고): " + FirstDifference(StripV4(v4), StripV4(fromV1.Write())));
            Expect(fromV1.Run.Gold == run.Gold && fromV1.Run.SealedMain.SequenceEqual(run.SealedMain), "골드와 봉인 카드");

            Broken(v1.Replace("\nphase Ready\n", "\nphase InDuel\n"), "형식 1에는 듀얼 중 상태가 없음", allFr);
            Broken(v1.Replace("\nfainted-main", "\nsealed-main"), "형식 1에 sealed-main", allFr);
            Broken(v1.Replace("\nsubstituted ", "\ngold 3\nsubstituted "), "형식 1의 런에 gold", allFr);
        }

        /// <summary>형식 2(레시피 전): 스타터 덱 4개를 레시피로, 시작 덱별 마지막 시작 사이드 덱을 그 레시피의 사이드 덱으로.</summary>
        void SaveFormatV2()
        {
            var side = StarterDecks.Joey.Entries.Take(2).Select(e => e.Code).ToList();
            var save = GameSave.Parse($"format 2\ngold 5\nlast-start-deck Kaiba\nlast-side-deck Kaiba {side[0]} {side[1]}\nlast-side-deck Nobody {DarkHole}\n",
                _db, _rarities);
            Expect(save.Recipes.Select(r => r.Id).SequenceEqual(StarterDecks.All.Select(d => d.Id)) && save.LastStartDeckId == "Kaiba" && save.Gold == 5,
                "형식 2: 스타터 덱 레시피 4개, 마지막 시작 덱, 골드");
            Expect(save.FindRecipe("Kaiba").Side.SequenceEqual(side) && save.Recipes.Where(r => r.Id != "Kaiba").All(r => r.Side.Count == 0),
                "형식 2의 마지막 시작 사이드 덱 → 그 레시피의 사이드 덱 (모르는 시작 덱은 버림)");
            Expect(save.FindRecipe("Kaiba").Deck.SequenceEqual(StarterDecks.Kaiba.ToCardList()) && save.FindRecipe("Kaiba").Name == StarterDecks.Kaiba.Name,
                "형식 2: 레시피 덱·이름은 스타터 덱 그대로");
            Broken("format 2\nlast-side-deck Yugi 1\nlast-side-deck Yugi 2\n", "형식 2에 같은 시작 덱 두 번");
            Broken("format 2\nrecipe a b\n", "형식 2에 recipe");

            // 형식 2의 런(시작 덱 이름 줄 없음): 이름은 스타터 덱 이름, 런 시작 이벤트에도 채운다.
            var v4 = GameSave.NewGame();
            v4.StartRun(new RunSetup { Seed = 8, StartDeck = StarterDecks.Pegasus }, _db, _rarities);
            string v2Text = Regex.Replace(StripV4(v4.Write()), @"\n(recipe|recipe-deck|recipe-side|start-deck-name) [^\n]*", "");
            v2Text = Regex.Replace(v2Text, @" deck-name=[^ \n]+", "").Replace("\nformat ?\n", "\nformat 2\n");
            var fromV2 = GameSave.Parse(v2Text, _db, _rarities);
            Expect(fromV2.Run.StartDeckName == StarterDecks.Pegasus.Name && StripV4(fromV2.Write()) == StripV4(v4.Write()),
                "형식 2의 런을 읽어 쓰면 지금 형식과 같음 (탑·칩 줄 빼고): " + FirstDifference(StripV4(v4.Write()), StripV4(fromV2.Write())));
        }

        /// <summary>덱 레시피: 저장 규칙, 편집기, 저장 파일, 레시피로 런 시작, 지금 덱을 레시피로.</summary>
        void DeckRecipes()
        {
            var save = GameSave.NewGame();
            var collection = save.Collection;
            Expect(save.Recipes.Select(r => r.Id).SequenceEqual(StarterDecks.All.Select(d => d.Id))
                   && save.Recipes.All(r => r.Name == StarterDecks.Find(r.Id).Name && r.Side.Count == 0 && r.Deck.SequenceEqual(StarterDecks.Find(r.Id).ToCardList())),
                "새 게임: 스타터 덱 4개가 레시피 (사이드 덱 없음)");
            Expect(save.Recipes.All(r => RecipeRules.StartProblems(r, _db, collection).Count == 0), "스타터 덱 레시피로 런을 시작할 수 있음");

            var yugi = save.FindRecipe("Yugi");
            bool IsExtra(uint c) => _db.TryGet(c, out var rec) && DeckRecipe.IsExtraDeckType(rec.Type);
            var yugiNames = new HashSet<uint>(yugi.Deck.Select(_db.NameCode));
            // 유희 편에 없는 카드명의 메인 덱 카드 (컬렉션에 있는 것, 카드명마다 하나)
            var outside = collection.Cards.Keys.Where(c => !IsExtra(c) && !yugiNames.Contains(_db.NameCode(c)))
                .GroupBy(_db.NameCode).Select(g => g.Min()).OrderBy(c => c).ToList();
            int yugiMain = yugi.Deck.Count(c => !IsExtra(c));
            Expect(outside.Count >= 20 && yugiMain >= 40 && yugiMain < 60, $"시험 준비: 유희 편 메인 {yugiMain}장, 바깥 카드명 {outside.Count}개");
            var mainOnly = yugi.Deck.Where(c => !IsExtra(c)).ToList();

            // ---- 저장 규칙 ----
            List<string> Refused(PlayerRecipe recipe, string what)
            {
                int before = save.Recipes.Count;
                var problems = save.SaveRecipe(recipe, _db);
                Expect(problems.Count > 0 && save.Recipes.Count == before && save.FindRecipe(recipe.Id) == null, $"저장 안 됨: {what}");
                return problems;
            }
            var p39 = Refused(new PlayerRecipe("deck-x", "짧은 덱", mainOnly.Take(39), null), "메인 39장");
            Expect(p39.Any(p => p.Contains("메인 덱 39장")), "메인 39장의 문제 글: " + string.Join(" / ", p39));
            var tooLong = mainOnly.Concat(outside.Take(61 - mainOnly.Count)).ToList();
            Expect(Refused(new PlayerRecipe("deck-x", "긴 덱", tooLong, null), "메인 61장").Any(p => p.Contains("메인 덱 61장")), "메인 61장의 문제 글");
            var three = yugi.Deck.Concat(new[] { DarkHole, DarkHole }).ToList();
            Expect(Refused(new PlayerRecipe("deck-x", "블랙홀 4장", three, new[] { DarkHole }), "블랙홀 덱 3장 + 사이드 1장")
                .Any(p => p.Contains("합쳐 같은 카드명 4장")), "덱 + 사이드 같은 카드명 4장의 문제 글");
            Refused(new PlayerRecipe("deck-x", "블랙홀 4장", yugi.Deck.Concat(new[] { DarkHole, DarkHole, DarkHole }), null), "덱에 블랙홀 4장");
            Refused(new PlayerRecipe("deck-x", "  ", yugi.Deck, null), "이름 없음");
            Refused(new PlayerRecipe("deck-x", new string('가', PlayerRecipe.MaxNameLength + 1), yugi.Deck, null), "이름이 너무 김");
            Refused(new PlayerRecipe("deck-x", "없는 카드", mainOnly.Take(40).Append(99999999u), null), "카드 DB에 없는 카드");

            // 컬렉션 초과·사이드 4장: 저장은 되고 런 시작은 안 된다.
            uint single = yugi.Deck.First(c => !IsExtra(c) && collection.Count(c) == 1);
            var overOwned = new PlayerRecipe(save.NewRecipeId(), "컬렉션 부족", yugi.Deck.Concat(new[] { single }), null);
            Expect(overOwned.Id == "deck-1" && save.SaveRecipe(overOwned, _db).Count == 0 && save.FindRecipe("deck-1") == overOwned,
                "컬렉션보다 많은 장수: 저장됨 (Id deck-1)");
            var overProblems = RecipeRules.StartOnlyProblems(overOwned, _db, collection);
            Expect(overProblems.Count == 1 && overProblems[0].Contains("컬렉션에는 1장"), "컬렉션 초과는 시작만 막음: " + string.Join(" / ", overProblems));
            Expect(RunState.CheckSetup(overOwned.ToSetup(1, StarterDecks.All), _db, collection).Count > 0, "런 시작 확인도 컬렉션 초과를 막음");
            bool threw = false;
            try
            {
                save.StartRun(overOwned, 1, StarterDecks.All, _db, _rarities);
            }
            catch (ArgumentException)
            {
                threw = true;
            }
            Expect(threw && save.Run == null, "컬렉션 초과 레시피로는 런을 시작할 수 없음");
            var sideFour = new PlayerRecipe(save.NewRecipeId(), "사이드 4장", yugi.Deck, outside.Take(4));
            Expect(sideFour.Id == "deck-2" && save.SaveRecipe(sideFour, _db).Count == 0, "시작 사이드 덱 4장: 저장됨");
            Expect(RecipeRules.StartOnlyProblems(sideFour, _db, collection).Any(p => p.Contains("사이드 덱 4장")), "시작 사이드 덱 4장은 시작만 막음");

            // 같은 Id로 저장하면 바꾼다, 지우기.
            var renamed = new PlayerRecipe("deck-2", "사이드 3장", yugi.Deck, outside.Take(3));
            Expect(save.SaveRecipe(renamed, _db).Count == 0 && save.Recipes.Count == 6 && save.Recipes[5] == renamed, "같은 Id로 저장하면 그 자리에서 바뀜");
            Expect(save.DeleteRecipe("Kaiba") && save.FindRecipe("Kaiba") == null && !save.DeleteRecipe("Kaiba") && save.Recipes.Count == 5, "레시피 지우기");
            Expect(save.NewRecipeId() == "deck-3", "다음 새 레시피 Id는 deck-3");

            // 저장 파일: 이름의 공백·#·=·%도 그대로 돌아온다.
            const string oddName = "내 덱 #1 = 100% 강함";
            var odd = new PlayerRecipe(save.NewRecipeId(), oddName, yugi.Deck, new[] { DarkHole });
            Expect(save.SaveRecipe(odd, _db).Count == 0, "이상한 글자가 든 이름 저장");
            var reloaded = GameSave.Parse(save.Write(), _db, _rarities);
            Expect(reloaded.Write() == save.Write(), "레시피 저장 다시 읽기: " + FirstDifference(save.Write(), reloaded.Write()));
            Expect(reloaded.Recipes.Select(r => (r.Id, r.Name)).SequenceEqual(save.Recipes.Select(r => (r.Id, r.Name)))
                   && reloaded.Recipes.Zip(save.Recipes, (a, b) => a.Deck.SequenceEqual(b.Deck) && a.Side.SequenceEqual(b.Side)).All(x => x),
                "레시피 Id·이름·덱·사이드 덱이 그대로");
            Expect(reloaded.FindRecipe(odd.Id).Name == oddName, "이름 " + reloaded.FindRecipe(odd.Id).Name);
            Expect(GameSave.Parse(save.Write().Replace("\nrecipe-side Yugi\n", "\n"), _db, _rarities).FindRecipe("Yugi").Side.Count == 0,
                "recipe-side 줄이 없으면 빈 사이드 덱");

            // 레시피로 런 시작: 덱·사이드, 런은 레시피 이름을 들고, 레시피를 지워도 이어진다.
            var run = save.StartRun(odd, 11, StarterDecks.All, _db, _rarities);
            Expect(run.StartDeckId == odd.Id && run.StartDeckName == oddName && save.LastStartDeckId == odd.Id
                   && run.MainDeck.Concat(run.ExtraDeck).OrderBy(c => c).SequenceEqual(odd.Deck.OrderBy(c => c)) && run.SideDeck.SequenceEqual(odd.Side),
                "레시피로 런 시작: 덱·사이드 덱·시작 덱 이름");
            Expect(run.Events[0].Describe(c => c.ToString()).Contains(oddName), "런 로그의 시작 줄에 레시피 이름: " + run.Events[0].Describe(c => c.ToString()));
            save.DeleteRecipe(odd.Id);
            Begin(run);
            run.FinishDuel(DuelResult.Win, 5000);
            Choose(run);
            var again = GameSave.Parse(save.Write(), _db, _rarities);
            Expect(again.Run != null && again.Run.StartDeckName == oddName && again.Run.Floor == run.Floor && again.FindRecipe(odd.Id) == null,
                "레시피를 지워도 런은 이어지고, 다시 읽어도 시작 덱 이름이 남음");
            Expect(again.Run.Events[0].Describe(c => c.ToString()) == run.Events[0].Describe(c => c.ToString()), "다시 읽은 런 시작 줄");

            // 지금 덱을 레시피로 (층 사이 화면): 메인·엑스트라 덱만, 사이드 덱은 비움.
            var fromRun = PlayerRecipe.FromRun(run, save.NewRecipeId(), "2층의 덱");
            Expect(save.SaveRecipe(fromRun, _db).Count == 0 && fromRun.Side.Count == 0
                   && fromRun.Deck.OrderBy(c => c).SequenceEqual(run.MainDeck.Concat(run.ExtraDeck).OrderBy(c => c)),
                "지금 덱을 레시피로 저장: 메인·엑스트라 덱");
            Expect(RecipeRules.StartProblems(fromRun, _db, collection).Count == 0, "런의 덱은 컬렉션 안이라 바로 시작할 수 있음");

            // ---- 편집기 ----
            var editor = new RecipeEditor(save.FindRecipe("Yugi"), _db, collection);
            Expect(!editor.IsDirty && editor.Id == "Yugi" && editor.MainCount == yugiMain, "연 레시피는 바뀌지 않은 상태");
            Expect(editor.Add(DarkHole, false) && editor.Add(DarkHole, true) && editor.NameCopies(DarkHole) == 3 && editor.Left(DarkHole) == 1 && editor.IsDirty,
                "블랙홀 덱 +1, 사이드 +1 (같은 카드명 3장, 컬렉션 1장 남음)");
            Expect(editor.AddReason(DarkHole, false) != null && !editor.Add(DarkHole, true) && editor.NameCopies(DarkHole) == 3,
                "같은 카드명 4장째는 덱에도 사이드 덱에도 넣을 수 없음");
            Expect(editor.Add(outside[0], true) && editor.Add(outside[1], true) && editor.Side.Count == 3 && editor.AddReason(outside[2], true) != null,
                "시작 사이드 덱 3장이 가득 차면 더 못 넣음");
            Expect(editor.MoveReason(mainOnly[0], true) != null && !editor.Move(mainOnly[0], true), "가득 찬 사이드 덱으로 옮기기 막힘");
            Expect(editor.Move(outside[0], false) && editor.Side.Count == 2 && editor.MainCount == yugiMain + 2, "사이드 덱 → 덱");
            Expect(editor.Move(mainOnly[0], true) && editor.Side.Count == 3 && editor.MainCount == yugiMain + 1, "덱 → 사이드 덱");
            Expect(editor.Remove(mainOnly[0], fromSide: true) && !editor.Remove(mainOnly[0], fromSide: true) && editor.Side.Count == 2, "사이드 덱에서 빼기");
            uint once = outside.First(c => collection.Count(c) == 1 && editor.NameCopies(c) == 0);
            Expect(editor.Add(once, false) && editor.Add(once, false) && editor.Left(once) == -1 && editor.ShortCards().SequenceEqual(new[] { once }),
                "컬렉션에 1장인 카드를 2장 넣을 수 있음 (컬렉션 부족으로 표시)");
            Expect(editor.StartOnlyProblems().Any(p => p.Contains("컬렉션에는 1장")) && editor.SaveProblems().Count == 0,
                "컬렉션보다 많이 넣으면 저장은 되고 시작만 막힘: " + string.Join(" / ", editor.StartOnlyProblems()));
            editor.Remove(once, false);
            while (editor.MainCount < RunRules.MaxMainDeck)
                Expect(editor.Add(outside.First(c => editor.NameCopies(c) < RunRules.MaxCopies), false), "60장까지 넣기");
            uint fresh = outside.Last(c => editor.NameCopies(c) == 0);
            Expect(editor.AddReason(fresh, false)?.Contains("60장") == true && editor.AddReason(fresh, true) == null, "메인 덱 60장이면 덱에는 못 넣고 사이드 덱에는 넣음");
            Expect(editor.SaveProblems().Count == 0, "60장 덱은 저장할 수 있음: " + string.Join(" / ", editor.SaveProblems()));
            while (editor.MainCount >= RunRules.MinMainDeck)
                editor.Remove(editor.MainDeck.Last(), false);
            Expect(editor.SaveProblems().Any(p => p.Contains("메인 덱 39장")), "편집 중에는 39장도 둘 수 있고, 저장 규칙이 알려 줌");
            editor.Name = "  ";
            Expect(editor.SaveProblems().Any(p => p.Contains("이름")), "이름을 지우면 저장 규칙이 알려 줌");

            var blank = new RecipeEditor(null, _db, collection);
            Expect(blank.Id == null && blank.IsDirty && blank.Deck.Count == 0 && blank.Name.Length > 0, "빈 새 레시피");
            foreach (uint code in mainOnly.Take(RunRules.MinMainDeck))
                blank.Add(code, false);
            var built = blank.Build(save.NewRecipeId());
            Expect(save.SaveRecipe(built, _db).Count == 0, "빈 레시피에 40장을 넣어 저장");
            blank.MarkSaved(built);
            Expect(!blank.IsDirty && blank.Id == built.Id, "저장한 뒤에는 바뀌지 않은 상태, Id가 정해짐");
            blank.Name = built.Name + " ";
            Expect(!blank.IsDirty, "이름 앞뒤 공백만 바꾼 것은 바뀐 것이 아님");
        }

        static string FirstDifference(string a, string b)
        {
            var x = a.Split('\n');
            var y = b.Split('\n');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                string p = i < x.Length ? x[i] : "(없음)", q = i < y.Length ? y[i] : "(없음)";
                if (p != q)
                    return $"{i + 1}번째 줄 \"{p}\" / \"{q}\"";
            }
            return "같음";
        }

        void SaveFiles()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DuelRoguelike-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var empty = GameSaveFile.Load(dir, _db, _rarities, out string notice);
                Expect(empty.Run == null && notice == null && empty.Collection.TotalCount == CardCollection.FromRecipes(StarterDecks.All).TotalCount,
                    "파일이 없으면 새 게임");

                var save = GameSave.NewGame();
                save.StartRun(new RunSetup { Seed = 11, StartDeck = StarterDecks.Joey }, _db, _rarities);
                GameSaveFile.Save(save, dir);
                Expect(!File.Exists(GameSaveFile.BackupPath(dir)), "처음 저장에는 .bak이 없음");
                Begin(save.Run);
                save.Run.FinishDuel(DuelResult.Win, 7000);
                GameSaveFile.Save(save, dir);
                Expect(File.Exists(GameSaveFile.BackupPath(dir)) && !File.Exists(GameSaveFile.FilePath(dir) + ".tmp"), "두 번째 저장: 이전 파일은 .bak, 임시 파일은 남지 않음");
                var loaded = GameSaveFile.Load(dir, _db, _rarities, out notice);
                Expect(notice == null && loaded.Write() == save.Write(), "저장한 파일 읽기");

                // 깨진 파일 + 읽을 수 있는 .bak → .bak으로 이어 감 (한 걸음 전)
                File.WriteAllText(GameSaveFile.FilePath(dir), "format 2\nfloor 이층\n");
                loaded = GameSaveFile.Load(dir, _db, _rarities, out notice);
                Expect(notice != null && loaded.Run != null && loaded.Run.DuelsPlayed == 0, $"깨진 파일이면 .bak으로 ({notice})");
                Expect(Directory.GetFiles(dir, "save-broken-*.txt").Length == 1 && !File.Exists(GameSaveFile.FilePath(dir)), "깨진 파일은 save-broken-*.txt로 옮김");

                // 깨진 파일 + .bak도 깨짐 → 새 게임
                File.WriteAllText(GameSaveFile.FilePath(dir), "엉망");
                File.WriteAllText(GameSaveFile.BackupPath(dir), "엉망");
                loaded = GameSaveFile.Load(dir, _db, _rarities, out notice);
                Expect(notice != null && loaded.Run == null && Directory.GetFiles(dir, "save-broken-*.txt").Length == 2, "둘 다 깨졌으면 새 게임");
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }
    }
}
