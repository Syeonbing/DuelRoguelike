// 런 화면: 탑 위에 뜨는 창들의 공통 틀. docs/UI.md "나머지 창". RunScreen.cs의 머리말 참고.
// - 돌판 창(OpenSlab): 장면을 어둡게 덮고 가운데에 돌판을 띄운다. 크기는 1920×1080 기준이고 허브와 같은 배율로 줄인다.
// - 창의 제목은 "[ 제목. ]" (금색 Black), 창 아래 줄에는 키 도움말(modal-hints)과 닫는 버튼.
// - 창의 키보드: 목록(MenuList)이 있으면 위아래로 고르고 결정 키로 실행, 버튼 줄(ButtonRow)이 있으면 좌우로 고른다.
//   취소 키는 그 창의 닫기·나중에 버튼을 누른다 (RunScreen.Keys.cs의 CancelButtons).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        /// <summary>지금 열린 창의 목록과 버튼 줄 (없으면 null). 창을 열 때 비우고, 창이 제 것을 넣는다.</summary>
        MenuList _windowMenu;
        ButtonRow _windowRow;
        /// <summary>닫는 버튼이 없는 창의 취소 키가 할 일 (없으면 그 창의 닫기·나중에 버튼을 누른다).</summary>
        Action _windowCancel;

        bool WindowKeysOpen =>
            (_windowMenu != null && _windowMenu.Root.panel != null) || (_windowRow != null && _windowRow.Root.panel != null);

        /// <summary>
        /// 돌판 창. 선형 색 공간에서는 어두운 덮개가 옅게 보여서 시안(66%)보다 진하게(90%) 덮는다.
        /// 키 도움말은 창이 제 아래 줄에 넣는다 (WindowFooter).
        /// </summary>
        VisualElement OpenSlab(string name, float width, float dim = 0.9f)
        {
            var panel = OpenModal(width);
            panel.name = name;
            _modalLayer.style.backgroundColor = new Color(0, 0, 0, dim);
            _modalLayer.Q("modal-hints")?.RemoveFromHierarchy();
            panel.style.maxWidth = StyleKeyword.None;
            panel.style.flexShrink = 0;
            panel.style.paddingLeft = panel.style.paddingRight = 34;
            panel.style.paddingTop = 28;
            panel.style.paddingBottom = 24;
            UiStyle.BorderColor(panel, UiStyle.Stone9, 3);
            panel.style.borderTopColor = UiStyle.SlabTop;
            FitWindow(panel);
            return panel;
        }

        /// <summary>창을 허브의 무대와 같은 배율로 줄인다 (1920×1080 기준).</summary>
        void FitWindow(VisualElement panel)
        {
            float w = _root.layout.width, h = _root.layout.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0)
                return;
            float scale = Mathf.Min(w / StageWidth, h / StageHeight);
            panel.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        /// <summary>창의 제목: "[ 제목. ]".</summary>
        static Label WindowTitle(string text, int size = 38)
        {
            var title = UiStyle.Label($"[ {text}. ]", size, UiStyle.Gold);
            UiStyle.Black(title);
            title.style.whiteSpace = WhiteSpace.NoWrap;
            return title;
        }

        /// <summary>돌 버튼 오른쪽에 키 표시를 단 것 (닫기 X 등).</summary>
        Button KeyButton(string name, string text, Action run, string arrows, string wasd, float width, bool gold = false)
        {
            var button = UiStyle.StoneButton(text, run, gold, small: true);
            button.name = name;
            button.style.width = width;
            button.style.paddingLeft = 14;
            button.style.paddingRight = 44;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            var key = KeyCap(arrows, wasd);
            key.pickingMode = PickingMode.Ignore;
            key.style.position = Position.Absolute;
            key.style.right = 9;
            key.style.top = 8;
            button.Add(key);
            return button;
        }

        /// <summary>창의 아래 줄: 왼쪽에 키 도움말, 오른쪽에 버튼들.</summary>
        VisualElement WindowFooter(VisualElement hints, params VisualElement[] buttons)
        {
            var footer = Row();
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 22;
            footer.style.flexShrink = 0;
            hints.name = "modal-hints";
            footer.Add(hints);
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            footer.Add(gap);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i > 0)
                    buttons[i].style.marginLeft = 12;
                footer.Add(buttons[i]);
            }
            return footer;
        }

        /// <summary>목록의 한 줄에 넣는 글 (누르기는 줄이 받는다).</summary>
        static Label RowLabel(string text, int size, Color color, bool bold = false)
        {
            var label = UiStyle.Label(text, size, color, bold);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>목록의 줄 오른쪽 끝에 붙는 꼬리표 (조건, 값).</summary>
        static Label RowTag(string text, int size, Color color)
        {
            var tag = RowLabel(text, size, color, bold: true);
            tag.style.whiteSpace = WhiteSpace.NoWrap;
            tag.style.marginLeft = StyleKeyword.Auto;
            tag.style.paddingLeft = 16;
            tag.style.flexShrink = 0;
            return tag;
        }

        /// <summary>색 바탕 위의 픽토그램 아이콘 (칩교환소의 상품, 도감의 칸).</summary>
        static VisualElement IconTile(string pictogram, float size, Color color)
        {
            var tile = new VisualElement();
            tile.pickingMode = PickingMode.Ignore;
            tile.style.width = tile.style.height = size;
            tile.style.flexShrink = 0;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.backgroundColor = color;
            UiStyle.Radius(tile, 6);
            var data = Pictograms.Get(pictogram);
            if (data != null)
            {
                var view = new PictogramView(data, UiStyle.Hex(0x1b140b));
                view.style.width = view.style.height = size * 0.66f;
                tile.Add(view);
            }
            return tile;
        }

        // 이벤트의 그림 칸에 넣는 픽토그램. 이벤트마다 새로 그린 것이 아니라 지금 있는 픽토그램에서 고른 것이다.
        static readonly Dictionary<string, string> EventPictograms = new Dictionary<string, string>
        {
            { "quiet-floor", "속성:바람" },
            { "lucky-coin", "층:칩교환소" },
            { "dragon-shrine", "드래곤족" },
            { "old-duelist", "전사족" },
            { "white-dragon-legend", "속성:빛" },
            { "crossroads", "속성:땅" },
            { "barter", "마법:장착" },
            { "rockfall", "암석족" },
            { "pickpocket", "악마족" },
            { "cursed-mirror", "속성:어둠" },
            { "toll-gate", "함정:지속" },
            { "ambush", "함정:일반" },
        };

        static string EventPictogram(string eventId) =>
            eventId != null && EventPictograms.TryGetValue(eventId, out string name) ? name : "마법:일반";

        /// <summary>이벤트의 그림 칸: 카드의 그림 칸과 같은 방식 (어두운 바탕, 이벤트 색 빛, 픽토그램).</summary>
        static VisualElement EventArt(string eventId, float size)
        {
            var art = new VisualElement { name = "event-art" };
            art.pickingMode = PickingMode.Ignore;
            art.style.width = art.style.height = size;
            art.style.flexShrink = 0;
            art.style.alignItems = Align.Center;
            art.style.justifyContent = Justify.Center;
            art.style.backgroundColor = UiStyle.Hex(0x1a1410);
            UiStyle.BorderColor(art, UiStyle.Stone9, 3);
            var glow = new VisualElement();
            glow.style.position = Position.Absolute;
            glow.style.left = glow.style.top = glow.style.right = glow.style.bottom = 0;
            glow.style.backgroundImage = new StyleBackground(UiStyle.Radial);
            glow.style.unityBackgroundImageTintColor = UiStyle.Alpha(EventFloorColor, 0.45f);
            art.Add(glow);
            var data = Pictograms.Get(EventPictogram(eventId));
            if (data != null)
            {
                var view = new PictogramView(data, UiStyle.Text);
                view.style.width = view.style.height = size * 0.62f;
                art.Add(view);
            }
            return art;
        }

        /// <summary>
        /// 좌우로 고르는 버튼 줄 (듀얼 창과 같은 모양): 돌 버튼을 나란히 놓고, 고른 버튼에 ▶를 붙인다.
        /// </summary>
        sealed class ButtonRow
        {
            public readonly VisualElement Root = UiStyle.Box(null, FlexDirection.Row);
            readonly List<(Button Button, Label Mark, bool Gold)> _items = new List<(Button, Label, bool)>();

            public int Index { get; private set; }

            public Button Add(string name, string text, Action run, bool gold = false, float width = 300)
            {
                int index = _items.Count;
                var button = UiStyle.StoneButton(text, run, gold);
                button.name = name;
                button.style.width = width;
                if (index > 0)
                    button.style.marginLeft = 16;
                var mark = UiStyle.Label("▶", 19, Color.clear);
                mark.pickingMode = PickingMode.Ignore;
                UiStyle.Place(mark, 16, 18);
                button.Add(mark);
                button.RegisterCallback<MouseEnterEvent>(_ => Select(index));
                _items.Add((button, mark, gold));
                Root.Add(button);
                return button;
            }

            public void Select(int index)
            {
                if (_items.Count == 0)
                    return;
                Index = Mathf.Clamp(index, 0, _items.Count - 1);
                for (int i = 0; i < _items.Count; i++)
                    _items[i].Mark.style.color = i != Index ? Color.clear : _items[i].Gold ? GoldInk : UiStyle.Gold;
            }

            /// <summary>좌우로 옮긴다 (끝에서 반대쪽 끝으로 넘어간다). 꺼진 버튼은 건너뛴다.</summary>
            public void Move(int delta)
            {
                if (_items.Count == 0)
                    return;
                int i = Index;
                for (int n = 0; n < _items.Count; n++)
                {
                    i = (i + delta + _items.Count) % _items.Count;
                    if (_items[i].Button.enabledSelf)
                        break;
                }
                Select(i);
            }

            public void Activate()
            {
                if (Index >= 0 && Index < _items.Count)
                    Press(_items[Index].Button);
            }
        }

        /// <summary>창의 키보드: 목록은 위아래, 버튼 줄은 좌우, 결정 키로 실행, 취소 키는 닫는 버튼.</summary>
        void WindowKey(Nav nav)
        {
            bool menu = _windowMenu != null && _windowMenu.Root.panel != null;
            bool row = _windowRow != null && _windowRow.Root.panel != null;
            switch (nav)
            {
                case Nav.Up:
                    if (menu)
                        _windowMenu.Move(-1);
                    break;
                case Nav.Down:
                    if (menu)
                        _windowMenu.Move(1);
                    break;
                case Nav.Left:
                    if (row)
                        _windowRow.Move(-1);
                    break;
                case Nav.Right:
                    if (row)
                        _windowRow.Move(1);
                    break;
                case Nav.Confirm:
                    if (menu)
                        _windowMenu.Activate();
                    else if (row)
                        _windowRow.Activate();
                    break;
                case Nav.Cancel:
                    if (_windowCancel != null)
                        _windowCancel();
                    else
                        ScopeKey(_modalLayer[0], Nav.Cancel);
                    break;
            }
        }
    }
}
