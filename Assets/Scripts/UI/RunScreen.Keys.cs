// 런 화면의 키보드. docs/UI.md "키보드".
// 기본 자리 두 가지를 언제나 함께 받는다: 방향키 + Z(결정)·X(취소), W A S D + J(결정)·K(취소).
// Enter·Space는 결정, Esc·Backspace는 취소로 함께 받는다. 글자 키는 한/영 상태와 상관없이 키의 자리로 읽는다.
// 화면의 키 표시는 마지막에 쓴 이동 키의 자리에 맞춘다.
// 지금 키보드를 받는 곳: 허브의 명령 메뉴, 올라가는 연출(건너뛰기), 듀얼 창, 덱 창. 나머지 창은 적용 5단계에서 붙인다.
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
        }

        void HandleKeys()
        {
            if (!_hubShown || _run == null || _docRoot == null || _docRoot.style.display == DisplayStyle.None)
                return;
            var nav = ReadNav();
            if (nav == Nav.None || Time.frameCount <= _keysBlockedFrame)
                return;
            if (_climbing)
            {
                if (nav == Nav.Confirm || nav == Nav.Cancel)
                    SkipClimb();
                return;
            }
            if (_modalLayer.style.display != DisplayStyle.None)
            {
                // 창이 떠 있다: 듀얼 창만 키보드를 받는다
                if (DuelWindowOpen)
                    DuelWindowKey(nav);
                return;
            }
            if (_deckOpen)
                DeckKey(nav);
            else
                HubKey(nav);
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
        Nav ReadNav()
        {
#if ENABLE_INPUT_SYSTEM // 유니티 밖(헤드리스 컴파일)에는 Input System이 없다
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return Nav.None;
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
