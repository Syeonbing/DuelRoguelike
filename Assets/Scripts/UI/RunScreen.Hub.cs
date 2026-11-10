// 런 화면: 층 사이 화면과 보충 고르기 창 (승리 보상 고르기 창은 RunScreen.Reward.cs). RunScreen.cs의 머리말 참고.
// 배치: 위 상태 줄(run-status) / 왼쪽 카드 정보(run-card-detail) / 가운데 덱(run-decks): 메인 덱·엑스트라 덱·사이드 덱·봉인.
// 덱의 카드는 같은 카드도 한 장씩 따로 놓는다 (몬스터 레벨 높은 순 → 마법 → 함정, 그 안에서 카드명 순).
// 옮기기: 덱 ↔ 사이드 덱 칸으로 끌어다 놓기, 두 번 누르기, 카드 정보의 버튼. 옮길 수 없으면 그 까닭을 move-notice에 적는다.
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
        static readonly Pile[] HubPiles = { Pile.Main, Pile.Extra, Pile.Side, Pile.Sealed };

        uint _refillSelected;
        ScrollView _deckScroll;
        Vector2 _deckScrollOffset;
        /// <summary>직전 옮기기의 결과 (move-notice). 듀얼을 시작하거나 런을 이어 하면 지운다.</summary>
        string _moveNotice;
        bool _moveNoticeIsError;

        void ShowHub()
        {
            if (_run.Phase == RunPhase.Ended)
            {
                ShowEnd();
                return;
            }
            if (_deckScroll != null)
                _deckScrollOffset = _deckScroll.scrollOffset;
            BeginScreen();
            FixSelection();

            _root.Add(HubTopBar());
            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;

            var detail = UiStyle.PanelBox("run-card-detail");
            detail.style.width = 380;
            detail.style.marginRight = 12;
            body.Add(detail);

            var center = UiStyle.Box();
            center.style.flexGrow = 1;
            var notice = Text(_moveNotice ?? "", 16, _moveNoticeIsError ? UiStyle.Opponent : Positive);
            notice.name = "move-notice";
            notice.style.marginBottom = 6;
            notice.style.display = _moveNotice == null ? DisplayStyle.None : DisplayStyle.Flex;
            center.Add(notice);
            center.Add(HubDeckPanel());
            body.Add(center);
            body.Add(TowerPanel());
            _root.Add(body);

            _detail = detail;
            _renderDetail = RenderHubDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);

            if (_run.Phase == RunPhase.ChoosingReward)
                OpenReward();
            else if (_run.Phase == RunPhase.ChoosingUnseal)
                OpenUnseal();
            else if (_run.Phase == RunPhase.NeedsRefill)
                OpenRefill();
            else if (_run.FloorKind == FloorKind.Event && _eventShownFloor != _run.Floor)
                OpenEvent(); // 이벤트 층에 처음 왔을 때 한 번
            else if (_run.FloorKind == FloorKind.Rest && _restShownFloor != _run.Floor)
                OpenRest(); // 휴식 층도 이벤트처럼 처음 왔을 때 한 번
        }

        VisualElement HubTopBar()
        {
            var bar = UiStyle.PanelBox("run-status");
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.alignItems = Align.Center;
            UiStyle.Padding(bar, 14);

            var texts = UiStyle.Box();
            texts.style.flexShrink = 1;
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            var floorLabel = UiStyle.Label($"{_run.Floor}층 · {Tower.KindName(_run.FloorKind)}", 34, UiStyle.Accent, bold: true);
            floorLabel.name = "floor-label";
            head.Add(floorLabel);
            string record = $"{_run.Wins}승 {_run.Losses}패" + (_run.Draws > 0 ? $" {_run.Draws}무" : "");
            var summary = Text($"LP {_run.Lp} · 칩 {_run.Chips} · 연패 {_run.LosingStreak} · {record} · 끝낸 듀얼 {_run.DuelsPlayed}", 19);
            summary.name = "run-summary";
            summary.style.marginLeft = 16;
            summary.style.marginBottom = 4;
            head.Add(summary);
            texts.Add(head);
            var last = Text(LastDuelLine(), 16, top: 4);
            last.name = "last-duel";
            last.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(last);
            texts.Add(Text($"시작 덱 {_run.StartDeckName} · 시드 {_run.Seed}", 14, UiStyle.TextDim, top: 2));
            bar.Add(texts);

            var right = UiStyle.Box();
            right.style.alignItems = Align.FlexEnd;
            right.style.flexShrink = 0;
            right.style.marginLeft = 16;
            var buttons = Row();
            var log = UiStyle.Button("런 로그", OpenLog, 16);
            log.name = "open-log";
            log.style.marginRight = 10;
            log.style.unityTextAlign = TextAnchor.MiddleCenter;
            buttons.Add(log);
            var saveRecipe = UiStyle.Button("지금 덱을 레시피로", OpenSaveRecipe, 16);
            saveRecipe.name = "save-recipe";
            saveRecipe.style.marginRight = 10;
            saveRecipe.style.unityTextAlign = TextAnchor.MiddleCenter;
            saveRecipe.SetEnabled(_run.Phase == RunPhase.Ready);
            buttons.Add(saveRecipe);
            var toTitle = UiStyle.Button("처음 화면으로", GoToStart, 16);
            toTitle.name = "to-title";
            toTitle.style.marginRight = 10;
            toTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            buttons.Add(toTitle);
            AddFloorButtons(buttons);
            right.Add(buttons);
            var note = Text(FloorNote(), 14, UiStyle.TextDim, top: 4);
            note.name = "floor-note";
            right.Add(note);
            right.Add(Text("저절로 저장됩니다. 듀얼 도중에 게임을 끄면 그 듀얼은 패배로 처리됩니다.", 13, UiStyle.TextDim, top: 2));
            if (_forfeitNotice != null)
            {
                var forfeit = Text(_forfeitNotice, 14, UiStyle.Opponent, top: 2);
                forfeit.name = "forfeit-notice";
                right.Add(forfeit);
            }
            if (_saveNotice != null)
                right.Add(Text(_saveNotice, 14, UiStyle.Opponent, top: 2));
            bar.Add(right);
            return bar;
        }

        /// <summary>
        /// 직전 듀얼 한 줄: 결과 · 칩 · 보상 · 봉인 … (런 로그의 그 듀얼 부분을 이어 붙인 것). 다음 층에 들어간 뒤의 일(칩교환소 등)은 넣지 않는다.
        /// </summary>
        string LastDuelLine()
        {
            if (_run.DuelsPlayed == 0)
                return "아직 듀얼하지 않았습니다. 1층부터 시작합니다.";
            var events = _run.Events.Where(e => e.DuelNumber == _run.DuelsPlayed)
                .TakeWhile(e => e.Kind != RunEventKind.FloorEntered)
                .Where(e => e.Kind != RunEventKind.DuelStarted && e.Kind != RunEventKind.MovedToDeck && e.Kind != RunEventKind.MovedToSide)
                .Select(e => e.Describe(_text.CardName)).ToList();
            return "직전 듀얼: " + string.Join(" · ", events);
        }

        void OpenLog()
        {
            var panel = OpenModal(1100);
            panel.name = "run-log";
            panel.style.height = Length.Percent(86);
            var head = Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.Add(UiStyle.Label("런 로그", 26, UiStyle.Accent, bold: true));
            var close = UiStyle.Button("닫기", CloseModal, 18);
            close.name = "close-log";
            head.Add(close);
            panel.Add(head);
            panel.Add(EventLog());
        }

        /// <summary>
        /// "지금 덱을 레시피로 저장" 창: 지금 메인·엑스트라 덱을 새 레시피로 (PlayerRecipe.FromRun). 시작 사이드 덱은 비운다
        /// (런 중 사이드 덱은 장수 제한이 없어 시작 사이드 덱 한도와 맞지 않는다). 봉인된 카드는 넣지 않는다.
        /// </summary>
        void OpenSaveRecipe()
        {
            if (_run == null || _run.Phase != RunPhase.Ready)
                return;
            var panel = OpenModal(720);
            panel.name = "save-recipe-panel";
            panel.Add(UiStyle.Label("지금 덱을 레시피로 저장", 26, UiStyle.Accent, bold: true));
            var note = Text($"메인 덱 {_run.MainDeck.Count}장과 엑스트라 덱 {_run.ExtraDeck.Count}장을 새 덱 레시피로 저장합니다. " +
                            "사이드 덱과 봉인된 카드는 넣지 않습니다 (시작 사이드 덱은 런 시작 화면의 덱 편집에서 고릅니다). " +
                            "런에서 얻은 카드도 컬렉션에 들어 있어 다음 런에 쓸 수 있습니다.", 15, UiStyle.TextDim, top: 8);
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);
            panel.Add(SubHeading("레시피 이름"));
            var name = DarkTextField("recipe-name", ClipName($"{_run.StartDeckName} {_run.Floor}층"), 20);
            name.maxLength = PlayerRecipe.MaxNameLength;
            panel.Add(name);
            var error = Text("", 15, UiStyle.Opponent, top: 6);
            error.name = "recipe-error";
            error.style.whiteSpace = WhiteSpace.Normal;
            error.style.display = DisplayStyle.None;
            panel.Add(error);
            var buttons = Row();
            buttons.style.marginTop = 12;
            buttons.style.justifyContent = Justify.FlexEnd;
            var confirm = UiStyle.Button("저장", () => SaveRunAsRecipe(name.value, error), 20);
            confirm.name = "recipe-save-confirm";
            buttons.Add(confirm);
            var cancel = UiStyle.Button("취소", CloseModal, 20);
            cancel.name = "recipe-save-cancel";
            cancel.style.marginLeft = 8;
            buttons.Add(cancel);
            panel.Add(buttons);
        }

        void SaveRunAsRecipe(string name, Label error)
        {
            var recipe = PlayerRecipe.FromRun(_run, _save.NewRecipeId(), (name ?? "").Trim());
            var problems = _save.SaveRecipe(recipe, _data.Database);
            if (problems.Count > 0)
            {
                ShowError(error, "저장할 수 없습니다: " + string.Join(" / ", problems));
                return;
            }
            Persist();
            Debug.Log($"[런] 지금 덱을 레시피로: {recipe.Name} ({recipe.Id}), 덱 {recipe.Deck.Count}장");
            _moveNotice = $"지금 덱을 레시피 「{recipe.Name}」(으)로 저장했습니다. 런 시작 화면의 덱 레시피 목록에 있습니다.";
            _moveNoticeIsError = false;
            ShowHub();
        }

        // ---- 덱 ----

        VisualElement HubDeckPanel()
        {
            var panel = UiStyle.PanelBox("run-decks");
            panel.style.flexGrow = 1;
            _deckScroll = new ScrollView(ScrollViewMode.Vertical);
            _deckScroll.style.flexGrow = 1;
            panel.Add(_deckScroll);

            var main = _run.MainDeck;
            var mainZone = AddPileSection(Pile.Main,
                $"메인 덱 {main.Count}장 · {KindCounts(main)}", $"{RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장");
            var extraZone = AddPileSection(Pile.Extra, $"엑스트라 덱 {_run.ExtraDeck.Count}장", $"최대 {RunRules.MaxExtraDeck}장");
            var sideZone = AddPileSection(Pile.Side, $"사이드 덱 {_run.SideDeck.Count}장", "승리 보상이 들어온다");
            int sealedCount = _run.SealedMain.Count + _run.SealedExtra.Count;
            if (sealedCount > 0)
                AddPileSection(Pile.Sealed, $"봉인 {sealedCount}장", "이번 런에서는 쓸 수 없음");
            AddDropZone(mainZone, (pile, code) => TryMove(pile, code, toDeck: true));
            AddDropZone(extraZone, (pile, code) => TryMove(pile, code, toDeck: true));
            AddDropZone(sideZone, (pile, code) => TryMove(pile, code, toDeck: false));

            var hint = Text("카드에 마우스를 올리면 왼쪽에 크게 보이고, 누르면 그 카드를 고릅니다. " +
                            "두 번 누르거나 덱 칸과 사이드 덱 칸 사이로 끌어다 놓으면 옮깁니다. " +
                            $"같은 카드명은 덱에 {RunRules.MaxCopies}장까지입니다.", 13, UiStyle.TextDim, top: 10);
            hint.style.whiteSpace = WhiteSpace.Normal;
            _deckScroll.Add(hint);

            var scroll = _deckScroll;
            var offset = _deckScrollOffset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).ExecuteLater(10);
            return panel;
        }

        /// <summary>제목과 카드 격자로 된 칸 하나를 덱 목록에 더하고, 그 칸(놓을 곳)을 돌려준다.</summary>
        VisualElement AddPileSection(Pile pile, string title, string note)
        {
            var zone = UiStyle.Box($"zone-{pile.ToString().ToLowerInvariant()}");
            zone.style.marginTop = pile == Pile.Main ? 0 : 10;
            UiStyle.Radius(zone, 6);
            UiStyle.Padding(zone, 6);
            UiStyle.BorderColor(zone, UiStyle.Border, 1);
            var header = Row();
            header.style.alignItems = Align.FlexEnd;
            header.Add(UiStyle.Label(title, 18, UiStyle.TextDim, bold: true));
            var noteLabel = Text(note, 13, UiStyle.TextDim);
            noteLabel.style.marginLeft = 10;
            noteLabel.style.marginBottom = 2;
            header.Add(noteLabel);
            zone.Add(header);

            var grid = Grid();
            grid.style.marginTop = 4;
            var cards = Cards(pile);
            // 사이드 덱 칸은 비어 있어도 카드 한 줄 높이를 둔다 (끌어다 놓을 곳). 다른 칸은 비면 한 줄로 줄인다.
            if (cards.Count > 0 || pile == Pile.Side)
                grid.style.minHeight = CardFaces.SizeOf(CardFaceSize.Small).y + 14;
            if (cards.Count == 0)
                grid.Add(Text(pile == Pile.Side ? "비어 있음 (메인·엑스트라 덱의 카드를 여기로 끌어다 놓으면 뺍니다)" : "비어 있음", 15, UiStyle.TextDim, top: 4));

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
                bool isSealed = pile == Pile.Sealed;
                Action onDouble = isSealed ? (Action)null : () => TryMove(pile, c, toDeck: pile == Pile.Side);
                grid.Add(CardSlot(pile, c, CardFaceSize.Small, tag: tag, dim: isSealed, onDoubleClick: onDouble, draggable: !isSealed));
            }
            zone.Add(grid);
            _deckScroll.Add(zone);
            return zone;
        }

        IReadOnlyList<uint> Cards(Pile pile)
        {
            switch (pile)
            {
                case Pile.Main: return _run.MainDeck;
                case Pile.Extra: return _run.ExtraDeck;
                case Pile.Side: return _run.SideDeck;
                case Pile.Sealed: return _run.SealedMain.Concat(_run.SealedExtra).ToList();
                default: return Array.Empty<uint>();
            }
        }

        /// <summary>고른 카드가 그 목록에서 사라졌으면 (봉인, 교환) 그 카드가 있는 다른 목록을 고른다. 아무 데도 없으면 고르지 않은 상태.</summary>
        void FixSelection()
        {
            if (_selectedCode == 0 || (HubPiles.Contains(_selectedPile) && Cards(_selectedPile).Contains(_selectedCode)))
                return;
            foreach (Pile pile in HubPiles)
            {
                if (Cards(pile).Contains(_selectedCode))
                {
                    _selectedPile = pile;
                    return;
                }
            }
            _selectedCode = 0;
        }

        void RenderHubDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
            {
                panel.Add(Text("카드에 마우스를 올리면 여기에 크게 보이고, 누르면 정보와 옮기기 버튼이 여기에 나옵니다.", 17, UiStyle.TextDim));
                panel.Add(Text("듀얼 사이에는 사이드 덱과 메인·엑스트라 덱의 카드를 바꿀 수 있습니다. " +
                               $"메인 덱 {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장, 엑스트라 덱 {RunRules.MaxExtraDeck}장 이하, " +
                               $"같은 카드명은 덱에 {RunRules.MaxCopies}장까지입니다.", 15, UiStyle.TextDim, top: 10));
                return;
            }
            AddCardDetail(panel, code, pinned);
            int side = _run.SideDeck.Count(c => c == code);
            int sealedCount = _run.SealedMain.Count(c => c == code) + _run.SealedExtra.Count(c => c == code);
            panel.Add(Text($"덱 {_run.DeckCopies(code)}장(같은 카드명) · 사이드 덱 {side}장 · 봉인 {sealedCount}장 · 컬렉션 {_collection.Count(code)}장",
                14, UiStyle.TextDim, top: 6));
            if (!pinned)
                return;

            var actions = UiStyle.Box();
            actions.style.marginTop = 8;
            switch (pile)
            {
                case Pile.Main:
                case Pile.Extra:
                {
                    bool ok = _run.CanMoveToSide(code, out string reason);
                    var b = UiStyle.Button("사이드 덱으로 빼기", () => TryMove(pile, code, toDeck: false), 18);
                    b.name = "move-to-side";
                    b.SetEnabled(ok);
                    actions.Add(b);
                    if (!ok)
                        actions.Add(Text(reason, 14, UiStyle.TextDim, top: 2));
                    break;
                }
                case Pile.Side:
                {
                    bool ok = _run.CanMoveToDeck(code, out string reason);
                    string target = _run.IsExtraDeckCard(code) ? "엑스트라 덱에 넣기" : "메인 덱에 넣기";
                    var b = UiStyle.Button(target, () => TryMove(pile, code, toDeck: true), 18);
                    b.name = "move-to-deck";
                    b.SetEnabled(ok);
                    actions.Add(b);
                    if (!ok)
                        actions.Add(Text(reason, 14, UiStyle.TextDim, top: 2));
                    break;
                }
                default:
                    actions.Add(Text("봉인된 카드는 이번 런에서 쓸 수 없습니다. 다음 런에서는 다시 쓸 수 있습니다.", 15, UiStyle.TextDim));
                    break;
            }
            panel.Add(actions);
        }

        /// <summary>덱 ↔ 사이드 덱 옮기기 (끌어다 놓기, 두 번 누르기, 버튼). 고른 카드는 옮겨 간 곳에서 계속 고른 상태.</summary>
        void TryMove(Pile from, uint code, bool toDeck)
        {
            if (_run == null || _run.Phase != RunPhase.Ready)
                return;
            if (toDeck ? from != Pile.Side : from != Pile.Main && from != Pile.Extra)
                return; // 같은 칸에 놓았거나 봉인된 카드
            string name = _text.CardName(code);
            bool ok = toDeck ? _run.CanMoveToDeck(code, out string reason) : _run.CanMoveToSide(code, out reason);
            if (!ok)
            {
                SetMoveNotice($"옮길 수 없습니다 — {name}: {reason}", error: true);
                return;
            }
            bool extra = _run.IsExtraDeckCard(code);
            try
            {
                if (toDeck)
                    _run.MoveToDeck(code);
                else
                    _run.MoveToSide(code);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Persist();
            _moveNotice = toDeck ? $"{name} → {(extra ? "엑스트라" : "메인")} 덱" : $"{name} → 사이드 덱";
            _moveNoticeIsError = false;
            _selectedPile = toDeck ? (extra ? Pile.Extra : Pile.Main) : Pile.Side;
            _selectedCode = code;
            ShowHub();
        }

        void SetMoveNotice(string text, bool error)
        {
            _moveNotice = text;
            _moveNoticeIsError = error;
            var label = _root.Q<Label>("move-notice");
            if (label == null)
                return;
            label.text = text;
            label.style.color = error ? UiStyle.Opponent : Positive;
            label.style.display = DisplayStyle.Flex;
        }

        // ================= 보충 고르기 =================

        void OpenRefill()
        {
            var candidates = _run.RefillCandidates();
            if (!candidates.Contains(_refillSelected))
                _refillSelected = 0;
            var panel = OpenModal(1500);
            panel.name = "refill";
            panel.style.height = Length.Percent(88);
            panel.Add(UiStyle.Label($"보충: 메인 덱 {_run.MainDeck.Count}장 → {RunRules.MinMainDeck}장", 28, UiStyle.Accent, bold: true));
            panel.Add(Text($"메인 덱이 {RunRules.MinMainDeck}장보다 적어졌습니다. 사이드 덱에서 메인 덱 카드 {_run.RefillNeeded}장을 더 골라 넣으세요. " +
                           $"두 번 누르면 바로 넣습니다. 같은 카드명은 덱에 {RunRules.MaxCopies}장까지 넣을 수 있고, 다 채우면 이 창이 닫힙니다.",
                16, UiStyle.TextDim, top: 6));
            var sealedNames = _run.Events.Where(e => e.DuelNumber == _run.DuelsPlayed && e.Kind == RunEventKind.CardSealed)
                .Select(e => _text.CardName(e.Code)).ToList();
            if (sealedNames.Count > 0)
                panel.Add(Text("이번 패배로 봉인된 카드: " + string.Join(", ", sealedNames), 15, UiStyle.Opponent, top: 6));

            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var grid = Grid(scroll.contentContainer);
            foreach (uint candidate in Sorted(candidates))
            {
                uint code = candidate;
                grid.Add(CardSlot(Pile.Refill, code, CardFaceSize.Medium, count: _run.SideDeck.Count(c => c == code),
                    onDoubleClick: () =>
                    {
                        _refillSelected = code;
                        ConfirmRefill();
                    }, draggable: false));
            }
            body.Add(scroll);

            var detail = UiStyle.PanelBox("refill-detail");
            detail.style.width = 380;
            detail.style.marginLeft = 12;
            detail.style.backgroundColor = UiStyle.PanelLight;
            body.Add(detail);
            panel.Add(body);

            _detail = detail;
            _renderDetail = RenderRefillDetail;
            ShowDetail(Pile.Refill, _refillSelected, pinned: true);
        }

        void RenderRefillDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
            {
                panel.Add(Text("왼쪽에서 넣을 카드를 고르세요.", 17, UiStyle.TextDim));
                return;
            }
            AddCardDetail(panel, code, pinned);
            panel.Add(Text($"사이드 덱 {_run.SideDeck.Count(c => c == code)}장 · 덱 {_run.DeckCopies(code)}장", 14, UiStyle.TextDim, top: 6));
            if (!pinned)
                return;
            var confirm = UiStyle.Button("메인 덱에 넣기", ConfirmRefill, 20);
            confirm.name = "refill-confirm";
            confirm.style.marginTop = 10;
            panel.Add(confirm);
        }

        void ConfirmRefill()
        {
            if (_refillSelected == 0)
                return;
            try
            {
                _run.MoveToDeck(_refillSelected);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Persist();
            ShowHub(); // 아직 모자라면 보충 창을 다시 연다
        }
    }
}
