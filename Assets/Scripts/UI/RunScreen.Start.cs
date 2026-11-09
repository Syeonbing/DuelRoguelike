// 런 화면: 런 시작 화면. RunScreen.cs의 머리말 참고.
// 배치: 위 제목·규칙, (진행 중인 런이 있으면) 이어하기 / 왼쪽 카드 정보(start-card-detail) /
// 가운데 컬렉션(start-collection, 같은 카드는 한 장에 ×더 넣을 수 있는 장수)과 그 아래 시작 사이드 덱(start-side) / 오른쪽 새 런 설정(start-setup).
// 컬렉션 → 시작 사이드 덱: 두 번 누르기, 끌어다 놓기, 카드 정보의 "사이드 덱에 넣기". 반대도 같다.
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
        /// <summary>컬렉션 보기 거르기.</summary>
        enum CollectionFilter
        {
            All,
            Monster,
            Spell,
            Trap,
            Extra,
        }

        DeckRecipe _startDeck = StarterDecks.Yugi;
        string _seedText;
        readonly List<uint> _startSide = new List<uint>();
        CollectionFilter _filter;
        ScrollView _pickScroll;
        Vector2 _pickScrollOffset;
        bool _confirmAbandon;

        /// <summary>층 사이 화면이나 런 종료 화면에서 런 시작 화면으로 (새 시드).</summary>
        void GoToStart()
        {
            _seedText = NewSeed().ToString();
            _moveNotice = null;
            ShowStart();
        }

        void ShowStart()
        {
            _run = null;
            if (_pickScroll != null)
                _pickScrollOffset = _pickScroll.scrollOffset;
            BeginScreen();
            if (_selectedPile != Pile.Collection && _selectedPile != Pile.StartSide)
            {
                _selectedPile = Pile.Collection;
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
            var center = UiStyle.Box();
            center.style.flexGrow = 1;
            center.style.flexShrink = 1;
            center.style.marginRight = 12;
            center.Add(CollectionPanel());
            center.Add(StartSidePanel());
            body.Add(center);
            body.Add(StartSetupPanel());
            _root.Add(body);

            _detail = detail;
            _renderDetail = RenderStartDetail;
            ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

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
            var summary = Text($"{DeckName(run.StartDeckId)} · {run.Floor}층 · LP {run.Lp} · {record} · 끝낸 듀얼 {run.DuelsPlayed} · 시드 {run.Seed}{state}", 20, top: 4);
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
            Debug.Log($"[런] 이어하기: {DeckName(_run.StartDeckId)}, {_run.Floor}층, 듀얼 {_run.DuelsPlayed}판, 시드 {_run.Seed}");
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
        }

        // ---- 오른쪽: 새 런 설정 ----

        VisualElement StartSetupPanel()
        {
            var panel = UiStyle.PanelBox("start-setup");
            panel.style.width = 420;
            panel.style.flexShrink = 0;
            panel.Add(UiStyle.Label("새 런", 22, bold: true));

            panel.Add(SubHeading("시작 덱"));
            foreach (var recipe in StarterDecks.All)
            {
                var chosen = recipe;
                var b = UiStyle.Button(DeckSummary(recipe), () => ChooseStartDeck(chosen), 15);
                b.name = "deck-" + recipe.Id;
                b.style.marginBottom = 4;
                UiStyle.SetSelected(b, recipe == _startDeck);
                panel.Add(b);
            }

            panel.Add(SubHeading("시드"));
            var seedRow = Row();
            seedRow.style.alignItems = Align.Center;
            var field = new TextField { name = "seed", value = _seedText };
            field.style.flexGrow = 1;
            field.style.fontSize = 18;
            // 기본 테마의 입력 칸은 흰 바탕이라 화면 글자색(밝은 색)이 보이지 않는다. 어두운 칸으로 바꾼다.
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = UiStyle.PanelLight;
                input.style.color = UiStyle.Text;
                UiStyle.BorderColor(input, UiStyle.Border, 1);
            }
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
                var warn = Text($"진행 중인 런({DeckName(_save.Run.StartDeckId)} · {_save.Run.Floor}층)을 포기하고 새 런을 시작합니다. " +
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

        void ChooseStartDeck(DeckRecipe deck)
        {
            _startDeck = deck;
            // 이 시작 덱으로 고른 적이 있으면 그 사이드 덱, 없으면 지금 고른 것을 남는 장수 안에서 그대로 둔다.
            var remembered = _save.LastSideDeck(deck.Id);
            SetStartSide(remembered.Count > 0 ? remembered : _startSide.ToList());
            ShowStart();
        }

        /// <summary>시작 사이드 덱을 정한다. 컬렉션에서 쓸 수 있는 장수, 장수 제한, 같은 카드명 제한 안의 카드만 남긴다.</summary>
        void SetStartSide(IEnumerable<uint> cards)
        {
            var wanted = cards.ToList();
            _startSide.Clear();
            foreach (uint code in wanted)
                if (_startSide.Count < RunRules.StartingSideDeckMax && Available(code) > 0)
                    _startSide.Add(code);
        }

        /// <summary>
        /// 시작 사이드 덱에 더 넣을 수 있는 장수: 컬렉션 − 시작 덱 − 이미 고른 사이드 덱 (같은 ID),
        /// 그리고 시작 덱과 사이드 덱을 합쳐 같은 카드명 RunRules.MaxCopies장까지 중 작은 쪽.
        /// </summary>
        int Available(uint code) => Math.Min(OwnedLeft(code), RunRules.MaxCopies - NameCopies(code));

        /// <summary>컬렉션 − 시작 덱 − 이미 고른 사이드 덱 (같은 ID).</summary>
        int OwnedLeft(uint code) =>
            _collection.Count(code) - _startDeck.Entries.Where(e => e.Code == code).Sum(e => e.Count) - _startSide.Count(c => c == code);

        /// <summary>시작 덱과 시작 사이드 덱을 합친 같은 카드명의 장수 (일러스트·에라타 ID를 함께 센다).</summary>
        int NameCopies(uint code)
        {
            var db = _data.Database;
            uint name = db.NameCode(code);
            return _startDeck.Entries.Where(e => db.NameCode(e.Code) == name).Sum(e => e.Count) + _startSide.Count(c => db.NameCode(c) == name);
        }

        string DeckSummary(DeckRecipe recipe)
        {
            var cards = recipe.ToCardList();
            int extra = cards.Count(IsExtraDeckCard);
            var counts = new int[RarityTable.RarityCount];
            foreach (uint code in cards)
                if (_rarities.TryGet(code, out var rarity))
                    counts[(int)rarity]++;
            string rarities = string.Join(" · ", Enumerable.Range(0, counts.Length).Reverse()
                .Where(i => counts[i] > 0).Select(i => $"{(Rarity)i} {counts[i]}"));
            return $"{recipe.Name}\n메인 {cards.Count - extra}장 · 엑스트라 {extra}장 · {rarities}";
        }

        void StartRun(Label error)
        {
            if (!ulong.TryParse((_seedText ?? "").Trim(), out ulong seed))
            {
                ShowError(error, "시드는 0 이상의 정수로 적으세요.");
                return;
            }
            var setup = new RunSetup { Seed = seed, StartDeck = _startDeck, SideDeck = _startSide.ToList(), Opponents = StarterDecks.All };
            var problems = RunState.CheckSetup(setup, _data.Database, _collection);
            if (problems.Count > 0)
            {
                ShowError(error, "런을 시작할 수 없습니다: " + string.Join(" / ", problems));
                return;
            }
            if (_save.Run != null)
            {
                var old = _save.Run;
                Debug.Log($"[런] 포기: {DeckName(old.StartDeckId)}, {old.Floor}층, 듀얼 {old.DuelsPlayed}판, 시드 {old.Seed}");
                _save.AbandonRun();
            }
            _run = _save.StartRun(setup, _data.Database, _rarities);
            _forfeitNotice = null;
            Persist();
            ResetRunView();
            Debug.Log($"[런] 시작: {_startDeck.Name}, 시드 {seed}, 시작 사이드 덱 {_startSide.Count}장");
            ShowHub();
        }

        static void ShowError(Label label, string text)
        {
            label.text = text;
            label.style.display = DisplayStyle.Flex;
        }

        // ---- 가운데: 컬렉션과 시작 사이드 덱 ----

        VisualElement CollectionPanel()
        {
            var panel = UiStyle.PanelBox("start-collection");
            panel.style.flexGrow = 1;
            panel.style.flexShrink = 1;

            var available = _collection.Cards.Keys.Where(code => Available(code) > 0).ToList();
            var head = Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.Add(UiStyle.Label("컬렉션 — 시작 사이드 덱에 넣을 수 있는 카드", 18, UiStyle.TextDim, bold: true));
            var filters = Row();
            foreach (CollectionFilter filter in Enum.GetValues(typeof(CollectionFilter)))
            {
                var f = filter;
                int n = available.Count(c => Matches(c, f));
                var b = UiStyle.Button($"{FilterName(f)} {n}", () =>
                {
                    _filter = f;
                    _pickScrollOffset = Vector2.zero;
                    _pickScroll = null;
                    ShowStart();
                }, 14);
                b.name = "filter-" + f.ToString().ToLowerInvariant();
                b.style.marginLeft = 4;
                UiStyle.SetSelected(b, f == _filter);
                filters.Add(b);
            }
            head.Add(filters);
            panel.Add(head);
            var note = Text($"×n은 더 넣을 수 있는 장수입니다 (시작 덱에 든 장수를 빼고, 시작 덱과 사이드 덱을 합쳐 같은 카드명 {RunRules.MaxCopies}장까지). " +
                            $"두 번 누르거나 아래 시작 사이드 덱으로 끌어다 놓으면 넣습니다. 런 중에도 같은 카드명은 덱에 {RunRules.MaxCopies}장까지입니다.", 13, UiStyle.TextDim, top: 2);
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);

            _pickScroll = new ScrollView(ScrollViewMode.Vertical);
            _pickScroll.style.flexGrow = 1;
            _pickScroll.style.marginTop = 6;
            var grid = Grid(_pickScroll.contentContainer);
            var shown = available.Where(c => Matches(c, _filter)).ToList();
            if (shown.Count == 0)
                grid.Add(Text("없음", 15, UiStyle.TextDim));
            foreach (uint code in Sorted(shown))
            {
                uint c = code;
                grid.Add(CardSlot(Pile.Collection, c, CardFaceSize.Medium, count: Available(c), onDoubleClick: () => AddToStartSide(c)));
            }
            panel.Add(_pickScroll);
            AddDropZone(panel, (pile, code) =>
            {
                if (pile == Pile.StartSide)
                    RemoveFromStartSide(code);
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

        VisualElement StartSidePanel()
        {
            var panel = UiStyle.PanelBox("start-side");
            panel.style.marginTop = 12;
            panel.style.flexShrink = 0;
            var head = Row();
            head.style.alignItems = Align.Center;
            var count = UiStyle.Label($"시작 사이드 덱 {_startSide.Count}/{RunRules.StartingSideDeckMax}장", 18, UiStyle.TextDim, bold: true);
            count.name = "side-count";
            head.Add(count);
            var note = Text("런 중에 메인·엑스트라 덱과 바꾸거나, 봉인으로 모자란 메인 덱을 채울 때 씁니다.", 13, UiStyle.TextDim);
            note.style.marginLeft = 12;
            note.style.flexGrow = 1;
            note.style.flexShrink = 1;
            head.Add(note);
            var clear = UiStyle.Button("비우기", () =>
            {
                _startSide.Clear();
                _moveNotice = null;
                if (_selectedPile == Pile.StartSide)
                    _selectedPile = Pile.Collection;
                ShowStart();
            }, 15);
            clear.name = "side-clear";
            clear.SetEnabled(_startSide.Count > 0);
            head.Add(clear);
            panel.Add(head);

            var notice = Text(_moveNotice ?? "", 15, _moveNoticeIsError ? UiStyle.Opponent : Positive, top: 2);
            notice.name = "move-notice";
            notice.style.display = _moveNotice == null ? DisplayStyle.None : DisplayStyle.Flex;
            panel.Add(notice);

            var grid = Grid();
            grid.style.marginTop = 4;
            grid.style.minHeight = CardFaces.SizeOf(CardFaceSize.Small).y + 14;
            if (_startSide.Count == 0)
                grid.Add(Text("비어 있음 — 컬렉션의 카드를 두 번 누르거나 여기로 끌어다 놓으면 넣습니다.", 15, UiStyle.TextDim, top: 4));
            foreach (uint code in Sorted(_startSide))
            {
                uint c = code;
                grid.Add(CardSlot(Pile.StartSide, c, CardFaceSize.Small, onDoubleClick: () => RemoveFromStartSide(c)));
            }
            panel.Add(grid);
            AddDropZone(panel, (pile, code) =>
            {
                if (pile == Pile.Collection)
                    AddToStartSide(code);
            });
            return panel;
        }

        /// <summary>시작 사이드 덱에 넣을 수 없는 까닭. 넣을 수 있으면 null.</summary>
        string AddReason(uint code) =>
            _startSide.Count >= RunRules.StartingSideDeckMax ? $"시작 사이드 덱은 {RunRules.StartingSideDeckMax}장까지"
            : NameCopies(code) >= RunRules.MaxCopies ? $"시작 덱과 사이드 덱을 합쳐 같은 카드명은 {RunRules.MaxCopies}장까지"
            : OwnedLeft(code) <= 0 ? "더 넣을 장수가 없음 (컬렉션 장수를 모두 씀)"
            : null;

        void AddToStartSide(uint code)
        {
            string reason = AddReason(code);
            if (reason != null)
            {
                SetMoveNotice($"넣을 수 없습니다 — {_text.CardName(code)}: {reason}", error: true);
                return;
            }
            _startSide.Add(code);
            _moveNotice = null;
            // 더 넣을 장수가 없으면 사이드 덱 쪽에서 계속 고른 상태로 (빼기 버튼이 보이게)
            if (_selectedCode == code && Available(code) <= 0)
                _selectedPile = Pile.StartSide;
            ShowStart();
        }

        void RemoveFromStartSide(uint code)
        {
            if (!_startSide.Remove(code))
                return;
            _moveNotice = null;
            if (_selectedCode == code && !_startSide.Contains(code))
                _selectedPile = Pile.Collection;
            ShowStart();
        }

        // ---- 왼쪽: 카드 정보 ----

        void RenderStartDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
            {
                panel.Add(Text("카드에 마우스를 올리면 여기에 크게 보이고, 누르면 정보와 넣기·빼기 버튼이 여기에 나옵니다.", 16, UiStyle.TextDim));
                return;
            }
            AddCardDetail(panel, code, pinned);
            int inDeck = _startDeck.Entries.Where(e => e.Code == code).Sum(e => e.Count);
            panel.Add(Text($"컬렉션 {_collection.Count(code)}장 · 시작 덱 {inDeck}장 · 사이드 덱 {_startSide.Count(c => c == code)}장 · " +
                           $"같은 카드명 {NameCopies(code)}/{RunRules.MaxCopies}장", 14, UiStyle.TextDim, top: 6));
            if (!pinned)
                return;
            var actions = UiStyle.Box();
            actions.style.marginTop = 8;
            if (pile == Pile.Collection)
            {
                string reason = AddReason(code);
                var add = UiStyle.Button("사이드 덱에 넣기", () => AddToStartSide(code), 18);
                add.name = "side-add";
                add.SetEnabled(reason == null);
                actions.Add(add);
                if (reason != null)
                    actions.Add(Text(reason, 14, UiStyle.TextDim, top: 2));
            }
            else
            {
                var remove = UiStyle.Button("사이드 덱에서 빼기", () => RemoveFromStartSide(code), 18);
                remove.name = "side-remove";
                remove.SetEnabled(_startSide.Contains(code));
                actions.Add(remove);
            }
            panel.Add(actions);
        }
    }
}
