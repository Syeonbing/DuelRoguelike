// 화면 없이 로그라이크 런을 처음부터 끝까지 돌린다 (tools/headless-run.ps1).
// 플레이어 0(시험 플레이어)이 런을 하고, 층마다 WindBot이 상대 덱을 맡는다. 승리 보상(후보·빈 자리)과 보충할 카드는 무작위로 고른다.
// 확인하는 것:
// - 듀얼마다 화면 밖 듀얼 시험과 같은 점검 (엔진 오류·예외·대체 응답·거절된 응답·해석하지 않은 메시지 0)
// - 단계마다 런 규칙 점검 (RunRuleChecker: 보상 후보·골드·봉인·LP·보충·종료)
// - 같은 시드와 같은 듀얼 결과로 런 규칙만 다시 돌리면 이벤트가 똑같은지 (재현)
// - 런의 첫 듀얼을 엔진으로 다시 돌리면 결과가 같은지 (엔진·플레이어까지 포함한 재현)
// - 저장: 단계마다 저장 파일로 쓰고 읽어 다시 쓴 글이 같은지, 걸음마다 저장 파일을 읽은 런으로 이어 가도 원래 런과 같은지,
//   듀얼 중에 쓴 저장 파일을 읽으면 그 듀얼이 패배로 처리되는지
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Run;

namespace DuelRoguelike.Dev
{
    public sealed class HeadlessRunTest
    {
        public sealed class Report
        {
            public int Runs;
            /// <summary>런 종료 조건(덱을 40장으로 채울 수 없음)으로 끝난 런.</summary>
            public int Ended;
            /// <summary>듀얼 수 상한에 걸려 멈춘 런 (끝나지 않은 런, 실패로 친다).</summary>
            public int Capped;
            /// <summary>듀얼이 끝나지 않거나 예외가 나서 멈춘 런.</summary>
            public int Aborted;
            public int Duels;
            public int Wins;
            public int Losses;
            public int Draws;
            public readonly List<int> FinalFloors = new List<int>();
            public readonly List<int> RunDuels = new List<int>();
            /// <summary>시작 덱별 [런 수, 듀얼 수, 승리 수, 최고 층 합].</summary>
            public readonly Dictionary<string, int[]> ByStartDeck = new Dictionary<string, int[]>();
            /// <summary>상대 덱별 [듀얼 수, 플레이어 승리 수].</summary>
            public readonly Dictionary<string, int[]> ByOpponent = new Dictionary<string, int[]>();

            public readonly HeadlessDuelRun.Result Duel = new HeadlessDuelRun.Result();
            public readonly HeadlessDuelRun.Result ReplayDuel = new HeadlessDuelRun.Result();
            public readonly RunRuleStats Rules = new RunRuleStats();

            /// <summary>런 규칙만 다시 돌렸을 때 이벤트가 달랐던 런.</summary>
            public int RuleReplayMismatches;
            /// <summary>첫 듀얼을 엔진으로 다시 돌렸을 때 결과(승자·LP·턴)가 달랐던 런.</summary>
            public int EngineReplayMismatches;
            /// <summary>저장 파일로 쓰고 읽어 다시 쓴 횟수와, 글이 달랐던 횟수.</summary>
            public int SaveRoundTrips;
            public int SaveRoundTripMismatches;
            /// <summary>걸음마다 저장 파일을 읽은 런으로 이어 간 재현이 원래 런과 달랐던 런.</summary>
            public int SaveReplayMismatches;
            /// <summary>듀얼 중에 쓴 저장 파일을 읽어 그 듀얼을 패배로 처리한 횟수와, 결과가 이상했던 횟수.</summary>
            public int ForfeitChecks;
            public int ForfeitMismatches;
            public readonly List<string> Problems = new List<string>();

            public bool Ok => Runs > 0 && Duel.Ok && (ReplayDuel.Duels == 0 || ReplayDuel.Ok) && Rules.ViolationCount == 0
                              && Capped == 0 && Aborted == 0 && RuleReplayMismatches == 0 && EngineReplayMismatches == 0
                              && SaveRoundTripMismatches == 0 && SaveReplayMismatches == 0 && ForfeitMismatches == 0 && Ended == Runs;
        }

        readonly HeadlessDuelRun _duels;
        readonly CardDatabase _db;
        readonly RarityTable _rarities;
        readonly Action<string> _log;

        public HeadlessRunTest(HeadlessDuelRun duels, RarityTable rarities, Action<string> log)
        {
            _duels = duels;
            _db = duels.Database;
            _rarities = rarities;
            _log = log;
        }

