// 런 화면: 처음 화면과 레시피 창(새 런, 덱 편집). docs/UI.md "나머지 창 > 처음 화면, 새 런". RunScreen.cs의 머리말 참고.
// - 처음 화면: 탑 장면 위에 제목, 오른쪽 아래 명령 메뉴(이어하기, 새 런, 덱 편집, 골드 상점, 이벤트 도감, 끝내기),
//   왼쪽 아래 판(이어 할 런, 컬렉션, 골드). 골드 상점은 RunScreen.GoldShop.cs.
// - 레시피 창(recipe-window): 처음 화면 위에 뜨는 큰 돌판. 왼쪽 레시피 목록(recipe-<Id>), 가운데 고른 레시피의 카드(탭),
//   오른쪽 상세 카드(start-card-detail)·시드·시작. "새 런"으로 열면 금색 버튼이 "이 덱으로 시작"이고,
//   "덱 편집"으로 열면 "이 레시피를 편집"이다.
// - 사이드 덱 임시 확장(run-side-slots, 새 런일 때만): 레시피의 시작 사이드 덱에서 영구 한도를 넘는 카드가 임시 칸의 카드다.
//   이번 런에 몇 장 살지 좌우 키나 − + 버튼으로 정한다 (1장에 골드 8). 산 만큼 앞에서부터 가져가고 나머지는 두고 간다.
// 레시피 고치기는 덱 편집 화면(RunScreen.Deck.cs)에서 한다. 런 시작 확인은 RecipeRules·RunState.CheckSetup(Run 어셈블리)이 한다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        enum RecipeWindow
        {
            Closed,
            NewRun,
            Edit,
        }

        const float StartCardWidth = 92, StartDetailHeight = 540;
        /// <summary>사이드 덱 임시 확장 칸이 보일 때 상세 카드를 이만큼 줄인다 (오른쪽 열에 그 칸이 들어갈 자리).</summary>
        const float RunSideBlockHeight = 96;
        static readonly Color TitleInk = UiStyle.Hex(0x1b140b);

        /// <summary>레시피 창에서 고른 레시피 Id (없어졌으면 첫 레시피).</summary>
        string _startRecipeId;
        string _seedText;
        /// <summary>
        /// 새 런 창에서 이번 런에 살 사이드 덱 임시 칸 수. -1이면 아직 정하지 않았다 (살 수 있는 만큼 다 사는 것으로 시작한다).
        /// 레시피를 바꾸거나 창을 새로 열면 -1로 되돌린다.
        /// </summary>
        int _runSideSlots = -1;
        /// <summary>레시피 창의 상세 카드 높이 (임시 확장 칸이 보이면 그만큼 줄어든다).</summary>
        float _startDetailHeight = StartDetailHeight;
        ScrollView _recipeScroll;
        Vector2 _recipeScrollOffset;
        bool _confirmAbandon;
        /// <summary>레시피 창이 열려 있는지와 그 쓰임새. 덱 편집 화면에 다녀와도 이어진다.</summary>
        RecipeWindow _recipeWindow;
        /// <summary>레시피 창에서 보고 있는 탭 (메인, 엑스트라, 시작 사이드).</summary>
        Pile _startTab = Pile.DeckMain;
        MenuList _titleMenu;
        int _titleMenuIndex;
        MenuList _recipeMenu;
        Button _recipeGo;

        PlayerRecipe StartRecipe => _save.FindRecipe(_startRecipeId ?? "") ?? _save.Recipes.FirstOrDefault();

        bool TitleKeysOpen => _titleMenu != null && _titleMenu.Root.panel != null;

        bool RecipeWindowOpen => _recipeMenu != null && _recipeMenu.Root.panel != null && _modalLayer.style.display != DisplayStyle.None;

        /// <summary>층 사이 화면에서 처음 화면으로 (새 시드).</summary>
        void GoToStart()
        {
            _seedText = NewSeed().ToString();
            _confirmAbandon = false;
            _recipeWindow = RecipeWindow.Closed;
            ShowStart();
        }

        /// <summary>런 종료 화면의 "새 런": 처음 화면 위에 새 런 창을 연 채로.</summary>
        void GoToNewRun()
        {
            _seedText = NewSeed().ToString();
            _confirmAbandon = false;
            _recipeWindow = RecipeWindow.NewRun;
            ShowStart();
        }

        void ShowStart()
        {
            StopClimb();
            _run = null;
            _editor = null;
            if (_recipeScroll != null && _recipeScroll.panel != null)
                _recipeScrollOffset = _recipeScroll.scrollOffset;
            BeginScreen();
            _startRecipeId = StartRecipe?.Id;

            var stage = NewStage("title-stage");
            stage.Add(new TowerScene { Marker = 0, Opponent = 0 });
            var title = UiStyle.Box("title-name");
            title.pickingMode = PickingMode.Ignore;
            title.style.position = Position.Absolute;
            title.style.right = 64;
            title.style.top = 36;
            title.style.alignItems = Align.FlexEnd;
            var name = UiStyle.Label("DuelRoguelike", 104, TitleInk);
            UiStyle.Black(name);
            title.Add(name);
            var sub = UiStyle.Label("탑을 오르는 듀얼 로그라이크 · 비상업 팬게임", 26, TitleInk, bold: true);
            sub.style.marginTop = -8;
            title.Add(sub);
            stage.Add(title);
            stage.Add(TitleInfoPanel());
            stage.Add(TitleMenuPanel());
            ApplyKeyCaps();
            if (_recipeWindow != RecipeWindow.Closed)
                OpenRecipeWindow();
        }

        static readonly Pile[] StartPiles = { Pile.DeckMain, Pile.DeckExtra, Pile.DeckSide };

        /// <summary>왼쪽 아래 판: 알림, 이어 할 런, 컬렉션과 골드.</summary>
        VisualElement TitleInfoPanel()
        {
            var panel = new VisualElement { name = "continue-panel" };
            UiStyle.Slab(panel);
            panel.style.position = Position.Absolute;
            panel.style.left = 28;
            panel.style.bottom = 28;
            panel.style.width = 600;
            panel.style.paddingLeft = panel.style.paddingRight = 22;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 14;
            if (_saveNotice != null)
            {
                var notice = Text(_saveNotice, 17, UiStyle.Opponent);
                notice.name = "save-notice";
                notice.style.whiteSpace = WhiteSpace.Normal;
                notice.style.marginBottom = 8;
                panel.Add(notice);
            }
            if (_forfeitNotice != null)
            {
                var forfeit = Text(_forfeitNotice, 17, UiStyle.Opponent);
                forfeit.name = "forfeit-notice";
                forfeit.style.whiteSpace = WhiteSpace.Normal;
                forfeit.style.marginBottom = 8;
                panel.Add(forfeit);
            }
            var run = _save.Run;
            if (run != null)
            {
                panel.Add(Text("이어 할 런", 19, UiStyle.TextDim));
                var deck = UiStyle.Label($"{run.StartDeckName} · {run.Floor}층", 27, UiStyle.Text, bold: true);
                deck.style.whiteSpace = WhiteSpace.NoWrap;
                panel.Add(deck);
                string record = $"{run.Wins}승 {run.Losses}패" + (run.Draws > 0 ? $" {run.Draws}무" : "");
                string state = run.Phase == RunPhase.NeedsRefill ? " · 보충 고르는 중" : run.Phase == RunPhase.ChoosingReward ? " · 승리 보상 고르는 중" : "";
                var summary = Text($"{record} · LP {run.Lp} · 칩 {run.Chips} · 봉인 {run.SealedMain.Count + run.SealedExtra.Count}장 · 끝낸 듀얼 {run.DuelsPlayed} · 시드 {run.Seed}{state}", 19, UiStyle.TextDim);
                summary.name = "continue-summary";
                summary.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(summary);
                var warn = Text("듀얼 도중에 게임을 끄면 그 듀얼은 패배로 처리된다.", 15, UiStyle.TextDim, top: 2);
                panel.Add(warn);
            }
            var owned = Text($"컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장 · 골드 {_save.Gold}", 19, UiStyle.TextDim, top: run != null ? 8 : 0);
            panel.Add(owned);
            var path = Text("저장: " + GameSaveFile.FilePath(_saveDir), 13, UiStyle.TextDim, top: 2);
            path.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(path);
            return panel;
        }

        /// <summary>오른쪽 아래 명령 메뉴. 진행 중인 런이 있으면 이어하기가 맨 위이고 처음에 골라져 있다.</summary>
        VisualElement TitleMenuPanel()
        {
            var panel = new VisualElement { name = "title-menu" };
            UiStyle.Slab(panel);
            panel.style.position = Position.Absolute;
            panel.style.right = 28;
            panel.style.bottom = 28;
            panel.style.width = 540;
            UiStyle.Padding(panel, 16);
            panel.style.paddingBottom = 14;
            var menu = new MenuList();
            var run = _save.Run;
            if (run != null)
                menu.Add("continue-run", "이어하기", ContinueRun, $"{run.Floor}층 · 듀얼 {run.DuelsPlayed}판");
            menu.Add("title-new-run", "새 런", () => OpenRecipes(RecipeWindow.NewRun));
            menu.Add("title-edit", "덱 편집", () => OpenRecipes(RecipeWindow.Edit), $"레시피 {_save.Recipes.Count}개");
            menu.Add("open-gold-shop", "골드 상점", OpenGoldShop, $"골드 {_save.Gold}", subName: "title-gold");
            menu.Add("open-codex", "이벤트 도감", OpenCodex, $"{_save.Codex.SeenCount} / {TowerEventList.All.Count}");
            menu.Add("open-settings", "설정", OpenSettings);
            menu.Add("title-quit", "끝내기", () => Application.Quit());
            menu.Select(Mathf.Clamp(_titleMenuIndex, 0, menu.Count - 1));
            _titleMenu = menu;
            panel.Add(menu.Root);
            var hints = Hints(("↑", "W", null), ("↓", "S", "고르기"), ("Z", "J", "결정"));
            hints.style.marginTop = 10;
            hints.style.paddingLeft = 10;
            panel.Add(hints);
            return panel;
        }

        void TitleKey(Nav nav)
        {
            switch (nav)
            {
                case Nav.Up:
                    _titleMenu.Move(-1);
                    break;
                case Nav.Down:
                    _titleMenu.Move(1);
                    break;
                case Nav.Confirm:
                    _titleMenuIndex = _titleMenu.Index;
                    _titleMenu.Activate();
                    return;
            }
            _titleMenuIndex = _titleMenu.Index;
        }

        void ContinueRun()
        {
            if (_save.Run == null)
                return;
            _run = _save.Run;
            _recipeWindow = RecipeWindow.Closed;
            ResetRunView();
            Debug.Log($"[런] 이어하기: {_run.StartDeckName}, {_run.Floor}층, 듀얼 {_run.DuelsPlayed}판, 시드 {_run.Seed}");
            ShowHub();
        }

        /// <summary>런을 시작하거나 이어 할 때: 고른 카드·알림·스크롤을 처음 상태로.</summary>
        void ResetRunView()
        {
            _confirmAbandon = false;
            _selectedPile = Pile.Main;
            _selectedCode = 0;
            _refillSelected = 0;
            _moveNotice = null;
            _deckScroll = null;
            _deckScrollOffset = Vector2.zero;
            // 층에 들어가면 한 번 뜨는 창(이벤트·휴식·칩교환소)은 런마다, 이어하기마다 다시 센다.
            _eventShownFloor = _restShownFloor = _shopShownFloor = 0;
            _eventResult = null;
            _shopProduct = null;
            ResetHubView();
            _shopRelease = 0;
        }

        // ================= 레시피 창 (새 런, 덱 편집) =================

        void OpenRecipes(RecipeWindow mode)
        {
            _recipeWindow = mode;
            _confirmAbandon = false;
            _runSideSlots = -1;
            OpenRecipeWindow();
        }

        /// <summary>새 런 창의 지금 레시피로 이번 런에 살 임시 칸 수: 정한 값을 살 수 있는 범위 안으로 맞춘 것 (정하지 않았으면 살 수 있는 만큼).</summary>
        int RunSideSlots(PlayerRecipe recipe)
        {
            int affordable = _save.RunSideSlotsAffordable(recipe);
            return _runSideSlots < 0 ? affordable : Mathf.Clamp(_runSideSlots, 0, affordable);
        }

        /// <summary>새 런 창: 이번 런에 살 임시 칸 수를 delta만큼 바꾼다 (좌우 키, − + 버튼).</summary>
        void ChangeRunSideSlots(int delta)
        {
            var recipe = StartRecipe;
            if (_recipeWindow != RecipeWindow.NewRun || recipe == null || _save.RunSideSlotsMax(recipe) == 0)
                return;
            int now = RunSideSlots(recipe), next = Mathf.Clamp(now + delta, 0, _save.RunSideSlotsAffordable(recipe));
            if (next == now)
                return;
            _runSideSlots = next;
            OpenRecipeWindow();
        }

        void CloseRecipeWindow()
        {
            _recipeWindow = RecipeWindow.Closed;
            _confirmAbandon = false;
            CloseModal();
        }

        void OpenRecipeWindow()
        {
            if (_recipeScroll != null && _recipeScroll.panel != null)
                _recipeScrollOffset = _recipeScroll.scrollOffset;
            var recipe = StartRecipe;
            _startRecipeId = recipe?.Id;
            bool newRun = _recipeWindow == RecipeWindow.NewRun;
            if (_selectedPile != _startTab || recipe == null || !RecipeCards(recipe, _selectedPile).Contains(_selectedCode))
            {
                _selectedPile = _startTab;
                _selectedCode = 0;
            }
            _cardViews.Clear(); // 처음 화면에는 카드가 없다. 이 창을 다시 그릴 때마다 비운다.

            var panel = OpenSlab("recipe-window", 1800);
            panel.style.height = 980;
            panel.style.paddingLeft = panel.style.paddingRight = 26;
            panel.style.paddingTop = 22;
            panel.style.paddingBottom = 18;
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.style.flexShrink = 0;
            head.Add(WindowTitle(newRun ? "새 런" : "덱 레시피"));
            var problems = recipe == null ? new List<string>() : RecipeRules.StartProblems(recipe, _data.Database, _collection, _save.SideDeckLimit).ToList();
            var status = Text(recipe == null ? "덱 레시피가 없다. \"빈 새 레시피\"로 컬렉션에서 덱을 짠다."
                : problems.Count == 0 ? "이 레시피로 런을 시작할 수 있다." : "런을 시작할 수 없다: " + string.Join(" / ", problems),
                19, recipe != null && problems.Count == 0 ? UiStyle.Good : UiStyle.Bad);
            status.name = "start-recipe-status";
            status.style.marginLeft = 18;
            status.style.marginBottom = 7;
            status.style.flexShrink = 1;
            status.style.whiteSpace = WhiteSpace.Normal;
            head.Add(status);
            panel.Add(head);

            var body = Row();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.marginTop = 14;
            body.Add(RecipeListColumn(recipe));
            body.Add(RecipeCardsColumn(recipe));
            body.Add(RecipeSideColumn(recipe, problems.Count == 0));
            panel.Add(body);
            bool sideSlots = newRun && recipe != null && _save.RunSideSlotsMax(recipe) > 0;
            panel.Add(WindowFooter(!newRun
                ? Hints(("↑", "W", null), ("↓", "S", "레시피"), ("Q", "Q", null), ("E", "E", "탭"), ("Z", "J", "편집"), ("X", "K", "뒤로"))
                : sideSlots
                    ? Hints(("↑", "W", null), ("↓", "S", "레시피"), ("Q", "Q", null), ("E", "E", "탭"), ("←", "A", null), ("→", "D", "임시 확장"),
                        ("Z", "J", "시작"), ("X", "K", "뒤로"))
                    : Hints(("↑", "W", null), ("↓", "S", "레시피"), ("Q", "Q", null), ("E", "E", "탭"), ("Z", "J", "시작"), ("X", "K", "뒤로"))));
            ApplyKeyCaps();

            _renderDetail = RenderStartDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        /// <summary>왼쪽: 레시피 목록과 그 아래 편집 버튼 셋.</summary>
        VisualElement RecipeListColumn(PlayerRecipe recipe)
        {
            var column = UiStyle.Box("start-setup");
            column.style.width = 340;
            column.style.flexShrink = 0;
            _recipeScroll = new ScrollView(ScrollViewMode.Vertical);
            _recipeScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _recipeScroll.style.flexGrow = 1;
            _recipeScroll.style.flexShrink = 1;
            var menu = new MenuList { HoverSelects = false };
            int chosenIndex = 0;
            for (int i = 0; i < _save.Recipes.Count; i++)
            {
                var r = _save.Recipes[i];
                if (r == recipe)
                    chosenIndex = i;
                bool startable = RecipeRules.StartProblems(r, _data.Database, _collection, _save.SideDeckLimit).Count == 0;
                var row = menu.AddRow("recipe-" + r.Id, () => ChooseRecipe(r), height: 0);
                var texts = UiStyle.Box();
                texts.pickingMode = PickingMode.Ignore;
                texts.style.flexShrink = 1;
                var name = RowLabel(r.Name, 23, UiStyle.Text, bold: true);
                name.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(name);
                texts.Add(RowLabel(RecipeCounts(r), 17, UiStyle.TextDim));
                string rarity = RarityLine(r);
                if (rarity.Length > 0)
                    texts.Add(RowLabel(rarity, 17, UiStyle.TextDim));
                if (!startable)
                    texts.Add(RowLabel("시작 불가", 17, UiStyle.Bad, bold: true));
                row.Add(texts);
                if (!startable)
                    row.style.opacity = 0.7f;
            }
            if (_save.Recipes.Count > 0)
                menu.Select(chosenIndex);
            _recipeMenu = menu;
            _recipeScroll.Add(menu.Root);
            column.Add(_recipeScroll);
            var scroll = _recipeScroll;
            var offset = _recipeScrollOffset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).ExecuteLater(10);

            var buttons = Row();
            buttons.style.marginTop = 12;
            buttons.style.flexShrink = 0;
            buttons.Add(RecipeButton("edit-recipe", "덱 편집", () => OpenEditor(recipe), recipe != null, 0));
            buttons.Add(RecipeButton("copy-recipe", "복사해서 새로", () => OpenEditor(recipe, copy: true), recipe != null, 6));
            buttons.Add(RecipeButton("new-recipe", "빈 새 레시피", () => OpenEditor(null), true, 6));
            column.Add(buttons);
            return column;
        }

        static Button RecipeButton(string name, string text, Action run, bool enabled, float left)
        {
            var button = UiStyle.StoneButton(text, run, small: true);
            button.name = name;
            button.style.fontSize = 17;
            button.style.flexGrow = 1;
            button.style.paddingLeft = button.style.paddingRight = 4;
            button.style.marginLeft = left;
            button.SetEnabled(enabled);
            return button;
        }

        /// <summary>가운데: 고른 레시피의 카드. 탭(메인, 엑스트라, 시작 사이드)과 한 줄 10장의 격자.</summary>
        VisualElement RecipeCardsColumn(PlayerRecipe recipe)
        {
            var column = UiStyle.Box("start-recipe");
            column.style.width = 10 * (StartCardWidth + 8) + 28; // 세로 스크롤 막대 자리까지
            column.style.marginLeft = 24;
            column.style.flexShrink = 0;
            var tabs = Row();
            tabs.style.alignItems = Align.Center;
            tabs.style.flexShrink = 0;
            foreach (var pile in StartPiles)
            {
                var p = pile;
                int count = recipe == null ? 0 : RecipeCards(recipe, pile).Count;
                string label = pile == Pile.DeckMain ? "메인" : pile == Pile.DeckExtra ? "엑스트라" : "시작 사이드";
                var tab = UiStyle.StoneButton($"{label} {count}", () =>
                {
                    _startTab = p;
                    OpenRecipeWindow();
                }, gold: pile == _startTab, small: true);
                tab.name = "start-tab-" + (pile == Pile.DeckMain ? "main" : pile == Pile.DeckExtra ? "extra" : "side");
                tab.style.width = 190;
                tab.style.marginRight = 6;
                tabs.Add(tab);
            }
            if (recipe != null)
            {
                var name = Text(recipe.Name, 19, UiStyle.TextDim);
                name.name = "start-recipe-name";
                name.style.marginLeft = StyleKeyword.Auto;
                name.style.whiteSpace = WhiteSpace.NoWrap;
                tabs.Add(name);
            }
            column.Add(tabs);
            if (recipe == null)
                return column;

            var cards = RecipeCards(recipe, _startTab);
            bool sideTab = _startTab == Pile.DeckSide;
            int sideLimit = _save.SideDeckLimit, tempCards = _save.RunSideSlotsMax(recipe);
            string sideNote = tempCards == 0
                ? $"시작 사이드 덱 {sideLimit}장까지. 런을 시작할 때 사이드 덱에 들어간다."
                : $"시작 사이드 덱 {sideLimit}장까지. 그 뒤의 {tempCards}장은 임시 칸의 카드이고, 런마다 1장에 골드 {RunRules.RunSideDeckSlotPrice}로 가져간다.";
            var info = Text(_startTab == Pile.DeckMain ? KindCounts(cards)
                : _startTab == Pile.DeckExtra ? $"엑스트라 덱 {RunRules.MaxExtraDeck}장 이하"
                : sideNote, 17, UiStyle.TextDim, top: 8);
            info.name = "start-tab-note";
            column.Add(info);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 8;
            var grid = Grid(scroll.contentContainer);
            if (cards.Count == 0)
                grid.Add(Text("비어 있음", 19, UiStyle.TextDim));
            // 시작 사이드 덱은 순서가 칸이라 정렬하지 않는다: 앞에서부터 영구 칸, 그 뒤가 임시 칸.
            var ordered = sideTab ? cards : Sorted(cards).ToList();
            int bought = _recipeWindow == RecipeWindow.NewRun ? RunSideSlots(recipe) : tempCards;
            for (int i = 0; i < ordered.Count; i++)
            {
                uint code = ordered[i];
                string tag = _collection.Count(code) < RecipeUsed(recipe, code) ? "부족" : null;
                Color? tagColor = null;
                bool left = false;
                if (sideTab && i >= sideLimit && tag == null)
                {
                    // 임시 칸의 카드: 이번 런에 산 만큼은 "임시", 사지 않은 것은 "두고 감"
                    left = i >= sideLimit + bought;
                    tag = left ? "두고 감" : "임시";
                    tagColor = left ? UiStyle.Bad : UiStyle.Gold;
                }
                var slot = CardSlot(_startTab, code, CardFaceSize.Small, tag: tag, dim: left, draggable: false, width: StartCardWidth, tagColor: tagColor);
                slot.focusable = false;
                UiStyle.Margin(slot, 0);
                grid.Add(slot);
            }
            column.Add(scroll);
            return column;
        }

        /// <summary>
        /// 새 런 창 오른쪽의 사이드 덱 임시 확장 칸 (run-side-slots): 이번 런에 살 임시 칸 수를 − + 버튼이나 좌우 키로 정한다.
        /// 임시 칸에 든 카드 수와 가진 골드 안에서만 고를 수 있다. 값은 런을 시작할 때 낸다.
        /// </summary>
        VisualElement RunSideSlotsBlock(PlayerRecipe recipe)
        {
            int max = _save.RunSideSlotsMax(recipe), affordable = _save.RunSideSlotsAffordable(recipe), slots = RunSideSlots(recipe);
            var block = UiStyle.Box("run-side-slots");
            block.style.marginTop = 12;
            block.style.paddingTop = 8;
            block.style.borderTopWidth = 2;
            block.style.borderTopColor = UiStyle.SlabTop;
            block.style.flexShrink = 0;
            var head = Row();
            head.Add(Text("사이드 덱 임시 확장", 17, UiStyle.TextDim));
            // 골드가 모자라 임시 칸의 카드를 다 가져갈 수 없으면 가진 골드를 내림 색으로 쓴다
            var have = Text($"가진 골드 {_save.Gold}", 17, affordable < max ? UiStyle.Bad : UiStyle.TextDim);
            have.name = "run-side-gold";
            have.style.marginLeft = StyleKeyword.Auto;
            head.Add(have);
            block.Add(head);

            var row = Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6;
            Button Step(string name, string text, int delta, bool enabled)
            {
                var button = UiStyle.StoneButton(text, () => ChangeRunSideSlots(delta), small: true);
                button.name = name;
                button.style.width = 52;
                button.style.fontSize = 24;
                button.style.paddingLeft = button.style.paddingRight = 0;
                button.SetEnabled(enabled);
                return button;
            }
            row.Add(Step("run-side-less", "−", -1, slots > 0));
            var count = UiStyle.Label($"{slots}장", 27);
            UiStyle.Black(count);
            count.name = "run-side-count";
            count.style.width = 76;
            count.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(count);
            row.Add(Step("run-side-more", "+", 1, slots < affordable));
            var price = UiStyle.Label($"골드 {RunRules.RunSideDeckSlotsPrice(slots)}", 24, slots > 0 ? UiStyle.Gold : UiStyle.TextDim);
            UiStyle.Black(price);
            price.name = "run-side-price";
            price.style.marginLeft = StyleKeyword.Auto;
            price.style.whiteSpace = WhiteSpace.NoWrap;
            row.Add(price);
            block.Add(row);
            return block;
        }

        /// <summary>오른쪽: 상세 카드, 시드, 시작(또는 편집) 버튼.</summary>
        VisualElement RecipeSideColumn(PlayerRecipe recipe, bool startable)
        {
            bool newRun = _recipeWindow == RecipeWindow.NewRun;
            bool sideSlots = newRun && recipe != null && _save.RunSideSlotsMax(recipe) > 0;
            _startDetailHeight = StartDetailHeight - (sideSlots ? RunSideBlockHeight : 0);
            var column = UiStyle.Box();
            column.style.width = 324;
            column.style.marginLeft = StyleKeyword.Auto;
            column.style.flexShrink = 0;
            var detail = UiStyle.Box("start-card-detail");
            detail.style.height = _startDetailHeight + 34;
            detail.style.flexShrink = 0;
            column.Add(detail);
            _detail = detail;

            var error = Text("", 17, UiStyle.Bad, top: 6);
            error.name = "start-error";
            error.style.display = DisplayStyle.None;
            error.style.whiteSpace = WhiteSpace.Normal;
            if (!newRun)
            {
                _recipeGo = UiStyle.StoneButton("이 레시피를 편집", () => OpenEditor(recipe), gold: true);
                _recipeGo.name = "edit-recipe-go";
                _recipeGo.style.marginTop = 12;
                _recipeGo.style.fontSize = 23;
                _recipeGo.SetEnabled(recipe != null);
                column.Add(_recipeGo);
                column.Add(RecipeBackButton("start-back", "뒤로", CloseRecipeWindow));
                return column;
            }

            var seedRow = Row();
            seedRow.style.alignItems = Align.Center;
            seedRow.style.marginTop = 12;
            seedRow.style.flexShrink = 0;
            var seedLabel = Text("시드", 19, UiStyle.TextDim);
            seedLabel.style.width = 46;
            seedRow.Add(seedLabel);
            var field = DarkTextField("seed", _seedText, 20);
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.RegisterValueChangedCallback(e => _seedText = e.newValue);
            seedRow.Add(field);
            var reroll = UiStyle.StoneButton("무작위", () =>
            {
                _seedText = NewSeed().ToString();
                OpenRecipeWindow();
            }, small: true);
            reroll.name = "seed-random";
            reroll.style.width = 92;
            reroll.style.fontSize = 17;
            reroll.style.paddingLeft = reroll.style.paddingRight = 4;
            reroll.style.marginLeft = 8;
            seedRow.Add(reroll);
            column.Add(seedRow);
            if (sideSlots)
                column.Add(RunSideSlotsBlock(recipe));
            column.Add(error);

            if (_confirmAbandon && _save.Run != null)
            {
                var warn = Text($"진행 중인 런 「{_save.Run.StartDeckName}」 {_save.Run.Floor}층을 포기하고 새 런을 시작한다. 그 런에서 얻은 카드는 컬렉션에 남는다.", 17, UiStyle.Opponent, top: 8);
                warn.style.whiteSpace = WhiteSpace.Normal;
                column.Add(warn);
                _recipeGo = UiStyle.StoneButton("포기하고 새 런 시작", () => StartRun(error), gold: true);
                _recipeGo.name = "start-run";
                _recipeGo.style.marginTop = 8;
                _recipeGo.style.fontSize = 23;
                column.Add(_recipeGo);
                column.Add(RecipeBackButton("start-cancel", "취소", () =>
                {
                    _confirmAbandon = false;
                    OpenRecipeWindow();
                }));
                return column;
            }
            bool hasRun = _save.Run != null;
            _recipeGo = UiStyle.StoneButton(hasRun ? "진행 중인 런을 포기하고 시작" : "이 덱으로 시작", () =>
            {
                if (_save.Run != null)
                {
                    _confirmAbandon = true;
                    OpenRecipeWindow();
                }
                else
                    StartRun(error);
            }, gold: true);
            _recipeGo.name = "start-run";
            _recipeGo.style.marginTop = 12;
            _recipeGo.style.fontSize = hasRun ? 20 : 25;
            _recipeGo.style.paddingLeft = _recipeGo.style.paddingRight = 6;
            column.Add(_recipeGo);
            column.Add(RecipeBackButton("start-back", "뒤로", CloseRecipeWindow));
            return column;
        }

        Button RecipeBackButton(string name, string text, Action run)
        {
            var back = UiStyle.StoneButton(text, run, small: true);
            back.name = name;
            back.style.marginTop = 8;
            var key = KeyCap("X", "K");
            key.pickingMode = PickingMode.Ignore;
            key.style.position = Position.Absolute;
            key.style.right = 9;
            key.style.top = 8;
            back.Add(key);
            return back;
        }

        void RecipeWindowKey(Nav nav)
        {
            switch (nav)
            {
                case Nav.Up:
                case Nav.Down:
                {
                    var recipes = _save.Recipes;
                    if (recipes.Count == 0)
                        return;
                    int index = Mathf.Max(0, recipes.ToList().IndexOf(StartRecipe));
                    int next = Mathf.Clamp(index + (nav == Nav.Down ? 1 : -1), 0, recipes.Count - 1);
                    if (next != index)
                        ChooseRecipe(recipes[next]);
                    break;
                }
                case Nav.PrevTab:
                case Nav.NextTab:
                {
                    int tab = Array.IndexOf(StartPiles, _startTab);
                    _startTab = StartPiles[(tab + (nav == Nav.NextTab ? 1 : StartPiles.Length - 1)) % StartPiles.Length];
                    OpenRecipeWindow();
                    break;
                }
                case Nav.Left:
                    ChangeRunSideSlots(-1); // 새 런: 사이드 덱 임시 확장
                    break;
                case Nav.Right:
                    ChangeRunSideSlots(1);
                    break;
                case Nav.Confirm:
                    Press(_recipeGo);
                    break;
                case Nav.Cancel:
                    if (_confirmAbandon)
                    {
                        _confirmAbandon = false;
                        OpenRecipeWindow();
                    }
                    else
                        CloseRecipeWindow();
                    break;
            }
        }

        List<uint> RecipeCards(PlayerRecipe recipe, Pile pile)
        {
            switch (pile)
            {
                case Pile.DeckMain: return recipe.Deck.Where(c => !IsExtraDeckCard(c)).ToList();
                case Pile.DeckExtra: return recipe.Deck.Where(IsExtraDeckCard).ToList();
                case Pile.DeckSide: return recipe.Side.ToList();
                default: return new List<uint>();
            }
        }

        static int RecipeUsed(PlayerRecipe recipe, uint code) => recipe.Deck.Count(c => c == code) + recipe.Side.Count(c => c == code);

        static string PileTitle(Pile pile) =>
            pile == Pile.DeckMain ? "메인 덱" : pile == Pile.DeckExtra ? "엑스트라 덱" : pile == Pile.DeckSide ? "시작 사이드 덱" : pile.ToString();

        /// <summary>"메인 40 · 엑스트라 2 · 사이드 3"</summary>
        string RecipeCounts(PlayerRecipe recipe)
        {
            int extra = recipe.Deck.Count(IsExtraDeckCard);
            return $"메인 {recipe.Deck.Count - extra} · 엑스트라 {extra} · 사이드 {recipe.Side.Count}";
        }

        void RenderStartDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
            {
                var hint = Text("카드에 마우스를 올리면 여기에 크게 보인다.", 19, UiStyle.TextDim);
                hint.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(hint);
                var rule = Text($"메인 덱 {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장, 엑스트라 덱 {RunRules.MaxExtraDeck}장 이하, " +
                                $"시작 사이드 덱 {_save.SideDeckLimit}장과 임시 칸 {RunRules.RunSideDeckExtraMax}장. " +
                                $"같은 카드명은 덱과 시작 사이드 덱을 합쳐 {RunRules.MaxCopies}장까지.", 17, UiStyle.TextDim, top: 10);
                rule.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(rule);
                return;
            }
            panel.Add(_faces.BuildDetail(code, 324, _startDetailHeight));
            var recipe = StartRecipe;
            int used = recipe == null ? 0 : RecipeUsed(recipe, code);
            panel.Add(Text($"컬렉션 {_collection.Count(code)}장 · 이 레시피 {used}장", 17,
                used > _collection.Count(code) ? UiStyle.Bad : UiStyle.TextDim, top: 8));
        }

        void ChooseRecipe(PlayerRecipe recipe)
        {
            if (_startRecipeId != recipe.Id)
                _runSideSlots = -1; // 레시피가 바뀌면 임시 칸은 살 수 있는 만큼으로 되돌린다
            _startRecipeId = recipe.Id;
            _selectedCode = 0;
            _confirmAbandon = false;
            OpenRecipeWindow();
        }

        /// <summary>"UR 3 · SR 3 · R 9 · N 35" (등급 표에 있는 카드만, 없으면 빈 글)</summary>
        string RarityLine(PlayerRecipe recipe)
        {
            var counts = new int[RarityTable.RarityCount];
            foreach (uint code in recipe.Deck)
                if (_rarities.TryGet(code, out var rarity))
                    counts[(int)rarity]++;
            var parts = Enumerable.Range(0, counts.Length).Reverse().Where(i => counts[i] > 0).Select(i => $"{(Rarity)i} {counts[i]}").ToList();
            return string.Join(" · ", parts);
        }

        void StartRun(Label error)
        {
            if (!ulong.TryParse((_seedText ?? "").Trim(), out ulong seed))
            {
                ShowError(error, "시드는 0 이상의 정수로 적는다.");
                return;
            }
            var recipe = StartRecipe;
            if (recipe == null)
            {
                ShowError(error, "덱 레시피가 없다. 먼저 새 레시피를 만든다.");
                return;
            }
            var problems = RecipeRules.StartProblems(recipe, _data.Database, _collection, _save.SideDeckLimit);
            // 이번 런에 살 임시 칸: 진행 중인 런을 포기하기 전의 골드로 정한 값 그대로 (포기하면 정산 골드가 더해질 뿐 줄지 않는다)
            int sideSlots = RunSideSlots(recipe);
            string slotsProblem = _save.RunSideSlotsProblem(recipe, sideSlots);
            if (slotsProblem != null)
                problems.Add(slotsProblem);
            if (problems.Count > 0)
            {
                ShowError(error, "런을 시작할 수 없다: " + string.Join(" / ", problems) + " · 덱 편집에서 고친다");
                return;
            }
            if (_save.Run != null)
            {
                var old = _save.Run;
                Debug.Log($"[런] 포기: {old.StartDeckName}, {old.Floor}층, 듀얼 {old.DuelsPlayed}판, 시드 {old.Seed}");
                _save.AbandonRun();
            }
            _run = _save.StartRun(recipe, seed, StarterDecks.All, _data.Database, _rarities, sideSlots);
            _forfeitNotice = null;
            _recipeWindow = RecipeWindow.Closed;
            _runSideSlots = -1;
            Persist();
            ResetRunView();
            Debug.Log($"[런] 시작: {recipe.Name} ({recipe.Id}), 시드 {seed}, 시작 사이드 덱 {_run.SideDeck.Count}장" +
                      (sideSlots > 0 ? $" (임시 칸 {sideSlots}장, 골드 {RunRules.RunSideDeckSlotsPrice(sideSlots)}, 남은 골드 {_save.Gold})" : ""));
            ShowHub();
        }

        static void ShowError(Label label, string text)
        {
            label.text = text;
            label.style.display = DisplayStyle.Flex;
        }
    }
}
