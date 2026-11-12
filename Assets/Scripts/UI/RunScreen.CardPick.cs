// 런 화면: 카드 고르기 창의 틀. docs/UI.md "나머지 창 > 카드 고르기". RunScreen.cs의 머리말 참고.
// 카드를 골라야 하는 창 셋(보스 보상의 봉인 해제, 메인 덱 보충, 칩교환소에서 풀 카드·대신 봉인할 카드)이 같은 틀을 쓴다:
// 덱 창과 같은 크기의 돌판에 제목과 설명, 왼쪽에 격자(한 줄 10장), 오른쪽에 상세 카드와 금색 실행 버튼.
// 마우스: 올리면 미리 보기, 누르면 고르기(실행 버튼이 나온다), 두 번 누르면 바로 실행.
// 키보드: 이동 키로 카드를 고르고(첫 키는 첫 카드를 가리키기만 한다) 결정 키로 실행한다. 닫을 수 있는 창만 취소 키를 받는다.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        const float PickCardWidth = 119, PickDetailWidth = 324, PickDetailHeight = 640;
        const int PickColumns = 10;

        readonly List<(Pile Pile, uint Code, VisualElement Slot, Action Confirm)> _pickSlots = new List<(Pile, uint, VisualElement, Action)>();
        VisualElement _cardPickGrid;
        /// <summary>키보드로 가리키는 카드의 자리 (-1 = 아직 없음). 같은 창이 다시 열리면 이어진다.</summary>
        int _pickCursor = -1;
        string _pickName;

        bool PickOpen => _cardPickGrid != null && _cardPickGrid.panel != null && _modalLayer.style.display != DisplayStyle.None;

        /// <summary>
        /// 카드 고르기 창을 열고 격자를 돌려준다 (PickSlot으로 카드를 넣는다). 상세 칸은 render가 그린다.
        /// cancel이 있으면 아래 줄 오른쪽에 놓고 취소 키가 그것을 누른다. 없으면 닫을 수 없는 창이다.
        /// </summary>
        VisualElement OpenCardPick(string name, string title, string intro, string warning, string detailName,
            Action<VisualElement, Pile, uint, bool> render, Button cancel = null, string cancelLabel = null)
        {
            var panel = OpenSlab(name, 1720);
            panel.style.height = 960;
            panel.style.paddingLeft = panel.style.paddingRight = 24;
            panel.style.paddingTop = 20;
            panel.style.paddingBottom = 18;
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.style.flexShrink = 0;
            head.Add(WindowTitle(title));
            var introLabel = Text(intro, 19, UiStyle.TextDim);
            introLabel.style.marginLeft = 18;
            introLabel.style.marginBottom = 7;
            introLabel.style.flexShrink = 1;
            introLabel.style.whiteSpace = WhiteSpace.Normal;
            head.Add(introLabel);
            panel.Add(head);
            if (!string.IsNullOrEmpty(warning))
            {
                var note = Text(warning, 17, UiStyle.Opponent, top: 4);
                note.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(note);
            }

            var body = Row();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.marginTop = 14;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.width = PickColumns * (PickCardWidth + 12) + 28; // 세로 스크롤 막대 자리까지
            scroll.style.flexShrink = 0;
            var grid = Grid(scroll.contentContainer);
            body.Add(scroll);
            var detail = UiStyle.Box(detailName);
            detail.style.width = PickDetailWidth;
            detail.style.marginLeft = StyleKeyword.Auto;
            detail.style.flexShrink = 0;
            body.Add(detail);
            panel.Add(body);

            var hints = cancel != null
                ? Hints(("← ↑ ↓ →", "W A S D", "카드 고르기"), ("Z", "J", "고른다"), ("X", "K", cancelLabel ?? "그만둔다"))
                : Hints(("← ↑ ↓ →", "W A S D", "카드 고르기"), ("Z", "J", "고른다"));
            panel.Add(cancel != null ? WindowFooter(hints, cancel) : WindowFooter(hints));
            ApplyKeyCaps();

            _detail = detail;
            _renderDetail = render;
            _pickSlots.Clear();
            _cardPickGrid = grid;
            if (_pickName != name)
                _pickCursor = -1;
            _pickName = name;
            return grid;
        }

        /// <summary>카드 고르기 창의 카드 한 장. confirm = 두 번 누르거나 결정 키를 눌렀을 때 할 일.</summary>
        VisualElement PickSlot(Pile pile, uint code, Action confirm, int count = 0)
        {
            var slot = CardSlot(pile, code, CardFaceSize.Small, count: count, onDoubleClick: confirm, draggable: false, width: PickCardWidth);
            slot.focusable = false; // 키보드는 화면이 직접 읽는다
            UiStyle.Margin(slot, 2);
            _pickSlots.Add((pile, code, slot, confirm));
            return slot;
        }

        /// <summary>상세 칸: 상세 카드, 그 카드의 정보 한 줄, 금색 실행 버튼 (고른 카드일 때만).</summary>
        void AddPickDetail(VisualElement panel, uint code, string info, string buttonName, string buttonText, Action confirm, bool pinned)
        {
            panel.Add(_faces.BuildDetail(code, PickDetailWidth, PickDetailHeight));
            if (!string.IsNullOrEmpty(info))
            {
                var line = Text(info, 17, UiStyle.TextDim, top: 8);
                line.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(line);
            }
            if (!pinned)
                return;
            var button = UiStyle.StoneButton(buttonText, confirm, gold: true);
            button.name = buttonName;
            button.style.marginTop = 10;
            button.style.fontSize = 23;
            button.style.paddingLeft = button.style.paddingRight = 8;
            panel.Add(button);
        }

        /// <summary>상세 칸: 아직 고른 카드가 없을 때의 안내.</summary>
        static void AddPickEmpty(VisualElement panel, string text)
        {
            var label = Text(text, 19, UiStyle.TextDim);
            label.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(label);
        }

        void PickKey(Nav nav)
        {
            if (nav == Nav.Cancel)
            {
                ScopeKey(_modalLayer[0], Nav.Cancel);
                return;
            }
            int n = _pickSlots.Count;
            if (n == 0)
                return;
            if (_pickCursor >= n)
                _pickCursor = n - 1;
            if (nav == Nav.Confirm)
            {
                // 가리키는 카드가 고른 카드일 때만 실행한다 (마우스로 다른 카드를 고른 뒤라면 그 카드를 가리키게만 한다)
                if (_pickCursor >= 0 && IsSelected(_pickSlots[_pickCursor].Pile, _pickSlots[_pickCursor].Code))
                    _pickSlots[_pickCursor].Confirm?.Invoke();
                else
                    PointPick(Mathf.Max(0, _pickSlots.FindIndex(s => IsSelected(s.Pile, s.Code))));
                return;
            }
            int cursor = _pickCursor;
            if (cursor < 0)
                cursor = 0; // 첫 키는 첫 카드를 가리키기만 한다
            else
            {
                switch (nav)
                {
                    case Nav.Left:
                        if (cursor % PickColumns > 0)
                            cursor--;
                        break;
                    case Nav.Right:
                        if (cursor % PickColumns < PickColumns - 1 && cursor + 1 < n)
                            cursor++;
                        break;
                    case Nav.Up:
                        if (cursor - PickColumns >= 0)
                            cursor -= PickColumns;
                        break;
                    case Nav.Down:
                        if (cursor + PickColumns < n)
                            cursor += PickColumns;
                        else if (cursor / PickColumns < (n - 1) / PickColumns)
                            cursor = n - 1; // 아랫줄이 짧으면 그 줄의 끝으로
                        break;
                    default:
                        return;
                }
            }
            PointPick(cursor);
        }

        /// <summary>키보드로 카드를 가리킨다: 그 카드를 고르고(금색 테, 상세 칸) 보이게 스크롤한다.</summary>
        void PointPick(int index)
        {
            if (index < 0 || index >= _pickSlots.Count)
                return;
            _pickCursor = index;
            var (pile, code, slot, _) = _pickSlots[index];
            Select(pile, code);
            slot.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(slot);
        }
    }
}
