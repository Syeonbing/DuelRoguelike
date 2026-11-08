// 런 규칙이 맞게 적용됐는지, 런 상태를 단계마다 앞뒤로 비교하고 이벤트를 읽어 따로 다시 계산한다.
// RunState의 계산 함수(기절 장수, 보충 가능 장수 등)는 쓰지 않고 설계 문서의 규칙을 여기서 직접 적는다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Run;

namespace DuelRoguelike.Dev
{
    /// <summary>여러 런에 걸쳐 모으는 규칙 통계 (통과 조건이 아니라 보고용).</summary>
    public sealed class RunRuleStats
    {
        public const int LpBuckets = 5;

        public int ViolationCount;
        public readonly List<string> Violations = new List<string>();

        /// <summary>승리 보상에서 뽑힌 등급 수, 확률표로 계산한 기대값, 실제로 얻은 수, 포인트 대체 수 (등급별).</summary>
        public readonly int[] Rolls = new int[RarityTable.RarityCount];
        public readonly double[] ExpectedRolls = new double[RarityTable.RarityCount];
        public readonly int[] Gained = new int[RarityTable.RarityCount];
        public readonly int[] Substituted = new int[RarityTable.RarityCount];

        public int MainFainted;
        public int ExtraFainted;
        /// <summary>엑스트라 덱이 2장 이상 남아 있던 패배에서 엑스트라 덱 기절 장수 분포 (0, 1, 2, 3장 이상).</summary>
        public readonly int[] ExtraChain = new int[4];

        public int BossHeals;
        public int LossHeals;
        public int RefillsRequired;
        public int RefillCards;
        public int RunsEnded;

        /// <summary>듀얼 시작 LP 구간별 듀얼 수와 승리 수: ~1999, ~3999, ~5999, ~7999, 8000 이상.</summary>
        public readonly int[] LpBucketDuels = new int[LpBuckets];
        public readonly int[] LpBucketWins = new int[LpBuckets];

        public static int LpBucket(int lp) => lp >= 8000 ? 4 : Math.Max(0, lp / 2000);

        public static readonly string[] LpBucketNames = { "~1999", "2000~3999", "4000~5999", "6000~7999", "8000~" };

        const int MaxViolationsKept = 40;

        public void AddViolation(string text)
        {
            ViolationCount++;
            if (Violations.Count < MaxViolationsKept)
                Violations.Add(text);
        }
    }

    public sealed class RunRuleChecker
    {
        sealed class Snapshot
        {
            public List<uint> Main, Extra, Side, FaintedMain, FaintedExtra;
            public int Floor, Streak, Lp, Wins, Losses, Draws, DuelsPlayed, EventCount;
            public RunPhase Phase;
            public int[] Substituted;
            public Dictionary<uint, int> Collection;
        }

        readonly RunState _run;
        readonly CardDatabase _db;
        readonly RarityTable _rarities;
        readonly RunRuleStats _stats;
        readonly string _tag;
        readonly Dictionary<uint, int> _expectedCards;
        Snapshot _before;

        public RunRuleChecker(RunState run, CardDatabase db, RarityTable rarities, RunRuleStats stats, string tag)
        {
            _run = run;
            _db = db;
            _rarities = rarities;
            _stats = stats;
            _tag = tag;
            Expect(run.Phase == RunPhase.Ready && run.Floor == 1 && run.Lp == 8000 && run.LosingStreak == 0 && run.DuelsPlayed == 0,
                $"런 시작 상태가 이상함: {run.Phase}, {run.Floor}층, LP {run.Lp}, 연패 {run.LosingStreak}");
            Expect(run.Events.Count == 1 && run.Events[0].Kind == RunEventKind.RunStarted, "런 시작 이벤트가 하나가 아님");
            _expectedCards = Tally(AllCards(run));
            CheckDeckRules("런 시작");
        }

        /// <summary>런 상태를 바꾸는 호출(BeginDuel, FinishDuel, MoveToDeck) 바로 전에 부른다.</summary>
        public void BeforeStep() => _before = Take();

