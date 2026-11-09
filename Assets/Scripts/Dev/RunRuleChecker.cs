// 런 규칙이 맞게 적용됐는지, 런 상태를 단계마다 앞뒤로 비교하고 이벤트를 읽어 따로 다시 계산한다.
// RunState의 계산 함수(봉인 장수, 보충 가능 장수 등)는 쓰지 않고 규칙 결정 문서(2026-11-10)의 규칙을 여기서 직접 적는다.
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

        /// <summary>
        /// 승리 보상에서 뽑힌 등급 수, 확률표로 계산한 기대값, 후보에서 카드를 고른 수, 빈 자리(골드)를 고른 수,
        /// 상대 덱에 그 등급이 없어 골드로 대체한 수, 후보가 3장보다 적었던 수 (등급별).
        /// </summary>
        public readonly int[] Rolls = new int[RarityTable.RarityCount];
        public readonly double[] ExpectedRolls = new double[RarityTable.RarityCount];
        public readonly int[] Gained = new int[RarityTable.RarityCount];
        public readonly int[] GoldChosen = new int[RarityTable.RarityCount];
        public readonly int[] Substituted = new int[RarityTable.RarityCount];
        public readonly int[] ShortOffers = new int[RarityTable.RarityCount];
        /// <summary>런에서 받은 골드 합 (빈 자리 + 등급 대체).</summary>
        public int Gold;

        public int MainSealed;
        public int ExtraSealed;
        /// <summary>엑스트라 덱이 2장 이상 남아 있던 패배에서 엑스트라 덱 봉인 장수 분포 (0, 1, 2, 3장 이상).</summary>
        public readonly int[] ExtraChain = new int[4];

        /// <summary>보스 층 듀얼 직전에 LP를 8000으로 맞춘 수 (9층 휴식이 생길 때까지 임시).</summary>
        public int BossFloorAdjusts;
        public int BossClears;
        public int LossHeals;
        public int RefillsRequired;
        public int RefillCards;
        public int RunsEnded;

        /// <summary>듀얼 시작 LP 구간별 듀얼 수와 승리 수: ~1999, ~3999, ~5999, ~7999, 8000 이상.</summary>
        public readonly int[] LpBucketDuels = new int[LpBuckets];
        public readonly int[] LpBucketWins = new int[LpBuckets];
        /// <summary>8000보다 높은 LP로 시작한 듀얼 수.</summary>
        public int DuelsAboveFullLp;

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
        /// <summary>등급별 골드 (N, R, SR, UR, FR). 규칙 결정 문서에는 값이 없어서 정한 임시 값 (RunRules.GoldByRarity와 같아야 함).</summary>
        static readonly int[] GoldTable = { 1, 2, 4, 8, 16 };

        sealed class Snapshot
        {
            public List<uint> Main, Extra, Side, SealedMain, SealedExtra;
            public int Floor, Streak, Lp, Wins, Losses, Draws, DuelsPlayed, EventCount, Gold;
            public RunPhase Phase;
            public RewardOffer Offer;
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
            Expect(run.Phase == RunPhase.Ready && run.Floor == 1 && run.Lp == 8000 && run.LosingStreak == 0 && run.DuelsPlayed == 0 && run.Gold == 0,
                $"런 시작 상태가 이상함: {run.Phase}, {run.Floor}층, LP {run.Lp}, 연패 {run.LosingStreak}, 골드 {run.Gold}");
            Expect(run.Events.Count == 1 && run.Events[0].Kind == RunEventKind.RunStarted, "런 시작 이벤트가 하나가 아님");
            Expect(run.SideDeck.Count <= 3, $"시작 사이드 덱 {run.SideDeck.Count}장 (3장까지)");
            foreach (var group in run.MainDeck.Concat(run.ExtraDeck).Concat(run.SideDeck).GroupBy(Name))
                Expect(group.Count() <= 3, $"런 시작: 시작 덱과 사이드 덱을 합쳐 카드명 {group.Key}가 {group.Count()}장");
            _expectedCards = Tally(AllCards(run));
            CheckDeckRules("런 시작");
        }

        /// <summary>런 상태를 바꾸는 호출(BeginDuel, FinishDuel, ChooseReward, MoveToDeck) 바로 전에 부른다.</summary>
        public void BeforeStep() => _before = Take();

        public void AfterBeginDuel(RunDuel duel, DuelSessionOptions options)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{b.Floor}층 듀얼 {b.DuelsPlayed + 1} 시작";
            Expect(b.Phase == RunPhase.Ready, $"{where}: Ready가 아닌데 듀얼을 시작함 ({b.Phase})");
            Expect(_run.Phase == RunPhase.InDuel, $"{where}: 상태가 InDuel이 아님 ({_run.Phase})");
            Expect(duel.Floor == b.Floor && duel.DuelNumber == b.DuelsPlayed + 1, $"{where}: 듀얼의 층·번호가 다름 ({duel.Floor}층, {duel.DuelNumber})");

            // LP: 보스 층(10층마다) 직전에만 8000으로 맞춤 (8000보다 높아도 8000). 9층 휴식이 생길 때까지 임시.
            bool boss = b.Floor % 10 == 0;
            int expectedLp = boss ? 8000 : b.Lp;
            Expect(duel.PlayerLp == expectedLp && _run.Lp == expectedLp, $"{where}: 시작 LP {duel.PlayerLp} (기대 {expectedLp})");
            Expect(options.Team1.StartingLP == (uint)expectedLp, $"{where}: 엔진 시작 LP {options.Team1.StartingLP} (기대 {expectedLp})");
            Expect(options.Team2.StartingLP == 8000, $"{where}: 상대 시작 LP {options.Team2.StartingLP}");
            var adjusted = events.Where(e => e.Kind == RunEventKind.LpRestored).ToList();
            Expect(adjusted.Count == (boss && b.Lp != 8000 ? 1 : 0), $"{where}: 보스 층 LP 맞춤 이벤트 {adjusted.Count}개 (LP {b.Lp})");
            if (adjusted.Count == 1)
            {
                _stats.BossFloorAdjusts++;
                Expect(adjusted[0].LpReason == LpRestoreReason.BossFloor && adjusted[0].PreviousLp == b.Lp && adjusted[0].Lp == 8000,
                    $"{where}: 보스 층 LP 맞춤 이벤트가 이상함 ({adjusted[0].LpReason}, {adjusted[0].PreviousLp} → {adjusted[0].Lp})");
            }
            Expect(events.Count(e => e.Kind == RunEventKind.DuelStarted) == 1 && events.Last().Kind == RunEventKind.DuelStarted,
                $"{where}: 듀얼 시작 이벤트가 이상함");

            // 상대: 상대 목록 중 하나, 엔진에는 그 레시피 그대로.
            Expect(_run.Opponents.Contains(duel.Opponent), $"{where}: 상대 덱 {duel.Opponent.Id}가 상대 목록에 없음");
            Expect(SameCards(options.Decks[1], duel.Opponent.ToCardList()), $"{where}: 엔진에 넣은 상대 덱이 레시피와 다름");
            // 나: 지금 메인 + 엑스트라 덱 그대로 (사이드·봉인 카드는 넣지 않는다).
            Expect(SameCards(options.Decks[0], b.Main.Concat(b.Extra)), $"{where}: 엔진에 넣은 나의 덱이 런의 메인·엑스트라 덱과 다름");
            Expect(options.Seed.Any(s => s != 0), $"{where}: 엔진 시드가 모두 0");
            ExpectUnchanged(b, where, main: true, extra: true, side: true, sealedCards: true);
            Expect(_run.Gold == b.Gold, $"{where}: 골드가 바뀜");
            CheckDeckRules(where);

            _stats.LpBucketDuels[RunRuleStats.LpBucket(expectedLp)]++;
            if (expectedLp > 8000)
                _stats.DuelsAboveFullLp++;
        }

        public void AfterFinishDuel(RunDuel duel, DuelResult result, int endLp)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{duel.Floor}층 듀얼 {duel.DuelNumber} ({result})";
            Expect(_run.DuelsPlayed == duel.DuelNumber, $"{where}: 끝난 듀얼 수 {_run.DuelsPlayed}");
            Expect(_run.CurrentDuel == null, $"{where}: 진행 중인 듀얼이 남아 있음");

            if (result == DuelResult.Win)
                CheckWin(duel, endLp, b, events, where);
            else
                CheckLoss(result, endLp, b, events, where);
            CheckConservation(where);
            if (_run.Phase != RunPhase.Ended)
                CheckDeckRules(where, allowShortMain: _run.Phase == RunPhase.NeedsRefill);
        }

        void CheckWin(RunDuel duel, int endLp, Snapshot b, List<RunEvent> events, string where)
        {
            _stats.LpBucketWins[RunRuleStats.LpBucket(duel.PlayerLp)]++;
            Expect(_run.Floor == b.Floor + 1, $"{where}: 승리 뒤 층 {_run.Floor} (기대 {b.Floor + 1})");
            Expect(_run.LosingStreak == 0, $"{where}: 승리 뒤 연패 {_run.LosingStreak}");
            Expect(_run.Wins == b.Wins + 1, $"{where}: 승리 수");
            ExpectUnchanged(b, where, main: true, extra: true, side: true, sealedCards: true);

            // LP: 보스 층을 이기면 8000, 아니면 남은 LP + 1000 (8000을 넘을 수 있다).
            bool boss = duel.Floor % 10 == 0;
            int expectedLp = boss ? 8000 : endLp + 1000;
            Expect(_run.Lp == expectedLp, $"{where}: 승리 뒤 LP {_run.Lp} (듀얼이 끝날 때 {endLp}, 기대 {expectedLp})");
            var won = events.FirstOrDefault(e => e.Kind == RunEventKind.DuelWon);
            Expect(won != null && won.Lp == endLp, $"{where}: 승리 이벤트의 남은 LP {won?.Lp} (기대 {endLp})");
            var lpEvents = events.Where(e => e.Kind == RunEventKind.LpRestored).ToList();
            if (boss)
            {
                Expect(lpEvents.Count == (endLp != 8000 ? 1 : 0) && lpEvents.All(e => e.LpReason == LpRestoreReason.BossCleared),
                    $"{where}: 보스 격파 LP 이벤트가 이상함");
                _stats.BossClears++;
            }
            else
                Expect(lpEvents.Count == 1 && lpEvents[0].LpReason == LpRestoreReason.Win && lpEvents[0].PreviousLp == endLp && lpEvents[0].Lp == endLp + 1000,
                    $"{where}: 승리 LP 이벤트가 이상함");

            // 보상: 확률표로 등급 하나. 상대 덱에 그 등급이 있으면 후보(서로 다른 카드 3장까지, 모자라면 빈 자리는 골드),
            // 없으면 바로 골드.
            var rewards = events.Where(e => e.Kind == RunEventKind.RewardOffered || e.Kind == RunEventKind.RewardSubstituted).ToList();
            if (!Expect(rewards.Count == 1, $"{where}: 보상 이벤트 {rewards.Count}개 (1개여야 함)"))
                return;
            var reward = rewards[0];
            int r = (int)reward.Rarity;
            _stats.Rolls[r]++;
            int[] row = RewardTable.Percentages(duel.Floor);
            for (int i = 0; i < row.Length; i++)
                _stats.ExpectedRolls[i] += row[i] / 100.0;
            var pool = duel.OpponentCards.Where(c => _rarities.TryGet(c, out var cr) && cr == reward.Rarity).Distinct().ToList();
            if (reward.Kind == RunEventKind.RewardSubstituted)
            {
                _stats.Substituted[r]++;
                _stats.Gold += GoldTable[r];
                Expect(pool.Count == 0, $"{where}: 상대 덱에 {reward.Rarity} 카드가 있는데 골드로 대체함");
                Expect(reward.Count == GoldTable[r] && _run.Gold == b.Gold + GoldTable[r],
                    $"{where}: 등급 대체 골드 {reward.Count} (기대 {GoldTable[r]}), 런 골드 {b.Gold} → {_run.Gold}");
                Expect(_run.Phase == RunPhase.Ready && _run.PendingReward == null, $"{where}: 골드로 대체했는데 상태 {_run.Phase}");
                return;
            }
            var offer = _run.PendingReward;
            Expect(_run.Phase == RunPhase.ChoosingReward && offer != null, $"{where}: 후보가 나왔는데 상태 {_run.Phase}");
            Expect(_run.Gold == b.Gold, $"{where}: 보상을 고르기 전에 골드가 바뀜");
            if (offer == null)
                return;
            var cards = offer.Cards;
            Expect(offer.Rarity == reward.Rarity && offer.DuelNumber == duel.DuelNumber && offer.Floor == duel.Floor,
                $"{where}: 보상 후보의 등급·듀얼·층이 이벤트와 다름");
            Expect(reward.Codes.SequenceEqual(cards), $"{where}: 보상 후보 이벤트의 카드가 런의 후보와 다름");
            Expect(cards.Count == Math.Min(3, pool.Count), $"{where}: 후보 {cards.Count}장 (상대 덱의 {reward.Rarity} {pool.Count}종, 기대 {Math.Min(3, pool.Count)}장)");
            Expect(cards.Distinct().Count() == cards.Count, $"{where}: 후보에 같은 카드가 있음");
            Expect(cards.All(pool.Contains), $"{where}: 후보에 상대 덱의 {reward.Rarity} 카드가 아닌 것이 있음");
            int empty = 3 - cards.Count;
            // 후보의 골드 값은 등급 골드 그대로 두고, 이벤트에는 빈 자리가 있을 때만 적는다.
            Expect(offer.Gold == GoldTable[r] && offer.EmptySlots == empty && reward.Count == (empty > 0 ? offer.Gold : 0),
                $"{where}: 빈 자리 골드 {offer.Gold}, 이벤트 {reward.Count} (빈 자리 {empty}곳, 기대 골드 {GoldTable[r]})");
            if (empty > 0)
                _stats.ShortOffers[r]++;
        }

        void CheckLoss(DuelResult result, int endLp, Snapshot b, List<RunEvent> events, string where)
        {
            int streak = b.Streak + 1;
            Expect(_run.LosingStreak == streak, $"{where}: 연패 {_run.LosingStreak} (기대 {streak})");
            Expect(_run.Floor == b.Floor, $"{where}: 패배 뒤 층 {_run.Floor} (그대로여야 함)");
            // 패배: 8000으로 회복 (듀얼이 8000보다 높은 LP로 끝났으면 그대로).
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
            Expect(_run.Gold == b.Gold, $"{where}: 패배했는데 골드가 바뀜");
            Expect(_run.PendingReward == null && !events.Any(e => e.Kind == RunEventKind.RewardOffered || e.Kind == RunEventKind.RewardGained
                                                                  || e.Kind == RunEventKind.RewardSubstituted), $"{where}: 패배했는데 보상");

            // 메인 덱 봉인: 연패 횟수 + 층÷10(버림)장, 메인 덱보다 많으면 전부.
            int expectedMain = Math.Min(streak + b.Floor / 10, b.Main.Count);
            var sealedMain = events.Where(e => e.Kind == RunEventKind.CardSealed && e.Pile == RunPile.Main).Select(e => e.Code).ToList();
            var sealedExtra = events.Where(e => e.Kind == RunEventKind.CardSealed && e.Pile == RunPile.Extra).Select(e => e.Code).ToList();
            Expect(sealedMain.Count == expectedMain, $"{where}: 메인 덱 봉인 {sealedMain.Count}장 (기대 {expectedMain}장, 연패 {streak}, {b.Floor}층)");
            Expect(SameCards(_run.MainDeck.Concat(sealedMain), b.Main), $"{where}: 메인 덱 - 봉인 카드가 맞지 않음");
            Expect(SameCards(_run.SealedMain, b.SealedMain.Concat(sealedMain)), $"{where}: 봉인된 메인 덱 카드 목록이 맞지 않음");
            Expect(sealedExtra.Count <= b.Extra.Count, $"{where}: 엑스트라 덱보다 많이 봉인");
            Expect(SameCards(_run.ExtraDeck.Concat(sealedExtra), b.Extra), $"{where}: 엑스트라 덱 - 봉인 카드가 맞지 않음");
            Expect(SameCards(_run.SealedExtra, b.SealedExtra.Concat(sealedExtra)), $"{where}: 봉인된 엑스트라 덱 카드 목록이 맞지 않음");
            _stats.MainSealed += sealedMain.Count;
            _stats.ExtraSealed += sealedExtra.Count;
            if (b.Extra.Count >= 2)
                _stats.ExtraChain[Math.Min(3, sealedExtra.Count)]++;

            // 메인 덱이 40장 미만이면: 사이드 덱의 메인 덱 카드로(같은 카드명 3장까지) 채울 수 있으면 보충, 아니면 런 종료.
            int needed = 40 - _run.MainDeck.Count;
            if (needed <= 0)
            {
                Expect(_run.Phase == RunPhase.Ready, $"{where}: 메인 덱 {_run.MainDeck.Count}장인데 상태 {_run.Phase}");
                return;
            }
            int refillable = 0;
            foreach (var group in _run.SideDeck.Where(c => !IsExtra(c)).GroupBy(Name))
            {
                int inDeck = _run.MainDeck.Concat(_run.ExtraDeck).Count(c => Name(c) == group.Key);
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

        public void AfterChooseReward(int slot)
        {
            var b = _before;
            var events = NewEvents(b);
            var offer = b.Offer;
            string where = $"듀얼 {b.DuelsPlayed} 보상 고르기 (자리 {slot})";
            if (!Expect(b.Phase == RunPhase.ChoosingReward && offer != null, $"{where}: 보상 고르기 상태가 아니었음 ({b.Phase})"))
                return;
            Expect(_run.Phase == RunPhase.Ready && _run.PendingReward == null, $"{where}: 고른 뒤 상태 {_run.Phase}");
            Expect(events.Count == 1, $"{where}: 이벤트 {events.Count}개 (1개여야 함)");
            ExpectUnchanged(b, where, main: true, extra: true, side: false, sealedCards: true);
            int r = (int)offer.Rarity;
            if (slot < offer.Cards.Count)
            {
                uint code = offer.Cards[slot];
                _stats.Gained[r]++;
                Expect(events.Count == 1 && events[0].Kind == RunEventKind.RewardGained && events[0].Code == code && events[0].Rarity == offer.Rarity,
                    $"{where}: 획득 이벤트가 이상함");
                Expect(SameCards(_run.SideDeck, b.Side.Append(code)), $"{where}: 사이드 덱에 고른 카드 1장만 더해지지 않음");
                if (_run.Collection != null)
                    Expect(_run.Collection.Count(code) == Get(b.Collection, code) + 1, $"{where}: 컬렉션에 1장 더해지지 않음");
                Expect(_run.Gold == b.Gold, $"{where}: 카드를 골랐는데 골드가 바뀜");
                _expectedCards[code] = Get(_expectedCards, code) + 1;
            }
            else
            {
                _stats.GoldChosen[r]++;
                _stats.Gold += offer.Gold;
                Expect(offer.Gold == GoldTable[r] && _run.Gold == b.Gold + GoldTable[r],
                    $"{where}: 빈 자리 골드 {offer.Gold}, 런 골드 {b.Gold} → {_run.Gold} (기대 +{GoldTable[r]})");
                Expect(events.Count == 1 && events[0].Kind == RunEventKind.RewardGold && events[0].Count == offer.Gold, $"{where}: 골드 이벤트가 이상함");
                Expect(SameCards(_run.SideDeck, b.Side), $"{where}: 빈 자리를 골랐는데 사이드 덱이 바뀜");
            }
            CheckConservation(where);
            CheckDeckRules(where);
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
            // 같은 카드명(일러스트·에라타가 다른 ID를 함께 센다)은 메인·엑스트라 덱에 3장까지. 사이드 덱은 제한 없음.
            foreach (var group in _run.MainDeck.Concat(_run.ExtraDeck).GroupBy(Name))
                Expect(group.Count() <= 3, $"{where}: 카드명 {group.Key}가 덱에 {group.Count()}장");
        }

        /// <summary>메인 + 엑스트라 + 사이드 + 봉인 = 처음 카드 + 얻은 카드 (카드가 사라지거나 생기지 않는다).</summary>
        void CheckConservation(string where)
        {
            var now = Tally(AllCards(_run));
            bool same = now.Count == _expectedCards.Count && now.All(p => Get(_expectedCards, p.Key) == p.Value);
            Expect(same, $"{where}: 카드 총합이 맞지 않음 (지금 {now.Values.Sum()}장, 기대 {_expectedCards.Values.Sum()}장)");
        }

        void ExpectUnchanged(Snapshot b, string where, bool main, bool extra, bool side, bool sealedCards)
        {
            if (main) Expect(SameCards(_run.MainDeck, b.Main), $"{where}: 메인 덱이 바뀜");
            if (extra) Expect(SameCards(_run.ExtraDeck, b.Extra), $"{where}: 엑스트라 덱이 바뀜");
            if (side) Expect(SameCards(_run.SideDeck, b.Side), $"{where}: 사이드 덱이 바뀜");
            if (sealedCards) Expect(SameCards(_run.SealedMain, b.SealedMain) && SameCards(_run.SealedExtra, b.SealedExtra), $"{where}: 봉인 카드가 바뀜");
        }

        bool IsExtra(uint code) => _db.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

        /// <summary>카드명 기준 ID: 카드 정보의 alias가 있으면 그것 (일러스트·에라타가 다른 ID는 원래 카드를 가리킨다).</summary>
        uint Name(uint code) => _db.TryGet(code, out var record) && record.Data.Alias != 0 ? record.Data.Alias : code;

        List<RunEvent> NewEvents(Snapshot b) => _run.Events.Skip(b.EventCount).ToList();

        Snapshot Take() => new Snapshot
        {
            Main = _run.MainDeck.ToList(),
            Extra = _run.ExtraDeck.ToList(),
            Side = _run.SideDeck.ToList(),
            SealedMain = _run.SealedMain.ToList(),
            SealedExtra = _run.SealedExtra.ToList(),
            Floor = _run.Floor,
            Streak = _run.LosingStreak,
            Lp = _run.Lp,
            Wins = _run.Wins,
            Losses = _run.Losses,
            Draws = _run.Draws,
            DuelsPlayed = _run.DuelsPlayed,
            EventCount = _run.Events.Count,
            Gold = _run.Gold,
            Phase = _run.Phase,
            Offer = _run.PendingReward,
            Collection = _run.Collection?.Cards.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<uint, int>(),
        };

        static IEnumerable<uint> AllCards(RunState run) =>
            run.MainDeck.Concat(run.ExtraDeck).Concat(run.SideDeck).Concat(run.SealedMain).Concat(run.SealedExtra);

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
