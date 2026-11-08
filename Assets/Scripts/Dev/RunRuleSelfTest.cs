// 엔진 없이 런 규칙만 시험한다: 듀얼 결과를 정해 두고 런을 진행시켜 기대값과 비교한다 (tools/headless-run.ps1 첫 단계).
// 확률표·연쇄 확률은 난수를 많이 뽑아 비율을 본다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Run;

namespace DuelRoguelike.Dev
{
    public sealed class RunRuleSelfTest
    {
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
                         ("엑스트라 덱 연쇄 기절", test.ExtraFaintChain),
                         ("기절 장수·LP·층 진행", test.ScriptedRun),
                         ("런 종료와 보충", test.EndAndRefill),
                         ("사이드 덱 교환 규칙", test.ExchangeRules),
                         ("보상 대체와 등급 표 바꾸기", test.RewardSubstitutionAndTableChange),
                         ("같은 시드 재현", test.Determinism),
                         ("런 시작 확인", test.SetupChecks),
                         ("등급 파일 읽기", test.RarityParsing),
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

        void ExtraFaintChain()
        {
            const int trials = 200000;
            var random = new RunRandom(99);
            int atLeast1 = 0, atLeast2 = 0, atLeast3 = 0, total = 0;
            for (int i = 0; i < trials; i++)
            {
                int n = RunRules.RollExtraFaintCount(random, 15);
                total += n;
                if (n >= 1) atLeast1++;
                if (n >= 2) atLeast2++;
                if (n >= 3) atLeast3++;
            }
            Expect(Math.Abs(atLeast1 * 100.0 / trials - 20) < 0.5, $"엑스트라 1장 이상 {atLeast1 * 100.0 / trials:F2}% (20%)");
            Expect(Math.Abs(atLeast2 * 100.0 / trials - 4) < 0.25, $"엑스트라 2장 이상 {atLeast2 * 100.0 / trials:F2}% (4%)");
            Expect(Math.Abs(atLeast3 * 100.0 / trials - 0.8) < 0.1, $"엑스트라 3장 이상 {atLeast3 * 100.0 / trials:F2}% (0.8%)");
            Expect(Math.Abs(total / (double)trials - 0.25) < 0.01, $"엑스트라 평균 {total / (double)trials:F3}장 (0.25장)");
            Expect(Enumerable.Range(0, 10000).All(_ => RunRules.RollExtraFaintCount(random, 1) <= 1), "엑스트라 덱보다 많이 기절하지 않음");
            Expect(RunRules.RollExtraFaintCount(random, 0) == 0, "엑스트라 덱이 비면 0장");
        }

        // --- 정해 둔 결과로 런 진행 ---

        RunState NewRun(DeckRecipe deck, ulong seed = 42, IReadOnlyList<uint> side = null, RarityTable rarities = null) =>
            RunState.Start(new RunSetup { Seed = seed, StartDeck = deck, SideDeck = side ?? Array.Empty<uint>() },
                _db, rarities ?? _rarities, CardCollection.FromRecipes(StarterDecks.All));

        /// <summary>보충이 필요하면 첫 후보로 채우고, 듀얼 하나를 정한 결과로 끝낸다.</summary>
        static RunDuel Play(RunState run, DuelResult result, int lp)
        {
            while (run.Phase == RunPhase.NeedsRefill)
                run.MoveToDeck(run.RefillCandidates()[0]);
            var duel = run.BeginDuel();
            run.FinishDuel(result, lp);
            return duel;
        }

        static int CountNew(RunState run, int from, RunEventKind kind, RunPile? pile = null) =>
            run.Events.Skip(from).Count(e => e.Kind == kind && (pile == null || e.Pile == pile));

