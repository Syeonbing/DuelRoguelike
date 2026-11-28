// 런 화면: 골드 상점 (처음 화면의 명령 메뉴 "골드 상점"). docs/UI.md "상점 > 골드 상점". RunScreen.cs의 머리말 참고.
// 골드(영구 재화)를 쓰는 곳과 카드를 골드로 바꾸는 곳을 한 화면에 모은다. 규칙과 값은 GameSave·RunRules(Run 어셈블리)가 정하고,
// 화면은 값과 살 수 없는 까닭을 보여 준다. 런 시작 때의 사이드 덱 임시 칸은 새 런 창에서 산다 (RunScreen.Start.cs).
// 창이 아니라 화면 전체를 쓰는 장면이다 (gold-shop): 탑 앞 노점(ShopScene.cs)의 좌판에 물건을 놓고, 아래 판에 가진 골드와 설명을 모은다.
// 부품은 칩교환소와 같다 (RunScreen.Shop.cs). 닫으면(gold-shop-close) 처음 화면으로 돌아간다.
// - 두 창구를 Q·E(또는 팻말 gold-tab-buy, gold-tab-disassemble)로 바꾼다.
// - 산다: 좌판에 사이드 덱 확장(gold-shop-side), 카드 팩(gold-shop-pack), 스트럭처 덱(gold-shop-structure). 뒤의 둘은 아직 없어서 "입고 예정"이다.
//   사이드 덱 확장을 가리키면 아래 판에 칸 10개와 앞으로의 값이 나오고, 아래 줄의 금색 "산다"(gold-shop-buy)로 산다.
//   확인 창은 따로 없다: 결정 키가 좌판에서 "산다"로 내려가고, 한 번 더 눌러야 산다. 사면 띠 알림(gold-banner)이 뜬다.
// - 분해: 3장을 넘게 가진 카드(card-disassemble-코드)가 좌판에 깔린다. 결정 키나 금색 버튼(disassemble-one)은 1장씩 바로 분해한다 (다시 묻지 않는다).
//   남는 카드가 없어진 카드는 좌판에서 빠진다. "전부 분해"(disassemble-all)만 아래 판에서 한 번 묻는다
//   (gold-disassemble-confirm. 처음에는 "그만둔다"가 골라져 있다).
// - 키보드: 좌우로 좌판의 물건, 아래로 버튼 줄, 결정 키, 취소 키는 한 줄 위로 (좌판에서는 닫기).
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
        enum GoldTab
        {
            Buy,
            Disassemble,
        }

        const float GoldCardWidth = 142;
        static readonly Color StallInk = UiStyle.Hex(0xf3d9b0);

        /// <summary>골드 상점 화면의 무대 (열려 있지 않으면 null).</summary>
        VisualElement _goldStage;
        GoldTab _goldTab;
        /// <summary>아래 버튼 줄을 가리키는 중인지 (아니면 좌판).</summary>
        bool _goldButtons;
        /// <summary>좌판에서 가리키는 것: 산다 쪽의 상품, 분해 쪽의 카드.</summary>
        int _goldItem, _goldCard;
        int _goldButton;
        /// <summary>"전부 분해"를 묻는 중인지.</summary>
        bool _goldConfirmAll;
        /// <summary>분해 쪽 아래 판의 알림 (방금 분해한 것). 없으면 null.</summary>
        string _disassembleNotice;
        readonly List<(VisualElement Frame, Button Button)> _goldNavButtons = new List<(VisualElement, Button)>();

        bool GoldShopOpen => _goldStage != null && _goldStage.panel != null && _goldStage == _stage;

        // ================= 골드 상점 화면 =================

        void OpenGoldShop()
        {
            if (_titleMenu != null && _titleMenu.Root.panel != null)
                _titleMenuIndex = _titleMenu.Index; // 닫으면 메뉴의 "골드 상점"에 그대로 선다
            Sound.Play(Sfx.WindowOpen);
            _goldTab = GoldTab.Buy;
            _goldButtons = false;
            _goldItem = _goldCard = 0;
            _goldConfirmAll = false;
            _disassembleNotice = null;
            ShowGoldShop();
        }

        /// <summary>골드 상점을 닫는다: 처음 화면을 다시 그린다 (메뉴와 왼쪽 판의 골드가 맞는다).</summary>
        void CloseGoldShop()
        {
            _goldStage = null;
            _disassembleNotice = null;
            _goldConfirmAll = false;
            ShowStart();
        }

        /// <summary>분해할 수 있는 카드 (화면 순서: 몬스터 → 마법 → 함정 → 엑스트라, 카드명 순).</summary>
        List<uint> DisassembleCodes() => Sorted(_save.Disassemblable(_rarities).Select(x => x.Code)).ToList();

        /// <summary>골드 상점 화면을 지금 상태로 (다시) 그린다.</summary>
        void ShowGoldShop()
        {
            BeginScreen();
            _goldNavButtons.Clear();
            var stage = NewStage("gold-shop");
            _goldStage = stage;
            stage.Add(new ShopScene(ShopScene.Kind.GoldStall));
            stage.Add(ShopNameBoard("골드 상점", 810, 84, 300));

            var excess = _save.Disassemblable(_rarities);
            int excessCards = excess.Sum(x => x.Count), excessGold = excess.Sum(x => x.Count * x.GoldEach);
            var buyTab = GoldTabSign("gold-tab-buy", "산다", null, "Q", _goldTab == GoldTab.Buy, () => SetGoldTab(GoldTab.Buy), keyFirst: true);
            UiStyle.Place(buyTab, 286, 204);
            stage.Add(buyTab);
            var sellTab = GoldTabSign("gold-tab-disassemble", "분해", excessCards > 0 ? $"남는 카드 {excessCards}장" : "남는 카드 없음", "E",
                _goldTab == GoldTab.Disassemble, () => SetGoldTab(GoldTab.Disassemble), keyFirst: false);
            UiStyle.Place(sellTab, 446, 204);
            stage.Add(sellTab);
            stage.Add(ShopBubble(_goldTab == GoldTab.Disassemble ? (excessCards > 0 ? "남는 카드는 내가 사 주지." : "남는 카드가 없군.") : "어서 오게.", 1056, 234));

            var slab = ShopSlab("gold-shop-slab", 746, 310, out var body, out var foot);
            body.Add(CurrencyBox("gold-counter", "가진 골드", "재화:골드", UiStyle.Gold, _save.Gold, "gold-shop-gold", "gold-shop-after",
                "골드와 여기서 산 것은\n런이 끝나도 남는다."));
            body.Add(ShopDivider());
            var center = UiStyle.Box("gold-shop-center");
            center.style.flexGrow = 1;
            center.style.flexShrink = 1;
            body.Add(center);
            var hints = Row();
            hints.name = "gold-shop-hints";
            hints.style.alignItems = Align.Center;
            foot.Add(hints);
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            foot.Add(gap);
            var close = KeyButton("gold-shop-close", "닫기", CloseGoldShop, "X", "K", 112);
            foot.Add(close);

            string after = "";
            if (_goldTab == GoldTab.Buy)
                AddGoldBuy(stage, center, foot, ref after);
            else
                AddGoldDisassemble(stage, center, foot, excessCards, excessGold, ref after);
            stage.Add(slab);
            // 전부 분해를 묻는 동안에는 취소 키가 "그만둔다"라서 닫기 버튼을 숨긴다
            if (_goldConfirmAll)
                close.style.display = DisplayStyle.None;

            if (_goldNavButtons.Count == 0 || !_goldNavButtons.Any(b => b.Button.enabledSelf))
                _goldButtons = false;
            else if (!_goldNavButtons[Mathf.Clamp(_goldButton, 0, _goldNavButtons.Count - 1)].Button.enabledSelf)
                _goldButton = _goldNavButtons.FindLastIndex(b => b.Button.enabledSelf);
            _goldButton = Mathf.Clamp(_goldButton, 0, Mathf.Max(0, _goldNavButtons.Count - 1));
            for (int i = 0; i < _goldNavButtons.Count; i++)
                SetFocusFrame(_goldNavButtons[i].Frame, _goldButtons && i == _goldButton);
            var afterLabel = slab.Q<Label>("gold-shop-after");
            if (afterLabel != null)
                afterLabel.text = after;

            hints.Add(_goldConfirmAll ? Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "그만둔다"))
                : _goldButtons ? Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("↑", "W", "위 줄로"), ("X", "K", "뒤로"))
                : _goldTab == GoldTab.Buy ? Hints(("Q", "Q", null), ("E", "E", "산다 · 분해"), ("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "닫기"))
                : Hints(("Q", "Q", null), ("E", "E", "산다 · 분해"), ("←", "A", null), ("→", "D", "카드 고르기"), ("Z", "J", "1장 분해"), ("X", "K", "닫기")));
            ApplyKeyCaps();
        }

        void SetGoldTab(GoldTab tab)
        {
            _goldTab = tab;
            _goldButtons = false;
            _goldConfirmAll = false;
            ShowGoldShop();
        }

        /// <summary>창구를 바꾸는 팻말: 나무 판에 이름과 키 표시. 고른 쪽은 금색.</summary>
        Button GoldTabSign(string name, string text, string small, string key, bool on, Action click, bool keyFirst)
        {
            var sign = BareButton(name, click);
            sign.style.flexDirection = FlexDirection.Row;
            sign.style.alignItems = Align.Center;
            sign.style.height = 54;
            sign.style.paddingLeft = sign.style.paddingRight = 16;
            sign.style.backgroundColor = on ? UiStyle.Gold : SignWood;
            UiStyle.BorderColor(sign, on ? UiStyle.Hex(0x5f4519) : SignEdge, 3);
            sign.style.borderTopColor = on ? UiStyle.Hex(0xf6dc9b) : SignTop;
            UiStyle.Radius(sign, 6);
            var cap = KeyCap(key, key);
            cap.pickingMode = PickingMode.Ignore;
            if (keyFirst)
            {
                cap.style.marginRight = 10;
                sign.Add(cap);
            }
            var label = UiStyle.Label(text, 26, on ? GoldInk : UiStyle.Hex(0xc9b890), bold: true);
            label.pickingMode = PickingMode.Ignore;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            sign.Add(label);
            if (small != null)
            {
                var note = UiStyle.Label(small, 19, on ? GoldInk : UiStyle.Hex(0xc9b890));
                note.name = name + "-note";
                note.pickingMode = PickingMode.Ignore;
                note.style.whiteSpace = WhiteSpace.NoWrap;
                note.style.marginLeft = 10;
                sign.Add(note);
            }
            if (!keyFirst)
            {
                cap.style.marginLeft = 10;
                sign.Add(cap);
            }
            sign.EnableInClassList("selected", on);
            return sign;
        }

        /// <summary>아래 줄에 키보드로 고를 버튼을 더한다 (금색 테를 두를 수 있게 감싼다).</summary>
        Button AddGoldNavButton(VisualElement foot, string name, string text, Action run, float width, bool gold = false, bool enabled = true)
        {
            var button = UiStyle.StoneButton(text, run, gold, small: true);
            button.name = name;
            button.style.width = width;
            button.SetEnabled(enabled);
            var frame = FocusFrame(button);
            foot.Add(frame);
            _goldNavButtons.Add((frame, button));
            return button;
        }

        // ================= 산다 =================

        static readonly (string Name, string Pictogram, string Title)[] GoldGoods =
        {
            ("gold-shop-side", "상점:사이드 덱", "사이드 덱 확장"),
            ("gold-shop-pack", "상점:팩", "카드 팩"),
            ("gold-shop-structure", "상점:스트럭처 덱", "스트럭처 덱"),
        };

        void AddGoldBuy(VisualElement stage, VisualElement center, VisualElement foot, ref string after)
        {
            _goldItem = Mathf.Clamp(_goldItem, 0, GoldGoods.Length - 1);
            int limit = _save.SideDeckLimit, price = _save.SideDeckExpansionPrice;
            bool maxed = _save.SideDeckLimitMaxed, canBuy = _save.SideDeckExpansionProblem() == null;
            for (int i = 0; i < GoldGoods.Length; i++)
            {
                int index = i;
                bool on = i == _goldItem, soon = i > 0;
                var (name, pictogram, title) = GoldGoods[i];
                VisualElement tag;
                if (i == 0)
                    tag = maxed ? PriceTag(name + "-price", null, Color.clear, "다 늘렸다", UiStyle.Stone4, 19)
                        : PriceTag(name + "-price", "재화:골드", UiStyle.Gold, price.ToString(), canBuy ? UiStyle.Gold : UiStyle.Bad);
                else
                    tag = SoonTag(name, i == 1 ? RunRules.PackPrice : RunRules.StructureDeckPrice);
                var good = GoldGood(name, pictogram, i == 0 ? UiStyle.Gold : UiStyle.Stone4, title, tag, on, soon, () =>
                {
                    _goldItem = index;
                    _goldButtons = false;
                    ShowGoldShop();
                });
                UiStyle.Place(good, 330 + i * 520, 414);
                stage.Add(good);
            }

            if (_goldItem == 0)
            {
                center.Add(ShopHeadLine("사이드 덱 확장", $"임시 칸은 새 런을 시작할 때 산다 · 1장 골드 {RunRules.RunSideDeckSlotPrice}",
                    (maxed ? "영구 · 끝까지 늘렸다" : $"영구 · {limit + 1}장째", UiStyle.Gold, true)));
                var row = Row();
                row.style.alignItems = Align.Center;
                row.style.marginTop = 12;
                row.Add(SideSlots("gold-shop-side-slots", limit, canBuy ? limit + 1 : 0));
                var texts = UiStyle.Box();
                texts.style.marginLeft = 30;
                texts.style.flexShrink = 1;
                var change = Row();
                change.name = "gold-shop-side-change";
                change.style.alignItems = Align.Center;
                Label Big(string text, Color color)
                {
                    var label = UiStyle.Label(text, 44, color);
                    UiStyle.Black(label);
                    label.style.whiteSpace = WhiteSpace.NoWrap;
                    return label;
                }
                if (maxed)
                    change.Add(Big($"{limit}장", UiStyle.Gold));
                else
                {
                    change.Add(Big($"{limit}장", UiStyle.TextDim));
                    var arrow = Big("→", UiStyle.TextDim);
                    arrow.style.marginLeft = arrow.style.marginRight = 14;
                    change.Add(arrow);
                    change.Add(Big($"{limit + 1}장", UiStyle.Good));
                }
                texts.Add(change);
                string note = maxed ? $"시작 사이드 덱이 {limit}장이다. 더 늘릴 수 없다."
                    : !canBuy ? $"골드가 {price - _save.Gold} 모자라다."
                    : "런을 시작할 때 사이드 덱에 넣는 장수. 다음에 시작하는 런부터 쓴다. 되돌릴 수 없다.";
                var noteLabel = Text(note, 17, !canBuy && !maxed ? UiStyle.Bad : UiStyle.TextDim, top: 2);
                noteLabel.name = "gold-shop-side-note";
                noteLabel.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(noteLabel);
                row.Add(texts);
                center.Add(row);
                AddGoldNavButton(foot, "gold-shop-buy", maxed ? "산다" : $"산다 · 골드 {price}", BuySideDeckExpansion, 280, gold: true, enabled: canBuy);
                if (canBuy && _goldButtons)
                    after = $"사면 {_save.Gold - price}";
            }
            else
            {
                center.Add(ShopHeadLine(GoldGoods[_goldItem].Title, null, ("입고 예정", UiStyle.TextDim, true)));
                var note = Text(_goldItem == 1 ? "팩 구성이 정해지면 열린다." : "덱 목록이 정해지면 열린다.", 24, UiStyle.TextDim, top: 10);
                note.name = GoldGoods[_goldItem].Name + "-note";
                center.Add(note);
            }
        }

        /// <summary>"입고 예정 · (골드) 80": 아직 없는 상품의 딱지.</summary>
        static VisualElement SoonTag(string name, int price)
        {
            var tag = Row();
            tag.pickingMode = PickingMode.Ignore;
            tag.style.height = 42;
            tag.style.alignItems = Align.Center;
            tag.style.alignSelf = Align.Center;
            tag.style.paddingLeft = tag.style.paddingRight = 13;
            tag.style.backgroundColor = UiStyle.Stone9;
            UiStyle.BorderColor(tag, UiStyle.Stone5, 2);
            UiStyle.Radius(tag, 7);
            var soon = UiStyle.Label("입고 예정", 19, UiStyle.Stone4, bold: true);
            soon.name = name + "-tag";
            soon.style.whiteSpace = WhiteSpace.NoWrap;
            tag.Add(soon);
            var icon = Picto("재화:골드", 18, UiStyle.Stone4);
            icon.style.marginLeft = 10;
            icon.style.marginRight = 5;
            tag.Add(icon);
            var number = UiStyle.Label(price.ToString(), 19, UiStyle.Stone4, bold: true);
            number.name = name + "-price";
            tag.Add(number);
            return tag;
        }

        /// <summary>
        /// 좌판의 상품 하나 (누르면 가리킨다): 그림 칸(186×140), 이름, 가격표. 가리킨 상품은 올라오고 금색 테와 ▼가 붙는다.
        /// </summary>
        Button GoldGood(string name, string pictogram, Color glow, string title, VisualElement tag, bool on, bool soon, Action click)
        {
            const float width = 220, tile = 186, tileHeight = 140;
            var good = BareButton(name, click);
            good.style.width = width;
            good.style.height = CounterMark + 14 + tileHeight + 6 + 32 + 8 + 42;
            float top = CounterMark + (on ? 0 : 14);
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyle.Place(art, (width - tile) / 2, top, tile, tileHeight);
            art.style.alignItems = Align.Center;
            art.style.justifyContent = Justify.Center;
            art.style.backgroundColor = UiStyle.Hex(0x15110d);
            UiStyle.BorderColor(art, on ? UiStyle.Gold : UiStyle.Stone9, 3);
            if (!on)
                art.style.borderTopColor = PlaqueTop;
            UiStyle.Radius(art, 10);
            art.style.overflow = Overflow.Hidden;
            var light = new VisualElement { pickingMode = PickingMode.Ignore };
            light.style.position = Position.Absolute;
            light.style.left = light.style.top = light.style.right = light.style.bottom = 0;
            light.style.backgroundImage = new StyleBackground(UiStyle.Radial);
            light.style.unityBackgroundImageTintColor = UiStyle.Alpha(glow, soon ? 0.16f : 0.4f);
            art.Add(light);
            art.Add(Picto(pictogram, 92, soon ? UiStyle.Stone4 : UiStyle.Text));
            good.Add(art);
            var texts = UiStyle.Box();
            texts.pickingMode = PickingMode.Ignore;
            UiStyle.Place(texts, 0, CounterMark + 14 + tileHeight + 6, width);
            texts.style.alignItems = Align.Center;
            var label = UiStyle.Label(title, 22, soon ? UiStyle.Hex(0xd8c4a8) : StallInk, bold: true);
            label.pickingMode = PickingMode.Ignore;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            texts.Add(label);
            tag.style.marginTop = 8;
            texts.Add(tag);
            good.Add(texts);
            if (on)
            {
                var mark = UiStyle.Label("▼", 24, UiStyle.Gold);
                mark.pickingMode = PickingMode.Ignore;
                mark.style.unityTextAlign = TextAnchor.MiddleCenter;
                UiStyle.Place(mark, 0, -4, width, CounterMark);
                good.Add(mark);
            }
            good.EnableInClassList("selected", on);
            return good;
        }

        /// <summary>
        /// 시작 사이드 덱의 영구 칸 10개 (52×74)와 칸마다 그 칸을 살 때의 값. 가진 칸(owned)은 채우고, 다음에 살 칸(next, 1부터. 없으면 0)은 오름 색으로 가리킨다.
        /// </summary>
        static VisualElement SideSlots(string name, int owned, int next)
        {
            var row = UiStyle.Box(name, FlexDirection.Row);
            row.pickingMode = PickingMode.Ignore;
            row.style.flexShrink = 0;
            for (int i = 1; i <= RunRules.PermanentSideDeckMax; i++)
            {
                bool have = i <= owned, buying = i == next;
                var column = UiStyle.Box();
                column.style.alignItems = Align.Center;
                column.style.marginLeft = i == 1 ? 0 : 8;
                var slot = new VisualElement { name = have ? "slot-owned" : buying ? "slot-next" : "slot-empty" };
                slot.style.width = 52;
                slot.style.height = 74;
                slot.style.alignItems = Align.Center;
                slot.style.justifyContent = Justify.Center;
                UiStyle.Radius(slot, 6);
                UiStyle.BorderColor(slot, have ? UiStyle.Gold : buying ? UiStyle.Good : UiStyle.Stone5, 2);
                slot.style.backgroundColor = have ? UiStyle.Stone6 : buying ? UiStyle.Alpha(UiStyle.Good, 0.05f) : Color.clear;
                if (buying)
                {
                    var plus = UiStyle.Label("+", 30, UiStyle.Good);
                    UiStyle.Black(plus);
                    slot.Add(plus);
                    // 새로 생길 칸이 천천히 깜박인다
                    var blink = slot;
                    blink.schedule.Execute(() => blink.style.opacity = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.4f, 1f)).Every(33);
                }
                column.Add(slot);
                // 아직 없는 칸 아래에는 그 칸의 값 (50, 100, …): 앞으로 얼마가 드는지 보인다
                int nth = i - RunRules.StartingSideDeckMax;
                var price = UiStyle.Label(!have && nth >= 1 ? RunRules.SideDeckExpansionPrice(nth).ToString() : "", 15, buying ? UiStyle.Gold : UiStyle.Stone4, bold: true);
                price.style.height = 20;
                price.style.marginTop = 4;
                column.Add(price);
                row.Add(column);
            }
            return row;
        }

        void BuySideDeckExpansion()
        {
            int price = _save.SideDeckExpansionPrice;
            if (!_save.BuySideDeckExpansion())
            {
                ShowGoldShop();
                return;
            }
            Persist();
            Sound.Play(Sfx.Coin);
            Debug.Log($"[골드 상점] 사이드 덱 확장: 시작 사이드 덱 {_save.SideDeckLimit}장 (골드 {price}, 남은 골드 {_save.Gold})");
            _goldButtons = false; // 이어서 또 사려면 다시 "산다"로 내려가야 한다
            ShowGoldShop();
            ShowStageBanner("gold-banner", $"사이드 덱이 {_save.SideDeckLimit}장으로 늘었다.");
            ShopFloat("gold-counter", $"-{price}", UiStyle.Bad);
        }

        // ================= 분해 =================

        void AddGoldDisassemble(VisualElement stage, VisualElement center, VisualElement foot, int excessCards, int excessGold, ref string after)
        {
            var codes = DisassembleCodes();
            _goldCard = Mathf.Clamp(_goldCard, 0, Mathf.Max(0, codes.Count - 1));
            if (codes.Count == 0)
            {
                var none = UiStyle.Label($"{RunRules.DisassemblyKeep}장을 넘게 가진 카드가 없다.", 24, StallInk);
                none.name = "disassemble-none";
                none.pickingMode = PickingMode.Ignore;
                UiStyle.Place(none, 0, 540, StageWidth);
                none.style.unityTextAlign = TextAnchor.MiddleCenter;
                stage.Add(none);
                center.Add(ShopHeadLine("컬렉션 분해", null, ("남는 카드 없음", UiStyle.TextDim, true)));
                center.Add(Text($"카드마다 {RunRules.DisassemblyKeep}장은 남기고, 남는 카드를 골드로 바꾸는 곳이다.", 24, UiStyle.TextDim, top: 10));
                AddDisassembleNotice(center);
                _goldConfirmAll = false;
                return;
            }

            var row = CounterRow("disassemble-row", 196, 414, 1528, GoldCardWidth);
            VisualElement on = null;
            for (int i = 0; i < codes.Count; i++)
            {
                int index = i;
                uint code = codes[i];
                bool isOn = i == _goldCard;
                var slot = CounterCard(SlotName(Pile.Disassemble, code), code, isOn, () =>
                {
                    _goldCard = index;
                    _goldButtons = false;
                    _goldConfirmAll = false;
                    ShowGoldShop();
                }, GoldCardWidth, count: _collection.Count(code), tag: $"남는 카드 {_save.DisassemblableCount(code, _rarities)}");
                row.Add(slot);
                if (isOn)
                    on = slot;
            }
            stage.Add(row);
            ScrollCounterTo(row, on);

            if (_goldConfirmAll)
            {
                // 전부 분해: 아래 판에서 한 번 묻는다. 처음에는 "그만둔다"가 골라져 있어서 결정 키를 거듭 눌러도 분해되지 않는다.
                var byRarity = new int[RarityTable.RarityCount];
                foreach (var (card, count, _) in _save.Disassemblable(_rarities))
                    if (_rarities.TryGet(card, out var grade))
                        byRarity[(int)grade] += count;
                string breakdown = string.Join(" · ", Enumerable.Range(0, byRarity.Length).Reverse().Where(i => byRarity[i] > 0).Select(i => $"{(Rarity)i} {byRarity[i]}장"));
                var confirm = UiStyle.Box("gold-disassemble-confirm");
                var what = Text($"남는 카드 {excessCards}장을 모두 골드로 바꾼다.", 30, UiStyle.Text, bold: true);
                what.name = "disassemble-all-text";
                confirm.Add(what);
                confirm.Add(Text(breakdown, 19, UiStyle.TextDim, top: 6));
                confirm.Add(Text("되돌릴 수 없다. 분해한 카드는 돌아오지 않는다.", 19, UiStyle.Opponent, top: 4));
                var reward = Text($"받을 골드 +{excessGold}", 27, UiStyle.Good, top: 10);
                UiStyle.Black(reward);
                reward.name = "disassemble-all-gold";
                confirm.Add(reward);
                center.Add(confirm);
                var yes = AddGoldNavButton(foot, "disassemble-all-confirm", "분해한다", DisassembleAll, 240);
                yes.style.color = UiStyle.Bad;
                AddGoldNavButton(foot, "disassemble-all-cancel", "그만둔다", CancelDisassembleAll, 240, gold: true);
                after = $"분해하면 {_save.Gold + excessGold}";
                return;
            }

            uint pointed = codes[_goldCard];
            int excess = _save.DisassemblableCount(pointed, _rarities), each = GameSave.DisassemblyGold(pointed, _rarities);
            var tags = new List<(string, Color, bool)>();
            if (_rarities.TryGet(pointed, out var rarity))
                tags.Add((rarity.ToString(), CardFaces.RarityColor(rarity), true));
            var head = ShopHeadLine($"「{_text.CardName(pointed)}」", null, tags.ToArray());
            var gain = UiStyle.Label($"1장에 골드 +{each}", 27, UiStyle.Good);
            UiStyle.Black(gain);
            gain.name = "disassemble-each";
            gain.style.whiteSpace = WhiteSpace.NoWrap;
            gain.style.marginLeft = StyleKeyword.Auto;
            head.Add(gain);
            center.Add(head);
            var info = Text($"컬렉션 {_collection.Count(pointed)}장 · 남는 카드 {excess}장", 24, top: 6);
            info.name = "disassemble-info";
            center.Add(info);
            center.Add(Text($"카드마다 {RunRules.DisassemblyKeep}장은 남긴다. 분해한 카드는 돌아오지 않는다.", 19, UiStyle.TextDim, top: 2));
            var rates = string.Join(" · ", Enumerable.Range(0, RarityTable.RarityCount).Select(i => $"{(Rarity)i} {RunRules.DisassemblyGoldFor((Rarity)i)}"));
            var legend = Text("1장에 받는 골드   " + rates, 17, UiStyle.TextDim, top: 8);
            legend.name = "disassemble-rates";
            center.Add(legend);
            AddDisassembleNotice(center);

            AddGoldNavButton(foot, "disassemble-all", $"전부 분해 · {excessCards}장 · 골드 +{excessGold}", AskDisassembleAll, 300);
            AddGoldNavButton(foot, "disassemble-card-all", $"이 카드의 남는 카드 모두 · 골드 +{each * excess}", () => DisassembleCards(pointed, excess), 330, enabled: excess > 1);
            AddGoldNavButton(foot, "disassemble-one", $"1장 분해 · 골드 +{each}", () => DisassembleCards(pointed, 1), 250, gold: true);
            if (!_goldButtons)
                _goldButton = 2; // 버튼 줄로 내려가면 금색 "1장 분해"부터
            after = $"1장 분해하면 {_save.Gold + each}";
        }

        void AddDisassembleNotice(VisualElement center)
        {
            var notice = Text(_disassembleNotice ?? "", 19, UiStyle.Good, top: 6);
            notice.name = "disassemble-notice";
            notice.style.minHeight = 26;
            center.Add(notice);
        }

        /// <summary>카드 count장을 분해한다 (다시 묻지 않는다). 남는 카드가 없어진 카드는 좌판에서 빠지고, 같은 자리의 다음 카드를 가리킨다.</summary>
        void DisassembleCards(uint code, int count)
        {
            int index = DisassembleCodes().IndexOf(code);
            int gold = _save.Disassemble(code, count, _rarities);
            if (gold <= 0)
                return;
            Persist();
            Sound.Play(Sfx.Destroy);
            _disassembleNotice = $"「{_text.CardName(code)}」 {count}장을 분해했다. 골드 +{gold}";
            Debug.Log($"[골드 상점] 분해: {_text.CardName(code)} {count}장 → 골드 +{gold} (컬렉션 {_collection.Count(code)}장, 골드 {_save.Gold})");
            var after = DisassembleCodes();
            int at = after.IndexOf(code);
            _goldCard = at >= 0 ? at : Mathf.Clamp(index, 0, Mathf.Max(0, after.Count - 1));
            if (after.Count == 0)
                _goldButtons = false;
            ShowGoldShop();
            ShopFloat("gold-counter", $"+{gold}", UiStyle.Good);
        }

        void AskDisassembleAll()
        {
            _goldConfirmAll = true;
            _goldButtons = true;
            _goldButton = 1; // 그만둔다
            ShowGoldShop();
        }

        void CancelDisassembleAll()
        {
            _goldConfirmAll = false;
            _goldButtons = false;
            ShowGoldShop();
        }

        void DisassembleAll()
        {
            int gold = _save.DisassembleAll(_rarities, out int cards);
            if (cards > 0)
            {
                Persist();
                Sound.Play(Sfx.Destroy);
                Debug.Log($"[골드 상점] 전부 분해: {cards}장 → 골드 +{gold} (골드 {_save.Gold})");
            }
            _disassembleNotice = null;
            _goldConfirmAll = false;
            _goldButtons = false;
            _goldCard = 0;
            ShowGoldShop();
            if (cards > 0)
            {
                ShowStageBanner("gold-banner", $"카드 {cards}장을 분해했다. 골드 +{gold}");
                ShopFloat("gold-counter", $"+{gold}", UiStyle.Good);
            }
        }

        // ================= 키보드 =================

        void GoldShopKey(Nav nav)
        {
            if (nav == Nav.PrevTab || nav == Nav.NextTab)
            {
                if (!_goldConfirmAll)
                    SetGoldTab(nav == Nav.PrevTab ? GoldTab.Buy : GoldTab.Disassemble);
                return;
            }
            bool anyButton = _goldNavButtons.Any(b => b.Button.enabledSelf);
            if (_goldButtons)
            {
                // 아래 버튼 줄
                switch (nav)
                {
                    case Nav.Left:
                    case Nav.Right:
                        int n = _goldNavButtons.Count, i = _goldButton;
                        for (int step = 0; step < n; step++)
                        {
                            i = ((i + (nav == Nav.Left ? -1 : 1)) % n + n) % n;
                            if (_goldNavButtons[i].Button.enabledSelf)
                                break;
                        }
                        _goldButton = i;
                        ShowGoldShop();
                        break;
                    case Nav.Confirm:
                        if (_goldButton < _goldNavButtons.Count)
                            Press(_goldNavButtons[_goldButton].Button);
                        break;
                    case Nav.Up:
                    case Nav.Cancel:
                        if (_goldConfirmAll)
                        {
                            if (nav == Nav.Cancel)
                                CancelDisassembleAll();
                            break;
                        }
                        _goldButtons = false;
                        ShowGoldShop();
                        break;
                }
                return;
            }
            // 좌판
            int count = _goldTab == GoldTab.Buy ? GoldGoods.Length : DisassembleCodes().Count;
            switch (nav)
            {
                case Nav.Left:
                case Nav.Right:
                    if (count == 0)
                        break;
                    int delta = nav == Nav.Left ? -1 : 1;
                    if (_goldTab == GoldTab.Buy)
                        _goldItem = ((_goldItem + delta) % count + count) % count;
                    else
                        _goldCard = ((_goldCard + delta) % count + count) % count;
                    ShowGoldShop();
                    break;
                case Nav.Down:
                    if (anyButton)
                    {
                        _goldButtons = true;
                        ShowGoldShop();
                    }
                    break;
                case Nav.Confirm:
                    if (_goldTab == GoldTab.Disassemble)
                    {
                        // 결정 키는 가리킨 카드를 1장 분해한다
                        var codes = DisassembleCodes();
                        if (_goldCard < codes.Count)
                            DisassembleCards(codes[_goldCard], 1);
                    }
                    else if (anyButton)
                    {
                        // 사는 것은 "산다" 버튼으로 내려가서 한 번 더 누른다
                        _goldButtons = true;
                        ShowGoldShop();
                    }
                    break;
                case Nav.Cancel:
                    CloseGoldShop();
                    break;
            }
        }
    }
}