        /// <param name="printRunSeed">이 시드의 런은 이벤트를 모두 출력한다 (-1이면 출력하지 않음).</param>
        public Report Run(int runs, int firstSeed, HeadlessPlayerKind player, IReadOnlyList<DeckRecipe> startDecks,
            int maxDuelsPerRun = 400, int maxTurns = 100, int printRunSeed = -1, bool windBotHidesInfo = true)
        {
            var report = new Report();
            foreach (var deck in startDecks)
                for (int seed = firstSeed; seed < firstSeed + runs; seed++)
                    RunOne(report, deck, seed, player, maxDuelsPerRun, maxTurns, seed == printRunSeed, windBotHidesInfo);
            return report;
        }

        void RunOne(Report report, DeckRecipe startDeck, int seed, HeadlessPlayerKind player, int maxDuels, int maxTurns,
            bool print, bool hides)
        {
            report.Runs++;
            string runTag = $"[런 {startDeck.Id} 시드 {seed}]";
            var setup = new RunSetup { Seed = (ulong)seed, StartDeck = startDeck, Opponents = StarterDecks.All };
            var save = GameSave.NewGame();
            var run = save.StartRun(setup, _db, _rarities);
            var checker = new RunRuleChecker(run, _db, _rarities, report.Rules, runTag);
            CheckSaveRoundTrip(report, save, runTag);
            var picks = RunRandom.For((ulong)seed, 0, RunRandomPurpose.External);
            var outcomes = new List<(DuelResult Result, int Lp)>();
            var refills = new List<uint>();
            var rewardSlots = new List<int>();
            (DuelSessionOptions Options, int PlayerSeed, HeadlessDuelRun.DuelOutcome Outcome)? first = null;

            while (run.Phase != RunPhase.Ended)
            {
                if (run.Phase == RunPhase.ChoosingReward)
                {
                    // 자리 3개 중 무작위 (후보가 모자라면 빈 자리 = 골드도 고른다)
                    int slot = picks.Next(RunRules.RewardChoices);
                    checker.BeforeStep();
                    run.ChooseReward(slot);
                    checker.AfterChooseReward(slot);
                    rewardSlots.Add(slot);
                    CheckSaveRoundTrip(report, save, runTag);
                    continue;
                }
                if (run.Phase == RunPhase.NeedsRefill)
                {
                    var candidates = run.RefillCandidates();
                    if (candidates.Count == 0)
                    {
                        report.Rules.AddViolation($"{runTag} 보충해야 하는데 고를 카드가 없음");
                        report.Aborted++;
                        break;
                    }
                    uint code = candidates[picks.Next(candidates.Count)];
                    checker.BeforeStep();
                    run.MoveToDeck(code);
                    checker.AfterMoveToDeck(code);
                    refills.Add(code);
                    CheckSaveRoundTrip(report, save, runTag);
                    continue;
                }
                if (run.DuelsPlayed >= maxDuels)
                {
                    report.Capped++;
                    report.Problems.Add($"{runTag} 듀얼 {maxDuels}판 안에 끝나지 않음 ({run.Floor}층)");
                    break;
                }

                string beforeDuel = save.Write();
                checker.BeforeStep();
                var duel = run.BeginDuel();
                var options = duel.CreateSessionOptions();
                checker.AfterBeginDuel(duel, options);
                CheckForfeitOnLoad(report, save, beforeDuel, runTag);

                int playerSeed = RunRandom.For((ulong)seed, duel.DuelNumber, RunRandomPurpose.External).NextSeed();
                string tag = $"{runTag} {duel.Floor}층 듀얼 {duel.DuelNumber} 대 {duel.Opponent.Id}";
                var outcome = _duels.PlayDuel(report.Duel, options, tag, playerSeed, false, maxTurns, player, HeadlessPlayerKind.WindBot, hides);
                if (!outcome.Finished)
                {
                    report.Aborted++;
                    report.Problems.Add($"{tag} 듀얼이 끝나지 않아 런을 멈춤");
                    break;
                }
                if (first == null)
                    first = (duel.CreateSessionOptions(), playerSeed, outcome);

                var result = RunDuel.ResultOf(outcome.State);
                checker.BeforeStep();
                run.FinishDuel(result, outcome.Lp[0]);
                checker.AfterFinishDuel(duel, result, outcome.Lp[0]);
                outcomes.Add((result, outcome.Lp[0]));
                CheckSaveRoundTrip(report, save, runTag);

                report.Duels++;
                if (result == DuelResult.Win) report.Wins++;
                else if (result == DuelResult.Loss) report.Losses++;
                else report.Draws++;
                var byOpponent = Get(report.ByOpponent, duel.Opponent.Id, 2);
                byOpponent[0]++;
                if (result == DuelResult.Win) byOpponent[1]++;
            }

            if (run.Phase == RunPhase.Ended)
                report.Ended++;
            report.FinalFloors.Add(run.Floor);
            report.RunDuels.Add(run.DuelsPlayed);
            var byDeck = Get(report.ByStartDeck, startDeck.Id, 4);
            byDeck[0]++;
            byDeck[1] += run.DuelsPlayed;
            byDeck[2] += run.Wins;
            byDeck[3] += run.Floor;

            _log($"    {runTag} {run.Floor}층, 듀얼 {run.DuelsPlayed} (승 {run.Wins} 패 {run.Losses + run.Draws}), " +
                 $"사이드 {run.SideDeck.Count}장, 봉인 {run.SealedMain.Count}+{run.SealedExtra.Count}장, 골드 {run.Gold}, " +
                 $"보충 {refills.Count}장, {(run.Phase == RunPhase.Ended ? "런 종료" : $"멈춤 ({run.Phase})")}");
            if (print)
                foreach (var e in run.Events)
                    _log($"      #{e.DuelNumber,-3} {e.Describe(_duels.CardName)}");

            CheckRuleReplay(report, setup, run, outcomes, refills, rewardSlots, runTag);
            CheckSaveReplay(report, setup, save, outcomes, refills, rewardSlots, runTag);
            if (first.HasValue)
                CheckEngineReplay(report, first.Value.Options, first.Value.PlayerSeed, first.Value.Outcome, runTag, player, maxTurns, hides);
        }