        void ScriptedRun()
        {
            var run = NewRun(StarterDecks.Yugi);
            Expect(run.MainDeck.Count == 50 && run.ExtraDeck.Count == 0 && run.SideDeck.Count == 0, "유희 편 시작 장수");

            // 1층 2연패: 1장, 2장 기절. LP는 8000, 층은 그대로.
            int mark = run.Events.Count;
            Play(run, DuelResult.Loss, 0);
            Expect(run.LosingStreak == 1 && run.Floor == 1 && run.Lp == 8000 && run.MainDeck.Count == 49, $"1패 뒤: 연패 {run.LosingStreak}, {run.Floor}층, LP {run.Lp}, 메인 {run.MainDeck.Count}");
            Expect(CountNew(run, mark, RunEventKind.CardFainted, RunPile.Main) == 1, "1층 1연패는 1장 기절");
            mark = run.Events.Count;
            Play(run, DuelResult.Draw, 0);
            Expect(run.LosingStreak == 2 && run.Draws == 1 && run.MainDeck.Count == 47, $"무승부는 패배로: 연패 {run.LosingStreak}, 메인 {run.MainDeck.Count}");
            Expect(CountNew(run, mark, RunEventKind.CardFainted, RunPile.Main) == 2, "1층 2연패는 2장 기절");

            // 승리: 연패 0, 다음 층, LP 이어 가기, 보상 1개.
            mark = run.Events.Count;
            Play(run, DuelResult.Win, 3000);
            Expect(run.LosingStreak == 0 && run.Floor == 2 && run.Lp == 3000, $"승리 뒤: 연패 {run.LosingStreak}, {run.Floor}층, LP {run.Lp}");
            Expect(CountNew(run, mark, RunEventKind.RewardGained) + CountNew(run, mark, RunEventKind.RewardSubstituted) == 1, "승리 보상 1개");
            var duel = run.BeginDuel();
            Expect(duel.PlayerLp == 3000 && duel.CreateSessionOptions().Team1.StartingLP == 3000, "다음 층은 남은 LP로 시작");
            run.FinishDuel(DuelResult.Win, 2000);

            // 9층까지 이기고 10층(보스 층) 직전에 회복. 4~9층은 회복하지 않고 남은 LP로 시작한다.
            while (run.Floor < 10)
            {
                int floor = run.Floor;
                duel = run.BeginDuel();
                Expect(duel.PlayerLp == (floor == 3 ? 2000 : 1000), $"{floor}층은 회복하지 않음 (LP {duel.PlayerLp})");
                run.FinishDuel(DuelResult.Win, 1000);
            }
            mark = run.Events.Count;
            duel = run.BeginDuel();
            Expect(duel.PlayerLp == 8000 && run.Lp == 8000, $"10층 직전 회복: LP {duel.PlayerLp}");
            Expect(run.Events.Skip(mark).Any(e => e.Kind == RunEventKind.LpRestored && e.LpReason == LpRestoreReason.BossFloor && e.PreviousLp == 1000),
                "10층 회복 이벤트");
            run.FinishDuel(DuelResult.Win, 2500);
            Expect(run.Floor == 11 && run.Lp == 2500, $"10층 승리 뒤 LP 이어 가기: {run.Floor}층 LP {run.Lp}");
            Expect(run.BeginDuel().PlayerLp == 2500, "11층은 회복하지 않음");

            // 11층 패배: 1 + 11÷10 = 2장, LP 회복.
            mark = run.Events.Count;
            run.FinishDuel(DuelResult.Loss, 0);
            Expect(CountNew(run, mark, RunEventKind.CardFainted, RunPile.Main) == 2 && run.Lp == 8000, "11층 1연패는 2장 기절, LP 회복");

            // 20층 직전 LP가 8000보다 높으면 그대로.
            while (run.Floor < 20)
                Play(run, DuelResult.Win, 9000);
            mark = run.Events.Count;
            Expect(run.BeginDuel().PlayerLp == 9000 && CountNew(run, mark, RunEventKind.LpRestored) == 0, "8000을 넘은 LP는 보스 층에서 낮추지 않음");
            run.FinishDuel(DuelResult.Win, 9000);

            // 25층 3연패: 3, 4, 5장 (설계 문서 예: 25층 3연패째 5장).
            while (run.Floor < 25)
                Play(run, DuelResult.Win, 5000);
            for (int streak = 1; streak <= 3; streak++)
            {
                if (run.Phase == RunPhase.Ended)
                    break;
                mark = run.Events.Count;
                int mainBefore = run.Phase == RunPhase.NeedsRefill ? 40 : run.MainDeck.Count;
                Play(run, DuelResult.Loss, 0);
                int fainted = CountNew(run, mark, RunEventKind.CardFainted, RunPile.Main);
                Expect(fainted == Math.Min(streak + 2, mainBefore), $"25층 {streak}연패: {fainted}장 기절 (기대 {streak + 2}장)");
            }
            Expect(RunRules.MainFaintCount(3, 25) == 5 && RunRules.MainFaintCount(1, 9) == 1 && RunRules.MainFaintCount(1, 10) == 2,
                "기절 장수 공식");
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

            // 기절 장수가 메인 덱보다 많으면 전부 기절 (여기서는 40장 런을 많이 지게 하지 않고 공식만 본다).
            Expect(RunRules.MainFaintCount(50, 1) > 40, "기절 장수에 상한 없음");
        }

