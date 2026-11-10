// 런 화면: 덱 편집 화면 (런 시작 전, 레시피 하나를 컬렉션의 카드로 고친다). RunScreen.cs의 머리말 참고.
// 배치: 위 편집 줄(editor-bar: 이름 editor-name, 저장 editor-save, 지우기 editor-delete, 돌아가기 editor-back, 규칙 확인 editor-status) /
// 왼쪽 카드 정보(editor-card-detail) / 가운데 컬렉션(editor-collection, ×n은 아직 넣지 않은 장수) /
// 오른쪽 레시피(editor-deck: 메인 editor-main, 엑스트라 editor-extra, 시작 사이드 덱 editor-side).
// 옮기기: 두 번 누르기(컬렉션 → 덱, 덱·사이드 → 컬렉션), 끌어다 놓기(어느 칸으로든), 카드 정보의 버튼.
// 규칙은 RecipeEditor·RecipeRules(Run 어셈블리)가 정한다. 화면은 막힌 까닭과 저장·시작 가능 여부를 보여 준다:
// - 넣기를 막음: 메인 60장·엑스트라 15장·시작 사이드 덱 3장 가득, 같은 카드명 3장 (덱 + 사이드 합쳐).
// - 저장을 막음: 메인 40장 미만 등 (editor-status에 빨간 글).
// - 컬렉션보다 많이 넣으면: 카드에 "부족" 표시, 저장은 되지만 런을 시작할 수 없음 (런 시작 화면에 "시작 불가").
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
        /// <summary>컬렉션 보기 거르기.</summary>
        enum CollectionFilter
        {
            All,
            Monster,
            Spell,
            Trap,
            Extra,
        }

        static readonly Color Warning = UiStyle.Hex(0xe0a040);
        static readonly Pile[] EditorPiles = { Pile.Collection, Pile.DeckMain, Pile.DeckExtra, Pile.DeckSide };

        RecipeEditor _editor;
        CollectionFilter _filter;
        ScrollView _pickScroll, _editorDeckScroll;
        Vector2 _pickScrollOffset, _editorDeckScrollOffset;
        string _editorNotice;
        bool _editorNoticeIsError, _confirmLeave, _confirmDelete;

        /// <summary>덱 편집 화면을 연다. recipe가 null이면 빈 새 레시피, copy면 그 레시피를 복사한 새 레시피.</summary>
        void OpenEditor(PlayerRecipe recipe, bool copy = false)
        {
            var db = _data.Database;
            if (recipe == null)
                _editor = new RecipeEditor(null, db, _collection);
            else if (copy)
                _editor = RecipeEditor.CopyOf(recipe, db, _collection, ClipName(recipe.Name + " 사본"));
            else
                _editor = new RecipeEditor(recipe, db, _collection);
            _editorNotice = null;
            _confirmLeave = _confirmDelete = false;
            _filter = CollectionFilter.All;
            _pickScroll = _editorDeckScroll = null;
            _pickScrollOffset = _editorDeckScrollOffset = Vector2.zero;
            _selectedPile = Pile.Collection;
            _selectedCode = 0;
            Debug.Log($"[덱 편집] 열기: {(recipe == null ? "빈 새 레시피" : copy ? recipe.Name + " 복사" : recipe.Name)}");
            ShowEditor();
        }

        static string ClipName(string name) => name.Length <= PlayerRecipe.MaxNameLength ? name : name.Substring(0, PlayerRecipe.MaxNameLength);

        void ShowEditor()
        {
            if (_pickScroll != null)
                _pickScrollOffset = _pickScroll.scrollOffset;
            if (_editorDeckScroll != null)
                _editorDeckScrollOffset = _editorDeckScroll.scrollOffset;
            BeginScreen();
            if (_selectedCode != 0 && !EditorCards(_selectedPile).Contains(_selectedCode))
                _selectedPile = EditorPiles.FirstOrDefault(p => EditorCards(p).Contains(_selectedCode));
            if (!EditorPiles.Contains(_selectedPile) || !EditorCards(_selectedPile).Contains(_selectedCode))
            {
                _selectedPile = Pile.Collection;
                _selectedCode = 0;
            }

            _root.Add(EditorBar());
            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            var detail = UiStyle.PanelBox("editor-card-detail");
            detail.style.width = 380;
            detail.style.marginRight = 12;
            body.Add(detail);
            body.Add(EditorCollectionPanel());
            body.Add(EditorDeckPanel());
            _root.Add(body);

            _detail = detail;
            _renderDetail = RenderEditorDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
            RefreshEditorStatus();
        }

        List<uint> EditorCards(Pile pile)
        {
            switch (pile)
            {
                case Pile.Collection: return _collection.Cards.Keys.Where(c => Matches(c, _filter)).ToList();
                case Pile.DeckMain: return _editor.MainDeck.ToList();
                case Pile.DeckExtra: return _editor.ExtraDeck.ToList();
                case Pile.DeckSide: return _editor.Side.ToList();
                default: return new List<uint>();
            }
        }

        // ---- 위: 이름, 저장·지우기·돌아가기, 규칙 확인 ----

        VisualElement EditorBar()
        {
            var bar = UiStyle.PanelBox("editor-bar");
            UiStyle.Padding(bar, 14);
            var row = Row();
            row.style.alignItems = Align.Center;
            row.Add(UiStyle.Label("덱 편집", 30, UiStyle.Accent, bold: true));
            var name = DarkTextField("editor-name", _editor.Name, 20);
            name.maxLength = PlayerRecipe.MaxNameLength;
            name.style.width = 380;
            name.style.marginLeft = 16;
            name.RegisterValueChangedCallback(e =>
            {
                _editor.Name = e.newValue;
                RefreshEditorStatus();
            });
            row.Add(name);
            var dirty = Text("저장하지 않은 변경 있음", 15, Warning);
            dirty.name = "editor-dirty";
            dirty.style.marginLeft = 12;
            row.Add(dirty);
            var spacer = UiStyle.Box();
            spacer.style.flexGrow = 1;
            row.Add(spacer);

            if (_confirmDelete)
            {
                row.Add(Text($"「{_editor.Name}」 레시피를 지웁니다 (되돌릴 수 없음).", 15, UiStyle.Opponent));
                row.Add(BarButton("지우기", "editor-delete-confirm", DeleteEditorRecipe));
                row.Add(BarButton("취소", "editor-delete-cancel", () =>
                {
                    _confirmDelete = false;
                    ShowEditor();
                }));
            }
            else if (_confirmLeave)
            {
                row.Add(Text("저장하지 않은 변경이 사라집니다.", 15, UiStyle.Opponent));
                row.Add(BarButton("저장하지 않고 나가기", "editor-leave", ShowStart));
                row.Add(BarButton("계속 고치기", "editor-leave-cancel", () =>
                {
                    _confirmLeave = false;
                    ShowEditor();
                }));
            }
            else
            {
                var save = BarButton("저장", "editor-save", SaveEditor, 20);
                save.style.paddingLeft = save.style.paddingRight = 24;
                row.Add(save);
                if (_editor.Id != null)
                    row.Add(BarButton("레시피 지우기", "editor-delete", () =>
                    {
                        _confirmDelete = true;
                        ShowEditor();
                    }));
                row.Add(BarButton("런 시작 화면으로", "editor-back", LeaveEditor));
            }
            bar.Add(row);

            var status = Text("", 16, top: 8);
            status.name = "editor-status";
            status.style.whiteSpace = WhiteSpace.Normal;
            bar.Add(status);
            var notice = Text(_editorNotice ?? "", 15, _editorNoticeIsError ? UiStyle.Opponent : Positive, top: 4);
            notice.name = "editor-notice";
            notice.style.whiteSpace = WhiteSpace.Normal;
            notice.style.display = _editorNotice == null ? DisplayStyle.None : DisplayStyle.Flex;
            bar.Add(notice);
            return bar;
        }

        static Button BarButton(string text, string name, Action onClick, int size = 16)
        {
            var b = UiStyle.Button(text, onClick, size);
            b.name = name;
            b.style.marginLeft = 8;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            return b;
        }

        /// <summary>이름을 고칠 때마다 (화면을 다시 그리지 않고) 규칙 확인 글, 변경 표시, 저장 버튼만 바꾼다.</summary>
        void RefreshEditorStatus()
        {
            if (_editor == null)
                return;
            var saveProblems = _editor.SaveProblems();
            var startProblems = _editor.StartOnlyProblems();
            var status = _root.Q<Label>("editor-status");
            if (status != null)
            {
                if (saveProblems.Count > 0)
                {
                    status.text = "저장할 수 없음: " + string.Join(" / ", saveProblems);
                    status.style.color = UiStyle.Opponent;
                }
                else if (startProblems.Count > 0)
                {
                    status.text = "저장할 수 있지만 이대로는 런을 시작할 수 없음: " + string.Join(" / ", startProblems);
                    status.style.color = Warning;
                }
                else
                {
                    status.text = "규칙에 맞습니다. 이 레시피로 런을 시작할 수 있습니다.";
                    status.style.color = Positive;
                }
            }
            var dirty = _root.Q<Label>("editor-dirty");
            if (dirty != null)
                dirty.style.display = _editor.IsDirty ? DisplayStyle.Flex : DisplayStyle.None;
            _root.Q<Button>("editor-save")?.SetEnabled(_editor.IsDirty && saveProblems.Count == 0);
        }

        void SaveEditor()
        {
            var problems = _editor.SaveProblems();
            if (problems.Count == 0)
            {
                var recipe = _editor.Build(_editor.Id ?? _save.NewRecipeId());
                problems = _save.SaveRecipe(recipe, _data.Database);
                if (problems.Count == 0)
                {
                    _editor.MarkSaved(recipe);
                    _startRecipeId = recipe.Id;
                    Persist();
                    var startProblems = _editor.StartOnlyProblems();
                    SetEditorNotice($"「{recipe.Name}」을(를) 저장했습니다." +
                                    (startProblems.Count > 0 ? " 다만 이대로는 런을 시작할 수 없습니다." : ""), error: false);
                    Debug.Log($"[덱 편집] 저장: {recipe.Name} ({recipe.Id}), 덱 {recipe.Deck.Count}장, 사이드 {recipe.Side.Count}장" +
                              (startProblems.Count > 0 ? ", 시작 불가: " + string.Join(" / ", startProblems) : ""));
                    ShowEditor();
                    return;
                }
            }
            SetEditorNotice("저장할 수 없습니다: " + string.Join(" / ", problems), error: true);
            ShowEditor();
        }

        void LeaveEditor()
        {
            if (_editor.IsDirty)
            {
                _confirmLeave = true;
                ShowEditor();
                return;
            }
            ShowStart();
        }

        void DeleteEditorRecipe()
        {
            string id = _editor.Id;
            if (id != null && _save.DeleteRecipe(id))
            {
                Persist();
                Debug.Log($"[덱 편집] 지움: {_editor.Name} ({id})");
                if (_startRecipeId == id)
                    _startRecipeId = null;
            }
            ShowStart();
        }

        void SetEditorNotice(string text, bool error)
        {
            _editorNotice = text;
            _editorNoticeIsError = error;
        }

        // ---- 가운데: 컬렉션 ----

        VisualElement EditorCollectionPanel()
        {
            var panel = UiStyle.PanelBox("editor-collection");
            panel.style.flexGrow = 1;
            panel.style.flexShrink = 1;
            panel.style.marginRight = 12;

            var head = Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.Add(UiStyle.Label($"컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장", 18, UiStyle.TextDim, bold: true));
            var filters = Row();
            foreach (CollectionFilter filter in Enum.GetValues(typeof(CollectionFilter)))
            {
                var f = filter;
                int n = _collection.Cards.Keys.Count(c => Matches(c, f));
                var b = UiStyle.Button($"{FilterName(f)} {n}", () =>
                {
                    _filter = f;
                    _pickScrollOffset = Vector2.zero;
                    _pickScroll = null;
                    ShowEditor();
                }, 14);
                b.name = "filter-" + f.ToString().ToLowerInvariant();
                b.style.marginLeft = 4;
                UiStyle.SetSelected(b, f == _filter);
                filters.Add(b);
            }
            head.Add(filters);
            panel.Add(head);
            var note = Text("×n은 아직 넣지 않은 장수입니다. 두 번 누르면 덱에 넣고, 오른쪽 칸으로 끌어다 놓으면 그 칸에 넣습니다. " +
                            "흐린 카드는 컬렉션의 장수를 다 넣은 카드입니다. 더 넣을 수는 있지만(\"부족\" 표시) 그 레시피로는 런을 시작할 수 없습니다.",
                13, UiStyle.TextDim, top: 2);
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);

            _pickScroll = new ScrollView(ScrollViewMode.Vertical);
            _pickScroll.style.flexGrow = 1;
            _pickScroll.style.marginTop = 6;
            var grid = Grid(_pickScroll.contentContainer);
            var shown = EditorCards(Pile.Collection);
            if (shown.Count == 0)
                grid.Add(Text("없음", 15, UiStyle.TextDim));
            foreach (uint code in Sorted(shown))
            {
                uint c = code;
                int left = _editor.Left(c);
                grid.Add(CardSlot(Pile.Collection, c, CardFaceSize.Medium, count: Math.Max(left, 0), tag: left < 0 ? "부족" : null, dim: left <= 0,
                    onDoubleClick: () => EditorAdd(c, toSide: false)));
            }
            panel.Add(_pickScroll);
            // 덱·사이드 덱의 카드를 컬렉션으로 끌어다 놓으면 뺀다.
            AddDropZone(panel, (pile, code) =>
            {
                if (pile == Pile.DeckMain || pile == Pile.DeckExtra || pile == Pile.DeckSide)
                    EditorRemove(code, fromSide: pile == Pile.DeckSide);
            });

            var scroll = _pickScroll;
            var offset = _pickScrollOffset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).ExecuteLater(10);
            return panel;
        }

        bool Matches(uint code, CollectionFilter filter)
        {
            bool extra = IsExtraDeckCard(code);
            switch (filter)
            {
                case CollectionFilter.Monster: return !extra && KindOrder(code) == 0;
                case CollectionFilter.Spell: return KindOrder(code) == 1;
                case CollectionFilter.Trap: return KindOrder(code) == 2;
                case CollectionFilter.Extra: return extra;
                default: return true;
            }
        }

        static string FilterName(CollectionFilter filter)
        {
            switch (filter)
            {
                case CollectionFilter.Monster: return "몬스터";
                case CollectionFilter.Spell: return "마법";
                case CollectionFilter.Trap: return "함정";
                case CollectionFilter.Extra: return "엑스트라";
                default: return "전체";
            }
        }

        // ---- 오른쪽: 레시피 (메인·엑스트라·시작 사이드 덱) ----

        VisualElement EditorDeckPanel()
        {
            var panel = UiStyle.PanelBox("editor-deck");
            panel.style.width = 640;
            panel.style.flexShrink = 0;
            _editorDeckScroll = new ScrollView(ScrollViewMode.Vertical);
            _editorDeckScroll.style.flexGrow = 1;
            _editorDeckScroll.style.flexShrink = 1;
            panel.Add(_editorDeckScroll);

            int main = _editor.MainCount;
            var mainZone = AddEditorSection(_editorDeckScroll, Pile.DeckMain, "editor-main", $"메인 덱 {main}장 · {KindCounts(_editor.MainDeck)}",
                $"{RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장", main < RunRules.MinMainDeck);
            var extraZone = AddEditorSection(_editorDeckScroll, Pile.DeckExtra, "editor-extra", $"엑스트라 덱 {_editor.ExtraCount}장", $"최대 {RunRules.MaxExtraDeck}장", false);
            // 시작 사이드 덱은 스크롤 밖 아래에 고정해서 메인 덱을 내려 보지 않아도 늘 보이게 한다.
            var sideZone = AddEditorSection(panel, Pile.DeckSide, "editor-side", $"시작 사이드 덱 {_editor.Side.Count}/{RunRules.StartingSideDeckMax}장",
                "런 중에 덱과 바꾸거나 보충에 쓴다", false);
            sideZone.style.flexShrink = 0;
            void ToDeck(Pile pile, uint code)
            {
                if (pile == Pile.Collection)
                    EditorAdd(code, toSide: false);
                else if (pile == Pile.DeckSide)
                    EditorMove(code, toSide: false);
            }
            AddDropZone(mainZone, ToDeck);
            AddDropZone(extraZone, ToDeck);
            AddDropZone(sideZone, (pile, code) =>
            {
                if (pile == Pile.Collection)
                    EditorAdd(code, toSide: true);
                else if (pile == Pile.DeckMain || pile == Pile.DeckExtra)
                    EditorMove(code, toSide: true);
            });

            var hint = Text("덱·사이드 덱의 카드를 두 번 누르거나 컬렉션으로 끌어다 놓으면 뺍니다. 덱과 사이드 덱 사이로 끌어다 놓으면 옮깁니다. " +
                            $"같은 카드명은 덱과 시작 사이드 덱을 합쳐 {RunRules.MaxCopies}장까지입니다.", 13, UiStyle.TextDim, top: 10);
            hint.style.whiteSpace = WhiteSpace.Normal;
            _editorDeckScroll.Add(hint);

            var scroll = _editorDeckScroll;
            var offset = _editorDeckScrollOffset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).ExecuteLater(10);
            return panel;
        }

        VisualElement AddEditorSection(VisualElement parent, Pile pile, string name, string title, string note, bool problem)
        {
            var zone = UiStyle.Box(name);
            zone.style.marginTop = pile == Pile.DeckMain ? 0 : 10;
            UiStyle.Radius(zone, 6);
            UiStyle.Padding(zone, 6);
            UiStyle.BorderColor(zone, problem ? UiStyle.Opponent : UiStyle.Border, 1);
            var header = Row();
            header.style.alignItems = Align.FlexEnd;
            var titleLabel = UiStyle.Label(title, 18, problem ? UiStyle.Opponent : UiStyle.TextDim, bold: true);
            titleLabel.name = name + "-count";
            header.Add(titleLabel);
            var noteLabel = Text(note, 13, UiStyle.TextDim);
            noteLabel.style.marginLeft = 10;
            noteLabel.style.marginBottom = 2;
            header.Add(noteLabel);
            zone.Add(header);

            var grid = Grid();
            grid.style.marginTop = 4;
            grid.style.minHeight = CardFaces.SizeOf(CardFaceSize.Small).y + 14;
            var cards = EditorCards(pile);
            if (cards.Count == 0)
                grid.Add(Text("비어 있음 — 컬렉션의 카드를 여기로 끌어다 놓으면 넣습니다.", 15, UiStyle.TextDim, top: 4));
            foreach (uint code in Sorted(cards))
            {
                uint c = code;
                grid.Add(CardSlot(pile, c, CardFaceSize.Small, tag: _editor.Left(c) < 0 ? "부족" : null,
                    onDoubleClick: () => EditorRemove(c, fromSide: pile == Pile.DeckSide)));
            }
            zone.Add(grid);
            parent.Add(zone);
            return zone;
        }

        // ---- 옮기기 ----

        void EditorAdd(uint code, bool toSide)
        {
            string reason = _editor.AddReason(code, toSide);
            string name = _text.CardName(code);
            if (reason != null || !_editor.Add(code, toSide))
            {
                SetEditorNotice($"넣을 수 없습니다 — {name}: {reason}", error: true);
                ShowEditor();
                return;
            }
            string where = toSide ? "시작 사이드 덱" : _editor.IsExtraDeckCard(code) ? "엑스트라 덱" : "메인 덱";
            SetEditorNotice($"{name} → {where}" + (_editor.Left(code) < 0 ? " (컬렉션보다 많음: 이대로는 런을 시작할 수 없음)" : ""), error: false);
            ShowEditor();
        }

        void EditorMove(uint code, bool toSide)
        {
            string reason = _editor.MoveReason(code, toSide);
            string name = _text.CardName(code);
            if (reason != null || !_editor.Move(code, toSide))
            {
                SetEditorNotice($"옮길 수 없습니다 — {name}: {reason}", error: true);
                ShowEditor();
                return;
            }
            SetEditorNotice(toSide ? $"{name} → 시작 사이드 덱" : $"{name} → {(_editor.IsExtraDeckCard(code) ? "엑스트라" : "메인")} 덱", error: false);
            _selectedPile = toSide ? Pile.DeckSide : _editor.IsExtraDeckCard(code) ? Pile.DeckExtra : Pile.DeckMain;
            _selectedCode = code;
            ShowEditor();
        }

        void EditorRemove(uint code, bool fromSide)
        {
            if (!_editor.Remove(code, fromSide))
                return;
            SetEditorNotice($"{_text.CardName(code)} → 컬렉션 ({(fromSide ? "시작 사이드 덱" : "덱")}에서 뺌)", error: false);
            ShowEditor();
        }

        // ---- 왼쪽: 카드 정보 ----

        void RenderEditorDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
            {
                panel.Add(Text("카드에 마우스를 올리면 여기에 크게 보이고, 누르면 정보와 넣기·빼기 버튼이 여기에 나옵니다.", 16, UiStyle.TextDim));
                panel.Add(Text($"메인 덱 {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장, 엑스트라 덱 {RunRules.MaxExtraDeck}장 이하, " +
                               $"시작 사이드 덱 {RunRules.StartingSideDeckMax}장 이하. 같은 카드명(에라타 전·후 버전 포함)은 덱과 시작 사이드 덱을 합쳐 {RunRules.MaxCopies}장까지입니다. " +
                               "메인 덱이 40장보다 적으면 저장할 수 없습니다.", 15, UiStyle.TextDim, top: 10));
                return;
            }
            AddCardDetail(panel, code, pinned);
            int left = _editor.Left(code);
            panel.Add(Text($"컬렉션 {_editor.Owned(code)}장 · 덱 {_editor.Deck.Count(c => c == code)}장 · 사이드 덱 {_editor.Side.Count(c => c == code)}장 · " +
                           $"같은 카드명 {_editor.NameCopies(code)}/{RunRules.MaxCopies}장", 14, left < 0 ? UiStyle.Opponent : UiStyle.TextDim, top: 6));
            if (!pinned)
                return;
            var actions = UiStyle.Box();
            actions.style.marginTop = 8;
            void AddAction(string text, string name, string reason, Action onClick)
            {
                var b = UiStyle.Button(text, onClick, 18);
                b.name = name;
                b.style.marginTop = 4;
                b.SetEnabled(reason == null);
                actions.Add(b);
                if (reason != null)
                    actions.Add(Text(reason, 14, UiStyle.TextDim, top: 2));
            }
            if (pile == Pile.Collection)
            {
                string target = _editor.IsExtraDeckCard(code) ? "엑스트라 덱에 넣기" : "메인 덱에 넣기";
                AddAction(target, "editor-add", _editor.AddReason(code, false), () => EditorAdd(code, toSide: false));
                AddAction("시작 사이드 덱에 넣기", "editor-add-side", _editor.AddReason(code, true), () => EditorAdd(code, toSide: true));
                if (left <= 0)
                    actions.Add(Text("컬렉션의 장수를 모두 넣었습니다. 더 넣으면 저장은 되지만 그 레시피로는 런을 시작할 수 없습니다.", 14, Warning, top: 6));
            }
            else
            {
                bool side = pile == Pile.DeckSide;
                AddAction("빼기 (컬렉션으로)", "editor-remove", null, () => EditorRemove(code, fromSide: side));
                if (side)
                    AddAction(_editor.IsExtraDeckCard(code) ? "엑스트라 덱으로" : "메인 덱으로", "editor-to-deck", _editor.MoveReason(code, false),
                        () => EditorMove(code, toSide: false));
                else
                    AddAction("시작 사이드 덱으로", "editor-to-side", _editor.MoveReason(code, true), () => EditorMove(code, toSide: true));
            }
            panel.Add(actions);
        }
    }
}
