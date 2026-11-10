// 런 상태: 이벤트 층 (규칙 결정 문서 "이벤트 시스템", "이벤트 전투").
// - 이벤트 층에 들어가면 성향을 먼저 뽑고(긍정 35 : 무성향 15 : 부정 50), 그 성향 안에서 자리·깊이·덱·반복이 맞는 이벤트를
//   가중치로 고른다. 조건을 만족하는 선택지가 있는 이벤트는 가중치 × RunRules.EventConditionMultiplier.
// - 선택지는 조건(카드 보유·칩)을 만족해야 고를 수 있다. 모두 잠기면 강제 선택지가 있으면 그것만, 없으면 그냥 지나간다(-1).
// - 결과를 적용하고 다음 층으로 간다. 결과로 메인 덱이 40장 아래가 되면 보충한 뒤 LeaveFloor로 간다.
// - 이벤트 전투: 고르면 그 층에서 정해진 상대와 듀얼한다. 이기면 선택지의 결과만 받고(일반 승리 보상·칩 없음, LP는 전투 층과 같음),
//   지면 일반 패배처럼 봉인된다. 어느 쪽이든 같은 층을 다시 하지 않고 다음 층으로 간다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public sealed partial class RunState
    {
        /// <summary>지금 이벤트 층의 이벤트를 끝냈는지 (결과를 받았거나 이벤트 전투가 끝남). 보충하느라 층에 남았을 때 true.</summary>
        public bool EventDone { get; private set; }

        /// <summary>고른 이벤트 전투 선택지 (아직 듀얼 전이거나 듀얼 중). 없으면 -1.</summary>
        int _eventBattleChoice = -1;

        public bool EventBattlePending => _eventBattleChoice >= 0;

        /// <summary>고른 이벤트 전투 선택지 (없으면 null).</summary>
        public EventChoice PendingEventBattle => EventBattlePending ? CurrentEvent?.Choices[_eventBattleChoice] : null;

        public int PendingEventBattleIndex => _eventBattleChoice;

        // ---------------- 고르기 ----------------

        /// <summary>10층 묶음 안의 자리 (1~4층 Early, 6층 Sixth, 7~8층 Late, 나머지 None).</summary>
        public static EventSlots SlotOf(int floor)
        {
            int pos = (floor - 1) % RunRules.BossFloorInterval + 1;
            return pos <= 4 ? EventSlots.Early : pos == 6 ? EventSlots.Sixth : pos == 7 || pos == 8 ? EventSlots.Late : EventSlots.None;
        }

        /// <summary>이번 런에서 이미 나온 이벤트 Id.</summary>
        HashSet<string> SeenEvents() =>
            new HashSet<string>(_events.Where(e => e.Kind == RunEventKind.FloorEntered && e.EventId != null).Select(e => e.EventId));

        /// <summary>지금 층에 나올 수 있는 이벤트인지 (자리·깊이·덱·반복).</summary>
        bool IsEligible(TowerEvent ev, HashSet<string> seen)
        {
            if ((ev.Slots & SlotOf(Floor)) == 0 || Floor < ev.MinFloor || (ev.MaxFloor > 0 && Floor > ev.MaxFloor))
                return false;
            if (ev.DeckId != null && ev.DeckId != StartDeckId)
                return false;
            return ev.Repeatable || !seen.Contains(ev.Id);
        }

        /// <summary>
        /// 이벤트 층에 들어갈 때 이벤트를 고른다. 들어가는 순간의 상태(덱·칩·이미 나온 이벤트)를 보고 고르므로 시드만으로 미리 정해지지 않는다.
        /// 같은 상태·같은 시드·같은 층이면 같은 이벤트가 나온다 (시험의 재현용).
        /// </summary>
        TowerEvent PickEvent()
        {
            var seen = SeenEvents();
            var pool = TowerEventList.All.Where(ev => IsEligible(ev, seen)).ToList();
            var random = RunRandom.For(Seed, Floor, RunRandomPurpose.Event);
            var tendencies = Enum.GetValues(typeof(EventTendency)).Cast<EventTendency>()
                .Where(t => pool.Any(ev => ev.Tendency == t)).ToList();
            if (tendencies.Count == 0)
                return TowerEventList.QuietFloor;
            var tendency = WeightedPick(tendencies, t => RunRules.EventTendencyWeights[(int)t], random);
            var candidates = pool.Where(ev => ev.Tendency == tendency).ToList();
            return WeightedPick(candidates, EventWeight, random);
        }

        /// <summary>이벤트 고르기 가중치: 기본 가중치, 조건을 만족하는 선택지가 있으면 × EventConditionMultiplier.</summary>
        public int EventWeight(TowerEvent ev) =>
            ev.Weight * (ev.Choices.Any(c => c.HasCondition && !c.IsForced && Satisfies(c, ev.Scope)) ? RunRules.EventConditionMultiplier : 1);

        static T WeightedPick<T>(IReadOnlyList<T> items, Func<T, int> weight, RunRandom random)
        {
            int total = items.Sum(weight);
            int roll = random.Next(total);
            foreach (var item in items)
            {
                roll -= weight(item);
                if (roll < 0)
                    return item;
            }
            return items[items.Count - 1];
        }

        // ---------------- 조건 ----------------

        /// <summary>보유 범위의 카드 (같은 카드는 장수만큼).</summary>
        public IEnumerable<uint> HeldCards(HoldScope scope)
        {
            IEnumerable<uint> cards = _main.Concat(_extra);
            if (scope != HoldScope.Deck)
                cards = cards.Concat(_side);
            return cards;
        }

        /// <summary>선택지의 조건을 만족하는지 (카드 보유, 칩).</summary>
        public bool Satisfies(EventChoice choice, HoldScope scope) =>
            Chips >= choice.MinChips && (choice.Requires == null || choice.Requires.CountIn(HeldCards(scope), _database) >= choice.Requires.Count);

        /// <summary>강제 선택지를 뺀 선택지가 모두 잠겼는지.</summary>
        public bool AllEventChoicesLocked
        {
            get
            {
                var ev = CurrentEvent;
                return ev != null && ev.Choices.Where(c => !c.IsForced).All(c => !Satisfies(c, ev.Scope));
            }
        }

        /// <summary>
        /// 이 선택지를 지금 고를 수 있는지. index -1은 "그냥 지나간다" (모든 선택지가 잠겼고 강제 선택지가 없을 때만).
        /// 못 고르면 reason에 까닭 (잠긴 선택지면 조건).
        /// </summary>
        public bool CanChooseEventOption(int index, out string reason)
        {
            reason = null;
            var ev = CurrentEvent;
            if (Phase != RunPhase.Ready)
                reason = PhaseReason();
            else if (FloorKind != FloorKind.Event || ev == null)
                reason = $"{Floor}층은 이벤트 층이 아닙니다";
            else if (EventDone || EventBattlePending)
                reason = "이 층의 이벤트는 이미 골랐습니다";
            else if (index == -1)
            {
                if (!AllEventChoicesLocked || ev.Choices.Any(c => c.IsForced))
                    reason = "고를 수 있는 선택지가 있습니다";
            }
            else if (index < 0 || index >= ev.Choices.Count)
                reason = $"선택지는 0~{ev.Choices.Count - 1}입니다";
            else if (ev.Choices[index].IsForced)
            {
                if (!AllEventChoicesLocked)
                    reason = "다른 선택지를 고를 수 있을 때는 나오지 않는 선택지입니다";
            }
            else if (!Satisfies(ev.Choices[index], ev.Scope))
                reason = "조건: " + ConditionText(ev.Choices[index], ev.Scope);
            return reason == null;
        }

        /// <summary>"드래곤족 몬스터 2장 이상 (덱·사이드 덱), 칩 30 이상"</summary>
        public static string ConditionText(EventChoice choice, HoldScope scope)
        {
            var parts = new List<string>();
            if (choice.Requires != null)
                parts.Add(choice.Requires.Describe(scope));
            if (choice.MinChips > 0)
                parts.Add($"칩 {choice.MinChips} 이상");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// 시험용: 지금 이벤트 층의 이벤트를 바꾼다 (층에 들어간 기록도 같이). 고르기 전에만.
        /// 바꾼 런은 같은 시드로 다시 만들면 같아지지 않는다 (재현 시험에는 쓰지 않는다).
        /// </summary>
        internal void ReplaceEventForTest(string id)
        {
            var ev = TowerEventList.Find(id) ?? throw new ArgumentException($"모르는 이벤트 {id}", nameof(id));
            if (Phase != RunPhase.Ready || FloorKind != FloorKind.Event || EventDone || EventBattlePending)
                throw new InvalidOperationException("이벤트를 고르기 전의 이벤트 층이 아닙니다");
            CurrentEventId = ev.Id;
            _events.Last(e => e.Kind == RunEventKind.FloorEntered).EventId = ev.Id;
        }

        // ---------------- 고른 뒤 ----------------

        /// <summary>
        /// 이벤트의 선택지를 고른다 (Ready인 이벤트 층에서). index -1은 그냥 지나간다 (CanChooseEventOption 참고).
        /// 이벤트 전투면 그 층에 남아 듀얼을 기다린다 (BeginDuel). 아니면 결과를 적용하고, 보충할 일이 없으면 다음 층으로 간다.
        /// </summary>
        public void ChooseEventOption(int index)
        {
            if (!CanChooseEventOption(index, out string reason))
                throw new InvalidOperationException($"이벤트 선택지 {index}를 고를 수 없습니다: {reason}");
            var ev = CurrentEvent;
            AddEvent(RunEventKind.EventResolved, DuelsPlayed, e =>
            {
                e.EventId = ev.Id;
                e.Count = index;
            });
            var choice = index >= 0 ? ev.Choices[index] : null;
            if (choice != null && choice.IsBattle)
            {
                _eventBattleChoice = index;
                return;
            }
            EventDone = true;
            if (choice != null)
                ApplyEventEffects(choice, index);
            if (Phase == RunPhase.Ready)
                AdvanceFloor();
        }

        /// <summary>선택지의 결과를 차례로 적용한다. 무작위(봉인할 카드, 잃을 카드, 풀 카드)는 런 시드·층·선택지로 정한다.</summary>
        void ApplyEventEffects(EventChoice choice, int index)
        {
            var random = RunRandom.For(Seed, Floor * 8 + index + 1, RunRandomPurpose.EventEffect);
            foreach (var effect in choice.Effects)
            {
                switch (effect.Kind)
                {
                    case EventEffectKind.Chips:
                        if (effect.Amount > 0)
                            GainChips(effect.Amount, DuelsPlayed);
                        else
                            LoseChips(-effect.Amount);
                        break;
                    case EventEffectKind.Lp:
                        int lp = Math.Max(1, Lp + effect.Amount);
                        if (lp != Lp)
                            ChangeLp(lp, LpRestoreReason.Event, DuelsPlayed);
                        break;
                    case EventEffectKind.Gold:
                        Gold += effect.Amount;
                        AddEvent(RunEventKind.EventGold, DuelsPlayed, e => e.Count = effect.Amount);
                        break;
                    case EventEffectKind.GainCard:
                        _side.Add(effect.Card);
                        Collection?.Add(effect.Card);
                        AddEvent(RunEventKind.EventCardGained, DuelsPlayed, e => e.Code = effect.Card);
                        break;
                    case EventEffectKind.Seal:
                        for (int i = 0; i < effect.Amount && _main.Count > 0; i++)
                            SealOne(_main, _sealedMain, RunPile.Main, random, DuelsPlayed);
                        break;
                    case EventEffectKind.Unseal:
                        var sealedCards = SealedCards();
                        if (sealedCards.Count > 0)
                            Unseal(sealedCards[random.Next(sealedCards.Count)], bossReward: false);
                        else
                            GainChips(effect.Amount, DuelsPlayed);
                        break;
                    case EventEffectKind.LoseCard:
                        LoseMatching(effect.Target, CurrentEvent?.Scope ?? HoldScope.DeckAndSide, random);
                        break;
                }
            }
            CheckMainDeckSize();
        }

        void LoseChips(int amount)
        {
            int lost = Math.Min(amount, Chips);
            if (lost <= 0)
                return;
            Chips -= lost;
            AddEvent(RunEventKind.ChipsLost, DuelsPlayed, e =>
            {
                e.Count = lost;
                e.Chips = Chips;
            });
        }

        /// <summary>조건에 맞는 카드 1장을 이번 런에서 잃는다 (보유 범위 안에서 무작위: 메인 → 엑스트라 → 사이드 순으로 센 목록).</summary>
        void LoseMatching(CardCondition target, HoldScope scope, RunRandom random)
        {
            var piles = new List<(List<uint> Pile, RunPile Kind)> { (_main, RunPile.Main), (_extra, RunPile.Extra) };
            if (scope != HoldScope.Deck)
                piles.Add((_side, RunPile.Side));
            var candidates = piles.SelectMany(p => p.Pile.Select((code, i) => (p.Pile, p.Kind, Index: i, Code: code)))
                .Where(c => target.Matches(c.Code, _database)).ToList();
            if (candidates.Count == 0)
                return;
            var pick = candidates[random.Next(candidates.Count)];
            pick.Pile.RemoveAt(pick.Index);
            AddEvent(RunEventKind.CardLost, DuelsPlayed, e =>
            {
                e.Code = pick.Code;
                e.Pile = pick.Kind;
            });
        }

        /// <summary>이벤트 전투가 끝났다: 이기면 선택지 결과(일반 보상 없음), 지면 일반 패배처럼 봉인. 어느 쪽이든 다음 층으로 (보충이 필요하면 그 뒤).</summary>
        void FinishEventBattle(RunDuel duel, DuelResult result, int playerLp)
        {
            int index = _eventBattleChoice;
            var choice = PendingEventBattle;
            _eventBattleChoice = -1;
            EventDone = true;
            if (result == DuelResult.Win)
            {
                Wins++;
                LosingStreak = 0;
                AddEvent(RunEventKind.DuelWon, duel.DuelNumber, e => e.Lp = playerLp);
                Lp = playerLp;
                ChangeLp(playerLp + RunRules.WinLpBonus, LpRestoreReason.Win, duel.DuelNumber);
                ApplyEventEffects(choice, index);
            }
            else
            {
                ApplyLoss(duel, result, playerLp);
                CheckMainDeckSize();
            }
            if (Phase == RunPhase.Ready)
                AdvanceFloor();
        }
    }
}
