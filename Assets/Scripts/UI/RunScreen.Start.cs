// 런 화면: 런 시작 화면. RunScreen.cs의 머리말 참고.
// 배치: 위 제목·규칙, (진행 중인 런이 있으면) 이어하기 / 왼쪽 카드 정보(start-card-detail) /
// 가운데 고른 레시피의 메인·엑스트라·시작 사이드 덱 보기(start-recipe, 고칠 수 없음) / 오른쪽 새 런 설정(start-setup):
// 레시피 목록(recipe-<Id>), 덱 편집·복사·새 레시피 버튼, 시드, 런 시작.
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
        /// <summary>런 시작 화면에서 고른 레시피 Id (없어졌으면 첫 레시피).</summary>
        string _startRecipeId;
        string _seedText;
        ScrollView _recipeScroll;
        Vector2 _recipeScrollOffset;
        bool _confirmAbandon;

        PlayerRecipe StartRecipe => _save.FindRecipe(_startRecipeId ?? "") ?? _save.Recipes.FirstOrDefault();

        /// <summary>층 사이 화면이나 런 종료 화면에서 런 시작 화면으로 (새 시드).</summary>
        void GoToStart()
        {
            _seedText = NewSeed().ToString();
            _confirmAbandon = false;
            ShowStart();
        }

        void ShowStart()
        {
            StopClimb();
            _run = null;
            _editor = null;
            if (_recipeScroll != null)
                _recipeScrollOffset = _recipeScroll.scrollOffset;
            BeginScreen();
            var recipe = StartRecipe;
            _startRecipeId = recipe?.Id;
            if (!StartPiles.Contains(_selectedPile) || recipe == null || !RecipeCards(recipe, _selectedPile).Contains(_selectedCode))
            {
                _selectedPile = Pile.DeckMain;
                _selectedCode = 0;
            }

            var title = Row();
            title.style.alignItems = Align.FlexEnd;
            title.Add(UiStyle.Label("로그라이크 런", 40, UiStyle.Accent, bold: true));
            var rules = Text("한 층은 단판 듀얼 하나. 이기면 다음 층으로 가고 LP +" + RunRules.WinLpBonus +
                             $", 상대 덱에서 나온 후보 {RunRules.RewardChoices}장 중 1장을 사이드 덱에 넣습니다. 지면 같은 층을 다시 하고 메인 덱의 카드가 봉인됩니다. " +
                             $"LP는 이어지고({RunRules.FullLp}을 넘을 수 있음), 지면 {RunRules.FullLp}으로 회복합니다. 보스 층({RunRules.BossFloorInterval}층마다)은 {RunRules.FullLp}으로 시작하고, 이기면 {RunRules.FullLp}이 됩니다. " +
                             $"메인 덱을 {RunRules.MinMainDeck}장으로 채울 수 없으면 런이 끝납니다.",
                15, UiStyle.TextDim);
            rules.style.marginLeft = 18;
            rules.style.marginBottom = 6;
            rules.style.flexShrink = 1;
            rules.style.whiteSpace = WhiteSpace.Normal;
            title.Add(rules);
            var codex = UiStyle.Button($"이벤트 도감 ({_save.Codex.SeenCount}/{TowerEventList.All.Count})", OpenCodex, 16);
            codex.name = "open-codex";
            codex.style.marginLeft = StyleKeyword.Auto;
            codex.style.flexShrink = 0;
            title.Add(codex);
            _root.Add(title);

            if (_saveNotice != null)
            {
                var notice = Text(_saveNotice, 16, UiStyle.Opponent, top: 8);
                notice.name = "save-notice";
                _root.Add(notice);
            }
            if (_forfeitNotice != null)
            {
                var forfeit = Text(_forfeitNotice, 16, UiStyle.Opponent, top: 8);
                forfeit.name = "forfeit-notice";
                _root.Add(forfeit);
            }
            if (_save.Run != null)
                _root.Add(ContinuePanel(_save.Run));

            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            var detail = UiStyle.PanelBox("start-card-detail");
            detail.style.width = 380;
            detail.style.marginRight = 12;
            body.Add(detail);
            var center = RecipePreviewPanel(recipe);
            center.style.marginRight = 12;
            body.Add(center);
            body.Add(StartSetupPanel(recipe));
            _root.Add(body);

            _detail = detail;
            _renderDetail = RenderStartDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        static readonly Pile[] StartPiles = { Pile.DeckMain, Pile.DeckExtra, Pile.DeckSide };

        VisualElement ContinuePanel(RunState run)
        {
            var panel = UiStyle.PanelBox("continue-panel");
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.justifyContent = Justify.SpaceBetween;
            panel.style.alignItems = Align.Center;
            panel.style.marginTop = 10;
            UiStyle.BorderColor(panel, UiStyle.Accent, 2);
            var texts = UiStyle.Box();
            texts.Add(UiStyle.Label("진행 중인 런", 18, UiStyle.Accent, bold: true));
            string record = $"{run.Wins}승 {run.Losses}패" + (run.Draws > 0 ? $" {run.Draws}무" : "");
            string state = run.Phase == RunPhase.NeedsRefill ? " · 보충 고르는 중" : run.Phase == RunPhase.ChoosingReward ? " · 승리 보상 고르는 중" : "";
            var summary = Text($"{run.StartDeckName} · {run.Floor}층 · LP {run.Lp} · {record} · 끝낸 듀얼 {run.DuelsPlayed} · 시드 {run.Seed}{state}", 20, top: 4);
            summary.name = "continue-summary";
            texts.Add(summary);
            texts.Add(Text("듀얼 도중에 게임을 끄면 그 듀얼은 패배로 처리됩니다.", 14, UiStyle.TextDim, top: 2));
            panel.Add(texts);
            var resume = UiStyle.Button("이어하기", ContinueRun, 24);
            resume.name = "continue-run";
            resume.style.paddingLeft = resume.style.paddingRight = 32;
            panel.Add(resume);
            return panel;
        }

        void ContinueRun()
        {
            if (_save.Run == null)
                return;
            _run = _save.Run;
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

        // ---- 가운데: 고른 레시피 보기 ----

        VisualElement RecipePreviewPanel(PlayerRecipe recipe)
        {
            var panel = UiStyle.PanelBox("start-recipe");
            panel.style.flexGrow = 1;
            panel.style.flexShrink = 1;
            if (recipe == null)
            {
                panel.Add(UiStyle.Label("덱 레시피가 없습니다", 20, UiStyle.TextDim, bold: true));
                panel.Add(Text("오른쪽의 \"새 레시피\"로 컬렉션에서 덱을 짜세요.", 16, UiStyle.TextDim, top: 6));
                return panel;
            }
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            var name = UiStyle.Label(recipe.Name, 22, bold: true);
            name.name = "start-recipe-name";
            head.Add(name);
            var counts = Text(RecipeCounts(recipe), 15, UiStyle.TextDim);
            counts.style.marginLeft = 12;
            counts.style.marginBottom = 3;
            head.Add(counts);
            panel.Add(head);

            var problems = RecipeRules.StartProblems(recipe, _data.Database, _collection);
            var status = Text(problems.Count == 0 ? "이 레시피로 런을 시작할 수 있습니다." : "런을 시작할 수 없습니다: " + string.Join(" / ", problems),
                15, problems.Count == 0 ? Positive : UiStyle.Opponent, top: 4);
            status.name = "start-recipe-status";
            status.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(status);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 6;
            panel.Add(scroll);
            foreach (var pile in StartPiles)
            {
                var cards = RecipeCards(recipe, pile);
                if (cards.Count == 0 && pile == Pile.DeckExtra)
                    continue;
                // 시작 사이드 덱은 스크롤 밖 아래에 고정한다 (메인 덱을 내려 보지 않아도 보이게).
                VisualElement parent = scroll;
                if (pile == Pile.DeckSide)
                {
                    parent = new VisualElement();
                    parent.style.flexShrink = 0;
                    panel.Add(parent);
                }
                parent.Add(SubHeading($"{PileTitle(pile)} {cards.Count}장" + (pile == Pile.DeckMain ? " · " + KindCounts(cards) : "")));
                var grid = Grid();
                if (cards.Count == 0)
                    grid.Add(Text("비어 있음", 15, UiStyle.TextDim));
                foreach (uint code in Sorted(cards))
                    grid.Add(CardSlot(pile, code, CardFaceSize.Small, tag: _collection.Count(code) < RecipeUsed(recipe, code) ? "부족" : null, draggable: false));
                parent.Add(grid);
            }
            return panel;
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

        /// <summary>"메인 40장 · 엑스트라 2장 · 사이드 3장"</summary>
        string RecipeCounts(PlayerRecipe recipe)
        {
            int extra = recipe.Deck.Count(IsExtraDeckCard);
            return $"메인 {recipe.Deck.Count - extra}장 · 엑스트라 {extra}장 · 사이드 {recipe.Side.Count}장";
        }

        void RenderStartDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
            {
                panel.Add(Text("카드에 마우스를 올리면 여기에 크게 보입니다.", 16, UiStyle.TextDim));
                panel.Add(Text("레시피는 오른쪽 \"덱 편집\"에서 컬렉션의 카드로 고칩니다. " +
                               $"메인 덱 {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장, 엑스트라 덱 {RunRules.MaxExtraDeck}장 이하, 시작 사이드 덱 {RunRules.StartingSideDeckMax}장 이하, " +
                               $"같은 카드명은 덱과 시작 사이드 덱을 합쳐 {RunRules.MaxCopies}장까지입니다.", 15, UiStyle.TextDim, top: 10));
                return;
            }
            AddCardDetail(panel, code, pinned);
            var recipe = StartRecipe;
            int used = recipe == null ? 0 : RecipeUsed(recipe, code);
            panel.Add(Text($"컬렉션 {_collection.Count(code)}장 · 이 레시피 {used}장", 14,
                used > _collection.Count(code) ? UiStyle.Opponent : UiStyle.TextDim, top: 6));
        }

        // ---- 오른쪽: 새 런 설정 ----

        VisualElement StartSetupPanel(PlayerRecipe recipe)
        {
            var panel = UiStyle.PanelBox("start-setup");
            panel.style.width = 440;
            panel.style.flexShrink = 0;
            panel.Add(UiStyle.Label("새 런", 22, bold: true));

            panel.Add(SubHeading($"덱 레시피 {_save.Recipes.Count}개"));
            _recipeScroll = new ScrollView(ScrollViewMode.Vertical);
            _recipeScroll.style.maxHeight = 330;
            _recipeScroll.style.flexShrink = 1;
            foreach (var r in _save.Recipes)
            {
                var chosen = r;
                bool startable = RecipeRules.StartProblems(r, _data.Database, _collection).Count == 0;
                string rarity = RarityLine(r);
                var b = UiStyle.Button($"{r.Name}{(startable ? "" : "  · 시작 불가")}\n{RecipeCounts(r)}{(rarity.Length == 0 ? "" : "\n" + rarity)}", () => ChooseRecipe(chosen), 15);
                b.name = "recipe-" + r.Id;
                b.style.marginBottom = 4;
                if (!startable)
                    b.style.color = UiStyle.Opponent;
                UiStyle.SetSelected(b, r == recipe);
                _recipeScroll.Add(b);
            }
            panel.Add(_recipeScroll);
            var scroll = _recipeScroll;
            var offset = _recipeScrollOffset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).ExecuteLater(10);

            var recipeButtons = Row();
            recipeButtons.style.marginTop = 4;
            var edit = UiStyle.Button("덱 편집", () => OpenEditor(recipe), 16);
            edit.name = "edit-recipe";
            edit.SetEnabled(recipe != null);
            recipeButtons.Add(edit);
            var copy = UiStyle.Button("복사해서 새 레시피", () => OpenEditor(recipe, copy: true), 16);
            copy.name = "copy-recipe";
            copy.style.marginLeft = 6;
            copy.SetEnabled(recipe != null);
            recipeButtons.Add(copy);
            var blank = UiStyle.Button("빈 새 레시피", () => OpenEditor(null), 16);
            blank.name = "new-recipe";
            blank.style.marginLeft = 6;
            recipeButtons.Add(blank);
            panel.Add(recipeButtons);

            panel.Add(SubHeading("시드"));
            var seedRow = Row();
            seedRow.style.alignItems = Align.Center;
            var field = DarkTextField("seed", _seedText, 18);
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(e => _seedText = e.newValue);
            seedRow.Add(field);
            var reroll = UiStyle.Button("무작위", () =>
            {
                _seedText = NewSeed().ToString();
                ShowStart();
            }, 16);
            reroll.name = "seed-random";
            reroll.style.marginLeft = 8;
            seedRow.Add(reroll);
            panel.Add(seedRow);
            var seedNote = Text("같은 시드에서 듀얼 결과가 같으면 상대·보상 후보·봉인도 모두 같습니다.", 13, UiStyle.TextDim, top: 4);
            seedNote.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(seedNote);

            var spacer = UiStyle.Box();
            spacer.style.flexGrow = 1;
            panel.Add(spacer);

            var owned = Text($"컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장 · 골드 {_save.Gold} (끝난 런의 골드를 더한 것)", 14, UiStyle.TextDim);
            owned.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(owned);
            var path = Text("저장: " + GameSaveFile.FilePath(_saveDir), 12, UiStyle.TextDim, top: 2);
            path.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(path);

            var error = Text("", 15, UiStyle.Opponent, top: 8);
            error.name = "start-error";
            error.style.display = DisplayStyle.None;
            error.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(error);

            if (_confirmAbandon && _save.Run != null)
            {
                var warn = Text($"진행 중인 런({_save.Run.StartDeckName} · {_save.Run.Floor}층)을 포기하고 새 런을 시작합니다. " +
                                "그 런에서 얻은 카드는 컬렉션에 남습니다.", 15, UiStyle.Opponent, top: 8);
                warn.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(warn);
                var buttons = Row();
                buttons.style.marginTop = 8;
                var confirm = UiStyle.Button("포기하고 새 런 시작", () => StartRun(error), 20);
                confirm.name = "start-run";
                buttons.Add(confirm);
                var cancel = UiStyle.Button("취소", () =>
                {
                    _confirmAbandon = false;
                    ShowStart();
                }, 20);
                cancel.name = "start-cancel";
                cancel.style.marginLeft = 8;
                buttons.Add(cancel);
                panel.Add(buttons);
            }
            else
            {
                bool hasRun = _save.Run != null;
                var start = UiStyle.Button(hasRun ? "진행 중인 런을 포기하고 새 런 시작" : "런 시작", () =>
                {
                    if (_save.Run != null)
                    {
                        _confirmAbandon = true;
                        ShowStart();
                    }
                    else
                        StartRun(error);
                }, hasRun ? 18 : 24);
                start.name = "start-run";
                start.style.marginTop = 10;
                start.style.unityTextAlign = TextAnchor.MiddleCenter;
                panel.Add(start);
            }
            return panel;
        }

        void ChooseRecipe(PlayerRecipe recipe)
        {
            _startRecipeId = recipe.Id;
            _selectedCode = 0;
            ShowStart();
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
                ShowError(error, "시드는 0 이상의 정수로 적으세요.");
                return;
            }
            var recipe = StartRecipe;
            if (recipe == null)
            {
                ShowError(error, "덱 레시피가 없습니다. 먼저 새 레시피를 만드세요.");
                return;
            }
            var problems = RecipeRules.StartProblems(recipe, _data.Database, _collection);
            if (problems.Count > 0)
            {
                ShowError(error, "런을 시작할 수 없습니다: " + string.Join(" / ", problems) + " (덱 편집에서 고치세요)");
                return;
            }
            if (_save.Run != null)
            {
                var old = _save.Run;
                Debug.Log($"[런] 포기: {old.StartDeckName}, {old.Floor}층, 듀얼 {old.DuelsPlayed}판, 시드 {old.Seed}");
                _save.AbandonRun();
            }
            _run = _save.StartRun(recipe, seed, StarterDecks.All, _data.Database, _rarities);
            _forfeitNotice = null;
            Persist();
            ResetRunView();
            Debug.Log($"[런] 시작: {recipe.Name} ({recipe.Id}), 시드 {seed}, 시작 사이드 덱 {recipe.Side.Count}장");
            ShowHub();
        }

        static void ShowError(Label label, string text)
        {
            label.text = text;
            label.style.display = DisplayStyle.Flex;
        }
    }
}
