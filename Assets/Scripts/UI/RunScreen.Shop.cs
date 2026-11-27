// 런 화면: 상점 장면의 공통 부품과 칩교환소. docs/UI.md "상점". RunScreen.cs의 머리말 참고.
// 상점은 탑을 어둡게 덮은 돌판 창이 아니라 그 자리의 장면이다 (바탕 그림은 ShopScene.cs). 계산대 위에 물건을 놓고,
// 설명·값·살 수 없는 까닭·가진 재화는 화면 아래에 붙은 판 하나에 모은다. 골드 상점(RunScreen.GoldShop.cs)도 같은 부품을 쓴다.
//
// 칩교환소 (chip-exchange): 허브의 무대 위에 덱 창처럼 얹는다 (위 막대는 그대로 보인다). 칩교환소 층에 올라서면 한 번 저절로 펴지고,
// 나간 뒤에는 메뉴의 "칩교환소"로 다시 편다. 규칙과 값은 RunState·ChipExchange·RunRules가 정하고 화면은 보여 주기만 한다.
// - "카드 먼저": 계산대에 봉인된 카드(card-shoprelease-코드)와 아이템 카드(shop-item)가 놓인다. 하나를 가리키면 아래 판에
//   그 카드에 쓸 수 있는 방법이 값과 함께 나란히 나온다 (shop-buy-상품, 값 shop-price-상품, 못 쓰는 까닭 shop-reason-상품).
//   봉인된 카드: 봉인 대상 교체 네 가지(같은 레어도·레어도 상관없음 × 무작위·직접 고름)와 봉인 해제. 아이템 카드: 산다.
// - 방법을 누르거나 결정 키를 누르면 바로 산다 (다시 묻지 않는다). 직접 고르는 교체만 대신 봉인할 카드를 고르는 창(shop-pick)으로 넘어간다.
// - 산 결과(풀린 카드, 대신 봉인된 카드, 낸 칩)는 아래 판에 카드와 함께 남는다 (shop-result). 다음 조작에 사라진다.
// - 키보드: 세 줄을 위아래로 옮겨 다닌다. 계산대(좌우로 카드) → 방법(좌우로 방법, 결정 키로 산다) → 아래 버튼(덱 보기, N층으로 올라간다).
//   결정 키는 계산대에서 방법 줄로 내려간다. 취소 키는 한 줄 위로, 계산대에서는 나간다(shop-close).
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
        // ================= 상점 장면의 공통 부품 =================

        const float CounterCardWidth = 162, CounterMark = 34, CounterRaise = 16, CounterGap = 22;
        static readonly Color SignWood = UiStyle.Hex(0x3a2a18), SignEdge = UiStyle.Hex(0x1a120a), SignTop = UiStyle.Hex(0x6b4f2e), SignInk = UiStyle.Hex(0xf1e3c4);
        static readonly Color PlaqueTop = UiStyle.Hex(0x4a4136), PlaqueOn = UiStyle.Hex(0x3b3220);

        static float CardHeight(float width) => width * 158f / 108f;

        static PictogramView Picto(string name, float size, Color color)
        {
            var view = new PictogramView(Pictograms.Get(name), color);
            view.style.width = view.style.height = size;
            view.style.flexShrink = 0;
            return view;
        }

        /// <summary>가게 이름 판: 작은 돌판에 "[ 이름. ]". 칩교환소에서는 장면이 그 위에 사슬을 그린다.</summary>
        static VisualElement ShopNameBoard(string text, float left, float top, float width)
        {
            var board = new VisualElement { name = "shop-name", pickingMode = PickingMode.Ignore };
            UiStyle.Slab(board);
            UiStyle.Place(board, left, top, width, 66);
            board.style.alignItems = Align.Center;
            board.style.justifyContent = Justify.Center;
            board.Add(WindowTitle(text, 34));
            return board;
        }

        /// <summary>계산대 위의 작은 나무 팻말 (그 칸에 무엇이 놓였는지). pictogram은 글 앞의 작은 그림 (없으면 null).</summary>
        static VisualElement ShopSign(string name, string pictogram, string text, string small = null)
        {
            var sign = Row();
            sign.name = name;
            sign.pickingMode = PickingMode.Ignore;
            sign.style.position = Position.Absolute;
            sign.style.height = 34;
            sign.style.alignItems = Align.Center;
            sign.style.paddingLeft = sign.style.paddingRight = 12;
            sign.style.backgroundColor = SignWood;
            UiStyle.BorderColor(sign, SignEdge, 2);
            sign.style.borderTopColor = SignTop;
            UiStyle.Radius(sign, 4);
            if (pictogram != null)
            {
                var icon = Picto(pictogram, 18, SignInk);
                icon.style.marginRight = 8;
                sign.Add(icon);
            }
            var label = UiStyle.Label(text, 18, SignInk, bold: true);
            label.name = name + "-text";
            label.style.whiteSpace = WhiteSpace.NoWrap;
            sign.Add(label);
            if (small != null)
            {
                var note = UiStyle.Label(small, 15, UiStyle.Hex(0xc9b890));
                note.name = name + "-note";
                note.style.whiteSpace = WhiteSpace.NoWrap;
                note.style.marginLeft = 10;
                sign.Add(note);
            }
            return sign;
        }

        /// <summary>주인의 말풍선: 양피지 색 상자와 왼쪽 꼬리. 지금 무엇을 고르는 중인지 한 줄로 말한다.</summary>
        static VisualElement ShopBubble(string text, float left, float top)
        {
            var holder = new VisualElement { name = "shop-bubble", pickingMode = PickingMode.Ignore };
            UiStyle.Place(holder, left, top);
            var tail = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyle.Place(tail, -9, 20, 22, 22);
            tail.style.backgroundColor = UiStyle.Paper;
            UiStyle.BorderColor(tail, UiStyle.Stone9, 3);
            tail.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            holder.Add(tail);
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.backgroundColor = UiStyle.Paper;
            UiStyle.BorderColor(box, UiStyle.Stone9, 3);
            UiStyle.Radius(box, 12);
            box.style.paddingLeft = box.style.paddingRight = 18;
            box.style.paddingTop = box.style.paddingBottom = 9;
            var label = UiStyle.Label(text, 22, UiStyle.PaperInk, bold: true);
            label.name = "shop-bubble-text";
            label.style.whiteSpace = WhiteSpace.NoWrap;
            box.Add(label);
            holder.Add(box);
            // 꼬리의 상자 쪽 반을 가린다 (테두리가 상자 안으로 들어오지 않게)
            var cover = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyle.Place(cover, 3, 14, 14, 34);
            cover.style.backgroundColor = UiStyle.Paper;
            holder.Add(cover);
            return holder;
        }

        /// <summary>
        /// 화면 아래에 붙는 판: 왼쪽에 가진 재화, 가운데에 가리킨 물건의 설명, 아래 줄에 키 도움말과 버튼.
        /// body에 재화 칸·세로 선·가운데 칸을, foot에 도움말과 버튼을 넣는다.
        /// </summary>
        static VisualElement ShopSlab(string name, float top, float height, out VisualElement body, out VisualElement foot)
        {
            var slab = new VisualElement { name = name };
            UiStyle.Slab(slab);
            UiStyle.Place(slab, 24, top, StageWidth - 48, height);
            slab.style.paddingLeft = slab.style.paddingRight = 30;
            slab.style.paddingTop = 20;
            slab.style.paddingBottom = 14;
            body = Row();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            slab.Add(body);
            foot = Row();
            foot.style.alignItems = Align.Center;
            foot.style.flexShrink = 0;
            foot.style.height = 56;
            foot.style.marginTop = 6;
            slab.Add(foot);
            return slab;
        }

        static VisualElement ShopDivider()
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.width = 2;
            line.style.flexShrink = 0;
            line.style.marginLeft = line.style.marginRight = 26;
            line.style.backgroundColor = UiStyle.Stone6;
            return line;
        }

        /// <summary>
        /// 가진 재화 칸: 이름(17), 그림과 수(54 Black, 금), 그 아래 "사면 N" 줄(afterName. 글은 부른 쪽이 채운다), 작은 안내 두 줄.
        /// </summary>
        static VisualElement CurrencyBox(string boxName, string label, string pictogram, Color iconColor, int value, string valueName, string afterName, string note)
        {
            var box = UiStyle.Box(boxName);
            box.pickingMode = PickingMode.Ignore;
            box.style.width = 262;
            box.style.flexShrink = 0;
            box.Add(Text(label, 17, UiStyle.TextDim));
            var line = Row();
            line.style.alignItems = Align.Center;
            line.style.marginTop = -4;
            var icon = Picto(pictogram, 44, iconColor);
            icon.style.marginRight = 10;
            line.Add(icon);
            var number = UiStyle.Label(value.ToString(), 54, UiStyle.Gold);
            UiStyle.Black(number);
            number.name = valueName;
            number.style.whiteSpace = WhiteSpace.NoWrap;
            line.Add(number);
            box.Add(line);
            var after = Text("", 19, UiStyle.TextDim, top: -2);
            after.name = afterName;
            after.style.whiteSpace = WhiteSpace.NoWrap;
            after.style.height = 26;
            box.Add(after);
            var small = Text(note, 15, UiStyle.TextDim, top: 8);
            small.style.whiteSpace = WhiteSpace.Normal;
            box.Add(small);
            return box;
        }

        /// <summary>가격표: 어두운 딱지에 재화 그림과 값 (26 Black). pictogram이 null이면 글만.</summary>
        static VisualElement PriceTag(string name, string pictogram, Color iconColor, string text, Color color, int size = 26)
        {
            var tag = Row();
            tag.pickingMode = PickingMode.Ignore;
            tag.style.height = 42;
            tag.style.alignItems = Align.Center;
            tag.style.alignSelf = Align.Center;
            tag.style.flexShrink = 0;
            tag.style.paddingLeft = pictogram != null ? 9 : 13;
            tag.style.paddingRight = 13;
            tag.style.backgroundColor = UiStyle.Stone9;
            UiStyle.BorderColor(tag, UiStyle.Stone5, 2);
            UiStyle.Radius(tag, 7);
            if (pictogram != null)
            {
                var icon = Picto(pictogram, size - 2, iconColor);
                icon.style.marginRight = 7;
                tag.Add(icon);
            }
            var label = UiStyle.Label(text, size, color);
            UiStyle.Black(label);
            label.name = name;
            label.pickingMode = PickingMode.Ignore;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            tag.Add(label);
            return tag;
        }

        /// <summary>그림 안의 모든 요소가 누르기를 받지 않게 한다 (누르기는 그것을 담은 버튼이 받는다).</summary>
        static T Inert<T>(T element) where T : VisualElement
        {
            element.pickingMode = PickingMode.Ignore;
            element.Query().ForEach(e => e.pickingMode = PickingMode.Ignore);
            return element;
        }

        /// <summary>테두리·바탕·여백이 없는 버튼 (안에 든 것이 모양을 정한다). 화면 시험이 이름으로 찾아 누를 수 있다.</summary>
        static Button BareButton(string name, Action click)
        {
            var button = new Button(UiStyle.Clicked(click)) { name = name, text = "", focusable = false };
            UiStyle.Margin(button, 0);
            UiStyle.Padding(button, 0);
            button.style.backgroundColor = Color.clear;
            UiStyle.BorderColor(button, Color.clear, 0);
            button.style.flexShrink = 0;
            return button;
        }

        /// <summary>
        /// 계산대에 놓인 카드 한 장 (누르면 가리킨다). 가리킨 카드(on)는 조금 올라오고 금색 테와 ▼가 붙는다.
        /// locked면 봉인 띠를 두른다. 칸의 높이는 ▼와 올라올 자리까지 잡는다 (가로로 넘기는 줄 안에서 잘리지 않게).
        /// </summary>
        Button CounterCard(string name, uint code, bool on, Action click, float width = CounterCardWidth, int count = 0, string tag = null,
            Color? tagColor = null, bool locked = false, bool dim = false)
        {
            float height = CardHeight(width);
            var slot = BareButton(name, click);
            slot.style.width = width + CounterGap;
            slot.style.height = CounterMark + CounterRaise + height + 14;
            float left = CounterGap / 2, top = CounterMark + (on ? 0 : CounterRaise);
            var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyle.Place(shadow, left + 10, CounterMark + CounterRaise + height - 7, width - 20, 14);
            shadow.style.backgroundColor = new Color(0, 0, 0, 0.3f);
            UiStyle.Radius(shadow, 7);
            slot.Add(shadow);
            var holder = new VisualElement { name = on ? "counter-on" : null, pickingMode = PickingMode.Ignore };
            UiStyle.Place(holder, left - 6, top - 6, width + 12, height + 12);
            UiStyle.Padding(holder, 3);
            UiStyle.BorderColor(holder, on ? UiStyle.Gold : Color.clear, 3);
            UiStyle.Radius(holder, 11);
            var face = Inert(_faces.Build(code, width, count, tag, tagColor: tagColor));
            if (dim)
                face.style.opacity = 0.4f;
            holder.Add(face);
            if (locked)
                holder.Add(SealBand(width));
            slot.Add(holder);
            if (on)
            {
                var mark = UiStyle.Label("▼", 24, UiStyle.Gold);
                mark.pickingMode = PickingMode.Ignore;
                mark.style.unityTextAlign = TextAnchor.MiddleCenter;
                UiStyle.Place(mark, 0, -4, width + CounterGap, CounterMark);
                slot.Add(mark);
            }
            slot.EnableInClassList("selected", on);
            return slot;
        }

        /// <summary>봉인된 카드에 두르는 띠: 어두운 띠에 자물쇠와 "봉인". 조금 기울인다.</summary>
        static VisualElement SealBand(float width)
        {
            float s = width / 108f;
            var band = Row();
            band.name = "seal-band";
            band.pickingMode = PickingMode.Ignore;
            UiStyle.Place(band, 1, 6 + 62 * s, width + 4, 30 * s);
            band.style.alignItems = Align.Center;
            band.style.justifyContent = Justify.Center;
            band.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.92f);
            band.style.borderTopWidth = band.style.borderBottomWidth = 2;
            band.style.borderTopColor = band.style.borderBottomColor = UiStyle.Bad;
            band.style.rotate = new Rotate(new Angle(-7f, AngleUnit.Degree));
            var icon = Picto("상점:봉인", 15 * s, UiStyle.Opponent);
            icon.style.marginRight = 5 * s;
            band.Add(icon);
            var label = UiStyle.Label("봉인", Mathf.RoundToInt(15 * s), UiStyle.Opponent);
            UiStyle.Black(label);
            label.pickingMode = PickingMode.Ignore;
            band.Add(label);
            return band;
        }

        /// <summary>계산대의 카드를 가로로 늘어놓는 줄. 넘치면 가리킨 카드를 따라 가로로 넘어간다 (스크롤 막대는 없다).</summary>
        static ScrollView CounterRow(string name, float left, float top, float width, float cardWidth = CounterCardWidth)
        {
            var row = new ScrollView(ScrollViewMode.Horizontal) { name = name };
            row.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            row.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            UiStyle.Place(row, left, top, width, CounterMark + CounterRaise + CardHeight(cardWidth) + 14);
            return row;
        }

        /// <summary>줄이 자리 잡은 뒤 가리킨 카드가 보이게 넘긴다.</summary>
        static void ScrollCounterTo(ScrollView row, VisualElement slot)
        {
            if (slot == null)
                return;
            void Go(GeometryChangedEvent _) => row.ScrollTo(slot);
            row.contentContainer.RegisterCallback<GeometryChangedEvent>(Go);
            row.RegisterCallback<GeometryChangedEvent>(Go);
        }

        /// <summary>아래 줄의 버튼을 감싸는 테: 키보드로 가리키면 금색이 된다 (SetFocusFrame).</summary>
        static VisualElement FocusFrame(VisualElement button)
        {
            var frame = new VisualElement();
            frame.style.flexShrink = 0;
            frame.style.marginLeft = 6;
            UiStyle.Padding(frame, 3);
            UiStyle.BorderColor(frame, Color.clear, 3);
            UiStyle.Radius(frame, 13);
            frame.Add(button);
            return frame;
        }

        static void SetFocusFrame(VisualElement frame, bool on)
        {
            UiStyle.BorderColor(frame, on ? UiStyle.Gold : Color.clear, 3);
            frame.EnableInClassList("selected", on);
        }

        /// <summary>방법·상품 패의 모양 (고른 패는 금색 테와 밝은 바탕).</summary>
        static void SetPlaque(VisualElement plaque, bool on)
        {
            plaque.style.backgroundColor = on ? PlaqueOn : UiStyle.Stone7;
            UiStyle.BorderColor(plaque, on ? UiStyle.Gold : UiStyle.Stone9, 2);
            if (!on)
                plaque.style.borderTopColor = PlaqueTop;
            plaque.EnableInClassList("selected", on);
        }

        /// <summary>키 도움말 칸을 비운다 (그 안의 키 표시도 목록에서 뺀다). 가리키는 줄이 바뀔 때마다 도움말을 새로 넣는다.</summary>
        void ClearHints(VisualElement holder)
        {
            var old = holder.Query<Label>().ToList();
            _keyCaps.RemoveAll(c => old.Contains(c.Cap));
            holder.Clear();
        }

        /// <summary>재화 칸의 수 옆에 "−30", "+4" 같은 글이 떠올랐다 사라진다 (사거나 분해한 직후).</summary>
        void ShopFloat(string boxName, string text, Color color)
        {
            var box = _root.Q(boxName);
            if (box == null || !isActiveAndEnabled)
                return;
            var label = UiStyle.Label(text, 34, color);
            UiStyle.Black(label);
            label.name = "shop-float";
            label.pickingMode = PickingMode.Ignore;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.position = Position.Absolute;
            label.style.left = 150;
            label.style.top = 8;
            box.Add(label);
            StartCoroutine(ShopFloatRoutine(label));
        }

        static IEnumerator ShopFloatRoutine(VisualElement label)
        {
            yield return Tween(0.9f, t =>
            {
                label.style.opacity = t < 0.25f ? t * 4f : 1f - (t - 0.25f) / 0.75f;
                label.style.translate = new Translate(0, 16f - 40f * t);
            });
            label.RemoveFromHierarchy();
        }

        /// <summary>띠 알림의 띠: 무대를 가로지르는 어두운 띠와 큰 글 (이름은 name, 글은 name-text).</summary>
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

        /// <summary>띠 알림: 0.16초에 펴지고 1.15초 머문 뒤 사라진다 (docs/UI.md "부품 > 띠 알림"). 끝나면 layer를 떼어 낸다.</summary>
        static IEnumerator GoldBannerRoutine(VisualElement layer, VisualElement banner)
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

        // ================= 칩교환소 =================

        enum ShopRow
        {
            /// <summary>계산대의 카드 (봉인된 카드, 아이템 카드)</summary>
            Counter,
            /// <summary>가리킨 카드에 쓸 방법 (값이 붙은 패)</summary>
            Methods,
            /// <summary>아래 줄의 버튼 (덱 보기, 올라간다)</summary>
            Buttons,
        }

        /// <summary>방금 산 것 (아래 판에 결과로 보인다).</summary>
        sealed class ShopResult
        {
            public ShopProduct Product;
            /// <summary>봉인이 풀린 카드, 대신 봉인된 카드 (없으면 0), 산 아이템 카드 (아니면 0).</summary>
            public uint Freed, NewSeal, Item;
            public int Price, ChipsBefore;
        }

        /// <summary>칩교환소 장면이 펴져 있는지 (칩교환소 층에 서 있을 때만).</summary>
        bool _shopOpen;
        VisualElement _shopLayer;
        ShopRow _shopRow;
        /// <summary>계산대에서 가리키는 것: 봉인된 카드의 자리, 그다음이 아이템 카드.</summary>
        int _shopCard;
        int _shopMethod;
        /// <summary>아래 버튼 줄에서 가리키는 것 (0 = 덱 보기, 1 = 올라간다).</summary>
        int _shopButton = 1;
        ShopResult _shopResult;
        /// <summary>직접 고르는 교체를 사는 중: 그 상품과 풀기로 한 카드 (대신 봉인할 카드를 고르는 창이 떠 있다).</summary>
        ShopProduct? _shopProduct;
        uint _shopRelease;
        /// <summary>칩교환소 장면을 저절로 편 층.</summary>
        int _shopShownFloor;

        // 장면을 그릴 때마다 다시 채운다 (가리키는 것만 바뀌면 이것들의 모양만 고친다: MarkShopFocus)
        readonly List<(Button Plaque, ShopProduct Product, int Price, bool Enabled)> _shopPlaques = new List<(Button, ShopProduct, int, bool)>();
        readonly List<(VisualElement Frame, Button Button)> _shopNavButtons = new List<(VisualElement, Button)>();
        Label _shopAfter;
        VisualElement _shopHints;

        /// <summary>칩교환소 장면을 그릴 수 있는지: 덱 창을 보거나 올라가는 중이 아닐 때.</summary>
        bool ShopShown => _shopOpen && !_deckOpen && !_climbing && _run != null && _run.FloorKind == FloorKind.ChipExchange;

        /// <summary>메뉴의 "칩교환소": 장면을 편다.</summary>
        void OpenShop()
        {
            if (_run == null || _run.Phase != RunPhase.Ready || _run.FloorKind != FloorKind.ChipExchange)
                return;
            BeginShop();
            ShowHub();
        }

        /// <summary>칩교환소 장면을 처음 상태로 편다 (첫 카드를 가리킨다). 그리는 것은 ShowHub가 한다.</summary>
        void BeginShop()
        {
            if (!_shopOpen)
                Sound.Play(Sfx.WindowOpen);
            _shopShownFloor = _run.Floor;
            _shopOpen = true;
            _shopRow = ShopRow.Counter;
            _shopCard = 0;
            _shopMethod = 0;
            _shopButton = 1;
            _shopResult = null;
            _shopProduct = null;
            _shopRelease = 0;
        }

        /// <summary>칩교환소에서 나간다 (탑으로). 메뉴의 "칩교환소"로 다시 들어온다.</summary>
        void CloseShop()
        {
            ForgetShop();
            if (_run != null)
                ShowHub();
        }

        void ForgetShop()
        {
            _shopOpen = false;
            _shopResult = null;
            _shopProduct = null;
            _shopRelease = 0;
        }

        /// <summary>계산대의 봉인된 카드 (같은 카드는 한 번, 화면 순서).</summary>
        List<uint> ShopSealedCards() => Sorted(_run.SealedCards()).ToList();

        ShopOffer ShopOfferOf(ShopProduct product) => _run.Shop.FirstOrDefault(o => o.Product == product);

        static readonly ShopProduct[] ShopMethods =
        {
            ShopProduct.SwapSameRarity, ShopProduct.SwapSameRarityChosen, ShopProduct.SwapAny, ShopProduct.SwapAnyChosen, ShopProduct.Unseal,
        };

        /// <summary>"봉인 대상 교체 · 같은 레어도 · 무작위", "봉인 해제", "아이템 카드" (결과 줄과 고르기 창에 쓴다).</summary>
        static string ShopProductText(ShopProduct product) =>
            product == ShopProduct.Unseal ? "봉인 해제"
            : product == ShopProduct.ItemCard ? "아이템 카드"
            : $"봉인 대상 교체 · {(ChipExchange.SameRarity(product) ? "같은 레어도" : "레어도 상관없음")} · {(ChipExchange.ChoosesNewSeal(product) ? "직접 고름" : "무작위")}";

        /// <summary>
        /// 이 방법을 code(봉인된 카드)에 쓸 수 없는 까닭 (쓸 수 있으면 null). 화면의 말투로 짧게 쓴다.
        /// 판정은 규칙(RunState.CanBuy, ReleaseCandidates)이 한다.
        /// </summary>
        string ShopMethodProblem(ShopOffer offer, uint code)
        {
            if (offer.Sold)
                return "샀음";
            if (_run.Phase != RunPhase.Ready)
                return "지금은 살 수 없다";
            if (_run.Chips < offer.Price)
                return $"칩 {offer.Price - _run.Chips} 모자람";
            var product = offer.Product;
            if (product == ShopProduct.Unseal || _run.ReleaseCandidates(product).Contains(code))
                return _run.CanBuy(product, out string reason) ? null : reason;
            string deck = IsExtraDeckCard(code) ? "엑스트라 덱" : "메인 덱";
            if (_run.NewSealCandidates(product, code).Count == 0)
                return ChipExchange.SameRarity(product) ? $"{deck}에 같은 레어도 카드가 없다" : $"{deck}에 대신 봉인할 카드가 없다";
            return $"교체하면 메인 덱 {RunRules.MinMainDeck}장을 채울 수 없다";
        }

        /// <summary>아이템 카드를 살 수 없는 까닭 (살 수 있으면 null).</summary>
        string ShopItemProblem(ShopOffer offer)
        {
            if (offer.Sold)
                return "샀음";
            if (_run.Phase != RunPhase.Ready)
                return "지금은 살 수 없다";
            if (_run.Chips < offer.Price)
                return $"칩 {offer.Price - _run.Chips} 모자람";
            if (_run.ShopDeck.Count >= RunRules.MaxShopDeck)
                return "상점 덱이 꽉 찼다";
            if (!_run.ShopDeckHasRoom(offer.Card, out _))
                return $"상점 덱에 같은 카드가 이미 {RunRules.MaxCopies}장 있다";
            return _run.CanBuy(ShopProduct.ItemCard, out string reason) ? null : reason;
        }

        /// <summary>
        /// 칩교환소 장면 (허브의 무대에 얹는 층). 위 막대 자리는 비워 둔다. 계산대에 봉인된 카드와 아이템 카드, 아래 판에 가진 칩과 방법들.
        /// </summary>
        VisualElement ShopLayer()
        {
            _shopPlaques.Clear();
            _shopNavButtons.Clear();
            var sealedCards = ShopSealedCards();
            var itemOffer = ShopOfferOf(ShopProduct.ItemCard);
            int count = sealedCards.Count + (itemOffer != null ? 1 : 0);
            _shopCard = Mathf.Clamp(_shopCard, 0, Mathf.Max(0, count - 1));
            bool onItem = itemOffer != null && _shopCard == sealedCards.Count;
            uint pointed = onItem || sealedCards.Count == 0 ? 0 : sealedCards[_shopCard];
            var result = _shopResult;

            var layer = new VisualElement { name = "chip-exchange" };
            UiStyle.Place(layer, 0, 0, StageWidth, StageHeight);
            _shopLayer = layer;
            layer.Add(new ShopScene(ShopScene.Kind.ChipExchange, _run.Floor) { DividerX = 1404, SignLeft = 550, SignRight = 790, SignTop = 118 });
            layer.Add(ShopNameBoard("칩교환소", 520, 118, 300));
            layer.Add(ShopBubble(result != null ? "또 오게." : onItem ? "좋은 물건이지." : pointed == 0 ? "맡긴 카드가 없군." : _shopRow == ShopRow.Counter ? "어느 카드를 풀어 줄까?" : "어떻게 풀어 줄까?",
                1050, 150));

            // ---- 계산대: 봉인된 카드 ----
            var sealedSign = ShopSign("shop-sealed-sign", "상점:봉인", $"봉인된 카드 {_run.SealedCount}장");
            UiStyle.Place(sealedSign, 44, 328);
            layer.Add(sealedSign);
            if (sealedCards.Count == 0)
            {
                var none = UiStyle.Label("봉인된 카드가 없다. 풀 것이 없으면 봉인을 다루는 일은 살 수 없다.", 22, UiStyle.Hex(0xb9e3d8));
                none.name = "shop-no-sealed";
                none.pickingMode = PickingMode.Ignore;
                UiStyle.Place(none, 60, 490);
                layer.Add(none);
            }
            else
            {
                var row = CounterRow("shop-sealed-row", 36, 366, 1352);
                VisualElement on = null;
                for (int i = 0; i < sealedCards.Count; i++)
                {
                    int index = i;
                    uint code = sealedCards[i];
                    bool isOn = !onItem && i == _shopCard;
                    int copies = _run.SealedMain.Count(c => c == code) + _run.SealedExtra.Count(c => c == code);
                    bool fresh = result != null && result.NewSeal == code;
                    var slot = CounterCard(SlotName(Pile.ShopRelease, code), code, isOn, () => PointShopCard(index), count: copies > 1 ? copies : 0,
                        tag: fresh ? "새로 봉인" : null, tagColor: UiStyle.Bad, locked: true);
                    row.Add(slot);
                    if (isOn)
                        on = slot;
                }
                layer.Add(row);
                ScrollCounterTo(row, on);
            }

            // ---- 계산대: 아이템 카드 ----
            if (itemOffer != null)
            {
                var itemSign = ShopSign("shop-item-sign", null, "아이템 카드", $"상점 덱 {_run.ShopDeck.Count} / {RunRules.MaxShopDeck}");
                UiStyle.Place(itemSign, 1432, 328);
                layer.Add(itemSign);
                int index = sealedCards.Count;
                var slot = CounterCard("shop-item", itemOffer.Card, onItem, () => PointShopCard(index), tag: itemOffer.Sold ? "샀음" : null,
                    tagColor: UiStyle.TextDim, dim: itemOffer.Sold);
                UiStyle.Place(slot, 1547, 366);
                layer.Add(slot);
                string problem = ShopItemProblem(itemOffer);
                var tag = itemOffer.Sold ? PriceTag("shop-item-tag", null, Color.clear, "샀음", UiStyle.Stone4, 19)
                    : PriceTag("shop-item-tag", "재화:칩", ShopFloorColor, itemOffer.Price.ToString(), problem == null ? UiStyle.Gold : UiStyle.Bad);
                UiStyle.Place(tag, 1740, 574);
                layer.Add(tag);
            }

            // ---- 아래 판 ----
            var slab = ShopSlab("shop-slab", 704, 352, out var body, out var foot);
            body.Add(CurrencyBox("shop-chips-box", "가진 칩", "재화:칩", ShopFloorColor, _run.Chips, "shop-chips", "shop-after",
                $"남긴 칩은 런이 끝날 때\n{RunRules.SettlementHeldChipsDivisor}개마다 골드 1이 된다."));
            _shopAfter = body.Q<Label>("shop-after");
            body.Add(ShopDivider());
            var center = UiStyle.Box("shop-center");
            center.style.flexGrow = 1;
            center.style.flexShrink = 1;
            if (result != null)
                AddShopResult(center, result);
            else if (onItem)
                AddShopItem(center, itemOffer);
            else if (pointed != 0)
                AddShopMethods(center, pointed);
            else
                center.Add(Text("봉인된 카드도 아이템 카드도 없다.", 24, UiStyle.TextDim));
            body.Add(center);

            _shopHints = Row();
            _shopHints.name = "shop-hints";
            _shopHints.style.alignItems = Align.Center;
            foot.Add(_shopHints);
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            foot.Add(gap);
            foot.Add(KeyButton("shop-close", "나간다", CloseShop, "X", "K", 132));
            var deck = UiStyle.StoneButton("덱 보기", OpenDeckFromShop, small: true);
            deck.name = "shop-deck";
            deck.style.width = 170;
            var deckFrame = FocusFrame(deck);
            deckFrame.style.marginLeft = 12;
            foot.Add(deckFrame);
            _shopNavButtons.Add((deckFrame, deck));
            var leave = UiStyle.StoneButton($"{_run.Floor + 1}층으로 올라간다", LeaveFloor, gold: true, small: true);
            leave.name = "shop-leave";
            leave.style.width = 270;
            var leaveFrame = FocusFrame(leave);
            foot.Add(leaveFrame);
            _shopNavButtons.Add((leaveFrame, leave));
            layer.Add(slab);

            // 가리키던 줄이 없어졌으면 (살 수 있는 방법이 없다) 계산대로 돌아간다
            if (_shopRow == ShopRow.Methods && !_shopPlaques.Any(p => p.Enabled))
                _shopRow = ShopRow.Counter;
            MarkShopFocus();
            return layer;
        }

        /// <summary>가운데 칸의 첫 줄: 카드명(30 Bold)과 꼬리표들, 오른쪽 끝에 작은 안내.</summary>
        static VisualElement ShopHeadLine(string title, string note, params (string Text, Color Color, bool Bold)[] tags)
        {
            var line = Row();
            line.style.alignItems = Align.FlexEnd;
            line.style.flexShrink = 0;
            var name = UiStyle.Label(title, 30, UiStyle.Text, bold: true);
            name.name = "shop-title";
            name.style.whiteSpace = WhiteSpace.NoWrap;
            line.Add(name);
            foreach (var (text, color, bold) in tags)
            {
                var tag = UiStyle.Label(text, 19, color, bold);
                tag.style.whiteSpace = WhiteSpace.NoWrap;
                tag.style.marginLeft = 14;
                tag.style.marginBottom = 4;
                line.Add(tag);
            }
            if (note != null)
            {
                var small = UiStyle.Label(note, 17, UiStyle.TextDim);
                small.style.whiteSpace = WhiteSpace.NoWrap;
                small.style.marginLeft = StyleKeyword.Auto;
                small.style.marginBottom = 5;
                line.Add(small);
            }
            return line;
        }

        /// <summary>방법 묶음의 머리 줄: 굵은 이름과 흐린 설명, 아래에 선. cells = 그 아래에 놓인 패의 수 (패 사이 12를 더해 폭을 맞춘다).</summary>
        static VisualElement ShopGroupHead(string title, string note, int cells)
        {
            var head = Row();
            head.style.flexGrow = cells;
            head.style.flexBasis = 12 * (cells - 1);
            head.style.overflow = Overflow.Hidden;
            head.style.alignItems = Align.FlexEnd;
            head.style.paddingBottom = 4;
            head.style.borderBottomWidth = 2;
            head.style.borderBottomColor = UiStyle.Stone5;
            var name = UiStyle.Label(title, 19, UiStyle.Text, bold: true);
            name.style.whiteSpace = WhiteSpace.NoWrap;
            head.Add(name);
            if (note != null)
            {
                var small = UiStyle.Label(note, 17, UiStyle.TextDim);
                small.style.whiteSpace = WhiteSpace.NoWrap;
                small.style.marginLeft = 10;
                head.Add(small);
            }
            return head;
        }

        /// <summary>
        /// 방법·상품 패 하나: 위에 작은 구분(17), 그림과 이름(24 Bold), 아래에 값(33 Black). 살 수 없으면 꺼지고 까닭을 오른쪽 아래에 내림 색으로 쓴다.
        /// 까닭은 패 밖(위에 겹친 글)에 둬서 꺼진 패와 함께 흐려지지 않게 한다.
        /// </summary>
        VisualElement ShopPlaque(ShopProduct product, string small, string pictogram, string title, int price, string problem, bool offered = true)
        {
            bool sold = problem == "샀음", enabled = offered && problem == null;
            var cell = new VisualElement();
            cell.style.flexGrow = 1;
            cell.style.flexBasis = 0;
            cell.style.height = 150;
            var plaque = new Button(UiStyle.Clicked(() => BuyInShop(product))) { name = $"shop-buy-{product}", text = "", focusable = false };
            UiStyle.Margin(plaque, 0);
            plaque.style.position = Position.Absolute;
            plaque.style.left = plaque.style.top = plaque.style.right = plaque.style.bottom = 0;
            plaque.style.paddingLeft = plaque.style.paddingRight = 14;
            plaque.style.paddingTop = 9;
            plaque.style.paddingBottom = 8;
            plaque.style.alignItems = Align.FlexStart;
            plaque.style.justifyContent = Justify.FlexStart;
            UiStyle.Radius(plaque, 8);
            SetPlaque(plaque, false);
            var top = UiStyle.Label(small, 17, UiStyle.TextDim);
            top.pickingMode = PickingMode.Ignore;
            top.style.whiteSpace = WhiteSpace.NoWrap;
            plaque.Add(top);
            var line = Row();
            line.pickingMode = PickingMode.Ignore;
            line.style.alignItems = Align.Center;
            if (pictogram != null)
            {
                var icon = Picto(pictogram, 22, UiStyle.Text);
                icon.style.marginRight = 7;
                line.Add(icon);
            }
            var name = UiStyle.Label(title, 24, UiStyle.Text, bold: true);
            name.pickingMode = PickingMode.Ignore;
            name.style.whiteSpace = WhiteSpace.NoWrap;
            line.Add(name);
            plaque.Add(line);
            var cost = Row();
            cost.pickingMode = PickingMode.Ignore;
            cost.style.alignItems = Align.Center;
            cost.style.marginTop = StyleKeyword.Auto;
            if (offered && !sold)
            {
                var icon = Picto("재화:칩", 28, ShopFloorColor);
                icon.style.marginRight = 8;
                cost.Add(icon);
            }
            bool poor = offered && !sold && _run.Chips < price;
            var number = UiStyle.Label(!offered ? "없음" : sold ? "샀음" : price.ToString(), offered && !sold ? 33 : 22, poor ? UiStyle.Bad : enabled ? UiStyle.Gold : UiStyle.Stone4);
            UiStyle.Black(number);
            number.name = $"shop-price-{product}";
            number.pickingMode = PickingMode.Ignore;
            cost.Add(number);
            plaque.Add(cost);
            plaque.SetEnabled(enabled);
            cell.Add(plaque);
            if (!enabled && !sold)
            {
                var why = UiStyle.Label(offered ? problem : "이번 칩교환소에는 없다", 15, offered ? UiStyle.Bad : UiStyle.TextDim);
                why.name = $"shop-reason-{product}";
                why.pickingMode = PickingMode.Ignore;
                why.style.position = Position.Absolute;
                why.style.right = 12;
                why.style.bottom = 12;
                why.style.maxWidth = 150;
                why.style.whiteSpace = WhiteSpace.Normal;
                why.style.unityTextAlign = TextAnchor.LowerRight;
                cell.Add(why);
            }
            int index = _shopPlaques.Count;
            plaque.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!plaque.enabledSelf || _shopResult != null)
                    return;
                _shopRow = ShopRow.Methods;
                _shopMethod = index;
                MarkShopFocus();
            });
            _shopPlaques.Add((plaque, product, price, enabled));
            return cell;
        }

        /// <summary>가운데 칸: 봉인된 카드를 가리킬 때. 카드명과 그 카드에 쓸 방법 다섯 가지.</summary>
        void AddShopMethods(VisualElement center, uint code)
        {
            var tags = new List<(string, Color, bool)>();
            if (_rarities.TryGet(code, out var rarity))
                tags.Add((rarity.ToString(), CardFaces.RarityColor(rarity), true));
            tags.Add((IsExtraDeckCard(code) ? "엑스트라 덱" : "메인 덱", UiStyle.TextDim, false));
            center.Add(ShopHeadLine($"「{_text.CardName(code)}」", "풀린 카드는 사이드 덱으로 간다", tags.ToArray()));

            var heads = Row();
            heads.style.marginTop = 8;
            heads.style.flexShrink = 0;
            heads.Add(ShopGroupHead("봉인 대상 교체", "같은 덱의 다른 카드 1장을 대신 봉인한다", 4));
            var gap = new VisualElement();
            gap.style.width = 12;
            heads.Add(gap);
            heads.Add(ShopGroupHead("봉인 해제", null, 1));
            center.Add(heads);

            var row = Row();
            row.name = "shop-methods";
            row.style.marginTop = 8;
            row.style.flexShrink = 0;
            foreach (var product in ShopMethods)
            {
                var offer = ShopOfferOf(product);
                bool unseal = product == ShopProduct.Unseal;
                string small = unseal ? "대신 봉인하지 않는다" : ChipExchange.SameRarity(product) ? "같은 레어도" : "레어도 상관없음";
                string picture = unseal ? "상점:봉인 해제" : ChipExchange.ChoosesNewSeal(product) ? "상점:직접" : "상점:무작위";
                string title = unseal ? "봉인 해제" : ChipExchange.ChoosesNewSeal(product) ? "직접 고름" : "무작위";
                var cell = offer != null ? ShopPlaque(product, small, picture, title, offer.Price, ShopMethodProblem(offer, code))
                    : ShopPlaque(product, small, picture, title, 0, null, offered: false);
                if (product != ShopMethods[0])
                    cell.style.marginLeft = 12;
                row.Add(cell);
            }
            center.Add(row);
        }

        /// <summary>가운데 칸: 아이템 카드를 가리킬 때. 탑 효과와 "산다" 패.</summary>
        void AddShopItem(VisualElement center, ShopOffer offer)
        {
            var item = ItemCards.All.FirstOrDefault(i => i.Code == offer.Card);
            center.Add(ShopHeadLine($"「{_text.CardName(offer.Card)}」", "산 카드는 상점 덱으로 간다", ("아이템 카드", ShopFloorColor, true)));
            var row = Row();
            row.style.marginTop = 12;
            row.style.flexShrink = 0;
            var cell = ShopPlaque(ShopProduct.ItemCard, "아이템 카드", null, "산다", offer.Price, ShopItemProblem(offer));
            cell.style.flexGrow = 0;
            cell.style.flexBasis = StyleKeyword.Auto;
            cell.style.width = 286;
            row.Add(cell);
            var texts = UiStyle.Box();
            texts.style.marginLeft = 26;
            texts.style.flexShrink = 1;
            if (item != null)
            {
                var effect = Text(item.EffectText, 26, UiStyle.Text, bold: true);
                effect.name = "shop-item-effect";
                texts.Add(effect);
                var where = new List<string>();
                if (item.InMain) where.Add("메인 덱");
                if (item.InShop) where.Add("상점 덱");
                texts.Add(Text($"효과 위치 {string.Join("·", where)} · {(item.Stacks ? "중첩됨" : "중첩 안 됨")}{(item.Bound ? " · 귀속" : "")}", 19, UiStyle.TextDim, top: 4));
                if (item.Bound)
                    texts.Add(Text("귀속: 메인 덱에서 적용하면 봉인될 때까지 덱에서 뺄 수 없다.", 17, UiStyle.TextDim, top: 2));
            }
            var apply = Text("산 뒤 덱 창의 상점 덱 탭에서 효과를 적용한다. 듀얼과 보충에는 쓰지 않고, 런이 끝나면 사라진다.", 17, UiStyle.TextDim, top: 10);
            apply.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(apply);
            row.Add(texts);
            center.Add(row);
        }

        /// <summary>가운데 칸: 방금 산 것의 결과. 풀린 카드와 대신 봉인된 카드를 작은 카드로 보이고, 낸 칩을 쓴다.</summary>
        void AddShopResult(VisualElement center, ShopResult result)
        {
            var row = Row();
            row.name = "shop-result";
            row.style.alignItems = Align.FlexStart;
            const float small = 108;
            VisualElement Card(uint code, string tag, Color color, bool locked)
            {
                var holder = new VisualElement { pickingMode = PickingMode.Ignore };
                holder.style.width = small + 12;
                holder.style.height = CardHeight(small) + 12;
                holder.style.flexShrink = 0;
                UiStyle.Padding(holder, 6);
                holder.Add(Inert(_faces.Build(code, small, tag: tag, tagColor: color)));
                if (locked)
                    holder.Add(SealBand(small));
                return holder;
            }
            var texts = UiStyle.Box();
            texts.style.marginLeft = 22;
            texts.style.flexShrink = 1;
            if (result.Item != 0)
            {
                row.Add(Card(result.Item, "상점 덱", UiStyle.Good, false));
                var title = Text($"「{_text.CardName(result.Item)}」 1장을 샀다.", 30, UiStyle.Text, bold: true);
                title.name = "shop-result-text";
                texts.Add(title);
                texts.Add(Text("상점 덱으로 갔다.", 24, UiStyle.Good, top: 4));
                var apply = Text("효과는 덱 창의 상점 덱 탭에서 적용한다.", 19, UiStyle.TextDim, top: 6);
                texts.Add(apply);
            }
            else
            {
                row.Add(Card(result.Freed, "사이드 덱", UiStyle.Good, false));
                if (result.NewSeal != 0)
                {
                    var arrow = UiStyle.Label("↔", 34, UiStyle.TextDim);
                    arrow.style.alignSelf = Align.Center;
                    arrow.style.marginLeft = arrow.style.marginRight = 6;
                    row.Add(arrow);
                    row.Add(Card(result.NewSeal, "새로 봉인", UiStyle.Bad, true));
                }
                var title = Text($"「{_text.CardName(result.Freed)}」의 봉인을 풀었다.", 30, UiStyle.Text, bold: true);
                title.name = "shop-result-text";
                texts.Add(title);
                texts.Add(Text("풀린 카드는 사이드 덱으로 갔다.", 24, UiStyle.Good, top: 4));
                if (result.NewSeal != 0)
                {
                    var sealedNow = Text($"대신 「{_text.CardName(result.NewSeal)}」 1장을 봉인했다.", 24, UiStyle.Opponent, top: 2);
                    sealedNow.name = "shop-result-seal";
                    texts.Add(sealedNow);
                }
            }
            var paid = Text($"{ShopProductText(result.Product)} · 칩 {result.Price} · 가진 칩 {result.ChipsBefore} → {_run.Chips}", 19, UiStyle.TextDim, top: 10);
            paid.name = "shop-result-paid";
            texts.Add(paid);
            row.Add(texts);
            center.Add(row);
        }

        /// <summary>가리키는 것이 바뀌었을 때: 패와 아래 버튼의 테, "사면 N", 키 도움말만 고친다 (장면을 다시 그리지 않는다).</summary>
        void MarkShopFocus()
        {
            for (int i = 0; i < _shopPlaques.Count; i++)
                SetPlaque(_shopPlaques[i].Plaque, _shopResult == null && _shopRow == ShopRow.Methods && i == _shopMethod);
            for (int i = 0; i < _shopNavButtons.Count; i++)
                SetFocusFrame(_shopNavButtons[i].Frame, _shopRow == ShopRow.Buttons && i == _shopButton);
            if (_shopAfter != null)
            {
                bool buying = _shopResult == null && _shopRow == ShopRow.Methods && _shopMethod >= 0 && _shopMethod < _shopPlaques.Count && _shopPlaques[_shopMethod].Enabled;
                _shopAfter.text = buying ? $"사면 {_run.Chips - _shopPlaques[_shopMethod].Price}" : "";
            }
            if (_shopHints == null)
                return;
            ClearHints(_shopHints);
            VisualElement hints;
            if (_shopResult != null)
                hints = Hints(("Z", "J", "계속"), ("X", "K", "나간다"));
            else if (_shopRow == ShopRow.Counter)
                hints = Hints(("←", "A", null), ("→", "D", "카드 고르기"), ("Z", "J", "방법 고르기로"), ("↓", "S", "아래 줄로"), ("X", "K", "나간다"));
            else if (_shopRow == ShopRow.Methods)
                hints = Hints(("←", "A", null), ("→", "D", "방법 고르기"), ("Z", "J", "산다"), ("↑", "W", null), ("↓", "S", "줄 옮기기"), ("X", "K", "뒤로"));
            else
                hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("↑", "W", "위 줄로"), ("X", "K", "뒤로"));
            _shopHints.Add(hints);
            ApplyKeyCaps();
        }

        /// <summary>칩교환소 장면만 다시 그린다 (허브의 다른 것은 그대로 둔다).</summary>
        void RefreshShop()
        {
            if (_shopLayer == null || _shopLayer.parent == null || _shopLayer.parent != _stage)
            {
                ShowHub();
                return;
            }
            int index = _stage.IndexOf(_shopLayer);
            _shopLayer.RemoveFromHierarchy();
            _stage.Insert(index, ShopLayer());
            _keyCaps.RemoveAll(c => c.Cap.panel == null);
            ApplyKeyCaps();
        }

        /// <summary>계산대의 카드를 가리킨다 (누르기). 결과를 보는 중이었으면 결과를 접는다.</summary>
        void PointShopCard(int index)
        {
            _shopResult = null;
            _shopCard = index;
            _shopRow = ShopRow.Counter;
            RefreshShop();
        }

        int FirstShopMethod(int from, int delta)
        {
            int n = _shopPlaques.Count;
            for (int step = 0; step < n; step++)
            {
                int i = ((from + delta * step) % n + n) % n;
                if (_shopPlaques[i].Enabled)
                    return i;
            }
            return -1;
        }

        /// <summary>칩교환소의 키보드. 머리말 참고.</summary>
        void ShopKey(Nav nav)
        {
            if (_shopResult != null)
            {
                // 결과를 보는 중: 취소 키는 나가고, 다른 키는 결과를 접는다
                if (nav == Nav.Cancel)
                {
                    CloseShop();
                    return;
                }
                _shopResult = null;
                _shopRow = ShopRow.Counter;
                RefreshShop();
                return;
            }
            int count = ShopSealedCards().Count + (ShopOfferOf(ShopProduct.ItemCard) != null ? 1 : 0);
            bool anyMethod = _shopPlaques.Any(p => p.Enabled);
            switch (_shopRow)
            {
                case ShopRow.Counter:
                    if ((nav == Nav.Left || nav == Nav.Right) && count > 0)
                    {
                        _shopCard = ((_shopCard + (nav == Nav.Left ? -1 : 1)) % count + count) % count;
                        RefreshShop();
                    }
                    else if (nav == Nav.Confirm || nav == Nav.Down)
                    {
                        if (anyMethod)
                        {
                            _shopRow = ShopRow.Methods;
                            _shopMethod = FirstShopMethod(Mathf.Clamp(_shopMethod, 0, _shopPlaques.Count - 1), 1);
                            RefreshShop(); // 주인의 말이 바뀐다
                        }
                        else if (nav == Nav.Down)
                        {
                            _shopRow = ShopRow.Buttons;
                            MarkShopFocus();
                        }
                    }
                    else if (nav == Nav.Cancel)
                        CloseShop();
                    break;
                case ShopRow.Methods:
                    if (nav == Nav.Left || nav == Nav.Right)
                    {
                        int next = FirstShopMethod(_shopMethod + (nav == Nav.Left ? -1 : 1), nav == Nav.Left ? -1 : 1);
                        if (next >= 0)
                            _shopMethod = next;
                        MarkShopFocus();
                    }
                    else if (nav == Nav.Up || nav == Nav.Cancel)
                    {
                        _shopRow = ShopRow.Counter;
                        RefreshShop();
                    }
                    else if (nav == Nav.Down)
                    {
                        _shopRow = ShopRow.Buttons;
                        MarkShopFocus();
                    }
                    else if (nav == Nav.Confirm && _shopMethod >= 0 && _shopMethod < _shopPlaques.Count)
                        Press(_shopPlaques[_shopMethod].Plaque);
                    break;
                case ShopRow.Buttons:
                    if (nav == Nav.Left || nav == Nav.Right)
                    {
                        _shopButton = (_shopButton + 1) % Mathf.Max(1, _shopNavButtons.Count);
                        MarkShopFocus();
                    }
                    else if (nav == Nav.Up || nav == Nav.Cancel)
                    {
                        _shopRow = anyMethod && nav == Nav.Up ? ShopRow.Methods : ShopRow.Counter;
                        if (_shopRow == ShopRow.Methods)
                            _shopMethod = FirstShopMethod(Mathf.Clamp(_shopMethod, 0, _shopPlaques.Count - 1), 1);
                        RefreshShop();
                    }
                    else if (nav == Nav.Confirm && _shopButton < _shopNavButtons.Count)
                        Press(_shopNavButtons[_shopButton].Button);
                    break;
            }
        }

        /// <summary>"덱 보기": 덱 창을 연다. 닫으면 칩교환소로 돌아온다 (ReopenFloorWindow).</summary>
        void OpenDeckFromShop()
        {
            _shopResult = null;
            OpenDeck(_deckTab, fromDuel: true);
        }

        /// <summary>패를 눌렀다: 지금 가리키는 카드에 이 방법을 쓴다 (아이템 카드면 산다). 다시 묻지 않는다.</summary>
        void BuyInShop(ShopProduct product)
        {
            if (_run == null || _shopResult != null)
                return;
            if (product == ShopProduct.ItemCard)
            {
                BuyShopItem();
                return;
            }
            var sealedCards = ShopSealedCards();
            if (_shopCard < 0 || _shopCard >= sealedCards.Count)
                return;
            uint code = sealedCards[_shopCard];
            var offer = ShopOfferOf(product);
            if (offer == null || ShopMethodProblem(offer, code) != null)
            {
                Debug.Log($"[런] 칩교환소: {ChipExchange.Name(product)}을(를) 살 수 없음");
                return;
            }
            if (ChipExchange.ChoosesNewSeal(product))
            {
                // 직접 고르는 교체: 대신 봉인할 카드를 고르는 창으로
                _shopProduct = product;
                _shopRelease = code;
                _selectedPile = Pile.ShopSeal;
                _selectedCode = 0;
                OpenShopPicker();
                return;
            }
            CompleteShopPurchase(product, code, 0);
        }

        void BuyShopItem()
        {
            var offer = ShopOfferOf(ShopProduct.ItemCard);
            if (offer == null || ShopItemProblem(offer) != null)
                return;
            int before = _run.Chips;
            try
            {
                _run.BuyItem();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }
            Persist();
            Sound.Play(Sfx.Coin);
            _moveNotice = $"칩교환소: 아이템 카드 {_text.CardName(offer.Card)} 구매 → 상점 덱 (남은 칩 {_run.Chips})";
            _moveNoticeIsError = false;
            Debug.Log($"[런] {_moveNotice}");
            _shopResult = new ShopResult { Product = ShopProduct.ItemCard, Item = offer.Card, Price = offer.Price, ChipsBefore = before };
            _shopRow = ShopRow.Counter;
            _selectedPile = Pile.Shop;
            _selectedCode = offer.Card;
            ShowHub();
            ShopFloat("shop-chips-box", $"-{offer.Price}", UiStyle.Bad);
        }

        /// <summary>봉인 해제·봉인 대상 교체를 치른다. newSeal이 0이면 대신 봉인할 카드는 규칙이 정한다 (무작위, 봉인 해제는 없음).</summary>
        void CompleteShopPurchase(ShopProduct product, uint release, uint newSeal)
        {
            int before = _run.Chips, mark = _run.Events.Count;
            int price = ShopOfferOf(product)?.Price ?? 0;
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
                _shopProduct = null;
                _shopRelease = 0;
                ShowHub();
                return;
            }
            Persist();
            Sound.Play(Sfx.Coin);
            var sealedNow = _run.Events.Skip(mark).FirstOrDefault(e => e.Kind == RunEventKind.CardSealed);
            _moveNotice = $"칩교환소: {ChipExchange.Name(product)} — {_text.CardName(release)} 봉인 해제 → 사이드 덱" +
                          (sealedNow != null ? $", 대신 {_text.CardName(sealedNow.Code)} 봉인" : "") + $" (남은 칩 {_run.Chips})";
            _moveNoticeIsError = false;
            Debug.Log($"[런] {_moveNotice}");
            _shopResult = new ShopResult { Product = product, Freed = release, NewSeal = sealedNow?.Code ?? 0, Price = price, ChipsBefore = before };
            _shopProduct = null;
            _shopRelease = 0;
            _shopRow = ShopRow.Counter;
            // 새로 봉인된 카드를 가리킨다 (없으면 같은 자리)
            var sealedCards = ShopSealedCards();
            int at = sealedNow != null ? sealedCards.IndexOf(sealedNow.Code) : -1;
            _shopCard = at >= 0 ? at : Mathf.Clamp(_shopCard, 0, Mathf.Max(0, sealedCards.Count - 1));
            _selectedPile = Pile.Side;
            _selectedCode = release;
            ShowHub(); // 메인 덱이 모자라면 보충 창이 장면 위에 뜬다
            ShopFloat("shop-chips-box", $"-{price}", UiStyle.Bad);
        }

        // ---- 직접 고르는 교체: 대신 봉인할 카드 고르기 (카드 고르기 창의 틀, 칩교환소 장면 위에 뜬다) ----

        void OpenShopPicker()
        {
            if (_shopProduct == null || _shopRelease == 0)
                return;
            var product = _shopProduct.Value;
            string rarity = ChipExchange.SameRarity(product) ? "같은 레어도의 " : "";
            var back = KeyButton("shop-back", "그만둔다", CancelShopPicker, "X", "K", 150);
            // 머리 오른쪽: 풀기로 한 카드와 값
            var head = Row();
            head.name = "shop-pick-release";
            head.style.alignItems = Align.Center;
            var texts = UiStyle.Box();
            texts.style.alignItems = Align.FlexEnd;
            texts.style.marginRight = 14;
            texts.Add(Text("봉인을 풀 카드", 17, UiStyle.TextDim));
            var name = Text($"「{_text.CardName(_shopRelease)}」", 22, UiStyle.Text, bold: true);
            name.style.whiteSpace = WhiteSpace.NoWrap;
            texts.Add(name);
            head.Add(texts);
            head.Add(Inert(_faces.Build(_shopRelease, 56)));
            var price = PriceTag("shop-pick-price", "재화:칩", ShopFloorColor, ShopOfferPrice(product).ToString(), UiStyle.Gold);
            price.style.marginLeft = 14;
            head.Add(price);
            var grid = OpenCardPick("shop-pick", "대신 봉인할 카드",
                $"{ShopProductText(product)} · 같은 덱의 {rarity}카드 1장을 고른다. 고르면 바로 값을 치른다.", null, "shop-detail", RenderShopDetail, back, "그만둔다",
                headRight: head, dim: 0.72f);
            foreach (uint candidate in Sorted(_run.NewSealCandidates(product, _shopRelease)))
            {
                uint code = candidate;
                grid.Add(PickSlot(Pile.ShopSeal, code, () => PickShopCard(code)));
            }
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        void CancelShopPicker()
        {
            _shopProduct = null;
            _shopRelease = 0;
            CloseModal();
        }

        /// <summary>지금 칩교환소에 진열된 이 상품의 가격. 진열할 때 그 층의 가격으로 정해져 저장된 값이다 (층대마다 오른다, docs/ECONOMY.md).</summary>
        int ShopOfferPrice(ShopProduct product) => ShopOfferOf(product)?.Price ?? RunRules.ShopPrice(product, _run.Floor);

        void RenderShopDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (_shopProduct == null)
                return;
            if (code == 0 || pile != Pile.ShopSeal)
            {
                AddPickEmpty(panel, "왼쪽에서 대신 봉인할 카드를 고른다.");
                return;
            }
            AddPickDetail(panel, code, _rarities.TryGet(code, out var grade) ? $"레어도 {grade}" : null, "shop-confirm",
                $"대신 봉인한다 · 칩 {ShopOfferPrice(_shopProduct.Value)}", () => PickShopCard(code), pinned);
        }

        void PickShopCard(uint code)
        {
            if (_shopProduct == null || _shopRelease == 0)
                return;
            CompleteShopPurchase(_shopProduct.Value, _shopRelease, code);
        }
    }
}
