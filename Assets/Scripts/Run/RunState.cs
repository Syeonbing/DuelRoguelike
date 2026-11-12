// 런 하나의 상태와 진행 규칙 (규칙 결정 문서 2026-11-10 "탑 구조", "런 진행", "LP 규칙", "승리 보상", "봉인", "재화"). 듀얼을 직접 돌리지 않는다:
// BeginDuel로 다음 듀얼의 설정(덱, LP, 시드)을 내주고, 화면이나 시험이 듀얼을 끝낸 뒤 FinishDuel로 결과를 받는다.
//
// 탑은 10층 묶음이 반복되고 층 번호의 끝자리로 층 종류(FloorKind)가 정해진다 (Tower.cs). 전투 층 = 단판 듀얼 하나.
// 비전투 층(칩교환소·이벤트·휴식)의 일은 RunStateTower.cs에 있다.
// - 전투 층에서 이기면 다음 층으로, 지면(무승부 포함) 같은 층을 새로 뽑은 상대와 다시 한다.
// - 승리: 연패 0, 칩을 받는다. LP는 남은 LP + 1000 (8000을 넘을 수 있다), 보스 층(10층마다)을 이기면 8000으로 맞춘다.
//         층별 확률표로 등급을 뽑고, 상대 덱의 그 등급 카드 중 서로 다른 카드 3장까지를 후보로 보여 준다 (ChoosingReward).
//         플레이어가 ChooseReward로 1장을 고르면 사이드 덱과 컬렉션에 들어간다. 후보가 3장보다 적으면 빈 자리는 골드다.
//         그 등급 카드가 상대 덱에 없으면 골드로 대신 준다 (고르기 없음).
//         보스를 이기면 이어서 봉인된 카드 1장을 골라 봉인 해제한다 (ChoosingUnseal). 봉인된 카드가 없으면 보상 후보를 한 번 더 받는다.
//         보상을 다 받으면 다음 층으로 올라간다 (그때까지 Floor는 이긴 층 그대로).
// - 패배: 연패 +1, 메인 덱에서 "1 + 연패 횟수 + 층÷10"장, 엑스트라 덱에서 20% 연쇄로 봉인. LP는 전부 회복한다. 칩은 잃지 않는다.
//         메인 덱이 40장 미만이면 사이드 덱에서 보충해야 하고(NeedsRefill), 보충할 카드가 모자라면 런이 끝난다.
// - 듀얼 사이에는 사이드 덱과 메인·엑스트라 덱의 카드를 옮길 수 있다. 같은 카드명은 메인·엑스트라 덱에 3장까지 (카드 DB의 alias 기준),
//   사이드 덱에는 제한이 없다. 봉인된 카드는 쓸 수 없다.
// - 듀얼 중에 저장된 런을 읽으면 그 듀얼은 패배로 처리한다 (ForfeitDuel, GameSave.Parse가 부른다).
// 골드는 영구 재화다. 런 안에서 얻은 골드(Gold)와 런 종료 정산(SettlementGold)은 런이 끝나면 GameSave가 누적에 더한다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Run
{
    public enum RunPhase
    {
        /// <summary>듀얼 사이: 다음 듀얼을 시작하거나 사이드 덱과 교환할 수 있다.</summary>
        Ready,
        /// <summary>BeginDuel 뒤, FinishDuel 전.</summary>
        InDuel,
        /// <summary>승리 보상 후보 중 하나를 골라야 한다 (ChooseReward).</summary>
        ChoosingReward,
        /// <summary>메인 덱이 40장 미만이라 사이드 덱에서 보충해야 한다 (MoveToDeck).</summary>
        NeedsRefill,
        /// <summary>보스 보상: 봉인된 카드 1장을 골라 봉인 해제해야 한다 (ChooseUnseal).</summary>
        ChoosingUnseal,
        Ended,
    }

    public enum DuelResult
    {
        Win,
        Loss,
        /// <summary>무승부. 패배로 처리한다.</summary>
        Draw,
    }

    public enum RunEndReason
    {
        None,
        /// <summary>메인 덱이 40장 미만인데 사이드 덱에서 보충할 카드가 모자람</summary>
        DeckBelowMinimum,
        /// <summary>플레이어가 런을 포기함 (탑 메뉴의 "런 포기", 또는 진행 중인 런이 있을 때 새 런을 시작)</summary>
        Abandoned,
    }

    /// <summary>런을 시작할 때 정하는 것.</summary>
    public sealed class RunSetup
    {
        /// <summary>런 시드. 같은 시드와 같은 듀얼 결과·같은 선택이면 상대·보상 후보·봉인이 모두 같다.</summary>
        public ulong Seed = 1;

        /// <summary>시작 덱 (메인 + 엑스트라).</summary>
        public DeckRecipe StartDeck;

        /// <summary>시작 사이드 덱 (최대 RunRules.StartingSideDeckMax장, 시작 덱과 합쳐 같은 카드명 3장까지).</summary>
        public IReadOnlyList<uint> SideDeck = Array.Empty<uint>();

        /// <summary>층마다 무작위로 고를 상대 덱. 첫 버전은 스타터 덱 4개.</summary>
        public IReadOnlyList<DeckRecipe> Opponents = StarterDecks.All;
    }

    /// <summary>
    /// 듀얼 전 코인 토스. 이긴 쪽이 선공·후공을 고른다 (규칙 결정 문서).
    /// 같은 런 시드·듀얼 번호면 결과가 늘 같다 (다시 켜도 바뀌지 않는다).
    /// </summary>
    public sealed class CoinToss
    {
        /// <summary>플레이어가 이겼는지. 이겼으면 플레이어가 선공·후공을 고른다.</summary>
        public bool PlayerWon { get; }

        /// <summary>상대가 이겼을 때 상대가 선공을 고르는지 (플레이어가 이겼으면 쓰지 않는다).</summary>
        public bool OpponentChoosesFirst { get; }

        /// <summary>이 토스에서 플레이어가 이길 확률 (퍼센트, 아이템·보스가 바꾼 값).</summary>
        public int WinPercent { get; }

        internal CoinToss(bool playerWon, bool opponentChoosesFirst, int winPercent)
        {
            PlayerWon = playerWon;
            OpponentChoosesFirst = opponentChoosesFirst;
            WinPercent = winPercent;
        }

        /// <summary>플레이어가 선공인지. playerChoice는 플레이어가 이겼을 때 고른 것 (true = 선공).</summary>
        public bool PlayerGoesFirst(bool playerChoice) => PlayerWon ? playerChoice : !OpponentChoosesFirst;
    }

    /// <summary>
    /// 한 층의 듀얼. 코인 토스로 선후공을 정한다: 선공이 엔진의 팀 0이다 (PlayerTeam).
    /// 상대는 FullLp로 시작한다.
    /// </summary>
    public sealed class RunDuel
    {
        public int DuelNumber { get; }
        public int Floor { get; }
        public bool IsBossFloor => RunRules.IsBossFloor(Floor);
        public DeckRecipe Opponent { get; }

        /// <summary>상대 덱의 카드 (장수만큼, 레시피 순서).</summary>
        public IReadOnlyList<uint> OpponentCards { get; }

        public int PlayerLp { get; }
        public int OpponentLp => RunRules.FullLp;

        /// <summary>엔진에 넣을 나의 메인 + 엑스트라 덱 (BeginDuel 시점의 복사본).</summary>
        public IReadOnlyList<uint> PlayerDeck { get; }

        public IReadOnlyList<ulong> EngineSeed { get; }
        public int DeckShuffleSeed { get; }

        public CoinToss Toss { get; }
        public bool PlayerGoesFirst { get; }

        /// <summary>이벤트 전투인지 (이기면 이벤트 결과만 받고, 이기든 지든 다음 층으로 간다).</summary>
        public bool IsEventBattle { get; }

        /// <summary>엔진에서 플레이어의 팀 (선공 = 0, 후공 = 1).</summary>
        public int PlayerTeam => PlayerGoesFirst ? 0 : 1;

        internal RunDuel(int duelNumber, int floor, DeckRecipe opponent, int playerLp, IReadOnlyList<uint> playerDeck,
            IReadOnlyList<ulong> engineSeed, int deckShuffleSeed, CoinToss toss, bool playerGoesFirst, bool isEventBattle = false)
        {
            DuelNumber = duelNumber;
            Floor = floor;
            Opponent = opponent;
            OpponentCards = opponent.ToCardList();
            PlayerLp = playerLp;
            PlayerDeck = playerDeck;
            EngineSeed = engineSeed;
            DeckShuffleSeed = deckShuffleSeed;
            Toss = toss;
            PlayerGoesFirst = playerGoesFirst;
            IsEventBattle = isEventBattle;
        }

        /// <summary>이 듀얼의 엔진 설정 (덱, 시작 LP, 시드). 팀 0이 선공이다.</summary>
        public DuelSessionOptions CreateSessionOptions()
        {
            var mine = PlayerDeck.ToArray();
            var theirs = OpponentCards.ToArray();
            var options = new DuelSessionOptions
            {
                Seed = EngineSeed.ToArray(),
                Decks = PlayerGoesFirst ? new IReadOnlyList<uint>[] { mine, theirs } : new IReadOnlyList<uint>[] { theirs, mine },
                DeckShuffleSeed = DeckShuffleSeed,
            };
            options.Team1.StartingLP = (uint)(PlayerGoesFirst ? PlayerLp : OpponentLp);
            options.Team2.StartingLP = (uint)(PlayerGoesFirst ? OpponentLp : PlayerLp);
            return options;
        }

        /// <summary>끝난 듀얼의 결과. playerTeam: 엔진에서 플레이어의 팀 (RunDuel.PlayerTeam).</summary>
        public static DuelResult ResultOf(DuelState state, int playerTeam)
        {
            if (!state.IsFinished)
                throw new InvalidOperationException("듀얼이 아직 끝나지 않았습니다.");
            if (state.Winner == playerTeam)
                return DuelResult.Win;
            return state.Winner == OcgConstants.PlayerNone ? DuelResult.Draw : DuelResult.Loss;
        }
    }

    /// <summary>승리 보상 후보 (ChoosingReward일 때). 자리는 RunRules.RewardChoices개: 앞은 카드, 남는 자리는 골드.</summary>
    public sealed class RewardOffer
    {
        /// <summary>이긴 듀얼 번호와 층.</summary>
        public int DuelNumber { get; }
        public int Floor { get; }
        public Rarity Rarity { get; }

        /// <summary>후보 카드 (서로 다른 카드, 1장 이상 RewardChoices장 이하). 자리 0부터 차례로.</summary>
        public IReadOnlyList<uint> Cards { get; }

        /// <summary>빈 자리 하나를 고르면 받는 골드.</summary>
        public int Gold { get; }

        public int EmptySlots => RunRules.RewardChoices - Cards.Count;

        /// <summary>보스 보상: 봉인된 카드가 없어 한 번 더 받는 승리 보상.</summary>
        public bool IsBossBonus { get; }

        internal RewardOffer(int duelNumber, int floor, Rarity rarity, IReadOnlyList<uint> cards, int gold, bool isBossBonus)
        {
            DuelNumber = duelNumber;
            Floor = floor;
            Rarity = rarity;
            Cards = cards;
            Gold = gold;
            IsBossBonus = isBossBonus;
        }

        /// <summary>이 자리가 카드인지 (아니면 골드).</summary>
        public bool IsCard(int slot) => slot >= 0 && slot < Cards.Count;
    }

    /// <summary>런 하나. 저장 파일 읽기·쓰기는 RunStateSave.cs (GameSave가 부른다).</summary>
    public sealed partial class RunState
    {
        readonly CardDatabase _database;
        readonly RarityTable _rarities;
        readonly List<uint> _main = new List<uint>();
        readonly List<uint> _extra = new List<uint>();
        readonly List<uint> _side = new List<uint>();
        readonly List<uint> _sealedMain = new List<uint>();
        readonly List<uint> _sealedExtra = new List<uint>();
        readonly List<RunEvent> _events = new List<RunEvent>();

        public ulong Seed { get; }
        /// <summary>시작 덱(레시피)의 Id. 레시피는 런이 끝나기 전에 지우거나 고칠 수 있으므로, 이름은 StartDeckName에 따로 든다.</summary>
        public string StartDeckId { get; }

        /// <summary>런을 시작할 때의 레시피 이름 (표시용).</summary>
        public string StartDeckName { get; }

        public IReadOnlyList<DeckRecipe> Opponents { get; }

        /// <summary>이름을 모르는 시작 덱(형식 2 이전 저장)의 표시 이름: 스타터 덱이면 그 이름, 아니면 Id.</summary>
        internal static string DefaultDeckName(string id) => StarterDecks.Find(id ?? "")?.Name ?? id;

        /// <summary>런에서 얻은 카드가 더해지는 컬렉션. 없으면(null) 더하지 않는다.</summary>
        public CardCollection Collection { get; }

        public RarityTable Rarities => _rarities;

        public RunPhase Phase { get; private set; } = RunPhase.Ready;
        public RunEndReason EndReason { get; private set; }

        /// <summary>지금 층 (1부터). 이기면 보상을 다 받은 뒤 1 오르고, 지면 그대로다. 비전투 층은 일을 마치면 오른다.</summary>
        public int Floor { get; private set; } = 1;

        /// <summary>지금 층의 종류. 층에 들어갈 때 정한다 (Tower.KindOf).</summary>
        public FloorKind FloorKind { get; private set; }

        /// <summary>가진 칩 (런 재화). 런이 끝나면 사라지고 일부가 골드 정산에 들어간다.</summary>
        public int Chips { get; private set; }

        /// <summary>이 런 동안 얻은 칩의 합 (쓴 칩도 빼지 않는다. 골드 정산에 쓴다).</summary>
        public int ChipsEarned { get; private set; }

        /// <summary>끝난 듀얼 수 = 마지막으로 끝난 듀얼 번호.</summary>
        public int DuelsPlayed { get; private set; }

        public int LosingStreak { get; private set; }

        /// <summary>다음 듀얼에 가져갈 LP (보스 층이면 BeginDuel에서 8000으로 맞춘다).</summary>
        public int Lp { get; private set; } = RunRules.FullLp;

        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Draws { get; private set; }

        /// <summary>이 런에서 얻은 골드 (보상 빈 자리, 등급 대체). 런이 끝나면 GameSave가 누적에 더한다.</summary>
        public int Gold { get; private set; }

        /// <summary>진행 중인 듀얼 (InDuel일 때만).</summary>
        public RunDuel CurrentDuel { get; private set; }

        /// <summary>고르는 중인 승리 보상 (ChoosingReward일 때만).</summary>
        public RewardOffer PendingReward { get; private set; }

        public IReadOnlyList<uint> MainDeck => _main;
        public IReadOnlyList<uint> ExtraDeck => _extra;
        public IReadOnlyList<uint> SideDeck => _side;
        public IReadOnlyList<uint> SealedMain => _sealedMain;
        public IReadOnlyList<uint> SealedExtra => _sealedExtra;

        public IReadOnlyList<RunEvent> Events => _events;

        /// <summary>보충해야 하는 장수 (NeedsRefill일 때만 0보다 크다).</summary>
        public int RefillNeeded => Phase == RunPhase.NeedsRefill ? RunRules.MinMainDeck - _main.Count : 0;

        RunState(RunSetup setup, CardDatabase database, RarityTable rarities, CardCollection collection)
            : this(setup.Seed, setup.StartDeck.Id, setup.StartDeck.Name, setup.Opponents.ToArray(), database, rarities, collection)
        {
        }

        RunState(ulong seed, string startDeckId, string startDeckName, IReadOnlyList<DeckRecipe> opponents, CardDatabase database,
            RarityTable rarities, CardCollection collection)
        {
            Seed = seed;
            StartDeckId = startDeckId;
            StartDeckName = string.IsNullOrEmpty(startDeckName) ? DefaultDeckName(startDeckId) : startDeckName;
            Opponents = opponents;
            _database = database;
            _rarities = rarities;
            Collection = collection;
        }

        /// <summary>런을 시작할 수 있는지 확인한다. 문제가 없으면 빈 목록. collection이 null이면 컬렉션 장수는 보지 않는다.</summary>
        public static List<string> CheckSetup(RunSetup setup, CardDatabase database, CardCollection collection)
        {
            var problems = new List<string>();
            if (setup.StartDeck == null)
            {
                problems.Add("시작 덱이 없음");
                return problems;
            }
            problems.AddRange(setup.StartDeck.Validate(database));
            var side = setup.SideDeck ?? Array.Empty<uint>();
            if (side.Count > RunRules.StartingSideDeckMax)
                problems.Add($"사이드 덱 {side.Count}장 ({RunRules.StartingSideDeckMax}장 이하여야 함)");
            foreach (uint code in side.Distinct())
                if (!database.TryGet(code, out _))
                    problems.Add($"사이드 덱의 {code}: 카드 DB에 없음");
            // 런을 시작할 때는 시작 덱과 사이드 덱을 합쳐 같은 카드명 3장까지 (덱만으로 넘친 것은 Validate가 이미 적었다).
            var sideNames = new HashSet<uint>(side.Select(database.NameCode));
            foreach (var group in setup.StartDeck.ToCardList().Concat(side).GroupBy(database.NameCode))
                if (sideNames.Contains(group.Key) && group.Count() > RunRules.MaxCopies)
                    problems.Add($"{database.GetName(group.Key)}: 시작 덱과 사이드 덱을 합쳐 같은 카드명 {group.Count()}장 ({RunRules.MaxCopies}장까지)");
            if (setup.Opponents == null || setup.Opponents.Count == 0)
                problems.Add("상대 덱 목록이 비어 있음");
            if (collection != null)
                problems.AddRange(collection.CheckCards(setup.StartDeck.ToCardList().Concat(side), database));
            return problems;
        }

        /// <summary>런을 시작한다. 시작할 수 없으면 ArgumentException (CheckSetup의 문제 목록).</summary>
        public static RunState Start(RunSetup setup, CardDatabase database, RarityTable rarities, CardCollection collection = null)
        {
            var problems = CheckSetup(setup, database, collection);
            if (problems.Count > 0)
                throw new ArgumentException("런을 시작할 수 없습니다: " + string.Join("; ", problems));
            var run = new RunState(setup, database, rarities, collection);
            foreach (uint code in setup.StartDeck.ToCardList())
                (run.IsExtraDeckCard(code) ? run._extra : run._main).Add(code);
            run._side.AddRange(setup.SideDeck ?? Array.Empty<uint>());
            run.AddEvent(RunEventKind.RunStarted, 0, e =>
            {
                e.DeckId = setup.StartDeck.Id;
                e.DeckName = run.StartDeckName;
                e.MainCount = run._main.Count;
                e.ExtraCount = run._extra.Count;
                e.SideCount = run._side.Count;
            });
            run.EnterFloor();
            return run;
        }

        /// <summary>
        /// 지금 층의 듀얼을 시작한다 (전투 층·보스 층에서 Ready일 때만). 상대 덱과 엔진 시드를 정한다.
        /// 듀얼이 끝나면 FinishDuel을 부른다.
        /// </summary>
        /// <param name="playerFirst">코인 토스에 이겼을 때 플레이어가 고른 것 (true = 선공). 상대가 이겼으면 쓰지 않는다.</param>
        public RunDuel BeginDuel(bool playerFirst = true)
        {
            RequirePhase(RunPhase.Ready, "듀얼을 시작할");
            if (!Tower.IsBattle(FloorKind) && !EventBattlePending)
                throw new InvalidOperationException($"듀얼을 시작할 수 없습니다: {Floor}층은 {Tower.KindName(FloorKind)} 층");
            int number = DuelsPlayed + 1;
            var toss = CoinTossFor(number);
            CurrentDuel = CreateDuel(number, toss.PlayerGoesFirst(playerFirst));
            Phase = RunPhase.InDuel;
            AddEvent(RunEventKind.DuelStarted, number, e =>
            {
                e.DeckId = CurrentDuel.Opponent.Id;
                e.Lp = Lp;
                e.Coin = toss.PlayerWon ? CoinTossWinner.Player : CoinTossWinner.Opponent;
                e.Turn = CurrentDuel.PlayerGoesFirst ? TurnOrder.First : TurnOrder.Second;
            });
            return CurrentDuel;
        }

        /// <summary>지금 층 듀얼의 코인 토스 (Ready인 전투 층에서). 화면이 BeginDuel 전에 결과를 보여 주고 선후공을 고르게 한다.</summary>
        public CoinToss NextCoinToss()
        {
            RequirePhase(RunPhase.Ready, "코인 토스를 할");
            if (!Tower.IsBattle(FloorKind) && !EventBattlePending)
                throw new InvalidOperationException($"코인 토스를 할 수 없습니다: {Floor}층은 {Tower.KindName(FloorKind)} 층");
            return CoinTossFor(DuelsPlayed + 1);
        }

        /// <summary>코인 토스에서 플레이어가 이길 확률 (퍼센트): 기본값 + 적용한 아이템 (보스·이벤트가 바꿀 자리는 아직 없음).</summary>
        public int CoinTossWinPercent => Math.Min(100, RunRules.CoinTossWinPercent + ItemBonus(ItemEffectKind.CoinTossWin));

        /// <summary>듀얼 번호의 코인 토스 (런 시드와 번호로 정해진다).</summary>
        CoinToss CoinTossFor(int number)
        {
            var random = RunRandom.For(Seed, number, RunRandomPurpose.CoinToss);
            int percent = Math.Max(0, Math.Min(100, CoinTossWinPercent));
            bool won = random.Next(100) < percent;
            bool opponentFirst = random.Next(100) < RunRules.AiChoosesFirstPercent;
            return new CoinToss(won, opponentFirst, percent);
        }

        /// <summary>듀얼 번호로 상대와 엔진 시드를 정한다 (같은 런 시드·번호면 늘 같다). 저장 파일의 "듀얼 중" 런을 읽을 때도 쓴다.</summary>
        internal RunDuel CreateDuel(int number, bool playerGoesFirst)
        {
            var opponent = EventBattlePending ? StarterDecks.Find(PendingEventBattle.BattleDeckId) : OpponentFor(number);
            var random = RunRandom.For(Seed, number, RunRandomPurpose.Duel);
            var engineSeed = new ulong[4];
            for (int i = 0; i < engineSeed.Length; i++)
                engineSeed[i] = random.NextULong();
            if (engineSeed.All(s => s == 0))
                engineSeed[0] = 1; // 엔진은 시드가 모두 0이면 듀얼을 만들지 못한다
            int shuffleSeed = random.NextSeed();
            return new RunDuel(number, Floor, opponent, Lp, _main.Concat(_extra).ToArray(), engineSeed, shuffleSeed,
                CoinTossFor(number), playerGoesFirst, EventBattlePending);
        }

        /// <summary>듀얼 번호의 상대 덱 (런 시드와 번호로 정해진다).</summary>
        DeckRecipe OpponentFor(int number) => Opponents[RunRandom.For(Seed, number, RunRandomPurpose.Opponent).Next(Opponents.Count)];

        /// <summary>끝난 듀얼의 결과를 반영한다. 플레이어의 팀은 선후공에 따라 다르다 (RunDuel.PlayerTeam).</summary>
        public void FinishDuel(DuelState state)
        {
            int team = CurrentDuel?.PlayerTeam ?? 0;
            FinishDuel(RunDuel.ResultOf(state, team), state.Players[team].Lp);
        }

        /// <summary>듀얼 결과를 반영한다 (InDuel에서만). playerLp는 듀얼이 끝났을 때 플레이어의 LP.</summary>
        public void FinishDuel(DuelResult result, int playerLp)
        {
            RequirePhase(RunPhase.InDuel, "듀얼을 끝낼");
            if (result == DuelResult.Win && playerLp <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerLp), playerLp, "이긴 듀얼의 LP는 1 이상이어야 합니다.");
            var duel = CurrentDuel;
            CurrentDuel = null;
            DuelsPlayed = duel.DuelNumber;
            Phase = RunPhase.Ready;
            if (duel.IsEventBattle)
            {
                FinishEventBattle(duel, result, playerLp);
                return;
            }

            if (result == DuelResult.Win)
            {
                Wins++;
                LosingStreak = 0;
                AddEvent(RunEventKind.DuelWon, duel.DuelNumber, e => e.Lp = playerLp);
                Lp = playerLp;
                if (duel.IsBossFloor)
                {
                    if (Lp != RunRules.FullLp)
                        ChangeLp(RunRules.FullLp, LpRestoreReason.BossCleared, duel.DuelNumber);
                }
                else
                    ChangeLp(playerLp + RunRules.WinLpBonus, LpRestoreReason.Win, duel.DuelNumber);
                GainChips(duel.IsBossFloor ? RunRules.ChipsPerBossWin : RunRules.ChipsPerWin, duel.DuelNumber);
                _bossBonusPending = duel.IsBossFloor;
                OfferReward(duel.DuelNumber, duel.Floor, duel.OpponentCards, bossBonus: false);
                ContinueAfterWin();
                return;
            }

            ApplyLoss(duel, result, playerLp);
            CheckMainDeckSize();
        }

        /// <summary>패배(무승부 포함): 연패 +1, 봉인, LP 전부 회복. 전투 층과 이벤트 전투가 같이 쓴다.</summary>
        void ApplyLoss(RunDuel duel, DuelResult result, int playerLp)
        {
            if (result == DuelResult.Draw)
                Draws++;
            else
                Losses++;
            LosingStreak++;
            AddEvent(RunEventKind.DuelLost, duel.DuelNumber, e =>
            {
                e.Result = result;
                e.Count = LosingStreak;
            });
            Seal(duel);
            // 패배하면 전부 회복. 회복은 LP를 낮추지 않는다 (덱 아웃 등으로 FullLp보다 높은 LP로 졌으면 그대로).
            // 엔진 LP는 0 아래로 내려갈 수 있어서 0으로 맞춰 기록한다.
            Lp = Math.Max(0, playerLp);
            if (playerLp < RunRules.FullLp)
                ChangeLp(RunRules.FullLp, LpRestoreReason.Loss, duel.DuelNumber);
        }

        /// <summary>
        /// 진행 중인 듀얼을 패배로 끝낸다 (InDuel에서만). 듀얼 도중에 게임이 꺼져 저장 파일에 듀얼 중으로 남은 런을 읽었을 때 쓴다.
        /// LP 0으로 진 것으로 친다 (보통 패배처럼 FullLp로 회복한다. 시작 LP가 FullLp보다 높았어도 그 LP를 남기지 않는다).
        /// </summary>
        public void ForfeitDuel()
        {
            RequirePhase(RunPhase.InDuel, "듀얼을 패배로 처리할");
            AddEvent(RunEventKind.DuelForfeited, CurrentDuel.DuelNumber, null);
            FinishDuel(DuelResult.Loss, 0);
        }

        /// <summary>
        /// 승리 보상 후보 중 하나를 고른다 (ChoosingReward에서만). slot이 카드 자리면 그 카드를 사이드 덱과 컬렉션에 넣고,
        /// 빈 자리면 골드를 받는다. 보스 보상이 남았으면 이어서 그것을, 아니면 다음 층으로 올라간다.
        /// </summary>
        public void ChooseReward(int slot)
        {
            RequirePhase(RunPhase.ChoosingReward, "보상을 고를");
            if (slot < 0 || slot >= RunRules.RewardChoices)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, $"보상 자리는 0~{RunRules.RewardChoices - 1}입니다.");
            var offer = PendingReward;
            PendingReward = null;
            Phase = RunPhase.Ready;
            if (offer.IsCard(slot))
            {
                uint card = offer.Cards[slot];
                _side.Add(card);
                Collection?.Add(card);
                AddEvent(RunEventKind.RewardGained, offer.DuelNumber, e =>
                {
                    e.Code = card;
                    e.Rarity = offer.Rarity;
                }, offer.Floor);
            }
            else
            {
                Gold += offer.Gold;
                AddEvent(RunEventKind.RewardGold, offer.DuelNumber, e =>
                {
                    e.Rarity = offer.Rarity;
                    e.Count = offer.Gold;
                }, offer.Floor);
            }
            ContinueAfterWin();
        }

        /// <summary>사이드 덱의 카드를 메인·엑스트라 덱(카드 종류에 따라)으로 옮길 수 있는지. 보충 중에는 메인 덱 카드만.</summary>
        public bool CanMoveToDeck(uint code, out string reason)
        {
            reason = null;
            if (Phase != RunPhase.Ready && Phase != RunPhase.NeedsRefill)
                reason = PhaseReason();
            else if (!_side.Contains(code))
                reason = "사이드 덱에 없는 카드";
            else if (IsExtraDeckCard(code) && Phase == RunPhase.NeedsRefill)
                reason = "보충할 때는 메인 덱 카드만 넣을 수 있음";
            else if (IsExtraDeckCard(code) && _extra.Count >= RunRules.MaxExtraDeck)
                reason = $"엑스트라 덱이 {RunRules.MaxExtraDeck}장으로 가득 참";
            else if (!IsExtraDeckCard(code) && _main.Count >= RunRules.MaxMainDeck)
                reason = $"메인 덱이 {RunRules.MaxMainDeck}장으로 가득 참";
            else if (DeckCopies(code) >= RunRules.MaxCopies)
                reason = $"같은 카드명은 덱에 {RunRules.MaxCopies}장까지";
            return reason == null;
        }

        /// <summary>사이드 덱 → 메인·엑스트라 덱. 보충 중이면 메인 덱이 40장이 되는 순간 Ready로 돌아간다.</summary>
        public void MoveToDeck(uint code)
        {
            if (!CanMoveToDeck(code, out string reason))
                throw new InvalidOperationException($"{_database.GetName(code)}을(를) 덱에 넣을 수 없습니다: {reason}");
            bool refill = Phase == RunPhase.NeedsRefill;
            bool extra = IsExtraDeckCard(code);
            _side.Remove(code);
            (extra ? _extra : _main).Add(code);
            AddEvent(RunEventKind.MovedToDeck, DuelsPlayed, e =>
            {
                e.Code = code;
                e.Pile = extra ? RunPile.Extra : RunPile.Main;
                e.IsRefill = refill;
            });
            if (refill && _main.Count >= RunRules.MinMainDeck)
                Phase = RunPhase.Ready;
        }

        /// <summary>메인·엑스트라 덱의 카드를 사이드 덱으로 옮길 수 있는지 (Ready에서만, 메인 덱은 40장 아래로 줄이지 않는다).</summary>
        public bool CanMoveToSide(uint code, out string reason)
        {
            reason = null;
            if (Phase != RunPhase.Ready)
                reason = PhaseReason();
            else if (_main.Contains(code))
            {
                if (_main.Count <= RunRules.MinMainDeck)
                    reason = $"메인 덱은 {RunRules.MinMainDeck}장 아래로 줄일 수 없음 (먼저 사이드 덱에서 넣기)";
                else if (IsBound(code))
                    reason = "귀속: 봉인될 때까지 메인 덱에서 뺄 수 없음";
            }
            else if (!_extra.Contains(code))
                reason = "메인·엑스트라 덱에 없는 카드";
            return reason == null;
        }

        public void MoveToSide(uint code)
        {
            if (!CanMoveToSide(code, out string reason))
                throw new InvalidOperationException($"{_database.GetName(code)}을(를) 사이드 덱으로 옮길 수 없습니다: {reason}");
            bool fromMain = _main.Remove(code);
            if (!fromMain)
                _extra.Remove(code);
            _side.Add(code);
            AddEvent(RunEventKind.MovedToSide, DuelsPlayed, e =>
            {
                e.Code = code;
                e.Pile = fromMain ? RunPile.Main : RunPile.Extra;
            });
            ReleaseEmptyItems();
        }

        /// <summary>
        /// 런을 포기한다 (듀얼 사이에만: Ready, ChoosingReward, NeedsRefill, ChoosingUnseal). 런 종료로 처리한다:
        /// 이미 얻은 카드는 컬렉션에 남고, 얻은 골드와 정산 골드는 GameSave가 누적에 더한다. 고르는 중이던 보상은 받지 않는다.
        /// </summary>
        public void Abandon()
        {
            if (!CanAbandon)
                throw new InvalidOperationException($"런을 포기할 수 없습니다: 지금 상태는 {Phase} (듀얼 사이에만 포기할 수 있음)");
            EndRun(RunEndReason.Abandoned);
        }

        /// <summary>지금 런을 포기할 수 있는지 (듀얼 사이: Ready, ChoosingReward, NeedsRefill, ChoosingUnseal).</summary>
        public bool CanAbandon =>
            Phase == RunPhase.Ready || Phase == RunPhase.ChoosingReward || Phase == RunPhase.NeedsRefill || Phase == RunPhase.ChoosingUnseal;

        /// <summary>보충할 때 고를 수 있는 사이드 덱 카드 (같은 카드는 한 번, 사이드 덱 순서). NeedsRefill이 아니면 빈 목록.</summary>
        public List<uint> RefillCandidates()
        {
            if (Phase != RunPhase.NeedsRefill)
                return new List<uint>();
            return _side.Distinct().Where(code => CanMoveToDeck(code, out _)).ToList();
        }

        /// <summary>사이드 덱에서 메인 덱으로 지금 옮길 수 있는 최대 장수 (메인 덱 카드만, 같은 카드명 3장 제한).</summary>
        public int RefillableCount() => RefillableCount(_side, _main.Count);

        /// <summary>side가 사이드 덱이고 메인 덱이 mainCount장일 때 보충할 수 있는 장수 (같은 카드명 장수는 지금 덱 기준).</summary>
        int RefillableCount(IEnumerable<uint> side, int mainCount)
        {
            int total = 0;
            foreach (var group in side.Where(code => !IsExtraDeckCard(code)).GroupBy(NameCode))
                total += Math.Max(0, Math.Min(group.Count(), RunRules.MaxCopies - DeckCopies(group.Key)));
            return Math.Min(total, RunRules.MaxMainDeck - mainCount);
        }

        /// <summary>엑스트라 덱에 들어가는 카드인지 (카드 DB의 종류로 판단).</summary>
        public bool IsExtraDeckCard(uint code) => _database.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

        /// <summary>같은 카드명으로 치는 기준 코드 (카드 DB의 alias, 없으면 자기 코드).</summary>
        public uint NameCode(uint code) => _database.NameCode(code);

        /// <summary>메인·엑스트라 덱에 든 같은 카드명의 장수 (에라타 전·후 버전, 카드명 취급 카드도 합친다).</summary>
        public int DeckCopies(uint code)
        {
            uint name = NameCode(code);
            return _main.Count(c => NameCode(c) == name) + _extra.Count(c => NameCode(c) == name);
        }

        /// <summary>승리 보상 (bossBonus면 보스 보상으로 한 번 더 주는 것: 난수를 따로 쓴다).</summary>
        void OfferReward(int duelNumber, int floor, IReadOnlyList<uint> opponentCards, bool bossBonus)
        {
            var random = RunRandom.For(Seed, duelNumber, bossBonus ? RunRandomPurpose.BossBonusReward : RunRandomPurpose.Reward);
            var rarity = RewardTable.Roll(floor, random);
            int gold = RunRules.GoldFor(rarity);
            // 등급은 지금 표에서 찾는다 (표가 바뀌면 다음 보상부터 바로 반영된다).
            var pool = opponentCards.Where(code => _rarities.TryGet(code, out var r) && r == rarity).Distinct().ToList();
            if (pool.Count == 0)
            {
                Gold += gold;
                AddEvent(RunEventKind.RewardSubstituted, duelNumber, e =>
                {
                    e.Rarity = rarity;
                    e.Count = gold;
                    e.IsBonus = bossBonus;
                }, floor);
                return;
            }
            var cards = new List<uint>();
            while (cards.Count < RunRules.RewardChoices && pool.Count > 0)
            {
                int index = random.Next(pool.Count);
                cards.Add(pool[index]);
                pool.RemoveAt(index);
            }
            PendingReward = new RewardOffer(duelNumber, floor, rarity, cards, gold, bossBonus);
            Phase = RunPhase.ChoosingReward;
            AddEvent(RunEventKind.RewardOffered, duelNumber, e =>
            {
                e.Rarity = rarity;
                e.Codes = cards.ToArray();
                e.Count = PendingReward.EmptySlots > 0 ? gold : 0;
                e.IsBonus = bossBonus;
            }, floor);
        }

        void Seal(RunDuel duel)
        {
            var random = RunRandom.For(Seed, duel.DuelNumber, RunRandomPurpose.Seal);
            int mainCount = Math.Min(RunRules.MainSealCount(LosingStreak, duel.Floor), _main.Count);
            for (int i = 0; i < mainCount; i++)
                SealOne(_main, _sealedMain, RunPile.Main, random, duel.DuelNumber);
            int extraCount = RunRules.RollExtraSealCount(random, _extra.Count);
            for (int i = 0; i < extraCount; i++)
                SealOne(_extra, _sealedExtra, RunPile.Extra, random, duel.DuelNumber);
        }

        void SealOne(List<uint> from, List<uint> to, RunPile pile, RunRandom random, int duelNumber)
        {
            int index = random.Next(from.Count);
            uint code = from[index];
            from.RemoveAt(index);
            to.Add(code);
            AddEvent(RunEventKind.CardSealed, duelNumber, e =>
            {
                e.Code = code;
                e.Pile = pile;
            });
            ReleaseEmptyItems();
        }

        void ChangeLp(int lp, LpRestoreReason reason, int duelNumber)
        {
            int previous = Lp;
            Lp = lp;
            AddEvent(RunEventKind.LpRestored, duelNumber, e =>
            {
                e.PreviousLp = previous;
                e.Lp = lp;
                e.LpReason = reason;
            });
        }

        void CheckMainDeckSize()
        {
            int needed = RunRules.MinMainDeck - _main.Count;
            if (needed <= 0)
                return;
            if (RefillableCount() < needed)
            {
                EndRun(RunEndReason.DeckBelowMinimum);
                return;
            }
            Phase = RunPhase.NeedsRefill;
            AddEvent(RunEventKind.RefillRequired, DuelsPlayed, e => e.Count = needed);
        }

        void RequirePhase(RunPhase phase, string action)
        {
            if (Phase != phase)
                throw new InvalidOperationException($"{action} 수 없습니다: 지금 상태는 {Phase} (필요: {phase})");
        }

        string PhaseReason() =>
            Phase == RunPhase.InDuel ? "듀얼 중에는 덱을 바꿀 수 없음"
            : Phase == RunPhase.Ended ? "런이 끝났음"
            : Phase == RunPhase.NeedsRefill ? "보충을 먼저 끝내야 함"
            : Phase == RunPhase.ChoosingReward ? "승리 보상을 먼저 골라야 함"
            : Phase == RunPhase.ChoosingUnseal ? "보스 보상(봉인 해제)을 먼저 골라야 함"
            : "지금은 덱을 바꿀 수 없음";

        void AddEvent(RunEventKind kind, int duelNumber, Action<RunEvent> fill, int? floor = null)
        {
            var e = new RunEvent { Kind = kind, DuelNumber = duelNumber, Floor = floor ?? Floor };
            fill?.Invoke(e);
            _events.Add(e);
        }
    }
}
