// 로그라이크 런 화면 (UI Toolkit). 설계 문서 "로그라이크 진행".
// 화면: 런 시작 → 층 사이 화면 ⇄ 듀얼 화면(DuelScreen) → … → 런 종료 → 런 시작.
// - 런 시작 (RunScreen.Start.cs): 진행 중인 런이 있으면 "이어하기". 새 런은 덱 레시피(메인·엑스트라·시작 사이드 덱)와 시드를 고른다.
//   진행 중인 런이 있을 때 새 런을 시작하려면 한 번 더 눌러 포기를 확인한다.
// - 덱 편집 (RunScreen.Deck.cs): 런 시작 화면에서 레시피를 열어 컬렉션의 카드로 고치고 저장한다 (규칙은 RecipeEditor·RecipeRules).
// - 층 사이 화면 (RunScreen.Hub.cs, docs/UI.md "탑 (허브)"): 화면 전체가 탑 장면(TowerScene.cs)이고 그 위에 위 막대(층·LP·칩·골드·연승·봉인),
//   높이 자, 층 팻말, 직전 듀얼 판, 명령 메뉴를 얹는다. 메뉴의 "N층으로 올라간다"를 고르면 올라가는 연출 뒤에 그 층의 일이 시작된다.
//   전투 층에서는 듀얼 창("듀얼 시작", "덱 조정")이 뜨고, "듀얼 시작"을 누르면 코인 토스 뒤 RunState.BeginDuel로 상대·덱·LP·시드를 정해
//   듀얼 화면에 맡긴다. "처음 화면으로"는 저장된 채로 런 시작 화면으로. 런 로그는 따로 여는 창. 키보드는 RunScreen.Keys.cs.
// - 덱 창 (RunScreen.DeckWindow.cs): 메인·엑스트라·사이드 덱, 봉인, 상점 덱을 탭으로 나눠 카드 모양으로 늘어놓는다. 덱과 사이드 덱 사이는
//   끌어다 놓기, 두 번 누르기, 카드 정보의 버튼으로 옮긴다. "지금 덱을 레시피로"는 지금 메인·엑스트라 덱을 새 레시피로 저장한다 (사이드 덱은 비운다).
// - 승리 보상 고르기 (RunScreen.Reward.cs): 이기면(ChoosingReward) 층 사이 화면 위에 뜨는 창. 후보 카드 3장(모자라면 빈 자리는 골드) 중 하나를 고른다.
// - 보충 고르기: 메인 덱이 40장 미만(NeedsRefill)이면 층 사이 화면 위에 뜨는 창. 닫을 수 없고, 다 채우면 저절로 닫힌다.
// - 칩교환소·이벤트·휴식·보스 보상 (RunScreen.Tower.cs): 그 층에 올라서면 한 번 저절로 뜨는 창들과 보스 보상 봉인 해제 창.
// - 런 종료: 도달한 층, 전적, 얻은 카드·골드·칩, 골드 정산, 런 로그.
// 카드는 모두 CardFaces(텍스트 카드 템플릿)로 그린다. 마우스를 올리면 카드 정보 칸에 크게 보이고, 누르면 그 카드에 고정된다.
// 저장(GameSave): 런 시작, 듀얼 시작, 듀얼 결과(보상 후보·봉인), 보상 고르기, 교환·보충마다 저장 파일에 쓴다.
// 듀얼 도중에 게임을 끄면 다시 켰을 때 그 듀얼은 패배로 처리된다 (GameSave.ForfeitedDuel, 알림을 띄우고 바로 저장). 저장 폴더는 환경 변수 DUELROGUELIKE_SAVE_DIR(시험용), 없으면 persistentDataPath.
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
            Sealed,
            /// <summary>승리 보상 고르기 창의 후보</summary>
            Reward,
            /// <summary>덱 편집 화면의 컬렉션</summary>
            Collection,
            /// <summary>레시피의 메인 덱 (런 시작 화면의 레시피 보기, 덱 편집)</summary>
            DeckMain,
            /// <summary>레시피의 엑스트라 덱</summary>
            DeckExtra,
            /// <summary>레시피의 시작 사이드 덱</summary>
            DeckSide,
            /// <summary>보충 창의 후보</summary>
            Refill,
            /// <summary>칩교환소에서 봉인을 풀 카드 후보</summary>
            ShopRelease,
            /// <summary>칩교환소에서 대신 봉인할 카드 후보 (직접 고르는 교체)</summary>
            ShopSeal,
            /// <summary>상점 덱 (칩교환소에서 산 카드)</summary>
            Shop,
            /// <summary>보스 보상으로 봉인을 풀 카드 후보</summary>
            Unseal,
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
        /// <summary>듀얼 도중에 꺼진 듀얼을 패배로 처리했다는 알림 (다음 듀얼을 시작하면 지운다). 없으면 null.</summary>
        string _forfeitNotice;
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
            BuildKeyLayer();

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
            duelScreen.Attach(_data, _images);
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
            if (_save.ForfeitedDuel > 0)
            {
                // 듀얼 도중에 게임이 꺼졌다: 읽을 때 그 듀얼을 패배로 처리했으니, 알리고 곧바로 저장한다.
                _forfeitNotice = $"지난번에 듀얼 {_save.ForfeitedDuel} 도중에 게임이 꺼져서 그 듀얼을 패배로 처리했습니다.";
                Debug.Log("[런] " + _forfeitNotice);
                Persist();
            }
            _startRecipeId = _save.LastStartDeckId;
            Debug.Log($"[런] 카드 {_data.CardCount}장, 한글 {_data.LocalizedCount}장, 등급 표 {_rarities.Count}장, " +
                      $"카드 그림 폴더 {_images.Folder}{(_images.FolderExists ? "" : " (없음)")}, " +
                      $"저장 {GameSaveFile.FilePath(_saveDir)}: 컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장, " +
                      (_save.Run != null ? $"진행 중인 런 {_save.Run.Floor}층 (듀얼 {_save.Run.DuelsPlayed}판)" : "진행 중인 런 없음"));
        }

        /// <summary>저장한다. 끝난 런은 먼저 정리한다 (골드 누적, 진행 중인 런에서 뺌).</summary>
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

        void SetVisible(bool visible)
        {
            _docRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _keysBlockedFrame = Time.frameCount; // 듀얼 화면에서 누른 키가 돌아온 화면에 그대로 들어가지 않게
        }

        static int NewSeed() => new System.Random().Next(1, 1000000);

        // ================= 듀얼 =================

        /// <summary>"다음 듀얼 시작": 먼저 코인 토스 창을 띄운다. 이긴 쪽이 선공·후공을 고른다 (상대는 대부분 선공).</summary>
        void StartNextDuel()
        {
            if (_run == null || _run.Phase != RunPhase.Ready)
                return;
            CoinToss toss;
            try
            {
                toss = _run.NextCoinToss();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowHub();
                return;
            }
            var panel = OpenModal(760);
            panel.name = "coin-toss";
            panel.style.alignItems = Align.Center;
            panel.style.paddingTop = panel.style.paddingBottom = 24;
            panel.Add(UiStyle.Label($"{_run.Floor}층{(_run.FloorKind == FloorKind.Boss ? " · 보스 듀얼" : "")} · 듀얼 전", 17, UiStyle.TextDim));
            var title = UiStyle.Label("[ 코인 토스. ]", 34, UiStyle.Gold);
            UiStyle.Black(title);
            panel.Add(title);

            // 동전: 던져 올려 돌다가 결과 면으로 내려앉는다. 누르면 바로 결과가 나온다.
            var coin = new CoinView(150);
            var flip = new Button { name = "coin-flip", text = "", focusable = false };
            flip.style.width = flip.style.height = 150;
            flip.style.marginTop = 70;
            flip.style.marginBottom = 14;
            UiStyle.Padding(flip, 0);
            flip.style.backgroundColor = Color.clear;
            UiStyle.BorderColor(flip, Color.clear, 0);
            flip.Add(coin);
            panel.Add(flip);

            var result = UiStyle.Label(toss.PlayerWon ? "앞면 — 내가 이겼습니다" : "뒷면 — 상대가 이겼습니다", 30,
                toss.PlayerWon ? UiStyle.Mine : UiStyle.Opponent);
            result.name = "coin-result";
            UiStyle.Black(result);
            panel.Add(result);
            var landed = UiStyle.Box();
            landed.style.alignItems = Align.Center;
            if (toss.WinPercent != RunRules.CoinTossWinPercent)
                landed.Add(Text($"이번 코인 토스에서 내가 이길 확률 {toss.WinPercent}%", 16, UiStyle.TextDim, top: 4));
            var buttons = Row();
            buttons.style.marginTop = 16;
            var choices = new List<Button>();
            if (toss.PlayerWon)
            {
                landed.Add(Text("선공·후공을 고르세요. 선공은 첫 턴에 드로우하지 않고, 후공은 첫 턴부터 공격할 수 있습니다.", 18, top: 8));
                choices.Add(CoinButton("coin-first", "선공 (먼저 시작)", true, gold: true));
                choices.Add(CoinButton("coin-second", "후공 (나중에 시작)", false, gold: false));
            }
            else
            {
                landed.Add(Text(toss.OpponentChoosesFirst ? "상대가 선공을 골랐습니다. 나는 후공입니다." : "상대가 후공을 골랐습니다. 나는 선공입니다.", 18, top: 8));
                choices.Add(CoinButton("coin-start", "듀얼 시작", true, gold: true));
            }
            foreach (var choice in choices)
                buttons.Add(choice);
            landed.Add(buttons);
            panel.Add(landed);
            // 나중에: 듀얼 창으로 돌아간다 (덱을 조정하고 다시 시작할 수 있다. 코인 토스 결과는 그대로다)
            var later = UiStyle.Button("나중에", () =>
            {
                CloseModal();
                OpenDuelWindow();
            }, 16);
            later.name = "coin-close";
            later.style.marginTop = 14;
            later.style.alignSelf = Align.FlexEnd;
            panel.Add(later);

            // 결과가 나온 모습으로 만든다: 동전은 결과 면, 결과 글과 고르는 버튼이 보인다
            void Land()
            {
                coin.SetSpin(toss.PlayerWon ? 0f : 1f);
                coin.style.translate = new Translate(0, 0);
                result.style.opacity = 1f;
                landed.style.opacity = 1f;
                foreach (var choice in choices)
                    choice.SetEnabled(true);
            }

            // 같은 듀얼의 코인 토스를 다시 열면 (나중에 → 듀얼 시작) 연출 없이 결과를 바로 보인다
            int duel = _run.DuelsPlayed + 1;
            if (_coinFlipShown == duel)
            {
                Land();
                return;
            }
            _coinFlipShown = duel;
            result.style.opacity = 0f;
            landed.style.opacity = 0f;
            foreach (var choice in choices)
                choice.SetEnabled(false);
            // 반 바퀴씩 돈다: 앞면에서 시작해 앞면이면 여섯 번, 뒷면이면 일곱 번 돌고 멈춘다 (1.2초, 갈수록 느려진다)
            float turns = toss.PlayerWon ? 6f : 7f, started = Time.realtimeSinceStartup;
            bool done = false;
            IVisualElementScheduledItem spin = null;
            void Finish()
            {
                if (done)
                    return;
                done = true;
                spin?.Pause();
                Land();
            }
            flip.clicked += Finish;
            spin = panel.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / 1.2f);
                float eased = 1f - (1f - t) * (1f - t);
                coin.SetSpin(turns * eased);
                coin.style.translate = new Translate(0, -64f * Mathf.Sin(t * Mathf.PI));
                if (t >= 1f)
                    Finish();
            }).Every(16);
        }

        /// <summary>코인 토스 연출을 보여 준 듀얼 번호 (같은 듀얼에서 창을 다시 열면 연출을 되풀이하지 않는다).</summary>
        int _coinFlipShown;

        Button CoinButton(string name, string text, bool playerFirst, bool gold)
        {
            var b = UiStyle.StoneButton(text, () => BeginNextDuel(playerFirst), gold: gold);
            b.name = name;
            b.style.marginLeft = b.style.marginRight = 6;
            b.style.minWidth = 260;
            return b;
        }

        /// <param name="playerFirst">코인 토스에 이겼을 때 고른 것 (true = 선공). 상대가 이겼으면 런이 정한다.</param>
        void BeginNextDuel(bool playerFirst)
        {
            if (_run == null || _run.Phase != RunPhase.Ready)
                return;
            RunDuel duel;
            try
            {
                duel = _run.BeginDuel(playerFirst);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowHub();
                return;
            }
            // 듀얼 중 상태로 저장한다: 이대로 게임이 꺼지면 다시 켰을 때 이 듀얼은 패배로 처리된다.
            Persist();
            // 임시 자동 응답·WindBot 난수는 화면 밖 런 시험(HeadlessRunTest)과 같은 식으로 정한다.
            int helperSeed = RunRandom.For(_run.Seed, duel.DuelNumber, RunRandomPurpose.External).NextSeed();
            string floor = $"{duel.Floor}층" + (duel.IsBossFloor ? " (보스 층)" : "");
            _moveNotice = null;
            _forfeitNotice = null;
            CloseModal();
            SetVisible(false);
            string turn = duel.PlayerGoesFirst ? "나의 선공" : "나의 후공";
            string coin = duel.Toss.PlayerWon ? "코인 토스 승리" : "코인 토스 패배";
            Debug.Log($"[런] {floor} 듀얼 {duel.DuelNumber}: 상대 {duel.Opponent.Name}, 나의 LP {duel.PlayerLp}, {coin} → {turn}");
            duelScreen.BeginExternalDuel(duel.CreateSessionOptions(), duel.PlayerTeam, helperSeed, helperSeed + 7777,
                $"나 · {floor}", $"상대 · {duel.Opponent.Name}",
                $"{floor} 듀얼 {duel.DuelNumber} (런 시드 {_run.Seed}) — 나: LP {duel.PlayerLp}, 상대: {duel.Opponent.Name} LP {duel.OpponentLp}, {coin} → {turn}, 상대 AI: WindBot",
                OnDuelLeft);
        }

        void OnDuelLeft(DuelState state)
        {
            var eventBattle = _run.CurrentDuel != null && _run.CurrentDuel.IsEventBattle ? _run.CurrentEvent : null;
            var eventChoice = eventBattle != null ? _run.PendingEventBattle : null;
            int mark = _run.Events.Count, floor = _run.Floor;
            try
            {
                _run.FinishDuel(state);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (eventBattle != null && eventChoice != null)
            {
                var happened = _run.Events.Skip(mark).ToList();
                bool won = happened.Any(e => e.Kind == RunEventKind.DuelWon);
                _eventResult = new EventResultView(eventBattle, eventChoice, floor, won, happened);
                if (_run.Floor != floor)
                {
                    _moveNotice = $"{floor}층 이벤트 전투 {(won ? "승리" : "패배")} → {_run.Floor}층 {Tower.KindName(_run.FloorKind)}";
                    _moveNoticeIsError = false;
                }
            }
            Persist(); // 보상 후보·봉인이 정해진 상태로 저장. 런이 끝났으면 여기서 정리된다 (골드 누적, 진행 중인 런에서 뺌)
            var lastResult = _run.Events.LastOrDefault(e => e.Kind == RunEventKind.DuelWon || e.Kind == RunEventKind.DuelLost);
            Debug.Log($"[런] 듀얼 {_run.DuelsPlayed} 끝: {lastResult?.Describe(_text.CardName)} → {_run.Phase}, {_run.Floor}층, LP {_run.Lp}");
            SetVisible(true);
            if (_run.Phase == RunPhase.Ended)
                ShowEnd();
            else
                ShowHub();
        }

        // ================= 런 종료 =================

        /// <summary>런 종료 화면의 버튼 줄 (좌우로 고른다).</summary>
        ButtonRow _endRow;

        bool EndKeysOpen => _endRow != null && _endRow.Root.panel != null;

        /// <summary>
        /// 런 종료 화면: 도달한 층 높이의 탑 장면을 어둡게 덮고, 가운데에 제목과 끝난 까닭, 숫자 판 다섯 개, 얻은 카드, 골드 정산, 버튼.
        /// 런 로그는 이 화면에 두지 않고 "런 기록"으로 연다.
        /// </summary>
        void ShowEnd()
        {
            StopClimb();
            BeginScreen();
            var stage = NewStage("run-end");
            stage.Add(new TowerScene { Marker = Mathf.Max(1, _run.Floor), Opponent = 0 });
            var dim = new VisualElement();
            dim.pickingMode = PickingMode.Ignore;
            UiStyle.Place(dim, 0, 0, StageWidth, StageHeight);
            dim.style.backgroundColor = new Color(0.04f, 0.03f, 0.024f, 0.86f);
            stage.Add(dim);
            var column = UiStyle.Box("run-end-body");
            UiStyle.Place(column, 0, 0, StageWidth, StageHeight);
            column.style.alignItems = Align.Center;
            column.style.paddingTop = 66;
            stage.Add(column);

            column.Add(WindowTitle("런 종료", 64));
            string reason = _run.EndReason == RunEndReason.DeckBelowMinimum
                ? $"메인 덱이 {RunRules.MinMainDeck}장보다 적어졌는데 사이드 덱에서 채울 카드가 모자랐다."
                : _run.EndReason == RunEndReason.Abandoned ? "런을 포기했다."
                : "런이 끝났다.";
            column.Add(Text(reason, 24, top: 2));
            column.Add(Text($"{_run.StartDeckName} · 시드 {_run.Seed}", 19, UiStyle.TextDim));

            var rewards = _run.Events.Where(e => e.Kind == RunEventKind.RewardGained).ToList();
            var stats = Row();
            stats.style.marginTop = 24;
            stats.Add(EndStat("도달한 층", $"{_run.Floor}층", UiStyle.Gold));
            stats.Add(EndStat("전적", $"{_run.Wins}승 {_run.Losses}패" + (_run.Draws > 0 ? $" {_run.Draws}무" : ""), UiStyle.Text));
            stats.Add(EndStat("얻은 카드", $"{rewards.Count}장", UiStyle.Text));
            stats.Add(EndStat("봉인된 카드", $"{_run.SealedMain.Count + _run.SealedExtra.Count}장", UiStyle.Text));
            stats.Add(EndStat("골드", $"+{_run.Gold + _run.SettlementGold}", UiStyle.Good));
            column.Add(stats);

            column.Add(Text(rewards.Count > 0 ? "얻은 카드 · 컬렉션에 더해졌다" : "얻은 카드 없음", 19, UiStyle.TextDim, top: 24));
            if (rewards.Count > 0)
            {
                // 한 줄에 14장, 두 줄까지. 더 많으면 두 줄에 다 들어가게 카드를 줄인다.
                const float rowWidth = 1640, gap = 10;
                int columns = rewards.Count <= 28 ? Mathf.Min(rewards.Count, 14) : (rewards.Count + 1) / 2;
                float cardWidth = Mathf.Min(108f, Mathf.Floor((rowWidth - gap * (columns - 1)) / columns));
                var grid = Grid();
                grid.name = "run-end-cards";
                grid.style.justifyContent = Justify.Center;
                grid.style.width = columns * (cardWidth + gap);
                grid.style.marginTop = 8;
                foreach (var e in rewards)
                {
                    var face = _faces.Build(e.Code, cardWidth, tag: $"{e.Floor}층");
                    face.style.marginLeft = face.style.marginRight = gap / 2;
                    face.style.marginBottom = gap;
                    grid.Add(face);
                }
                column.Add(grid);
            }

            int floorBonus = RunRules.SettlementFloorBonus(_run.Floor);
            var settlement = Text($"골드 정산 {_run.SettlementGold} = 도달 {_run.Floor}층 + 10층 보너스 {floorBonus} + 얻은 칩 {_run.ChipsEarned}÷{RunRules.SettlementEarnedChipsDivisor} " +
                                  $"+ 남은 칩 {_run.Chips}÷{RunRules.SettlementHeldChipsDivisor} · 이번 런에서 얻은 골드 {_run.Gold} · 골드 누적 {_save.Gold}", 19, UiStyle.TextDim, top: 12);
            settlement.name = "run-settlement";
            settlement.style.whiteSpace = WhiteSpace.Normal;
            settlement.style.unityTextAlign = TextAnchor.MiddleCenter;
            settlement.style.maxWidth = 1640;
            column.Add(settlement);

            var buttons = new ButtonRow();
            buttons.Root.style.marginTop = 24;
            buttons.Add("new-run", "새 런", GoToNewRun, gold: true, width: 300);
            buttons.Add("end-log", "런 기록", OpenLog, width: 260);
            buttons.Add("end-title", "처음 화면으로", GoToStart, width: 300);
            buttons.Select(0);
            _endRow = buttons;
            column.Add(buttons.Root);
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"));
            hints.style.marginTop = 14;
            column.Add(hints);
            ApplyKeyCaps();
            Debug.Log($"[런] 종료: {_run.Floor}층, 듀얼 {_run.DuelsPlayed}판, {_run.Wins}승 {_run.Losses}패 {_run.Draws}무, 얻은 카드 {rewards.Count}장");
        }

        /// <summary>런 종료 화면의 숫자 판 하나.</summary>
        static VisualElement EndStat(string label, string value, Color color)
        {
            var box = new VisualElement();
            UiStyle.Slab(box);
            box.style.width = 250;
            box.style.height = 120;
            box.style.marginLeft = box.style.marginRight = 7;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            box.Add(UiStyle.Label(label, 18, UiStyle.TextDim));
            var number = UiStyle.Label(value, 44, color);
            UiStyle.Black(number);
            box.Add(number);
            return box;
        }

        void EndKey(Nav nav)
        {
            switch (nav)
            {
                case Nav.Left:
                    _endRow.Move(-1);
                    break;
                case Nav.Right:
                    _endRow.Move(1);
                    break;
                case Nav.Confirm:
                    _endRow.Activate();
                    break;
            }
        }

        // ================= 런 로그 =================

        ScrollView EventLog()
        {
            var log = new ScrollView(ScrollViewMode.Vertical) { name = "run-events" };
            log.style.flexGrow = 1;
            log.style.marginTop = 6;
            foreach (var e in _run.Events)
            {
                bool head = e.Kind == RunEventKind.DuelStarted || e.Kind == RunEventKind.RunStarted || e.Kind == RunEventKind.FloorEntered;
                var line = Text((head ? "" : "    ") + e.Describe(_text.CardName), 19, EventColor(e.Kind), top: head ? 8 : 1);
                line.style.whiteSpace = WhiteSpace.Normal;
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
                case RunEventKind.RewardOffered:
                case RunEventKind.FloorEntered:
                case RunEventKind.UnsealOffered:
                case RunEventKind.GoldSettled:
                    return UiStyle.Accent;
                case RunEventKind.DuelWon:
                case RunEventKind.RewardGained:
                case RunEventKind.RewardGold:
                case RunEventKind.ChipsGained:
                case RunEventKind.CardUnsealed:
                case RunEventKind.EventCardGained:
                case RunEventKind.EventGold:
                    return Positive;
                case RunEventKind.DuelLost:
                case RunEventKind.DuelForfeited:
                case RunEventKind.CardSealed:
                case RunEventKind.ChipsLost:
                case RunEventKind.CardLost:
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
            // 층 사이 화면의 요소들 (ShowHub가 다시 만든다)
            _hubShown = false;
            _stage = null;
            _scene = null;
            _hubMenu = null;
            _deckScroll = null;
            _keyCaps.Clear();
        }

        static string SlotName(Pile pile, uint code)
        {
            switch (pile)
            {
                case Pile.Collection: return "pick-" + code;
                case Pile.DeckMain: return "deck-main-" + code;
                case Pile.DeckExtra: return "deck-extra-" + code;
                case Pile.DeckSide: return "deck-side-" + code;
                case Pile.Refill: return "refill-" + code;
                case Pile.Reward: return "reward-card-" + code;
                default: return $"card-{pile.ToString().ToLowerInvariant()}-{code}";
            }
        }

        /// <summary>
        /// 누르고 끌 수 있는 카드 하나. 마우스를 올리면 카드 정보 칸에 미리 보기, 누르면 고르기(고정),
        /// 두 번 누르면 onDoubleClick, 끌어다 놓으면 놓은 곳(AddDropZone)의 동작.
        /// </summary>
        /// <param name="width">0보다 크면 카드 폭을 직접 정한다 (덱 창의 1.1배 카드). 그때 size는 쓰지 않는다.</param>
        VisualElement CardSlot(Pile pile, uint code, CardFaceSize size, int count = 0, string tag = null, bool dim = false,
            Action onDoubleClick = null, bool draggable = true, float width = 0)
        {
            var slot = new VisualElement { name = SlotName(pile, code), focusable = true };
            UiStyle.Padding(slot, 2);
            UiStyle.Margin(slot, 3);
            UiStyle.Radius(slot, 6);
            UiStyle.BorderColor(slot, Color.clear, 2);
            var face = width > 0 ? _faces.Build(code, width, count, tag) : _faces.Build(code, size, count, tag);
            if (dim)
                face.style.opacity = 0.4f;
            face.pickingMode = PickingMode.Ignore;
            slot.Add(face);
            CardInput.Attach(slot, _docRoot,
                onHover: () => ShowDetail(pile, code, pinned: false),
                onLeave: () => ShowPinnedDetail(pile),
                onClick: () => Select(pile, code),
                onDoubleClick: onDoubleClick,
                makeGhost: draggable ? () => width > 0 ? _faces.Build(code, width) : _faces.Build(code, size) : (Func<VisualElement>)null,
                onDrop: position => Drop(pile, code, position));
            _cardViews.Add((pile, code, slot));
            if (IsSelected(pile, code))
                UiStyle.BorderColor(slot, UiStyle.Accent, 2);
            return slot;
        }

        /// <summary>보충 창·보상 창의 후보는 층 사이 화면과 따로 고른다 (_refillSelected, _rewardSlot).</summary>
        bool IsSelected(Pile pile, uint code) =>
            code != 0 && (pile == Pile.Refill ? code == _refillSelected
                : pile == Pile.Reward ? code == RewardSelectedCode()
                : pile == _selectedPile && code == _selectedCode);

        void ShowPinnedDetail(Pile hovered)
        {
            if (hovered == Pile.Refill)
                ShowDetail(Pile.Refill, _refillSelected, pinned: true);
            else if (hovered == Pile.Reward)
                ShowDetail(Pile.Reward, RewardSelectedCode(), pinned: true);
            else
                ShowDetail(_selectedPile, _selectedCode, pinned: true);
        }

        void AddDropZone(VisualElement zone, Action<Pile, uint> onDrop) => _dropZones.Add((zone, onDrop));

        void Drop(Pile pile, uint code, Vector2 position)
        {
            foreach (var (zone, onDrop) in _dropZones)
            {
                if (zone.panel != null && VisibleBound(zone).Contains(position))
                {
                    onDrop(pile, code);
                    return;
                }
            }
        }

        /// <summary>스크롤 안에서 보이는 부분만 (스크롤 밖으로 나간 부분은 그 아래 고정한 칸과 겹치므로 놓을 곳으로 치지 않는다).</summary>
        static Rect VisibleBound(VisualElement element)
        {
            var r = element.worldBound;
            for (var p = element.parent; p != null; p = p.parent)
            {
                if (!(p is ScrollView scroll))
                    continue;
                var v = scroll.contentViewport.worldBound;
                r = Rect.MinMaxRect(Mathf.Max(r.xMin, v.xMin), Mathf.Max(r.yMin, v.yMin), Mathf.Min(r.xMax, v.xMax), Mathf.Min(r.yMax, v.yMax));
            }
            return r;
        }

        /// <summary>카드를 고른다: 테두리 표시와 카드 정보 칸만 바꾼다 (화면을 다시 그리지 않아서 두 번 누르기가 이어진다).</summary>
        void Select(Pile pile, uint code)
        {
            if (pile == Pile.Refill)
                _refillSelected = code;
            else if (pile == Pile.Reward)
            {
                SelectRewardSlot(RewardSlotOf(code));
                return;
            }
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

        /// <summary>
        /// 카드 정보 칸의 공통 부분: 큰 카드, 종류 한 줄, 효과문. 아래에 붙일 것은 부르는 쪽이 더한다.
        /// showFace가 false면 큰 카드 없이 카드명부터 (후보가 이미 큰 카드로 보이는 보상 창).
        /// </summary>
        void AddCardDetail(VisualElement into, uint code, bool pinned, bool showFace = true)
        {
            if (showFace)
            {
                var face = _faces.Build(code, CardFaceSize.Large);
                face.style.alignSelf = Align.Center;
                into.Add(face);
            }
            else
                into.Add(UiStyle.Label(_text.CardName(code), 20, bold: true));
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
            _windowMenu = null;
            _windowRow = null;
            _windowCancel = null;
            _modalLayer.Clear();
            _modalLayer.style.display = DisplayStyle.Flex;
            _modalLayer.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            var panel = UiStyle.PanelBox();
            panel.style.width = width;
            panel.style.maxWidth = Length.Percent(96);
            UiStyle.Padding(panel, 20);
            UiStyle.BorderColor(panel, UiStyle.Accent, 2);
            _modalLayer.Add(panel);
            // 창 아래쪽 가운데에 키 도움말 (창마다 따로 넣지 않는다)
            var hints = Hints(("← ↑ ↓ →", "W A S D", "고르기"), ("Z", "J", "결정"), ("X", "K", "닫기"));
            hints.name = "modal-hints";
            hints.pickingMode = PickingMode.Ignore;
            hints.style.position = Position.Absolute;
            hints.style.bottom = 10;
            hints.style.alignSelf = Align.Center;
            hints.style.paddingLeft = 12;
            hints.style.paddingTop = hints.style.paddingBottom = 4;
            hints.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.7f);
            UiStyle.Radius(hints, 6);
            _modalLayer.Add(hints);
            ApplyKeyCaps();
            return panel;
        }

        /// <summary>
        /// 창틀 없이 화면을 어둡게 덮고 그 위에 내용을 놓는다 (보상 고르기). 돌려준 상자에 내용을 넣는다.
        /// </summary>
        VisualElement OpenPlain(string name, float dim)
        {
            _windowMenu = null;
            _windowRow = null;
            _windowCancel = null;
            _modalLayer.Clear();
            _modalLayer.style.display = DisplayStyle.Flex;
            _modalLayer.style.backgroundColor = new Color(0, 0, 0, dim);
            var box = UiStyle.Box(name);
            _modalLayer.Add(box);
            return box;
        }

        void CloseModal()
        {
            _windowMenu = null;
            _windowRow = null;
            _windowCancel = null;
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

        /// <summary>글 입력 칸. 기본 테마의 입력 칸은 흰 바탕이라 화면 글자색(밝은 색)이 보이지 않아 어두운 칸으로 바꾼다.</summary>
        static TextField DarkTextField(string name, string value, int fontSize)
        {
            var field = new TextField { name = name, value = value ?? "" };
            field.style.fontSize = fontSize;
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = UiStyle.PanelLight;
                input.style.color = UiStyle.Text;
                UiStyle.BorderColor(input, UiStyle.Border, 1);
            }
            return field;
        }
    }
}
