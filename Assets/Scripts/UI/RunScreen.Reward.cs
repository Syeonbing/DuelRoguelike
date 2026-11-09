// 런 화면: 승리 보상 고르기 창. RunScreen.cs의 머리말 참고.
// 이기면(RunPhase.ChoosingReward) 층 사이 화면 위에 뜬다. 닫을 수 없고, 하나를 고르면 닫힌다.
// 자리는 RunRules.RewardChoices(3)개: 앞에서부터 후보 카드(서로 다른 카드), 후보가 모자라면 남은 자리는 골드.
// 누르면 고르고(reward-confirm으로 확정), 두 번 누르면 바로 확정한다. 저장 파일에는 후보가 이미 적혀 있어서
// 이 창을 띄운 채 게임을 꺼도 같은 후보가 다시 나온다.
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
        /// <summary>보상 창에서 고른 자리 (0부터). 고르지 않았으면 -1.</summary>
        int _rewardSlot = -1;
        /// <summary>보상 창의 자리별 테두리 칸 (고른 자리 표시).</summary>
        readonly List<VisualElement> _rewardViews = new List<VisualElement>();
        Button _rewardConfirm;

        RewardOffer Offer => _run?.PendingReward;

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

            var panel = OpenModal(1460);
            panel.name = "reward";
            panel.Add(UiStyle.Label($"승리 보상 · {offer.Floor}층", 30, UiStyle.Accent, bold: true));
            string why = offer.EmptySlots > 0
                ? $"상대 덱의 {offer.Rarity} 카드가 {offer.Cards.Count}종뿐이라 남은 {offer.EmptySlots}자리는 골드 {offer.Gold}입니다. "
                : "";
            var intro = Text($"[{offer.Rarity}] 등급이 뽑혔습니다. 상대 덱의 {offer.Rarity} 카드 중에서 후보가 나왔습니다. {why}" +
                             "하나를 골라 주세요. 카드는 사이드 덱과 컬렉션에 들어갑니다. 두 번 누르면 바로 고릅니다.",
                16, UiStyle.TextDim, top: 6);
            intro.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(intro);
            panel.Add(Text($"LP {_run.Lp}", 16, top: 4));

            var body = Row();
            body.style.marginTop = 14;
            var slots = Row();
            slots.style.flexGrow = 1;
            slots.style.justifyContent = Justify.Center;
            slots.style.alignItems = Align.FlexStart;
            for (int i = 0; i < RunRules.RewardChoices; i++)
                slots.Add(RewardSlot(offer, i));
            body.Add(slots);

            var detail = UiStyle.PanelBox("reward-detail");
            detail.style.width = 400;
            detail.style.minHeight = CardFaces.SizeOf(CardFaceSize.Large).y + 40;
            detail.style.marginLeft = 12;
            detail.style.backgroundColor = UiStyle.PanelLight;
            body.Add(detail);
            panel.Add(body);

            var buttons = Row();
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 14;
            _rewardConfirm = UiStyle.Button("", ConfirmReward, 22);
            _rewardConfirm.name = "reward-confirm";
            _rewardConfirm.style.paddingLeft = _rewardConfirm.style.paddingRight = 28;
            buttons.Add(_rewardConfirm);
            panel.Add(buttons);

            _detail = detail;
            _renderDetail = (into, pile, code, pinned) => RenderRewardDetail(into, code != 0 ? RewardSlotOf(code) : _rewardSlot, pinned);
            SelectRewardSlot(_rewardSlot);
        }

        /// <summary>보상 자리 하나 (reward-0, reward-1, …): 카드 자리는 큰 카드, 빈 자리는 골드 칸. 아래에 자리 번호.</summary>
        VisualElement RewardSlot(RewardOffer offer, int slot)
        {
            var column = UiStyle.Box($"reward-{slot}");
            column.style.alignItems = Align.Center;
            column.style.marginLeft = column.style.marginRight = 8;
            VisualElement view;
            if (offer.IsCard(slot))
            {
                uint code = offer.Cards[slot];
                view = CardSlot(Pile.Reward, code, CardFaceSize.Large, draggable: false, onDoubleClick: () =>
                {
                    _rewardSlot = slot;
                    ConfirmReward();
                });
            }
            else
                view = GoldSlot(offer, slot);
            column.Add(view);
            _rewardViews.Add(view);
            column.Add(Text(offer.IsCard(slot) ? $"후보 {slot + 1}" : $"후보 {slot + 1} · 빈 자리", 14, UiStyle.TextDim, top: 2));
            return column;
        }

        /// <summary>후보 카드가 모자라 빈 자리: 카드 크기의 골드 칸.</summary>
        VisualElement GoldSlot(RewardOffer offer, int slot)
        {
            var size = CardFaces.SizeOf(CardFaceSize.Large);
            var outer = new VisualElement { name = $"reward-gold-{slot}", focusable = true };
            UiStyle.Padding(outer, 2);
            UiStyle.Margin(outer, 3);
            UiStyle.Radius(outer, 6);
            UiStyle.BorderColor(outer, Color.clear, 2);
            var box = UiStyle.Box();
            box.style.width = size.x;
            box.style.height = size.y;
            box.style.justifyContent = Justify.Center;
            box.style.alignItems = Align.Center;
            box.style.backgroundColor = UiStyle.PanelLight;
            UiStyle.Radius(box, 8);
            UiStyle.BorderColor(box, UiStyle.Border, 1);
            box.pickingMode = PickingMode.Ignore;
            var head = UiStyle.Label("빈 자리", 18, UiStyle.TextDim);
            head.pickingMode = PickingMode.Ignore;
            box.Add(head);
            var gold = UiStyle.Label($"골드 {offer.Gold}", 34, UiStyle.Accent, bold: true);
            gold.pickingMode = PickingMode.Ignore;
            gold.style.marginTop = 8;
            box.Add(gold);
            outer.Add(box);
            CardInput.Attach(outer, _docRoot,
                onHover: () => ShowRewardDetail(slot, pinned: false),
                onLeave: () => ShowRewardDetail(_rewardSlot, pinned: true),
                onClick: () => SelectRewardSlot(slot),
                onDoubleClick: () =>
                {
                    _rewardSlot = slot;
                    ConfirmReward();
                });
            return outer;
        }

        void SelectRewardSlot(int slot)
        {
            _rewardSlot = slot;
            for (int i = 0; i < _rewardViews.Count; i++)
                UiStyle.BorderColor(_rewardViews[i], i == slot ? UiStyle.Accent : Color.clear, 2);
            var offer = Offer;
            if (_rewardConfirm != null && offer != null)
            {
                _rewardConfirm.text = slot < 0 ? "후보를 고르세요"
                    : offer.IsCard(slot) ? $"{_text.CardName(offer.Cards[slot])} 받기"
                    : $"골드 {offer.Gold} 받기";
                _rewardConfirm.SetEnabled(slot >= 0);
            }
            ShowRewardDetail(slot, pinned: true);
        }

        void ShowRewardDetail(int slot, bool pinned)
        {
            if (_detail == null)
                return;
            _detail.Clear();
            RenderRewardDetail(_detail, slot, pinned);
        }

        void RenderRewardDetail(VisualElement panel, int slot, bool pinned)
        {
            var offer = Offer;
            if (offer == null)
                return;
            if (slot < 0)
            {
                panel.Add(Text("왼쪽 후보에 마우스를 올리면 여기에 카드 정보가 나오고, 누르면 그 후보를 고릅니다.", 17, UiStyle.TextDim));
                return;
            }
            if (!offer.IsCard(slot))
            {
                panel.Add(UiStyle.Label($"골드 {offer.Gold}", 24, UiStyle.Accent, bold: true));
                var note = Text($"상대 덱의 {offer.Rarity} 카드가 {offer.Cards.Count}종뿐이라 생긴 빈 자리입니다. " +
                                "고르면 카드 대신 골드를 받습니다. 골드를 쓰는 곳은 아직 없습니다.", 15, UiStyle.TextDim, top: 8);
                note.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(note);
                if (!pinned)
                    panel.Add(Text("누르면 이 자리를 고릅니다.", 13, UiStyle.TextDim, top: 4));
                return;
            }
            uint code = offer.Cards[slot];
            AddCardDetail(panel, code, pinned, showFace: false);
            panel.Add(Text($"[{offer.Rarity}] · 같은 카드명 덱 {_run.DeckCopies(code)}장 · 사이드 덱 {_run.SideDeck.Count(c => _run.NameCode(c) == _run.NameCode(code))}장 · " +
                           $"컬렉션 {_collection.Count(code)}장", 14, UiStyle.TextDim, top: 6));
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