        public void AfterBeginDuel(RunDuel duel, DuelSessionOptions options)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{b.Floor}층 듀얼 {b.DuelsPlayed + 1} 시작";
            Expect(b.Phase == RunPhase.Ready, $"{where}: Ready가 아닌데 듀얼을 시작함 ({b.Phase})");
            Expect(_run.Phase == RunPhase.InDuel, $"{where}: 상태가 InDuel이 아님 ({_run.Phase})");
            Expect(duel.Floor == b.Floor && duel.DuelNumber == b.DuelsPlayed + 1, $"{where}: 듀얼의 층·번호가 다름 ({duel.Floor}층, {duel.DuelNumber})");

            // LP: 보스 층(10층마다) 직전에만 8000으로 회복, 8000을 넘으면 그대로.
            bool boss = b.Floor % 10 == 0;
            int expectedLp = boss ? Math.Max(b.Lp, 8000) : b.Lp;
            Expect(duel.PlayerLp == expectedLp && _run.Lp == expectedLp, $"{where}: 시작 LP {duel.PlayerLp} (기대 {expectedLp})");
            Expect(options.Team1.StartingLP == (uint)expectedLp, $"{where}: 엔진 시작 LP {options.Team1.StartingLP} (기대 {expectedLp})");
            Expect(options.Team2.StartingLP == 8000, $"{where}: 상대 시작 LP {options.Team2.StartingLP}");
            bool healed = events.Any(e => e.Kind == RunEventKind.LpRestored);
            Expect(healed == (boss && b.Lp < 8000), $"{where}: 보스 층 회복 이벤트 {(healed ? "있음" : "없음")} (LP {b.Lp})");
            if (healed)
                _stats.BossHeals++;
            Expect(events.Count(e => e.Kind == RunEventKind.DuelStarted) == 1 && events.Last().Kind == RunEventKind.DuelStarted,
                $"{where}: 듀얼 시작 이벤트가 이상함");

            // 상대: 상대 목록 중 하나, 엔진에는 그 레시피 그대로.
            Expect(_run.Opponents.Contains(duel.Opponent), $"{where}: 상대 덱 {duel.Opponent.Id}가 상대 목록에 없음");
            Expect(SameCards(options.Decks[1], duel.Opponent.ToCardList()), $"{where}: 엔진에 넣은 상대 덱이 레시피와 다름");
            // 나: 지금 메인 + 엑스트라 덱 그대로 (사이드·기절 카드는 넣지 않는다).
            Expect(SameCards(options.Decks[0], b.Main.Concat(b.Extra)), $"{where}: 엔진에 넣은 나의 덱이 런의 메인·엑스트라 덱과 다름");
            Expect(options.Seed.Any(s => s != 0), $"{where}: 엔진 시드가 모두 0");
            ExpectUnchanged(b, where, main: true, extra: true, side: true, fainted: true);
            CheckDeckRules(where);

            _stats.LpBucketDuels[RunRuleStats.LpBucket(expectedLp)]++;
        }

        public void AfterFinishDuel(RunDuel duel, DuelResult result, int endLp)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{duel.Floor}층 듀얼 {duel.DuelNumber} ({result})";
            Expect(_run.DuelsPlayed == duel.DuelNumber, $"{where}: 끝난 듀얼 수 {_run.DuelsPlayed}");
            Expect(_run.CurrentDuel == null, $"{where}: 진행 중인 듀얼이 남아 있음");

