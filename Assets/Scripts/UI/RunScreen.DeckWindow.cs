// 런 화면: 덱 창 (run-decks). docs/UI.md "덱 창". RunScreen.cs의 머리말 참고.
// 허브의 "덱"·"봉인"이나 듀얼 창의 "덱 조정"으로 연다. 탑 장면을 어둡게 덮고 그 위에 돌판을 띄운다. 위 막대는 그대로 보인다.
// - 위에 탭 (zone-main, zone-extra, zone-side, zone-sealed, zone-shop): 메인, 엑스트라, 사이드, 봉인, 상점 덱. 탭마다 장수.
// - 왼쪽에 고른 탭의 카드 격자 (한 줄 10장, 카드 1.1배), 오른쪽에 카드 정보 (run-card-detail: 상세 카드, 장수, 옮기기 버튼).
// - 옮기기: 카드를 끌어 다른 탭에 놓기, 두 번 누르기, 카드 정보의 버튼. 옮길 수 없으면 그 까닭을 아래 줄(move-notice)에 적는다.
// - 키보드: 이동 키로 카드 고르기 (위아래는 한 줄씩), 결정 키로 카드 정보의 버튼 고르기, Q·E로 탭 바꾸기, 취소 키로 닫기.
// 듀얼 창에서 열었으면 닫을 때 듀얼 창으로 돌아간다.
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
        const float DeckCardWidth = 119;
        const int DeckColumns = 10;

        bool _deckOpen, _deckFromDuel;
        Pile _deckTab = Pile.Main;
        /// <summary>키보드로 고른 카드의 자리 (격자 순서). -1이면 아직 키보드로 고르지 않았다.</summary>
        int _deckCursor = -1;
        /// <summary>카드 정보의 버튼을 키보드로 고르는 중인지, 그중 몇 번째인지.</summary>
        bool _deckActions;
        int _deckActionIndex;
        readonly List<(uint Code, VisualElement Slot)> _deckSlots = new List<(uint, VisualElement)>();
        readonly List<Button> _deckActionButtons = new List<Button>();

        void OpenDeck(Pile tab, bool fromDuel = false)
        {
            if (_run == null || _climbing)
                return;
            _deckOpen = true;
            _deckFromDuel = fromDuel;
            SetDeckTab(tab);
        }

        void SetDeckTab(Pile tab)
        {
            _deckTab = tab;
            _deckCursor = -1;
            _deckActions = false;
            _deckScroll = null;
            _deckScrollOffset = Vector2.zero;
            ShowHub();
        }

        /// <summary>덱 창을 닫는다. 듀얼 창에서 열었으면 듀얼 창으로 돌아간다.</summary>
        void CloseDeck()
        {
            if (!_deckOpen)
                return;
            bool back = _deckFromDuel;
            _deckOpen = _deckFromDuel = false;
            _deckActions = false;
            _deckScroll = null;
            ShowHub();
            if (back)
                ReopenFloorWindow();
        }

        /// <summary>창에서 연 덱 창을 닫았을 때 그 창으로 돌아간다 (듀얼 창, 휴식 창).</summary>
        void ReopenFloorWindow()
        {
            if (_run != null && _run.Phase == RunPhase.Ready && _run.FloorKind == FloorKind.Rest)
                OpenRest();
            else
                OpenDuelWindow();
        }

        VisualElement DeckWindow()
        {
            _deckSlots.Clear();
            _deckActionButtons.Clear();
            var layer = new VisualElement { name = "deck-layer" };
            UiStyle.Place(layer, 0, TopBarHeight, StageWidth, StageHeight - TopBarHeight);
            // 선형 색 공간에서는 어두운 덮개가 옅게 보여서 문서의 70%보다 진하게 한다
            layer.style.backgroundColor = new Color(10 / 255f, 8 / 255f, 6 / 255f, 0.84f);

            var window = UiStyle.PanelBox("run-decks");
            UiStyle.Place(window, 100, 92 - TopBarHeight, 1720, 960);
            window.style.paddingLeft = window.style.paddingRight = 24;
            window.style.paddingTop = 20;
            window.style.paddingBottom = 12;
            layer.Add(window);

            // ---- 탭 ----
            var tabs = Row();
            tabs.style.alignItems = Align.Center;
            tabs.style.flexShrink = 0;
            foreach (Pile pile in HubPiles)
                tabs.Add(DeckTab(pile));
            var space = new VisualElement();
            space.style.flexGrow = 1;
            tabs.Add(space);
            var saveRecipe = UiStyle.StoneButton("지금 덱을 레시피로", OpenSaveRecipe, small: true);
            saveRecipe.name = "save-recipe";
            saveRecipe.style.marginRight = 8;
            saveRecipe.SetEnabled(_run.Phase == RunPhase.Ready);
            tabs.Add(saveRecipe);
            var close = UiStyle.StoneButton("닫기", CloseDeck, small: true);
            close.name = "close-deck";
            close.style.width = 112;
            close.style.paddingLeft = 14;
            close.style.paddingRight = 44;
            close.style.unityTextAlign = TextAnchor.MiddleLeft;
            var closeKey = KeyCap("X", "K");
            closeKey.pickingMode = PickingMode.Ignore;
            closeKey.style.position = Position.Absolute;
            closeKey.style.right = 9;
            closeKey.style.top = 8;
            close.Add(closeKey);
            tabs.Add(close);
            window.Add(tabs);

            var info = Text(DeckTabInfo(_deckTab), 17, UiStyle.TextDim, top: 10);
            info.name = "deck-info";
            info.style.whiteSpace = WhiteSpace.NoWrap;
            info.style.marginBottom = 8;
            info.style.flexShrink = 0;
            window.Add(info);

            // ---- 카드 격자와 카드 정보 ----
            var body = Row();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.overflow = Overflow.Hidden;
            _deckScroll = new ScrollView(ScrollViewMode.Vertical);
            _deckScroll.style.width = DeckColumns * (DeckCardWidth + 12) + 26; // 세로 스크롤 막대 자리
            _deckScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _deckScroll.style.flexShrink = 0;
            var grid = Grid();
            grid.style.width = DeckColumns * (DeckCardWidth + 12);
            _deckScroll.Add(grid);
            AddDeckCards(grid, _deckTab);
            body.Add(_deckScroll);

            var detail = UiStyle.Box("run-card-detail");
            detail.style.width = 324;
            detail.style.flexShrink = 0;
            detail.style.marginLeft = StyleKeyword.Auto;
            body.Add(detail);
            window.Add(body);

            // ---- 아래 줄: 알림과 도움말 ----
            var foot = Row();
            foot.style.alignItems = Align.Center;
            foot.style.height = 38;
            foot.style.flexShrink = 0;
            foot.style.marginTop = 6;
            if (_deckFromDuel)
            {
                var from = UiStyle.Label(_run.FloorKind == FloorKind.Rest ? "덱 정리 중. 닫으면 휴식 창으로 돌아간다." : "듀얼 전 덱 조정 중. 닫으면 듀얼 창으로 돌아간다.", 17, UiStyle.Gold, bold: true);
                from.name = "deck-from-duel";
                from.style.whiteSpace = WhiteSpace.NoWrap;
                from.style.marginRight = 18;
                foot.Add(from);
            }
            var notice = NoticeLabel(17);
            notice.style.whiteSpace = WhiteSpace.NoWrap;
            notice.style.overflow = Overflow.Hidden;
            notice.style.flexShrink = 1;
            notice.style.marginRight = 18;
            foot.Add(notice);
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            foot.Add(gap);
            var hints = Hints(("← ↑ ↓ →", "W A S D", "카드 고르기"), ("Z", "J", "옮기기·적용"), ("Q", "Q", null), ("E", "E", "탭 바꾸기"));
            hints.style.flexShrink = 0;
            foot.Add(hints);
            var drag = UiStyle.Label("마우스: 두 번 누르거나 다른 탭에 끌어다 놓으면 옮긴다", 17, UiStyle.TextDim);
            drag.style.whiteSpace = WhiteSpace.NoWrap;
            drag.style.flexShrink = 0;
            foot.Add(drag);
            window.Add(foot);

            _detail = detail;
            _renderDetail = RenderDeckDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
            MarkDeckCursor();

            var scroll = _deckScroll;
            var offset = _deckScrollOffset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).ExecuteLater(10);
            return layer;
        }

        static readonly Color TabGoldEdge = UiStyle.Hex(0x5f4519);

        /// <summary>탭 하나. 메인·엑스트라·사이드 탭은 카드를 끌어다 놓는 곳이기도 하다.</summary>
        Button DeckTab(Pile pile)
        {
            bool on = pile == _deckTab;
            var tab = new Button(UiStyle.Clicked(() => SetDeckTab(pile))) { name = $"zone-{pile.ToString().ToLowerInvariant()}", text = "", focusable = false };
            tab.style.height = 52;
            tab.style.flexShrink = 0;
            tab.style.flexDirection = FlexDirection.Row;
            tab.style.alignItems = Align.Center;
            UiStyle.Margin(tab, 0);
            tab.style.marginRight = 6;
            tab.style.paddingTop = tab.style.paddingBottom = 0;
            tab.style.paddingLeft = tab.style.paddingRight = 20;
            tab.style.backgroundColor = on ? UiStyle.Gold : UiStyle.Stone7;
            UiStyle.Radius(tab, 6);
            UiStyle.BorderColor(tab, on ? TabGoldEdge : UiStyle.Stone9, 2);
            tab.style.borderBottomWidth = 5;
            tab.EnableInClassList("selected", on);
            var label = UiStyle.Label(DeckTabText(pile), 23, on ? GoldInk : UiStyle.TextDim, bold: true);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.pickingMode = PickingMode.Ignore;
            tab.Add(label);
            if (pile == Pile.Main || pile == Pile.Extra)
                AddDropZone(tab, (from, code) => TryMove(from, code, toDeck: true));
            else if (pile == Pile.Side)
                AddDropZone(tab, (from, code) => TryMove(from, code, toDeck: false));
            return tab;
        }

        string DeckTabText(Pile pile)
        {
            switch (pile)
            {
                case Pile.Main: return $"메인 {_run.MainDeck.Count}";
                case Pile.Extra: return $"엑스트라 {_run.ExtraDeck.Count}";
                case Pile.Side: return $"사이드 {_run.SideDeck.Count}";
                case Pile.Sealed: return $"봉인 {_run.SealedMain.Count + _run.SealedExtra.Count}";
                default: return $"상점 덱 {_run.ShopDeck.Count}/{RunRules.MaxShopDeck}";
            }
        }

        string DeckTabInfo(Pile pile)
        {
            switch (pile)
            {
                case Pile.Main:
                    return $"메인 덱 {_run.MainDeck.Count}장 · {KindCounts(_run.MainDeck)} · {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장 · 같은 카드명은 덱에 {RunRules.MaxCopies}장까지";
                case Pile.Extra:
                    return $"엑스트라 덱 {_run.ExtraDeck.Count}장 · 최대 {RunRules.MaxExtraDeck}장";
                case Pile.Side:
                    return $"사이드 덱 {_run.SideDeck.Count}장 · 승리 보상이 들어온다. 듀얼 사이에 메인·엑스트라 덱의 카드와 바꿀 수 있다";
                case Pile.Sealed:
                    return $"봉인 {_run.SealedMain.Count + _run.SealedExtra.Count}장 · 이번 런에서는 쓸 수 없다. 다음 런에서는 다시 쓸 수 있다";
                default:
                    return $"상점 덱 {_run.ShopDeck.Count}/{RunRules.MaxShopDeck}장 · 칩교환소에서 산 카드. 듀얼에 쓰지 않고 아이템 효과만 낸다. 런이 끝나면 사라진다";
            }
        }

        /// <summary>고른 탭의 카드를 격자에 늘어놓는다. 같은 카드도 한 장씩 따로 놓는다 (몬스터 레벨 높은 순 → 마법 → 함정, 그 안에서 카드명 순).</summary>
        void AddDeckCards(VisualElement grid, Pile pile)
        {
            var cards = Cards(pile);
            if (cards.Count == 0)
            {
                grid.Add(Text(pile == Pile.Side ? "비어 있음 (메인·엑스트라 덱의 카드를 사이드 탭에 끌어다 놓으면 뺀다)" : "비어 있음", 19, UiStyle.TextDim, top: 8));
                return;
            }
            // 사이드 덱: 직전 듀얼에서 얻은 카드에 "새 카드" 표시
            var fresh = new Dictionary<uint, int>();
            if (pile == Pile.Side)
                foreach (var e in _run.Events.Where(e => e.Kind == RunEventKind.RewardGained && e.DuelNumber == _run.DuelsPlayed))
                    fresh[e.Code] = fresh.TryGetValue(e.Code, out int n) ? n + 1 : 1;

            foreach (uint code in Sorted(cards))
            {
                uint c = code;
                string tag = null;
                if (fresh.TryGetValue(c, out int left) && left > 0)
                {
                    tag = "새 카드";
                    fresh[c] = left - 1;
                }
                if (tag == null && (pile == Pile.Main || pile == Pile.Shop) && _run.IsApplied(pile == Pile.Main ? RunPile.Main : RunPile.Shop, c))
                    tag = pile == Pile.Main && _run.IsBound(c) ? "귀속" : "적용 중";
                bool isSealed = pile == Pile.Sealed;
                bool fixedCard = isSealed || pile == Pile.Shop;
                Action onDouble = fixedCard ? (Action)null : () => TryMove(pile, c, toDeck: pile == Pile.Side);
                var slot = CardSlot(pile, c, CardFaceSize.Small, tag: tag, dim: isSealed, onDoubleClick: onDouble, draggable: !fixedCard, width: DeckCardWidth);
                slot.focusable = false; // 키보드는 화면이 직접 읽는다
                UiStyle.Margin(slot, 2);
                grid.Add(slot);
                _deckSlots.Add((c, slot));
            }
        }

        void RenderDeckDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            _deckActionButtons.Clear();
            if (!pinned)
                _deckActions = false;
            if (code == 0)
            {
                panel.Add(Text("카드에 마우스를 올리면 여기에 크게 보이고, 누르면 옮기기 버튼이 나온다.", 19, UiStyle.TextDim));
                panel.Add(Text($"메인 덱 {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장, 엑스트라 덱 {RunRules.MaxExtraDeck}장 이하, " +
                               $"같은 카드명은 덱에 {RunRules.MaxCopies}장까지.", 17, UiStyle.TextDim, top: 10));
                return;
            }
            panel.Add(_faces.BuildDetail(code, 324, 500));
            int side = _run.SideDeck.Count(c => c == code);
            int sealedCount = _run.SealedMain.Count(c => c == code) + _run.SealedExtra.Count(c => c == code);
            int shop = _run.ShopDeck.Count(c => c == code);
            var counts = Text($"덱 {_run.DeckCopies(code)}장(같은 카드명) · 사이드 덱 {side}장 · 봉인 {sealedCount}장" + (shop > 0 ? $" · 상점 덱 {shop}장" : "") + $" · 컬렉션 {_collection.Count(code)}장",
                15, UiStyle.TextDim, top: 6);
            counts.name = "card-counts";
            panel.Add(counts);
            var item = _run.ItemOf(code);
            if (item != null)
            {
                string State(RunPile where) => _run.IsApplied(where, code) ? (where == RunPile.Main && _run.IsBound(code) ? "적용 중 (귀속)" : "적용 중") : "적용 안 함";
                var itemText = Text($"아이템 카드 (탑 효과): {item.Describe()}\n메인 덱 {State(RunPile.Main)} · 상점 덱 {State(RunPile.Shop)}", 15, UiStyle.Accent, top: 6);
                itemText.name = "item-info";
                panel.Add(itemText);
            }
            if (!pinned)
                return;

            var actions = UiStyle.Box();
            actions.style.marginTop = 8;
            if (item != null && (pile == Pile.Main || pile == Pile.Shop))
            {
                var where = pile == Pile.Main ? RunPile.Main : RunPile.Shop;
                bool can = _run.CanApplyItem(code, where, out string why);
                var apply = DeckAction("apply-item", _run.IsApplied(where, code) ? "아이템 효과 적용 중" : $"아이템 효과 적용 ({RunEvent.PileName(where)})", () => ApplyItem(code, where), can);
                actions.Add(apply);
                if (!can && !_run.IsApplied(where, code))
                    actions.Add(Text(why, 15, UiStyle.TextDim, top: 2));
                else if (can && where == RunPile.Main && item.Bound)
                    actions.Add(Text("귀속: 메인 덱에서 적용하면 봉인될 때까지 사이드 덱으로 뺄 수 없다.", 15, UiStyle.Opponent, top: 2));
            }
            switch (pile)
            {
                case Pile.Shop:
                    actions.Add(Text("상점 덱의 카드는 듀얼·보충에 쓰지 않고 옮길 수 없다. 런이 끝나면 사라진다.", 15, UiStyle.TextDim, top: 4));
                    break;
                case Pile.Main:
                case Pile.Extra:
                {
                    bool ok = _run.CanMoveToSide(code, out string reason);
                    actions.Add(DeckAction("move-to-side", "사이드 덱으로 빼기", () => TryMove(pile, code, toDeck: false), ok));
                    if (!ok)
                        actions.Add(Text(reason, 15, UiStyle.TextDim, top: 2));
                    break;
                }
                case Pile.Side:
                {
                    bool ok = _run.CanMoveToDeck(code, out string reason);
                    actions.Add(DeckAction("move-to-deck", _run.IsExtraDeckCard(code) ? "엑스트라 덱에 넣기" : "메인 덱에 넣기", () => TryMove(pile, code, toDeck: true), ok));
                    if (!ok)
                        actions.Add(Text(reason, 15, UiStyle.TextDim, top: 2));
                    break;
                }
                default:
                    actions.Add(Text("봉인된 카드는 이번 런에서 쓸 수 없다. 다음 런에서는 다시 쓸 수 있다.", 15, UiStyle.TextDim, top: 4));
                    break;
            }
            panel.Add(actions);
            MarkDeckActions();
        }

        /// <summary>카드 정보의 버튼 하나 (키보드로 고르면 왼쪽에 ▶가 붙는다).</summary>
        Button DeckAction(string name, string text, Action run, bool enabled)
        {
            var button = UiStyle.StoneButton(text, run, small: true);
            button.name = name;
            button.style.marginTop = 6;
            button.SetEnabled(enabled);
            var mark = UiStyle.Label("▶", 15, Color.clear);
            mark.name = "choice-mark";
            mark.pickingMode = PickingMode.Ignore;
            UiStyle.Place(mark, 10, 12);
            button.Add(mark);
            _deckActionButtons.Add(button);
            return button;
        }

        void MarkDeckActions()
        {
            for (int i = 0; i < _deckActionButtons.Count; i++)
                _deckActionButtons[i].Q<Label>("choice-mark").style.color = _deckActions && i == _deckActionIndex ? UiStyle.Gold : Color.clear;
        }

        /// <summary>키보드로 고른 카드를 조금 키워 보여 준다. 고른 카드가 이 탭에 없으면 (방금 옮긴 카드) 표시하지 않는다.</summary>
        void MarkDeckCursor()
        {
            if (_deckCursor >= _deckSlots.Count)
                _deckCursor = _deckSlots.Count - 1;
            bool show = _deckCursor >= 0 && _selectedPile == _deckTab && _deckSlots[_deckCursor].Code == _selectedCode;
            for (int i = 0; i < _deckSlots.Count; i++)
            {
                bool on = show && i == _deckCursor;
                _deckSlots[i].Slot.style.scale = on ? new Scale(new Vector3(1.07f, 1.07f, 1f)) : new Scale(Vector3.one);
            }
        }

        // ---- 키보드 ----

        void DeckKey(Nav nav)
        {
            switch (nav)
            {
                case Nav.Cancel:
                    if (_deckActions)
                    {
                        _deckActions = false;
                        MarkDeckActions();
                    }
                    else
                        CloseDeck();
                    return;
                case Nav.PrevTab:
                case Nav.NextTab:
                {
                    int i = Array.IndexOf(HubPiles, _deckTab);
                    SetDeckTab(HubPiles[(i + (nav == Nav.NextTab ? 1 : HubPiles.Length - 1)) % HubPiles.Length]);
                    return;
                }
                case Nav.Confirm:
                    if (_deckActions)
                    {
                        if (_deckActionIndex < _deckActionButtons.Count && _deckActionButtons[_deckActionIndex].enabledSelf)
                        {
                            var chosen = _deckActionButtons[_deckActionIndex];
                            _deckActions = false;
                            using (var submit = NavigationSubmitEvent.GetPooled())
                            {
                                submit.target = chosen;
                                chosen.SendEvent(submit);
                            }
                        }
                        return;
                    }
                    if (_deckCursor < 0 && _selectedCode == 0)
                    {
                        MoveDeckCursor(0);
                        return;
                    }
                    int first = _deckActionButtons.FindIndex(b => b.enabledSelf);
                    if (first < 0)
                    {
                        if (_selectedCode != 0)
                            SetMoveNotice($"{_text.CardName(_selectedCode)}: 지금 할 수 있는 일이 없다.", error: true);
                        return;
                    }
                    _deckActions = true;
                    _deckActionIndex = first;
                    MarkDeckActions();
                    return;
            }
            if (_deckActions)
            {
                // 버튼 고르기: 위아래로, 꺼진 버튼은 건너뛴다
                int step = nav == Nav.Up ? -1 : nav == Nav.Down ? 1 : 0;
                if (step == 0 || _deckActionButtons.Count == 0)
                    return;
                int i = _deckActionIndex;
                for (int n = 0; n < _deckActionButtons.Count; n++)
                {
                    i = (i + step + _deckActionButtons.Count) % _deckActionButtons.Count;
                    if (_deckActionButtons[i].enabledSelf)
                        break;
                }
                _deckActionIndex = i;
                MarkDeckActions();
                return;
            }
            MoveDeckCursor(nav == Nav.Left ? -1 : nav == Nav.Right ? 1 : nav == Nav.Up ? -DeckColumns : nav == Nav.Down ? DeckColumns : 0);
        }

        /// <summary>격자에서 카드를 고른다. 위아래는 한 줄(10장)씩. 끝을 넘어가면 그 줄에 머문다.</summary>
        void MoveDeckCursor(int delta)
        {
            if (_deckSlots.Count == 0)
                return;
            int current = _deckCursor;
            if (current < 0)
            {
                // 처음: 마우스로 고른 카드가 이 탭에 있으면 거기서, 없으면 첫 카드에서 시작한다
                current = _selectedPile == _deckTab ? _deckSlots.FindIndex(s => s.Code == _selectedCode) : -1;
                if (current < 0)
                {
                    current = 0;
                    delta = 0;
                }
            }
            int target = current + delta;
            if (target < 0 || target >= _deckSlots.Count)
            {
                // 마지막 줄이 덜 찼으면 아래로 갈 때 마지막 카드로 간다
                bool lastRowBelow = delta == DeckColumns && current / DeckColumns < (_deckSlots.Count - 1) / DeckColumns;
                target = lastRowBelow ? _deckSlots.Count - 1 : current;
            }
            _deckCursor = target;
            var (code, slot) = _deckSlots[target];
            Select(_deckTab, code);
            MarkDeckCursor();
            _deckScroll?.ScrollTo(slot);
        }
    }
}
