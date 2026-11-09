// 카드 하나의 마우스 조작: 올리기(미리 보기), 누르기(고르기), 두 번 누르기, 끌어다 놓기.
// 제출 이벤트(NavigationSubmitEvent: 키보드 Enter, 플레이 모드 시험)도 누르기로 친다.
// 끌기: 누른 채 8px 넘게 움직이면 카드 그림(ghost)이 마우스를 따라오고, 놓은 위치(패널 좌표)를 onDrop에 넘긴다.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed class CardInput
    {
        const float DragThreshold = 8;

        readonly VisualElement _target;
        readonly VisualElement _dragLayer;
        readonly Action _onHover, _onLeave, _onClick, _onDoubleClick;
        readonly Func<VisualElement> _makeGhost;
        readonly Action<Vector2> _onDrop;

        bool _down, _dragging;
        int _clickCount;
        Vector2 _start, _grabOffset;
        VisualElement _ghost;

        CardInput(VisualElement target, VisualElement dragLayer, Action onHover, Action onLeave, Action onClick, Action onDoubleClick,
            Func<VisualElement> makeGhost, Action<Vector2> onDrop)
        {
            _target = target;
            _dragLayer = dragLayer;
            _onHover = onHover;
            _onLeave = onLeave;
            _onClick = onClick;
            _onDoubleClick = onDoubleClick;
            _makeGhost = makeGhost;
            _onDrop = onDrop;
            target.RegisterCallback<PointerEnterEvent>(_ => { if (!_dragging) _onHover?.Invoke(); });
            target.RegisterCallback<PointerLeaveEvent>(_ => { if (!_down) _onLeave?.Invoke(); });
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag(null));
            target.RegisterCallback<NavigationSubmitEvent>(e =>
            {
                _onClick?.Invoke();
                e.StopPropagation();
            });
        }

        /// <param name="dragLayer">끌 때 카드 그림을 올릴 곳 (화면 루트). null이거나 makeGhost가 null이면 끌 수 없다.</param>
        /// <param name="onDoubleClick">두 번 눌렀을 때. null이면 누르기와 같다.</param>
        /// <param name="onDrop">놓은 위치 (패널 좌표).</param>
        public static void Attach(VisualElement target, VisualElement dragLayer, Action onHover, Action onLeave, Action onClick,
            Action onDoubleClick = null, Func<VisualElement> makeGhost = null, Action<Vector2> onDrop = null)
        {
            new CardInput(target, dragLayer, onHover, onLeave, onClick, onDoubleClick, makeGhost, onDrop);
        }

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0)
                return;
            _down = true;
            _clickCount = e.clickCount;
            _start = e.position;
            _grabOffset = _target.WorldToLocal(e.position);
            _target.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!_down || !_target.HasPointerCapture(e.pointerId))
                return;
            if (!_dragging)
            {
                if (_makeGhost == null || _dragLayer == null || ((Vector2)e.position - _start).magnitude < DragThreshold)
                    return;
                _dragging = true;
                _ghost = _makeGhost();
                _ghost.pickingMode = PickingMode.Ignore;
                _ghost.style.position = Position.Absolute;
                _ghost.style.opacity = 0.85f;
                _dragLayer.Add(_ghost);
                _target.style.opacity = 0.35f;
            }
            var local = _dragLayer.WorldToLocal(e.position);
            _ghost.style.left = local.x - _grabOffset.x;
            _ghost.style.top = local.y - _grabOffset.y;
            e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (!_down || e.button != 0)
                return;
            bool dragged = _dragging;
            Vector2 position = e.position;
            if (_target.HasPointerCapture(e.pointerId))
                _target.ReleasePointer(e.pointerId);
            EndDrag(null);
            _down = false;
            e.StopPropagation();
            if (dragged)
                _onDrop?.Invoke(position);
            else if (_clickCount >= 2 && _onDoubleClick != null)
                _onDoubleClick();
            else
                _onClick?.Invoke();
        }

        void EndDrag(object _)
        {
            if (_ghost != null)
            {
                _ghost.RemoveFromHierarchy();
                _ghost = null;
            }
            if (_dragging)
                _target.style.opacity = StyleKeyword.Null;
            _dragging = false;
        }
    }
}
