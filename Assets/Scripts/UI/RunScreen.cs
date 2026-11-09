// 로그라이크 런 화면 (UI Toolkit). 설계 문서 "로그라이크 진행".
// 화면: 런 시작 → 층 사이 화면 ⇄ 듀얼 화면(DuelScreen) → … → 런 종료 → 런 시작.
// - 런 시작: 시작 덱(스타터 덱 4개)과 시드를 고른다. 시작 사이드 덱은 비어 있다 (고르는 화면은 아직 없음).
// - 층 사이 화면: 층·LP·연패·전적, 직전 듀얼 결과와 런 로그, 메인·엑스트라·사이드 덱과 기절한 카드, 누른 카드의 정보와 교환 버튼.
//   "다음 듀얼 시작"을 누르면 RunState.BeginDuel로 상대·덱·LP·시드를 정해 듀얼 화면에 맡긴다.
// - 보충 고르기: 메인 덱이 40장 미만(NeedsRefill)이면 층 사이 화면 위에 뜨는 창. 닫을 수 없고, 다 채우면 저절로 닫힌다.
// - 런 종료: 도달한 층, 전적, 얻은 카드, 런 로그.
// 런 규칙은 모두 RunState(Run 어셈블리)에 있고, 이 화면은 그것을 부르고 보여 주기만 한다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed class RunScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("듀얼 화면. 비워 두면 같은 게임 오브젝트에서 찾고, 없으면 붙인다.")]
        DuelScreen duelScreen;

        /// <summary>층 사이 화면의 카드 목록.</summary>
        enum Pile
        {
            Main,
            Extra,
            Side,
            Fainted,
        }

        static readonly Color Positive = UiStyle.Hex(0x5ee07a);
        static readonly Color Cyan = UiStyle.Hex(0x4fd1e8);

        CardDataSet _data;
        DuelText _text;
        RarityTable _rarities;
        CardCollection _collection;
        RunState _run;

        DeckRecipe _startDeck = StarterDecks.Yugi;
        string _seedText;

        // 층 사이 화면에서 누른 카드 (0이면 없음)
        Pile _selectedPile;
        uint _selectedCode;
        uint _refillSelected;
        ScrollView _deckScroll;
        Vector2 _deckScrollOffset;

        VisualElement _docRoot, _root, _modalLayer;

        void Awake()
        {
            if (duelScreen == null)
                duelScreen = GetComponent<DuelScreen>();
            if (duelScreen == null)
                duelScreen = gameObject.AddComponent<DuelScreen>();
            duelScreen.UseExternalDuels();
        }

        void Start()
        {
            _docRoot = UiStyle.CreateDocument(transform, "RunUI");
            _root = UiStyle.Box("run");
            _root.style.flexGrow = 1;
            _root.style.backgroundColor = UiStyle.Background;
            UiStyle.Padding(_root, 16);
            _docRoot.Add(_root);

            _modalLayer = new VisualElement { name = "run-modal" };
            _modalLayer.style.position = Position.Absolute;
            _modalLayer.style.left = _modalLayer.style.top = _modalLayer.style.right = _modalLayer.style.bottom = 0;
            _modalLayer.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            _modalLayer.style.justifyContent = Justify.Center;
            _modalLayer.style.alignItems = Align.Center;
            _modalLayer.style.display = DisplayStyle.None;
            _docRoot.Add(_modalLayer);

            try
            {
                LoadData();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowLoadError(e);
                return;
            }
            duelScreen.Attach(_data);
            _seedText = NewSeed().ToString();
            ShowStart();
        }

        void LoadData()
        {
            // 에디터에서는 저장소의 External 폴더를 바로 읽는다 (DuelScreen과 같다). 실행 파일 빌드용 경로는 나중에 정한다.
            string externalRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "External"));
            _data = CardDataSet.Load(externalRoot);
            _text = new DuelText(_data.Database, _data.Strings, 0);
            _rarities = RarityTable.Load(Path.Combine(Application.streamingAssetsPath, RarityTable.DefaultRelativePath));
            // 저장이 아직 없어서 게임을 켤 때마다 스타터 덱 4개로 시작하고, 런에서 얻은 카드는 게임을 끌 때까지 남는다.
            _collection = CardCollection.FromRecipes(StarterDecks.All);
            Debug.Log($"[런] 카드 {_data.CardCount}장, 한글 {_data.LocalizedCount}장, 등급 표 {_rarities.Count}장, " +
                      $"컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장");
        }

        void SetVisible(bool visible) => _docRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        static int NewSeed() => new System.Random().Next(1, 1000000);

        // ================= 런 시작 =================

        void ShowStart()
        {
            _run = null;
            CloseModal();
            _root.Clear();
            var panel = CenterPanel(1240);
            panel.Add(UiStyle.Label("로그라이크 런", 40, UiStyle.Accent, bold: true));
            panel.Add(Text("한 층은 단판 듀얼 하나입니다. 이기면 다음 층으로 올라가고, 상대 덱의 카드 1장을 사이드 덱에 얻습니다. " +
                           "지면 같은 층을 새로 뽑은 상대와 다시 하고, 메인 덱의 카드가 기절합니다 (이번 런에서만 빠짐). " +
                           $"LP는 다음 듀얼로 이어지고, 지거나 보스 층({RunRules.BossFloorInterval}층마다) 직전에 전부 회복합니다. " +
                           $"메인 덱을 {RunRules.MinMainDeck}장으로 채울 수 없으면 런이 끝납니다.", 16, UiStyle.TextDim, top: 8));

            panel.Add(Heading("시작 덱"));
            var decks = Row();
            decks.style.flexWrap = Wrap.Wrap;
            foreach (var recipe in StarterDecks.All)
            {
                var chosen = recipe;
                var b = UiStyle.Button(DeckSummary(recipe), () =>
                {
                    _startDeck = chosen;
                    ShowStart();
                }, 16);
                b.name = "deck-" + recipe.Id;
                b.style.width = 290;
                b.style.marginRight = 10;
                UiStyle.SetSelected(b, recipe == _startDeck);
                decks.Add(b);
            }
            panel.Add(decks);

            panel.Add(Heading("시드"));
            var seedRow = Row();
            seedRow.style.alignItems = Align.Center;
            var field = new TextField { name = "seed", value = _seedText };
            field.style.width = 260;
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
            panel.Add(Text("같은 시드에서 듀얼 결과가 같으면 상대·보상·기절도 모두 같습니다.", 14, UiStyle.TextDim, top: 4));

            panel.Add(Heading("사이드 덱 · 컬렉션"));
            panel.Add(Text($"사이드 덱 0장으로 시작합니다 (시작 사이드 덱 고르기는 아직 없음). " +
                           $"컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장: 스타터 덱 4개와, 게임을 켠 뒤 런에서 얻은 카드 (저장은 아직 없음).",
                15, UiStyle.TextDim));

            var error = Text("", 16, UiStyle.Opponent, top: 12);
            error.name = "start-error";
            error.style.display = DisplayStyle.None;
            panel.Add(error);
            var start = UiStyle.Button("런 시작", () => StartRun(error), 24);
            start.name = "start-run";
            start.style.marginTop = 18;
            start.style.alignSelf = Align.FlexStart;
            start.style.paddingLeft = start.style.paddingRight = 28;
            panel.Add(start);
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
            return $"{recipe.Name}\n메인 {cards.Count - extra}장 · 엑스트라 {extra}장\n{rarities}";
        }

        void StartRun(Label error)
        {
            if (!ulong.TryParse((_seedText ?? "").Trim(), out ulong seed))
            {
                ShowError(error, "시드는 0 이상의 정수로 적으세요.");
                return;
            }
            var setup = new RunSetup { Seed = seed, StartDeck = _startDeck, Opponents = StarterDecks.All };
            var problems = RunState.CheckSetup(setup, _data.Database, _collection);
            if (problems.Count > 0)
            {
                ShowError(error, "런을 시작할 수 없습니다: " + string.Join(" / ", problems));
                return;
            }
            _run = RunState.Start(setup, _data.Database, _rarities, _collection);
            _selectedCode = 0;
            _refillSelected = 0;
            _deckScrollOffset = Vector2.zero;
            Debug.Log($"[런] 시작: {_startDeck.Name}, 시드 {seed}");
            ShowHub();
        }

        static void ShowError(Label label, string text)
        {
            label.text = text;
            label.style.display = DisplayStyle.Flex;
        }

        // ================= 층 사이 화면 =================

        void ShowHub()
        {
            if (_run.Phase == RunPhase.Ended)
            {
                ShowEnd();
                return;
            }
            if (_deckScroll != null)
                _deckScrollOffset = _deckScroll.scrollOffset;
            CloseModal();
            _root.Clear();
            FixSelection();

            _root.Add(HubTopBar());
            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            body.Add(HubLogPanel());
            body.Add(HubDeckPanel());
            body.Add(HubDetailPanel());
            _root.Add(body);

            if (_run.Phase == RunPhase.NeedsRefill)
                OpenRefill();
        }

        VisualElement HubTopBar()
        {
            var bar = UiStyle.PanelBox("run-status");
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.alignItems = Align.Center;
            UiStyle.Padding(bar, 14);

            var texts = UiStyle.Box();
            bool boss = RunRules.IsBossFloor(_run.Floor);
            texts.Add(UiStyle.Label($"{_run.Floor}층" + (boss ? " · 보스 층" : ""), 34, UiStyle.Accent, bold: true));
            string record = $"{_run.Wins}승 {_run.Losses}패" + (_run.Draws > 0 ? $" {_run.Draws}무" : "");
            texts.Add(Text($"LP {_run.Lp} · 연패 {_run.LosingStreak} · {record} · 끝낸 듀얼 {_run.DuelsPlayed}", 19, top: 4));
            texts.Add(Text($"시작 덱 {DeckName(_run.StartDeckId)} · 시드 {_run.Seed}", 14, UiStyle.TextDim, top: 2));
            bar.Add(texts);

            var right = UiStyle.Box();
            right.style.alignItems = Align.FlexEnd;
            var next = UiStyle.Button("다음 듀얼 시작", StartNextDuel, 22);
            next.name = "next-duel";
            next.style.paddingLeft = next.style.paddingRight = 24;
            next.style.unityTextAlign = TextAnchor.MiddleCenter;
            next.SetEnabled(_run.Phase == RunPhase.Ready);
            right.Add(next);
            string note = boss && _run.Lp < RunRules.FullLp
                ? $"보스 층: 듀얼을 시작하기 직전에 LP를 {RunRules.FullLp}로 회복합니다."
                : "상대는 듀얼을 시작할 때 스타터 덱 4개 중에서 무작위로 정해집니다.";
            right.Add(Text(note, 14, UiStyle.TextDim, top: 4));
            bar.Add(right);
            return bar;
        }

        VisualElement HubLogPanel()
        {
            var panel = UiStyle.PanelBox("run-log");
            panel.style.width = 560;
            panel.style.marginRight = 12;

            panel.Add(UiStyle.Label("직전 듀얼", 18, UiStyle.TextDim, bold: true));
            var last = _run.Events.Where(e => e.DuelNumber == _run.DuelsPlayed && e.DuelNumber > 0
                                              && e.Kind != RunEventKind.MovedToDeck && e.Kind != RunEventKind.MovedToSide).ToList();
            var lastBox = UiStyle.Box("last-duel");
            lastBox.style.marginTop = 6;
            lastBox.style.marginBottom = 12;
            if (last.Count == 0)
                lastBox.Add(Text("아직 듀얼하지 않았습니다. 1층부터 시작합니다.", 16, UiStyle.TextDim));
            foreach (var e in last)
                lastBox.Add(Text(e.Describe(_text.CardName), e.Kind == RunEventKind.DuelStarted ? 16 : 17, EventColor(e.Kind),
                    bold: e.Kind == RunEventKind.DuelWon || e.Kind == RunEventKind.DuelLost, top: 2));
            panel.Add(lastBox);

            panel.Add(UiStyle.Label("런 로그", 18, UiStyle.TextDim, bold: true));
            panel.Add(EventLog());
            return panel;
        }

        ScrollView EventLog()
        {
            var log = new ScrollView(ScrollViewMode.Vertical) { name = "run-events" };
            log.style.flexGrow = 1;
            log.style.marginTop = 6;
            foreach (var e in _run.Events)
            {
                bool head = e.Kind == RunEventKind.DuelStarted || e.Kind == RunEventKind.RunStarted;
                var line = Text((head ? "" : "    ") + e.Describe(_text.CardName), 15, EventColor(e.Kind), top: head ? 6 : 1);
                log.Add(line);
            }
            // 최근 일이 보이도록 맨 아래로. 배치가 정해지거나 화면 크기가 바뀔 때마다 다시 내린다.
            void ScrollToEnd(GeometryChangedEvent _) => log.scrollOffset = new Vector2(0, float.MaxValue);
            log.RegisterCallback<GeometryChangedEvent>(ScrollToEnd);
            log.contentContainer.RegisterCallback<GeometryChangedEvent>(ScrollToEnd);
            return log;
        }

        static Color EventColor(RunEventKind kind)
        {
            switch (kind)
            {
                case RunEventKind.RunStarted:
                case RunEventKind.DuelStarted:
                case RunEventKind.RefillRequired:
                case RunEventKind.RunEnded:
                    return UiStyle.Accent;
                case RunEventKind.DuelWon:
                case RunEventKind.RewardGained:
                    return Positive;
                case RunEventKind.DuelLost:
                case RunEventKind.CardFainted:
                    return UiStyle.Opponent;
                case RunEventKind.MovedToDeck:
                case RunEventKind.MovedToSide:
                case RunEventKind.LpRestored:
                case RunEventKind.RewardSubstituted:
                    return UiStyle.TextDim;
                default:
                    return UiStyle.Text;
            }
        }

        VisualElement HubDeckPanel()
        {
            var panel = UiStyle.PanelBox("run-decks");
            panel.style.flexGrow = 1;
            panel.style.marginRight = 12;
            _deckScroll = new ScrollView(ScrollViewMode.Vertical);
            _deckScroll.style.flexGrow = 1;
            panel.Add(_deckScroll);

            AddPile(Pile.Main, $"메인 덱 {_run.MainDeck.Count}장 ({RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장)");
            AddPile(Pile.Extra, $"엑스트라 덱 {_run.ExtraDeck.Count}장 (최대 {RunRules.MaxExtraDeck}장)");
            AddPile(Pile.Side, $"사이드 덱 {_run.SideDeck.Count}장 (승리 보상이 들어온다)");
            AddPile(Pile.Fainted, $"기절 {_run.FaintedMain.Count + _run.FaintedExtra.Count}장 (이번 런에서는 쓸 수 없음)");

            var scroll = _deckScroll;
            var offset = _deckScrollOffset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).ExecuteLater(10);
            return panel;
        }

        void AddPile(Pile pile, string title)
        {
            var header = UiStyle.Label(title, 18, UiStyle.TextDim, bold: true);
            header.style.marginTop = pile == Pile.Main ? 0 : 14;
            header.style.marginBottom = 4;
            _deckScroll.Add(header);
            var cards = Cards(pile);
            if (cards.Count == 0)
            {
                _deckScroll.Add(Text("비어 있음", 15, UiStyle.TextDim));
                return;
            }
            foreach (var (code, count) in Grouped(cards))
                _deckScroll.Add(CardRow(pile, code, count));
        }

        VisualElement CardRow(Pile pile, uint code, int count)
        {
            var row = UiStyle.Button("", () => Select(pile, code), 16);
            row.name = $"card-{pile.ToString().ToLowerInvariant()}-{code}";
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = row.style.marginBottom = 1;
            row.style.paddingTop = row.style.paddingBottom = 4;
            var name = UiStyle.Label($"{_text.CardName(code)}  ×{count}", 16, pile == Pile.Fainted ? UiStyle.TextDim : UiStyle.Text);
            name.style.flexShrink = 1;
            name.pickingMode = PickingMode.Ignore;
            var rarity = UiStyle.Label(RarityName(code), 15, RarityColor(code), bold: true);
            rarity.style.marginLeft = 10;
            rarity.pickingMode = PickingMode.Ignore;
            row.Add(name);
            row.Add(rarity);
            UiStyle.SetSelected(row, pile == _selectedPile && code == _selectedCode);
            return row;
        }

        void Select(Pile pile, uint code)
        {
            _selectedPile = pile;
            _selectedCode = code;
            ShowHub();
        }

        /// <summary>누른 카드가 그 목록에서 사라졌으면 (기절, 교환) 그 카드가 있는 다른 목록을 고른다. 아무 데도 없으면 고르지 않은 상태.</summary>
        void FixSelection()
        {
            if (_selectedCode == 0 || Cards(_selectedPile).Contains(_selectedCode))
                return;
            foreach (Pile pile in Enum.GetValues(typeof(Pile)))
            {
                if (Cards(pile).Contains(_selectedCode))
                {
                    _selectedPile = pile;
                    return;
                }
            }
            _selectedCode = 0;
        }

        VisualElement HubDetailPanel()
        {
            var panel = UiStyle.PanelBox("run-card-detail");
            panel.style.width = 440;
            if (_selectedCode == 0)
            {
                panel.Add(Text("카드를 누르면 정보와 교환 버튼이 여기에 나옵니다.", 17, UiStyle.TextDim));
                panel.Add(Text("듀얼 사이에는 사이드 덱과 메인·엑스트라 덱의 카드를 바꿀 수 있습니다. " +
                               $"메인 덱 {RunRules.MinMainDeck}~{RunRules.MaxMainDeck}장, 엑스트라 덱 {RunRules.MaxExtraDeck}장 이하, " +
                               $"같은 카드는 덱에 {RunRules.MaxCopies}장까지입니다.", 15, UiStyle.TextDim, top: 10));
                return panel;
            }
            uint code = _selectedCode;
            AddCardDetail(panel, code);
            int side = _run.SideDeck.Count(c => c == code);
            int fainted = _run.FaintedMain.Count(c => c == code) + _run.FaintedExtra.Count(c => c == code);
            panel.Add(Text($"덱 {_run.DeckCopies(code)}장 · 사이드 덱 {side}장 · 기절 {fainted}장 · 컬렉션 {_collection.Count(code)}장",
                14, UiStyle.TextDim, top: 8));

            var actions = UiStyle.Box();
            actions.style.marginTop = 10;
            switch (_selectedPile)
            {
                case Pile.Main:
                case Pile.Extra:
                {
                    bool ok = _run.CanMoveToSide(code, out string reason);
                    var b = UiStyle.Button("사이드 덱으로 빼기", () => MoveSelected(toDeck: false), 18);
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
                    var b = UiStyle.Button(target, () => MoveSelected(toDeck: true), 18);
                    b.name = "move-to-deck";
                    b.SetEnabled(ok);
                    actions.Add(b);
                    if (!ok)
                        actions.Add(Text(reason, 14, UiStyle.TextDim, top: 2));
                    break;
                }
                default:
                    actions.Add(Text("기절한 카드는 이번 런에서 쓸 수 없습니다. 다음 런에서는 다시 쓸 수 있습니다.", 15, UiStyle.TextDim));
                    break;
            }
            panel.Add(actions);
            return panel;
        }

        void MoveSelected(bool toDeck)
        {
            uint code = _selectedCode;
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
            // 옮긴 카드가 원래 목록에 더 없으면 옮겨 간 목록에서 계속 고른 상태로 둔다.
            if (!Cards(_selectedPile).Contains(code))
                _selectedPile = toDeck ? (_run.IsExtraDeckCard(code) ? Pile.Extra : Pile.Main) : Pile.Side;
            ShowHub();
        }

        // ================= 보충 고르기 =================

        void OpenRefill()
        {
            var candidates = _run.RefillCandidates();
            if (!candidates.Contains(_refillSelected))
                _refillSelected = 0;
            var panel = OpenModal(1360);
            panel.name = "refill";
            panel.style.height = Length.Percent(82);
            panel.Add(UiStyle.Label($"보충: 메인 덱 {_run.MainDeck.Count}장 → {RunRules.MinMainDeck}장", 28, UiStyle.Accent, bold: true));
            panel.Add(Text($"메인 덱이 {RunRules.MinMainDeck}장보다 적어졌습니다. 사이드 덱에서 메인 덱 카드 {_run.RefillNeeded}장을 더 골라 넣으세요. " +
                           $"같은 카드는 덱에 {RunRules.MaxCopies}장까지 넣을 수 있고, 다 채우면 이 창이 닫힙니다.", 16, UiStyle.TextDim, top: 6));
            var fainted = _run.Events.Where(e => e.DuelNumber == _run.DuelsPlayed && e.Kind == RunEventKind.CardFainted)
                .Select(e => _text.CardName(e.Code)).ToList();
            if (fainted.Count > 0)
                panel.Add(Text("이번 패배로 기절한 카드: " + string.Join(", ", fainted), 15, UiStyle.Opponent, top: 6));

            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var grid = scroll.contentContainer;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            foreach (uint candidate in candidates)
            {
                uint code = candidate;
                int side = _run.SideDeck.Count(c => c == code);
                var b = UiStyle.Button($"{_text.CardName(code)}\n[{RarityName(code)}] 사이드 {side}장 · 덱 {_run.DeckCopies(code)}장", () =>
                {
                    _refillSelected = code;
                    OpenRefill();
                }, 15);
                b.name = $"refill-{code}";
                b.style.width = 270;
                b.style.marginRight = 8;
                UiStyle.SetSelected(b, code == _refillSelected);
                grid.Add(b);
            }
            body.Add(scroll);

            var detail = UiStyle.PanelBox();
            detail.style.width = 440;
            detail.style.marginLeft = 12;
            detail.style.backgroundColor = UiStyle.PanelLight;
            if (_refillSelected == 0)
                detail.Add(Text("왼쪽에서 넣을 카드를 고르세요.", 17, UiStyle.TextDim));
            else
            {
                AddCardDetail(detail, _refillSelected);
                var confirm = UiStyle.Button("메인 덱에 넣기", ConfirmRefill, 20);
                confirm.name = "refill-confirm";
                confirm.style.marginTop = 10;
                detail.Add(confirm);
            }
            body.Add(detail);
            panel.Add(body);
        }

        void ConfirmRefill()
        {
            try
            {
                _run.MoveToDeck(_refillSelected);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            ShowHub(); // 아직 모자라면 보충 창을 다시 연다
        }

        // ================= 듀얼 =================

        void StartNextDuel()
        {
            if (_run == null || _run.Phase != RunPhase.Ready)
                return;
            RunDuel duel;
            try
            {
                duel = _run.BeginDuel();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowHub();
                return;
            }
            // 임시 자동 응답·WindBot 난수는 화면 밖 런 시험(HeadlessRunTest)과 같은 식으로 정한다.
            int helperSeed = RunRandom.For(_run.Seed, duel.DuelNumber, RunRandomPurpose.External).NextSeed();
            string floor = $"{duel.Floor}층" + (duel.IsBossFloor ? " (보스 층)" : "");
            CloseModal();
            SetVisible(false);
            Debug.Log($"[런] {floor} 듀얼 {duel.DuelNumber}: 상대 {duel.Opponent.Name}, 나의 LP {duel.PlayerLp}");
            duelScreen.BeginExternalDuel(duel.CreateSessionOptions(), helperSeed, helperSeed + 7777,
                $"나 · {floor}", $"상대 · {duel.Opponent.Name}",
                $"{floor} 듀얼 {duel.DuelNumber} (런 시드 {_run.Seed}) — 나: LP {duel.PlayerLp}, 상대: {duel.Opponent.Name} LP {duel.OpponentLp}, 나의 선공, 상대 AI: WindBot",
                OnDuelLeft);
        }

        void OnDuelLeft(DuelState state)
        {
            try
            {
                _run.FinishDuel(state);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            var lastResult = _run.Events.LastOrDefault(e => e.Kind == RunEventKind.DuelWon || e.Kind == RunEventKind.DuelLost);
            Debug.Log($"[런] 듀얼 {_run.DuelsPlayed} 끝: {lastResult?.Describe(_text.CardName)} → {_run.Phase}, {_run.Floor}층, LP {_run.Lp}");
            SetVisible(true);
            if (_run.Phase == RunPhase.Ended)
                ShowEnd();
            else
                ShowHub();
        }

        // ================= 런 종료 =================

        void ShowEnd()
        {
            CloseModal();
            _root.Clear();
            var panel = CenterPanel(1500);
            panel.name = "run-end";
            panel.style.height = Length.Percent(94);
            panel.Add(UiStyle.Label("런 종료", 40, UiStyle.Accent, bold: true));
            string reason = _run.EndReason == RunEndReason.DeckBelowMinimum
                ? $"패배로 메인 덱이 {RunRules.MinMainDeck}장보다 적어졌는데, 사이드 덱에서 채울 카드가 모자라 런이 끝났습니다."
                : "런이 끝났습니다.";
            panel.Add(Text(reason, 17, top: 6));
            panel.Add(Text($"시작 덱 {DeckName(_run.StartDeckId)} · 시드 {_run.Seed}", 14, UiStyle.TextDim, top: 2));

            var rewards = _run.Events.Where(e => e.Kind == RunEventKind.RewardGained).ToList();
            var stats = Row();
            stats.style.marginTop = 14;
            stats.Add(Stat("도달한 층", $"{_run.Floor}층"));
            stats.Add(Stat("끝낸 듀얼", $"{_run.DuelsPlayed}판"));
            stats.Add(Stat("전적", $"{_run.Wins}승 {_run.Losses}패" + (_run.Draws > 0 ? $" {_run.Draws}무" : "")));
            stats.Add(Stat("얻은 카드", $"{rewards.Count}장"));
            stats.Add(Stat("기절", $"메인 {_run.FaintedMain.Count} · 엑스트라 {_run.FaintedExtra.Count}"));
            panel.Add(stats);

            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 14;
            var left = UiStyle.PanelBox();
            left.style.width = 560;
            left.style.marginRight = 12;
            left.style.backgroundColor = UiStyle.PanelLight;
            left.Add(UiStyle.Label("얻은 카드 (컬렉션에 더해짐)", 18, UiStyle.TextDim, bold: true));
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.flexGrow = 1;
            list.style.marginTop = 6;
            if (rewards.Count == 0)
                list.Add(Text("없음", 15, UiStyle.TextDim));
            foreach (var e in rewards)
            {
                var line = Text($"{e.Floor}층  [{e.Rarity}] {_text.CardName(e.Code)}", 16, top: 2);
                line.style.color = RarityColor(e.Rarity);
                list.Add(line);
            }
            left.Add(list);
            var substituted = Enumerable.Range(0, RarityTable.RarityCount).Where(i => _run.SubstitutedRewards[i] > 0)
                .Select(i => $"{(Rarity)i} {_run.SubstitutedRewards[i]}회").ToList();
            if (substituted.Count > 0)
                left.Add(Text("포인트 대체: " + string.Join(" · ", substituted) + " (지급량은 아직 정하지 않음)", 14, UiStyle.TextDim, top: 6));
            body.Add(left);

            var right = UiStyle.PanelBox();
            right.style.flexGrow = 1;
            right.style.backgroundColor = UiStyle.PanelLight;
            right.Add(UiStyle.Label("런 로그", 18, UiStyle.TextDim, bold: true));
            right.Add(EventLog());
            body.Add(right);
            panel.Add(body);

            var again = UiStyle.Button("새 런", () =>
            {
                _seedText = NewSeed().ToString();
                ShowStart();
            }, 22);
            again.name = "new-run";
            again.style.marginTop = 14;
            again.style.alignSelf = Align.FlexStart;
            again.style.paddingLeft = again.style.paddingRight = 28;
            panel.Add(again);
            Debug.Log($"[런] 종료: {_run.Floor}층, 듀얼 {_run.DuelsPlayed}판, {_run.Wins}승 {_run.Losses}패 {_run.Draws}무, 얻은 카드 {rewards.Count}장");
        }

        static VisualElement Stat(string label, string value)
        {
            var box = UiStyle.Box();
            box.style.backgroundColor = UiStyle.PanelLight;
            UiStyle.Radius(box, 6);
            UiStyle.Padding(box, 12);
            box.style.marginRight = 10;
            box.style.minWidth = 200;
            box.Add(UiStyle.Label(label, 14, UiStyle.TextDim));
            box.Add(UiStyle.Label(value, 26, bold: true));
            return box;
        }

        // ================= 카드 =================

        IReadOnlyList<uint> Cards(Pile pile)
        {
            switch (pile)
            {
                case Pile.Main: return _run.MainDeck;
                case Pile.Extra: return _run.ExtraDeck;
                case Pile.Side: return _run.SideDeck;
                default: return _run.FaintedMain.Concat(_run.FaintedExtra).ToList();
            }
        }

        /// <summary>같은 카드를 묶어 장수와 함께. 몬스터 → 마법 → 함정, 그 안에서는 카드명 순.</summary>
        IEnumerable<(uint Code, int Count)> Grouped(IEnumerable<uint> cards) =>
            cards.GroupBy(c => c)
                .Select(g => (Code: g.Key, Count: g.Count()))
                .OrderBy(g => KindOrder(g.Code))
                .ThenBy(g => _text.CardName(g.Code), StringComparer.Ordinal);

        int KindOrder(uint code)
        {
            var type = _text.Record(code)?.Type ?? OcgCardType.None;
            return (type & OcgCardType.Spell) != 0 ? 1 : (type & OcgCardType.Trap) != 0 ? 2 : 0;
        }

        bool IsExtraDeckCard(uint code) => _data.Database.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

        void AddCardDetail(VisualElement into, uint code)
        {
            var record = _text.Record(code);
            into.Add(UiStyle.Label(_text.CardName(code), 22, bold: true));
            if (record != null)
            {
                var data = record.Data;
                into.Add(Text(DuelText.TypeLine(record.Type, data.Level, (OcgAttribute)data.Attribute, data.Race), 15, UiStyle.TextDim, top: 6));
                if ((record.Type & (OcgCardType.Spell | OcgCardType.Trap)) == 0)
                    into.Add(Text($"ATK {StatValue(data.Attack)} / DEF {StatValue(data.Defense)}", 16, UiStyle.Accent, top: 4));
            }
            into.Add(Text($"등급 {RarityName(code)}", 15, RarityColor(code), bold: true, top: 4));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 10;
            scroll.Add(Text(record?.Description ?? "", 15));
            into.Add(scroll);
        }

        static string StatValue(int value) => value < 0 ? "?" : value.ToString();

        string RarityName(uint code) => _rarities.TryGet(code, out var rarity) ? rarity.ToString() : "-";

        Color RarityColor(uint code) => _rarities.TryGet(code, out var rarity) ? RarityColor(rarity) : UiStyle.TextDim;

        static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.R: return UiStyle.Text;
                case Rarity.SR: return Cyan;
                case Rarity.UR: return UiStyle.Accent;
                case Rarity.FR: return UiStyle.Opponent;
                default: return UiStyle.TextDim;
            }
        }

        static string DeckName(string id) => StarterDecks.Find(id ?? "")?.Name ?? id;

        // ================= 화면 도우미 =================

        void ShowLoadError(Exception e)
        {
            _root.Clear();
            var panel = CenterPanel(900);
            panel.Add(UiStyle.Label("런을 준비하지 못했습니다.", 26, UiStyle.Opponent, bold: true));
            string detail = e is DllNotFoundException
                ? $"네이티브 DLL이 없습니다. tools/build-ocgcore.ps1과 tools/fetch-sqlite.ps1을 실행하세요. ({e.Message})"
                : e.Message;
            panel.Add(Text(detail, 16, UiStyle.TextDim, top: 10));
        }

        /// <summary>화면 가운데에 놓는 판. 돌려준 판에 내용을 넣는다.</summary>
        VisualElement CenterPanel(float width)
        {
            var center = UiStyle.Box();
            center.style.flexGrow = 1;
            center.style.justifyContent = Justify.Center;
            center.style.alignItems = Align.Center;
            var panel = UiStyle.PanelBox();
            panel.style.width = width;
            panel.style.maxWidth = Length.Percent(100);
            UiStyle.Padding(panel, 24);
            center.Add(panel);
            _root.Add(center);
            return panel;
        }

        VisualElement OpenModal(float width)
        {
            _modalLayer.Clear();
            _modalLayer.style.display = DisplayStyle.Flex;
            var panel = UiStyle.PanelBox();
            panel.style.width = width;
            panel.style.maxWidth = Length.Percent(96);
            UiStyle.Padding(panel, 20);
            UiStyle.BorderColor(panel, UiStyle.Accent, 2);
            _modalLayer.Add(panel);
            return panel;
        }

        void CloseModal()
        {
            _modalLayer.Clear();
            _modalLayer.style.display = DisplayStyle.None;
        }

        static VisualElement Row() => UiStyle.Box(null, FlexDirection.Row);

        static Label Heading(string text)
        {
            var label = UiStyle.Label(text, 22, bold: true);
            label.style.marginTop = 22;
            label.style.marginBottom = 8;
            return label;
        }

        static Label Text(string text, int size, Color? color = null, bool bold = false, float top = 0)
        {
            var label = UiStyle.Label(text, size, color, bold);
            label.style.marginTop = top;
            return label;
        }
    }
}
