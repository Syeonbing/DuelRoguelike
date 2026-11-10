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
                         ("봉인 장수·LP·층 진행", test.ScriptedRun),
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

        // --- 정해 둔 결과로 런 진행 ---

        RunState NewRun(DeckRecipe deck, ulong seed = 42, IReadOnlyList<uint> side = null, RarityTable rarities = null,
            CardCollection collection = null) =>
            RunState.Start(new RunSetup { Seed = seed, StartDeck = deck, SideDeck = side ?? Array.Empty<uint>() },
                _db, rarities ?? _rarities, collection ?? CardCollection.FromRecipes(StarterDecks.All));

        /// <summary>승리 보상을 고르는 중이면 그 자리를 고른다.</summary>
        static void Choose(RunState run, int slot = 0)
        {
            if (run.Phase == RunPhase.ChoosingReward)
                run.ChooseReward(slot);
        }

        /// <summary>보상 고르기·보충이 남았으면 끝내고 (보충은 첫 후보), 듀얼 하나를 정한 결과로 끝낸다. 이기면 rewardSlot 자리를 고른다.</summary>
        static RunDuel Play(RunState run, DuelResult result, int lp, int rewardSlot = 0)
        {
            Choose(run, rewardSlot);
            while (run.Phase == RunPhase.NeedsRefill)
                run.MoveToDeck(run.RefillCandidates()[0]);
            var duel = run.BeginDuel();
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

            // 1층 2연패: 1장, 2장 봉인. LP는 8000, 층은 그대로.
            int mark = run.Events.Count;
            Play(run, DuelResult.Loss, 0);
            Expect(run.LosingStreak == 1 && run.Floor == 1 && run.Lp == 8000 && run.MainDeck.Count == 49, $"1패 뒤: 연패 {run.LosingStreak}, {run.Floor}층, LP {run.Lp}, 메인 {run.MainDeck.Count}");
            Expect(CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main) == 1 && run.SealedMain.Count == 1, "1층 1연패는 1장 봉인");
            mark = run.Events.Count;
            Play(run, DuelResult.Draw, 0);
            Expect(run.LosingStreak == 2 && run.Draws == 1 && run.MainDeck.Count == 47, $"무승부는 패배로: 연패 {run.LosingStreak}, 메인 {run.MainDeck.Count}");
            Expect(CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main) == 2, "1층 2연패는 2장 봉인");

            // 승리: 연패 0, 다음 층, 남은 LP + 1000, 보상 1개 (후보 또는 골드 대체).
            mark = run.Events.Count;
            run.BeginDuel();
            run.FinishDuel(DuelResult.Win, 3000);
            Expect(run.LosingStreak == 0 && run.Floor == 2 && run.Lp == 4000, $"승리 뒤: 연패 {run.LosingStreak}, {run.Floor}층, LP {run.Lp} (3000 + 1000)");
            Expect(HasNew(run, mark, e => e.Kind == RunEventKind.DuelWon && e.Lp == 3000) && HasNew(run, mark, e => IsLp(e, LpRestoreReason.Win, 3000, 4000)),
                "승리 이벤트(남은 LP 3000)와 LP +1000 이벤트");
            Expect(CountNew(run, mark, RunEventKind.RewardOffered) + CountNew(run, mark, RunEventKind.RewardSubstituted) == 1, "승리 보상 1개");
            Choose(run);
            var duel = run.BeginDuel();
            Expect(duel.PlayerLp == 4000 && duel.CreateSessionOptions().Team1.StartingLP == 4000, "다음 층은 남은 LP + 1000으로 시작");
            run.FinishDuel(DuelResult.Win, 2000);
            Choose(run);

            // 9층까지: 남은 LP 1000으로 이기면 다음 층은 2000으로 시작한다 (보스 층 말고는 맞추지 않는다).
            while (run.Floor < 10)
            {
                int floor = run.Floor;
                duel = run.BeginDuel();
                Expect(duel.PlayerLp == (floor == 3 ? 3000 : 2000), $"{floor}층 시작 LP {duel.PlayerLp}");
                run.FinishDuel(DuelResult.Win, 1000);
                Choose(run);
            }

            // 10층(보스 층): 시작 직전에 8000으로 맞추고, 이기면 남은 LP와 상관없이 8000.
            mark = run.Events.Count;
            duel = run.BeginDuel();
            Expect(duel.PlayerLp == 8000 && run.Lp == 8000, $"10층 직전 LP 맞춤: LP {duel.PlayerLp}");
            Expect(HasNew(run, mark, e => IsLp(e, LpRestoreReason.BossFloor, 2000, 8000)), "10층 LP 맞춤 이벤트");
            mark = run.Events.Count;
            run.FinishDuel(DuelResult.Win, 2500);
            Expect(run.Floor == 11 && run.Lp == 8000, $"10층 승리 뒤 8000: {run.Floor}층 LP {run.Lp}");
            Expect(HasNew(run, mark, e => IsLp(e, LpRestoreReason.BossCleared, 2500, 8000)) && CountNew(run, mark, RunEventKind.LpRestored) == 1,
                "보스 격파 LP 이벤트 (+1000은 없음)");
            Choose(run);

            // 11층: 7500으로 이기면 8500 (8000을 넘을 수 있다). 12층은 8500으로 시작.
            Expect(run.BeginDuel().PlayerLp == 8000, "11층은 8000 그대로");
            run.FinishDuel(DuelResult.Win, 7500);
            Choose(run);
            Expect(run.Lp == 8500, $"승리 LP는 8000을 넘을 수 있음: {run.Lp}");
            Expect(run.BeginDuel().PlayerLp == 8500, "12층은 8500으로 시작");

            // 12층 패배: 1 + 12÷10 = 2장, LP 8000으로 회복.
            mark = run.Events.Count;
            run.FinishDuel(DuelResult.Loss, 0);
            Expect(CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main) == 2 && run.Lp == 8000, "12층 1연패는 2장 봉인, LP 회복");

            // 20층(보스 층) 직전: LP가 8000보다 높아도 8000으로 맞춘다 (9층 휴식이 생길 때까지 임시).
            while (run.Floor < 20)
                Play(run, DuelResult.Win, 7500);
            Expect(run.Lp == 8500, $"19층을 7500으로 이긴 LP {run.Lp}");
            mark = run.Events.Count;
            Expect(run.BeginDuel().PlayerLp == 8000 && HasNew(run, mark, e => IsLp(e, LpRestoreReason.BossFloor, 8500, 8000)),
                "8000보다 높아도 보스 층 직전에는 8000");
            mark = run.Events.Count;
            run.FinishDuel(DuelResult.Win, 8000);
            Expect(run.Lp == 8000 && CountNew(run, mark, RunEventKind.LpRestored) == 0, "보스 층을 LP 8000으로 이기면 LP 이벤트 없음");
            Choose(run);

            // 25층 3연패: 3, 4, 5장 (규칙 문서 예: 25층 3연패째 5장).
            while (run.Floor < 25)
                Play(run, DuelResult.Win, 5000);
            for (int streak = 1; streak <= 3; streak++)
            {
                if (run.Phase == RunPhase.Ended)
                    break;
                mark = run.Events.Count;
                int mainBefore = run.Phase == RunPhase.NeedsRefill ? 40 : run.MainDeck.Count;
                Play(run, DuelResult.Loss, 0);
                int sealedCount = CountNew(run, mark, RunEventKind.CardSealed, RunPile.Main);
                Expect(sealedCount == Math.Min(streak + 2, mainBefore), $"25층 {streak}연패: {sealedCount}장 봉인 (기대 {streak + 2}장)");
            }
            Expect(RunRules.MainSealCount(3, 25) == 5 && RunRules.MainSealCount(1, 9) == 1 && RunRules.MainSealCount(1, 10) == 2,
                "봉인 장수 공식");
        }

        void EndAndRefill()
        {
            var forty = new DeckRecipe("Test40", "시험 덱 40장", StarterDecks.Yugi.Entries.Take(40).ToList());
            var spare = StarterDecks.Yugi.Entries.Skip(40).Take(3).Select(e => e.Code).ToList();

            // 사이드 덱이 비어 있으면 첫 패배로 런 종료.
            var run = NewRun(forty);
            Play(run, DuelResult.Loss, 0);
            Expect(run.Phase == RunPhase.Ended && run.EndReason == RunEndReason.DeckBelowMinimum && run.Events.Last().Kind == RunEventKind.RunEnded,
                $"보충할 카드가 없으면 런 종료 ({run.Phase})");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "끝난 런에서 듀얼 시작");

            // 사이드 덱이 있으면 보충할 때까지 듀얼을 시작할 수 없고, 40장이 되면 Ready.
            run = NewRun(forty, side: spare);
            Play(run, DuelResult.Loss, 0);
            Expect(run.Phase == RunPhase.NeedsRefill && run.RefillNeeded == 1 && run.RefillCandidates().Count == 3,
                $"보충 필요: {run.Phase}, {run.RefillNeeded}장, 후보 {run.RefillCandidates().Count}");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "보충 전에 듀얼 시작");
            Expect(!run.CanMoveToSide(run.MainDeck[0], out _), "보충 중에는 사이드 덱으로 옮길 수 없음");
            run.MoveToDeck(spare[1]);
            Expect(run.Phase == RunPhase.Ready && run.MainDeck.Count == 40 && run.SideDeck.Count == 2
                   && run.Events.Last().Kind == RunEventKind.MovedToDeck && run.Events.Last().IsRefill, "보충 뒤 Ready");

            // 2연패(2장)인데 사이드 덱 2장으로 채울 수 있으면 보충, 1장 모자라면 종료.
            Play(run, DuelResult.Loss, 0);
            Expect(run.Phase == RunPhase.NeedsRefill && run.RefillNeeded == 2, $"2장 보충 필요: {run.Phase} {run.RefillNeeded}");
            run.MoveToDeck(run.RefillCandidates()[0]);
            Expect(run.Phase == RunPhase.NeedsRefill && run.RefillNeeded == 1, "1장 보충 뒤 아직 1장 더");
            run.MoveToDeck(run.RefillCandidates()[0]);
            Expect(run.Phase == RunPhase.Ready && run.SideDeck.Count == 0, "2장 보충 뒤 Ready");
            Play(run, DuelResult.Loss, 0);
            Expect(run.Phase == RunPhase.Ended, "3연패, 사이드 덱이 비어 런 종료");

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
            run.BeginDuel();
            Expect(!run.CanMoveToSide(run.MainDeck[0], out _) && !run.CanMoveToDeck(fusion, out _), "듀얼 중 교환");
            ExpectThrows<InvalidOperationException>(() => run.MoveToSide(run.MainDeck[0]), "듀얼 중 교환 예외");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "듀얼 중 다시 시작");
            ExpectThrows<ArgumentOutOfRangeException>(() => run.FinishDuel(DuelResult.Win, 0), "LP 0으로 승리");
            run.FinishDuel(DuelResult.Win, 100);

            // 보충 중에는 엑스트라 덱 카드를 넣을 수 없다.
            var joey40 = new DeckRecipe("Joey40", "조이 편 40장 + 엑스트라 1장",
                StarterDecks.Joey.Entries.Where(e => !IsExtra(e.Code)).Take(40).Append(StarterDecks.Joey.Entries.First(e => IsExtra(e.Code))).ToList());
            uint joeyFusion = StarterDecks.Joey.Entries.First(e => IsExtra(e.Code)).Code;
            uint joeySpare = StarterDecks.Joey.Entries.Where(e => !IsExtra(e.Code)).Skip(40).First().Code;
            run = NewRun(joey40, side: new[] { joeySpare });
            run.MoveToSide(joeyFusion); // 엑스트라 덱을 비워 봉인은 메인 덱에서만
            Play(run, DuelResult.Loss, 0);
            if (run.Phase == RunPhase.NeedsRefill)
            {
                Expect(!run.CanMoveToDeck(joeyFusion, out _), "보충 중 엑스트라 덱 카드");
                Expect(run.RefillCandidates().SequenceEqual(new[] { joeySpare }), "보충 후보는 메인 덱 카드만");
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
                run.BeginDuel();
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
                   && offered.Floor == run.Floor - 1, "보상 후보 이벤트 (이긴 층)");
            Expect(run.Phase == RunPhase.ChoosingReward && run.Floor == offer.Floor + 1, "고르기 전에도 층은 이미 올라감");
            ExpectThrows<InvalidOperationException>(() => run.BeginDuel(), "보상을 고르기 전에 듀얼 시작");
            Expect(!run.CanMoveToSide(run.MainDeck[0], out string why) && why.Contains("보상"), $"보상을 고르기 전에 교환 ({why})");
            ExpectThrows<ArgumentOutOfRangeException>(() => run.ChooseReward(RunRules.RewardChoices), "없는 자리");
            ExpectThrows<ArgumentOutOfRangeException>(() => run.ChooseReward(-1), "음수 자리");

            // 빈 자리를 고르면 골드.
            int gold = run.Gold;
            int side = run.SideDeck.Count;
            run.ChooseReward(1);
            Expect(run.Phase == RunPhase.Ready && run.PendingReward == null && run.Gold == gold + offer.Gold && run.SideDeck.Count == side
                   && run.Events.Last().Kind == RunEventKind.RewardGold && run.Events.Last().Count == offer.Gold, "빈 자리 → 골드");
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
                   && run.Events.Last().Kind == RunEventKind.RewardGained && run.Events.Last().Code == DarkHole, "카드 자리 → 사이드 덱과 컬렉션");
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
            Expect(offered.Count + substituted.Count == 40, "승리 40번, 보상 40개");
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
            run.BeginDuel();
            ExpectThrows<InvalidOperationException>(() => run.Abandon(), "듀얼 중 포기");
            run.FinishDuel(DuelResult.Win, 4000);
            Expect(run.Phase == RunPhase.ChoosingReward, $"1층 승리 뒤 보상 고르기 ({run.Phase})");
            int events = run.Events.Count;
            save.AbandonRun();
            Expect(run.Phase == RunPhase.Ended && run.EndReason == RunEndReason.Abandoned && run.Events.Count == events + 1 && run.PendingReward == null
                   && run.Events.Last().Kind == RunEventKind.RunEnded && run.Events.Last().Describe(c => "") == "런 종료: 포기",
                $"보상을 고르는 중에도 포기하면 런 종료 ({run.Phase}, {run.EndReason})");
            Expect(save.Run == null, "포기한 런은 저장에서 빠짐");
            int gained = run.Events.Count(e => e.Kind == RunEventKind.RewardGained);
            Expect(save.Collection.TotalCount == CardCollection.FromRecipes(StarterDecks.All).TotalCount + gained, "포기해도 얻은 카드는 컬렉션에 남음");
            Expect(save.Gold == run.Gold, "포기한 런의 골드도 누적에 더함");
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
                    run.BeginDuel();
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
            endRun.BeginDuel();
            endRun.FinishDuel(DuelResult.Loss, 0);
            Expect(endRun.Phase == RunPhase.Ended, "40장 덱 첫 패배로 런 종료");
            Expect(RoundTrip(ended) == ended.Write(), "끝난 런도 저장·읽기 (스타터 덱이 아닌 시작 덱 Id도 그대로)");
            int before = ended.Gold;
            Expect(ended.CloseEndedRun() && ended.Run == null && ended.Gold == before + endRun.Gold, "끝난 런 정리: 골드 누적, 런 빠짐");
            Expect(GameSave.Parse(ended.Write(), _db, _rarities).Gold == ended.Gold, "골드 저장·읽기");
        }

        /// <summary>듀얼 중(InDuel)으로 저장된 런을 읽으면 그 듀얼은 LP 0으로 진 것으로 처리된다.</summary>
        void ForfeitOnLoad()
        {
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = 21, StartDeck = StarterDecks.Kaiba }, _db, _rarities);
            run.BeginDuel();
            run.FinishDuel(DuelResult.Win, 7600);
            Choose(run);
            Expect(run.Lp == 8600, $"1층을 7600으로 이겨 LP {run.Lp}");
            var duel = run.BeginDuel();
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
                   && r.Events.Count(e => e.Kind == RunEventKind.CardSealed && e.DuelNumber == 2 && e.Pile == RunPile.Main) == 1,
                "DuelForfeited 이벤트 1개, 2층 1연패 메인 덱 봉인 1장");

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
            first.StartRun(new RunSetup { Seed = 22, StartDeck = StarterDecks.Joey }, _db, _rarities).BeginDuel();
            var firstLoaded = GameSave.Parse(first.Write(), _db, _rarities, "시험");
            Expect(firstLoaded.ForfeitedDuel == 1 && firstLoaded.Run.Losses == 1 && firstLoaded.Run.Floor == 1 && firstLoaded.Run.Phase == RunPhase.Ready,
                "첫 듀얼 중에 꺼짐");
        }

        void Broken(string text, string what, RarityTable rarities = null) =>
            ExpectThrows<FormatException>(() => GameSave.Parse(text, _db, rarities ?? _rarities), what);

        void SaveFormatErrors()
        {
            var save = GameSave.NewGame();
            var run = save.StartRun(new RunSetup { Seed = 3, StartDeck = StarterDecks.Pegasus }, _db, _rarities);
            run.BeginDuel();
            run.FinishDuel(DuelResult.Win, 6000);
            string choosing = run.Phase == RunPhase.ChoosingReward ? save.Write() : null;
            Choose(run);
            string good = save.Write();
            Expect(GameSave.Parse(good, _db, _rarities).Run.Floor == 2, "정상 파일 읽기");
            Expect(GameSave.Parse(good.Replace("\n", "\r\n"), _db, _rarities).Write() == good, "CRLF 줄바꿈도 읽음");

            string Replace(string line, string with) => good.Replace("\n" + line + "\n", "\n" + with + "\n");
            Broken("", "빈 파일");
            Broken(good.Replace("format 3\n", ""), "format 줄 없음");
            Broken(good.Replace("format 3\n", "format 4\n"), "새 버전 형식");
            Broken(good.Replace("format 3\n", "format 3\nunknown 3\n"), "모르는 키");
            Broken(good.Replace("format 3\n", "format 3\nformat 3\n"), "같은 키 두 번");
            Broken(good.Replace("\nend-run\n", "\n"), "end-run 없음");
            Broken(Replace("floor 2", "floor 0"), "0층");
            Broken(Replace("floor 2", "floor 이층"), "층이 숫자가 아님");
            Broken(Replace("phase Ready", "phase ChoosingReward"), "보상을 고르는 중인데 reward 줄 없음");
            Broken(Replace("phase Ready", "phase 1"), "상태를 숫자로");
            Broken(Replace("phase Ready", "phase Ended"), "끝났는데 종료 이유 없음");
            Broken(Replace("wins 1", "wins 2"), "전적이 듀얼 수와 다름");
            Broken(Replace("opponents Yugi Kaiba Joey Pegasus", "opponents Yugi Nobody"), "모르는 상대 덱");
            Broken(Replace("seed 3", ""), "seed 줄 없음");
            Broken(good.Replace("\nsealed-main\n", "\nsealed-main 99999999\n"), "카드 DB에 없는 카드");
            Broken(good.Replace("\nsealed-main\n", "\nfainted-main\n"), "형식 3에 fainted-main");
            Broken(good.Replace("event DuelWon duel=1", "event DuelWon duel=x"), "이벤트 칸 숫자 아님");
            Broken(good.Replace("event DuelWon duel=1", "event DuelWon wins=1 duel=1"), "모르는 이벤트 칸");
            Broken(good.Replace("event DuelWon duel=1 floor=1", "event DuelWon duel=1"), "이벤트에 floor 없음");
            Broken(good.Replace("format 3\ngold 0\n", "format 3\ngold -1\n"), "골드 음수");
            Broken(good.Replace("\nrecipe Kaiba ", "\nrecipe Yugi "), "레시피 Id 두 번");
            Broken(good.Replace("\nrecipe-side Joey", "\nrecipe-side Nobody"), "recipe 줄에 없는 레시피의 사이드 덱");
            Broken(Regex.Replace(good, @"\nrecipe-deck Pegasus [^\n]*", ""), "recipe-deck 줄 없음");
            Broken(good.Replace("\nrecipe-side Joey", "\nrecipe-side Joey\nrecipe-side Joey"), "recipe-side 두 번");
            Broken(good.Replace("\nrecipe Yugi ", "\nrecipe Yugi %2 "), "레시피 줄 칸 수");
            Broken(good.Replace("\nrecipe Yugi ", "\nrecipe Yugi %zz"), "이름의 % 표기 틀림");
            Broken(good.Replace("\nrecipe-deck Yugi ", "\nrecipe-deck Yugi 99999x "), "레시피 카드 코드가 숫자 아님");
            Broken("format 3\nrecipe-deck a 53129443\n", "recipe 없이 recipe-deck");
            Broken(good.Replace("format 3\n", "format 3\nlast-side-deck Yugi\n"), "형식 3에 last-side-deck");
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
        }

        /// <summary>형식 1(2026-11-10 규칙 전) 저장 파일: 포인트는 골드로, 기절은 봉인으로 바꿔 읽는다.</summary>
        void SaveFormatV1()
        {
            var profile = GameSave.Parse("format 1\npoints 1 2 0 0 1\nlast-start-deck Yugi\n", _db, _rarities);
            int expectedGold = RunRules.GoldFor(Rarity.N) + 2 * RunRules.GoldFor(Rarity.R) + RunRules.GoldFor(Rarity.FR);
            Expect(profile.Gold == expectedGold && profile.LastStartDeckId == "Yugi", $"포인트 → 골드 {profile.Gold} (기대 {expectedGold})");
            Expect(profile.Write().StartsWith("# ", StringComparison.Ordinal) && profile.Write().Contains("\nformat 3\n") && !profile.Write().Contains("points")
                   && profile.Recipes.Count == StarterDecks.All.Count, "형식 1을 읽어 쓰면 형식 3 (스타터 덱 레시피 4개)");
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
            string v3 = save.Write();

            // 같은 런을 형식 1 글로 바꾼다.
            var substituted = new int[RarityTable.RarityCount];
            foreach (var e in run.Events.Where(e => e.Kind == RunEventKind.RewardSubstituted))
                substituted[(int)e.Rarity]++;
            // 형식 3에 새로 생긴 줄(레시피, 시작 덱 이름)과 이벤트 칸(deck-name)은 형식 1에 없다.
            string v1 = Regex.Replace(v3, @"\n(recipe|recipe-deck|recipe-side|start-deck-name) [^\n]*", "");
            v1 = Regex.Replace(v1, @" deck-name=[^ \n]+", "")
                .Replace("\nformat 3\ngold 0\n", "\nformat 1\npoints 0 0 0 0 0\n")
                .Replace($"\ngold {run.Gold}\n", $"\nsubstituted {string.Join(" ", substituted)}\n")
                .Replace("\nsealed-main", "\nfainted-main")
                .Replace("\nsealed-extra", "\nfainted-extra")
                .Replace("event CardSealed ", "event CardFainted ");
            v1 = Regex.Replace(v1, @"(event RewardSubstituted [^\n]*?) count=\d+", "$1");
            Expect(v1.Contains("\nformat 1\n") && v1.Contains("\nsubstituted ") && v1.Contains("event CardFainted ")
                   && !Regex.IsMatch(v1, @"event RewardSubstituted [^\n]*count="), "형식 1 글 만들기");
            var fromV1 = GameSave.Parse(v1, _db, allFr, "시험");
            Expect(fromV1.Write() == v3, "형식 1을 읽어 형식 3으로 쓰면 원래 글과 같음: " + FirstDifference(v3, fromV1.Write()));
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
            var v3 = GameSave.NewGame();
            v3.StartRun(new RunSetup { Seed = 8, StartDeck = StarterDecks.Pegasus }, _db, _rarities);
            string v2Text = Regex.Replace(v3.Write(), @"\n(recipe|recipe-deck|recipe-side|start-deck-name) [^\n]*", "");
            v2Text = Regex.Replace(v2Text, @" deck-name=[^ \n]+", "").Replace("\nformat 3\n", "\nformat 2\n");
            var fromV2 = GameSave.Parse(v2Text, _db, _rarities);
            Expect(fromV2.Run.StartDeckName == StarterDecks.Pegasus.Name && fromV2.Write() == v3.Write(),
                "형식 2의 런을 읽어 쓰면 형식 3과 같음: " + FirstDifference(v3.Write(), fromV2.Write()));
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
            run.BeginDuel();
            run.FinishDuel(DuelResult.Win, 5000);
            Choose(run);
            var again = GameSave.Parse(save.Write(), _db, _rarities);
            Expect(again.Run != null && again.Run.StartDeckName == oddName && again.Run.Floor == 2 && again.FindRecipe(odd.Id) == null,
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
                save.Run.BeginDuel();
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