            if (result == DuelResult.Win)
            {
                _stats.LpBucketWins[RunRuleStats.LpBucket(duel.PlayerLp)]++;
                Expect(_run.Phase == RunPhase.Ready, $"{where}: 승리 뒤 상태 {_run.Phase}");
                Expect(_run.Floor == b.Floor + 1, $"{where}: 승리 뒤 층 {_run.Floor} (기대 {b.Floor + 1})");
                Expect(_run.LosingStreak == 0, $"{where}: 승리 뒤 연패 {_run.LosingStreak}");
                Expect(_run.Lp == endLp, $"{where}: 승리 뒤 LP {_run.Lp} (듀얼이 끝날 때 {endLp})");
                Expect(_run.Wins == b.Wins + 1, $"{where}: 승리 수");
                ExpectUnchanged(b, where, main: true, extra: true, side: false, fainted: true);

                var rewards = events.Where(e => e.Kind == RunEventKind.RewardGained || e.Kind == RunEventKind.RewardSubstituted).ToList();
                if (!Expect(rewards.Count == 1, $"{where}: 보상 이벤트 {rewards.Count}개 (1개여야 함)"))
                    return;
                var reward = rewards[0];
                _stats.Rolls[(int)reward.Rarity]++;
                int[] row = RewardTable.Percentages(duel.Floor);
                for (int i = 0; i < row.Length; i++)
                    _stats.ExpectedRolls[i] += row[i] / 100.0;
                bool opponentHas = duel.OpponentCards.Any(c => _rarities.TryGet(c, out var r) && r == reward.Rarity);
                if (reward.Kind == RunEventKind.RewardGained)
                {
                    _stats.Gained[(int)reward.Rarity]++;
                    Expect(duel.OpponentCards.Contains(reward.Code), $"{where}: 얻은 카드 {reward.Code}가 상대 덱에 없음");
                    Expect(_rarities.TryGet(reward.Code, out var rarity) && rarity == reward.Rarity,
                        $"{where}: 얻은 카드 {reward.Code}의 등급이 뽑힌 등급 {reward.Rarity}와 다름");
                    Expect(SameCards(_run.SideDeck, b.Side.Append(reward.Code)), $"{where}: 사이드 덱에 얻은 카드 1장만 더해지지 않음");
                    if (_run.Collection != null)
                        Expect(_run.Collection.Count(reward.Code) == Get(b.Collection, reward.Code) + 1, $"{where}: 컬렉션에 1장 더해지지 않음");
                    _expectedCards[reward.Code] = Get(_expectedCards, reward.Code) + 1;
                }
                else
                {
                    _stats.Substituted[(int)reward.Rarity]++;
                    Expect(!opponentHas, $"{where}: 상대 덱에 {reward.Rarity} 카드가 있는데 포인트로 대체함");
                    Expect(SameCards(_run.SideDeck, b.Side), $"{where}: 포인트 대체인데 사이드 덱이 바뀜");
                    Expect(_run.SubstitutedRewards[(int)reward.Rarity] == b.Substituted[(int)reward.Rarity] + 1, $"{where}: 포인트 대체 수");
                }
            }
            else
            {
                int streak = b.Streak + 1;
                Expect(_run.LosingStreak == streak, $"{where}: 연패 {_run.LosingStreak} (기대 {streak})");
                Expect(_run.Floor == b.Floor, $"{where}: 패배 뒤 층 {_run.Floor} (그대로여야 함)");
                Expect(_run.Lp == Math.Max(endLp, 8000), $"{where}: 패배 뒤 LP {_run.Lp} (기대 {Math.Max(endLp, 8000)})");
                var restored = events.FirstOrDefault(e => e.Kind == RunEventKind.LpRestored);
                Expect(restored == null || restored.PreviousLp >= 0, $"{where}: 회복 전 LP가 음수로 기록됨 ({restored?.PreviousLp})");
                if (endLp < 8000)
                {
                    _stats.LossHeals++;
                    Expect(events.Any(e => e.Kind == RunEventKind.LpRestored && e.LpReason == LpRestoreReason.Loss), $"{where}: 패배 회복 이벤트 없음");
                }
                Expect(result == DuelResult.Draw ? _run.Draws == b.Draws + 1 : _run.Losses == b.Losses + 1, $"{where}: 패배·무승부 수");
                Expect(SameCards(_run.SideDeck, b.Side), $"{where}: 패배했는데 사이드 덱이 바뀜");
                Expect(!events.Any(e => e.Kind == RunEventKind.RewardGained || e.Kind == RunEventKind.RewardSubstituted), $"{where}: 패배했는데 보상");

                // 메인 덱 기절: 연패 횟수 + 층÷10(버림)장, 메인 덱보다 많으면 전부.
                int expectedMain = Math.Min(streak + b.Floor / 10, b.Main.Count);
                var faintedMain = events.Where(e => e.Kind == RunEventKind.CardFainted && e.Pile == RunPile.Main).Select(e => e.Code).ToList();
                var faintedExtra = events.Where(e => e.Kind == RunEventKind.CardFainted && e.Pile == RunPile.Extra).Select(e => e.Code).ToList();
                Expect(faintedMain.Count == expectedMain, $"{where}: 메인 덱 기절 {faintedMain.Count}장 (기대 {expectedMain}장, 연패 {streak}, {b.Floor}층)");
                Expect(SameCards(_run.MainDeck.Concat(faintedMain), b.Main), $"{where}: 메인 덱 - 기절 카드가 맞지 않음");
                Expect(SameCards(_run.FaintedMain, b.FaintedMain.Concat(faintedMain)), $"{where}: 기절한 메인 덱 카드 목록이 맞지 않음");
                Expect(faintedExtra.Count <= b.Extra.Count, $"{where}: 엑스트라 덱보다 많이 기절");
                Expect(SameCards(_run.ExtraDeck.Concat(faintedExtra), b.Extra), $"{where}: 엑스트라 덱 - 기절 카드가 맞지 않음");
                Expect(SameCards(_run.FaintedExtra, b.FaintedExtra.Concat(faintedExtra)), $"{where}: 기절한 엑스트라 덱 카드 목록이 맞지 않음");
                _stats.MainFainted += faintedMain.Count;
                _stats.ExtraFainted += faintedExtra.Count;
                if (b.Extra.Count >= 2)
                    _stats.ExtraChain[Math.Min(3, faintedExtra.Count)]++;

                // 메인 덱이 40장 미만이면: 사이드 덱의 메인 덱 카드로(같은 카드 3장까지) 채울 수 있으면 보충, 아니면 런 종료.
                int needed = 40 - _run.MainDeck.Count;
                if (needed <= 0)
                    Expect(_run.Phase == RunPhase.Ready, $"{where}: 메인 덱 {_run.MainDeck.Count}장인데 상태 {_run.Phase}");
                else
                {
                    int refillable = 0;
                    foreach (var group in _run.SideDeck.Where(c => !IsExtra(c)).GroupBy(c => c))
                    {
                        int inDeck = _run.MainDeck.Count(c => c == group.Key) + _run.ExtraDeck.Count(c => c == group.Key);
                        refillable += Math.Max(0, Math.Min(group.Count(), 3 - inDeck));
                    }
                    var expectedPhase = refillable >= needed ? RunPhase.NeedsRefill : RunPhase.Ended;
                    Expect(_run.Phase == expectedPhase, $"{where}: 메인 덱 {_run.MainDeck.Count}장, 보충 가능 {refillable}장인데 상태 {_run.Phase} (기대 {expectedPhase})");
                    if (expectedPhase == RunPhase.NeedsRefill)
                    {
                        _stats.RefillsRequired++;
                        Expect(_run.RefillNeeded == needed, $"{where}: 보충할 장수 {_run.RefillNeeded} (기대 {needed})");
                    }
                    else
                    {
                        _stats.RunsEnded++;
                        Expect(_run.EndReason == RunEndReason.DeckBelowMinimum && events.Last().Kind == RunEventKind.RunEnded, $"{where}: 런 종료 기록이 없음");
                    }
                }
            }
            CheckConservation(where);
            if (_run.Phase != RunPhase.Ended)
                CheckDeckRules(where, allowShortMain: _run.Phase == RunPhase.NeedsRefill);
        }

        public void AfterMoveToDeck(uint code)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"듀얼 {b.DuelsPlayed} 뒤 보충·교환 {code}";
            bool extra = IsExtra(code);
            bool refill = b.Phase == RunPhase.NeedsRefill;
            Expect(!(refill && extra), $"{where}: 보충 중에 엑스트라 덱 카드를 넣음");
            Expect(SameCards(_run.SideDeck.Append(code), b.Side), $"{where}: 사이드 덱에서 1장 빠지지 않음");
            Expect(SameCards(extra ? _run.ExtraDeck : _run.MainDeck, (extra ? b.Extra : b.Main).Append(code)), $"{where}: 덱에 1장 더해지지 않음");
            Expect(events.Count == 1 && events[0].Kind == RunEventKind.MovedToDeck && events[0].IsRefill == refill, $"{where}: 옮기기 이벤트가 이상함");
            if (refill)
            {
                _stats.RefillCards++;
                var expectedPhase = _run.MainDeck.Count >= 40 ? RunPhase.Ready : RunPhase.NeedsRefill;
                Expect(_run.Phase == expectedPhase, $"{where}: 메인 덱 {_run.MainDeck.Count}장인데 상태 {_run.Phase}");
            }
            CheckConservation(where);
            CheckDeckRules(where, allowShortMain: _run.Phase == RunPhase.NeedsRefill);
        }

        void CheckDeckRules(string where, bool allowShortMain = false)
        {
            int main = _run.MainDeck.Count;
            Expect((allowShortMain || main >= 40) && main <= 60, $"{where}: 메인 덱 {main}장");
            Expect(_run.ExtraDeck.Count <= 15, $"{where}: 엑스트라 덱 {_run.ExtraDeck.Count}장");
            Expect(_run.MainDeck.All(c => !IsExtra(c)) && _run.ExtraDeck.All(IsExtra), $"{where}: 메인·엑스트라 덱에 맞지 않는 종류의 카드");
            foreach (var group in _run.MainDeck.Concat(_run.ExtraDeck).GroupBy(c => c))
                Expect(group.Count() <= 3, $"{where}: {group.Key}가 덱에 {group.Count()}장");
        }

        /// <summary>메인 + 엑스트라 + 사이드 + 기절 = 처음 카드 + 얻은 카드 (카드가 사라지거나 생기지 않는다).</summary>
        void CheckConservation(string where)
        {
            var now = Tally(AllCards(_run));
            bool same = now.Count == _expectedCards.Count && now.All(p => Get(_expectedCards, p.Key) == p.Value);
            Expect(same, $"{where}: 카드 총합이 맞지 않음 (지금 {now.Values.Sum()}장, 기대 {_expectedCards.Values.Sum()}장)");
        }

        void ExpectUnchanged(Snapshot b, string where, bool main, bool extra, bool side, bool fainted)
        {
            if (main) Expect(SameCards(_run.MainDeck, b.Main), $"{where}: 메인 덱이 바뀜");
            if (extra) Expect(SameCards(_run.ExtraDeck, b.Extra), $"{where}: 엑스트라 덱이 바뀜");
            if (side) Expect(SameCards(_run.SideDeck, b.Side), $"{where}: 사이드 덱이 바뀜");
            if (fainted) Expect(SameCards(_run.FaintedMain, b.FaintedMain) && SameCards(_run.FaintedExtra, b.FaintedExtra), $"{where}: 기절 카드가 바뀜");
        }

        bool IsExtra(uint code) => _db.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

        List<RunEvent> NewEvents(Snapshot b) => _run.Events.Skip(b.EventCount).ToList();

        Snapshot Take() => new Snapshot
        {
            Main = _run.MainDeck.ToList(),
            Extra = _run.ExtraDeck.ToList(),
            Side = _run.SideDeck.ToList(),
            FaintedMain = _run.FaintedMain.ToList(),
            FaintedExtra = _run.FaintedExtra.ToList(),
            Floor = _run.Floor,
            Streak = _run.LosingStreak,
            Lp = _run.Lp,
            Wins = _run.Wins,
            Losses = _run.Losses,
            Draws = _run.Draws,
            DuelsPlayed = _run.DuelsPlayed,
            EventCount = _run.Events.Count,
            Phase = _run.Phase,
            Substituted = _run.SubstitutedRewards.ToArray(),
            Collection = _run.Collection?.Cards.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<uint, int>(),
        };

        static IEnumerable<uint> AllCards(RunState run) =>
            run.MainDeck.Concat(run.ExtraDeck).Concat(run.SideDeck).Concat(run.FaintedMain).Concat(run.FaintedExtra);

        static Dictionary<uint, int> Tally(IEnumerable<uint> cards) => cards.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        static bool SameCards(IEnumerable<uint> a, IEnumerable<uint> b)
        {
            var left = Tally(a);
            var right = Tally(b);
            return left.Count == right.Count && left.All(p => Get(right, p.Key) == p.Value);
        }

        static int Get(Dictionary<uint, int> counts, uint code) => counts.TryGetValue(code, out int n) ? n : 0;

        bool Expect(bool condition, string message)
        {
            if (!condition)
                _stats.AddViolation($"{_tag} {message}");
            return condition;
        }
    }
}
