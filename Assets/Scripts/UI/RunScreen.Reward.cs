// 런 화면: 승리 보상 고르기. docs/UI.md "보상 고르기". RunScreen.cs의 머리말 참고.
// 이기면(RunPhase.ChoosingReward) 층 사이 화면 위에 뜬다. 닫을 수 없고, 하나를 고르면 닫힌다.
// 창틀 없이 화면을 어둡게 덮고, 가운데에 "[ 승리. ]"와 설명, 그 아래에 후보를 상세 카드(344×640)로 나란히 놓는다.
// 자리는 RunRules.RewardChoices(3)개: 앞에서부터 후보 카드(서로 다른 카드), 후보가 모자라면 남은 자리는 골드.
// 누르면 고르고(고른 후보는 올라오고 금색 테두리와 ▼가 붙는다, reward-confirm으로 확정), 두 번 누르면 바로 확정한다.
// 키보드: 좌우로 후보를 고르고 결정 키로 받는다. 저장 파일에는 후보가 이미 적혀 있어서
// 이 화면을 띄운 채 게임을 꺼도 같은 후보가 다시 나온다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        const float RewardCardWidth = 344, RewardCardHeight = 640, RewardGap = 44, RewardRise = 22;

        /// <summary>보상 창에서 고른 자리 (0부터). 고르지 않았으면 -1.</summary>
        int _rewardSlot = -1;
        /// <summary>보상 창의 자리별 칸 (고른 자리는 금색 테두리)과 그 위의 ▼.</summary>
        readonly List<VisualElement> _rewardViews = new List<VisualElement>();
        readonly List<Label> _rewardMarks = new List<Label>();
        Button _rewardConfirm;

        RewardOffer Offer => _run?.PendingReward;

        bool RewardOpen => _modalLayer.style.display != DisplayStyle.None && _modalLayer.childCount > 0 && _modalLayer[0].name == "reward";

        uint RewardSelectedCode()
        {
            var offer = Offer;
            return offer != null && offer.IsCard(_rewardSlot) ? offer.Cards[_rewardSlot] : 0;
        }

        int RewardSlotOf(uint code)
        {
            var offer = Offer;
            if (offer == null)
                return -1;
            for (int i = 0; i < offer.Cards.Count; i++)
                if (offer.Cards[i] == code)
                    return i;
            return -1;
        }

        void OpenReward()
        {
            var offer = Offer;
            if (offer == null)
                return;
            if (_rewardSlot >= RunRules.RewardChoices)
                _rewardSlot = -1;
            _rewardViews.Clear();
            _rewardMarks.Clear();

            // 선형 색 공간에서는 어두운 덮개가 옅게 보여서 문서의 80%보다 진하게 한다 (뒤의 팻말과 메뉴가 글과 겹쳐 보이지 않게)
            var panel = OpenPlain("reward", 0.93f);
            panel.style.alignItems = Align.Center;
            var title = UiStyle.Label(offer.IsBossBonus ? "[ 보스 보상. ]" : "[ 승리. ]", 50, UiStyle.Gold);
            UiStyle.Black(title);
            panel.Add(title);
            var intro = Text((offer.IsBossBonus ? "보스 보상 · 승리 보상 1장 더" : "승리 보상") + $" · {offer.Floor}층 · 하나를 고른다. 카드는 사이드 덱과 컬렉션에 들어간다.", 25);
            intro.style.whiteSpace = WhiteSpace.NoWrap;
            panel.Add(intro);
            string why = offer.EmptySlots > 0
                ? $" {offer.Rarity} 카드가 {offer.Cards.Count}종뿐이라 남은 {offer.EmptySlots}자리는 골드 {offer.Gold}이다."
                : "";
            var detail = Text($"[{offer.Rarity}] 등급이 뽑혔다. 상대 덱의 {offer.Rarity} 카드 중에서 후보가 나왔다.{why} 지금 LP {_run.Lp}.", 17, UiStyle.TextDim, top: 4);
            detail.style.whiteSpace = WhiteSpace.NoWrap;
            panel.Add(detail);

            var slots = Row();
            slots.style.marginTop = 4;
            slots.style.alignItems = Align.FlexEnd;
            for (int i = 0; i < RunRules.RewardChoices; i++)
                slots.Add(RewardSlot(offer, i));
            panel.Add(slots);

            _rewardConfirm = UiStyle.StoneButton("", ConfirmReward, gold: true);
            _rewardConfirm.name = "reward-confirm";
            _rewardConfirm.style.width = 520;
            _rewardConfirm.style.marginTop = 16;
            panel.Add(_rewardConfirm);
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "받기"));
            hints.style.marginTop = 12;
            panel.Add(hints);
            ApplyKeyCaps();

            _detail = null;
            _renderDetail = null;
            SelectRewardSlot(_rewardSlot);
        }

        /// <summary>보상 자리 하나 (reward-0, reward-1, …): 위에 ▼(고른 자리만 보인다), 그 아래에 상세 카드나 골드 판.</summary>
        VisualElement RewardSlot(RewardOffer offer, int slot)
        {
            var column = UiStyle.Box($"reward-{slot}");
            column.style.alignItems = Align.Center;
            column.style.marginLeft = slot == 0 ? 0 : RewardGap - 14; // 칸의 테두리와 안쪽 여백(7씩)을 뺀다
            column.style.paddingTop = RewardRise; // 고른 후보가 올라올 자리
            var mark = UiStyle.Label("▼", 26, Color.clear);
            mark.pickingMode = PickingMode.Ignore;
            column.Add(mark);
            _rewardMarks.Add(mark);

            bool isCard = offer.IsCard(slot);
            var view = new VisualElement { name = isCard ? SlotName(Pile.Reward, offer.Cards[slot]) : $"reward-gold-{slot}" };
            UiStyle.Padding(view, 3);
            UiStyle.Radius(view, 14);
            UiStyle.BorderColor(view, Color.clear, 4);
            if (isCard)
            {
                var face = _faces.BuildDetail(offer.Cards[slot], RewardCardWidth, RewardCardHeight);
                // 카드 어디를 눌러도 이 자리를 고르게 한다 (안쪽 요소가 누르기를 가로채지 않게)
                face.pickingMode = PickingMode.Ignore;
                face.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
                view.Add(face);
            }
            else
                view.Add(GoldFace(offer));
            CardInput.Attach(view, _docRoot, onHover: null, onLeave: null,
                onClick: () => SelectRewardSlot(slot),
                onDoubleClick: () =>
                {
                    _rewardSlot = slot;
                    ConfirmReward();
                });
            column.Add(view);
            _rewardViews.Add(view);
            return column;
        }

        /// <summary>후보 카드가 모자라 빈 자리: 카드와 같은 크기의 돌판에 골드 수량.</summary>
        VisualElement GoldFace(RewardOffer offer)
        {
            var box = UiStyle.PanelBox();
            box.pickingMode = PickingMode.Ignore;
            box.style.width = RewardCardWidth;
            box.style.height = RewardCardHeight;
            box.style.justifyContent = Justify.Center;
            box.style.alignItems = Align.Center;
            var head = UiStyle.Label("빈 자리", 24, UiStyle.TextDim);
            head.pickingMode = PickingMode.Ignore;
            box.Add(head);
            var gold = UiStyle.Label($"골드 {offer.Gold}", 60, UiStyle.Gold);
            UiStyle.Black(gold);
            gold.pickingMode = PickingMode.Ignore;
            gold.style.marginTop = 10;
            box.Add(gold);
            var note = UiStyle.Label($"상대 덱의 {offer.Rarity} 카드가 모자라 생긴 자리다. 고르면 카드 대신 골드를 받는다.", 19, UiStyle.TextDim);
            note.pickingMode = PickingMode.Ignore;
            note.style.marginTop = 16;
            note.style.paddingLeft = note.style.paddingRight = 30;
            note.style.unityTextAlign = TextAnchor.UpperCenter;
            box.Add(note);
            return box;
        }

        /// <summary>자리를 고른다: 고른 후보는 올라오고 금색 테두리와 ▼가 붙는다. 받기 버튼의 글도 바꾼다.</summary>
        void SelectRewardSlot(int slot)
        {
            _rewardSlot = slot;
            for (int i = 0; i < _rewardViews.Count; i++)
            {
                bool on = i == slot;
                UiStyle.BorderColor(_rewardViews[i], on ? UiStyle.Gold : Color.clear, 4);
                _rewardViews[i].style.translate = on ? new Translate(0, -RewardRise) : new Translate(0, 0);
                _rewardMarks[i].style.color = on ? UiStyle.Gold : Color.clear;
                _rewardMarks[i].style.translate = on ? new Translate(0, -RewardRise) : new Translate(0, 0);
            }
            var offer = Offer;
            if (_rewardConfirm != null && offer != null)
            {
                _rewardConfirm.text = slot < 0 ? "후보를 고른다"
                    : offer.IsCard(slot) ? $"{_text.CardName(offer.Cards[slot])} 받기"
                    : $"골드 {offer.Gold} 받기";
                _rewardConfirm.SetEnabled(slot >= 0);
            }
        }

        /// <summary>키보드: 좌우로 후보를 고르고 (끝에서 반대쪽 끝으로 넘어간다), 결정 키로 받는다.</summary>
        void RewardKey(Nav nav)
        {
            int count = _rewardViews.Count;
            if (count == 0)
                return;
            switch (nav)
            {
                case Nav.Left:
                    SelectRewardSlot(_rewardSlot < 0 ? 0 : (_rewardSlot + count - 1) % count);
                    break;
                case Nav.Right:
                    SelectRewardSlot(_rewardSlot < 0 ? 0 : (_rewardSlot + 1) % count);
                    break;
                case Nav.Confirm:
                    if (_rewardSlot < 0)
                        SelectRewardSlot(0); // 아직 고르지 않았으면 첫 후보를 가리키기만 한다
                    else
                        ConfirmReward();
                    break;
            }
        }

        void ConfirmReward()
        {
            var offer = Offer;
            if (offer == null || _rewardSlot < 0)
                return;
            int slot = _rewardSlot;
            _rewardSlot = -1;
            string got = offer.IsCard(slot) ? $"획득: {_text.CardName(offer.Cards[slot])} → 사이드 덱" : $"보상으로 골드 {offer.Gold}";
            try
            {
                _run.ChooseReward(slot);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Persist();
            Debug.Log($"[런] 보상 고름: 자리 {slot} ({got})");
            _moveNotice = got;
            _moveNoticeIsError = false;
            ShowHub();
        }
    }
}
