// 런 화면: 칩교환소 창, 이벤트 창, 휴식 창, 보스 보상(봉인 해제) 창. RunScreen.cs의 머리말 참고.
// 탑 그림과 층 팻말, 위 막대의 10층 묶음 칸은 RunScreen.Hub.cs에 있다. 아래 창들은 그 층에 올라섰을 때(올라가는 연출 뒤) 한 번 저절로 뜬다.
// 이벤트에서 생긴 전투는 다른 전투와 같은 듀얼 창(RunScreen.Hub.cs의 OpenDuelWindow)을 거친다.
// - 칩교환소 (chip-exchange): 상품 목록(가격, 살 수 없는 까닭)과 아이템 카드 자리. 사기를 누르면 풀 카드를 고르고(shop-pick),
//   직접 고르는 교체면 대신 봉인할 카드도 고른다. 산 뒤에는 칩교환소 창을 다시 연다 (메인 덱이 모자라면 보충 창이 먼저).
// - 이벤트 (tower-event): 제목, 글, 선택지. 이벤트 층에 들어가면 한 번 저절로 뜬다. 닫고 덱을 정리한 뒤 다시 열 수 있다.
// - 휴식 (tower-rest): 휴식 층에 들어가면 이벤트처럼 한 번 저절로 뜬다. 들어갈 때 맞춘 LP(이전 → 8000)와 다음 층으로 버튼.
// - 보스 보상 (unseal): 보스를 이기고 봉인된 카드가 있으면 뜬다 (ChoosingUnseal). 닫을 수 없다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        /// <summary>칩교환소에서 사는 중인 상품 (null이면 상품 목록), 고른 풀 카드.</summary>
        ShopProduct? _shopProduct;
        uint _shopRelease;
        /// <summary>이벤트 창을 저절로 띄운 층 (같은 층에서는 다시 띄우지 않는다).</summary>
        int _eventShownFloor;
        /// <summary>휴식 창·칩교환소 창을 저절로 띄운 층.</summary>
        int _restShownFloor;
        int _shopShownFloor;

        static readonly Color RestColor = UiStyle.Good;
        static readonly Color EventFloorColor = UiStyle.Hex(0xb9a0f2);
        static readonly Color ShopFloorColor = UiStyle.Hex(0x70cbb7);

        static Color FloorColor(FloorKind kind)
        {
            switch (kind)
            {
                case FloorKind.Boss: return UiStyle.Bad;
                case FloorKind.ChipExchange: return ShopFloorColor;
                case FloorKind.Rest: return RestColor;
                case FloorKind.Event: return EventFloorColor;
                default: return UiStyle.Mine;
            }
        }

        /// <summary>명령 메뉴 위쪽의 안내 한 줄 (층 종류마다).</summary>
        string FloorNote()
        {
            switch (_run.FloorKind)
            {
                case FloorKind.Boss:
                    return $"보스 층: 이기면 칩 {RunRules.ChipsPerBossWin}과 보스 보상 (봉인된 카드가 있으면 1장 봉인 해제, 없으면 승리 보상 1장 더).";
                case FloorKind.ChipExchange:
                    return "칩교환소: 칩으로 봉인 대상 교체·봉인 해제·아이템 카드를 삽니다. 다 보면 다음 층으로 올라갑니다.";
                case FloorKind.Rest:
                    return $"휴식 층: LP를 {RunRules.FullLp}로 맞췄습니다. 덱을 정리하고 다음 층으로 올라갑니다.";
                case FloorKind.Event:
                    return _run.EventBattlePending ? "이벤트 층: 이벤트 전투가 남았습니다. 이기면 결과를 받고, 지면 일반 패배처럼 봉인됩니다."
                        : _run.EventDone ? "이벤트 층: 이벤트는 끝났습니다. 덱을 정리하고 다음 층으로 올라갑니다."
                        : "이벤트 층: 이벤트의 선택지를 고르면 결과를 받고 다음 층으로 올라갑니다.";
                default:
                    return "상대는 듀얼을 시작할 때 스타터 덱 4개 중에서 무작위로 정해집니다.";
            }
        }

        /// <summary>칩교환소·휴식 층(또는 이벤트를 마친 이벤트 층)을 떠나 다음 층으로 올라간다 (올라가는 연출).</summary>
        void LeaveFloor()
        {
            if (_run == null)
                return;
            if (!_run.CanLeaveFloor(out string reason))
            {
                SetMoveNotice("다음 층으로 갈 수 없습니다: " + reason, error: true);
                return;
            }
            string from = $"{_run.Floor}층 {Tower.KindName(_run.FloorKind)}";
            try
            {
                _run.LeaveFloor();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Persist();
            _shopProduct = null;
            _moveNotice = null; // 어디로 가는지는 올라가는 연출이 보여 준다
            Debug.Log($"[런] {from} → {_run.Floor}층 {Tower.KindName(_run.FloorKind)}");
            if (!StartClimb())
                ShowHub();
        }

        // ================= 칩교환소 =================

        /// <summary>
        /// 칩교환소 창: 상품을 목록으로 한 줄씩 놓는다 (왼쪽에 아이콘, 이름과 설명, 오른쪽 끝에 값).
        /// 위아래로 고르고 결정 키나 누르기로 산다. 산 상품과 살 수 없는 상품은 흐리고 까닭을 쓴다.
        /// </summary>
        void OpenShop()
        {
            if (_run == null || _run.Phase != RunPhase.Ready || _run.FloorKind != FloorKind.ChipExchange)
                return;
            _shopShownFloor = _run.Floor;
            if (_shopProduct != null)
            {
                OpenShopPicker();
                return;
            }
            var panel = OpenSlab("chip-exchange", 1260);
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.Add(WindowTitle("칩교환소"));
            var where = Text($"{_run.Floor}층 · 상품마다 이 칩교환소에서 한 번만 살 수 있다", 19, UiStyle.TextDim);
            where.style.marginLeft = 18;
            where.style.marginBottom = 7;
            head.Add(where);
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            head.Add(gap);
            var have = Text("가진 칩", 19, UiStyle.TextDim);
            have.style.marginRight = 12;
            have.style.marginBottom = 7;
            head.Add(have);
            var chips = UiStyle.Label(_run.Chips.ToString(), 44, UiStyle.Gold);
            UiStyle.Black(chips);
            chips.name = "shop-chips";
            head.Add(chips);
            panel.Add(head);

            var menu = new MenuList();
            menu.Root.style.marginTop = 12;
            foreach (var offer in _run.Shop)
                ShopRow(menu, offer);
            panel.Add(menu.Root);
            menu.SelectEnabled(_shopMenuIndex);
            _windowMenu = menu;

            var leave = UiStyle.StoneButton($"{_run.Floor + 1}층으로 올라간다", LeaveFloor, gold: true, small: true);
            leave.name = "shop-leave";
            leave.style.width = 250;
            panel.Add(WindowFooter(Hints(("↑", "W", null), ("↓", "S", "고르기"), ("Z", "J", "산다"), ("X", "K", "닫기")),
                KeyButton("shop-close", "닫기", CloseModal, "X", "K", 112), leave));
            ApplyKeyCaps();
        }

        /// <summary>칩교환소 창에서 가리키던 줄 (사고 돌아왔을 때 같은 자리에 서게).</summary>
        int _shopMenuIndex;

        /// <summary>상품 이름에서 종류만: 봉인 대상 교체 4종은 모두 "봉인 대상 교체"이고 차이는 꼬리표로 보인다.</summary>
        static string ShopTitle(ShopOffer offer) =>
            ChipExchange.IsSwap(offer.Product) ? "봉인 대상 교체" : ChipExchange.Name(offer);

        /// <summary>봉인 대상 교체의 꼬리표: "같은 레어도 · 직접 고름". 다른 상품은 null.</summary>
        static string ShopTag(ShopProduct product) =>
            !ChipExchange.IsSwap(product) ? null
            : (ChipExchange.SameRarity(product) ? "같은 레어도" : "레어도 상관없음") + " · " + (ChipExchange.ChoosesNewSeal(product) ? "직접 고름" : "무작위");

        /// <summary>상품 줄의 설명 한 줄 (화면의 말투로 짧게 쓴다).</summary>
        string ShopNote(ShopOffer offer)
        {
            var product = offer.Product;
            if (product == ShopProduct.Unseal)
                return "봉인된 카드 1장을 푼다. 풀린 카드는 사이드 덱으로 간다.";
            if (product == ShopProduct.ItemCard)
            {
                var item = ItemCards.All.FirstOrDefault(i => i.Code == offer.Card);
                return (item != null ? item.Describe() + " · " : "") + $"산 카드는 상점 덱으로 간다 · 지금 {_run.ShopDeck.Count} / {RunRules.MaxShopDeck}장";
            }
            string rarity = ChipExchange.SameRarity(product) ? "같은 레어도 카드" : "카드";
            string pick = ChipExchange.ChoosesNewSeal(product) ? "직접 골라" : "무작위로";
            return $"봉인된 카드 1장을 풀고, 같은 덱의 {rarity} 1장을 {pick} 대신 봉인한다. 봉인된 장수는 그대로다.";
        }

        void ShopRow(MenuList menu, ShopOffer offer)
        {
            var product = offer.Product;
            bool ok = _run.CanBuy(product, out string reason);
            int index = menu.Count;
            var row = menu.AddRow($"shop-buy-{product}", () =>
            {
                _shopMenuIndex = index;
                BeginPurchase(product);
            }, ok, height: 0);

            // 아이콘: 아이템 카드는 그 카드, 봉인을 다루는 상품은 픽토그램
            VisualElement icon;
            if (product == ShopProduct.ItemCard)
            {
                icon = _faces.Build(offer.Card, 56);
                icon.pickingMode = PickingMode.Ignore;
                icon.Query().ForEach(e => e.pickingMode = PickingMode.Ignore);
                icon.style.flexShrink = 0;
            }
            else
                icon = IconTile(product == ShopProduct.Unseal ? "속성:빛" : "마법:속공", 56, product == ShopProduct.Unseal ? RestColor : ShopFloorColor);
            icon.style.marginRight = 16;
            row.Add(icon);

            var texts = UiStyle.Box();
            texts.pickingMode = PickingMode.Ignore;
            texts.style.flexShrink = 1;
            var names = Row();
            names.pickingMode = PickingMode.Ignore;
            names.style.alignItems = Align.Center;
            var name = RowLabel(ShopTitle(offer), 26, UiStyle.Text, bold: true);
            name.name = $"shop-name-{product}";
            name.style.whiteSpace = WhiteSpace.NoWrap;
            names.Add(name);
            string tagText = ShopTag(product);
            if (tagText != null)
            {
                var tag = RowLabel(tagText, 19, ShopFloorColor, bold: true);
                tag.style.whiteSpace = WhiteSpace.NoWrap;
                tag.style.marginLeft = 12;
                tag.style.paddingLeft = tag.style.paddingRight = 10;
                tag.style.paddingTop = tag.style.paddingBottom = 1;
                UiStyle.BorderColor(tag, UiStyle.Stone5, 2);
                UiStyle.Radius(tag, 14);
                names.Add(tag);
            }
            texts.Add(names);
            // 그 아래 한 줄: 살 수 없으면 까닭, 샀으면 샀다는 것, 아니면 설명
            string note = offer.Sold ? "이 칩교환소에서 이미 샀다" : !ok ? reason : ShopNote(offer);
            var description = RowLabel(note, 18, !ok && !offer.Sold ? UiStyle.Bad : UiStyle.TextDim);
            description.name = !ok && !offer.Sold ? $"shop-reason-{product}" : null;
            description.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(description);
            row.Add(texts);

            var price = RowTag(offer.Sold ? "샀음" : $"칩 {offer.Price}", 27, offer.Sold ? UiStyle.TextDim : ok ? UiStyle.Gold : UiStyle.Bad);
            UiStyle.Black(price);
            price.name = $"shop-price-{product}";
            row.Add(price);
        }

        void BeginPurchase(ShopProduct product)
        {
            if (!_run.CanBuy(product, out string reason))
            {
                Debug.Log($"[런] 칩교환소: {ChipExchange.Name(product)}을(를) 살 수 없음 — {reason}");
                return;
            }
            if (product == ShopProduct.ItemCard)
            {
                uint card = _run.Shop.First(o => o.Product == ShopProduct.ItemCard).Card;
                _run.BuyItem();
                Persist();
                _moveNotice = $"칩교환소: 아이템 카드 {_text.CardName(card)} 구매 → 상점 덱 (남은 칩 {_run.Chips})";
                _moveNoticeIsError = false;
                Debug.Log($"[런] {_moveNotice}");
                _selectedPile = Pile.Shop;
                _selectedCode = card;
                ShowHub();
                OpenShop();
                return;
            }
            _shopProduct = product;
            _shopRelease = 0;
            OpenShopPicker();
        }

        /// <summary>사는 중: 1) 봉인을 풀 카드 고르기, 2) 직접 고르는 교체면 대신 봉인할 카드 고르기.</summary>
        void OpenShopPicker()
        {
            var product = _shopProduct.Value;
            bool second = _shopRelease != 0;
            var pile = second ? Pile.ShopSeal : Pile.ShopRelease;
            var candidates = second ? _run.NewSealCandidates(product, _shopRelease) : _run.ReleaseCandidates(product);

            var panel = OpenModal(1500);
            panel.name = "shop-pick";
            panel.style.height = Length.Percent(88);
            panel.Add(UiStyle.Label($"{ChipExchange.Name(product)} · 칩 {RunRules.ShopPrice(product)}", 28, UiStyle.Accent, bold: true));
            string rarity = ChipExchange.SameRarity(product) ? "같은 레어도의 " : "";
            string step = product == ShopProduct.Unseal
                ? "봉인을 풀 카드를 고르세요. 풀린 카드는 사이드 덱으로 갑니다."
                : second
                    ? $"「{_text.CardName(_shopRelease)}」 대신 봉인할 카드를 고르세요 (같은 덱의 {rarity}카드)."
                    : "봉인을 풀 카드를 고르세요. 풀린 카드는 사이드 덱으로 갑니다. " +
                      (ChipExchange.ChoosesNewSeal(product)
                          ? "다음에 대신 봉인할 카드를 고릅니다."
                          : $"대신 봉인할 카드는 같은 덱의 {rarity}카드 중에서 무작위로 정해집니다.");
            var intro = Text(step + " 두 번 누르면 바로 고릅니다.", 16, UiStyle.TextDim, top: 6);
            intro.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(intro);

            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var grid = Grid(scroll.contentContainer);
            foreach (uint candidate in Sorted(candidates))
            {
                uint code = candidate;
                grid.Add(CardSlot(pile, code, CardFaceSize.Medium, onDoubleClick: () => PickShopCard(pile, code), draggable: false));
            }
            body.Add(scroll);
            var detail = UiStyle.PanelBox("shop-detail");
            detail.style.width = 380;
            detail.style.marginLeft = 12;
            detail.style.backgroundColor = UiStyle.PanelLight;
            body.Add(detail);
            panel.Add(body);

            var buttons = Row();
            buttons.style.marginTop = 12;
            buttons.style.justifyContent = Justify.FlexEnd;
            var back = UiStyle.Button("뒤로", () =>
            {
                if (_shopRelease != 0)
                    _shopRelease = 0;
                else
                    _shopProduct = null;
                OpenShop();
            }, 20);
            back.name = "shop-back";
            buttons.Add(back);
            panel.Add(buttons);

            _detail = detail;
            _renderDetail = RenderShopDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        void RenderShopDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (_shopProduct == null)
                return;
            var product = _shopProduct.Value;
            bool second = _shopRelease != 0;
            if (code == 0 || pile != (second ? Pile.ShopSeal : Pile.ShopRelease))
            {
                panel.Add(Text("왼쪽에서 카드를 고르세요.", 17, UiStyle.TextDim));
                return;
            }
            AddCardDetail(panel, code, pinned);
            if (_rarities.TryGet(code, out var rarity))
                panel.Add(Text($"레어도 {rarity}", 14, UiStyle.TextDim, top: 6));
            if (!pinned)
                return;
            string text = second ? "이 카드를 대신 봉인하고 사기"
                : product != ShopProduct.Unseal && ChipExchange.ChoosesNewSeal(product) ? "이 카드를 풀기 (다음: 대신 봉인할 카드)"
                : "이 카드의 봉인을 풀고 사기";
            var confirm = UiStyle.Button(text, () => PickShopCard(pile, code), 18);
            confirm.name = "shop-confirm";
            confirm.style.marginTop = 10;
            panel.Add(confirm);
        }

        void PickShopCard(Pile pile, uint code)
        {
            if (_shopProduct == null)
                return;
            var product = _shopProduct.Value;
            if (pile == Pile.ShopRelease && product != ShopProduct.Unseal && ChipExchange.ChoosesNewSeal(product))
            {
                _shopRelease = code;
                _selectedCode = 0;
                OpenShopPicker();
                return;
            }
            uint release = pile == Pile.ShopRelease ? code : _shopRelease;
            uint newSeal = pile == Pile.ShopSeal ? code : 0;
            int mark = _run.Events.Count;
            try
            {
                if (product == ShopProduct.Unseal)
                    _run.BuyUnseal(release);
                else
                    _run.BuySwap(product, release, newSeal);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Persist();
            var sealedNow = _run.Events.Skip(mark).FirstOrDefault(e => e.Kind == RunEventKind.CardSealed);
            _moveNotice = $"칩교환소: {ChipExchange.Name(product)} — {_text.CardName(release)} 봉인 해제 → 사이드 덱" +
                          (sealedNow != null ? $", 대신 {_text.CardName(sealedNow.Code)} 봉인" : "") + $" (남은 칩 {_run.Chips})";
            _moveNoticeIsError = false;
            Debug.Log($"[런] {_moveNotice}");
            _shopProduct = null;
            _shopRelease = 0;
            _selectedPile = Pile.Side;
            _selectedCode = release;
            ShowHub(); // 메인 덱이 모자라면 보충 창이 뜬다
            if (_run.Phase == RunPhase.Ready)
                OpenShop();
        }

        // ================= 이벤트 =================

        /// <summary>
        /// 이벤트 창: 왼쪽에 그림 칸, 오른쪽에 제목과 글, 선택지 목록. 선택지는 위아래로 고르고 결정 키로 고른다.
        /// 선택지에는 할 일만 쓰고, 조건이나 비용은 줄 오른쪽 끝에 따로 쓴다.
        /// </summary>
        void OpenEvent()
        {
            var ev = _run?.CurrentEvent;
            if (ev == null || _run.Phase != RunPhase.Ready || _run.EventDone)
                return;
            _eventShownFloor = _run.Floor;
            if (_run.EventBattlePending)
            {
                OpenDuelWindow();
                return;
            }
            var panel = OpenSlab("tower-event", 1260);
            var body = Row();
            body.style.alignItems = Align.FlexStart;
            var left = UiStyle.Box();
            left.style.width = EventArtSize;
            left.style.marginRight = 38;
            left.style.flexShrink = 0;
            left.Add(EventArt(ev.Id, EventArtSize));
            // 성향은 도감에서 모든 선택지를 골라 본 이벤트만 보인다
            left.Add(Text($"이벤트 · {_run.Floor}층" + (_save.Codex.IsComplete(ev) ? $" · {TowerEvent.TendencyName(ev.Tendency)}" : ""), 19, UiStyle.TextDim, top: 12));
            body.Add(left);

            var right = UiStyle.Box();
            right.style.flexGrow = 1;
            right.style.flexShrink = 1;
            var title = WindowTitle(ev.Title);
            title.name = "event-title";
            right.Add(title);
            var text = Text(ev.Text, 24, top: 10);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.marginBottom = 18;
            right.Add(text);

            var menu = new MenuList();
            bool allLocked = _run.AllEventChoicesLocked;
            for (int i = 0; i < ev.Choices.Count; i++)
            {
                var option = ev.Choices[i];
                if (option.IsForced && !allLocked)
                    continue; // 강제 선택지는 다른 선택지가 모두 잠겼을 때만 나온다
                int index = i;
                bool can = _run.CanChooseEventOption(index, out string reason);
                var row = menu.AddRow($"event-choice-{i}", () => ChooseEvent(index), can, height: 0);
                var texts = UiStyle.Box();
                texts.pickingMode = PickingMode.Ignore;
                texts.style.flexShrink = 1;
                var name = RowLabel(ChoiceTitle(option.Label), 27, UiStyle.Text, bold: true);
                name.name = $"event-choice-title-{i}";
                texts.Add(name);
                // 그 아래 작은 줄: 전에 골라 봐서 아는 결과, 또는 이벤트 전투라는 것
                bool known = KnowsResult(ev, index);
                if (option.IsBattle || known)
                {
                    var info = RowLabel(known ? "전에 골라 봤다 · " + option.DescribeEffects()
                        : $"이벤트 전투 · 상대 {StarterDecks.Find(option.BattleDeckId)?.Name ?? option.BattleDeckId} · 지면 일반 패배처럼 봉인된다", 17, UiStyle.TextDim);
                    info.name = $"event-choice-info-{i}";
                    info.style.whiteSpace = WhiteSpace.Normal;
                    texts.Add(info);
                }
                row.Add(texts);
                // 조건 글의 괄호(세는 범위)도 가운뎃점으로 바꿔 쓴다: "드래곤족 몬스터 2장 이상 (덱·사이드 덱)" → "… 2장 이상 · 덱·사이드 덱"
                string condition = option.HasCondition ? RunState.ConditionText(option, ev.Scope).Replace(" (", " · ").Replace(")", "") : ChoiceCost(option.Label);
                if (!string.IsNullOrEmpty(condition))
                {
                    var tag = RowTag(can ? condition : $"잠김 · {condition}", 18, can ? UiStyle.Gold : UiStyle.Bad);
                    tag.name = $"event-choice-note-{i}";
                    row.Add(tag);
                }
            }
            if (_run.CanChooseEventOption(-1, out _))
            {
                var pass = menu.AddRow("event-pass", () => ChooseEvent(-1));
                pass.Add(RowLabel("그냥 지나간다", 27, UiStyle.Text, bold: true));
            }
            right.Add(menu.Root);
            body.Add(right);
            panel.Add(body);
            menu.SelectEnabled(0);
            _windowMenu = menu;
            panel.Add(WindowFooter(Hints(("↑", "W", null), ("↓", "S", "고르기"), ("Z", "J", "결정"), ("X", "K", "나중에")),
                KeyButton("event-close", "나중에", CloseModal, "X", "K", 132)));
            ApplyKeyCaps();
        }

        const float EventArtSize = 330;

        /// <summary>
        /// 선택지 글에서 끝의 괄호(비용·조건)를 뗀 것: "시주한다 (칩 30)" → "시주한다".
        /// 이벤트 자료의 글에는 비용이 괄호로 붙어 있는데, 화면에서는 버튼 오른쪽 끝에 따로 보여 준다.
        /// </summary>
        static string ChoiceTitle(string label)
        {
            if (string.IsNullOrEmpty(label) || !label.EndsWith(")"))
                return label ?? "";
            int open = label.LastIndexOf(" (", StringComparison.Ordinal);
            return open > 0 ? label.Substring(0, open) : label;
        }

        /// <summary>선택지 글 끝의 괄호 안 (없으면 null): "시주한다 (칩 30)" → "칩 30". 조건이 따로 없는 선택지의 비용 표시에 쓴다.</summary>
        static string ChoiceCost(string label)
        {
            if (string.IsNullOrEmpty(label) || !label.EndsWith(")"))
                return null;
            int open = label.LastIndexOf(" (", StringComparison.Ordinal);
            return open > 0 ? label.Substring(open + 2, label.Length - open - 3) : null;
        }

        /// <summary>이 선택지를 전에 골라 봐서 결과를 아는지 (이번 런 또는 도감). 처음 고르는 선택지는 결과를 숨긴다.</summary>
        bool KnowsResult(TowerEvent ev, int index) =>
            _save.Codex.Chosen(ev.Id, index) || _run.Events.Any(e => e.Kind == RunEventKind.EventResolved && e.EventId == ev.Id && e.Count == index);

        void ChooseEvent(int index)
        {
            var ev = _run.CurrentEvent;
            if (ev == null)
                return;
            int mark = _run.Events.Count;
            int floor = _run.Floor;
            try
            {
                _run.ChooseEventOption(index);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowHub();
                return;
            }
            Persist();
            var option = index >= 0 ? ev.Choices[index] : null;
            Debug.Log($"[런] 이벤트 「{ev.Title}」: {option?.Label ?? "그냥 지나감"} → {_run.Floor}층 {Tower.KindName(_run.FloorKind)}");
            if (_run.EventBattlePending)
            {
                ShowHub(); // 메뉴를 "이벤트 전투"로 바꾼다. 이어서 듀얼 창 (다른 전투와 같다: 전투 전 덱 조정)
                OpenDuelWindow();
                return;
            }
            _eventResult = new EventResultView(ev, option, floor, won: null, _run.Events.Skip(mark).ToList());
            if (_run.Floor != floor)
                SetFloorNotice(floor, FloorKind.Event);
            ShowHub();
        }

        /// <summary>이벤트를 고른 뒤(또는 이벤트 전투가 끝난 뒤) 보여 줄 결과. ShowHub가 다른 창보다 먼저 연다.</summary>
        sealed class EventResultView
        {
            public readonly TowerEvent Event;
            public readonly EventChoice Choice;
            public readonly int Floor;
            /// <summary>이벤트 전투면 이겼는지, 아니면 null.</summary>
            public readonly bool? Won;
            public readonly List<RunEvent> Happened;

            public EventResultView(TowerEvent ev, EventChoice choice, int floor, bool? won, List<RunEvent> happened)
            {
                Event = ev;
                Choice = choice;
                Floor = floor;
                Won = won;
                Happened = happened.Where(e => e.Kind != RunEventKind.EventResolved && e.Kind != RunEventKind.FloorEntered).ToList();
            }
        }

        EventResultView _eventResult;

        /// <summary>이벤트 결과 창: 이벤트 창과 같은 틀. 고른 것, 결과 글, 일어난 일, 확인 버튼.</summary>
        void OpenEventResult()
        {
            var view = _eventResult;
            if (view == null)
                return;
            var panel = OpenSlab("event-result", 1260);
            var body = Row();
            body.style.alignItems = Align.FlexStart;
            var left = UiStyle.Box();
            left.style.width = EventArtSize;
            left.style.marginRight = 38;
            left.style.flexShrink = 0;
            left.Add(EventArt(view.Event.Id, EventArtSize));
            left.Add(Text($"이벤트 결과 · {view.Floor}층", 19, UiStyle.TextDim, top: 12));
            body.Add(left);

            var right = UiStyle.Box();
            right.style.flexGrow = 1;
            right.style.flexShrink = 1;
            right.Add(WindowTitle(view.Event.Title));
            var chosen = Row();
            chosen.style.alignItems = Align.Center;
            chosen.style.marginTop = 10;
            chosen.Add(UiStyle.Label("▶", 17, UiStyle.Gold));
            var chosenName = UiStyle.Label(view.Choice != null ? ChoiceTitle(view.Choice.Label) : "그냥 지나간다", 27, UiStyle.Text, bold: true);
            chosenName.style.marginLeft = 12;
            chosen.Add(chosenName);
            right.Add(chosen);
            string text = view.Won == false ? "이벤트 전투에서 졌다. 이벤트의 결과는 받지 못했다."
                : view.Choice?.ResultText ?? "아무 일 없이 지나갔다.";
            AddWrapped(right, text, 24, UiStyle.Text, 12);
            var list = new VisualElement { name = "event-result-list" };
            list.style.marginTop = 16;
            foreach (var e in view.Happened)
                list.Add(Text(e.Describe(_text.CardName), 21, EventColor(e.Kind), top: 3));
            if (view.Happened.Count == 0)
                list.Add(Text("바뀐 것 없음", 21, UiStyle.TextDim, top: 3));
            right.Add(list);
            body.Add(right);
            panel.Add(body);

            var buttons = new ButtonRow();
            buttons.Add("event-result-ok", "확인", () =>
            {
                _eventResult = null;
                ShowHub();
            }, gold: true, width: 240);
            buttons.Select(0);
            _windowRow = buttons;
            panel.Add(WindowFooter(Hints(("Z", "J", "확인")), buttons.Root));
            ApplyKeyCaps();
        }

        void AddWrapped(VisualElement panel, string text, int size, Color color, int top)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var label = Text(text, size, color, top: top);
            label.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(label);
        }

        /// <summary>층을 오른 뒤 상태 줄에 "N층 이벤트 → M층 …"을 띄운다.</summary>
        void SetFloorNotice(int fromFloor, FloorKind fromKind)
        {
            _moveNotice = $"{fromFloor}층 {Tower.KindName(fromKind)} → {_run.Floor}층 {Tower.KindName(_run.FloorKind)}";
            _moveNoticeIsError = false;
        }

        // ================= 휴식 =================

        /// <summary>
        /// 휴식 창: 듀얼 창과 같은 틀. 이 층에 들어갈 때 맞춘 LP를 크게 보여 주고, 버튼 둘(올라간다, 덱 정리)을 좌우로 고른다.
        /// 덱 정리로 연 덱 창을 닫으면 이 창으로 돌아온다. 취소 키는 창을 닫는다 (메뉴의 "휴식"으로 다시 연다).
        /// </summary>
        void OpenRest()
        {
            if (_run == null || _run.Phase != RunPhase.Ready || _run.FloorKind != FloorKind.Rest)
                return;
            _restShownFloor = _run.Floor;
            var panel = OpenSlab("tower-rest", 880);
            panel.style.alignItems = Align.Center;
            var title = WindowTitle("쉬어 가는 곳");
            title.name = "rest-title";
            panel.Add(title);

            var heal = _run.Events.LastOrDefault(e => e.Kind == RunEventKind.LpRestored && e.LpReason == LpRestoreReason.Rest && e.Floor == _run.Floor);
            var middle = Row();
            middle.name = "rest-lp";
            middle.style.alignItems = Align.Center;
            middle.style.marginTop = 22;
            middle.style.marginBottom = 14;
            var icon = IconTile("속성:화염", 96, RestColor);
            icon.style.marginRight = 26;
            middle.Add(icon);
            var from = UiStyle.Label($"LP {(heal != null ? heal.PreviousLp : _run.Lp)}", 54, heal != null ? UiStyle.TextDim : UiStyle.Text);
            from.name = "rest-lp-from";
            UiStyle.Black(from);
            middle.Add(from);
            if (heal != null)
            {
                var arrow = UiStyle.Label("→", 54, UiStyle.Text);
                UiStyle.Black(arrow);
                arrow.style.marginLeft = arrow.style.marginRight = 16;
                middle.Add(arrow);
                var to = UiStyle.Label(heal.Lp.ToString(), 54, RestColor);
                to.name = "rest-lp-to";
                UiStyle.Black(to);
                middle.Add(to);
            }
            panel.Add(middle);
            var text = Text(heal != null ? "보스 층을 앞두고 잠시 쉬어 간다. 덱을 정리한 뒤 올라가도 된다."
                : "보스 층을 앞두고 잠시 쉬어 간다. LP는 이미 가득하다. 덱을 정리한 뒤 올라가도 된다.", 24);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(text);

            var buttons = new ButtonRow();
            buttons.Root.style.marginTop = 24;
            buttons.Add("rest-leave", $"{_run.Floor + 1}층으로 올라간다", LeaveFloor, gold: true, width: 330);
            buttons.Add("rest-deck", "덱 정리", () => OpenDeck(_deckTab == Pile.Sealed ? Pile.Main : _deckTab, fromDuel: true));
            buttons.Select(0);
            _windowRow = buttons;
            panel.Add(buttons.Root);
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "나중에"));
            hints.name = "modal-hints";
            hints.style.marginTop = 16;
            panel.Add(hints);
            _windowCancel = CloseModal;
            ApplyKeyCaps();
        }

        // ================= 보스 보상: 봉인 해제 =================

        void OpenUnseal()
        {
            var panel = OpenModal(1500);
            panel.name = "unseal";
            panel.style.height = Length.Percent(88);
            panel.Add(UiStyle.Label($"보스 보상 · {_run.Floor}층", 30, UiStyle.Accent, bold: true));
            var intro = Text("보스를 이겼습니다. 봉인된 카드 1장을 골라 봉인을 풉니다. 풀린 카드는 사이드 덱으로 갑니다. 두 번 누르면 바로 고릅니다.",
                16, UiStyle.TextDim, top: 6);
            intro.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(intro);
            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var grid = Grid(scroll.contentContainer);
            foreach (uint candidate in Sorted(_run.SealedCards()))
            {
                uint code = candidate;
                grid.Add(CardSlot(Pile.Unseal, code, CardFaceSize.Medium, onDoubleClick: () => ConfirmUnseal(code), draggable: false));
            }
            body.Add(scroll);
            var detail = UiStyle.PanelBox("unseal-detail");
            detail.style.width = 380;
            detail.style.marginLeft = 12;
            detail.style.backgroundColor = UiStyle.PanelLight;
            body.Add(detail);
            panel.Add(body);

            _detail = detail;
            _renderDetail = RenderUnsealDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        void RenderUnsealDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0 || pile != Pile.Unseal)
            {
                panel.Add(Text("왼쪽에서 봉인을 풀 카드를 고르세요.", 17, UiStyle.TextDim));
                return;
            }
            AddCardDetail(panel, code, pinned);
            if (!pinned)
                return;
            var confirm = UiStyle.Button("이 카드의 봉인 풀기", () => ConfirmUnseal(code), 20);
            confirm.name = "unseal-confirm";
            confirm.style.marginTop = 10;
            panel.Add(confirm);
        }

        void ConfirmUnseal(uint code)
        {
            try
            {
                _run.ChooseUnseal(code);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Persist();
            _moveNotice = $"보스 보상: {_text.CardName(code)} 봉인 해제 → 사이드 덱";
            _moveNoticeIsError = false;
            Debug.Log($"[런] {_moveNotice} → {_run.Floor}층");
            _selectedPile = Pile.Side;
            _selectedCode = code;
            ShowHub();
        }
    }
}
