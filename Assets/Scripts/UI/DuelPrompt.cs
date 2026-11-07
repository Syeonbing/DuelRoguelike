// 선택 패널: 엔진의 선택 요청을 버튼으로 보여 주고, 고른 결과를 응답 바이트로 넘긴다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    internal sealed class DuelPrompt
    {
        readonly Label _title;
        readonly Label _detail;
        readonly VisualElement _options;
        readonly DuelText _text;
        readonly Action<byte[]> _respond;
        readonly Func<DuelRequest, byte[]> _auto;

        public DuelPrompt(VisualElement container, DuelText text, Action<byte[]> respond, Func<DuelRequest, byte[]> auto)
        {
            _text = text;
            _respond = respond;
            _auto = auto;
            _title = UiStyle.Label("", 19, UiStyle.Accent, bold: true);
            _detail = UiStyle.Label("", 15, UiStyle.TextDim);
            _detail.style.marginTop = 4;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 6;
            _options = scroll.contentContainer;
            container.Add(_title);
            container.Add(_detail);
            container.Add(scroll);
        }

        public void ShowText(string title, string detail = "")
        {
            _title.text = title;
            _detail.text = detail;
            _options.Clear();
        }

        public void AddButton(string label, Action onClick) => _options.Add(UiStyle.Button(label, onClick));

        public void Show(DuelRequest request, DuelState state, bool isRetry)
        {
            _options.Clear();
            _detail.text = isRetry ? "앞의 선택은 받아들여지지 않았다. 다시 고른다." : "";
            switch (request)
            {
                case SelectIdleCmdRequest r: Idle(r, state); break;
                case SelectBattleCmdRequest r: Battle(r, state); break;
                case SelectChainRequest r: Chain(r, state); break;
                case SelectEffectYesNoRequest r:
                    YesNo($"「{_text.CardName(r.Code)}」의 효과를 발동할까요?", _text.Description(r.Description));
                    break;
                case SelectYesNoRequest r:
                    YesNo(_text.Description(r.Description) ?? "예/아니오를 고르세요.", null);
                    break;
                case SelectOptionRequest r:
                    _title.text = HintTitle(r, "효과를 고르세요");
                    for (int i = 0; i < r.Options.Count; i++)
                    {
                        int index = i;
                        AddButton(_text.Description(r.Options[i]) ?? $"선택지 {i + 1}", () => _respond(DuelResponses.Option(index)));
                    }
                    break;
                case SelectCardRequest r:
                    MultiSelect(HintTitle(r, r.Min == r.Max ? $"카드를 {r.Min}장 고르세요" : $"카드를 {r.Min}~{r.Max}장 고르세요")
                                + CountSuffix(r.Min, r.Max),
                        r.Cards.Select(c => CardLabel(c, state)).ToList(), r.Min, r.Max, r.Cancelable,
                        chosen => DuelSelectionRules.IsValidCards(r, chosen));
                    break;
                case SelectTributeRequest r:
                    MultiSelect(HintTitle(r, "릴리스할 몬스터를 고르세요") + $" (릴리스 {r.Min}장분)",
                        r.Cards.Select(c => CardLabel(new CardRef(c.Code, c.Location), state) + (c.ReleaseParam > 1 ? $" ×{c.ReleaseParam}" : "")).ToList(),
                        r.Min, r.Max, r.Cancelable, chosen => DuelSelectionRules.IsValidTribute(r, chosen));
                    break;
                case SelectSumRequest r:
                    MultiSelect(HintTitle(r, "카드를 고르세요") + $" (합 {r.Target}{(r.Exact ? "" : " 이상")})",
                        r.Selectable.Select(c => $"{CardLabel(new CardRef(c.Code, c.Location), state)} ({c.Param & 0xffff})").ToList(),
                        r.Min, Math.Max(r.Max, r.Selectable.Count), false, chosen => DuelSelectionRules.IsValidSum(r, chosen));
                    break;
                case SelectUnselectCardRequest r: Unselect(r, state); break;
                case SelectPositionRequest r:
                    _title.text = $"「{_text.CardName(r.Code)}」의 표시 형식을 고르세요";
                    foreach (var p in new[] { OcgPosition.FaceUpAttack, OcgPosition.FaceUpDefense, OcgPosition.FaceDownAttack, OcgPosition.FaceDownDefense })
                        if ((r.Positions & p) != 0)
                            AddButton(DuelText.Position(p), () => _respond(DuelResponses.Position(p)));
                    break;
                case AnnounceNumberRequest r:
                    _title.text = "숫자를 선언하세요";
                    for (int i = 0; i < r.Options.Count; i++)
                    {
                        int index = i;
                        AddButton(r.Options[i].ToString(), () => _respond(DuelResponses.AnnounceNumber(index)));
                    }
                    break;
                case AnnounceRaceRequest r: AnnounceBits("종족", r.Count, r.Available, bit => DuelText.Race(bit), m => DuelResponses.AnnounceRace(m)); break;
                case AnnounceAttributeRequest r: AnnounceBits("속성", r.Count, r.Available, bit => DuelText.Attribute((OcgAttribute)bit), m => DuelResponses.AnnounceAttribute((uint)m)); break;
                case AnnounceCardRequest _: AnnounceCard(); break;
                case RockPaperScissorsRequest _:
                    _title.text = "가위바위보";
                    AddButton("가위", () => _respond(DuelResponses.RockPaperScissors(1)));
                    AddButton("바위", () => _respond(DuelResponses.RockPaperScissors(2)));
                    AddButton("보", () => _respond(DuelResponses.RockPaperScissors(3)));
                    break;
                default:
                    _title.text = $"아직 화면이 없는 선택: {request.Type}";
                    break;
            }
            // 개발용: 임시 자동 응답이 대신 고르게 한다.
            var autoButton = UiStyle.Button("(개발용) 자동으로 고르기", () => _respond(_auto(request)), 14);
            autoButton.style.marginTop = 12;
            autoButton.style.color = UiStyle.TextDim;
            _options.Add(autoButton);
        }

        void Idle(SelectIdleCmdRequest r, DuelState state)
        {
            _title.text = $"{DuelText.Phase(state.Phase)}: 행동을 고르세요";
            for (int i = 0; i < r.Summonable.Count; i++) { int k = i; AddButton($"일반 소환: {_text.CardName(r.Summonable[i].Code)}", () => _respond(DuelResponses.Summon(k))); }
            for (int i = 0; i < r.MonsterSetable.Count; i++) { int k = i; AddButton($"몬스터 세트: {_text.CardName(r.MonsterSetable[i].Code)}", () => _respond(DuelResponses.MonsterSet(k))); }
            for (int i = 0; i < r.SpecialSummonable.Count; i++) { int k = i; AddButton($"특수 소환: {_text.CardName(r.SpecialSummonable[i].Code)}", () => _respond(DuelResponses.SpecialSummon(k))); }
            for (int i = 0; i < r.Activatable.Count; i++) { int k = i; AddButton($"발동: {EffectLabel(r.Activatable[i])}", () => _respond(DuelResponses.IdleActivate(k))); }
            for (int i = 0; i < r.SpellSetable.Count; i++) { int k = i; AddButton($"마법·함정 세트: {_text.CardName(r.SpellSetable[i].Code)}", () => _respond(DuelResponses.SpellSet(k))); }
            for (int i = 0; i < r.Repositionable.Count; i++) { int k = i; AddButton($"표시 형식 변경: {_text.CardName(r.Repositionable[i].Code)}", () => _respond(DuelResponses.Reposition(k))); }
            if (r.CanBattlePhase) AddButton("▶ 배틀 페이즈로", () => _respond(DuelResponses.ToBattlePhase()));
            if (r.CanEndPhase) AddButton("▶ 엔드 페이즈로 (턴 종료)", () => _respond(DuelResponses.IdleToEndPhase()));
        }

        void Battle(SelectBattleCmdRequest r, DuelState state)
        {
            _title.text = "배틀 페이즈: 행동을 고르세요";
            for (int i = 0; i < r.Attackers.Count; i++)
            {
                int k = i;
                var a = r.Attackers[i];
                AddButton($"공격: {_text.CardName(a.Code)}{(a.CanDirectAttack ? " (직접 공격 가능)" : "")}", () => _respond(DuelResponses.Attack(k)));
            }
            for (int i = 0; i < r.Activatable.Count; i++) { int k = i; AddButton($"발동: {EffectLabel(r.Activatable[i])}", () => _respond(DuelResponses.BattleActivate(k))); }
            if (r.CanMain2) AddButton("▶ 메인 페이즈 2로", () => _respond(DuelResponses.ToMain2()));
            if (r.CanEndPhase) AddButton("▶ 엔드 페이즈로 (턴 종료)", () => _respond(DuelResponses.BattleToEndPhase()));
        }

        void Chain(SelectChainRequest r, DuelState state)
        {
            var top = state.Chain.LastOrDefault();
            _title.text = top != null
                ? $"체인 {state.Chain.Count}({_text.CardName(top.Code)})에 체인할까요?"
                : r.Forced ? "발동할 효과를 고르세요 (꼭 발동)" : "지금 발동할까요?";
            for (int i = 0; i < r.Chains.Count; i++) { int k = i; AddButton($"발동: {EffectLabel(r.Chains[i])}", () => _respond(DuelResponses.Chain(k))); }
            if (!r.Forced) AddButton("발동하지 않음", () => _respond(DuelResponses.NoChain()));
        }

        void YesNo(string title, string detail)
        {
            _title.text = title;
            if (!string.IsNullOrEmpty(detail)) _detail.text = detail;
            AddButton("예", () => _respond(DuelResponses.YesNo(true)));
            AddButton("아니오", () => _respond(DuelResponses.YesNo(false)));
        }

        /// <summary>엔진이 보낸 안내 문구(예: "파괴할 카드를 선택하세요")가 있으면 그것을, 없으면 fallback을 쓴다.</summary>
        string HintTitle(DuelRequest r, string fallback) =>
            r.SelectHint != 0 ? _text.Description(r.SelectHint) ?? fallback : fallback;

        static string CountSuffix(int min, int max) =>
            min == max ? (min > 1 ? $" ({min}장)" : "") : $" ({min}~{max}장)";

        void Unselect(SelectUnselectCardRequest r, DuelState state)
        {
            _title.text = HintTitle(r, "카드를 고르세요") + CountSuffix(r.Min, r.Max);
            for (int i = 0; i < r.Selectable.Count; i++) { int k = i; AddButton($"선택: {CardLabel(r.Selectable[i], state)}", () => _respond(DuelResponses.SelectUnselect(k))); }
            for (int i = 0; i < r.Unselectable.Count; i++) { int k = r.Selectable.Count + i; AddButton($"선택 해제: {CardLabel(r.Unselectable[i], state)}", () => _respond(DuelResponses.SelectUnselect(k))); }
            if (r.Finishable) AddButton("✔ 선택 끝내기", () => _respond(DuelResponses.FinishSelectUnselect()));
            if (r.Cancelable && !r.Finishable) AddButton("취소", () => _respond(DuelResponses.FinishSelectUnselect()));
        }

        /// <summary>여러 장 고르기. 한 장만 고르면 되는 경우는 누르자마자 보낸다.</summary>
        void MultiSelect(string title, List<string> labels, int min, int max, bool cancelable, Func<IReadOnlyCollection<int>, bool> isValid)
        {
            _title.text = title;
            if (min == 1 && max == 1)
            {
                for (int i = 0; i < labels.Count; i++) { int k = i; AddButton(labels[i], () => _respond(DuelResponses.Cards(new[] { k }))); }
            }
            else
            {
                var chosen = new List<int>();
                Button confirm = null;
                for (int i = 0; i < labels.Count; i++)
                {
                    int k = i;
                    Button b = null;
                    b = UiStyle.Button(labels[i], () =>
                    {
                        if (chosen.Contains(k)) chosen.Remove(k); else chosen.Add(k);
                        UiStyle.SetSelected(b, chosen.Contains(k));
                        confirm.SetEnabled(isValid(chosen));
                    });
                    _options.Add(b);
                }
                confirm = UiStyle.Button("✔ 확인", () => _respond(DuelResponses.Cards(chosen.OrderBy(x => x).ToList())));
                confirm.SetEnabled(isValid(chosen));
                _options.Add(confirm);
            }
            if (cancelable) AddButton("취소", () => _respond(DuelResponses.CancelCards()));
        }

        void AnnounceBits(string what, int count, ulong available, Func<ulong, string> name, Func<ulong, byte[]> response)
        {
            _title.text = $"{what}을(를) {count}개 선언하세요";
            ulong chosen = 0;
            Button confirm = null;
            for (int bit = 0; bit < 64; bit++)
            {
                ulong b = 1UL << bit;
                if ((available & b) == 0) continue;
                Button button = null;
                button = UiStyle.Button(name(b), () =>
                {
                    chosen ^= b;
                    UiStyle.SetSelected(button, (chosen & b) != 0);
                    confirm.SetEnabled(PopCount(chosen) == count);
                });
                _options.Add(button);
            }
            confirm = UiStyle.Button("✔ 확인", () => _respond(response(chosen)));
            confirm.SetEnabled(false);
            _options.Add(confirm);
        }

        void AnnounceCard()
        {
            _title.text = "카드를 선언하세요 (카드 코드 입력)";
            var field = new TextField();
            field.style.fontSize = 17;
            _options.Add(field);
            AddButton("✔ 확인", () =>
            {
                if (uint.TryParse(field.value.Trim(), out uint code))
                    _respond(DuelResponses.AnnounceCard(code));
            });
        }

        string EffectLabel(EffectOption o) => _text.Effect(o.Code, o.Description) + LocationSuffix(o.Location);

        /// <summary>카드 선택지 문구. 로컬 플레이어가 볼 수 없는 카드는 이름을 숨긴다.</summary>
        string CardLabel(CardRef card, DuelState state)
        {
            var info = state.Find(card.Location);
            bool visible = card.Location.Controller == _text.LocalPlayer
                           || (info != null && (info.IsPublic || info.IsFaceUp))
                           || card.Location.Location == OcgLocation.Grave
                           || (card.Location.Location == OcgLocation.Removed && (card.Location.Position & OcgPosition.FaceDown) == 0);
            string name = visible ? _text.CardName(card.Code) : "뒷면 카드";
            return name + LocationSuffix(card.Location);
        }

        string LocationSuffix(CardLocation loc) =>
            loc.Location == OcgLocation.None ? "" : $" ({_text.Player(loc.Controller)} {DuelText.Location(loc)})";

        static int PopCount(ulong v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1) n++;
            return n;
        }
    }
}
