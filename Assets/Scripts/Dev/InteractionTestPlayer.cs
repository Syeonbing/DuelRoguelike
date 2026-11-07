// 화면 조작 모델(DuelInteraction)만으로 답하는 시험용 플레이어.
// 화면과 똑같이 "카드 메뉴의 행동 / 가운데 줄 버튼 / 카드 고르기 / 칸 고르기 / 대화창"으로만 고른다.
// 이렇게 듀얼이 끝까지 가면, 화면에서 사람이 할 수 없는 선택이 없다는 뜻이다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.UI;

namespace DuelRoguelike.Dev
{
    public sealed class InteractionTestPlayer : IDuelPlayer
    {
        const int MaxActionsPerTurn = 30;

        readonly DuelText _text;
        readonly Random _random;
        // 칸 고르기는 항상 묻게 해서, 소환·세트할 때마다 화면의 칸 고르기 모델을 거치게 한다
        // (시험 덱에는 상대 쪽 칸·무효로 할 칸을 고르는 효과가 거의 없어 Smart로는 시험되지 않는다).
        readonly ManualPlayer _manual = new ManualPlayer { PlaceSelect = PlaceSelectMode.Always };

        /// <summary>칸 고르기(MSG_SELECT_PLACE / MSG_SELECT_DISFIELD)를 조작 모델로 답한 횟수.</summary>
        public int PlacesAnswered { get; private set; }
        int _turn = -1;
        int _actions;

        /// <summary>조작 모델로 답하지 못해 자동 응답으로 대신한 횟수 (0이어야 한다).</summary>
        public int Fallbacks { get; private set; }

        /// <summary>조작 모델로 답한 횟수.</summary>
        public int Answered { get; private set; }

        public readonly List<string> FallbackReasons = new List<string>();

        public InteractionTestPlayer(DuelText text, int seed)
        {
            _text = text;
            _random = new Random(seed);
        }

        public byte[] Respond(DuelRequest request, DuelState state)
        {
            // 화면도 ManualPlayer가 자동으로 답하는 요청(체인 확인 일부, 고를 것이 없는 칸 고르기, 순서 정하기)은 묻지 않는다.
            byte[] auto = _manual.Respond(request, state);
            if (auto != null)
                return auto;
            if (state.Turn != _turn)
            {
                _turn = state.Turn;
                _actions = 0;
            }

            var it = DuelInteraction.Build(request, state, _text, isRetry: false);
            byte[] response = Choose(it);
            if (response == null)
            {
                Fallbacks++;
                FallbackReasons.Add(request.Type.ToString());
                return new SimpleAutoPlayer().Respond(request, state);
            }
            Answered++;
            if (it.Places != null)
                PlacesAnswered++;
            return response;
        }

        byte[] Choose(DuelInteraction it)
        {
            if (it.Dialog != null)
            {
                var d = it.Dialog;
                if (d.Toggles != null && d.Toggles.Count >= d.ToggleCount)
                {
                    ulong mask = d.Toggles.Take(d.ToggleCount).Aggregate(0UL, (m, t) => m | t.Bit);
                    return d.BuildToggles(mask);
                }
                if (d.AskCardCode)
                    return null;
                return d.Buttons.Count > 0 ? d.Buttons[_random.Next(d.Buttons.Count)].Response : null;
            }

            if (it.Selection != null)
                return Select(it);

            if (it.Places != null)
                return Place(it.Places);

            // 카드 메뉴의 행동과 가운데 줄 버튼 중에서 고른다. 한 턴에 너무 많이 움직이면 페이즈를 넘긴다.
            var choices = it.Actions.Values.SelectMany(a => a).Select(a => a.Response).ToList();
            var buttons = it.Buttons.Select(b => b.Response).ToList();
            _actions++;
            if (choices.Count > 0 && (_actions < MaxActionsPerTurn || buttons.Count == 0) && _random.NextDouble() < 0.7)
                return choices[_random.Next(choices.Count)];
            if (buttons.Count > 0)
                return buttons[_random.Next(buttons.Count)];
            return choices.Count > 0 ? choices[_random.Next(choices.Count)] : null;
        }

        byte[] Select(DuelInteraction it)
        {
            var sel = it.Selection;
            var order = sel.Candidates.OrderBy(_ => _random.Next()).ToList();
            if (sel.Immediate)
            {
                // 고를 수 있는 만큼 고른 뒤 끝내기 버튼이 있으면 누른다.
                int selected = sel.Candidates.Count(c => c.Selected);
                if (it.Buttons.Count > 0 && selected >= sel.Min)
                    return it.Buttons[0].Response;
                var next = order.FirstOrDefault(c => !c.Selected) ?? order.FirstOrDefault();
                return next == null ? null : sel.Click(next);
            }
            foreach (var c in order)
            {
                byte[] r = sel.Click(c);
                if (r != null)
                    return r;
                if (sel.CanConfirm && (sel.Chosen.Count >= sel.Max || _random.NextDouble() < 0.5))
                    return sel.Confirm();
            }
            if (sel.CanConfirm)
                return sel.Confirm();
            return sel.CancelResponse;
        }

        /// <summary>화면처럼 강조된 칸을 무작위로 눌러 Count칸을 채운다. 여러 칸이면 확인을 누른다.</summary>
        byte[] Place(PlaceSelection places)
        {
            foreach (var zone in places.Candidates.OrderBy(_ => _random.Next()).ToList())
            {
                byte[] r = places.Click(zone);
                if (r != null)
                    return r;
                if (places.CanConfirm)
                    return places.Confirm();
            }
            return null;
        }
    }
}
