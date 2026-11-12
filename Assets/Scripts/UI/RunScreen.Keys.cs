// 런 화면의 키보드. docs/UI.md "키보드".
// 기본 자리 두 가지를 언제나 함께 받는다: 방향키 + Z(결정)·X(취소), W A S D + J(결정)·K(취소).
// Enter·Space는 결정, Esc·Backspace는 취소로 함께 받는다. 글자 키는 한/영 상태와 상관없이 키의 자리로 읽는다.
// 화면의 키 표시는 마지막에 쓴 이동 키의 자리에 맞춘다.
// 허브의 명령 메뉴, 올라가는 연출(건너뛰기), 듀얼 창, 덱 창, 보상 고르기는 저마다의 키보드가 있다.
// 그 밖의 창과 화면(런 시작, 덱 편집, 런 종료, 이벤트, 칩교환소, 휴식, 코인 토스, 런 기록 등)은 공통 키보드를 쓴다:
// 그 안의 켜진 버튼과 카드를 이동 키로 옮겨 다니고 결정 키로 누른다. 취소 키는 닫기·뒤로 버튼을 누른다.
// 허브의 버튼은 키보드 포커스를 받지 않게 해 두었다 (UI Toolkit의 기본 키보드 이동과 겹치지 않게).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        enum Nav
        {
            None,
            Up,
            Down,
            Left,
            Right,
            Confirm,
            Cancel,
            /// <summary>Q: 앞 탭</summary>
            PrevTab,
            /// <summary>E: 다음 탭</summary>
            NextTab,
        }

        /// <summary>마지막에 쓴 이동 키의 자리: false = 방향키 + Z·X, true = W A S D + J·K.</summary>
        bool _wasdLayout
        {
            get => UiStyle.WasdLayout;
            set => UiStyle.WasdLayout = value;
        }
        readonly List<(Label Cap, string Arrows, string Wasd)> _keyCaps = new List<(Label, string, string)>();
#pragma warning disable 0169 // 유니티 밖(헤드리스 컴파일)에서는 키보드를 읽는 코드가 빠져서 쓰이지 않는다
        Nav _heldMove;
        float _repeatAt;