        void ExchangeRules()
        {
            uint darkHole = 53129443; // 블랙홀: 4덱에 1장씩이라 컬렉션에 4장
            var kaibaOnly = StarterDecks.Kaiba.Entries.Select(e => e.Code).Where(c => StarterDecks.Yugi.Entries.All(y => y.Code != c)).Take(11).ToList();
            var run = NewRun(StarterDecks.Yugi, side: kaibaOnly.Take(10).ToList());

            foreach (uint code in kaibaOnly.Take(10))
                run.MoveToDeck(code);
            Expect(run.MainDeck.Count == 60 && run.SideDeck.Count == 0, $"메인 덱 60장까지 넣기 ({run.MainDeck.Count})");
            run.MoveToSide(kaibaOnly[0]);
            Expect(run.MainDeck.Count == 59 && run.SideDeck.Count == 1 && run.Events.Last().Kind == RunEventKind.MovedToSide, "메인 → 사이드");
            run.MoveToDeck(kaibaOnly[0]);
            Expect(!run.CanMoveToDeck(kaibaOnly[0], out _), "사이드 덱에 없는 카드는 넣을 수 없음");

            // 메인 덱 60장이면 더 못 넣는다.
            var sixty = new DeckRecipe("Test60", "시험 덱 60장",
                StarterDecks.Yugi.Entries.Concat(kaibaOnly.Take(10).Select(c => (c, 1))).ToList());
            var full = NewRun(sixty, side: new[] { kaibaOnly[10] });
            Expect(full.MainDeck.Count == 60 && !full.CanMoveToDeck(kaibaOnly[10], out string fullReason) && fullReason.Contains("60"),
                "메인 덱 60장이면 더 넣을 수 없음");

            // 같은 카드 3장 제한: 블랙홀 1장(덱) + 사이드 3장 → 2장까지만 더 넣는다.
            run = NewRun(StarterDecks.Yugi, side: new[] { darkHole, darkHole, darkHole });
            run.MoveToDeck(darkHole);
            run.MoveToDeck(darkHole);
            Expect(run.DeckCopies(darkHole) == 3 && !run.CanMoveToDeck(darkHole, out string reason) && reason.Contains("3장"), "같은 카드는 덱에 3장까지");

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
            run.MoveToSide(joeyFusion); // 엑스트라 덱을 비워 기절은 메인 덱에서만
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

        void RewardSubstitutionAndTableChange()
        {
            // 모든 카드를 SR로 둔 표: SR이 뽑히면 얻고, 다른 등급은 포인트로 대체한다.
            var allSr = new RarityTable();
            foreach (var deck in StarterDecks.All)
                foreach (var (code, _) in deck.Entries)
                    allSr.Set(code, Rarity.SR);
            var run = NewRun(StarterDecks.Yugi, seed: 7, rarities: allSr);
            for (int i = 0; i < 40; i++)
                Play(run, DuelResult.Win, 8000);
            var gained = run.Events.Where(e => e.Kind == RunEventKind.RewardGained).ToList();
            var substituted = run.Events.Where(e => e.Kind == RunEventKind.RewardSubstituted).ToList();
            Expect(gained.Count + substituted.Count == 40, "승리 40번, 보상 40개");
            Expect(gained.All(e => e.Rarity == Rarity.SR) && substituted.All(e => e.Rarity != Rarity.SR), "SR만 얻고 나머지는 대체");
            Expect(substituted.Count > 0 && gained.Count > 0, $"얻음 {gained.Count}, 대체 {substituted.Count}");
            Expect(run.SubstitutedRewards.Sum() == substituted.Count && run.SideDeck.Count == gained.Count, "대체 수와 사이드 덱 장수");
            Expect(gained.All(e => run.Collection.Count(e.Code) >= 2), "얻은 카드는 컬렉션에 더해짐");

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
                Expect(real.SubstitutedRewards.Sum() == 0, "모든 상대 덱에 N~UR이 있으면 31층까지 포인트 대체 없음");
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
                foreach (var (result, lp) in script)
                {
                    if (run.Phase == RunPhase.Ended)
                        break;
                    Play(run, result, lp);
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
            Expect(RunRandom.For(5, 3, RunRandomPurpose.Reward).NextULong() != RunRandom.For(5, 3, RunRandomPurpose.Faint).NextULong(), "용도가 다르면 다른 난수열");
            Expect(RunRandom.For(5, 3, RunRandomPurpose.Reward).NextULong() != RunRandom.For(5, 4, RunRandomPurpose.Reward).NextULong(), "듀얼 번호가 다르면 다른 난수열");
        }

        void SetupChecks()
        {
            var collection = CardCollection.FromRecipes(StarterDecks.All);
            var elevenSide = StarterDecks.Kaiba.Entries.Take(11).Select(e => e.Code).ToList();
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi, SideDeck = elevenSide }, _db, collection).Any(p => p.Contains("사이드 덱")),
                "시작 사이드 덱 11장");
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi, SideDeck = elevenSide.Take(10).ToList() }, _db, collection).Count == 0,
                "시작 사이드 덱 10장");
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = StarterDecks.Yugi }, _db, CardCollection.FromRecipes(new[] { StarterDecks.Kaiba })).Count > 0,
                "컬렉션에 없는 카드로 시작");
            var thirtyNine = new DeckRecipe("Test39", "39장", StarterDecks.Yugi.Entries.Take(39).ToList());
            Expect(RunState.CheckSetup(new RunSetup { StartDeck = thirtyNine }, _db, collection).Count > 0, "39장으로 시작");
            ExpectThrows<ArgumentException>(() => RunState.Start(new RunSetup { StartDeck = thirtyNine }, _db, _rarities, collection), "39장 런 시작");
            Expect(CardCollection.FromRecipes(StarterDecks.All).Count(53129443) == 4, "첫 컬렉션: 블랙홀 4장 (4덱 합)");
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
    }
}
