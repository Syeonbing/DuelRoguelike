// 로그라이크 런 화면 (UI Toolkit). 설계 문서 "로그라이크 진행".
// 화면: 런 시작 → 층 사이 화면 ⇄ 듀얼 화면(DuelScreen) → … → 런 종료 → 런 시작.
// - 런 시작 (RunScreen.Start.cs): 진행 중인 런이 있으면 "이어하기". 새 런은 시작 덱(스타터 덱 4개), 시드,
//   시작 사이드 덱(컬렉션에서 10장까지)을 고른다. 진행 중인 런이 있을 때 새 런을 시작하려면 한 번 더 눌러 포기를 확인한다.
// - 층 사이 화면 (RunScreen.Hub.cs): 위 상태 줄(층·LP·전적·직전 듀얼), 왼쪽 카드 정보, 가운데 메인·엑스트라·사이드 덱과 기절한 카드를
//   카드 모양으로 늘어놓는다. 덱과 사이드 덱 사이는 끌어다 놓기, 두 번 누르기, 카드 정보의 버튼으로 옮긴다. 런 로그는 따로 여는 창.
//   "다음 듀얼 시작"을 누르면 RunState.BeginDuel로 상대·덱·LP·시드를 정해 듀얼 화면에 맡긴다. "처음 화면으로"는 저장된 채로 런 시작 화면으로.
// - 보충 고르기: 메인 덱이 40장 미만(NeedsRefill)이면 층 사이 화면 위에 뜨는 창. 닫을 수 없고, 다 채우면 저절로 닫힌다.
// - 런 종료: 도달한 층, 전적, 얻은 카드, 런 로그.
// 카드는 모두 CardFaces(텍스트 카드 템플릿)로 그린다. 마우스를 올리면 카드 정보 칸에 크게 보이고, 누르면 그 카드에 고정된다.
// 저장(GameSave): 런 시작, 듀얼 결과, 교환·보충마다 저장 파일에 쓴다. 듀얼 도중에는 쓰지 않아서, 듀얼 중에 게임을 끄면
// 다시 켰을 때 그 듀얼을 시작하기 전으로 돌아간다. 저장 폴더는 환경 변수 DUELROGUELIKE_SAVE_DIR(시험용), 없으면 persistentDataPath.
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
    public sealed partial class RunScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("듀얼 화면. 비워 두면 같은 게임 오브젝트에서 찾고, 없으면 붙인다.")]
        DuelScreen duelScreen;

        /// <summary>카드가 놓인 곳 (화면 요소 이름의 앞부분도 정한다).</summary>
        enum Pile
        {
            Main,
            Extra,
            Side,
            Fainted,
            /// <summary>런 시작 화면의 컬렉션</summary>
            Collection,
            /// <summary>런 시작 화면의 시작 사이드 덱</summary>
            StartSide,
            /// <summary>보충 창의 후보</summary>
            Refill,
        }

        static readonly Color Positive = UiStyle.Hex(0x5ee07a);

        CardDataSet _data;
        DuelText _text;
        RarityTable _rarities;
        CardFaces _faces;
        CardImages _images;
        GameSave _save;
        string _saveDir;
        /// <summary>저장 파일 문제 알림 (런 시작 화면 맨 위). 없으면 null.</summary>
        string _saveNotice;
        RunState _run;

        CardCollection _collection => _save.Collection;

        VisualElement _docRoot, _root, _modalLayer;

        // ---- 카드 고르기 (화면마다 다시 정한다) ----
        Pile _selectedPile;
        uint _selectedCode;
        /// <summary>지금 화면의 카드 정보 칸과, 그 칸을 채우는 방법 (pinned = 누른 카드, 아니면 마우스를 올린 카드 미리 보기).</summary>
        VisualElement _detail;
        Action<VisualElement, Pile, uint, bool> _renderDetail;
        readonly List<(Pile Pile, uint Code, VisualElement Slot)> _cardViews = new List<(Pile, uint, VisualElement)>();
        readonly List<(VisualElement Zone, Action<Pile, uint> OnDrop)> _dropZones = new List<(VisualElement, Action<Pile, uint>)>();

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
            _images = CardImages.FromGameFolder();
            _faces = new CardFaces(_text, _rarities, _images);
            string overrideDir = Environment.GetEnvironmentVariable("DUELROGUELIKE_SAVE_DIR");
            _saveDir = string.IsNullOrEmpty(overrideDir) ? Application.persistentDataPath : overrideDir;
            _save = GameSaveFile.Load(_saveDir, _data.Database, _rarities, out _saveNotice);
            if (_saveNotice != null)
                Debug.LogWarning("[런] " + _saveNotice);
            _startDeck = StarterDecks.Find(_save.LastStartDeckId ?? "") ?? StarterDecks.Yugi;
            SetStartSide(_save.LastSideDeck(_startDeck.Id));
            Debug.Log($"[런] 카드 {_data.CardCount}장, 한글 {_data.LocalizedCount}장, 등급 표 {_rarities.Count}장, " +
                      $"카드 그림 폴더 {_images.Folder}{(_images.FolderExists ? "" : " (없음)")}, " +
                      $"저장 {GameSaveFile.FilePath(_saveDir)}: 컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장, " +
                      (_save.Run != null ? $"진행 중인 런 {_save.Run.Floor}층 (듀얼 {_save.Run.DuelsPlayed}판)" : "진행 중인 런 없음"));
        }

        /// <summary>저장한다. 끝난 런은 먼저 정리한다 (포인트 누적, 진행 중인 런에서 뺌).</summary>
        void Persist()
        {
            _save.CloseEndedRun();
            try
            {
                GameSaveFile.Save(_save, _saveDir);
                _saveNotice = null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _saveNotice = $"저장하지 못했습니다 ({GameSaveFile.FilePath(_saveDir)}): {e.Message}";
            }
        }

        void SetVisible(bool visible) => _docRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        static int NewSeed() => new System.Random().Next(1, 1000000);

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
            _moveNotice = null;
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
            Persist(); // 런이 끝났으면 여기서 정리된다 (포인트 누적, 진행 중인 런에서 뺌)
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
            BeginScreen();
            var panel = CenterPanel(1600);
            panel.name = "run-end";
            panel.style.height = Length.Percent(94);
            panel.Add(UiStyle.Label("런 종료", 40, UiStyle.Accent, bold: true));
            string reason = _run.EndReason == RunEndReason.DeckBelowMinimum
                ? $"패배로 메인 덱이 {RunRules.MinMainDeck}장보다 적어졌는데, 사이드 덱에서 채울 카드가 모자라 런이 끝났습니다."
                : _run.EndReason == RunEndReason.Abandoned ? "런을 포기했습니다."
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
            left.style.flexGrow = 1;
            left.style.marginRight = 12;
            left.style.backgroundColor = UiStyle.PanelLight;
            left.Add(UiStyle.Label("얻은 카드 (컬렉션에 더해짐)", 18, UiStyle.TextDim, bold: true));
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.flexGrow = 1;
            list.style.marginTop = 8;
            var grid = Grid(list.contentContainer);
            if (rewards.Count == 0)
                list.Add(Text("없음", 15, UiStyle.TextDim));
            foreach (var e in rewards)
            {
                var face = _faces.Build(e.Code, CardFaceSize.Medium, tag: $"{e.Floor}층");
                UiStyle.Margin(face, 6);
                grid.Add(face);
            }
            left.Add(list);
            var substituted = Enumerable.Range(0, RarityTable.RarityCount).Where(i => _run.SubstitutedRewards[i] > 0)
                .Select(i => $"{(Rarity)i} {_run.SubstitutedRewards[i]}회").ToList();
            if (substituted.Count > 0)
                left.Add(Text("포인트 대체: " + string.Join(" · ", substituted) + " (지급량은 아직 정하지 않음)", 14, UiStyle.TextDim, top: 6));
            var total = Enumerable.Range(0, RarityTable.RarityCount).Where(i => _save.Points[i] > 0)
                .Select(i => $"{(Rarity)i} {_save.Points[i]}회").ToList();
            if (total.Count > 0)
                left.Add(Text("포인트 대체 누적 (다음 런부터): " + string.Join(" · ", total), 14, UiStyle.TextDim, top: 2));
            body.Add(left);

            var right = UiStyle.PanelBox();
            right.style.width = 620;
            right.style.backgroundColor = UiStyle.PanelLight;
            right.Add(UiStyle.Label("런 로그", 18, UiStyle.TextDim, bold: true));
            right.Add(EventLog());
            body.Add(right);
            panel.Add(body);

            var again = UiStyle.Button("새 런", GoToStart, 22);
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

        // ================= 런 로그 =================

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

        // ================= 카드 (그리기, 고르기, 끌어다 놓기) =================

        /// <summary>새 화면을 그리기 전에: 창을 닫고, 카드·놓을 곳 목록과 카드 정보 칸을 비운다.</summary>
        void BeginScreen()
        {
            CloseModal();
            _root.Clear();
            _cardViews.Clear();
            _dropZones.Clear();
            _detail = null;
            _renderDetail = null;
        }

        static string SlotName(Pile pile, uint code)
        {
            switch (pile)
            {
                case Pile.Collection: return "pick-" + code;
                case Pile.StartSide: return "side-" + code;
                case Pile.Refill: return "refill-" + code;
                default: return $"card-{pile.ToString().ToLowerInvariant()}-{code}";
            }
        }

        /// <summary>
        /// 누르고 끌 수 있는 카드 하나. 마우스를 올리면 카드 정보 칸에 미리 보기, 누르면 고르기(고정),
        /// 두 번 누르면 onDoubleClick, 끌어다 놓으면 놓은 곳(AddDropZone)의 동작.
        /// </summary>
        VisualElement CardSlot(Pile pile, uint code, CardFaceSize size, int count = 0, string tag = null, bool dim = false,
            Action onDoubleClick = null, bool draggable = true)
        {
            var slot = new VisualElement { name = SlotName(pile, code), focusable = true };
            UiStyle.Padding(slot, 2);
            UiStyle.Margin(slot, 3);
            UiStyle.Radius(slot, 6);
            UiStyle.BorderColor(slot, Color.clear, 2);
            var face = _faces.Build(code, size, count, tag);
            if (dim)
                face.style.opacity = 0.4f;
            face.pickingMode = PickingMode.Ignore;
            slot.Add(face);
            CardInput.Attach(slot, _docRoot,
                onHover: () => ShowDetail(pile, code, pinned: false),
                onLeave: () => ShowPinnedDetail(pile),
                onClick: () => Select(pile, code),
                onDoubleClick: onDoubleClick,
                makeGhost: draggable ? () => _faces.Build(code, size) : (Func<VisualElement>)null,
                onDrop: position => Drop(pile, code, position));
            _cardViews.Add((pile, code, slot));
            if (IsSelected(pile, code))
                UiStyle.BorderColor(slot, UiStyle.Accent, 2);
            return slot;
        }

        /// <summary>보충 창의 후보는 층 사이 화면과 따로 고른다 (_refillSelected).</summary>
        bool IsSelected(Pile pile, uint code) =>
            code != 0 && (pile == Pile.Refill ? code == _refillSelected : pile == _selectedPile && code == _selectedCode);

        void ShowPinnedDetail(Pile hovered)
        {
            if (hovered == Pile.Refill)
                ShowDetail(Pile.Refill, _refillSelected, pinned: true);
            else
                ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        void AddDropZone(VisualElement zone, Action<Pile, uint> onDrop) => _dropZones.Add((zone, onDrop));

        void Drop(Pile pile, uint code, Vector2 position)
        {
            foreach (var (zone, onDrop) in _dropZones)
            {
                if (zone.panel != null && zone.worldBound.Contains(position))
                {
                    onDrop(pile, code);
                    return;
                }
            }
        }

        /// <summary>카드를 고른다: 테두리 표시와 카드 정보 칸만 바꾼다 (화면을 다시 그리지 않아서 두 번 누르기가 이어진다).</summary>
        void Select(Pile pile, uint code)
        {
            if (pile == Pile.Refill)
                _refillSelected = code;
            else
            {
                _selectedPile = pile;
                _selectedCode = code;
            }
            foreach (var (p, c, slot) in _cardViews)
                UiStyle.BorderColor(slot, IsSelected(p, c) ? UiStyle.Accent : Color.clear, 2);
            ShowDetail(pile, code, pinned: true);
        }

        void ShowDetail(Pile pile, uint code, bool pinned)
        {
            if (_detail == null || _renderDetail == null)
                return;
            _detail.Clear();
            _renderDetail(_detail, pile, code, pinned);
        }

        /// <summary>카드 정보 칸의 공통 부분: 큰 카드, 종류 한 줄, 효과문. 아래에 붙일 것은 부르는 쪽이 더한다.</summary>
        void AddCardDetail(VisualElement into, uint code, bool pinned)
        {
            var face = _faces.Build(code, CardFaceSize.Large);
            face.style.alignSelf = Align.Center;
            into.Add(face);
            var record = _text.Record(code);
            if (record != null)
            {
                var data = record.Data;
                into.Add(Text(DuelText.TypeLine(record.Type, data.Level, (OcgAttribute)data.Attribute, data.Race), 15, UiStyle.TextDim, top: 8));
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 6;
            scroll.Add(Text(record?.Description ?? "", 15));
            into.Add(scroll);
            if (!pinned)
                into.Add(Text("누르면 이 카드를 고릅니다.", 13, UiStyle.TextDim, top: 4));
        }

        /// <summary>카드를 줄 바꿈하며 늘어놓는 칸.</summary>
        static VisualElement Grid(VisualElement into = null)
        {
            var grid = into ?? new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.alignContent = Align.FlexStart;
            return grid;
        }

        /// <summary>카드 순서: 몬스터(레벨 높은 순) → 마법 → 함정 → 엑스트라 덱 몬스터, 그 안에서는 카드명 순.</summary>
        IOrderedEnumerable<uint> Sorted(IEnumerable<uint> cards) =>
            cards.OrderBy(c => IsExtraDeckCard(c) ? 3 : KindOrder(c))
                .ThenByDescending(c => _text.Record(c)?.Data.Level ?? 0)
                .ThenBy(c => _text.CardName(c), StringComparer.Ordinal)
                .ThenBy(c => c);

        int KindOrder(uint code)
        {
            var type = _text.Record(code)?.Type ?? OcgCardType.None;
            return (type & OcgCardType.Spell) != 0 ? 1 : (type & OcgCardType.Trap) != 0 ? 2 : 0;
        }

        /// <summary>"몬스터 24 · 마법 10 · 함정 7"</summary>
        string KindCounts(IEnumerable<uint> cards)
        {
            var counts = new int[3];
            foreach (uint code in cards)
                counts[KindOrder(code)]++;
            return $"몬스터 {counts[0]} · 마법 {counts[1]} · 함정 {counts[2]}";
        }

        bool IsExtraDeckCard(uint code) => _data.Database.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

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

        static Label SubHeading(string text)
        {
            var label = UiStyle.Label(text, 17, UiStyle.TextDim, bold: true);
            label.style.marginTop = 14;
            label.style.marginBottom = 6;
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