#pragma warning restore 0169
        /// <summary>이 프레임까지는 키를 받지 않는다 (듀얼 화면에서 누른 키가 돌아온 허브에 그대로 들어가지 않게).</summary>
        int _keysBlockedFrame;

        /// <summary>키 표시 하나. 이동 키의 자리에 따라 글자가 바뀐다 (arrows = 방향키 자리, wasd = W A S D 자리).</summary>
        Label KeyCap(string arrows, string wasd)
        {
            var cap = UiStyle.KeyCap(_wasdLayout ? wasd : arrows);
            _keyCaps.Add((cap, arrows, wasd));
            return cap;
        }

        /// <summary>키 도움말 줄: 키 표시와 설명을 늘어놓는다. 설명이 null이면 키 표시만 놓는다 (다음 키와 한 묶음).</summary>
        VisualElement Hints(params (string Arrows, string Wasd, string Label)[] items)
        {
            var row = Row();
            row.style.alignItems = Align.Center;
            foreach (var (arrows, wasd, label) in items)
            {
                var cap = KeyCap(arrows, wasd);
                cap.style.marginRight = label == null ? 4 : 7;
                row.Add(cap);
                if (label == null)
                    continue;
                var text = UiStyle.Label(label, 17, UiStyle.TextDim);
                text.style.whiteSpace = WhiteSpace.NoWrap;
                text.style.marginRight = 18;
                row.Add(text);
            }
            return row;
        }

        void ApplyKeyCaps()
        {
            foreach (var (cap, arrows, wasd) in _keyCaps)
                cap.text = _wasdLayout ? wasd : arrows;
        }

        void SetKeyLayout(bool wasd)
        {
            if (_wasdLayout == wasd)
                return;
            _wasdLayout = wasd;
            ApplyKeyCaps();
        }

        void Update()
        {
            HandleKeys();
            UpdateKeyRing();
        }

        void HandleKeys()
        {
            if (_docRoot == null || _root == null || _docRoot.style.display == DisplayStyle.None)
                return;
            // 글 입력 칸에 글을 치는 중에는 Esc만 받는다 (입력을 그만둔다)
            var typing = TypingField();
            var nav = ReadNav(typing != null);
            if (nav == Nav.None || Time.frameCount <= _keysBlockedFrame)
                return;
            if (typing != null)
            {
                typing.Blur();
                return;
            }
            bool modal = _modalLayer.style.display != DisplayStyle.None && _modalLayer.childCount > 0;
            if (_hubShown && _run != null && !modal)
            {
                // 허브: 올라가는 연출(건너뛰기), 덱 창, 명령 메뉴
                _navOn = false;
                if (_climbing)
                {
                    if (nav == Nav.Confirm || nav == Nav.Cancel)
                        SkipClimb();
                }
                else if (_deckOpen)
                    DeckKey(nav);
                else
                    HubKey(nav);
                return;
            }
            if (modal && DuelWindowOpen)
            {
                _navOn = false;
                DuelWindowKey(nav);
                return;
            }
            if (modal && RewardOpen)
            {
                _navOn = false;
                RewardKey(nav);
                return;
            }
            if (modal && LogOpen)
            {
                _navOn = false;
                LogKey(nav); // 런 기록 서랍
                return;
            }
            if (modal && CodexOpen)
            {
                _navOn = false;
                CodexKey(nav); // 이벤트 도감
                return;
            }
            if (!modal && EndKeysOpen)
            {
                _navOn = false;
                EndKey(nav); // 런 종료 화면의 버튼 줄
                return;
            }
            if (modal && PickOpen)
            {
                // 카드 고르기 창 (봉인 해제, 보충, 칩교환소의 카드 고르기)
                _navOn = false;
                PickKey(nav);
                return;
            }
            if (modal && WindowKeysOpen)
            {
                // 제 목록이나 버튼 줄이 있는 창 (이벤트, 칩교환소, 휴식 등)
                _navOn = false;
                WindowKey(nav);
                return;
            }
            // 그 밖의 창과 화면(런 시작, 덱 편집, 런 종료, 코인 토스 등): 버튼과 카드를 이동 키로 고른다
            ScopeKey(modal ? _modalLayer[0] : _root, nav);
        }

        // ================= 창과 화면의 공통 키보드 =================
        // 따로 키보드를 짜지 않은 창과 화면은 "그 안의 켜진 버튼과 카드"를 이동 키로 옮겨 다니고 결정 키로 누른다.
        // 가리키는 것에는 금색 테(run-key-ring)를 두른다. 화면을 마우스로 누르면 풀린다.

        /// <summary>닫기·뒤로·취소에 해당하는 버튼 이름 (취소 키가 누른다). 앞의 것이 먼저다.</summary>
        static readonly string[] CancelButtons =
        {
            "shop-back", "shop-close", "close-log", "event-close", "rest-close", "coin-close", "recipe-save-cancel",
            "event-codex-close", "editor-leave-cancel", "start-cancel", "editor-back",
        };

        bool _navOn;
        VisualElement _navScope, _keyLayer, _keyRing;
        int _navIndex;

        /// <summary>금색 테를 그리는 층 (화면 맨 위, 마우스는 통과). 유니티의 기본 키보드 이동도 여기서 막는다.</summary>
        void BuildKeyLayer()
        {
            _keyLayer = new VisualElement { name = "run-key-layer", pickingMode = PickingMode.Ignore };
            _keyLayer.style.position = Position.Absolute;
            _keyLayer.style.left = _keyLayer.style.top = _keyLayer.style.right = _keyLayer.style.bottom = 0;
            _keyRing = new VisualElement { name = "run-key-ring", pickingMode = PickingMode.Ignore };
            _keyRing.style.position = Position.Absolute;
            UiStyle.BorderColor(_keyRing, UiStyle.Gold, 4);
            UiStyle.Radius(_keyRing, 10);
            _keyRing.style.display = DisplayStyle.None;
            _keyLayer.Add(_keyRing);
            _docRoot.Add(_keyLayer);
            // 방향키·Tab으로 포커스가 버튼을 옮겨 다니지 않게 한다 (같은 키가 두 번 먹지 않게. 키보드는 HandleKeys가 직접 읽는다)
            _docRoot.RegisterCallback<NavigationMoveEvent>(e =>
            {
                _docRoot.focusController?.IgnoreEvent(e);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            _docRoot.RegisterCallback<PointerDownEvent>(_ => _navOn = false, TrickleDown.TrickleDown);
        }

        /// <summary>글을 치는 중인 입력 칸 (없으면 null).</summary>
        TextField TypingField()
        {
            var focused = _docRoot.focusController?.focusedElement as VisualElement;
            return focused == null ? null : focused as TextField ?? focused.GetFirstAncestorOfType<TextField>();
        }

        /// <summary>그 안에서 고를 수 있는 것: 켜져 있고 화면에 보이는 버튼과 카드 (화면 순서).</summary>
        List<VisualElement> NavItems(VisualElement scope)
        {
            var cards = new HashSet<VisualElement>();
            foreach (var (_, _, slot) in _cardViews)
                cards.Add(slot);
            return scope.Query<VisualElement>().Where(e =>
            {
                if (!(e is Button) && !cards.Contains(e))
                    return false;
                if (!e.enabledInHierarchy || e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden)
                    return false;
                var bound = e.worldBound;
                return !float.IsNaN(bound.width) && bound.width > 1 && bound.height > 1;
            }).ToList();
        }

        void ScopeKey(VisualElement scope, Nav nav)
        {
            if (scope == null)
                return;
            if (nav == Nav.Cancel)
            {
                foreach (string name in CancelButtons)
                {
                    var button = scope.Q<Button>(name);
                    if (button != null && button.enabledInHierarchy)
                    {
                        Press(button);
                        return;
                    }
                }
                return;
            }
            var items = NavItems(scope);
            if (items.Count == 0)
                return;
            if (scope != _navScope)
            {
                // 새 창·화면이면 첫 항목부터. 버튼이 포커스를 받지 않게 한다 (엔터·스페이스가 포커스된 버튼에도 먹는 것을 막는다)
                _navScope = scope;
                _navIndex = 0;
                scope.Query<Button>().ForEach(b => b.focusable = false);
            }
            _navIndex = Mathf.Clamp(_navIndex, 0, items.Count - 1);
            if (!_navOn)
            {
                // 첫 키는 금색 테를 켜기만 한다 (무엇을 가리키는지 보고 나서 누르게)
                _navOn = true;
                return;
            }
            if (nav == Nav.Confirm)
            {
                Press(items[_navIndex]);
                return;
            }
            Vector2 direction = nav == Nav.Left ? Vector2.left : nav == Nav.Right ? Vector2.right
                : nav == Nav.Up ? Vector2.down : nav == Nav.Down ? Vector2.up : Vector2.zero; // 화면은 아래가 +y
            if (direction == Vector2.zero)
                return;
            Vector2 from = items[_navIndex].worldBound.center;
            int best = -1;
            float bestCost = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                if (i == _navIndex)
                    continue;
                Vector2 d = items[i].worldBound.center - from;
                float along = Vector2.Dot(d, direction);
                if (along < 4f)
                    continue;
                float cost = along + (d - along * direction).magnitude * 2.5f;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }
            if (best < 0)
                return;
            _navIndex = best;
            items[best].GetFirstAncestorOfType<ScrollView>()?.ScrollTo(items[best]);
        }

        /// <summary>마우스로 누른 것과 같게 누른다 (버튼, 카드).</summary>
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

        /// <summary>프레임마다: 키보드로 가리키는 버튼·카드에 금색 테를 두른다.</summary>
        void UpdateKeyRing()
        {
            if (_keyRing == null)
                return;
            VisualElement target = null;
            if (_navOn && _navScope != null && _navScope.panel != null && _docRoot.style.display != DisplayStyle.None)
            {
                bool modal = _modalLayer.style.display != DisplayStyle.None && _modalLayer.childCount > 0;
                // 가리키던 창·화면이 그대로일 때만 (창이 닫히거나 바뀌면 다음 키에서 새로 잡는다)
                if (_navScope == (modal ? _modalLayer[0] : _root))
                {
                    var items = NavItems(_navScope);
                    if (items.Count > 0)
                        target = items[Mathf.Clamp(_navIndex, 0, items.Count - 1)];
                }
            }
            if (target == null)
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

        void HubKey(Nav nav)
        {
            if (_hubMenu == null)
                return;
            switch (nav)
            {
                case Nav.Up:
                    _hubMenu.Move(-1);
                    _hubMenuIndex = _hubMenu.Index;
                    break;
                case Nav.Down:
                    _hubMenu.Move(1);
                    _hubMenuIndex = _hubMenu.Index;
                    break;
                case Nav.Confirm:
                    _hubMenuIndex = _hubMenu.Index;
                    _hubMenu.Activate();
                    break;
            }
        }

        void DuelWindowKey(Nav nav)
        {
            switch (nav)
            {
                case Nav.Left:
                case Nav.Right:
                    SetDuelChoice(1 - _duelChoice);
                    break;
                case Nav.Confirm:
                    if (_duelChoice == 0)
                    {
                        if (_duelStart != null && _duelStart.enabledSelf)
                            StartNextDuel();
                    }
                    else
                        OpenDeck(_deckTab == Pile.Sealed ? Pile.Main : _deckTab, fromDuel: true);
                    break;
                case Nav.Cancel:
                    CloseModal(); // 나중에: 메뉴의 "듀얼"로 다시 연다
                    break;
            }
        }

        /// <summary>
        /// 이번 프레임에 들어온 조작 하나. 이동 키는 누르고 있으면 되풀이된다 (0.38초 뒤부터 0.09초마다).
        /// </summary>
        /// <param name="typing">글 입력 칸에 글을 치는 중: Esc(취소)만 받는다.</param>
        Nav ReadNav(bool typing)
        {
#if ENABLE_INPUT_SYSTEM // 유니티 밖(헤드리스 컴파일)에는 Input System이 없다
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
                    SetKeyLayout(wasd);
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
                SetKeyLayout(false);
                return Nav.Confirm;
            }
            if (keyboard.jKey.wasPressedThisFrame)
            {
                SetKeyLayout(true);
                return Nav.Confirm;
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                return Nav.Confirm;
            if (keyboard.xKey.wasPressedThisFrame)
            {
                SetKeyLayout(false);
                return Nav.Cancel;
            }
            if (keyboard.kKey.wasPressedThisFrame)
            {
                SetKeyLayout(true);
                return Nav.Cancel;
            }
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                return Nav.Cancel;
            if (keyboard.qKey.wasPressedThisFrame)
                return Nav.PrevTab;
            if (keyboard.eKey.wasPressedThisFrame)
                return Nav.NextTab;
#endif
            return Nav.None;
        }
    }
}
