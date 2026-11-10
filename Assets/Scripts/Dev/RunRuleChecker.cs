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

        /// <summary>휴식 층(끝자리 9)에 들어가 LP를 8000으로 맞춘 수.</summary>
        public int RestHeals;
        /// <summary>층 종류별로 들어간 수 (FloorKind 순서: 전투, 이벤트, 칩교환소, 휴식, 보스).</summary>
        public readonly int[] FloorVisits = new int[5];
        /// <summary>확률 이벤트 층 자리(1~10)별로 들어간 수와 그중 이벤트 층이었던 수.</summary>
        public readonly int[] RandomFloorVisits = new int[11];
        public readonly int[] RandomEventFloors = new int[11];
        /// <summary>칩: 얻은 합, 칩교환소에서 쓴 합, 상품별 구매 수 (ShopProduct 순서), 봉인 해제가 진열된 칩교환소 수.</summary>
        public int ChipsEarned;
        public int ChipsSpent;
        public readonly int[] Bought = new int[5];
        public int UnsealStocked;
        /// <summary>보스 보상: 봉인 해제를 고른 수, 보상 1장 더를 받은 수.</summary>
        public int BossUnseals;
        public int BossBonusRewards;
        /// <summary>런 종료 정산 골드 합.</summary>
        public int SettlementGold;
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
        /// <summary>코인 토스: 플레이어가 이긴 수, 상대가 이겨 선공을 고른 수, 플레이어가 후공으로 한 듀얼 수, 후공 듀얼 승리 수.</summary>
        public int CoinTossWins, OpponentChoseFirst, SecondDuels, SecondWins;
        /// <summary>이벤트: 성향별로 나온 수 (긍정·무성향·부정), 이벤트별로 나온 수, 고른 선택지 수, 그냥 지나간 수, 잠겨서 강제 선택지를 고른 수.</summary>
        public readonly int[] EventTendencies = new int[3];
        public readonly SortedDictionary<string, int> EventsSeen = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public int EventChoices, EventPasses, EventForced;
        /// <summary>이벤트 전투 수와 승리 수, 이벤트로 받은·잃은 카드, 봉인, 칩.</summary>
        public int EventBattles, EventBattleWins, EventCardsGained, EventCardsLost, EventSeals, EventUnseals, EventChipsGained, EventChipsLost;

        public string EventsSeenText => string.Join(", ", EventsSeen.Select(p => $"{p.Key} {p.Value}"));

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

        /// <summary>칩 (임시 값, RunRules와 같아야 함): 승리 20, 보스 층 승리 50. 칩교환소 가격은 ShopProduct 순서.</summary>
        const int ChipsPerWin = 20, ChipsPerBossWin = 50;
        static readonly int[] PriceTable = { 30, 50, 50, 80, 150 };

        static readonly ShopProduct[] Swaps =
            { ShopProduct.SwapSameRarity, ShopProduct.SwapSameRarityChosen, ShopProduct.SwapAny, ShopProduct.SwapAnyChosen };

        sealed class Snapshot
        {
            public List<uint> Main, Extra, Side, SealedMain, SealedExtra;
            public int Floor, Streak, Lp, Wins, Losses, Draws, DuelsPlayed, EventCount, Gold, Chips, ChipsEarned;
            public RunPhase Phase;
            public FloorKind Kind;
            public string EventId;
            public bool EventDone;
            public int BattleChoice;
            public RewardOffer Offer;
            public List<(ShopProduct Product, int Price, bool Sold)> Shop;
            public Dictionary<uint, int> Collection;
        }

        readonly RunState _run;
        readonly CardDatabase _db;
        readonly RarityTable _rarities;
        readonly RunRuleStats _stats;
        readonly string _tag;
        readonly Dictionary<uint, int> _expectedCards;
        readonly Dictionary<int, FloorKind> _kinds = new Dictionary<int, FloorKind>();
        IReadOnlyList<uint> _lastOpponentCards = Array.Empty<uint>();
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
            Expect(run.Events.Count == 2 && run.Events[0].Kind == RunEventKind.RunStarted && run.Events[1].Kind == RunEventKind.FloorEntered,
                "런 시작 이벤트가 RunStarted, FloorEntered가 아님");
            Expect(run.Chips == 0 && run.ChipsEarned == 0, $"런 시작 칩 {run.Chips} (얻은 칩 {run.ChipsEarned})");
            CheckFloor("런 시작", run.Events.Last());
            Expect(run.SideDeck.Count <= 3, $"시작 사이드 덱 {run.SideDeck.Count}장 (3장까지)");
            foreach (var group in run.MainDeck.Concat(run.ExtraDeck).Concat(run.SideDeck).GroupBy(Name))
                Expect(group.Count() <= 3, $"런 시작: 시작 덱과 사이드 덱을 합쳐 카드명 {group.Key}가 {group.Count()}장");
            _expectedCards = Tally(AllCards(run));
            CheckDeckRules("런 시작");
        }

        /// <summary>런 상태를 바꾸는 호출(BeginDuel, FinishDuel, ChooseReward, MoveToDeck) 바로 전에 부른다.</summary>
        public void BeforeStep() => _before = Take();

        /// <param name="playerChoice">코인 토스에 이겼을 때 시험 플레이어가 고른 것 (true = 선공).</param>
        public void AfterBeginDuel(RunDuel duel, DuelSessionOptions options, bool playerChoice)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{b.Floor}층 듀얼 {b.DuelsPlayed + 1} 시작";
            Expect(b.Phase == RunPhase.Ready, $"{where}: Ready가 아닌데 듀얼을 시작함 ({b.Phase})");
            Expect(_run.Phase == RunPhase.InDuel, $"{where}: 상태가 InDuel이 아님 ({_run.Phase})");
            Expect(duel.Floor == b.Floor && duel.DuelNumber == b.DuelsPlayed + 1, $"{where}: 듀얼의 층·번호가 다름 ({duel.Floor}층, {duel.DuelNumber})");

            // 듀얼은 전투 층(보스 층 포함)에서만, 또는 이벤트 전투를 고른 이벤트 층. LP는 듀얼 전에 바꾸지 않는다 (8000으로 맞추는 것은 9층 휴식).
            bool boss = b.Floor % 10 == 0;
            bool eventBattle = b.Kind == FloorKind.Event;
            if (eventBattle)
            {
                var choice = BattleChoiceOf(b);
                Expect(choice != null && duel.IsEventBattle && !duel.IsBossFloor && duel.Opponent.Id == choice.BattleDeckId,
                    $"{where}: 이벤트 층에서 이벤트 전투가 아닌 듀얼 (선택지 {b.BattleChoice}, 상대 {duel.Opponent.Id}, 이벤트 전투 {duel.IsEventBattle})");
                _stats.EventBattles++;
            }
            else
                Expect(b.Kind == (boss ? FloorKind.Boss : FloorKind.Battle) && duel.IsBossFloor == boss && !duel.IsEventBattle,
                    $"{where}: {b.Kind} 층에서 듀얼 (보스 {duel.IsBossFloor}, 이벤트 전투 {duel.IsEventBattle})");
            int expectedLp = b.Lp;
            Expect(duel.PlayerLp == expectedLp && _run.Lp == expectedLp, $"{where}: 시작 LP {duel.PlayerLp} (기대 {expectedLp})");

            // 코인 토스: 런 시드·듀얼 번호의 CoinToss 난수에서 첫 값 < 50이면 플레이어 승리, 둘째 값 < 90이면 상대가 선공을 고른다.
            // 이긴 쪽이 선후공을 고르고, 선공이 엔진의 팀 0이다.
            var toss = RunRandom.For(_run.Seed, b.DuelsPlayed + 1, RunRandomPurpose.CoinToss);
            bool won = toss.Next(100) < 50;
            bool opponentFirst = toss.Next(100) < 90;
            bool first = won ? playerChoice : !opponentFirst;
            int me = first ? 0 : 1;
            Expect(duel.Toss.PlayerWon == won && duel.PlayerGoesFirst == first && duel.PlayerTeam == me,
                $"{where}: 코인 토스 {duel.Toss.PlayerWon}/선공 {duel.PlayerGoesFirst} (기대 {won}/{first})");
            var lps = new[] { options.Team1.StartingLP, options.Team2.StartingLP };
            Expect(lps[me] == (uint)expectedLp, $"{where}: 엔진에서 나의 시작 LP {lps[me]} (기대 {expectedLp}, 팀 {me})");
            Expect(lps[1 - me] == 8000, $"{where}: 상대 시작 LP {lps[1 - me]}");
            var started = events.LastOrDefault(e => e.Kind == RunEventKind.DuelStarted);
            Expect(started != null && started.Coin == (won ? CoinTossWinner.Player : CoinTossWinner.Opponent)
                                   && started.Turn == (first ? TurnOrder.First : TurnOrder.Second),
                $"{where}: 듀얼 시작 기록의 코인 토스·선후공 ({started?.Coin}, {started?.Turn})");
            if (won) _stats.CoinTossWins++;
            else if (opponentFirst) _stats.OpponentChoseFirst++;
            if (!first) _stats.SecondDuels++;
            Expect(!events.Any(e => e.Kind == RunEventKind.LpRestored), $"{where}: 듀얼 시작에 LP 이벤트");
            Expect(_run.Chips == b.Chips, $"{where}: 칩이 바뀜");
            Expect(events.Count(e => e.Kind == RunEventKind.DuelStarted) == 1 && events.Last().Kind == RunEventKind.DuelStarted,
                $"{where}: 듀얼 시작 이벤트가 이상함");

            // 상대: 상대 목록 중 하나(이벤트 전투면 선택지에 정해진 덱), 엔진에는 그 레시피 그대로.
            Expect(eventBattle || _run.Opponents.Contains(duel.Opponent), $"{where}: 상대 덱 {duel.Opponent.Id}가 상대 목록에 없음");
            Expect(SameCards(options.Decks[1 - me], duel.Opponent.ToCardList()), $"{where}: 엔진에 넣은 상대 덱이 레시피와 다름");
            // 나: 지금 메인 + 엑스트라 덱 그대로 (사이드·봉인 카드는 넣지 않는다).
            Expect(SameCards(options.Decks[me], b.Main.Concat(b.Extra)), $"{where}: 엔진에 넣은 나의 덱이 런의 메인·엑스트라 덱과 다름");
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
            if (!duel.PlayerGoesFirst && result == DuelResult.Win)
                _stats.SecondWins++;
            Expect(_run.DuelsPlayed == duel.DuelNumber, $"{where}: 끝난 듀얼 수 {_run.DuelsPlayed}");
            Expect(_run.CurrentDuel == null, $"{where}: 진행 중인 듀얼이 남아 있음");
            _lastOpponentCards = duel.OpponentCards;

            if (duel.IsEventBattle)
                CheckEventBattleEnd(result, endLp, b, events, where);
            else if (result == DuelResult.Win)
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
            Expect(_run.LosingStreak == 0, $"{where}: 승리 뒤 연패 {_run.LosingStreak}");
            Expect(_run.Wins == b.Wins + 1, $"{where}: 승리 수");
            ExpectUnchanged(b, where, main: true, extra: true, side: true, sealedCards: true);

            // LP: 보스 층을 이기면 8000, 아니면 남은 LP + 1000 (8000을 넘을 수 있다).
            bool boss = duel.Floor % 10 == 0;
            int expectedLp = boss ? 8000 : endLp + 1000;
            var won = events.FirstOrDefault(e => e.Kind == RunEventKind.DuelWon);
            Expect(won != null && won.Lp == endLp, $"{where}: 승리 이벤트의 남은 LP {won?.Lp} (기대 {endLp})");
            var lpEvents = events.Where(e => e.Kind == RunEventKind.LpRestored && e.LpReason != LpRestoreReason.Rest).ToList();
            if (boss)
            {
                Expect(lpEvents.Count == (endLp != 8000 ? 1 : 0) && lpEvents.All(e => e.LpReason == LpRestoreReason.BossCleared),
                    $"{where}: 보스 격파 LP 이벤트가 이상함");
                _stats.BossClears++;
            }
            else
                Expect(lpEvents.Count == 1 && lpEvents[0].LpReason == LpRestoreReason.Win && lpEvents[0].PreviousLp == endLp && lpEvents[0].Lp == endLp + 1000,
                    $"{where}: 승리 LP 이벤트가 이상함");

            // 칩: 승리 20, 보스 층 승리 50 (임시 값).
            int chips = boss ? ChipsPerBossWin : ChipsPerWin;
            var gained = events.Where(e => e.Kind == RunEventKind.ChipsGained).ToList();
            Expect(gained.Count == 1 && gained[0].Count == chips && gained[0].Chips == b.Chips + chips, $"{where}: 칩 이벤트가 이상함 (기대 +{chips})");
            Expect(_run.Chips == b.Chips + chips && _run.ChipsEarned == b.ChipsEarned + chips,
                $"{where}: 칩 {b.Chips} → {_run.Chips}, 얻은 칩 {b.ChipsEarned} → {_run.ChipsEarned} (기대 +{chips})");
            _stats.ChipsEarned += chips;
            if (boss)
                Expect(duel.IsBossFloor, $"{where}: 보스 층인데 보스 듀얼이 아님");

            // 보상: 승리 보상 1번. 보스 층이면 이어서 보스 보상 (봉인된 카드가 있으면 봉인 해제 고르기, 없으면 보상 1장 더).
            // 고를 것이 남아 있는 동안 층은 그대로, 다 끝나면 다음 층.
            var rewards = events.Where(e => e.Kind == RunEventKind.RewardOffered || e.Kind == RunEventKind.RewardSubstituted).ToList();
            if (!Expect(rewards.Count >= 1 && !rewards[0].IsBonus, $"{where}: 승리 보상 이벤트가 없음 ({rewards.Count}개)"))
                return;
            int gold = CheckRewardEvent(rewards[0], duel.OpponentCards, duel.Floor, duel.DuelNumber, false, where);
            if (gold < 0)
            {
                Expect(rewards.Count == 1, $"{where}: 승리 보상 후보를 고르기 전에 보상이 더 나옴");
                ExpectStay(b, duel.Floor, 0, expectedLp, where);
                return;
            }
            CheckWinFlow(b, events, rewards.Skip(1).ToList(), boss, duel.OpponentCards, duel.DuelNumber, duel.Floor, gold, expectedLp, where);
        }

        /// <summary>
        /// 보상 이벤트 하나를 확인한다: 확률표로 등급 하나, 상대 덱에 그 등급이 있으면 후보(서로 다른 카드 3장까지, 모자라면 빈 자리는 골드),
        /// 없으면 바로 골드. 골드로 대체했으면 그 골드를, 후보가 나왔으면 -1을 돌려준다.
        /// </summary>
        int CheckRewardEvent(RunEvent reward, IReadOnlyList<uint> opponentCards, int floor, int duelNumber, bool bonus, string where)
        {
            int r = (int)reward.Rarity;
            _stats.Rolls[r]++;
            int[] row = RewardTable.Percentages(floor);
            for (int i = 0; i < row.Length; i++)
                _stats.ExpectedRolls[i] += row[i] / 100.0;
            if (bonus)
                _stats.BossBonusRewards++;
            Expect(reward.IsBonus == bonus && reward.Floor == floor, $"{where}: 보상 이벤트의 보스 보상 표시·층이 다름 ({reward.IsBonus}, {reward.Floor}층)");
            var pool = opponentCards.Where(c => _rarities.TryGet(c, out var cr) && cr == reward.Rarity).Distinct().ToList();
            if (reward.Kind == RunEventKind.RewardSubstituted)
            {
                _stats.Substituted[r]++;
                _stats.Gold += GoldTable[r];
                Expect(pool.Count == 0, $"{where}: 상대 덱에 {reward.Rarity} 카드가 있는데 골드로 대체함");
                Expect(reward.Count == GoldTable[r], $"{where}: 등급 대체 골드 {reward.Count} (기대 {GoldTable[r]})");
                return GoldTable[r];
            }
            var offer = _run.PendingReward;
            Expect(_run.Phase == RunPhase.ChoosingReward && offer != null, $"{where}: 후보가 나왔는데 상태 {_run.Phase}");
            if (offer == null)
                return -1;
            var cards = offer.Cards;
            Expect(offer.Rarity == reward.Rarity && offer.DuelNumber == duelNumber && offer.Floor == floor && offer.IsBossBonus == bonus,
                $"{where}: 보상 후보의 등급·듀얼·층·보스 보상 표시가 이벤트와 다름");
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
            return -1;
        }

        /// <summary>
        /// 승리 뒤 고르기 하나가 끝났을 때(또는 승리 보상이 골드로 대체됐을 때) 그다음: 보스 보상이 남았으면 그것을 열고,
        /// 다 끝났으면 다음 층. rewards는 이 걸음에서 아직 확인하지 않은 보상 이벤트, gold는 지금까지 확인한 이 걸음의 골드.
        /// </summary>
        void CheckWinFlow(Snapshot b, List<RunEvent> events, List<RunEvent> rewards, bool bonusPending, IReadOnlyList<uint> opponentCards,
            int duelNumber, int floor, int gold, int lp, string where)
        {
            if (bonusPending)
            {
                int sealedCount = _run.SealedMain.Count + _run.SealedExtra.Count;
                if (sealedCount > 0)
                {
                    var offered = events.Where(e => e.Kind == RunEventKind.UnsealOffered).ToList();
                    Expect(rewards.Count == 0 && _run.Phase == RunPhase.ChoosingUnseal && offered.Count == 1 && offered[0].Count == sealedCount,
                        $"{where}: 봉인된 카드 {sealedCount}장인데 보스 보상이 봉인 해제 고르기가 아님 ({_run.Phase}, 보상 이벤트 {rewards.Count}개)");
                    ExpectStay(b, floor, gold, lp, where);
                    return;
                }
                if (!Expect(rewards.Count == 1, $"{where}: 봉인된 카드가 없는데 보스 보상(1장 더) 이벤트 {rewards.Count}개"))
                    return;
                int more = CheckRewardEvent(rewards[0], opponentCards, floor, duelNumber, true, where);
                if (more < 0)
                {
                    ExpectStay(b, floor, gold, lp, where);
                    return;
                }
                gold += more;
            }
            else
                Expect(rewards.Count == 0, $"{where}: 보스 보상이 아닌데 보상이 더 나옴");
            Expect(_run.Phase == RunPhase.Ready && _run.PendingReward == null, $"{where}: 고를 것이 끝났는데 상태 {_run.Phase}");
            Expect(_run.Gold == b.Gold + gold, $"{where}: 런 골드 {b.Gold} → {_run.Gold} (기대 +{gold})");
            CheckEntered(events, floor, lp, where);
        }

        /// <summary>승리 뒤 아직 고를 것이 남아 층에 머무는지.</summary>
        void ExpectStay(Snapshot b, int floor, int gold, int lp, string where)
        {
            Expect(_run.Floor == floor && _run.Lp == lp, $"{where}: 고르기 전에 층·LP가 바뀜 ({_run.Floor}층, LP {_run.Lp}, 기대 {floor}층, LP {lp})");
            Expect(_run.Gold == b.Gold + gold, $"{where}: 런 골드 {b.Gold} → {_run.Gold} (기대 +{gold})");
            Expect(!_run.Events.Skip(b.EventCount).Any(e => e.Kind == RunEventKind.FloorEntered), $"{where}: 고르기 전에 다음 층으로 감");
        }

        /// <summary>다음 층으로 올라갔는지: 층 +1, FloorEntered 이벤트 하나, 휴식 층이면 LP 8000.</summary>
        void CheckEntered(List<RunEvent> events, int fromFloor, int lpBefore, string where)
        {
            Expect(_run.Floor == fromFloor + 1, $"{where}: 층 {_run.Floor} (기대 {fromFloor + 1})");
            var entered = events.Where(e => e.Kind == RunEventKind.FloorEntered).ToList();
            if (!Expect(entered.Count == 1 && entered[0].Floor == _run.Floor, $"{where}: 층 들어가기 이벤트 {entered.Count}개"))
                return;
            CheckFloor(where, entered[0]);
            bool rest = _run.FloorKind == FloorKind.Rest;
            var heals = events.Where(e => e.Kind == RunEventKind.LpRestored && e.LpReason == LpRestoreReason.Rest).ToList();
            Expect(heals.Count == (rest && lpBefore != 8000 ? 1 : 0) && heals.All(e => e.PreviousLp == lpBefore && e.Lp == 8000),
                $"{where}: 휴식 LP 이벤트 {heals.Count}개 (LP {lpBefore}, {_run.FloorKind})");
            int lp = rest ? 8000 : lpBefore;
            Expect(_run.Lp == lp, $"{where}: 층에 들어간 뒤 LP {_run.Lp} (기대 {lp})");
            if (heals.Count > 0)
                _stats.RestHeals++;
        }

        /// <summary>
        /// 층 종류 규칙: 끝자리 5 칩교환소, 6 이벤트, 9 휴식, 0 보스. 나머지(1~4, 7~8)는 전투 또는 확률 이벤트이고,
        /// 1~4층과 7~8층에는 전투 층이 하나는 있다. 칩교환소 진열: 봉인 대상 교체 4종은 늘, 봉인 해제는 확률, 가격은 임시 표.
        /// </summary>
        void CheckFloor(string where, RunEvent entered)
        {
            int floor = _run.Floor;
            var kind = _run.FloorKind;
            Expect(entered.FloorKind == kind, $"{where}: 층 들어가기 이벤트의 종류 {entered.FloorKind} (런 {kind})");
            _kinds[floor] = kind;
            _stats.FloorVisits[(int)kind]++;
            int pos = (floor - 1) % 10 + 1;
            FloorKind? fixedKind = pos == 5 ? FloorKind.ChipExchange : pos == 6 ? FloorKind.Event : pos == 9 ? FloorKind.Rest
                : pos == 10 ? FloorKind.Boss : (FloorKind?)null;
            if (fixedKind != null)
                Expect(kind == fixedKind, $"{where}: {floor}층이 {kind} (기대 {fixedKind})");
            else
            {
                Expect(kind == FloorKind.Battle || kind == FloorKind.Event, $"{where}: {floor}층이 {kind} (전투나 이벤트여야 함)");
                _stats.RandomFloorVisits[pos]++;
                if (kind == FloorKind.Event)
                    _stats.RandomEventFloors[pos]++;
            }
            if (pos == 4 || pos == 8)
            {
                int first = floor - (pos == 4 ? 3 : 1);
                bool battle = Enumerable.Range(first, floor - first + 1).Any(f => !_kinds.TryGetValue(f, out var k) || k == FloorKind.Battle);
                Expect(battle, $"{where}: {first}~{floor}층에 전투 층이 없음");
            }
            var shop = _run.Shop;
            if (kind == FloorKind.ChipExchange)
            {
                bool unseal = shop.Count == 5 && shop[4].Product == ShopProduct.Unseal;
                Expect(shop.Take(4).Select(o => o.Product).SequenceEqual(Swaps) && (shop.Count == 4 || unseal)
                       && shop.All(o => o.Price == PriceTable[(int)o.Product] && !o.Sold), $"{where}: 칩교환소 진열이 이상함");
                if (unseal)
                    _stats.UnsealStocked++;
            }
            else
                Expect(shop.Count == 0, $"{where}: 칩교환소가 아닌데 진열이 있음");
            if (kind == FloorKind.Event)
                CheckEventPick(where, entered);
            else
                Expect(_run.CurrentEventId == null && entered.EventId == null, $"{where}: 이벤트 층이 아닌데 이벤트 {entered.EventId}");
        }

        /// <summary>런 종료: RunEnded 다음에 골드 정산. 정산 = 도달 층 + 10층 단위 보너스(n번째 10층 n) + 얻은 칩÷100 + 남은 칩÷10.</summary>
        void CheckSettlement(List<RunEvent> events, string where)
        {
            int cleared = Math.Max(0, _run.Floor - 1) / 10;
            int expected = _run.Floor + cleared * (cleared + 1) / 2 + _run.ChipsEarned / 100 + _run.Chips / 10;
            int n = events.Count;
            Expect(n >= 2 && events[n - 2].Kind == RunEventKind.RunEnded && events[n - 1].Kind == RunEventKind.GoldSettled
                   && events[n - 1].Count == expected && events[n - 1].Chips == _run.Chips && events[n - 1].ChipsEarned == _run.ChipsEarned
                   && _run.SettlementGold == expected,
                $"{where}: 런 종료·정산 기록이 이상함 (정산 {_run.SettlementGold}, 기대 {expected})");
            _stats.SettlementGold += expected;
        }

        /// <param name="eventBattle">이벤트 전투의 패배: 봉인 규칙은 같고, 보충할 일이 없으면 다음 층으로 간다.</param>
        void CheckLoss(DuelResult result, int endLp, Snapshot b, List<RunEvent> events, string where, bool eventBattle = false)
        {
            int streak = b.Streak + 1;
            Expect(_run.LosingStreak == streak, $"{where}: 연패 {_run.LosingStreak} (기대 {streak})");
            bool moved = eventBattle && _run.Phase == RunPhase.Ready;
            Expect(_run.Floor == b.Floor + (moved ? 1 : 0), $"{where}: 패배 뒤 층 {_run.Floor} ({(moved ? "다음 층" : "그대로")}이어야 함)");
            // 패배: 8000으로 회복 (듀얼이 8000보다 높은 LP로 끝났으면 그대로). 이벤트 전투 뒤 휴식 층에 들어갔으면 8000.
            int lossLp = Math.Max(endLp, 8000);
            if (moved)
                CheckEntered(events, b.Floor, lossLp, where);
            else
                Expect(_run.Lp == lossLp, $"{where}: 패배 뒤 LP {_run.Lp} (기대 {lossLp})");
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
            Expect(_run.Chips == b.Chips && _run.ChipsEarned == b.ChipsEarned, $"{where}: 패배했는데 칩이 바뀜");
            Expect(moved || !events.Any(e => e.Kind == RunEventKind.FloorEntered), $"{where}: 패배했는데 다른 층으로 감");
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
                Expect(_run.EndReason == RunEndReason.DeckBelowMinimum, $"{where}: 런 종료 이유 {_run.EndReason}");
                CheckSettlement(events, where);
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
            if (!Expect(events.Count >= 1, $"{where}: 이벤트가 없음"))
                return;
            ExpectUnchanged(b, where, main: true, extra: true, side: false, sealedCards: true);
            Expect(_run.Chips == b.Chips, $"{where}: 칩이 바뀜");
            int r = (int)offer.Rarity;
            int gold = 0;
            if (slot < offer.Cards.Count)
            {
                uint code = offer.Cards[slot];
                _stats.Gained[r]++;
                Expect(events[0].Kind == RunEventKind.RewardGained && events[0].Code == code && events[0].Rarity == offer.Rarity,
                    $"{where}: 획득 이벤트가 이상함");
                Expect(SameCards(_run.SideDeck, b.Side.Append(code)), $"{where}: 사이드 덱에 고른 카드 1장만 더해지지 않음");
                if (_run.Collection != null)
                    Expect(_run.Collection.Count(code) == Get(b.Collection, code) + 1, $"{where}: 컬렉션에 1장 더해지지 않음");
                _expectedCards[code] = Get(_expectedCards, code) + 1;
            }
            else
            {
                _stats.GoldChosen[r]++;
                _stats.Gold += offer.Gold;
                gold = offer.Gold;
                Expect(offer.Gold == GoldTable[r], $"{where}: 빈 자리 골드 {offer.Gold} (기대 {GoldTable[r]})");
                Expect(events[0].Kind == RunEventKind.RewardGold && events[0].Count == offer.Gold, $"{where}: 골드 이벤트가 이상함");
                Expect(SameCards(_run.SideDeck, b.Side), $"{where}: 빈 자리를 골랐는데 사이드 덱이 바뀜");
            }
            // 보스 층의 승리 보상을 골랐으면 이어서 보스 보상, 아니면 다음 층.
            var rewards = events.Where(e => e.Kind == RunEventKind.RewardOffered || e.Kind == RunEventKind.RewardSubstituted).ToList();
            bool bonusPending = b.Kind == FloorKind.Boss && !offer.IsBossBonus;
            CheckWinFlow(b, events, rewards, bonusPending, _lastOpponentCards, offer.DuelNumber, b.Floor, gold, b.Lp, where);
            CheckConservation(where);
            CheckDeckRules(where);
        }

        /// <summary>보스 보상으로 봉인된 카드 1장을 골라 풀었다: 사이드 덱으로, 그리고 다음 층.</summary>
        public void AfterChooseUnseal(uint code)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"듀얼 {b.DuelsPlayed} 보스 보상 봉인 해제 {code}";
            if (!Expect(b.Phase == RunPhase.ChoosingUnseal, $"{where}: 봉인 해제 고르기 상태가 아니었음 ({b.Phase})"))
                return;
            bool fromMain = b.SealedMain.Contains(code);
            Expect(fromMain || b.SealedExtra.Contains(code), $"{where}: 봉인된 카드가 아님");
            Expect(events.Count >= 1 && events[0].Kind == RunEventKind.CardUnsealed && events[0].Code == code && events[0].IsBonus
                   && events[0].Pile == (fromMain ? RunPile.Main : RunPile.Extra), $"{where}: 봉인 해제 이벤트가 이상함");
            Expect(SameCards(_run.SideDeck, b.Side.Append(code)), $"{where}: 풀린 카드가 사이드 덱에 더해지지 않음");
            Expect(SameCards(_run.SealedMain.Concat(_run.SealedExtra).Append(code), b.SealedMain.Concat(b.SealedExtra)), $"{where}: 봉인 카드에서 1장 빠지지 않음");
            ExpectUnchanged(b, where, main: true, extra: true, side: false, sealedCards: false);
            Expect(_run.Chips == b.Chips, $"{where}: 칩이 바뀜");
            _stats.BossUnseals++;
            CheckWinFlow(b, events, new List<RunEvent>(), false, _lastOpponentCards, b.DuelsPlayed, b.Floor, 0, b.Lp, where);
            CheckConservation(where);
            CheckDeckRules(where);
        }

        /// <summary>칩교환소나 휴식 층을 떠났다: 아무것도 바뀌지 않고 다음 층.</summary>
        public void AfterLeaveFloor()
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{b.Floor}층({b.Kind}) 떠나기";
            Expect(b.Phase == RunPhase.Ready && (b.Kind == FloorKind.ChipExchange || b.Kind == FloorKind.Rest || (b.Kind == FloorKind.Event && b.EventDone && b.BattleChoice < 0)),
                $"{where}: 떠날 수 없는 층·상태 ({b.Phase}, 이벤트 끝남 {b.EventDone})");
            Expect(_run.Phase == RunPhase.Ready, $"{where}: 떠난 뒤 상태 {_run.Phase}");
            ExpectUnchanged(b, where, main: true, extra: true, side: true, sealedCards: true);
            Expect(_run.Chips == b.Chips && _run.Gold == b.Gold, $"{where}: 칩·골드가 바뀜");
            Expect(events.All(e => e.Kind == RunEventKind.FloorEntered || e.Kind == RunEventKind.LpRestored), $"{where}: 이벤트가 이상함");
            CheckEntered(events, b.Floor, b.Lp, where);
        }

        /// <summary>
        /// 이벤트 층에서 선택지를 골랐다 (-1은 그냥 지나감). 고를 수 있었는지(조건·강제·지나가기)를 고르기 전 상태로 따로 따지고,
        /// 이벤트 전투면 그 층에서 듀얼을 기다리는지, 아니면 결과를 따로 계산해 맞춰 보고 다음 층(보충·종료면 그대로)인지 본다.
        /// </summary>
        public void AfterEventChoice(int index)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{b.Floor}층 이벤트 {b.EventId} 선택지 {index}";
            var ev = TowerEventList.Find(b.EventId ?? "");
            if (!Expect(b.Phase == RunPhase.Ready && b.Kind == FloorKind.Event && ev != null && !b.EventDone && b.BattleChoice < 0,
                    $"{where}: 고를 수 있는 이벤트 층이 아니었음 ({b.Kind}, {b.Phase}, 끝남 {b.EventDone}, 전투 {b.BattleChoice})"))
                return;
            bool allLocked = ev.Choices.Where(c => !c.IsForced).All(c => !Meets(c, ev.Scope, b));
            var choice = index >= 0 && index < ev.Choices.Count ? ev.Choices[index] : null;
            bool legal = index == -1 ? allLocked && !ev.Choices.Any(c => c.IsForced)
                : choice == null ? false
                : choice.IsForced ? allLocked
                : Meets(choice, ev.Scope, b);
            Expect(legal, $"{where}: 고를 수 없는 선택지를 고름 (모두 잠김 {allLocked})");
            Expect(events.Count >= 1 && events[0].Kind == RunEventKind.EventResolved && events[0].EventId == b.EventId && events[0].Count == index,
                $"{where}: 이벤트 결과 기록이 이상함");
            if (index == -1) _stats.EventPasses++;
            else if (choice != null && choice.IsForced) _stats.EventForced++;
            else _stats.EventChoices++;

            if (choice != null && choice.IsBattle)
            {
                Expect(_run.EventBattlePending && _run.PendingEventBattleIndex == index && !_run.EventDone && _run.Phase == RunPhase.Ready
                       && _run.Floor == b.Floor && events.Count == 1, $"{where}: 이벤트 전투를 기다리지 않음");
                ExpectUnchanged(b, where, main: true, extra: true, side: true, sealedCards: true);
                Expect(_run.Chips == b.Chips && _run.Gold == b.Gold && _run.Lp == b.Lp, $"{where}: 이벤트 전투 전에 칩·골드·LP가 바뀜");
                return;
            }
            Expect(!_run.EventBattlePending, $"{where}: 전투가 아닌데 이벤트 전투를 기다림");
            int lp = CheckEventEffects(choice, b, b.Lp, b.Chips, b.ChipsEarned, events.Skip(1).ToList(), where);
            CheckAfterEvent(events, b, lp, where);
        }

        /// <summary>이벤트 결과를 다 받은 뒤: Ready면 다음 층, 보충이면 그 층에 남아 이벤트 끝남, 종료면 정산.</summary>
        void CheckAfterEvent(List<RunEvent> events, Snapshot b, int lp, string where)
        {
            if (_run.Phase == RunPhase.Ready)
                CheckEntered(events, b.Floor, lp, where);
            else
            {
                Expect(_run.Floor == b.Floor && !events.Any(e => e.Kind == RunEventKind.FloorEntered) && _run.Lp == lp,
                    $"{where}: 보충·종료인데 층 {_run.Floor}, LP {_run.Lp} (기대 {lp})");
                if (_run.Phase == RunPhase.NeedsRefill)
                {
                    _stats.RefillsRequired++;
                    Expect(_run.EventDone && _run.RefillNeeded == 40 - _run.MainDeck.Count, $"{where}: 이벤트 뒤 보충 상태가 이상함");
                }
                else if (Expect(_run.Phase == RunPhase.Ended && _run.EndReason == RunEndReason.DeckBelowMinimum, $"{where}: 이벤트 뒤 상태 {_run.Phase}"))
                {
                    _stats.RunsEnded++;
                    CheckSettlement(events, where);
                }
            }
            CheckConservation(where);
            if (_run.Phase != RunPhase.Ended)
                CheckDeckRules(where, allowShortMain: _run.Phase == RunPhase.NeedsRefill);
        }

        /// <summary>이벤트 전투가 끝났다: 이기면 남은 LP + 1000 뒤에 선택지 결과(보상·칩 없음), 지면 일반 패배처럼 봉인. 보충할 일이 없으면 다음 층.</summary>
        void CheckEventBattleEnd(DuelResult result, int endLp, Snapshot b, List<RunEvent> events, string where)
        {
            var choice = BattleChoiceOf(b);
            Expect(!_run.EventBattlePending && (_run.Phase != RunPhase.Ready || _run.Floor == b.Floor + 1 || _run.EventDone),
                $"{where}: 이벤트 전투가 끝났는데 아직 기다림");
            Expect(_run.PendingReward == null && !events.Any(e => e.Kind == RunEventKind.RewardOffered || e.Kind == RunEventKind.RewardGained
                                                                  || e.Kind == RunEventKind.RewardSubstituted || e.Kind == RunEventKind.UnsealOffered),
                $"{where}: 이벤트 전투인데 승리 보상");
            if (choice == null || result != DuelResult.Win)
            {
                CheckLoss(result, endLp, b, events, where, eventBattle: true);
                return;
            }
            _stats.EventBattleWins++;
            Expect(_run.LosingStreak == 0 && _run.Wins == b.Wins + 1, $"{where}: 승리 수·연패");
            int n = events.Count;
            Expect(n >= 2 && events[0].Kind == RunEventKind.DuelWon && events[0].Lp == endLp
                   && events[1].Kind == RunEventKind.LpRestored && events[1].LpReason == LpRestoreReason.Win && events[1].PreviousLp == endLp
                   && events[1].Lp == endLp + 1000, $"{where}: 이벤트 전투 승리 기록(승리, LP +1000)이 이상함");
            int lp = CheckEventEffects(choice, b, endLp + 1000, b.Chips, b.ChipsEarned, events.Skip(2).ToList(), where);
            CheckAfterEvent(events, b, lp, where);
        }

        EventChoice BattleChoiceOf(Snapshot b)
        {
            var ev = TowerEventList.Find(b.EventId ?? "");
            var choice = ev != null && b.BattleChoice >= 0 && b.BattleChoice < ev.Choices.Count ? ev.Choices[b.BattleChoice] : null;
            return choice != null && choice.IsBattle ? choice : null;
        }

        /// <summary>선택지 조건을 이 상태에서 만족하는지: 칩 이상, 보유 범위(덱 = 메인·엑스트라, 덱·사이드 덱 = + 사이드)의 조건 카드 장수 이상.</summary>
        bool Meets(EventChoice choice, HoldScope scope, int chips, IEnumerable<uint> main, IEnumerable<uint> extra, IEnumerable<uint> side)
        {
            var held = scope == HoldScope.Deck ? main.Concat(extra) : main.Concat(extra).Concat(side);
            return chips >= choice.MinChips && (choice.Requires == null || held.Count(c => choice.Requires.Matches(c, _db)) >= choice.Requires.Count);
        }

        bool Meets(EventChoice choice, HoldScope scope, Snapshot b) => Meets(choice, scope, b.Chips, b.Main, b.Extra, b.Side);

        /// <summary>
        /// 선택지 결과를 순서대로 따로 계산해 런과 맞춰 본다: 칩(잃는 칩은 가진 만큼만), LP(1 아래로는 안 감), 골드, 카드 받기(사이드 덱·컬렉션),
        /// 봉인(메인 덱에서), 봉인 해제(봉인된 카드가 없으면 칩), 잃기(보유 범위에서 조건에 맞는 카드). 결과를 받은 뒤의 LP를 돌려준다 (층 들어가기 전).
        /// </summary>
        int CheckEventEffects(EventChoice choice, Snapshot b, int lp, int chips, int earned, List<RunEvent> events, string where)
        {
            var main = b.Main.ToList();
            var extra = b.Extra.ToList();
            var side = b.Side.ToList();
            var sealedMain = b.SealedMain.ToList();
            var sealedExtra = b.SealedExtra.ToList();
            int gold = b.Gold;
            var collection = new Dictionary<uint, int>(b.Collection);
            var cardEvents = new Queue<RunEvent>(events.Where(e => e.Kind == RunEventKind.CardSealed || e.Kind == RunEventKind.CardUnsealed
                                                                   || e.Kind == RunEventKind.CardLost || e.Kind == RunEventKind.EventCardGained));
            var scope = TowerEventList.Find(b.EventId ?? "")?.Scope ?? HoldScope.DeckAndSide;
            RunEvent Next(RunEventKind kind)
            {
                var e = cardEvents.Count > 0 ? cardEvents.Dequeue() : null;
                Expect(e != null && e.Kind == kind, $"{where}: {kind} 기록이 없음 ({e?.Kind})");
                return e != null && e.Kind == kind ? e : null;
            }
            foreach (var effect in choice?.Effects ?? Array.Empty<EventEffect>())
            {
                switch (effect.Kind)
                {
                    case EventEffectKind.Chips:
                        if (effect.Amount > 0)
                        {
                            chips += effect.Amount;
                            earned += effect.Amount;
                            _stats.EventChipsGained += effect.Amount;
                        }
                        else
                        {
                            int lost = Math.Min(-effect.Amount, chips);
                            chips -= lost;
                            _stats.EventChipsLost += lost;
                        }
                        break;
                    case EventEffectKind.Lp:
                        lp = Math.Max(1, lp + effect.Amount);
                        break;
                    case EventEffectKind.Gold:
                        gold += effect.Amount;
                        break;
                    case EventEffectKind.GainCard:
                        if (Next(RunEventKind.EventCardGained) is RunEvent gained)
                            Expect(gained.Code == effect.Card, $"{where}: 받은 카드 {gained.Code} (기대 {effect.Card})");
                        side.Add(effect.Card);
                        collection[effect.Card] = Get(collection, effect.Card) + 1;
                        _expectedCards[effect.Card] = Get(_expectedCards, effect.Card) + 1;
                        _stats.EventCardsGained++;
                        break;
                    case EventEffectKind.Seal:
                        for (int i = 0; i < effect.Amount && main.Count > 0; i++)
                        {
                            if (!(Next(RunEventKind.CardSealed) is RunEvent sealedEvent))
                                break;
                            Expect(sealedEvent.Pile == RunPile.Main && main.Remove(sealedEvent.Code), $"{where}: 메인 덱에 없는 카드 {sealedEvent.Code}를 봉인");
                            sealedMain.Add(sealedEvent.Code);
                            _stats.EventSeals++;
                        }
                        break;
                    case EventEffectKind.Unseal:
                        if (sealedMain.Count + sealedExtra.Count == 0)
                        {
                            chips += effect.Amount;
                            earned += effect.Amount;
                            _stats.EventChipsGained += effect.Amount;
                        }
                        else if (Next(RunEventKind.CardUnsealed) is RunEvent unsealed)
                        {
                            var from = unsealed.Pile == RunPile.Extra ? sealedExtra : sealedMain;
                            Expect(!unsealed.IsBonus && from.Remove(unsealed.Code), $"{where}: 봉인되지 않은 카드 {unsealed.Code}를 봉인 해제");
                            side.Add(unsealed.Code);
                            _stats.EventUnseals++;
                        }
                        break;
                    case EventEffectKind.LoseCard:
                        var piles = new List<(List<uint> Pile, RunPile Kind)> { (main, RunPile.Main), (extra, RunPile.Extra) };
                        if (scope != HoldScope.Deck)
                            piles.Add((side, RunPile.Side));
                        if (!piles.Any(p => p.Pile.Any(c => effect.Target.Matches(c, _db))))
                            break;
                        if (Next(RunEventKind.CardLost) is RunEvent lostEvent)
                        {
                            var pile = piles.FirstOrDefault(p => p.Kind == lostEvent.Pile).Pile;
                            Expect(pile != null && effect.Target.Matches(lostEvent.Code, _db) && pile.Remove(lostEvent.Code),
                                $"{where}: 조건({effect.Target.Text})에 맞지 않거나 없는 카드 {lostEvent.Code}를 잃음 ({lostEvent.Pile})");
                            _expectedCards[lostEvent.Code] = Get(_expectedCards, lostEvent.Code) - 1;
                            if (_expectedCards[lostEvent.Code] == 0)
                                _expectedCards.Remove(lostEvent.Code);
                            _stats.EventCardsLost++;
                        }
                        break;
                }
            }
            Expect(cardEvents.Count == 0, $"{where}: 결과에 없는 카드 기록 {cardEvents.Count}개 ({cardEvents.FirstOrDefault()?.Kind})");
            Expect(_run.Chips == chips && _run.ChipsEarned == earned, $"{where}: 칩 {_run.Chips}/{_run.ChipsEarned} (기대 {chips}/{earned})");
            Expect(_run.Gold == gold, $"{where}: 골드 {_run.Gold} (기대 {gold})");
            Expect(SameCards(_run.MainDeck, main), $"{where}: 이벤트 뒤 메인 덱이 맞지 않음"); // 보충은 따로 고르는 걸음
            Expect(SameCards(_run.ExtraDeck, extra), $"{where}: 이벤트 뒤 엑스트라 덱이 맞지 않음");
            Expect(SameCards(_run.SideDeck, side), $"{where}: 이벤트 뒤 사이드 덱이 맞지 않음");
            Expect(SameCards(_run.SealedMain, sealedMain) && SameCards(_run.SealedExtra, sealedExtra), $"{where}: 이벤트 뒤 봉인 카드가 맞지 않음");
            var nowCollection = _run.Collection?.Cards.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<uint, int>();
            Expect(nowCollection.Count == collection.Count && nowCollection.All(p => Get(collection, p.Key) == p.Value), $"{where}: 이벤트 뒤 컬렉션이 맞지 않음");
            var lpEvents = events.Where(e => e.Kind == RunEventKind.LpRestored && e.LpReason == LpRestoreReason.Event).ToList();
            int lpEffects = (choice?.Effects ?? Array.Empty<EventEffect>()).Count(e => e.Kind == EventEffectKind.Lp);
            Expect(lpEvents.Count <= lpEffects && lpEvents.All(e => e.Lp != e.PreviousLp && e.Lp >= 1) && (lpEvents.Count == 0 || lpEvents.Last().Lp == lp),
                $"{where}: 이벤트 LP 기록이 이상함 ({lpEvents.Count}개, LP 결과 {lpEffects}개)");
            return lp;
        }

        /// <summary>
        /// 이벤트 층의 이벤트를 같은 규칙으로 다시 뽑아 맞춰 본다: 자리(1~4·6·7~8층)·깊이·덱·반복으로 후보를 모으고,
        /// 런 시드·층의 Event 난수로 성향(긍정 35 : 무성향 15 : 부정 50, 후보 없는 성향은 뺌)을 뽑고, 그 안에서 가중치
        /// (지금 조건을 만족하는 조건 선택지가 있으면 ×2)로 뽑는다. 후보가 없으면 고요한 층.
        /// </summary>
        void CheckEventPick(string where, RunEvent entered)
        {
            var ev = _run.CurrentEvent;
            if (!Expect(ev != null && entered.EventId == _run.CurrentEventId, $"{where}: 이벤트 층인데 이벤트가 없음 ({entered.EventId})"))
                return;
            Expect(!_run.EventDone && !_run.EventBattlePending, $"{where}: 새 이벤트 층인데 이미 고른 상태");
            int floor = _run.Floor, pos = (floor - 1) % 10 + 1;
            var slot = pos <= 4 ? EventSlots.Early : pos == 6 ? EventSlots.Sixth : pos == 7 || pos == 8 ? EventSlots.Late : EventSlots.None;
            var seen = new HashSet<string>(_run.Events.Where(e => e.Kind == RunEventKind.FloorEntered && e.EventId != null && !ReferenceEquals(e, entered))
                .Select(e => e.EventId));
            var pool = TowerEventList.All.Where(e => (e.Slots & slot) != 0 && floor >= e.MinFloor && (e.MaxFloor == 0 || floor <= e.MaxFloor)
                                                     && (e.DeckId == null || e.DeckId == _run.StartDeckId) && (e.Repeatable || !seen.Contains(e.Id))).ToList();
            int[] tendencyWeights = { 35, 15, 50 };
            var tendencies = new[] { EventTendency.Positive, EventTendency.Neutral, EventTendency.Negative }.Where(t => pool.Any(e => e.Tendency == t)).ToList();
            var expected = TowerEventList.QuietFloor;
            if (tendencies.Count > 0)
            {
                var random = RunRandom.For(_run.Seed, floor, RunRandomPurpose.Event);
                var tendency = Weighted(tendencies, t => tendencyWeights[(int)t], random);
                expected = Weighted(pool.Where(e => e.Tendency == tendency).ToList(),
                    e => e.Weight * (e.Choices.Any(c => c.HasCondition && !c.IsForced && Meets(c, e.Scope, _run.Chips, _run.MainDeck, _run.ExtraDeck, _run.SideDeck)) ? 2 : 1),
                    random);
            }
            Expect(ev == expected, $"{where}: 이벤트 {ev.Id} (다시 뽑으면 {expected.Id}, 후보 {pool.Count}개)");
            _stats.EventTendencies[(int)ev.Tendency]++;
            _stats.EventsSeen[ev.Id] = (_stats.EventsSeen.TryGetValue(ev.Id, out int n) ? n : 0) + 1;
        }

        static T Weighted<T>(List<T> items, Func<T, int> weight, RunRandom random)
        {
            int roll = random.Next(items.Sum(weight));
            foreach (var item in items)
            {
                roll -= weight(item);
                if (roll < 0)
                    return item;
            }
            return items[items.Count - 1];
        }

        /// <summary>
        /// 칩교환소에서 샀다. 봉인 해제: 봉인된 카드 1장을 사이드 덱으로. 봉인 대상 교체: 1장을 풀고(사이드 덱으로) 같은 덱의 다른 카드 1장을 대신 봉인
        /// (같은 레어도 상품이면 같은 레어도, 직접 고르는 상품이면 고른 카드). 칩은 가격만큼, 상품마다 한 번. 메인 덱이 40장 아래가 되면 보충 (런이 끝나지는 않는다).
        /// </summary>
        public void AfterBuy(ShopProduct product, uint release, uint newSeal)
        {
            var b = _before;
            var events = NewEvents(b);
            string where = $"{b.Floor}층 칩교환소 {product} (풀기 {release}{(newSeal != 0 ? $", 새 봉인 {newSeal}" : "")})";
            var offer = b.Shop.FirstOrDefault(o => o.Product == product);
            if (!Expect(b.Phase == RunPhase.Ready && b.Kind == FloorKind.ChipExchange && offer.Price > 0 && !offer.Sold, $"{where}: 살 수 없는 상태에서 삼 ({b.Kind}, {b.Phase})"))
                return;
            int price = PriceTable[(int)product];
            _stats.Bought[(int)product]++;
            _stats.ChipsSpent += price;
            Expect(offer.Price == price && b.Chips >= price, $"{where}: 가격 {offer.Price} (기대 {price}), 가진 칩 {b.Chips}");
            Expect(_run.Chips == b.Chips - price && _run.ChipsEarned == b.ChipsEarned, $"{where}: 칩 {b.Chips} → {_run.Chips} (기대 -{price})");
            Expect(_run.Shop.Select(o => (o.Product, o.Price, o.Sold)).SequenceEqual(b.Shop.Select(o => (o.Product, o.Price, o.Sold || o.Product == product))),
                $"{where}: 산 상품만 팔림으로 바뀌지 않음");
            Expect(_run.Floor == b.Floor && _run.Gold == b.Gold && _run.Lp == b.Lp, $"{where}: 층·골드·LP가 바뀜");
            Expect(_run.Phase == RunPhase.Ready || _run.Phase == RunPhase.NeedsRefill, $"{where}: 산 뒤 상태 {_run.Phase} (런이 끝나는 교환은 팔지 않음)");
            bool fromMain = b.SealedMain.Contains(release);
            Expect(fromMain || b.SealedExtra.Contains(release), $"{where}: 봉인된 카드가 아닌 것을 풂");
            Expect(events.Count >= 2 && events[0].Kind == RunEventKind.ShopBought && events[0].Product == product && events[0].Count == price
                   && events[0].Chips == _run.Chips && events[1].Kind == RunEventKind.CardUnsealed && events[1].Code == release && !events[1].IsBonus,
                $"{where}: 구매·봉인 해제 이벤트가 이상함");
            Expect(SameCards(_run.SideDeck, b.Side.Append(release)), $"{where}: 풀린 카드가 사이드 덱에 더해지지 않음");
            if (product == ShopProduct.Unseal)
            {
                Expect(events.Count == 2, $"{where}: 이벤트 {events.Count}개 (2개여야 함)");
                ExpectUnchanged(b, where, main: true, extra: true, side: false, sealedCards: false);
                Expect(SameCards(_run.SealedMain.Concat(_run.SealedExtra).Append(release), b.SealedMain.Concat(b.SealedExtra)), $"{where}: 봉인 카드에서 1장 빠지지 않음");
            }
            else
            {
                // 이벤트: 구매, 봉인 해제, 봉인 (+ 메인 덱이 40장 아래가 되면 보충 필요).
                var sealedNow = events.Where(e => e.Kind == RunEventKind.CardSealed).ToList();
                bool refill = _run.MainDeck.Count < 40;
                if (!Expect(sealedNow.Count == 1 && events.Count == (refill ? 4 : 3) && events[2].Kind == RunEventKind.CardSealed
                            && (!refill || events[3].Kind == RunEventKind.RefillRequired),
                        $"{where}: 새로 봉인한 이벤트 {sealedNow.Count}개, 이벤트 {events.Count}개 (보충 {refill})"))
                    return;
                uint target = sealedNow[0].Code;
                var pile = fromMain ? b.Main : b.Extra;
                Expect(sealedNow[0].Pile == (fromMain ? RunPile.Main : RunPile.Extra) && pile.Contains(target), $"{where}: 다른 덱의 카드 {target}를 봉인함");
                bool sameRarity = product == ShopProduct.SwapSameRarity || product == ShopProduct.SwapSameRarityChosen;
                if (sameRarity)
                    Expect(RarityOf(target) == RarityOf(release), $"{where}: 레어도가 다른 카드 {target}를 봉인함 ({RarityOf(target)} / {RarityOf(release)})");
                bool chosen = product == ShopProduct.SwapSameRarityChosen || product == ShopProduct.SwapAnyChosen;
                Expect(chosen ? target == newSeal : newSeal == 0, $"{where}: 새로 봉인한 카드 {target}");
                Expect(SameCards((fromMain ? _run.MainDeck : _run.ExtraDeck).Append(target), pile), $"{where}: 덱에서 봉인한 카드 1장만 빠지지 않음");
                Expect(SameCards(fromMain ? _run.ExtraDeck : _run.MainDeck, fromMain ? b.Extra : b.Main), $"{where}: 다른 덱이 바뀜");
                Expect(SameCards(fromMain ? _run.SealedMain : _run.SealedExtra, Without(fromMain ? b.SealedMain : b.SealedExtra, release).Append(target)),
                    $"{where}: 봉인 카드 목록이 맞지 않음");
                Expect(SameCards(fromMain ? _run.SealedExtra : _run.SealedMain, fromMain ? b.SealedExtra : b.SealedMain), $"{where}: 다른 덱의 봉인 카드가 바뀜");
            }
            int needed = 40 - _run.MainDeck.Count;
            Expect(needed <= 0 ? _run.Phase == RunPhase.Ready : _run.Phase == RunPhase.NeedsRefill && _run.RefillNeeded == needed,
                $"{where}: 메인 덱 {_run.MainDeck.Count}장인데 상태 {_run.Phase}");
            if (needed > 0)
                _stats.RefillsRequired++;
            CheckConservation(where);
            CheckDeckRules(where, allowShortMain: _run.Phase == RunPhase.NeedsRefill);
        }

        Rarity? RarityOf(uint code) => _rarities.TryGet(code, out var r) ? r : (Rarity?)null;

        static List<uint> Without(IEnumerable<uint> cards, uint code)
        {
            var list = cards.ToList();
            list.Remove(code);
            return list;
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
            Chips = _run.Chips,
            ChipsEarned = _run.ChipsEarned,
            Phase = _run.Phase,
            Kind = _run.FloorKind,
            EventId = _run.CurrentEventId,
            EventDone = _run.EventDone,
            BattleChoice = _run.PendingEventBattleIndex,
            Offer = _run.PendingReward,
            Shop = _run.Shop.Select(o => (o.Product, o.Price, o.Sold)).ToList(),
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
