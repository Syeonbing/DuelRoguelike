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

        /// <summary>컬렉션 보기 정렬. 기본 = 몬스터(레벨 높은 순) → 마법 → 함정 → 엑스트라, 같으면 카드명 순.</summary>
        enum CollectionSort
        {
            Default,
            Name,
            Level,
            Attack,
            Defense,
            Rarity,
        }

        RecipeEditor _editor;
        CollectionFilter _filter;
        /// <summary>정렬은 덱 편집을 다시 열어도 그대로 둔다 (검색어는 열 때마다 비운다).</summary>
        CollectionSort _sort;
        string _search = "";
        /// <summary>검색에 효과문도 포함할지 (아니면 카드명만).</summary>
        bool _searchText;
        VisualElement _pickGrid;
        Label _pickCount;
        readonly List<(CollectionFilter Filter, Button Button)> _filterButtons = new List<(CollectionFilter, Button)>();
        readonly List<(CollectionSort Sort, Button Button)> _sortButtons = new List<(CollectionSort, Button)>();
        Button _searchScopeButton;
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
            _search = "";
            _pickScroll = _editorDeckScroll = null;
            _pickScrollOffset = _editorDeckScrollOffset = Vector2.zero;
            _selectedPile = Pile.Collection;
            _selectedCode = 0;
            Debug.Log($"[덱 편집] 열기: {(recipe == null ? "빈 새 레시피" : copy ? recipe.Name + " 복사" : recipe.Name)}");
            ShowEditor();
        }

        static string ClipName(string name) => name.Length <= PlayerRecipe.MaxNameLength ? name : name.Substring(0, PlayerRecipe.MaxNameLength);

        /// <summary>덱 편집 화면의 카드 폭. 덱이 한 줄 10장, 컬렉션이 한 줄 5장으로 떨어지게 잡았다.</summary>
        const float EditorCardWidth = 82;

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

            // 화면 전체를 쓰는 무대: 위 막대, 왼쪽 컬렉션(한 줄 5장), 가운데 덱(한 줄 10장), 오른쪽 상세 카드
            var stage = NewStage("editor-stage");
            stage.style.backgroundColor = UiStyle.Hex(0x3b342c);
            stage.Add(EditorBar());
            var collection = EditorCollectionPanel();
            UiStyle.Place(collection, 24, 96, 520, 960);
            stage.Add(collection);
            var deck = EditorDeckPanel();
            UiStyle.Place(deck, 564, 96, 976, 960);
            stage.Add(deck);
            var detail = UiStyle.Box("editor-card-detail");
            UiStyle.Place(detail, 1572, 96, 324, 960);
            stage.Add(detail);

            _detail = detail;
            _renderDetail = RenderEditorDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
            RefreshEditorStatus();
            ApplyKeyCaps();
        }

        List<uint> EditorCards(Pile pile)
        {
            switch (pile)
            {
                case Pile.Collection: return _collection.Cards.Keys.Where(c => Matches(c, _filter) && MatchesSearch(c)).ToList();
                case Pile.DeckMain: return _editor.MainDeck.ToList();
                case Pile.DeckExtra: return _editor.ExtraDeck.ToList();
                case Pile.DeckSide: return _editor.Side.ToList();
                default: return new List<uint>();
            }
        }

        // ---- 위: 이름, 저장·지우기·돌아가기, 규칙 확인 ----

        /// <summary>위 막대: "덱 편집", 이름 입력 칸, 규칙 확인 한 줄, 저장하지 않은 변경 표시, 저장·지우기·나가기.</summary>
        VisualElement EditorBar()
        {
            var bar = UiStyle.Box("editor-bar", FlexDirection.Row);
            UiStyle.Place(bar, 0, 0, StageWidth, TopBarHeight);
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = bar.style.paddingRight = 28;
            bar.style.backgroundColor = UiStyle.Stone8;
            UiStyle.GrainOn(bar);
            bar.style.borderBottomWidth = 3;
            bar.style.borderBottomColor = UiStyle.Stone9;
            var title = UiStyle.Label("덱 편집", 36, UiStyle.Gold);
            UiStyle.Black(title);
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.flexShrink = 0;
            bar.Add(title);
            var name = DarkTextField("editor-name", _editor.Name, 21);
            name.maxLength = PlayerRecipe.MaxNameLength;
            name.style.width = 380;
            name.style.marginLeft = 18;
            name.style.flexShrink = 0;
            name.RegisterValueChangedCallback(e =>
            {
                _editor.Name = e.newValue;
                RefreshEditorStatus();
            });
            bar.Add(name);
            // 규칙 확인 (RefreshEditorStatus가 글과 색을 바꾼다)
            var status = Text("", 17);
            status.name = "editor-status";
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginLeft = 18;
            status.style.flexGrow = 1;
            status.style.flexShrink = 1;
            status.style.maxHeight = 66;
            status.style.overflow = Overflow.Hidden;
            bar.Add(status);
            var dirty = Text("저장하지 않은 변경", 17, Warning, bold: true);
            dirty.name = "editor-dirty";
            dirty.style.whiteSpace = WhiteSpace.NoWrap;
            dirty.style.marginLeft = 12;
            dirty.style.flexShrink = 0;
            bar.Add(dirty);

            if (_confirmDelete)
            {
                bar.Add(BarNote($"「{_editor.Name}」 레시피를 지운다. 되돌릴 수 없다."));
                bar.Add(BarButton("지우기", "editor-delete-confirm", DeleteEditorRecipe, gold: true));
                bar.Add(BarButton("취소", "editor-delete-cancel", () =>
                {
                    _confirmDelete = false;
                    ShowEditor();
                }));
            }
            else if (_confirmLeave)
            {
                bar.Add(BarNote("저장하지 않은 변경이 사라진다."));
                bar.Add(BarButton("저장하지 않고 나가기", "editor-leave", ShowStart));
                bar.Add(BarButton("계속 고치기", "editor-leave-cancel", () =>
                {
                    _confirmLeave = false;
                    ShowEditor();
                }, gold: true));
            }
            else
            {
                bar.Add(BarButton("저장", "editor-save", SaveEditor, gold: true));
                if (_editor.Id != null)
                    bar.Add(BarButton("레시피 지우기", "editor-delete", () =>
                    {
                        _confirmDelete = true;
                        ShowEditor();
                    }));
                var back = BarButton("나가기", "editor-back", LeaveEditor);
                back.style.width = 132;
                back.style.paddingLeft = 14;
                back.style.paddingRight = 44;
                back.style.unityTextAlign = TextAnchor.MiddleLeft;
                var key = KeyCap("X", "K");
                key.pickingMode = PickingMode.Ignore;
                key.style.position = Position.Absolute;
                key.style.right = 9;
                key.style.top = 8;
                back.Add(key);
                bar.Add(back);
            }
            return bar;
        }

        static Label BarNote(string text)
        {
            var note = Text(text, 17, UiStyle.Bad, bold: true);
            note.style.whiteSpace = WhiteSpace.NoWrap;
            note.style.marginLeft = 12;
            note.style.flexShrink = 0;
            return note;
        }

        static Button BarButton(string text, string name, Action onClick, bool gold = false)
        {
            var b = UiStyle.StoneButton(text, onClick, gold, small: true);
            b.name = name;
            b.style.marginLeft = 8;
            b.style.paddingLeft = b.style.paddingRight = 16;
            b.style.flexShrink = 0;
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

        /// <summary>왼쪽 돌판: 컬렉션. 찾기, 종류 거르기, 정렬, 한 줄 5장의 격자.</summary>
        VisualElement EditorCollectionPanel()
        {
            var panel = UiStyle.PanelBox("editor-collection");
            panel.style.paddingLeft = panel.style.paddingRight = 16;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 14;

            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.Add(UiStyle.Label("컬렉션", 24, UiStyle.Text, bold: true));
            var total = Text($"{_collection.Cards.Count}종 {_collection.TotalCount}장", 19, UiStyle.TextDim);
            total.style.marginLeft = 12;
            total.style.marginBottom = 2;
            head.Add(total);
            panel.Add(head);

            // 찾기 (카드명, 또는 효과문까지)
            var tools = Row();
            tools.style.alignItems = Align.Center;
            tools.style.marginTop = 8;
            var search = DarkTextField("collection-search", _search, 19);
            search.style.flexGrow = 1;
            search.style.flexShrink = 1;
            search.RegisterValueChangedCallback(e =>
            {
                _search = e.newValue ?? "";
                RefreshCollection(resetScroll: true);
            });
            tools.Add(search);
            _searchScopeButton = UiStyle.Button("", () =>
            {
                _searchText = !_searchText;
                RefreshCollection(resetScroll: true);
            }, 16);
            _searchScopeButton.name = "search-scope";
            _searchScopeButton.style.marginLeft = 6;
            _searchScopeButton.style.flexShrink = 0;
            tools.Add(_searchScopeButton);
            panel.Add(tools);

            // 종류 거르기
            var filters = Row();
            filters.style.flexWrap = Wrap.Wrap;
            filters.style.marginTop = 6;
            _filterButtons.Clear();
            foreach (CollectionFilter filter in Enum.GetValues(typeof(CollectionFilter)))
            {
                var f = filter;
                var b = UiStyle.Button(FilterName(f), () =>
                {
                    _filter = f;
                    RefreshCollection(resetScroll: true);
                }, 16);
                b.name = "filter-" + f.ToString().ToLowerInvariant();
                b.style.marginRight = 4;
                b.style.marginTop = 4;
                filters.Add(b);
                _filterButtons.Add((f, b));
            }
            panel.Add(filters);

            // 정렬
            var sorts = Row();
            sorts.style.flexWrap = Wrap.Wrap;
            sorts.style.alignItems = Align.Center;
            sorts.style.marginTop = 2;
            var sortLabel = Text("정렬", 16, UiStyle.TextDim);
            sortLabel.style.marginRight = 6;
            sortLabel.style.marginTop = 4;
            sorts.Add(sortLabel);
            _sortButtons.Clear();
            foreach (CollectionSort sort in Enum.GetValues(typeof(CollectionSort)))
            {
                var s = sort;
                var b = UiStyle.Button(SortName(s), () =>
                {
                    _sort = s;
                    RefreshCollection(resetScroll: true);
                }, 16);
                b.name = "sort-" + s.ToString().ToLowerInvariant();
                b.style.marginRight = 4;
                b.style.marginTop = 4;
                sorts.Add(b);
                _sortButtons.Add((s, b));
            }
            panel.Add(sorts);
            _pickCount = Text("", 15, UiStyle.TextDim, top: 6);
            _pickCount.name = "collection-count";
            _pickCount.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_pickCount);
            var note = Text("×n은 아직 넣지 않은 장수다. 두 번 누르면 덱에 넣는다. 흐린 카드는 컬렉션의 장수를 다 넣은 카드다.", 15, UiStyle.TextDim, top: 2);
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);

            _pickScroll = new ScrollView(ScrollViewMode.Vertical);
            _pickScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _pickScroll.style.flexGrow = 1;
            _pickScroll.style.flexShrink = 1;
            _pickScroll.style.marginTop = 8;
            _pickGrid = Grid(_pickScroll.contentContainer);
            RefreshCollection(resetScroll: false);
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

        /// <summary>
        /// 컬렉션 카드 칸만 다시 그린다. 검색 칸에 글자를 칠 때마다 불리므로
        /// 화면 전체를 다시 그리지 않는다 (그러면 검색 칸의 입력 포커스가 사라진다).
        /// </summary>
        void RefreshCollection(bool resetScroll)
        {
            if (_pickGrid == null)
                return;
            string term = SearchKey(_search);
            foreach (var (f, b) in _filterButtons)
            {
                int n = _collection.Cards.Keys.Count(c => Matches(c, f) && MatchesSearch(c, term));
                b.text = $"{FilterName(f)} {n}";
                UiStyle.SetSelected(b, f == _filter);
            }
            foreach (var (s, b) in _sortButtons)
                UiStyle.SetSelected(b, s == _sort);
            if (_searchScopeButton != null)
            {
                _searchScopeButton.text = _searchText ? "카드명+효과문" : "카드명만";
                UiStyle.SetSelected(_searchScopeButton, _searchText);
            }

            _pickGrid.Clear();
            _cardViews.RemoveAll(v => v.Pile == Pile.Collection);
            var shown = EditorCards(Pile.Collection);
            if (_pickCount != null)
                _pickCount.text = (term.Length == 0 ? $"{shown.Count}종" : $"「{_search.Trim()}」 찾은 카드 {shown.Count}종") + " · " + SortNote(_sort);
            if (shown.Count == 0)
                _pickGrid.Add(Text(term.Length == 0 ? "없음" : "찾는 카드가 없다.", 17, UiStyle.TextDim));
            foreach (uint code in SortedCollection(shown))
            {
                uint c = code;
                int left = _editor.Left(c);
                var slot = CardSlot(Pile.Collection, c, CardFaceSize.Small, count: Math.Max(left, 0), tag: left < 0 ? "부족" : null, dim: left <= 0,
                    onDoubleClick: () => EditorAdd(c, toSide: false), width: EditorCardWidth);
                UiStyle.Margin(slot, 0);
                _pickGrid.Add(slot);
            }
            if (resetScroll && _pickScroll != null)
            {
                _pickScrollOffset = Vector2.zero;
                _pickScroll.scrollOffset = Vector2.zero;
            }
        }

        bool MatchesSearch(uint code) => MatchesSearch(code, SearchKey(_search));

        /// <summary>대소문자와 띄어쓰기를 무시하고 카드명(설정하면 효과문도)에 검색어가 들어 있는지.</summary>
        bool MatchesSearch(uint code, string term)
        {
            if (term.Length == 0)
                return true;
            if (SearchKey(_text.CardName(code)).Contains(term))
                return true;
            return _searchText && SearchKey(_text.Record(code)?.Description).Contains(term);
        }

        static string SearchKey(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char ch in text)
                if (!char.IsWhiteSpace(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        /// <summary>고른 정렬. 값이 같으면 기본 순서(Sorted)를 그대로 둔다 (OrderBy는 안정 정렬).</summary>
        IEnumerable<uint> SortedCollection(IEnumerable<uint> cards)
        {
            var basic = Sorted(cards).ToList();
            switch (_sort)
            {
                case CollectionSort.Name:
                    return basic.OrderBy(c => _text.CardName(c), StringComparer.Ordinal);
                case CollectionSort.Level:
                    return basic.OrderBy(c => KindOrder(c) == 0 ? 0 : 1).ThenByDescending(c => _text.Record(c)?.Data.Level ?? 0);
                case CollectionSort.Attack:
                    return basic.OrderBy(c => KindOrder(c) == 0 ? 0 : 1).ThenByDescending(c => _text.Record(c)?.Data.Attack ?? 0);
                case CollectionSort.Defense:
                    return basic.OrderBy(c => KindOrder(c) == 0 ? 0 : 1).ThenByDescending(c => _text.Record(c)?.Data.Defense ?? 0);
                case CollectionSort.Rarity:
                    return basic.OrderByDescending(c => _rarities.TryGet(c, out var r) ? (int)r : -1);
                default:
                    return basic;
            }
        }

        static string SortName(CollectionSort sort)
        {
            switch (sort)
            {
                case CollectionSort.Name: return "이름";
                case CollectionSort.Level: return "레벨";
                case CollectionSort.Attack: return "공격력";
                case CollectionSort.Defense: return "수비력";
                case CollectionSort.Rarity: return "레어도";
                default: return "종류";
            }
        }

        static string SortNote(CollectionSort sort)
        {
            switch (sort)
            {
                case CollectionSort.Name: return "카드명 가나다순";
                case CollectionSort.Level: return "레벨 높은 순 (몬스터 먼저)";
                case CollectionSort.Attack: return "공격력 높은 순 (몬스터 먼저)";
                case CollectionSort.Defense: return "수비력 높은 순 (몬스터 먼저)";
                case CollectionSort.Rarity: return "레어도 높은 순";
                default: return "몬스터 → 마법 → 함정 → 엑스트라 순";
            }
        }

        // ---- 오른쪽: 레시피 (메인·엑스트라·시작 사이드 덱) ----

        /// <summary>
        /// 가운데 돌판: 덱. 메인·엑스트라 덱은 스크롤 안에 이어 놓고, 시작 사이드 덱은 그 아래에 고정한다. 한 줄 10장.
        /// 맨 아래 줄에 방금 한 일의 알림(editor-notice).
        /// </summary>
        VisualElement EditorDeckPanel()
        {
            var panel = UiStyle.PanelBox("editor-deck");
            panel.style.paddingLeft = panel.style.paddingRight = 12;
            panel.style.paddingTop = 14;
            panel.style.paddingBottom = 12;
            _editorDeckScroll = new ScrollView(ScrollViewMode.Vertical);
            _editorDeckScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
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

            var hint = Text("덱의 카드를 두 번 누르거나 컬렉션으로 끌어다 놓으면 뺀다. 덱과 시작 사이드 덱 사이로 끌어다 놓으면 옮긴다. " +
                            $"같은 카드명은 덱과 시작 사이드 덱을 합쳐 {RunRules.MaxCopies}장까지", 15, UiStyle.TextDim, top: 8);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.flexShrink = 0;
            panel.Add(hint);
            var notice = Text(_editorNotice ?? "", 17, _editorNoticeIsError ? UiStyle.Bad : UiStyle.Good, top: 4);
            notice.name = "editor-notice";
            notice.style.whiteSpace = WhiteSpace.Normal;
            notice.style.flexShrink = 0;
            notice.style.display = _editorNotice == null ? DisplayStyle.None : DisplayStyle.Flex;
            panel.Add(notice);

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
            UiStyle.Padding(zone, 4);
            UiStyle.BorderColor(zone, problem ? UiStyle.Bad : UiStyle.Border, 1);
            var header = Row();
            header.style.alignItems = Align.FlexEnd;
            var titleLabel = UiStyle.Label(title, 21, problem ? UiStyle.Bad : UiStyle.Text, bold: true);
            titleLabel.name = name + "-count";
            header.Add(titleLabel);
            var noteLabel = Text(note, 15, UiStyle.TextDim);
            noteLabel.style.marginLeft = 10;
            noteLabel.style.marginBottom = 2;
            header.Add(noteLabel);
            zone.Add(header);

            var grid = Grid();
            grid.style.marginTop = 4;
            grid.style.minHeight = EditorCardWidth * 158f / 108f + 10;
            var cards = EditorCards(pile);
            if (cards.Count == 0)
                grid.Add(Text("비어 있음. 컬렉션의 카드를 여기로 끌어다 놓으면 넣는다.", 17, UiStyle.TextDim, top: 4));
            foreach (uint code in Sorted(cards))
            {
                uint c = code;
                var slot = CardSlot(pile, c, CardFaceSize.Small, tag: _editor.Left(c) < 0 ? "부족" : null,
                    onDoubleClick: () => EditorRemove(c, fromSide: pile == Pile.DeckSide), width: EditorCardWidth);
                UiStyle.Margin(slot, 0);
                grid.Add(slot);
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
                var hint = Text("카드에 마우스를 올리면 여기에 크게 보이고, 누르면 넣기·빼기 버튼이 나온다.", 19, UiStyle.TextDim);
                hint.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(hint);
                var rule = Text($"메인 덱 {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장, 엑스트라 덱 {RunRules.MaxExtraDeck}장 이하, " +
                                $"시작 사이드 덱 {RunRules.StartingSideDeckMax}장 이하. 같은 카드명은 에라타 전·후 버전을 합쳐 덱과 시작 사이드 덱에 {RunRules.MaxCopies}장까지. " +
                                $"메인 덱이 {RunRules.MinMainDeck}장보다 적으면 저장할 수 없다.", 17, UiStyle.TextDim, top: 10);
                rule.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(rule);
                return;
            }
            panel.Add(_faces.BuildDetail(code, 324, 600));
            int left = _editor.Left(code);
            var counts = Text($"컬렉션 {_editor.Owned(code)}장 · 덱 {_editor.Deck.Count(c => c == code)}장 · 사이드 덱 {_editor.Side.Count(c => c == code)}장 · " +
                              $"같은 카드명 {_editor.NameCopies(code)}/{RunRules.MaxCopies}장", 17, left < 0 ? UiStyle.Bad : UiStyle.TextDim, top: 8);
            counts.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(counts);
            if (!pinned)
                return;
            var actions = UiStyle.Box();
            actions.style.marginTop = 4;
            bool first = true;
            void AddAction(string text, string name, string reason, Action onClick)
            {
                // 첫 버튼(가장 흔한 일)이 금색이다
                var b = UiStyle.StoneButton(text, onClick, gold: first, small: !first);
                if (first)
                    b.style.fontSize = 23;
                first = false;
                b.name = name;
                b.style.marginTop = 8;
                b.style.paddingLeft = b.style.paddingRight = 8;
                b.SetEnabled(reason == null);
                actions.Add(b);
                if (reason != null)
                {
                    var why = Text(reason, 15, UiStyle.TextDim, top: 2);
                    why.style.whiteSpace = WhiteSpace.Normal;
                    actions.Add(why);
                }
            }
            if (pile == Pile.Collection)
            {
                string target = _editor.IsExtraDeckCard(code) ? "엑스트라 덱에 넣기" : "메인 덱에 넣기";
                AddAction(target, "editor-add", _editor.AddReason(code, false), () => EditorAdd(code, toSide: false));
                AddAction("시작 사이드 덱에 넣기", "editor-add-side", _editor.AddReason(code, true), () => EditorAdd(code, toSide: true));
                if (left <= 0)
                {
                    var over = Text("컬렉션의 장수를 모두 넣었다. 더 넣으면 저장은 되지만 그 레시피로는 런을 시작할 수 없다.", 15, Warning, top: 6);
                    over.style.whiteSpace = WhiteSpace.Normal;
                    actions.Add(over);
                }
            }
            else
            {
                bool side = pile == Pile.DeckSide;
                AddAction("빼기", "editor-remove", null, () => EditorRemove(code, fromSide: side));
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
