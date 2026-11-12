// 듀얼 화면의 키보드 커서. docs/UI.md "키보드" › "듀얼의 키보드 커서".
// 기본 자리 두 가지를 함께 받는다: 방향키 + Z(결정)·X(취소), W A S D + J(결정)·K(취소). 내가 답할 차례이거나 창·메뉴가 떠 있으면
// Enter·Space도 결정, Backspace·Esc도 취소다 (진행을 보여 주는 중에는 스페이스·엔터가 넘기기·건너뛰기라서 결정으로 쓰지 않는다).
// - 필드 커서: 패의 카드 하나 또는 필드의 칸 하나를 가리킨다. 줄은 아래에서부터 패, 내 마법·함정, 내 몬스터, (양끝 열의 제외 칸),
//   상대 몬스터, 상대 마법·함정. 결정 키는 그 칸을 마우스로 누른 것과 같다 (카드 메뉴, 카드·칸 고르기, 더미의 카드 목록).
//   Tab은 테두리로 강조된 카드·칸(할 수 있는 일이 있거나 고를 수 있는 것)만 차례로 돌고, F는 패와 필드 사이를 건너뛴다.
// - 메뉴·창: 카드 메뉴나 창(선택 창, 대화창, 카드 목록)이 떠 있으면 그 안의 버튼과 카드를 이동 키로 고르고 결정 키로 누른다.
// 커서가 가리키는 것에는 금색 테(key-ring)를 두른다. 마우스를 움직여 패의 카드에 올리거나 화면을 누르면 키보드 커서는 풀린다.
// 유니티의 기본 키보드 이동(포커스가 버튼을 옮겨 다니는 것)은 이 화면에서 막는다 (같은 키가 두 번 먹지 않게).
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Duel;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class DuelScreen
    {
        enum Nav { None, Up, Down, Left, Right, Confirm, Cancel, NextCard, PrevCard, Jump }

        /// <summary>커서의 줄. 0은 패, 1~5는 필드의 줄 (아래에서 위로).</summary>
        const int HandRowIndex = 0, MySpellRow = 1, MyMonsterRow = 2, BandRow = 3, OppMonsterRow = 4, OppSpellRow = 5;

        /// <summary>키보드로 고르는 중인지 (금색 테를 보일지).</summary>
        bool _cursorOn;
        int _cursorRow = HandRowIndex, _cursorCol = 3, _cursorHand;
        /// <summary>F로 건너뛸 때 돌아갈 자리: 마지막으로 있던 필드 칸과 패의 카드.</summary>
        int _lastFieldRow = MyMonsterRow, _lastFieldCol = 3, _lastHand;
        VisualElement _keyLayer, _keyRing;
        /// <summary>키보드로 고르는 중인 메뉴·창 (없으면 null)과 그 안에서 고른 항목.</summary>
        VisualElement _keyScope;
        bool _keyScopeIsMenu;
        int _keyIndex;
        readonly List<(Label Cap, string Arrows, string Wasd)> _keyCaps = new List<(Label, string, string)>();
        bool _capsLayout;
#pragma warning disable 0169, 0414 // 유니티 밖(헤드리스 컴파일)에서는 키보드·마우스를 읽는 코드가 빠져서 쓰이지 않는다
        Nav _heldMove;
        float _repeatAt;
        Vector2 _lastMouse;
        int _mouseMovedFrame = -100;
#pragma warning restore 0169, 0414

        bool MouseMovedLately => Time.frameCount - _mouseMovedFrame <= 2;

        bool OverlayOpen => _modalOpen || (_menuLayer != null && _menuLayer.style.display == DisplayStyle.Flex);

        // ================= 키 표시 =================

        /// <summary>키 표시 하나. 이동 키의 자리에 따라 글자가 바뀐다 (arrows = 방향키 자리, wasd = W A S D 자리).</summary>
        Label KeyCap(string arrows, string wasd)
        {
            var cap = UiStyle.KeyCap(UiStyle.WasdLayout ? wasd : arrows);
            _keyCaps.Add((cap, arrows, wasd));
            return cap;
        }

        /// <summary>키 도움말 줄: 키 표시와 설명을 늘어놓는다.</summary>
        VisualElement Hints(params (string Arrows, string Wasd, string Label)[] items)
        {
            var row = UiStyle.Box("key-hints", FlexDirection.Row);
            row.pickingMode = PickingMode.Ignore;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = row.style.paddingRight = 8;
            row.style.paddingTop = row.style.paddingBottom = 3;
            row.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.55f);
            UiStyle.Radius(row, 6);
            for (int i = 0; i < items.Length; i++)
            {
                var (arrows, wasd, label) = items[i];
                var cap = KeyCap(arrows, wasd);
                cap.pickingMode = PickingMode.Ignore;
                cap.style.marginRight = 6;
                row.Add(cap);
                var text = UiStyle.Label(label, 15, UiStyle.TextDim);
                text.pickingMode = PickingMode.Ignore;
                text.style.whiteSpace = WhiteSpace.NoWrap;
                text.style.marginRight = i == items.Length - 1 ? 0 : 14;
                row.Add(text);
            }
            return row;
        }

        void ApplyKeyCaps()
        {
            _capsLayout = UiStyle.WasdLayout;
            _keyCaps.RemoveAll(c => c.Cap.panel == null && c.Cap.parent == null);
            foreach (var (cap, arrows, wasd) in _keyCaps)
                cap.text = UiStyle.WasdLayout ? wasd : arrows;
        }

        // ================= 화면 구성 =================

        /// <summary>금색 테를 그리는 층 (화면 맨 위, 마우스는 통과). 유니티의 기본 키보드 이동도 여기서 막는다.</summary>
        void BuildKeyLayer(VisualElement docRoot)
        {
            _keyLayer = new VisualElement { name = "key-layer", pickingMode = PickingMode.Ignore };
            _keyLayer.style.position = Position.Absolute;
            _keyLayer.style.left = _keyLayer.style.top = _keyLayer.style.right = _keyLayer.style.bottom = 0;
            _keyRing = new VisualElement { name = "key-ring", pickingMode = PickingMode.Ignore };
            _keyRing.style.position = Position.Absolute;
            UiStyle.BorderColor(_keyRing, UiStyle.Gold, 4);
            UiStyle.Radius(_keyRing, 10);
            _keyRing.style.display = DisplayStyle.None;
            _keyLayer.Add(_keyRing);
            docRoot.Add(_keyLayer);
            // 방향키·Tab으로 포커스가 버튼을 옮겨 다니지 않게 한다 (이 화면의 키보드는 HandleKeys가 직접 읽는다)
            docRoot.RegisterCallback<NavigationMoveEvent>(e =>
            {
                docRoot.focusController?.IgnoreEvent(e);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            // 화면을 마우스로 누르면 키보드 커서는 풀린다
            docRoot.RegisterCallback<PointerDownEvent>(_ => _cursorOn = false, TrickleDown.TrickleDown);
        }

        // ================= 커서가 가리키는 것 =================

        /// <summary>글 입력 칸에 글을 치는 중인지 (그동안은 글자 키를 조작으로 받지 않는다).</summary>
        bool TypingText()
        {
            var focused = _docRoot?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        }

        /// <summary>필드의 (줄, 열)에 있는 칸. 그 자리에 칸이 없으면 false (가운데 띠의 가운데 다섯 열).</summary>
        bool FieldCell(int row, int col, out VisualElement root, out CardInfo card, out bool visible, out bool highlighted)
        {
            root = null;
            card = null;
            visible = highlighted = false;
            CardCell cell = null;
            PileCell pile = null;
            switch (row)
            {
                case MySpellRow:
                    if (col == 0) pile = _extra[Me];
                    else if (col == 6) pile = _deck[Me];
                    else cell = _spells[Me, col - 1];
                    break;
                case MyMonsterRow:
                    if (col == 0) cell = _fieldZone[Me];
                    else if (col == 6) pile = _grave[Me];
                    else cell = _monsters[Me, col - 1];
                    break;
                case BandRow:
                    if (col == 0) pile = _removed[Opp];
                    else if (col == 6) pile = _removed[Me];
                    break;
                // 상대 쪽은 점대칭이라 열이 거꾸로다
                case OppMonsterRow:
                    if (col == 0) pile = _grave[Opp];
                    else if (col == 6) cell = _fieldZone[Opp];
                    else cell = _monsters[Opp, 5 - col];
                    break;
                case OppSpellRow:
                    if (col == 0) pile = _deck[Opp];
                    else if (col == 6) pile = _extra[Opp];
                    else cell = _spells[Opp, 5 - col];
                    break;
            }
            if (cell != null)
            {
                root = cell.Root;
                card = cell.Card;
                visible = cell.Visible;
                highlighted = cell.Highlighted;
            }
            else if (pile != null)
            {
                root = pile.Root;
                card = pile.TopCard;
                visible = pile.TopVisible;
                highlighted = pile.Highlighted;
            }
            return root != null;
        }

        /// <summary>패의 i번째 카드의 가운데 x (LayHand와 같은 식).</summary>
        float HandX(int index)
        {
            int n = _handCells[Me].Count;
            float spacing = Mathf.Min(164f, 1000f / Mathf.Max(1, n));
            return FieldCenterX + (index - (n - 1) / 2f) * spacing;
        }

        static int ColumnAt(float x) => Mathf.Clamp(Mathf.RoundToInt((x - FieldLeft - Tile / 2) / (Tile + TileGap)), 0, 6);

        /// <summary>그 열에서 가장 가까운 패의 카드.</summary>
        int NearestHand(int col)
        {
            float x = ColumnX(col) + Tile / 2;
            int best = 0;
            for (int i = 1; i < _handCells[Me].Count; i++)
                if (Mathf.Abs(HandX(i) - x) < Mathf.Abs(HandX(best) - x))
                    best = i;
            return best;
        }

        /// <summary>지금 키보드로 고르는 메뉴·창. 카드 메뉴가 먼저다 (창 위에 뜬다).</summary>
        VisualElement CurrentScope(out bool isMenu)
        {
            isMenu = _menuLayer != null && _menuLayer.style.display == DisplayStyle.Flex;
            if (isMenu)
                return _menuLayer.Q("card-menu");
            return _modalOpen && _modalLayer.childCount > 0 ? _modalLayer[0] : null;
        }

        /// <summary>메뉴·창 안에서 고를 수 있는 것: 켜져 있는 버튼과 카드 (화면 순서).</summary>
        static List<VisualElement> ScopeItems(VisualElement scope)
        {
            if (scope == null)
                return new List<VisualElement>();
            return scope.Query<VisualElement>().Where(e =>
                (e is Button b && b.enabledInHierarchy && b.resolvedStyle.display != DisplayStyle.None) || IsWindowCard(e)).ToList();
        }

        /// <summary>창 안의 카드 한 장인지 (선택 창의 후보, 카드 목록 창, 공개된 카드 확인 창).</summary>
        static bool IsWindowCard(VisualElement e) =>
            e.name != null && (e.name.StartsWith("candidate-") || e.name.StartsWith("pile-card-") || e.name.StartsWith("reveal-card-"));

        /// <summary>커서가 지금 가리키는 화면 요소 (없으면 null). 메뉴·창이 떠 있으면 그 안의 항목이다.</summary>
        VisualElement CursorTarget()
        {
            var scope = CurrentScope(out bool isMenu);
            if (scope != null)
            {
                var items = ScopeItems(scope);
                if (scope != _keyScope)
                {
                    // 새로 뜬 메뉴·창이면 첫 항목부터, 같은 창이 다시 그려진 것이면 (카드를 하나 골랐다) 그 자리를 지킨다
                    if (_keyScope == null || _keyScopeIsMenu != isMenu)
                        _keyIndex = 0;
                    _keyScope = scope;
                    _keyScopeIsMenu = isMenu;
                    // 버튼이 포커스를 받지 않게 한다 (엔터·스페이스가 포커스된 버튼에도 먹어 두 번 눌리는 것을 막는다)
                    scope.Query<Button>().ForEach(b => b.focusable = false);
                }
                if (items.Count == 0)
                    return null;
                _keyIndex = Mathf.Clamp(_keyIndex, 0, items.Count - 1);
                return items[_keyIndex];
            }
            _keyScope = null;
            if (_cursorRow == HandRowIndex)
            {
                int hand = _handCells[Me].Count;
                if (hand > 0)
                {
                    _cursorHand = Mathf.Clamp(_cursorHand, 0, hand - 1);
                    return _handCells[Me][_cursorHand].Root;
                }
                // 패가 비었다: 내 몬스터 줄로 올라간다
                _cursorRow = MyMonsterRow;
                _cursorCol = Mathf.Clamp(_cursorCol, 0, 6);
            }
            return FieldCell(_cursorRow, _cursorCol, out var root, out _, out _, out _) ? root : null;
        }

        /// <summary>
        /// 프레임마다: 커서가 가리키는 것에 금색 테를 두르고, 패의 카드를 가리키면 그 카드를 올려 보인다.
        /// </summary>
        void UpdateKeyCursor()
        {
            if (_keyRing == null || _session == null)
                return;
            if (_capsLayout != UiStyle.WasdLayout)
                ApplyKeyCaps();
            var target = _cursorOn && !_autoPlay ? CursorTarget() : null;
            if (!_cursorOn)
                _keyScope = null;
            else if (_keyScope == null)
            {
                // 키보드로 패의 카드를 가리키는 동안 그 카드를 올려 보인다
                int want = _cursorRow == HandRowIndex && target != null ? _cursorHand : -1;
                if (_hoveredHand != want)
                {
                    _hoveredHand = want;
                    LayHand(Me);
                }
            }
            if (target == null || target.panel == null)
            {
                _keyRing.style.display = DisplayStyle.None;
                return;
            }
            var bound = target.worldBound;
            var at = _keyLayer.WorldToLocal(bound.position);
            _keyRing.style.left = at.x - 6;
            _keyRing.style.top = at.y - 6;
            _keyRing.style.width = bound.width + 12;
            _keyRing.style.height = bound.height + 12;
            _keyRing.style.display = DisplayStyle.Flex;
        }

        // ================= 조작 =================

        /// <summary>프레임마다 (HandleKeys에서): 이동·결정·취소·Tab·F를 읽어 커서를 움직인다.</summary>
        void HandleCursorKeys()
        {
            bool waiting = _interaction != null;
            var nav = ReadNav(confirmWithEnterAndSpace: waiting || OverlayOpen, typing: TypingText());
            if (nav == Nav.None)
                return;
            if (nav == Nav.Cancel)
            {
                CancelKey();
                return;
            }
            if (_autoPlay)
                return;
            if (!_cursorOn)
            {
                // 첫 키는 커서를 켜기만 한다 (마지막으로 있던 자리, 메뉴·창이 떠 있으면 그 첫 항목). 무엇을 가리키는지 보고 나서 누르게 한다.
                _cursorOn = true;
                if (!OverlayOpen)
                    PinCursorCard();
                // 진행을 보여 주는 중의 결정 키는 멈춘 단계를 넘긴다
                if (nav == Nav.Confirm && !waiting && _focus != null && !OverlayOpen)
                    _holdUntil = 0;
                return;
            }
            var scope = CurrentScope(out _);
            if (scope != null)
            {
                ScopeKey(scope, nav);
                return;
            }
            switch (nav)
            {
                case Nav.Confirm:
                    if (!waiting && _focus != null)
                    {
                        _holdUntil = 0; // 진행을 보여 주는 중: 멈춘 단계를 넘긴다
                        return;
                    }
                    Press(CursorTarget());
                    break;
                case Nav.NextCard:
                case Nav.PrevCard:
                    CycleHighlighted(nav == Nav.PrevCard);
                    break;
                case Nav.Jump:
                    JumpBetweenHandAndField();
                    break;
                default:
                    MoveCursor(nav);
                    break;
            }
        }

        /// <summary>마우스로 누른 것과 같게 누른다 (버튼, 카드 칸, 더미).</summary>
        static void Press(VisualElement target)
        {
            if (target == null || target.panel == null || !target.enabledInHierarchy)
                return;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = target;
                target.SendEvent(submit);
            }
        }

        /// <summary>
        /// 취소 키 (X, K, Backspace, Esc): 메뉴가 있으면 메뉴만, 창이 있으면 창을 닫고(필드 확인), 필드 확인 중이면 선택 창을 다시 연다.
        /// 아무것도 떠 있지 않으면 지금 물음의 "취소" 버튼을 누른다 (공격 대상 고르기 등).
        /// </summary>
        void CancelKey()
        {
            if (_menuLayer != null && _menuLayer.style.display == DisplayStyle.Flex)
                CloseMenu();
            else if (_modalOpen)
            {
                if (!_revealOpen) // 공개된 카드 확인 창은 그냥 닫는다 (필드 확인 상태로 가지 않는다)
                    _windowHidden = true;
                CloseModal();
                Render();
            }
            else if (InFieldView)
            {
                _windowHidden = false;
                if (_interaction.Dialog != null)
                    OpenDialog(_interaction.Dialog);
                else
                    OpenSelectionWindow();
                Render();
            }
            else if (_interaction != null && !_autoPlay)
            {
                var cancel = _interaction.Buttons.FirstOrDefault(b => b.Label == "취소");
                if (cancel != null)
                    Respond(cancel.Response);
            }
        }

        /// <summary>메뉴·창 안에서: 이동 키는 그 방향으로 가장 가까운 항목으로, 결정 키는 그 항목을 누른다.</summary>
        void ScopeKey(VisualElement scope, Nav nav)
        {
            var items = ScopeItems(scope);
            if (items.Count == 0)
                return;
            _keyIndex = Mathf.Clamp(_keyIndex, 0, items.Count - 1);
            if (nav == Nav.Confirm)
            {
                Press(items[_keyIndex]);
                return;
            }
            if (nav == Nav.NextCard || nav == Nav.PrevCard)
            {
                _keyIndex = (_keyIndex + (nav == Nav.NextCard ? 1 : items.Count - 1)) % items.Count;
            }
            else
            {
                Vector2 direction = nav == Nav.Left ? Vector2.left : nav == Nav.Right ? Vector2.right
                    : nav == Nav.Up ? Vector2.down : nav == Nav.Down ? Vector2.up : Vector2.zero; // 화면은 아래가 +y
                if (direction == Vector2.zero)
                    return;
                Vector2 from = items[_keyIndex].worldBound.center;
                int best = -1;
                float bestCost = float.MaxValue;
                for (int i = 0; i < items.Count; i++)
                {
                    if (i == _keyIndex)
                        continue;
                    Vector2 d = items[i].worldBound.center - from;
                    float along = Vector2.Dot(d, direction);
                    if (along < 4f)
                        continue;
                    float across = (d - along * direction).magnitude;
                    float cost = along + across * 2.5f;
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = i;
                    }
                }
                if (best < 0)
                    return;
                _keyIndex = best;
            }
            var item = items[_keyIndex];
            item.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(item);
            // 창 안의 카드는 왼쪽 상세 카드에도 띄운다 (마우스를 올린 것처럼)
            if (IsWindowCard(item))
                using (var enter = PointerEnterEvent.GetPooled())
                {
                    enter.target = item;
                    item.SendEvent(enter);
                }
        }

        /// <summary>커서가 가리키는 카드를 왼쪽 상세 카드에 띄운다 (상대의 뒷면 카드는 뜨지 않는다).</summary>
        void PinCursorCard()
        {
            if (_cursorRow == HandRowIndex)
            {
                var cells = _handCells[Me];
                if (cells.Count == 0)
                    return;
                var cell = cells[Mathf.Clamp(_cursorHand, 0, cells.Count - 1)];
                ShowCard(cell.Card, cell.Visible);
            }
            else if (FieldCell(_cursorRow, _cursorCol, out _, out var card, out bool visible, out _))
                ShowCard(card, visible);
        }

        /// <summary>
        /// 이동: 패에서는 좌우로 카드를, 위로는 그 카드 바로 위 열의 내 마법·함정 칸으로. 필드에서는 일곱 열을 좌우로 (끝에서는 그대로),
        /// 위아래로 줄을 옮긴다. 가운데 띠(제외 칸)는 양끝 열에서만 거친다. 내 마법·함정 줄에서 아래로 가면 가장 가까운 패 카드로 내려온다.
        /// </summary>
        void MoveCursor(Nav nav)
        {
            int hand = _handCells[Me].Count;
            if (_cursorRow == HandRowIndex && hand > 0)
            {
                _cursorHand = Mathf.Clamp(_cursorHand, 0, hand - 1);
                if (nav == Nav.Left) _cursorHand = Mathf.Max(0, _cursorHand - 1);
                else if (nav == Nav.Right) _cursorHand = Mathf.Min(hand - 1, _cursorHand + 1);
                else if (nav == Nav.Up)
                {
                    _lastHand = _cursorHand;
                    _cursorRow = MySpellRow;
                    _cursorCol = ColumnAt(HandX(_cursorHand));
                }
            }
            else
            {
                if (_cursorRow == HandRowIndex)
                    _cursorRow = MyMonsterRow; // 패가 비어 있다
                if (nav == Nav.Left || nav == Nav.Right)
                {
                    if (_cursorRow == BandRow)
                        _cursorCol = _cursorCol == 0 ? 6 : 0;
                    else
                        _cursorCol = Mathf.Clamp(_cursorCol + (nav == Nav.Right ? 1 : -1), 0, 6);
                }
                else
                {
                    int step = nav == Nav.Up ? 1 : -1;
                    int row = _cursorRow + step;
                    if (row == BandRow && _cursorCol != 0 && _cursorCol != 6)
                        row += step;
                    if (row > OppSpellRow)
                        return;
                    if (row >= MySpellRow)
                        _cursorRow = row;
                    else if (hand > 0)
                    {
                        _lastFieldRow = _cursorRow;
                        _lastFieldCol = _cursorCol;
                        _cursorRow = HandRowIndex;
                        _cursorHand = NearestHand(_cursorCol);
                    }
                }
            }
            PinCursorCard();
        }

        /// <summary>F: 패와 필드 사이를 한 번에 건너뛴다 (마지막으로 있던 자리로).</summary>
        void JumpBetweenHandAndField()
        {
            int hand = _handCells[Me].Count;
            if (_cursorRow == HandRowIndex)
            {
                _lastHand = _cursorHand;
                _cursorRow = _lastFieldRow;
                _cursorCol = _lastFieldCol;
            }
            else if (hand > 0)
            {
                _lastFieldRow = _cursorRow;
                _lastFieldCol = _cursorCol;
                _cursorRow = HandRowIndex;
                _cursorHand = Mathf.Clamp(_lastHand, 0, hand - 1);
            }
            PinCursorCard();
        }

        /// <summary>
        /// Tab: 테두리로 강조된 카드·칸(할 수 있는 일이 있거나 고를 수 있는 것)만 차례로 돈다.
        /// 순서는 패 → 내 몬스터 → 내 마법·함정 → 제외 칸 → 상대 몬스터 → 상대 마법·함정. Shift+Tab은 거꾸로.
        /// </summary>
        void CycleHighlighted(bool backward)
        {
            var stops = new List<(int Row, int Index)>();
            for (int i = 0; i < _handCells[Me].Count; i++)
                if (_handCells[Me][i].Highlighted)
                    stops.Add((HandRowIndex, i));
            foreach (int row in new[] { MyMonsterRow, MySpellRow, BandRow, OppMonsterRow, OppSpellRow })
                for (int col = 0; col <= 6; col++)
                    if (FieldCell(row, col, out _, out _, out _, out bool highlighted) && highlighted)
                        stops.Add((row, col));
            if (stops.Count == 0)
                return;
            int current = stops.FindIndex(s => s.Row == _cursorRow && s.Index == (_cursorRow == HandRowIndex ? _cursorHand : _cursorCol));
            int next = current < 0 ? (backward ? stops.Count - 1 : 0) : (current + (backward ? stops.Count - 1 : 1)) % stops.Count;
            if (_cursorRow == HandRowIndex)
                _lastHand = _cursorHand;
            else
            {
                _lastFieldRow = _cursorRow;
                _lastFieldCol = _cursorCol;
            }
            _cursorRow = stops[next].Row;
            if (_cursorRow == HandRowIndex)
                _cursorHand = stops[next].Index;
            else
                _cursorCol = stops[next].Index;
            PinCursorCard();
        }

        /// <summary>
        /// 이번 프레임에 들어온 조작 하나. 이동 키는 누르고 있으면 되풀이된다 (0.38초 뒤부터 0.09초마다).
        /// 글자 키는 한/영 상태와 상관없이 키의 자리로 읽는다. 마우스가 움직였는지도 여기서 본다.
        /// </summary>
        /// <param name="typing">글 입력 칸에 글을 치는 중: Esc(취소)만 받는다.</param>
        Nav ReadNav(bool confirmWithEnterAndSpace, bool typing)
        {
#if ENABLE_INPUT_SYSTEM // 유니티 밖(헤드리스 컴파일)에는 Input System이 없다
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                if ((at - _lastMouse).sqrMagnitude > 9f)
                {
                    _lastMouse = at;
                    _mouseMovedFrame = Time.frameCount;
                }
            }
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return Nav.None;
            if (typing)
                return keyboard.escapeKey.wasPressedThisFrame ? Nav.Cancel : Nav.None;
            Nav held = Nav.None;
            bool wasd = false;
            if (keyboard.upArrowKey.isPressed) held = Nav.Up;
            else if (keyboard.downArrowKey.isPressed) held = Nav.Down;
            else if (keyboard.leftArrowKey.isPressed) held = Nav.Left;
            else if (keyboard.rightArrowKey.isPressed) held = Nav.Right;
            else
            {
                wasd = true;
                if (keyboard.wKey.isPressed) held = Nav.Up;
                else if (keyboard.sKey.isPressed) held = Nav.Down;
                else if (keyboard.aKey.isPressed) held = Nav.Left;
                else if (keyboard.dKey.isPressed) held = Nav.Right;
            }
            if (held == Nav.None)
                _heldMove = Nav.None;
            else
            {
                float now = Time.unscaledTime;
                if (held != _heldMove)
                {
                    _heldMove = held;
                    _repeatAt = now + 0.38f;
                    UiStyle.WasdLayout = wasd;
                    return held;
                }
                if (now >= _repeatAt)
                {
                    _repeatAt = now + 0.09f;
                    return held;
                }
            }
            if (keyboard.zKey.wasPressedThisFrame)
            {
                UiStyle.WasdLayout = false;
                return Nav.Confirm;
            }
            if (keyboard.jKey.wasPressedThisFrame)
            {
                UiStyle.WasdLayout = true;
                return Nav.Confirm;
            }
            if (confirmWithEnterAndSpace && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                                             || keyboard.spaceKey.wasPressedThisFrame))
                return Nav.Confirm;
            if (keyboard.xKey.wasPressedThisFrame)
            {
                UiStyle.WasdLayout = false;
                return Nav.Cancel;
            }
            if (keyboard.kKey.wasPressedThisFrame)
            {
                UiStyle.WasdLayout = true;
                return Nav.Cancel;
            }
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                return Nav.Cancel;
            if (keyboard.tabKey.wasPressedThisFrame)
                return keyboard.shiftKey.isPressed ? Nav.PrevCard : Nav.NextCard;
            if (keyboard.fKey.wasPressedThisFrame)
                return Nav.Jump;
#endif
            return Nav.None;
        }
    }
}