        /// <summary>같은 시드·같은 듀얼 결과·같은 보상·보충 선택으로 런 규칙만 다시 돌려, 이벤트가 모두 같은지 본다.</summary>
        void CheckRuleReplay(Report report, RunSetup setup, RunState original, List<(DuelResult Result, int Lp)> outcomes,
            List<uint> refills, List<int> rewardSlots, string runTag)
        {
            var replay = RunState.Start(setup, _db, _rarities, CardCollection.FromRecipes(StarterDecks.All));
            int nextOutcome = 0, nextRefill = 0, nextReward = 0;
            while (replay.Phase != RunPhase.Ended)
            {
                if (replay.Phase == RunPhase.ChoosingReward)
                {
                    if (nextReward >= rewardSlots.Count) break;
                    replay.ChooseReward(rewardSlots[nextReward++]);
                    continue;
                }
                if (replay.Phase == RunPhase.NeedsRefill)
                {
                    if (nextRefill >= refills.Count) break;
                    replay.MoveToDeck(refills[nextRefill++]);
                    continue;
                }
                if (nextOutcome >= outcomes.Count) break;
                replay.BeginDuel();
                var (result, lp) = outcomes[nextOutcome++];
                replay.FinishDuel(result, lp);
            }
            var a = original.Events.Select(e => e.ToString()).ToList();
            var b = replay.Events.Select(e => e.ToString()).ToList();
            if (!a.SequenceEqual(b))
            {
                report.RuleReplayMismatches++;
                int at = Enumerable.Range(0, Math.Min(a.Count, b.Count)).FirstOrDefault(i => a[i] != b[i]);
                report.Problems.Add($"{runTag} 런 규칙 재현 실패: {at}번째 이벤트 \"{(at < a.Count ? a[at] : "-")}\" / \"{(at < b.Count ? b[at] : "-")}\"");
            }
        }

        /// <summary>저장 파일로 쓰고, 읽고, 다시 쓴 글이 처음 쓴 글과 같은지 본다.</summary>
        void CheckSaveRoundTrip(Report report, GameSave save, string runTag)
        {
            report.SaveRoundTrips++;
            string text = save.Write();
            string again;
            try
            {
                again = GameSave.Parse(text, _db, _rarities, "시험").Write();
            }
            catch (FormatException e)
            {
                again = "읽기 실패: " + e.Message;
            }
            if (again != text)
            {
                report.SaveRoundTripMismatches++;
                if (report.SaveRoundTripMismatches <= 3)
                    report.Problems.Add($"{runTag} 듀얼 {save.Run.DuelsPlayed} 뒤 저장 다시 읽기 불일치: {FirstDifference(text, again)}");
            }
        }

