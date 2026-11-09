// 런 하나의 상태와 진행 규칙 (설계 문서 "로그라이크 진행"). 듀얼을 직접 돌리지 않는다:
// BeginDuel로 다음 듀얼의 설정(덱, LP, 시드)을 내주고, 화면이나 시험이 듀얼을 끝낸 뒤 FinishDuel로 결과를 받는다.
//
// 한 층 = 단판 듀얼 하나. 이기면 다음 층으로, 지면(무승부 포함) 같은 층을 새로 뽑은 상대와 다시 한다.
// - 승리: 연패 0, 층별 확률표로 등급을 뽑아 상대 덱의 그 등급 카드 1장을 사이드 덱에 넣는다 (컬렉션에도 +1).
//         그 등급 카드가 상대 덱에 없으면 포인트로 대체한다 (지금은 등급별 횟수만 센다). LP는 남은 LP로 이어 간다.
// - 패배: 연패 +1, 메인 덱에서 "연패 횟수 + 층÷10"장, 엑스트라 덱에서 20% 연쇄로 기절. LP는 전부 회복한다.
//         메인 덱이 40장 미만이면 사이드 덱에서 보충해야 하고(NeedsRefill), 보충할 카드가 모자라면 런이 끝난다.
// - 보스 층(10층마다) 듀얼 직전에 LP를 전부 회복한다.
// - 듀얼 사이에는 사이드 덱과 메인·엑스트라 덱의 카드를 옮길 수 있다 (덱 규칙 유지, 기절한 카드는 쓸 수 없다).
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
        /// <summary>메인 덱이 40장 미만이라 사이드 덱에서 보충해야 한다 (MoveToDeck).</summary>
        NeedsRefill,
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
        /// <summary>플레이어가 런을 포기함 (진행 중인 런이 있을 때 새 런을 시작)</summary>
        Abandoned,
    }

    /// <summary>런을 시작할 때 정하는 것.</summary>
    public sealed class RunSetup
    {
        /// <summary>런 시드. 같은 시드와 같은 듀얼 결과면 상대·보상·기절이 모두 같다.</summary>
        public ulong Seed = 1;

        /// <summary>시작 덱 (메인 + 엑스트라).</summary>
        public DeckRecipe StartDeck;

        /// <summary>시작 사이드 덱 (최대 RunRules.StartingSideDeckMax장).</summary>
        public IReadOnlyList<uint> SideDeck = Array.Empty<uint>();

        /// <summary>층마다 무작위로 고를 상대 덱. 첫 버전은 스타터 덱 4개.</summary>
        public IReadOnlyList<DeckRecipe> Opponents = StarterDecks.All;
    }

    /// <summary>한 층의 듀얼. 플레이어는 늘 팀 0(선공)이고, 상대는 FullLp로 시작한다.</summary>
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

        internal RunDuel(int duelNumber, int floor, DeckRecipe opponent, int playerLp, IReadOnlyList<uint> playerDeck,
            IReadOnlyList<ulong> engineSeed, int deckShuffleSeed)
        {
            DuelNumber = duelNumber;
            Floor = floor;
            Opponent = opponent;
            OpponentCards = opponent.ToCardList();
            PlayerLp = playerLp;
            PlayerDeck = playerDeck;
            EngineSeed = engineSeed;
            DeckShuffleSeed = deckShuffleSeed;
        }

        /// <summary>이 듀얼의 엔진 설정 (덱, 시작 LP, 시드).</summary>
        public DuelSessionOptions CreateSessionOptions()
        {
            var options = new DuelSessionOptions
            {
                Seed = EngineSeed.ToArray(),
                Decks = new IReadOnlyList<uint>[] { PlayerDeck.ToArray(), OpponentCards.ToArray() },
                DeckShuffleSeed = DeckShuffleSeed,
            };
            options.Team1.StartingLP = (uint)PlayerLp;
            options.Team2.StartingLP = (uint)OpponentLp;
            return options;
        }

        /// <summary>끝난 듀얼의 결과 (플레이어 = 팀 0).</summary>
        public static DuelResult ResultOf(DuelState state)
        {
            if (!state.IsFinished)
                throw new InvalidOperationException("듀얼이 아직 끝나지 않았습니다.");
            if (state.Winner == 0)
                return DuelResult.Win;
            return state.Winner == OcgConstants.PlayerNone ? DuelResult.Draw : DuelResult.Loss;
        }
    }

    /// <summary>런 하나. 저장 파일 읽기·쓰기는 RunStateSave.cs (GameSave가 부른다).</summary>
    public sealed partial class RunState
    {
        readonly CardDatabase _database;
        readonly RarityTable _rarities;
        readonly List<uint> _main = new List<uint>();
        readonly List<uint> _extra = new List<uint>();
        readonly List<uint> _side = new List<uint>();
        readonly List<uint> _faintedMain = new List<uint>();
        readonly List<uint> _faintedExtra = new List<uint>();
        readonly List<RunEvent> _events = new List<RunEvent>();
        readonly int[] _substituted = new int[RarityTable.RarityCount];

        public ulong Seed { get; }
        public string StartDeckId { get; }
        public IReadOnlyList<DeckRecipe> Opponents { get; }

        /// <summary>런에서 얻은 카드가 더해지는 컬렉션. 없으면(null) 더하지 않는다.</summary>
        public CardCollection Collection { get; }

        public RarityTable Rarities => _rarities;

        public RunPhase Phase { get; private set; } = RunPhase.Ready;
        public RunEndReason EndReason { get; private set; }

        /// <summary>지금 층 (1부터). 이기면 1 오르고, 지면 그대로다.</summary>
        public int Floor { get; private set; } = 1;

        /// <summary>끝난 듀얼 수 = 마지막으로 끝난 듀얼 번호.</summary>
        public int DuelsPlayed { get; private set; }

        public int LosingStreak { get; private set; }

        /// <summary>다음 듀얼에 가져갈 LP (보스 층이면 BeginDuel에서 회복한다).</summary>
        public int Lp { get; private set; } = RunRules.FullLp;

        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Draws { get; private set; }

        /// <summary>진행 중인 듀얼 (InDuel일 때만).</summary>
        public RunDuel CurrentDuel { get; private set; }

        public IReadOnlyList<uint> MainDeck => _main;
        public IReadOnlyList<uint> ExtraDeck => _extra;
        public IReadOnlyList<uint> SideDeck => _side;
        public IReadOnlyList<uint> FaintedMain => _faintedMain;
        public IReadOnlyList<uint> FaintedExtra => _faintedExtra;

        /// <summary>등급별 포인트 대체 횟수 (지급량은 미정).</summary>
        public IReadOnlyList<int> SubstitutedRewards => _substituted;

        public IReadOnlyList<RunEvent> Events => _events;

        /// <summary>보충해야 하는 장수 (NeedsRefill일 때만 0보다 크다).</summary>
        public int RefillNeeded => Phase == RunPhase.NeedsRefill ? RunRules.MinMainDeck - _main.Count : 0;

        RunState(RunSetup setup, CardDatabase database, RarityTable rarities, CardCollection collection)
            : this(setup.Seed, setup.StartDeck.Id, setup.Opponents.ToArray(), database, rarities, collection)
        {
        }

        RunState(ulong seed, string startDeckId, IReadOnlyList<DeckRecipe> opponents, CardDatabase database, RarityTable rarities,
            CardCollection collection)
        {
            Seed = seed;
            StartDeckId = startDeckId;
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
                e.MainCount = run._main.Count;
                e.ExtraCount = run._extra.Count;
                e.SideCount = run._side.Count;
            });
            return run;
        }

        /// <summary>
        /// 지금 층의 듀얼을 시작한다 (Ready에서만). 보스 층이면 먼저 LP를 회복하고, 상대 덱과 엔진 시드를 정한다.
        /// 듀얼이 끝나면 FinishDuel을 부른다.
        /// </summary>
        public RunDuel BeginDuel()
        {
            RequirePhase(RunPhase.Ready, "듀얼을 시작할");
            int number = DuelsPlayed + 1;
            if (RunRules.IsBossFloor(Floor) && Lp < RunRules.FullLp)
                RestoreLp(RunRules.FullLp, LpRestoreReason.BossFloor, number);

            var opponent = Opponents[RunRandom.For(Seed, number, RunRandomPurpose.Opponent).Next(Opponents.Count)];
            var random = RunRandom.For(Seed, number, RunRandomPurpose.Duel);
            var engineSeed = new ulong[4];
            for (int i = 0; i < engineSeed.Length; i++)
                engineSeed[i] = random.NextULong();
            if (engineSeed.All(s => s == 0))
                engineSeed[0] = 1; // 엔진은 시드가 모두 0이면 듀얼을 만들지 못한다
            int shuffleSeed = random.NextSeed();

            CurrentDuel = new RunDuel(number, Floor, opponent, Lp, _main.Concat(_extra).ToArray(), engineSeed, shuffleSeed);
            Phase = RunPhase.InDuel;
            AddEvent(RunEventKind.DuelStarted, number, e =>
            {
                e.DeckId = opponent.Id;
                e.Lp = Lp;
            });
            return CurrentDuel;
        }

        /// <summary>끝난 듀얼의 결과를 반영한다. 플레이어는 팀 0이다.</summary>
        public void FinishDuel(DuelState state) => FinishDuel(RunDuel.ResultOf(state), state.Players[0].Lp);

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

            if (result == DuelResult.Win)
            {
                Wins++;
                LosingStreak = 0;
                Lp = playerLp;
                AddEvent(RunEventKind.DuelWon, duel.DuelNumber, e => e.Lp = playerLp);
                GrantReward(duel);
                Floor++;
                return;
            }

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
            Faint(duel);
            // 패배하면 전부 회복. 회복은 LP를 낮추지 않는다 (덱 아웃 등으로 FullLp보다 높은 LP로 졌으면 그대로).
            // 엔진 LP는 0 아래로 내려갈 수 있어서 0으로 맞춰 기록한다.
            Lp = Math.Max(0, playerLp);
            if (playerLp < RunRules.FullLp)
                RestoreLp(RunRules.FullLp, LpRestoreReason.Loss, duel.DuelNumber);
            CheckMainDeckSize();
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
                reason = $"같은 카드는 덱에 {RunRules.MaxCopies}장까지";
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
        }

        /// <summary>
        /// 런을 포기한다 (듀얼 사이에만: Ready, NeedsRefill). 런 종료로 처리한다:
        /// 이미 얻은 카드는 컬렉션에 남고, 포인트 대체는 GameSave가 누적에 더한다.
        /// </summary>
        public void Abandon()
        {
            if (Phase != RunPhase.Ready && Phase != RunPhase.NeedsRefill)
                throw new InvalidOperationException($"런을 포기할 수 없습니다: 지금 상태는 {Phase} (듀얼 사이에만 포기할 수 있음)");
            Phase = RunPhase.Ended;
            EndReason = RunEndReason.Abandoned;
            AddEvent(RunEventKind.RunEnded, DuelsPlayed, e => e.EndReason = EndReason);
        }

        /// <summary>보충할 때 고를 수 있는 사이드 덱 카드 (같은 카드는 한 번, 사이드 덱 순서). NeedsRefill이 아니면 빈 목록.</summary>
        public List<uint> RefillCandidates()
        {
            if (Phase != RunPhase.NeedsRefill)
                return new List<uint>();
            return _side.Distinct().Where(code => CanMoveToDeck(code, out _)).ToList();
        }

        /// <summary>사이드 덱에서 메인 덱으로 지금 옮길 수 있는 최대 장수 (메인 덱 카드만, 같은 카드 3장 제한).</summary>
        public int RefillableCount()
        {
            int total = 0;
            foreach (var group in _side.Where(code => !IsExtraDeckCard(code)).GroupBy(code => code))
                total += Math.Max(0, Math.Min(group.Count(), RunRules.MaxCopies - DeckCopies(group.Key)));
            return Math.Min(total, RunRules.MaxMainDeck - _main.Count);
        }

        /// <summary>엑스트라 덱에 들어가는 카드인지 (카드 DB의 종류로 판단).</summary>
        public bool IsExtraDeckCard(uint code) => _database.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

        /// <summary>메인·엑스트라 덱에 든 같은 카드 장수.</summary>
        public int DeckCopies(uint code) => _main.Count(c => c == code) + _extra.Count(c => c == code);

        void GrantReward(RunDuel duel)
        {
            var random = RunRandom.For(Seed, duel.DuelNumber, RunRandomPurpose.Reward);
            var rarity = RewardTable.Roll(duel.Floor, random);
            // 등급은 지금 표에서 찾는다 (표가 바뀌면 다음 보상부터 바로 반영된다).
            var candidates = duel.OpponentCards.Where(code => _rarities.TryGet(code, out var r) && r == rarity).ToList();
            if (candidates.Count == 0)
            {
                _substituted[(int)rarity]++;
                AddEvent(RunEventKind.RewardSubstituted, duel.DuelNumber, e => e.Rarity = rarity, duel.Floor);
                return;
            }
            uint card = candidates[random.Next(candidates.Count)];
            _side.Add(card);
            Collection?.Add(card);
            AddEvent(RunEventKind.RewardGained, duel.DuelNumber, e =>
            {
                e.Code = card;
                e.Rarity = rarity;
            }, duel.Floor);
        }

        void Faint(RunDuel duel)
        {
            var random = RunRandom.For(Seed, duel.DuelNumber, RunRandomPurpose.Faint);
            int mainCount = Math.Min(RunRules.MainFaintCount(LosingStreak, duel.Floor), _main.Count);
            for (int i = 0; i < mainCount; i++)
                FaintOne(_main, _faintedMain, RunPile.Main, random, duel.DuelNumber);
            int extraCount = RunRules.RollExtraFaintCount(random, _extra.Count);
            for (int i = 0; i < extraCount; i++)
                FaintOne(_extra, _faintedExtra, RunPile.Extra, random, duel.DuelNumber);
        }

        void FaintOne(List<uint> from, List<uint> to, RunPile pile, RunRandom random, int duelNumber)
        {
            int index = random.Next(from.Count);
            uint code = from[index];
            from.RemoveAt(index);
            to.Add(code);
            AddEvent(RunEventKind.CardFainted, duelNumber, e =>
            {
                e.Code = code;
                e.Pile = pile;
            });
        }

        void RestoreLp(int lp, LpRestoreReason reason, int duelNumber)
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
                Phase = RunPhase.Ended;
                EndReason = RunEndReason.DeckBelowMinimum;
                AddEvent(RunEventKind.RunEnded, DuelsPlayed, e => e.EndReason = EndReason);
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
            : "지금은 덱을 바꿀 수 없음";

        void AddEvent(RunEventKind kind, int duelNumber, Action<RunEvent> fill, int? floor = null)
        {
            var e = new RunEvent { Kind = kind, DuelNumber = duelNumber, Floor = floor ?? Floor };
            fill?.Invoke(e);
            _events.Add(e);
        }
    }
}
