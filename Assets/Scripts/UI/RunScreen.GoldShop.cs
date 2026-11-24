// 런 화면: 골드 상점 (처음 화면의 명령 메뉴 "골드 상점"). docs/UI.md "나머지 창 > 골드 상점". RunScreen.cs의 머리말 참고.
// 골드(영구 재화)를 쓰는 곳과 카드를 골드로 바꾸는 곳을 한 창에 모은다. 규칙과 값은 GameSave·RunRules(Run 어셈블리)가 정하고,
// 화면은 값과 살 수 없는 까닭을 보여 준다. 런 시작 때의 사이드 덱 임시 칸은 새 런 창에서 산다 (RunScreen.Start.cs).
// - 골드 상점 창(gold-shop): 칩교환소와 같은 틀의 목록. 사이드 덱 확장(gold-shop-side), 컬렉션 분해(gold-shop-disassemble),
//   카드 팩(gold-shop-pack)·스트럭처 덱(gold-shop-structure)은 아직 없어서 "준비 중"으로 꺼져 있다.
//   닫으면(gold-shop-close) 처음 화면을 다시 그린다 (메뉴와 왼쪽 판의 골드).
// - 구매 확인 창(gold-shop-confirm): 사이드 덱 확장은 되돌릴 수 없어서 한 번 묻는다. "산다"(gold-shop-buy)가 금색이고 처음에 골라져 있다.
// - 컬렉션 분해 창(gold-disassemble): 카드 고르기 창과 같은 틀과 키보드. 3장을 넘게 가진 카드만 나온다.
//   결정 키·두 번 누르기·금색 버튼(disassemble-one)은 1장씩 바로 분해한다 (다시 묻지 않는다). 넘는 분이 없어진 카드는 격자에서 빠진다.
//   "전부 분해"(disassemble-all)만 한 번 묻는다 (gold-disassemble-confirm, 처음에는 "그만둔다"가 골라져 있다).
// - 띠 알림(gold-banner): 사거나 전부 분해한 뒤 창 위에 잠깐 뜬다. 누를 필요가 없다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        const float DisassembleDetailHeight = 560;

        /// <summary>골드 상점 창에서 가리키던 줄 (다른 창에 다녀와도 같은 자리에 서게).</summary>
        int _goldShopIndex;
        /// <summary>분해 창 아래의 알림 (방금 분해한 것). 없으면 null.</summary>
        string _disassembleNotice;

        // ================= 골드 상점 창 =================

        void OpenGoldShop()
        {
            if (_titleMenu != null && _titleMenu.Root.panel != null)
                _titleMenuIndex = _titleMenu.Index; // 닫으면 메뉴의 "골드 상점"에 그대로 선다
            var panel = OpenSlab("gold-shop", 1260);
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            var titles = UiStyle.Box();
            titles.Add(WindowTitle("골드 상점"));
            titles.Add(Text("골드는 런이 끝나도 남는다. 여기서 사거나 바꾼 것도 계속 남는다.", 19, UiStyle.TextDim, top: 4));
            head.Add(titles);
            head.Add(GoldCounter("gold-shop-gold"));
            panel.Add(head);

            var menu = new MenuList();
            menu.Root.style.marginTop = 14;
            GoldShopSideRow(menu);
            GoldShopDisassembleRow(menu);
            GoldShopSoonRow(menu, "gold-shop-pack", "상점:팩", "카드 팩", "팩 구성이 정해지면 열린다.", RunRules.PackPrice);
            GoldShopSoonRow(menu, "gold-shop-structure", "상점:스트럭처 덱", "스트럭처 덱", "덱 목록이 정해지면 열린다.", RunRules.StructureDeckPrice);
            panel.Add(menu.Root);
            menu.SelectEnabled(_goldShopIndex);
            _windowMenu = menu;

            panel.Add(WindowFooter(Hints(("↑", "W", null), ("↓", "S", "고르기"), ("Z", "J", "결정"), ("X", "K", "닫기")),
                KeyButton("gold-shop-close", "닫기", CloseGoldShop, "X", "K", 112)));
            _windowCancel = CloseGoldShop;
            ApplyKeyCaps();
        }

        /// <summary>골드 상점을 닫는다: 처음 화면을 다시 그려 메뉴와 왼쪽 판의 골드를 맞춘다.</summary>
        void CloseGoldShop()
        {
            _disassembleNotice = null;
            ShowStart();
        }

        /// <summary>창 머리 오른쪽의 가진 골드 (칩교환소의 가진 칩과 같은 자리).</summary>
        VisualElement GoldCounter(string name)
        {
            var box = UiStyle.Box("gold-counter");
            box.pickingMode = PickingMode.Ignore;
            box.style.marginLeft = StyleKeyword.Auto;
            box.style.alignItems = Align.FlexEnd;
            box.style.flexShrink = 0;
            box.Add(Text("가진 골드", 17, UiStyle.TextDim));
            var number = UiStyle.Label(_save.Gold.ToString(), 44, UiStyle.Gold);
            UiStyle.Black(number);
            number.name = name;
            number.style.whiteSpace = WhiteSpace.NoWrap;
            number.style.marginTop = -6;
            box.Add(number);
            return box;
        }

        /// <summary>
        /// 골드 상점의 한 줄: 아이콘(56), 이름(26 Bold)과 꼬리표, 그 아래 설명 한 줄(18). 칩교환소의 상품 줄과 같은 꼴이다.
        /// 줄을 돌려준다 (오른쪽 끝에 붙일 값은 부른 쪽이 더한다).
        /// </summary>
        Button GoldShopRow(MenuList menu, string name, Action run, bool enabled, string pictogram, Color iconColor, string title, string tagText,
            string note, Color noteColor)
        {
            var row = menu.AddRow(name, run, enabled, height: 0);
            row.style.minHeight = 92;
            var icon = IconTile(pictogram, 56, iconColor);
            icon.style.marginRight = 16;
            row.Add(icon);

            var texts = UiStyle.Box();
            texts.pickingMode = PickingMode.Ignore;
            texts.style.flexShrink = 1;
            var names = Row();
            names.pickingMode = PickingMode.Ignore;
            names.style.alignItems = Align.Center;
            var label = RowLabel(title, 26, UiStyle.Text, bold: true);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            names.Add(label);
            if (tagText != null)
            {
                var tag = RowLabel(tagText, 19, enabled ? UiStyle.Gold : UiStyle.TextDim, bold: true);
                tag.name = name + "-tag";
                tag.style.whiteSpace = WhiteSpace.NoWrap;
                tag.style.marginLeft = 12;
                tag.style.paddingLeft = tag.style.paddingRight = 10;
                tag.style.paddingTop = tag.style.paddingBottom = 1;
                UiStyle.BorderColor(tag, UiStyle.Stone5, 2);
                UiStyle.Radius(tag, 14);
                names.Add(tag);
            }
            texts.Add(names);
            var description = RowLabel(note, 18, noteColor);
            description.name = name + "-note";
            description.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(description);
            row.Add(texts);
            return row;
        }

        /// <summary>줄 오른쪽 끝의 값 (27 Black).</summary>
        static Label GoldShopValue(string name, string text, Color color)
        {
            var value = RowTag(text, 27, color);
            UiStyle.Black(value);
            value.name = name;
            return value;
        }

        void GoldShopSideRow(MenuList menu)
        {
            int limit = _save.SideDeckLimit, price = _save.SideDeckExpansionPrice;
            bool maxed = _save.SideDeckLimitMaxed, ok = _save.SideDeckExpansionProblem() == null;
            string tag = maxed ? "영구 · 끝까지 늘렸다" : $"영구 · {limit + 1}장째";
            string note = maxed ? $"시작 사이드 덱이 {limit}장이다. 더 늘릴 수 없다."
                : !ok ? $"골드가 {price - _save.Gold} 모자라다."
                : $"런을 시작할 때 사이드 덱에 넣는 장수가 {limit}장에서 {limit + 1}장으로 늘어난다.";
            var row = GoldShopRow(menu, "gold-shop-side", OpenSideDeckConfirm, ok, "상점:사이드 덱", UiStyle.Gold, "사이드 덱 확장", tag, note,
                !ok && !maxed ? UiStyle.Bad : UiStyle.TextDim);
            // 10칸 중 가진 칸. 살 수 있으면 다음 칸을 오름 색으로 가리킨다.
            var pips = SideSlotPips("gold-shop-side-slots", limit, ok ? limit + 1 : 0, large: false);
            pips.style.marginLeft = StyleKeyword.Auto;
            pips.style.paddingLeft = 16;
            row.Add(pips);
            var value = GoldShopValue("gold-shop-side-price", maxed ? "샀음" : $"골드 {price}", maxed ? UiStyle.TextDim : ok ? UiStyle.Gold : UiStyle.Bad);
            value.style.marginLeft = 0;
            value.style.minWidth = 150;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(value);
        }

        void GoldShopDisassembleRow(MenuList menu)
        {
            var list = _save.Disassemblable(_rarities);
            int cards = list.Sum(x => x.Count), gold = list.Sum(x => x.Count * x.GoldEach);
            bool any = cards > 0;
            var row = GoldShopRow(menu, "gold-shop-disassemble", OpenDisassemble, any, "상점:분해", UiStyle.Good, "컬렉션 분해",
                any ? $"넘는 분 {cards}장" : "넘는 분 없음",
                any ? $"카드마다 {RunRules.DisassemblyKeep}장은 남기고, 넘는 분을 골드로 바꾼다." : $"{RunRules.DisassemblyKeep}장을 넘게 가진 카드가 없다.",
                any ? UiStyle.TextDim : UiStyle.Bad);
            if (!any)
                return;
            var gain = UiStyle.Box();
            gain.pickingMode = PickingMode.Ignore;
            gain.style.marginLeft = StyleKeyword.Auto;
            gain.style.paddingLeft = 16;
            gain.style.alignItems = Align.FlexEnd;
            gain.style.flexShrink = 0;
            var value = RowLabel($"골드 +{gold}", 27, UiStyle.Good);
            UiStyle.Black(value);
            value.name = "gold-shop-disassemble-gold";
            value.style.whiteSpace = WhiteSpace.NoWrap;
            gain.Add(value);
            gain.Add(RowLabel("모두 분해하면", 15, UiStyle.TextDim));
            row.Add(gain);
        }

        /// <summary>아직 없는 상품의 자리 (카드 팩, 스트럭처 덱): 꺼져 있고 값만 보인다.</summary>
        void GoldShopSoonRow(MenuList menu, string name, string pictogram, string title, string note, int price)
        {
            var row = GoldShopRow(menu, name, () => { }, false, pictogram, UiStyle.Stone4, title, "준비 중", note, UiStyle.TextDim);
            row.Add(GoldShopValue(name + "-price", $"골드 {price}", UiStyle.TextDim));
        }

        /// <summary>
        /// 시작 사이드 덱의 영구 칸 10개. 가진 칸(owned)은 채우고, 다음에 살 칸(next, 1부터. 없으면 0)은 오름 색 테두리로 가리킨다.
        /// large면 확인 창의 큰 칸(52×74)이고 다음 칸에 +를 쓴다. 아니면 목록 줄의 작은 칸(18×26).
        /// </summary>
        static VisualElement SideSlotPips(string name, int owned, int next, bool large)
        {
            var row = UiStyle.Box(name, FlexDirection.Row);
            row.pickingMode = PickingMode.Ignore;
            row.style.flexShrink = 0;
            for (int i = 1; i <= RunRules.PermanentSideDeckMax; i++)
            {
                bool have = i <= owned, buying = i == next;
                var pip = new VisualElement { name = have ? "slot-owned" : buying ? "slot-next" : "slot-empty" };
                pip.pickingMode = PickingMode.Ignore;
                pip.style.width = large ? 52 : 18;
                pip.style.height = large ? 74 : 26;
                pip.style.marginLeft = i == 1 ? 0 : large ? 10 : 5;
                pip.style.alignItems = Align.Center;
                pip.style.justifyContent = Justify.Center;
                UiStyle.Radius(pip, large ? 6 : 3);
                UiStyle.BorderColor(pip, have ? (large ? UiStyle.Gold : UiStyle.GoldDeep) : buying ? UiStyle.Good : UiStyle.Stone5, 2);
                pip.style.backgroundColor = have ? (large ? UiStyle.Stone6 : UiStyle.Gold) : buying && large ? UiStyle.Alpha(UiStyle.Good, 0.05f) : Color.clear;
                if (buying && large)
                {
                    var plus = UiStyle.Label("+", 30, UiStyle.Good);
                    UiStyle.Black(plus);
                    plus.pickingMode = PickingMode.Ignore;
                    pip.Add(plus);
                    // 새로 생길 칸이 천천히 깜박인다
                    var blink = pip;
                    blink.schedule.Execute(() => blink.style.opacity = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.4f, 1f)).Every(33);
                }
                row.Add(pip);
            }
            return row;
        }

        // ================= 사이드 덱 확장: 구매 확인 =================

        void OpenSideDeckConfirm()
        {
            _goldShopIndex = 0;
            if (_save.SideDeckExpansionProblem() != null)
                return;
            int limit = _save.SideDeckLimit, price = _save.SideDeckExpansionPrice;
            var panel = OpenSlab("gold-shop-confirm", 880);
            panel.style.alignItems = Align.Center;
            panel.Add(WindowTitle("사이드 덱 확장"));
            panel.Add(Text("시작 사이드 덱", 19, UiStyle.TextDim, top: 16));
            var slots = SideSlotPips("gold-confirm-slots", limit, limit + 1, large: true);
            slots.style.marginTop = 10;
            panel.Add(slots);

            var change = Row();
            change.name = "gold-confirm-change";
            change.style.alignItems = Align.Center;
            change.style.marginTop = 10;
            Label Big(string text, Color color)
            {
                var label = UiStyle.Label(text, 54, color);
                UiStyle.Black(label);
                label.style.whiteSpace = WhiteSpace.NoWrap;
                return label;
            }
            change.Add(Big($"{limit}장", UiStyle.TextDim));
            var arrow = Big("→", UiStyle.TextDim);
            arrow.style.marginLeft = arrow.style.marginRight = 18;
            change.Add(arrow);
            change.Add(Big($"{limit + 1}장", UiStyle.Good));
            panel.Add(change);

            var note = Text("한 번 사면 계속 남는다. 다음에 시작하는 런부터 쓴다. 되돌릴 수 없다.", 19, UiStyle.TextDim, top: 6);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(note);
            var cost = Row();
            cost.style.alignItems = Align.FlexEnd;
            cost.style.marginTop = 14;
            var priceLabel = UiStyle.Label($"골드 {price}", 27, UiStyle.Gold);
            UiStyle.Black(priceLabel);
            priceLabel.name = "gold-confirm-price";
            cost.Add(priceLabel);
            var after = Text($"가진 골드 {_save.Gold} → {_save.Gold - price}", 19, UiStyle.TextDim);
            after.name = "gold-confirm-after";
            after.style.marginLeft = 16;
            after.style.marginBottom = 3;
            cost.Add(after);
            panel.Add(cost);

            var buttons = new ButtonRow();
            buttons.Root.style.marginTop = 22;
            buttons.Add("gold-shop-buy", "산다", BuySideDeckExpansion, gold: true, width: 300);
            buttons.Add("gold-shop-buy-cancel", "그만둔다", OpenGoldShop, width: 300);
            buttons.Select(0);
            _windowRow = buttons;
            panel.Add(buttons.Root);
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "그만둔다"));
            hints.name = "modal-hints";
            hints.style.marginTop = 16;
            panel.Add(hints);
            _windowCancel = OpenGoldShop;
            ApplyKeyCaps();
        }

        void BuySideDeckExpansion()
        {
            int price = _save.SideDeckExpansionPrice;
            if (!_save.BuySideDeckExpansion())
            {
                OpenGoldShop();
                return;
            }
            Persist();
            Sound.Play(Sfx.Coin);
            Debug.Log($"[골드 상점] 사이드 덱 확장: 시작 사이드 덱 {_save.SideDeckLimit}장 (골드 {price}, 남은 골드 {_save.Gold})");
            OpenGoldShop();
            ShowGoldBanner($"사이드 덱이 {_save.SideDeckLimit}장으로 늘었다.");
        }

        // ================= 띠 알림 (창 위) =================

        /// <summary>
        /// 창 위에 띠 알림을 띄운다 (docs/UI.md "부품 > 띠 알림"과 같은 모양·시간). 창 층에 무대와 같은 배율의 판을 깔고 그 위에 띠를 놓는다.
        /// 창이 바뀌면 띠도 함께 사라진다.
        /// </summary>
        void ShowGoldBanner(string text)
        {
            var layer = new VisualElement { name = "gold-banner-layer", pickingMode = PickingMode.Ignore };
            UiStyle.Place(layer, 0, 0, StageWidth, StageHeight);
            layer.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
            layer.AddToClassList(FitLayerClass);
            var banner = BannerStrip("gold-banner", text);
            layer.Add(banner);
            _modalLayer.Add(layer);
            FitLayer(layer);
            StartCoroutine(GoldBannerRoutine(layer, banner));
        }

        /// <summary>띠 알림의 띠: 무대 가운데를 가로지르는 어두운 띠와 큰 글 (이름은 name, 글은 name-text).</summary>
        static VisualElement BannerStrip(string name, string text)
        {
            var banner = Row();
            banner.name = name;
            banner.pickingMode = PickingMode.Ignore;
            UiStyle.Place(banner, 0, 482, StageWidth, 116);
            banner.style.alignItems = Align.Center;
            banner.style.justifyContent = Justify.Center;
            banner.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.92f);
            banner.style.borderTopWidth = banner.style.borderBottomWidth = 3;
            banner.style.borderTopColor = banner.style.borderBottomColor = UiStyle.GoldDeep;
            var label = UiStyle.Label(text, 48);
            UiStyle.Black(label);
            label.name = name + "-text";
            label.pickingMode = PickingMode.Ignore;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            banner.Add(label);
            return banner;
        }

        IEnumerator GoldBannerRoutine(VisualElement layer, VisualElement banner)
        {
            yield return Tween(0.16f, t =>
            {
                banner.style.opacity = t;
                banner.style.scale = new Scale(new Vector3(1f, 0.2f + 0.8f * t, 1f));
            });
            yield return Tween(1.15f, _ => { });
            yield return Tween(0.2f, t => banner.style.opacity = 1f - t);
            layer.RemoveFromHierarchy();
        }

        /// <summary>가진 골드 옆에 "+N"이 떠올랐다 사라진다 (분해로 골드를 얻었을 때).</summary>
        void ShowGoldGain(int gold)
        {
            var counter = _modalLayer.Q("gold-counter");
            if (counter == null)
                return;
            var gain = UiStyle.Label($"+{gold}", 34, UiStyle.Good);
            UiStyle.Black(gain);
            gain.name = "gold-gain";
            gain.pickingMode = PickingMode.Ignore;
            gain.style.whiteSpace = WhiteSpace.NoWrap;
            gain.style.position = Position.Absolute;
            gain.style.right = 0;
            gain.style.top = 58;
            counter.Add(gain);
            StartCoroutine(GoldGainRoutine(gain));
        }

        static IEnumerator GoldGainRoutine(VisualElement gain)
        {
            yield return Tween(0.9f, t =>
            {
                gain.style.opacity = t < 0.25f ? t * 4f : 1f - (t - 0.25f) / 0.75f;
                gain.style.translate = new Translate(0, 16f - 40f * t);
            });
            gain.RemoveFromHierarchy();
        }

        // ================= 컬렉션 분해 =================

        /// <summary>분해할 수 있는 카드 (화면 순서: 몬스터 → 마법 → 함정 → 엑스트라, 카드명 순).</summary>
        List<uint> DisassembleCodes() => Sorted(_save.Disassemblable(_rarities).Select(x => x.Code)).ToList();

        void OpenDisassemble()
        {
            _goldShopIndex = 1;
            var codes = DisassembleCodes();
            if (_selectedPile != Pile.Disassemble || !codes.Contains(_selectedCode))
            {
                _selectedPile = Pile.Disassemble;
                _selectedCode = 0;
            }
            _cardViews.Clear(); // 처음 화면에는 카드가 없다. 이 창을 다시 그릴 때마다 비운다.
            int totalCards = codes.Sum(c => _save.DisassemblableCount(c, _rarities));
            int totalGold = codes.Sum(c => _save.DisassemblableCount(c, _rarities) * GameSave.DisassemblyGold(c, _rarities));

            var panel = OpenSlab("gold-disassemble", 1720);
            panel.style.height = 960;
            panel.style.paddingLeft = panel.style.paddingRight = 24;
            panel.style.paddingTop = 20;
            panel.style.paddingBottom = 18;
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.style.flexShrink = 0;
            var titles = UiStyle.Box();
            titles.Add(WindowTitle("컬렉션 분해"));
            titles.Add(Text($"카드마다 {RunRules.DisassemblyKeep}장은 남기고, 넘는 분만 골드로 바꾼다. 분해한 카드는 돌아오지 않는다.", 19, UiStyle.TextDim, top: 4));
            head.Add(titles);
            head.Add(GoldCounter("disassemble-gold"));
            panel.Add(head);

            var body = Row();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.marginTop = 14;
            var left = UiStyle.Box();
            left.style.width = PickColumns * (PickCardWidth + 12) + 28; // 세로 스크롤 막대 자리까지
            left.style.flexShrink = 0;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            var grid = Grid(scroll.contentContainer);
            left.Add(scroll);
            var rates = string.Join(" · ", Enumerable.Range(0, RarityTable.RarityCount).Select(i => $"{(Rarity)i} {RunRules.DisassemblyGoldFor((Rarity)i)}"));
            var legend = Text("1장에 받는 골드   " + rates, 19, UiStyle.TextDim, top: 8);
            legend.name = "disassemble-rates";
            legend.style.flexShrink = 0;
            left.Add(legend);
            var notice = Text(_disassembleNotice ?? "", 19, UiStyle.Good, top: 4);
            notice.name = "disassemble-notice";
            notice.style.flexShrink = 0;
            notice.style.minHeight = 26;
            left.Add(notice);
            body.Add(left);
            var detail = UiStyle.Box("disassemble-detail");
            detail.style.width = PickDetailWidth;
            detail.style.marginLeft = StyleKeyword.Auto;
            detail.style.flexShrink = 0;
            body.Add(detail);
            panel.Add(body);

            var all = UiStyle.StoneButton(totalCards > 0 ? $"전부 분해 · {totalCards}장 · 골드 +{totalGold}" : "전부 분해", OpenDisassembleAllConfirm, small: true);
            all.name = "disassemble-all";
            all.SetEnabled(totalCards > 0);
            panel.Add(WindowFooter(Hints(("← ↑ ↓ →", "W A S D", "카드 고르기"), ("Z", "J", "1장 분해"), ("X", "K", "닫기")),
                all, KeyButton("gold-disassemble-close", "닫기", CloseDisassemble, "X", "K", 112)));
            ApplyKeyCaps();

            // 카드 고르기 창의 키보드(PickKey)를 그대로 쓴다
            _detail = detail;
            _renderDetail = RenderDisassembleDetail;
            _pickSlots.Clear();
            _cardPickGrid = grid;
            const string pickName = "gold-disassemble";
            if (_pickName != pickName)
                _pickCursor = -1;
            _pickName = pickName;
            if (codes.Count == 0)
                grid.Add(Text($"{RunRules.DisassemblyKeep}장을 넘게 가진 카드가 없다.", 19, UiStyle.TextDim));
            foreach (uint candidate in codes)
            {
                uint code = candidate;
                grid.Add(PickSlot(Pile.Disassemble, code, () => DisassembleCards(code, 1), count: _collection.Count(code),
                    tag: $"넘는 분 {_save.DisassemblableCount(code, _rarities)}"));
            }
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        void CloseDisassemble()
        {
            _disassembleNotice = null;
            _selectedCode = 0;
            OpenGoldShop();
        }

        void RenderDisassembleDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            int excess = code == 0 || pile != Pile.Disassemble ? 0 : _save.DisassemblableCount(code, _rarities);
            if (excess <= 0)
            {
                AddPickEmpty(panel, _pickSlots.Count > 0 ? "왼쪽에서 카드를 고른다. 결정 키나 두 번 누르기로 1장씩 분해한다."
                    : $"{RunRules.DisassemblyKeep}장을 넘게 가진 카드가 없다.");
                return;
            }
            panel.Add(_faces.BuildDetail(code, PickDetailWidth, DisassembleDetailHeight));
            int each = GameSave.DisassemblyGold(code, _rarities);
            string grade = _rarities.TryGet(code, out var rarity) ? rarity.ToString() : "?";
            var info = Text($"컬렉션 {_collection.Count(code)}장 · 넘는 분 {excess}장 · {grade} 1장에 골드 {each}", 17, UiStyle.TextDim, top: 8);
            info.name = "disassemble-info";
            info.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(info);
            if (!pinned)
                return;
            var one = UiStyle.StoneButton($"1장 분해 · 골드 +{each}", () => DisassembleCards(code, 1), gold: true);
            one.name = "disassemble-one";
            one.style.marginTop = 10;
            one.style.fontSize = 23;
            one.style.paddingLeft = one.style.paddingRight = 8;
            panel.Add(one);
            var rest = UiStyle.StoneButton($"이 카드의 넘는 분 모두 · 골드 +{each * excess}", () => DisassembleCards(code, excess), small: true);
            rest.name = "disassemble-card-all";
            rest.style.marginTop = 8;
            rest.style.fontSize = 17;
            rest.style.paddingLeft = rest.style.paddingRight = 4;
            rest.SetEnabled(excess > 1);
            panel.Add(rest);
        }

        /// <summary>카드 count장을 분해한다 (다시 묻지 않는다). 넘는 분이 없어진 카드는 격자에서 빠지고, 같은 자리의 다음 카드를 가리킨다.</summary>
        void DisassembleCards(uint code, int count)
        {
            var before = DisassembleCodes();
            int index = before.IndexOf(code);
            int gold = _save.Disassemble(code, count, _rarities);
            if (gold <= 0)
                return;
            Persist();
            Sound.Play(Sfx.Destroy);
            _disassembleNotice = $"「{_text.CardName(code)}」 {count}장을 분해했다. 골드 +{gold}";
            Debug.Log($"[골드 상점] 분해: {_text.CardName(code)} {count}장 → 골드 +{gold} (컬렉션 {_collection.Count(code)}장, 골드 {_save.Gold})");
            var after = DisassembleCodes();
            _selectedPile = Pile.Disassemble;
            if (!after.Contains(code))
            {
                int next = Mathf.Min(index, after.Count - 1);
                _selectedCode = next >= 0 ? after[next] : 0;
                if (_pickCursor >= 0)
                    _pickCursor = Mathf.Max(next, 0);
            }
            else
                _selectedCode = code;
            OpenDisassemble();
            ShowGoldGain(gold);
        }

        /// <summary>
        /// 전부 분해 확인 창. 런 포기 창처럼 처음에는 "그만둔다"가 골라져 있어서, 결정 키를 두 번 눌러도 분해되지 않는다.
        /// </summary>
        void OpenDisassembleAllConfirm()
        {
            var list = _save.Disassemblable(_rarities);
            int cards = list.Sum(x => x.Count), gold = list.Sum(x => x.Count * x.GoldEach);
            if (cards == 0)
                return;
            var byRarity = new int[RarityTable.RarityCount];
            foreach (var (code, count, _) in list)
                if (_rarities.TryGet(code, out var rarity))
                    byRarity[(int)rarity] += count;
            string breakdown = string.Join(" · ", Enumerable.Range(0, byRarity.Length).Reverse().Where(i => byRarity[i] > 0).Select(i => $"{(Rarity)i} {byRarity[i]}장"));

            var panel = OpenSlab("gold-disassemble-confirm", 880);
            panel.style.alignItems = Align.Center;
            panel.Add(WindowTitle("전부 분해"));
            var what = Text($"{RunRules.DisassemblyKeep}장을 넘는 카드 {cards}장을 모두 골드로 바꾼다.", 26, top: 18);
            what.name = "disassemble-all-text";
            what.style.whiteSpace = WhiteSpace.Normal;
            what.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(what);
            panel.Add(Text(breakdown, 19, UiStyle.TextDim, top: 6));
            var warn = Text("되돌릴 수 없다. 분해한 카드는 돌아오지 않는다.", 19, UiStyle.Opponent, top: 8);
            warn.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(warn);
            var reward = Text($"받을 골드 +{gold}", 24, UiStyle.Good, bold: true, top: 12);
            reward.name = "disassemble-all-gold";
            panel.Add(reward);

            var buttons = new ButtonRow();
            buttons.Root.style.marginTop = 22;
            var confirm = buttons.Add("disassemble-all-confirm", "분해한다", DisassembleAll, width: 300);
            confirm.style.color = UiStyle.Bad;
            buttons.Add("disassemble-all-cancel", "그만둔다", OpenDisassemble, gold: true, width: 300);
            buttons.Select(1);
            _windowRow = buttons;
            panel.Add(buttons.Root);
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "그만둔다"));
            hints.name = "modal-hints";
            hints.style.marginTop = 16;
            panel.Add(hints);
            _windowCancel = OpenDisassemble;
            ApplyKeyCaps();
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
            _selectedCode = 0;
            _pickCursor = -1;
            OpenGoldShop();
            if (cards > 0)
                ShowGoldBanner($"카드 {cards}장을 분해했다. 골드 +{gold}");
        }
    }
}
