// 런 화면: 탑 (10층 묶음), 칩교환소 창, 이벤트 창, 보스 보상(봉인 해제) 창. RunScreen.cs의 머리말 참고.
// - 탑 (tower): 층 사이 화면 오른쪽에 지금 10층 묶음을 위(보스)에서 아래(첫 층)로 쌓는다. 지난 층과 지금 층은 종류를 보여 주고,
//   앞 층은 정해진 층(칩교환소·이벤트·휴식·보스)만 보여 준다. 확률 이벤트 층이 될 수 있는 곳은 "?".
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

        static readonly Color RestColor = UiStyle.Hex(0x5ee07a);
        static readonly Color EventFloorColor = UiStyle.Hex(0x9b7ae0);

        static Color FloorColor(FloorKind kind)
        {
            switch (kind)
            {
                case FloorKind.Boss: return UiStyle.Opponent;
                case FloorKind.ChipExchange: return UiStyle.Accent;
                case FloorKind.Rest: return RestColor;
                case FloorKind.Event: return EventFloorColor;
                default: return UiStyle.Mine;
            }
        }

        // ================= 탑 =================

        VisualElement TowerPanel()
        {
            var panel = UiStyle.PanelBox("tower");
            panel.style.width = 250;
            panel.style.marginLeft = 12;
            int start = Tower.BlockStart(_run.Floor);
            panel.Add(UiStyle.Label($"탑 {start}~{start + Tower.BlockSize - 1}층", 18, UiStyle.TextDim, bold: true));
            var kinds = Tower.Block(_run.Seed, _run.Floor);
            for (int i = Tower.BlockSize - 1; i >= 0; i--)
            {
                int floor = start + i;
                bool current = floor == _run.Floor;
                bool past = floor < _run.Floor;
                bool preview = floor > _run.Floor && floor <= _run.Floor + _run.PreviewFloors && Tower.RandomEventPercent(floor) > 0;
                FloorKind? kind = current ? _run.FloorKind : past || preview ? kinds[i]
                    : Tower.RandomEventPercent(floor) > 0 ? (FloorKind?)null : Tower.FixedKind(floor);

                var box = Row();
                box.name = $"tower-floor-{floor}";
                box.style.alignItems = Align.Center;
                box.style.marginTop = 4;
                box.style.height = 36;
                box.style.backgroundColor = current ? UiStyle.PanelLight : UiStyle.ZoneEmpty;
                UiStyle.Radius(box, 4);
                UiStyle.BorderColor(box, current ? UiStyle.Accent : UiStyle.Border, current ? 2 : 1);
                if (past)
                    box.style.opacity = 0.45f;
                var stripe = new VisualElement();
                stripe.style.width = 8;
                stripe.style.alignSelf = Align.Stretch;
                stripe.style.backgroundColor = kind == null ? UiStyle.Border : FloorColor(kind.Value);
                box.Add(stripe);
                var number = Text($"{floor}층", 15, current ? UiStyle.Accent : UiStyle.TextDim, bold: current);
                number.style.width = 48;
                number.style.marginLeft = 8;
                number.style.flexShrink = 0;
                box.Add(number);
                var kindLabel = Text(kind == null ? "?" : Tower.KindName(kind.Value), 16, kind == null ? UiStyle.TextDim : UiStyle.Text, bold: current);
                kindLabel.style.flexShrink = 0;
                box.Add(kindLabel);
                if (preview)
                {
                    var seen = Text("미리보기", 12, UiStyle.TextDim);
                    seen.name = $"tower-preview-{floor}";
                    seen.style.marginLeft = StyleKeyword.Auto;
                    seen.style.marginRight = 6;
                    seen.style.flexShrink = 0;
                    box.Add(seen);
                }
                if (current)
                {
                    var here = Text("지금", 12, UiStyle.Accent, bold: true);
                    here.style.marginLeft = StyleKeyword.Auto;
                    here.style.marginRight = 6;
                    here.style.flexShrink = 0;
                    box.Add(here);
                }
                panel.Add(box);
            }
            var note = Text($"?: 전투 또는 이벤트 (들어가 봐야 안다). 미리보기: 바로 위 {_run.PreviewFloors}층까지 보인다 (아이템으로 늘어남).", 12, UiStyle.TextDim, top: 8);
            note.name = "tower-preview-note";
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);
            var chips = UiStyle.Label($"칩 {_run.Chips}", 22, UiStyle.Accent, bold: true);
            chips.name = "tower-chips";
            chips.style.marginTop = 10;
            panel.Add(chips);
            panel.Add(Text($"승리 +{RunRules.ChipsPerWin}, 보스 승리 +{RunRules.ChipsPerBossWin}. 칩교환소(5층마다)에서 씁니다.", 12, UiStyle.TextDim, top: 2));
            return panel;
        }

        /// <summary>층 사이 화면 위 상태 줄 오른쪽의 안내 한 줄 (층 종류마다).</summary>
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

        /// <summary>층 사이 화면 오른쪽 위의 큰 버튼: 전투 층은 듀얼 시작, 칩교환소는 열기·다음 층, 휴식은 다음 층, 이벤트는 이벤트 보기.</summary>
        void AddFloorButtons(VisualElement buttons)
        {
            bool ready = _run.Phase == RunPhase.Ready;
            switch (_run.FloorKind)
            {
                case FloorKind.Battle:
                case FloorKind.Boss:
                    buttons.Add(BigButton("next-duel", _run.FloorKind == FloorKind.Boss ? "보스 듀얼 시작" : "다음 듀얼 시작", StartNextDuel, ready));
                    break;
                case FloorKind.ChipExchange:
                    var shop = BigButton("open-shop", "칩교환소 열기", OpenShop, ready);
                    shop.style.marginRight = 10;
                    buttons.Add(shop);
                    buttons.Add(BigButton("leave-floor", "다음 층으로", LeaveFloor, ready));
                    break;
                case FloorKind.Rest:
                    var rest = BigButton("open-rest", "휴식 보기", OpenRest, ready);
                    rest.style.marginRight = 10;
                    buttons.Add(rest);
                    buttons.Add(BigButton("leave-floor", "다음 층으로", LeaveFloor, ready));
                    break;
                case FloorKind.Event:
                    if (_run.EventBattlePending)
                        buttons.Add(BigButton("next-duel", "이벤트 전투 시작", StartNextDuel, ready));
                    else if (_run.EventDone)
                        buttons.Add(BigButton("leave-floor", "다음 층으로", LeaveFloor, ready));
                    else
                        buttons.Add(BigButton("open-event", "이벤트 보기", OpenEvent, ready));
                    break;
            }
        }

        static Button BigButton(string name, string text, Action onClick, bool enabled)
        {
            var button = UiStyle.Button(text, onClick, 22);
            button.name = name;
            button.style.paddingLeft = button.style.paddingRight = 24;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.SetEnabled(enabled);
            return button;
        }

        /// <summary>칩교환소·휴식 층을 떠나 다음 층으로.</summary>
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
            _moveNotice = $"{from} → {_run.Floor}층 {Tower.KindName(_run.FloorKind)}";
            _moveNoticeIsError = false;
            Debug.Log($"[런] {_moveNotice}");
            ShowHub();
        }

        // ================= 칩교환소 =================

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
            var panel = OpenModal(1100);
            panel.name = "chip-exchange";
            var head = Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.Add(UiStyle.Label($"칩교환소 · {_run.Floor}층", 30, UiStyle.Accent, bold: true));
            var chips = UiStyle.Label($"보유 칩 {_run.Chips}", 24, UiStyle.Accent, bold: true);
            chips.name = "shop-chips";
            head.Add(chips);
            panel.Add(head);
            var intro = Text($"칩으로 봉인을 다루고 아이템 카드를 삽니다. 상품마다 이 칩교환소에서 한 번만 살 수 있습니다. 가격은 임시 값입니다. 상점 덱 {_run.ShopDeck.Count}/{RunRules.MaxShopDeck}장.", 15, UiStyle.TextDim, top: 6);
            intro.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(intro);

            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.maxHeight = 720;
            foreach (var offer in _run.Shop)
                list.Add(ShopRow(offer));
            if (_run.Shop.All(o => o.Product != ShopProduct.Unseal))
            {
                var none = ShopBox("shop-offer-Unseal-none");
                none.style.opacity = 0.5f;
                none.Add(UiStyle.Label(ChipExchange.Name(ShopProduct.Unseal), 19, bold: true));
                none.Add(Text("이번 칩교환소에는 없습니다 (칩교환소마다 확률로 나옵니다).", 14, UiStyle.TextDim, top: 2));
                list.Add(none);
            }
            panel.Add(list);

            var buttons = Row();
            buttons.style.marginTop = 14;
            buttons.style.justifyContent = Justify.FlexEnd;
            var close = UiStyle.Button("닫기", CloseModal, 20);
            close.name = "shop-close";
            close.style.marginRight = 8;
            buttons.Add(close);
            var leave = UiStyle.Button("다음 층으로", LeaveFloor, 20);
            leave.name = "shop-leave";
            buttons.Add(leave);
            panel.Add(buttons);
        }

        static VisualElement ShopBox(string name)
        {
            var box = UiStyle.Box(name);
            box.style.backgroundColor = UiStyle.PanelLight;
            UiStyle.Radius(box, 6);
            UiStyle.Padding(box, 12);
            box.style.marginTop = 8;
            return box;
        }

        VisualElement ShopRow(ShopOffer offer)
        {
            var box = ShopBox($"shop-offer-{offer.Product}");
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            if (offer.Sold)
                box.style.opacity = 0.55f;
            var texts = UiStyle.Box();
            texts.style.flexGrow = 1;
            texts.style.flexShrink = 1;
            texts.Add(UiStyle.Label(ChipExchange.Name(offer), 19, bold: true));
            var description = Text(ChipExchange.Description(offer), 14, UiStyle.TextDim, top: 2);
            description.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(description);
            bool ok = _run.CanBuy(offer.Product, out string reason);
            if (!ok && !offer.Sold)
            {
                var why = Text(reason, 13, UiStyle.Opponent, top: 2);
                why.name = $"shop-reason-{offer.Product}";
                texts.Add(why);
            }
            box.Add(texts);
            var price = UiStyle.Label($"칩 {offer.Price}", 22, UiStyle.Accent, bold: true);
            price.style.marginLeft = 12;
            price.style.marginRight = 12;
            box.Add(price);
            var product = offer.Product;
            var buy = UiStyle.Button(offer.Sold ? "샀음" : "사기", () => BeginPurchase(product), 18);
            buy.name = $"shop-buy-{offer.Product}";
            buy.style.width = 90;
            buy.style.unityTextAlign = TextAnchor.MiddleCenter;
            buy.SetEnabled(ok);
            box.Add(buy);
            return box;
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

        void OpenEvent()
        {
            var ev = _run?.CurrentEvent;
            if (ev == null || _run.Phase != RunPhase.Ready || _run.EventDone)
                return;
            _eventShownFloor = _run.Floor;
            if (_run.EventBattlePending)
            {
                OpenEventBattle();
                return;
            }
            var panel = OpenModal(760);
            panel.name = "tower-event";
            panel.Add(UiStyle.Label($"이벤트 · {_run.Floor}층", 16, UiStyle.TextDim));
            var title = UiStyle.Label(ev.Title, 30, UiStyle.Accent, bold: true);
            title.name = "event-title";
            panel.Add(title);
            // 성향은 도감에서 모든 선택지를 골라 본 이벤트만 보인다
            panel.Add(UiStyle.Label(ev.FormName + (_save.Codex.IsComplete(ev) ? $" · {TowerEvent.TendencyName(ev.Tendency)}" : ""), 14, UiStyle.TextDim));
            var text = Text(ev.Text, 18, top: 10);
            text.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(text);
            bool allLocked = _run.AllEventChoicesLocked;
            for (int i = 0; i < ev.Choices.Count; i++)
            {
                var option = ev.Choices[i];
                if (option.IsForced && !allLocked)
                    continue; // 강제 선택지는 다른 선택지가 모두 잠겼을 때만 나온다
                int index = i;
                bool can = _run.CanChooseEventOption(index, out string reason);
                var choice = UiStyle.Button(option.Label, () => ChooseEvent(index), 20);
                choice.name = $"event-choice-{i}";
                choice.style.marginTop = 10;
                choice.style.whiteSpace = WhiteSpace.Normal;
                choice.SetEnabled(can);
                panel.Add(choice);
                var notes = new List<string>();
                if (option.HasCondition)
                    notes.Add((can ? "조건 충족: " : "잠김 — 조건: ") + RunState.ConditionText(option, ev.Scope));
                if (option.IsBattle || KnowsResult(ev, index))
                    notes.Add(KnowsResult(ev, index) ? "결과: " + option.DescribeEffects()
                        : $"이벤트 전투 (상대 {StarterDecks.Find(option.BattleDeckId)?.Name ?? option.BattleDeckId}) — 지면 일반 패배처럼 봉인");
                if (notes.Count > 0)
                {
                    var note = Text(string.Join("\n", notes), 14, can ? UiStyle.TextDim : UiStyle.Opponent, top: 2);
                    note.name = $"event-choice-note-{i}";
                    note.style.whiteSpace = WhiteSpace.Normal;
                    panel.Add(note);
                }
            }
            if (_run.CanChooseEventOption(-1, out _))
            {
                var pass = UiStyle.Button("그냥 지나간다", () => ChooseEvent(-1), 20);
                pass.name = "event-pass";
                pass.style.marginTop = 10;
                panel.Add(pass);
            }
            var later = UiStyle.Button("나중에 (덱 정리)", CloseModal, 16);
            later.name = "event-close";
            later.style.marginTop = 14;
            later.style.alignSelf = Align.FlexEnd;
            panel.Add(later);
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
                ShowHub(); // 층 사이 화면의 버튼을 "이벤트 전투 시작"으로 바꾼 뒤 전투 창
                OpenEventBattle();
                return;
            }
            _eventResult = new EventResultView(ev, option, floor, won: null, _run.Events.Skip(mark).ToList());
            if (_run.Floor != floor)
                SetFloorNotice(floor, FloorKind.Event);
            ShowHub();
        }

        /// <summary>이벤트 전투를 기다리는 창: 상대와 이기면 받을 것을 보여 주고 듀얼을 시작한다.</summary>
        void OpenEventBattle()
        {
            var ev = _run?.CurrentEvent;
            var option = _run?.PendingEventBattle;
            if (ev == null || option == null || _run.Phase != RunPhase.Ready)
                return;
            var panel = OpenModal(760);
            panel.name = "event-battle";
            panel.Add(UiStyle.Label($"이벤트 전투 · {_run.Floor}층", 16, UiStyle.TextDim));
            panel.Add(UiStyle.Label(ev.Title, 30, UiStyle.Accent, bold: true));
            AddWrapped(panel, option.ResultText, 18, UiStyle.Text, 10);
            AddWrapped(panel, $"상대: {StarterDecks.Find(option.BattleDeckId)?.Name ?? option.BattleDeckId}. " +
                              "이기면 이벤트의 결과를 받습니다 (일반 승리 보상·칩은 없음, LP는 승리처럼 오름). 지면 일반 패배처럼 봉인됩니다. 어느 쪽이든 다음 층으로 갑니다.",
                16, UiStyle.TextDim, 8);
            var start = UiStyle.Button("이벤트 전투 시작", StartNextDuel, 20);
            start.name = "event-battle-start";
            start.style.marginTop = 14;
            panel.Add(start);
            var later = UiStyle.Button("나중에 (덱 정리)", CloseModal, 16);
            later.name = "event-battle-close";
            later.style.marginTop = 14;
            later.style.alignSelf = Align.FlexEnd;
            panel.Add(later);
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

        void OpenEventResult()
        {
            var view = _eventResult;
            if (view == null)
                return;
            var panel = OpenModal(760);
            panel.name = "event-result";
            panel.Add(UiStyle.Label($"이벤트 결과 · {view.Floor}층", 16, UiStyle.TextDim));
            panel.Add(UiStyle.Label(view.Event.Title, 30, UiStyle.Accent, bold: true));
            panel.Add(UiStyle.Label(view.Choice?.Label ?? "그냥 지나간다", 20, UiStyle.Text, bold: true));
            string text = view.Won == false ? "이벤트 전투에서 졌습니다. 이벤트의 결과는 받지 못했습니다."
                : view.Choice?.ResultText ?? "아무 일 없이 지나갔습니다.";
            AddWrapped(panel, text, 18, UiStyle.Text, 10);
            var list = new VisualElement { name = "event-result-list" };
            list.style.marginTop = 10;
            foreach (var e in view.Happened)
                list.Add(Text(e.Describe(_text.CardName), 16, EventColor(e.Kind), top: 2));
            if (view.Happened.Count == 0)
                list.Add(Text("바뀐 것 없음", 16, UiStyle.TextDim, top: 2));
            panel.Add(list);
            var ok = UiStyle.Button("확인", () =>
            {
                _eventResult = null;
                ShowHub();
            }, 20);
            ok.name = "event-result-ok";
            ok.style.marginTop = 14;
            panel.Add(ok);
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

        /// <summary>휴식 창: 이 층에 들어갈 때 맞춘 LP를 보여 주고, 다음 층으로 가거나 닫고 덱을 정리한다.</summary>
        void OpenRest()
        {
            if (_run == null || _run.Phase != RunPhase.Ready || _run.FloorKind != FloorKind.Rest)
                return;
            _restShownFloor = _run.Floor;
            var panel = OpenModal(760);
            panel.name = "tower-rest";
            panel.Add(UiStyle.Label($"휴식 · {_run.Floor}층", 16, UiStyle.TextDim));
            var title = UiStyle.Label("쉬어 가는 곳", 30, UiStyle.Accent, bold: true);
            title.name = "rest-title";
            panel.Add(title);
            var heal = _run.Events.LastOrDefault(e => e.Kind == RunEventKind.LpRestored && e.LpReason == LpRestoreReason.Rest && e.Floor == _run.Floor);
            var text = Text($"보스 층을 앞두고 잠시 쉬어 갑니다. LP를 {RunRules.FullLp}로 맞췄습니다. 덱을 정리한 뒤 다음 층으로 올라가세요.", 18, top: 10);
            text.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(text);
            var lp = UiStyle.Label(heal != null ? $"LP {heal.PreviousLp} → {heal.Lp}" : $"LP {_run.Lp} (그대로)", 28, RestColor, bold: true);
            lp.name = "rest-lp";
            lp.style.marginTop = 12;
            panel.Add(lp);
            var leave = UiStyle.Button("다음 층으로", LeaveFloor, 20);
            leave.name = "rest-leave";
            leave.style.marginTop = 14;
            panel.Add(leave);
            var later = UiStyle.Button("나중에 (덱 정리)", CloseModal, 16);
            later.name = "rest-close";
            later.style.marginTop = 14;
            later.style.alignSelf = Align.FlexEnd;
            panel.Add(later);
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