        /// <summary>
        /// 듀얼을 시작한 직후(듀얼 중)의 저장 파일을 읽으면 그 듀얼이 패배로 처리되는지 본다:
        /// 듀얼 직전 저장으로 같은 듀얼을 시작해 LP 0으로 진 것과 같아야 한다 (DuelForfeited 이벤트 하나만 더).
        /// </summary>
        void CheckForfeitOnLoad(Report report, GameSave save, string beforeDuel, string runTag)
        {
            report.ForfeitChecks++;
            var run = save.Run;
            int duelNumber = run.CurrentDuel.DuelNumber;
            string problem;
            try
            {
                var loaded = GameSave.Parse(save.Write(), _db, _rarities, "시험");
                var expected = GameSave.Parse(beforeDuel, _db, _rarities, "시험");
                expected.Run.BeginDuel();
                expected.Run.FinishDuel(DuelResult.Loss, 0);
                var forfeited = loaded.Run.Events.Where(e => e.Kind == RunEventKind.DuelForfeited).ToList();
                string withoutForfeit = string.Join("\n", loaded.Write().Split('\n').Where(l => !l.StartsWith("event DuelForfeited ", StringComparison.Ordinal)));
                problem = loaded.ForfeitedDuel != duelNumber ? $"패배로 처리한 듀얼 {loaded.ForfeitedDuel} (기대 {duelNumber})"
                    : loaded.Run.Phase == RunPhase.InDuel || loaded.Run.CurrentDuel != null ? $"읽은 뒤에도 듀얼 중 ({loaded.Run.Phase})"
                    : forfeited.Count != 1 || forfeited[0].DuelNumber != duelNumber ? $"DuelForfeited 이벤트 {forfeited.Count}개"
                    : loaded.Run.Lp != 8000 ? $"패배로 처리한 뒤 LP {loaded.Run.Lp}"
                    : withoutForfeit != expected.Write() ? "LP 0으로 진 것과 다름: " + FirstDifference(expected.Write(), withoutForfeit)
                    : null;
            }
            catch (Exception e) when (e is FormatException || e is InvalidOperationException)
            {
                problem = "읽기 실패: " + e.Message;
            }
            if (problem != null)
            {
                report.ForfeitMismatches++;
                if (report.ForfeitMismatches <= 3)
                    report.Problems.Add($"{runTag} 듀얼 {duelNumber} 중 저장을 읽어 패배로 처리: {problem}");
            }
        }

        /// <summary>
        /// 같은 결과·같은 보상·보충 선택으로 런 규칙만 다시 돌리되, 걸음(듀얼·보상·보충)마다 저장 파일을 읽은 런으로 이어 간다.
        /// 끝난 뒤의 저장 글(컬렉션·런 상태·이벤트 전부)이 원래 런과 같아야 한다.
        /// </summary>
        void CheckSaveReplay(Report report, RunSetup setup, GameSave original, List<(DuelResult Result, int Lp)> outcomes,
            List<uint> refills, List<int> rewardSlots, string runTag)
        {
            if (original.Run.Phase == RunPhase.InDuel)
                return; // 듀얼이 끝나지 않아 멈춘 런 (따로 중단으로 센다)
            var save = GameSave.NewGame();
            save.StartRun(setup, _db, _rarities);
            int nextOutcome = 0, nextRefill = 0, nextReward = 0;
            while (save.Run.Phase != RunPhase.Ended)
            {
                save = GameSave.Parse(save.Write(), _db, _rarities, "시험");
                var run = save.Run;
                if (run.Phase == RunPhase.ChoosingReward)
                {
                    if (nextReward >= rewardSlots.Count) break;
                    run.ChooseReward(rewardSlots[nextReward++]);
                    continue;
                }
                if (run.Phase == RunPhase.NeedsRefill)
                {
                    if (nextRefill >= refills.Count) break;
                    run.MoveToDeck(refills[nextRefill++]);
                    continue;
                }
                if (nextOutcome >= outcomes.Count) break;
                run.BeginDuel();
                var (result, lp) = outcomes[nextOutcome++];
                run.FinishDuel(result, lp);
            }
            string a = original.Write(), b = save.Write();
            if (a != b)
            {
                report.SaveReplayMismatches++;
                report.Problems.Add($"{runTag} 저장하며 이어 간 런이 원래 런과 다름: {FirstDifference(a, b)}");
            }
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

        /// <summary>런의 첫 듀얼을 같은 설정·같은 플레이어 시드로 엔진에서 다시 돌려, 승자·LP·턴 수가 같은지 본다.</summary>
        void CheckEngineReplay(Report report, DuelSessionOptions options, int playerSeed, HeadlessDuelRun.DuelOutcome original,
            string runTag, HeadlessPlayerKind player, int maxTurns, bool hides)
        {
            var again = _duels.PlayDuel(report.ReplayDuel, options, $"{runTag} 첫 듀얼 다시", playerSeed, false, maxTurns,
                player, HeadlessPlayerKind.WindBot, hides);
            if (!again.Finished || again.Winner != original.Winner || again.Lp[0] != original.Lp[0] || again.Lp[1] != original.Lp[1]
                || again.Turns != original.Turns)
            {
                report.EngineReplayMismatches++;
                report.Problems.Add($"{runTag} 첫 듀얼 재현 실패: 승자 {original.Winner}/{again.Winner}, LP {original.Lp[0]}:{original.Lp[1]} / " +
                                    $"{again.Lp[0]}:{again.Lp[1]}, 턴 {original.Turns}/{again.Turns}");
            }
        }

        static int[] Get(Dictionary<string, int[]> map, string key, int size)
        {
            if (!map.TryGetValue(key, out var value))
                map[key] = value = new int[size];
            return value;
        }
    }
}
