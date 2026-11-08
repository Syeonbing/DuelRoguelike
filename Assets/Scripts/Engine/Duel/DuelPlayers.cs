// 요청에 답하는 쪽: 사람(화면에서 고름)과 임시 자동 응답(상대 AI가 들어오기 전까지, 개발 단계 6에서 바꾼다).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Duel
{
    /// <summary>체인할지 묻는 방식 (설계: 설정에서 고를 수 있게 한다).</summary>
    public enum ChainPromptMode
    {
        /// <summary>EDOPro 기본과 같다: 이 타이밍에 발동할 수 있는 효과가 있다고 엔진이 알릴 때(특정 타이밍), 강제·유발 선택일 때만 묻는다.</summary>
        Smart,
        /// <summary>발동할 수 있는 효과가 하나라도 있으면 묻는다.</summary>
        Always,
        /// <summary>강제·유발 선택이 아니면 묻지 않는다.</summary>
        Never,
    }

    /// <summary>카드를 놓을 칸(MSG_SELECT_PLACE)·고를 칸(MSG_SELECT_DISFIELD)을 묻는 방식.</summary>
    public enum PlaceSelectMode
    {
        /// <summary>묻지 않고 첫 빈칸을 고른다 (자기 몬스터 존 → 자기 마법·함정 존 → 상대 쪽).</summary>
        Auto,
        /// <summary>
        /// 고르는 칸이 결과를 바꿀 수 있을 때만 묻는다: 무효로 할 칸·지정할 칸(MSG_SELECT_DISFIELD),
        /// 후보에 상대 쪽 칸이 있을 때, 몬스터 존과 마법·함정 존이 함께 후보일 때.
        /// 자기 몬스터 존끼리, 자기 마법·함정 존끼리 고르는 경우는 첫 빈칸을 고른다.
        /// </summary>
        Smart,
        /// <summary>고를 수 있는 칸이 고를 칸 수보다 많으면 언제나 묻는다.</summary>
        Always,
    }

    /// <summary>
    /// 사람이 화면에서 답한다. 고를 것이 없거나 의미가 적은 요청만 자동으로 답한다:
    /// 묻지 않아도 되는 체인 확인(ChainPrompt), 묻지 않아도 되는 칸 고르기(PlaceSelect, 첫 빈칸), 한 장뿐인 카드 순서 정하기.
    /// </summary>
    public sealed class ManualPlayer : IDuelPlayer
    {
        /// <summary>MSG_SELECT_CHAIN의 SpecialCount가 이 값이면 유발 효과를 고르는 중이다.</summary>
        const byte SelectTrigger = 0x7f;

        public ChainPromptMode ChainPrompt = ChainPromptMode.Smart;
        public PlaceSelectMode PlaceSelect = PlaceSelectMode.Smart;

        public static bool ShouldAskPlace(SelectPlaceRequest r, PlaceSelectMode mode)
        {
            var places = DuelResponses.AvailablePlaces(r);
            if (places.Count <= Math.Max(r.Count, 1))
                return false; // 고를 것이 없다
            switch (mode)
            {
                case PlaceSelectMode.Auto: return false;
                case PlaceSelectMode.Always: return true;
                default:
                    return r.IsDisableField
                           || places.Any(p => p.Player != r.Player)
                           || places.Any(p => p.Location != places[0].Location);
            }
        }

        public static bool ShouldAskChain(SelectChainRequest r, ChainPromptMode mode)
        {
            if (r.Forced || r.SpecialCount == SelectTrigger)
                return true;
            if (r.Chains.Count == 0)
                return false;
            switch (mode)
            {
                case ChainPromptMode.Always: return true;
                case ChainPromptMode.Never: return false;
                default: return r.SpecialCount != 0;
            }
        }

        public byte[] Respond(DuelRequest request, DuelState state)
        {
            switch (request)
            {
                case SelectChainRequest r when !ShouldAskChain(r, ChainPrompt):
                    return DuelResponses.NoChain();
                case SelectPlaceRequest r when !ShouldAskPlace(r, PlaceSelect):
                    return SimpleAutoPlayer.FirstPlaces(r);
                case SortCardRequest r when r.Cards.Count <= 1:
                    return DuelResponses.DefaultSort(); // 한 장이면 순서가 하나뿐이다
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 임시 자동 응답. 소환할 수 있으면 소환하고, 공격할 수 있으면 공격하고, 나머지는 첫 번째(또는 무작위) 선택지를 고른다.
    /// ActivateEffects를 켜면 마법·함정 발동, 세트, 체인도 한다 (자동 시험용).
    /// </summary>
    public sealed class SimpleAutoPlayer : IDuelPlayer
    {
        const int MaxActivationsPerTurn = 6;
        const int MaxAttacksPerTurn = 10;

        readonly Random _random;
        readonly bool _activateEffects;
        int _turnSeen = -1;
        int _activations;
        int _attacks;

        public SimpleAutoPlayer(bool activateEffects = false, int? seed = null)
        {
            _activateEffects = activateEffects;
            _random = seed.HasValue ? new Random(seed.Value) : null;
        }

        public byte[] Respond(DuelRequest request, DuelState state)
        {
            if (state.Turn != _turnSeen)
            {
                _turnSeen = state.Turn;
                _activations = 0;
                _attacks = 0;
            }

            switch (request)
            {
                case SelectIdleCmdRequest r: return Idle(r, state);
                case SelectBattleCmdRequest r: return Battle(r);
                case SelectChainRequest r:
                    if (r.Chains.Count > 0 && (r.Forced || (_activateEffects && _activations < MaxActivationsPerTurn && Chance(0.5))))
                    {
                        _activations++;
                        return DuelResponses.Chain(Pick(r.Chains.Count));
                    }
                    return DuelResponses.NoChain();
                case SelectEffectYesNoRequest _: return DuelResponses.YesNo(true);
                case SelectYesNoRequest _: return DuelResponses.YesNo(true);
                case SelectOptionRequest r: return DuelResponses.Option(Pick(r.Options.Count));
                case SelectCardRequest r:
                {
                    int count = Math.Min(r.Max, Math.Max(r.Min, 1));
                    return DuelResponses.Cards(PickIndices(r.Cards.Count, count));
                }
                case SelectTributeRequest r: return Tribute(r);
                case SelectUnselectCardRequest r:
                    if (r.Finishable || (r.Selectable.Count == 0 && r.Cancelable))
                        return DuelResponses.FinishSelectUnselect();
                    return DuelResponses.SelectUnselect(r.Selectable.Count > 0 ? Pick(r.Selectable.Count) : r.Selectable.Count);
                case SelectPlaceRequest r: return FirstPlaces(r);
                case SelectPositionRequest r: return DuelResponses.Position(PreferredPosition(r.Positions));
                case SelectCounterRequest r: return Counters(r);
                case SelectSumRequest r: return Sum(r);
                case SortCardRequest _: return DuelResponses.DefaultSort();
                case AnnounceRaceRequest r: return DuelResponses.AnnounceRace(LowestBits(r.Available, r.Count));
                case AnnounceAttributeRequest r: return DuelResponses.AnnounceAttribute((uint)LowestBits(r.Available, r.Count));
                case AnnounceNumberRequest r: return DuelResponses.AnnounceNumber(Pick(r.Options.Count));
                case AnnounceCardRequest _:
                    // 선언 조건 해석은 아직 없다. 엔진이 거절하면 DuelSession이 반복 한도에서 멈춘다.
                    return DuelResponses.AnnounceCard(0);
                case RockPaperScissorsRequest _: return DuelResponses.RockPaperScissors(1 + Pick(3));
                default:
                    throw new NotSupportedException($"자동 응답이 없는 요청: {request.Type}");
            }
        }

        byte[] Idle(SelectIdleCmdRequest r, DuelState state)
        {
            if (_activateEffects && r.Activatable.Count > 0 && _activations < MaxActivationsPerTurn && Chance(0.6))
            {
                _activations++;
                return DuelResponses.IdleActivate(Pick(r.Activatable.Count));
            }
            if (r.Summonable.Count > 0)
                return DuelResponses.Summon(Pick(r.Summonable.Count));
            if (r.MonsterSetable.Count > 0 && Chance(0.5))
                return DuelResponses.MonsterSet(Pick(r.MonsterSetable.Count));
            if (_activateEffects && r.SpellSetable.Count > 0 && Chance(0.3))
                return DuelResponses.SpellSet(Pick(r.SpellSetable.Count));
            if (r.CanBattlePhase && HasAttackPositionMonster(state, r.Player))
                return DuelResponses.ToBattlePhase();
            if (r.CanEndPhase)
                return DuelResponses.IdleToEndPhase();
            return DuelResponses.ToBattlePhase();
        }

        byte[] Battle(SelectBattleCmdRequest r)
        {
            if (r.Attackers.Count > 0 && _attacks < MaxAttacksPerTurn)
            {
                _attacks++;
                return DuelResponses.Attack(Pick(r.Attackers.Count));
            }
            if (r.CanEndPhase)
                return DuelResponses.BattleToEndPhase();
            return DuelResponses.ToMain2();
        }

        static bool HasAttackPositionMonster(DuelState state, byte player) =>
            state.Players[player].MonsterZones.Any(c => c != null && (c.Position & OcgPosition.FaceUpAttack) != 0);

        byte[] Tribute(SelectTributeRequest r)
        {
            var order = PickIndices(r.Cards.Count, r.Cards.Count);
            var chosen = new List<int>();
            int total = 0;
            foreach (int i in order)
            {
                if (total >= r.Min || chosen.Count >= r.Max)
                    break;
                chosen.Add(i);
                total += r.Cards[i].ReleaseParam;
            }
            return DuelResponses.Cards(chosen);
        }

        static byte[] Counters(SelectCounterRequest r)
        {
            int left = r.Count;
            var counts = new List<int>();
            foreach (var card in r.Cards)
            {
                int take = Math.Min(left, card.Count);
                counts.Add(take);
                left -= take;
            }
            return DuelResponses.Counters(counts);
        }

        static byte[] Sum(SelectSumRequest r) =>
            DuelResponses.Cards(DuelSelectionRules.FindSum(r) ?? new List<int>());

        /// <summary>앞에서부터 빈칸을 골라 응답을 만든다 (자기 몬스터 존 → 자기 마법·함정 존 → 상대 쪽).</summary>
        public static byte[] FirstPlaces(SelectPlaceRequest r)
        {
            var places = DuelResponses.AvailablePlaces(r);
            return DuelResponses.Places(places.Take(Math.Max(r.Count, 1)).ToList());
        }

        static OcgPosition PreferredPosition(OcgPosition allowed)
        {
            foreach (var p in new[] { OcgPosition.FaceUpAttack, OcgPosition.FaceDownDefense, OcgPosition.FaceUpDefense, OcgPosition.FaceDownAttack })
                if ((allowed & p) != 0)
                    return p;
            return OcgPosition.FaceUpAttack;
        }

        static ulong LowestBits(ulong available, int count)
        {
            ulong result = 0;
            for (int bit = 0; bit < 64 && count > 0; bit++)
            {
                ulong b = 1UL << bit;
                if ((available & b) != 0)
                {
                    result |= b;
                    count--;
                }
            }
            return result;
        }

        int Pick(int count) => count <= 1 || _random == null ? 0 : _random.Next(count);

        bool Chance(double p) => _random == null ? p >= 0.5 : _random.NextDouble() < p;

        List<int> PickIndices(int total, int count)
        {
            var indices = Enumerable.Range(0, total).ToList();
            if (_random != null)
            {
                for (int i = indices.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (indices[i], indices[j]) = (indices[j], indices[i]);
                }
            }
            return indices.Take(Math.Min(count, total)).ToList();
        }
    }
}
