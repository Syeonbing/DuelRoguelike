// 최소 듀얼 화면 (UI Toolkit). 설계 문서의 화면 구성을 따른다:
// 왼쪽 플레이어 정보(위 상대, 아래 나, 카드 효과문은 내 초상화 자리), 가운데 필드(점대칭, 엑스트라 몬스터 존 없음,
// 패는 필드 위아래, 묘지 옆 필드 가운데 쪽에 원형 제외 칸), 오른쪽 로그.
// 조작: 카드를 직접 눌러 행동을 고른다. 행동할 수 있는 카드·고를 수 있는 카드·고를 수 있는 칸은 테두리로 강조한다.
// 칸 고르기(놓을 칸·무효로 할 칸)는 필드 칸(빈칸 포함)을 눌러서 고른다.
// 센터 카드, 초상화 그림, 체인 박스 연출, 카드 크게 보기는 아직 없다.
//
// 두 가지로 쓴다:
// - 혼자: Duel 씬. 인스펙터의 고정 덱으로 듀얼 하나를 하고, 끝나면 새 시드로 다시 시작한다 (화면 시험용).
// - 런 화면(RunScreen)이 맡김: Run 씬. 런 화면이 덱·LP·시드를 정해 BeginExternalDuel로 듀얼을 맡기고,
//   듀얼이 끝나 "결과 보기"를 누르면 끝난 듀얼 상태를 돌려준다. 런 규칙은 이 화면이 모른다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelRoguelike.AI;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed class DuelScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("혼자 듀얼할 때: 듀얼 난수·덱 셔플 시드. 다시 시작할 때마다 1씩 늘어난다. (런에서는 런 시드로 정한다)")]
        int duelSeed = 1;

        [SerializeField, Tooltip("혼자 듀얼할 때: 내 덱 (스타터 덱)")]
        StarterDeckId myDeck = StarterDeckId.Yugi;

        [SerializeField, Tooltip("혼자 듀얼할 때: 상대 덱 (스타터 덱)")]
        StarterDeckId opponentDeck = StarterDeckId.Kaiba;

        [SerializeField, Tooltip("체인할지 묻는 방식")]
        ChainPromptMode chainPrompt = ChainPromptMode.Smart;

        [SerializeField, Tooltip("카드를 놓을 칸·무효로 할 칸을 묻는 방식. Smart: 무효로 할 칸, 상대 쪽 칸, 몬스터 존과 마법·함정 존이 함께 후보일 때만 묻고, 나머지는 첫 빈칸")]
        PlaceSelectMode placeSelect = PlaceSelectMode.Smart;

        [SerializeField, Tooltip("상대 AI(WindBot)가 나의 비공개 정보(손패, 뒷면 카드, 덱)를 본다. 끄면 EDOPro 서버처럼 가린다.")]
        bool opponentSeesHiddenInfo = false;

        [SerializeField, Tooltip("개발용: 임시 자동 응답이 대신 답하는 \"자동\"(한 번)과 \"자동으로 끝까지\" 버튼을 보인다.")]
        bool devAutoButtons = true;

        const byte Me = 0;
        const byte Opp = 1;
        const int MainZones = 5;
        const int MaxLogLines = 800;

        static readonly Color Actionable = UiStyle.Accent;
        static readonly Color Selectable = UiStyle.Hex(0x4fd1e8);
        static readonly Color Chosen = UiStyle.Hex(0x5ee07a);

        CardDatabase _db;
        CardScriptSource _scripts;
        DuelSession _session;
        DuelText _text;
        DuelLogFormatter _logFormatter;
        SimpleAutoPlayer _helper;
        WindBotPlayer _opponentAi;
        bool _resultLogged;

        // 런 화면이 맡긴 듀얼 (혼자 듀얼하면 _external이 false이고 _onLeave는 null)
        bool _external;
        Action<DuelState> _onLeave;
        readonly string[] _playerNames = { "나", "상대" };

        // "자동으로 끝까지": 듀얼이 끝날 때까지 프레임마다 임시 자동 응답이 대신 답한다 (개발용).
        bool _autoPlay;
        const float AutoPlaySecondsPerFrame = 0.02f;

        DuelInteraction _interaction;
        int _interactionSerial = -1;
        bool _windowHidden;
        bool _modalOpen;

        VisualElement _docRoot, _root;
        readonly Label[] _playerName = new Label[2];
        readonly Label[] _lp = new Label[2];
        readonly Label[] _playerInfo = new Label[2];
        readonly CardCell[,] _monsters = new CardCell[2, MainZones];
        readonly CardCell[,] _spells = new CardCell[2, MainZones];
        readonly CardCell[] _fieldZone = new CardCell[2];
        readonly List<CardCell>[] _handCells = { new List<CardCell>(), new List<CardCell>() };
        readonly PileCell[] _deck = new PileCell[2];
        readonly PileCell[] _extra = new PileCell[2];
        readonly PileCell[] _grave = new PileCell[2];
        readonly PileCell[] _removed = new PileCell[2];
        readonly VisualElement[] _handRows = new VisualElement[2];
        Label _turnLabel, _promptLabel, _promptDetail;
        VisualElement _chainBox, _stripButtons;
        ScrollView _log;
        Label _detailName, _detailType, _detailStats, _detailText;
        VisualElement _menuLayer, _modalLayer;

        void Start()
        {
            if (_external)
                return; // 런 화면이 Attach로 준비하고 BeginExternalDuel로 듀얼을 맡긴다
            CreateDocument();
            Exception loadError = null;
            try
            {
                LoadData();
            }
            catch (Exception e)
            {
                loadError = e;
                _text = new DuelText(new CardDatabase(), null, Me);
                _logFormatter = new DuelLogFormatter(_text);
            }
            BuildLayout();
            if (loadError != null)
            {
                Debug.LogException(loadError);
                ShowMessage("카드 데이터를 불러오지 못했습니다.",
                    loadError is DllNotFoundException
                        ? $"네이티브 DLL이 없습니다. tools/build-ocgcore.ps1과 tools/fetch-sqlite.ps1을 실행하세요. ({loadError.Message})"
                        : loadError.Message);
                return;
            }
            RunSafely(StartFreeDuel);
        }

        void OnDestroy() => _session?.Dispose();

        void Update()
        {
            if (_autoPlay)
                AutoPlayStep();
        }

        // ================= 런 화면이 맡기는 듀얼 =================

        /// <summary>런 화면이 맡긴다고 표시한다. Start가 혼자 듀얼을 시작하지 않도록 Start보다 먼저(Awake에서) 부른다.</summary>
        internal void UseExternalDuels() => _external = true;

        /// <summary>런 화면이 읽은 카드 데이터로 화면을 만들고 숨겨 둔다.</summary>
        internal void Attach(CardDataSet data)
        {
            _external = true;
            if (_docRoot != null)
                return;
            CreateDocument();
            _db = data.Database;
            _scripts = data.Scripts;
            _text = new DuelText(_db, data.Strings, Me);
            _logFormatter = new DuelLogFormatter(_text);
            BuildLayout();
            SetVisible(false);
        }

        internal void SetVisible(bool visible)
        {
            if (_docRoot != null)
                _docRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// 런 화면의 듀얼 하나를 시작하고 화면을 보인다. 플레이어(나)는 팀 0이고 상대는 WindBot이다.
        /// 듀얼이 끝나 "결과 보기"를 누르면 화면을 숨기고 onLeave(끝난 듀얼 상태)를 부른다.
        /// </summary>
        internal void BeginExternalDuel(DuelSessionOptions options, int helperSeed, int opponentSeed,
            string myName, string opponentName, string startLine, Action<DuelState> onLeave)
        {
            _onLeave = onLeave;
            _playerNames[Me] = myName;
            _playerNames[Opp] = opponentName;
            SetVisible(true);
            RunSafely(() => StartSession(options, helperSeed, opponentSeed, startLine));
        }

        void LeaveDuel()
        {
            var state = _session?.State;
            var onLeave = _onLeave;
            if (state == null || !state.IsFinished || onLeave == null)
                return;
            _onLeave = null;
            _autoPlay = false;
            _session.Dispose();
            _session = null;
            _interaction = null;
            _interactionSerial = -1;
            CloseMenu();
            CloseModal();
            SetVisible(false);
            onLeave(state);
        }

        // ================= 자동 진행 (개발용) =================

        void AutoPlayStep()
        {
            if (_session == null || _session.State.IsFinished || _session.PendingRequest == null)
            {
                _autoPlay = false;
                Render();
                return;
            }
            float until = Time.realtimeSinceStartup + AutoPlaySecondsPerFrame;
            try
            {
                do
                {
                    byte[] response = _helper.Respond(_session.PendingRequest, _session.State);
                    if (response == null)
                    {
                        _autoPlay = false;
                        break;
                    }
                    _session.Respond(response);
                } while (_session.PendingRequest != null && !_session.State.IsFinished && Time.realtimeSinceStartup < until);
            }
            catch (Exception e)
            {
                _autoPlay = false;
                Debug.LogException(e);
                AddLog($"오류: {e.Message}", UiStyle.Opponent);
            }
            if (_session.State.IsFinished)
                _autoPlay = false;
            Render();
        }

        void SetAutoPlay(bool on)
        {
            _autoPlay = on;
            CloseMenu();
            CloseModal();
            Render();
        }

        // ================= 듀얼 =================

        void LoadData()
        {
            // 에디터에서는 저장소의 External 폴더를 바로 읽는다. 실행 파일 빌드용 경로는 나중에 정한다.
            string externalRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "External"));
            var data = CardDataSet.Load(externalRoot);
            _db = data.Database;
            _scripts = data.Scripts;
            _text = new DuelText(_db, data.Strings, Me);
            _logFormatter = new DuelLogFormatter(_text);
            Debug.Log($"[듀얼] 카드 {data.CardCount}장, 한글 {data.LocalizedCount}장, 시스템 문구 {data.Strings.Count}개, 스크립트 {_scripts.Count}개");
        }

        /// <summary>혼자 듀얼: 인스펙터의 고정 덱과 시드로 시작한다.</summary>
        void StartFreeDuel()
        {
            var myRecipe = StarterDecks.Get(myDeck);
            var opponentRecipe = StarterDecks.Get(opponentDeck);
            var options = new DuelSessionOptions
            {
                Seed = new ulong[] { (ulong)duelSeed, 2, 3, 4 },
                Decks = new IReadOnlyList<uint>[] { myRecipe.ToCardList(), opponentRecipe.ToCardList() },
                DeckShuffleSeed = duelSeed * 100,
            };
            StartSession(options, duelSeed, duelSeed,
                $"듀얼 시작 (시드 {duelSeed}) — 나: {myRecipe.Name}, 상대: {opponentRecipe.Name}, 나의 선공, 상대 AI: WindBot");
        }

        /// <summary>듀얼을 새로 시작한다. helperSeed: 임시 자동 응답("자동" 버튼)의 난수, opponentSeed: WindBot의 난수.</summary>
        void StartSession(DuelSessionOptions options, int helperSeed, int opponentSeed, string startLine)
        {
            _session?.Dispose();
            _session = null;
            _interaction = null;
            _interactionSerial = -1;
            _autoPlay = false;
            _log.Clear();
            CloseMenu();
            CloseModal();
            for (int p = 0; p < 2; p++)
                _playerName[p].text = _playerNames[p];
            _helper = new SimpleAutoPlayer(activateEffects: true, seed: helperSeed);
            _opponentAi = new WindBotPlayer(Opp, _db, seed: opponentSeed, hideOpponentInfo: !opponentSeesHiddenInfo);
            _opponentAi.ProblemReported += text => Debug.LogWarning($"[WindBot] {text}");
            _session = DuelSession.Create(options, _db, _scripts,
                new ManualPlayer { ChainPrompt = chainPrompt, PlaceSelect = placeSelect },
                _opponentAi);
            _session.MessageReceived += OnMessage;
            _session.EngineLog += OnEngineLog;
            foreach (var (type, text) in _session.StartupLog)
                OnEngineLog(type, text);
            _resultLogged = false;
            AddLog(startLine, UiStyle.Accent);
            _session.Start();
        }

        void Respond(byte[] response)
        {
            if (response == null)
                return;
            CloseMenu();
            CloseModal();
            RunSafely(() => _session.Respond(response));
        }

        void RunSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                _autoPlay = false;
                Debug.LogException(e);
                AddLog($"오류: {e.Message}", UiStyle.Opponent);
            }
            Render();
        }

        void OnMessage(DuelMessage message)
        {
            string line = _logFormatter.Format(message, _session.State);
            if (line == null)
                return;
            Color color = message is NewTurnMessage || message is WinMessage ? UiStyle.Accent
                : message is NewPhaseMessage ? UiStyle.TextDim : UiStyle.Text;
            AddLog(line, color);
        }

        void OnEngineLog(OcgLogType type, string text)
        {
            if (type == OcgLogType.Error)
            {
                Debug.LogWarning($"[엔진 오류] {text}");
                AddLog($"엔진 오류: {text}", UiStyle.Opponent);
            }
        }

        // ================= 그리기 =================

        void Render()
        {
            if (_session == null)
                return;
            var state = _session.State;

            // 새로 물을 때만 조작 방법을 다시 만든다 (같은 질문이면 고르던 카드를 그대로 둔다).
            int serial = _session.PendingRequest == null ? -1 : _session.PendingSerial;
            if (serial != _interactionSerial)
            {
                _interactionSerial = serial;
                _interaction = _session.PendingRequest == null ? null
                    : DuelInteraction.Build(_session.PendingRequest, state, _text, _session.IsRetry);
                _windowHidden = false;
                CloseMenu();
                CloseModal();
                if (_autoPlay)
                    _windowHidden = true; // 자동 진행 중에는 창을 띄우지 않는다 (멈추면 "선택 창 열기"로 연다)
                else if (_interaction?.Dialog != null)
                    OpenDialog(_interaction.Dialog);
                else if (_interaction?.Selection != null && _interaction.Selection.NeedsWindow)
                    OpenSelectionWindow();
            }

            for (byte p = 0; p < 2; p++)
            {
                var player = state.Players[p];
                _lp[p].text = $"LP {player.Lp}";
                _playerInfo[p].text = $"패 {player.Hand.Count} · 덱 {player.DeckCount} · 묘지 {player.Grave.Count}";
                for (int i = 0; i < MainZones; i++)
                {
                    SetCell(_monsters[p, i], player.MonsterZones[i]);
                    SetCell(_spells[p, i], player.SpellZones[i]);
                }
                SetCell(_fieldZone[p], player.SpellZones[5]);
                _deck[p].Set($"덱\n{player.DeckCount}", Touches(p, OcgLocation.Deck));
                _extra[p].Set($"엑스트라\n{player.ExtraCount}", Touches(p, OcgLocation.Extra));
                _grave[p].Set($"묘지\n{player.Grave.Count}", Touches(p, OcgLocation.Grave));
                _removed[p].Set($"제외\n{player.Removed.Count}", Touches(p, OcgLocation.Removed));
                RenderHand(p, player);
            }

            _turnLabel.text = state.Turn == 0 ? "듀얼 준비"
                : $"{state.Turn}턴 · {_text.Player(state.TurnPlayer)}의 턴 · {DuelText.ShortPhase(state.Phase)}";
            _chainBox.Clear();
            for (int i = 0; i < state.Chain.Count; i++)
            {
                var link = state.Chain[i];
                _chainBox.Add(UiStyle.Label($"체인 {i + 1}: {_text.CardName(link.Code)} ({_text.Player(link.Location.Controller)})", 15, UiStyle.Accent));
            }
            RenderStrip(state);
        }

        void RenderStrip(DuelState state)
        {
            _stripButtons.Clear();
            if (state.IsFinished)
            {
                if (!_resultLogged && _opponentAi != null)
                {
                    _resultLogged = true;
                    Debug.Log($"[WindBot] 듀얼 종료: 직접 답함 {_opponentAi.Answered}, 대신 답함 {_opponentAi.Fallbacks}, " +
                              $"필드 모델 맞춤 {_opponentAi.ModelResyncs}칸, 메시지 처리 예외 {_opponentAi.MessageErrors}");
                }
                string result = state.Winner == OcgConstants.PlayerNone ? "무승부" : state.Winner == Me ? "승리!" : "패배";
                _promptLabel.text = $"듀얼 종료: {result}";
                _promptDetail.text = $"{state.Turn}턴, 나 LP {state.Players[Me].Lp} / 상대 LP {state.Players[Opp].Lp}";
                if (_onLeave != null)
                    AddStripButton("결과 보기", LeaveDuel, true).name = "leave-duel";
                else if (!_external)
                    AddStripButton("다시 시작 (새 시드)", () => { duelSeed++; RunSafely(StartFreeDuel); }, true);
                return;
            }
            if (_autoPlay)
            {
                _promptLabel.text = "자동 진행 중...";
                _promptDetail.text = "임시 자동 응답이 듀얼이 끝날 때까지 대신 답한다 (개발용)";
                AddStripButton("자동 멈추기", () => SetAutoPlay(false), true).name = "auto-stop";
                return;
            }
            if (_interaction == null)
            {
                _promptLabel.text = "진행 중...";
                _promptDetail.text = "";
                return;
            }
            _promptLabel.text = _interaction.Prompt;
            var sel = _interaction.Selection;
            var places = _interaction.Places;
            string detail = _interaction.PromptDetail;
            if (sel != null && sel.NeedsConfirm)
                detail = sel.Progress + (detail != null ? $" · {detail}" : "");
            if (places != null && places.NeedsConfirm)
                detail = $"{places.Chosen.Count} / {places.Count}칸 골랐다" + (detail != null ? $" · {detail}" : "");
            _promptDetail.text = detail ?? "";

            foreach (var b in _interaction.Buttons)
            {
                var response = b.Response;
                AddStripButton(b.Label, () => Respond(response), true);
            }
            if (sel != null && sel.NeedsConfirm)
            {
                var confirm = AddStripButton(sel.ConfirmLabel, () => Respond(sel.Confirm()), true);
                confirm.SetEnabled(sel.CanConfirm);
            }
            if (places != null && places.NeedsConfirm)
            {
                var confirm = AddStripButton("확인", () => Respond(places.Confirm()), true);
                confirm.SetEnabled(places.CanConfirm);
            }
            if (sel != null && sel.NeedsWindow && _windowHidden)
                AddStripButton("카드 목록 열기", () => { _windowHidden = false; OpenSelectionWindow(); }, true);
            if (_interaction.Dialog != null && !_modalOpen)
                AddStripButton("선택 창 열기", () => OpenDialog(_interaction.Dialog), true);
            // 개발용: 임시 자동 응답이 대신 고르게 한다.
            if (devAutoButtons)
            {
                AddStripButton("자동", () => Respond(_helper.Respond(_interaction.Request, _session.State)), false).name = "auto-once";
                AddStripButton("자동으로 끝까지", () => SetAutoPlay(true), false).name = "auto-to-end";
            }
        }

        Button AddStripButton(string label, Action onClick, bool primary)
        {
            var b = UiStyle.Button(label, onClick, primary ? 17 : 13);
            b.style.marginLeft = 6;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (!primary) b.style.color = UiStyle.TextDim;
            _stripButtons.Add(b);
            return b;
        }

        void RenderHand(byte p, PlayerState player)
        {
            var row = _handRows[p];
            row.Clear();
            _handCells[p].Clear();
            foreach (var card in player.Hand)
            {
                var cell = new CardCell(this, 96, 92);
                SetCell(cell, card, p == Me || card.IsPublic);
                _handCells[p].Add(cell);
                row.Add(cell.Root);
            }
        }

        void SetCell(CardCell cell, CardInfo card, bool? visibleOverride = null)
        {
            bool visible = visibleOverride ?? IsVisible(card);
            cell.Set(card, visible);
            var places = _interaction?.Places;
            if (places != null && cell.Zone.HasValue)
            {
                var zone = cell.Zone.Value;
                if (!places.Contains(zone))
                {
                    cell.SetHighlight(null);
                    return;
                }
                bool chosen = places.IsChosen(zone);
                cell.SetHighlight(chosen ? Chosen : Selectable);
                if (card == null)
                    cell.ShowEmptyHint(chosen ? "고름" : places.IsDisableField ? "고르기" : "여기에 놓기", chosen ? Chosen : Selectable);
                return;
            }
            cell.SetHighlight(HighlightFor(card));
        }

        Color? HighlightFor(CardInfo card)
        {
            if (card == null || _interaction == null)
                return null;
            var key = new CardKey(card.Location);
            var sel = _interaction.Selection;
            if (sel != null)
            {
                var c = sel.Find(key);
                if (c == null) return null;
                return sel.IsChosen(c) ? Chosen : Selectable;
            }
            return _interaction.HasAction(key) ? Actionable : (Color?)null;
        }

        bool Touches(byte p, OcgLocation location) => _interaction != null && _interaction.Touches(p, location);

        bool IsVisible(CardInfo card) =>
            card != null && (card.Location.Controller == Me || card.IsFaceUp || card.IsPublic);

        // ================= 카드 누르기 =================

        void OnCardClicked(CardInfo card, bool visible, VisualElement anchor, Action refresh, CardKey? zone)
        {
            if (card != null)
                ShowCard(card, visible);
            if (_interaction == null)
                return;
            var places = _interaction.Places;
            if (places != null)
            {
                // 칸 고르기 중에는 카드가 아니라 칸을 고른다 (무효로 할 칸은 카드가 있는 칸일 수도 있다).
                if (zone == null || !places.Contains(zone.Value))
                    return;
                byte[] placeResponse = places.Click(zone.Value);
                if (placeResponse != null)
                    Respond(placeResponse);
                else
                    Render();
                return;
            }
            if (card == null)
                return;
            var key = new CardKey(card.Location);
            var sel = _interaction.Selection;
            if (sel != null)
            {
                var candidate = sel.Find(key);
                if (candidate != null)
                    ClickCandidate(sel, candidate, refresh);
                return;
            }
            if (_interaction.Actions.TryGetValue(key, out var actions))
                OpenMenu(anchor, actions);
        }

        /// <summary>목록 창의 후보를 눌렀을 때. 자리가 아니라 후보로 고르므로 같은 카드가 두 번 나와도 구분된다 (체인 순서 정하기).</summary>
        void OnCandidateClicked(SelectionCandidate candidate, CardInfo info, bool visible)
        {
            ShowCard(info, visible);
            var sel = _interaction?.Selection;
            if (sel != null && sel.Candidates.Contains(candidate))
                ClickCandidate(sel, candidate, OpenSelectionWindow);
        }

        void ClickCandidate(CardSelection sel, SelectionCandidate candidate, Action refresh)
        {
            byte[] response = sel.Click(candidate);
            if (response != null)
                Respond(response);
            else
            {
                refresh?.Invoke();
                Render();
            }
        }

        void OpenMenu(VisualElement anchor, List<CardAction> actions)
        {
            CloseMenu();
            _menuLayer.style.display = DisplayStyle.Flex;
            var catcher = new VisualElement();
            catcher.style.position = Position.Absolute;
            catcher.style.left = catcher.style.top = catcher.style.right = catcher.style.bottom = 0;
            catcher.RegisterCallback<PointerDownEvent>(_ => CloseMenu());
            _menuLayer.Add(catcher);

            var menu = UiStyle.PanelBox();
            menu.style.position = Position.Absolute;
            menu.style.minWidth = 170;
            UiStyle.BorderColor(menu, UiStyle.Accent, 2);
            foreach (var a in actions)
            {
                var response = a.Response;
                menu.Add(UiStyle.Button(a.Label, () => Respond(response)));
            }
            Rect bound = anchor.worldBound;
            Vector2 topLeft = _menuLayer.WorldToLocal(new Vector2(bound.xMax + 6, bound.yMin));
            menu.style.left = topLeft.x;
            menu.style.top = topLeft.y;
            // 화면 밖으로 나가면 안쪽으로 당긴다 (패는 화면 아래, 오른쪽 끝 칸 등).
            menu.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var layer = _menuLayer.layout;
                float left = Mathf.Min(menu.layout.x, layer.width - menu.layout.width - 8);
                if (left < menu.layout.x)
                    left = _menuLayer.WorldToLocal(new Vector2(bound.xMin, bound.yMin)).x - menu.layout.width - 6;
                float top = Mathf.Min(menu.layout.y, layer.height - menu.layout.height - 8);
                if (!Mathf.Approximately(left, menu.layout.x)) menu.style.left = Mathf.Max(8, left);
                if (!Mathf.Approximately(top, menu.layout.y)) menu.style.top = Mathf.Max(8, top);
            });
            _menuLayer.Add(menu);
        }

        void CloseMenu()
        {
            if (_menuLayer == null) return;
            _menuLayer.Clear();
            _menuLayer.style.display = DisplayStyle.None;
        }

        // ================= 창 =================

        VisualElement OpenModal(string title, string detail, bool closable)
        {
            CloseModal();
            _modalLayer.style.display = DisplayStyle.Flex;
            _modalOpen = true;
            var panel = UiStyle.PanelBox();
            panel.style.maxWidth = 980;
            panel.style.maxHeight = Length.Percent(80);
            panel.style.minWidth = 420;
            UiStyle.BorderColor(panel, UiStyle.Accent, 2);
            var head = UiStyle.Box(null, FlexDirection.Row);
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.Add(UiStyle.Label(title, 20, UiStyle.Accent, bold: true));
            if (closable)
            {
                var close = UiStyle.Button("닫기", CloseModal, 15);
                close.style.marginLeft = 16;
                head.Add(close);
            }
            panel.Add(head);
            if (!string.IsNullOrEmpty(detail))
            {
                var d = UiStyle.Label(detail, 15, UiStyle.TextDim);
                d.style.marginTop = 6;
                panel.Add(d);
            }
            _modalLayer.Add(panel);
            return panel;
        }

        void CloseModal()
        {
            if (_modalLayer == null) return;
            _modalLayer.Clear();
            _modalLayer.style.display = DisplayStyle.None;
            _modalOpen = false;
        }

        void ShowMessage(string title, string detail)
        {
            var panel = OpenModal(title, detail, closable: true);
            panel.style.maxWidth = 700;
        }

        void OpenDialog(DialogSpec spec)
        {
            var panel = OpenModal(spec.Title, spec.Detail, closable: false);
            var list = UiStyle.Box();
            list.style.marginTop = 10;
            panel.Add(list);
            foreach (var b in spec.Buttons)
            {
                var response = b.Response;
                list.Add(UiStyle.Button(b.Label, () => Respond(response)));
            }
            if (spec.Toggles != null)
            {
                ulong chosen = 0;
                Button confirm = null;
                foreach (var (label, bit) in spec.Toggles)
                {
                    Button t = null;
                    t = UiStyle.Button(label, () =>
                    {
                        chosen ^= bit;
                        UiStyle.SetSelected(t, (chosen & bit) != 0);
                        confirm.SetEnabled(PopCount(chosen) == spec.ToggleCount);
                    });
                    list.Add(t);
                }
                confirm = UiStyle.Button("확인", () => Respond(spec.BuildToggles(chosen)));
                confirm.SetEnabled(false);
                list.Add(confirm);
            }
            if (spec.AskCardCode)
            {
                var field = new TextField();
                field.style.fontSize = 17;
                list.Add(field);
                list.Add(UiStyle.Button("확인", () =>
                {
                    if (uint.TryParse(field.value.Trim(), out uint code))
                        Respond(DuelResponses.AnnounceCard(code));
                }));
            }
            var row = UiStyle.Box(null, FlexDirection.Row);
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 10;
            var view = UiStyle.Button("필드 보기", () => { CloseModal(); Render(); }, 14);
            view.style.color = UiStyle.TextDim;
            row.Add(view);
            panel.Add(row);
        }

        /// <summary>필드·패 밖의 카드를 고르는 창. 후보를 카드로 늘어놓고 눌러서 고른다.</summary>
        void OpenSelectionWindow()
        {
            var sel = _interaction?.Selection;
            if (sel == null)
                return;
            var panel = OpenModal(_interaction.Prompt, _interaction.PromptDetail, closable: false);
            var grid = CardGrid(panel);
            foreach (var candidate in sel.Candidates)
            {
                var info = MakeInfo(candidate.Card);
                // 순서를 정할 카드는 엔진이 정하는 사람에게 보여 준 카드다 (상대 덱 위의 카드라도).
                bool visible = sel.Ordered ? candidate.Card.Code != 0 : IsVisibleRequestCard(candidate.Card, info);
                var cell = new CardCell(this, 112, 104);
                cell.ClickOverride = () => OnCandidateClicked(candidate, info, visible);
                cell.Set(info, visible, candidate.Extra);
                cell.SetHighlight(sel.IsChosen(candidate) ? Chosen : Selectable);
                cell.SetCaption(LocationCaption(candidate.Card.Location));
                int number = sel.OrderOf(candidate);
                cell.SetBadge(number > 0 ? number.ToString() : null);
                grid.Add(cell.Root);
            }
            var row = UiStyle.Box(null, FlexDirection.Row);
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 10;
            if (sel.NeedsConfirm)
            {
                row.Add(UiStyle.Label(sel.Progress + "  ", 15, UiStyle.TextDim));
                var confirm = UiStyle.Button(sel.ConfirmLabel, () => Respond(sel.Confirm()));
                confirm.SetEnabled(sel.CanConfirm);
                row.Add(confirm);
            }
            foreach (var b in _interaction.Buttons)
            {
                var response = b.Response;
                var button = UiStyle.Button(b.Label, () => Respond(response));
                button.style.marginLeft = 6;
                row.Add(button);
            }
            var view = UiStyle.Button("필드 보기", () => { _windowHidden = true; CloseModal(); Render(); }, 14);
            view.style.marginLeft = 6;
            view.style.color = UiStyle.TextDim;
            row.Add(view);
            panel.Add(row);
        }

        /// <summary>묘지·제외·엑스트라 덱(·덱) 안의 카드를 보여 주는 창. 행동할 수 있는 카드는 눌러서 메뉴를 연다.</summary>
        void OpenPileWindow(byte p, OcgLocation location)
        {
            var state = _session.State;
            var player = state.Players[p];
            List<CardInfo> cards;
            string name;
            switch (location)
            {
                case OcgLocation.Grave: cards = player.Grave; name = "묘지"; break;
                case OcgLocation.Removed: cards = player.Removed; name = "제외 존"; break;
                case OcgLocation.Extra: cards = player.Extra; name = "엑스트라 덱"; break;
                default: cards = new List<CardInfo>(); name = "덱"; break;
            }
            // 덱처럼 내용을 조회하지 않는 곳은 요청에 나온 카드만 보여 준다.
            if (location == OcgLocation.Deck && _interaction != null)
                cards = _interaction.Actions.Keys.Where(k => k.Controller == p && k.Location == location)
                    .Select(k => MakeInfo(RequestCard(k))).ToList();

            var panel = OpenModal($"{_text.Player(p)}의 {name} ({cards.Count}장)", null, closable: true);
            var grid = CardGrid(panel);
            if (cards.Count == 0)
                grid.Add(UiStyle.Label("비어 있음", 16, UiStyle.TextDim));
            foreach (var card in cards)
            {
                bool visible = p == Me || card.IsFaceUp || card.IsPublic || location == OcgLocation.Grave;
                var cell = new CardCell(this, 112, 104, refresh: () => OpenPileWindow(p, location));
                cell.Set(card, visible);
                cell.SetHighlight(HighlightFor(card));
                grid.Add(cell.Root);
            }
        }

        static VisualElement CardGrid(VisualElement panel)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.marginTop = 10;
            scroll.style.flexShrink = 1;
            var grid = scroll.contentContainer;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            panel.Add(scroll);
            return grid;
        }

        /// <summary>요청에 나온 카드(코드 + 위치)를 화면용 카드 정보로 만든다. 필드 등에 있으면 조회한 상태를 쓴다.</summary>
        CardInfo MakeInfo(CardRef card)
        {
            var found = _session.State.Find(card.Location);
            if (found != null && (found.Code == card.Code || card.Code == 0))
                return found;
            var info = new CardInfo { Location = card.Location, Code = card.Code };
            if (_db.TryGet(card.Code, out var record))
            {
                info.Type = record.Type;
                info.Level = record.Data.Level;
                info.Rank = record.Data.Level;
                info.Attribute = (OcgAttribute)record.Data.Attribute;
                info.Race = record.Data.Race;
                info.Attack = info.BaseAttack = record.Data.Attack;
                info.Defense = info.BaseDefense = record.Data.Defense;
            }
            return info;
        }

        CardRef RequestCard(CardKey key)
        {
            switch (_interaction?.Request)
            {
                case SelectIdleCmdRequest r:
                    foreach (var c in r.Summonable.Concat(r.SpecialSummonable).Concat(r.MonsterSetable).Concat(r.SpellSetable).Concat(r.Repositionable))
                        if (new CardKey(c.Location).Equals(key)) return c;
                    foreach (var e in r.Activatable)
                        if (new CardKey(e.Location).Equals(key)) return new CardRef(e.Code, e.Location);
                    break;
                case SelectBattleCmdRequest r:
                    foreach (var e in r.Activatable)
                        if (new CardKey(e.Location).Equals(key)) return new CardRef(e.Code, e.Location);
                    break;
                case SelectChainRequest r:
                    foreach (var e in r.Chains)
                        if (new CardKey(e.Location).Equals(key)) return new CardRef(e.Code, e.Location);
                    break;
            }
            return default;
        }

        /// <summary>요청에 나온 카드를 내가 볼 수 있는지. 상대의 패·덱·뒷면 카드는 숨긴다.</summary>
        static bool IsVisibleRequestCard(CardRef card, CardInfo info)
        {
            var loc = card.Location;
            if (card.Code == 0)
                return false;
            if (loc.Controller == Me)
                return true;
            if (info != null && (info.IsPublic || info.IsFaceUp))
                return true;
            bool faceUp = (loc.Position & OcgPosition.FaceDown) == 0;
            return loc.Location == OcgLocation.Grave
                   || (loc.Location == OcgLocation.Removed && faceUp)
                   || (loc.Location == OcgLocation.Extra && (loc.Position & OcgPosition.FaceUp) != 0);
        }

        string LocationCaption(CardLocation loc) => $"{_text.Player(loc.Controller)} {DuelText.Location(loc)}";

        // ================= 카드 정보 =================

        void ShowCard(CardInfo card, bool visible)
        {
            if (card == null)
                return;
            if (!visible)
            {
                _detailName.text = "뒷면 카드";
                _detailType.text = $"{_text.Player(card.Location.Controller)}의 {DuelText.Location(card.Location)}";
                _detailStats.text = "";
                _detailText.text = "";
                return;
            }
            var record = _text.Record(card.Code);
            _detailName.text = _text.CardName(card.Code);
            _detailType.text = DuelText.TypeLine(card.Type != 0 ? card.Type : record?.Type ?? OcgCardType.None,
                card.IsMonster && (card.Type & OcgCardType.Xyz) != 0 ? card.Rank : card.Level, card.Attribute, card.Race);
            _detailStats.text = card.IsMonster
                ? $"ATK {card.Attack} / DEF {card.Defense}" + (card.Attack != card.BaseAttack || card.Defense != card.BaseDefense ? $"  (원래 {card.BaseAttack} / {card.BaseDefense})" : "")
                  + (card.Location.IsOnField ? $" · {DuelText.Position(card.Position)}" : "")
                : card.Location.IsOnField ? DuelText.Position(card.Position) : "";
            _detailText.text = record?.Description ?? "";
        }

        void AddLog(string line, Color color)
        {
            var label = UiStyle.Label(line, 15, color);
            label.style.marginBottom = 2;
            _log.Add(label);
            while (_log.contentContainer.childCount > MaxLogLines)
                _log.contentContainer.RemoveAt(0);
            _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0, float.MaxValue)).ExecuteLater(10);
        }

        // ================= 화면 구성 =================

        void CreateDocument()
        {
            var docRoot = _docRoot = UiStyle.CreateDocument(transform, "DuelUI");

            _root = UiStyle.Box("duel", FlexDirection.Row);
            _root.style.flexGrow = 1;
            _root.style.backgroundColor = UiStyle.Background;
            UiStyle.Padding(_root, 12);
            docRoot.Add(_root);

            // 창(대화창·카드 목록)과 카드 메뉴는 화면 전체를 덮는 층에 띄운다.
            _modalLayer = OverlayLayer(new Color(0, 0, 0, 0.45f));
            _modalLayer.style.justifyContent = Justify.Center;
            _modalLayer.style.alignItems = Align.Center;
            docRoot.Add(_modalLayer);
            _menuLayer = OverlayLayer(Color.clear);
            docRoot.Add(_menuLayer);
        }

        static VisualElement OverlayLayer(Color background)
        {
            var layer = new VisualElement();
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            layer.style.backgroundColor = background;
            layer.style.display = DisplayStyle.None;
            return layer;
        }

        void BuildLayout()
        {
            // ---- 왼쪽: 상대 정보 / 카드 정보(내 초상화 자리) / 내 정보 ----
            var left = UiStyle.Box("left");
            left.style.width = 340;
            left.style.marginRight = 12;
            left.Add(PlayerPanel(Opp));
            var detail = UiStyle.PanelBox("card-detail");
            detail.style.flexGrow = 1;
            detail.style.marginTop = detail.style.marginBottom = 12;
            _detailName = UiStyle.Label("카드를 누르면 여기에 정보가 나온다.", 20, bold: true);
            _detailType = UiStyle.Label("", 15, UiStyle.TextDim);
            _detailStats = UiStyle.Label("", 16, UiStyle.Accent);
            _detailType.style.marginTop = _detailStats.style.marginTop = 6;
            var textScroll = new ScrollView(ScrollViewMode.Vertical);
            textScroll.style.flexGrow = 1;
            textScroll.style.marginTop = 10;
            _detailText = UiStyle.Label("", 15);
            textScroll.Add(_detailText);
            detail.Add(_detailName);
            detail.Add(_detailType);
            detail.Add(_detailStats);
            detail.Add(textScroll);
            left.Add(detail);
            left.Add(PlayerPanel(Me));
            _root.Add(left);

            // ---- 가운데: 필드 ----
            var center = UiStyle.Box("field");
            center.style.flexGrow = 1;
            center.style.justifyContent = Justify.SpaceBetween;
            center.style.alignItems = Align.Center;
            _handRows[Opp] = HandRow();
            center.Add(_handRows[Opp]);
            center.Add(BackRow(Opp));
            center.Add(FrontRow(Opp));
            center.Add(CenterStrip());
            center.Add(FrontRow(Me));
            center.Add(BackRow(Me));
            _handRows[Me] = HandRow();
            center.Add(_handRows[Me]);
            _root.Add(center);

            // ---- 오른쪽: 로그 ----
            var right = UiStyle.PanelBox("log");
            right.style.width = 460;
            right.style.marginLeft = 12;
            right.Add(UiStyle.Label("로그", 17, UiStyle.TextDim, bold: true));
            _log = new ScrollView(ScrollViewMode.Vertical);
            _log.style.flexGrow = 1;
            _log.style.marginTop = 6;
            right.Add(_log);
            _root.Add(right);
        }

        VisualElement PlayerPanel(byte p)
        {
            var panel = UiStyle.PanelBox();
            var nameLabel = _playerName[p] = UiStyle.Label(_playerNames[p], 18, p == Me ? UiStyle.Mine : UiStyle.Opponent, bold: true);
            _lp[p] = UiStyle.Label("LP 8000", 38, bold: true);
            _playerInfo[p] = UiStyle.Label("", 15, UiStyle.TextDim);
            panel.Add(nameLabel);
            panel.Add(_lp[p]);
            panel.Add(_playerInfo[p]);
            return panel;
        }

        static VisualElement HandRow()
        {
            var row = UiStyle.Box(null, FlexDirection.Row);
            row.style.height = 96;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;
            return row;
        }

        static VisualElement FieldRow()
        {
            var row = UiStyle.Box(null, FlexDirection.Row);
            row.style.alignItems = Align.Center;
            return row;
        }

        // 내 쪽 몬스터 줄: [필드 존][M0..M4][제외(위)·묘지]. 상대 쪽은 점대칭: [묘지·제외(아래)][M4..M0][필드 존]
        VisualElement FrontRow(byte p)
        {
            var row = FieldRow();
            _fieldZone[p] = new CardCell(this, 104, 104, zone: new CardKey(p, OcgLocation.SpellTrapZone, 5));
            for (int i = 0; i < MainZones; i++)
                _monsters[p, i] = new CardCell(this, 104, 104, zone: new CardKey(p, OcgLocation.MonsterZone, (uint)i));
            var gravePile = GraveStack(p);
            if (p == Me)
            {
                row.Add(_fieldZone[p].Root);
                for (int i = 0; i < MainZones; i++) row.Add(_monsters[p, i].Root);
                row.Add(gravePile);
            }
            else
            {
                row.Add(gravePile);
                for (int i = MainZones - 1; i >= 0; i--) row.Add(_monsters[p, i].Root);
                row.Add(_fieldZone[p].Root);
            }
            _fieldZone[p].SetEmptyText("필드");
            return row;
        }

        // 내 쪽 마법·함정 줄: [엑스트라][S0..S4][덱]. 상대 쪽은 점대칭: [덱][S4..S0][엑스트라]
        VisualElement BackRow(byte p)
        {
            var row = FieldRow();
            for (int i = 0; i < MainZones; i++)
                _spells[p, i] = new CardCell(this, 104, 92, zone: new CardKey(p, OcgLocation.SpellTrapZone, (uint)i));
            _deck[p] = new PileCell(104, 92, 6, () => OpenPileWindow(p, OcgLocation.Deck));
            _extra[p] = new PileCell(104, 92, 6, () => OpenPileWindow(p, OcgLocation.Extra));
            if (p == Me)
            {
                row.Add(_extra[p].Root);
                for (int i = 0; i < MainZones; i++) row.Add(_spells[p, i].Root);
                row.Add(_deck[p].Root);
            }
            else
            {
                row.Add(_deck[p].Root);
                for (int i = MainZones - 1; i >= 0; i--) row.Add(_spells[p, i].Root);
                row.Add(_extra[p].Root);
            }
            return row;
        }

        // 묘지(묘비 모양) + 원형 제외 칸. 제외 칸은 묘지 바로 옆, 필드 가운데(양쪽 필드 사이) 쪽에 둔다.
        VisualElement GraveStack(byte p)
        {
            var stack = UiStyle.Box();
            stack.style.alignItems = Align.Center;
            stack.style.width = 104;
            _grave[p] = new PileCell(92, 62, 0, () => OpenPileWindow(p, OcgLocation.Grave));
            _grave[p].Root.style.borderTopLeftRadius = _grave[p].Root.style.borderTopRightRadius = 40; // 묘비
            _removed[p] = new PileCell(44, 44, 22, () => OpenPileWindow(p, OcgLocation.Removed));      // 원형
            if (p == Me)
            {
                stack.Add(_removed[p].Root);
                stack.Add(_grave[p].Root);
            }
            else
            {
                stack.Add(_grave[p].Root);
                stack.Add(_removed[p].Root);
            }
            _removed[p].Root.style.marginTop = _removed[p].Root.style.marginBottom = 2;
            return stack;
        }

        // 양쪽 필드 사이: 위 줄은 턴·페이즈와 체인 목록(센터 카드 자리), 아래 줄은 안내 문구와 버튼(페이즈 넘기기, 확인 등).
        VisualElement CenterStrip()
        {
            var strip = UiStyle.Box();
            strip.style.width = Length.Percent(100);
            strip.style.backgroundColor = UiStyle.PanelLight;
            UiStyle.Radius(strip, 6);
            UiStyle.Padding(strip, 8);

            var top = UiStyle.Box(null, FlexDirection.Row);
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            _turnLabel = UiStyle.Label("듀얼 준비", 19, bold: true);
            _chainBox = UiStyle.Box();
            _chainBox.style.alignItems = Align.FlexEnd;
            top.Add(_turnLabel);
            top.Add(_chainBox);

            var bottom = UiStyle.Box(null, FlexDirection.Row);
            bottom.style.justifyContent = Justify.SpaceBetween;
            bottom.style.alignItems = Align.Center;
            bottom.style.marginTop = 6;
            var texts = UiStyle.Box();
            texts.style.flexShrink = 1;
            _promptLabel = UiStyle.Label("", 17, UiStyle.Accent, bold: true);
            _promptDetail = UiStyle.Label("", 14, UiStyle.TextDim);
            texts.Add(_promptLabel);
            texts.Add(_promptDetail);
            _stripButtons = UiStyle.Box(null, FlexDirection.Row);
            _stripButtons.style.flexShrink = 0;
            _stripButtons.style.alignItems = Align.Center;
            bottom.Add(texts);
            bottom.Add(_stripButtons);

            strip.Add(top);
            strip.Add(bottom);
            return strip;
        }

        static int PopCount(ulong v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1) n++;
            return n;
        }

        // ================= 칸 =================

        sealed class CardCell
        {
            public readonly VisualElement Root;
            /// <summary>필드 칸이면 그 자리 (칸 고르기용). 패·창 안의 카드는 null.</summary>
            public readonly CardKey? Zone;
            readonly Label _name;
            readonly Label _stats;
            readonly Label _caption;
            Label _badge;
            readonly DuelScreen _screen;
            /// <summary>있으면 누를 때 OnCardClicked 대신 부른다 (목록 창의 후보).</summary>
            public Action ClickOverride;
            CardInfo _card;
            bool _visible;
            string _emptyText = "";
            Color _border;
            float _borderWidth;

            public CardCell(DuelScreen screen, float width, float height, Action refresh = null, CardKey? zone = null)
            {
                _screen = screen;
                Zone = zone;
                Root = UiStyle.Box();
                Root.style.width = width;
                Root.style.height = height;
                UiStyle.Margin(Root, 3);
                UiStyle.Radius(Root, 5);
                UiStyle.Padding(Root, 5);
                Root.style.justifyContent = Justify.SpaceBetween;
                Root.style.overflow = Overflow.Hidden;
                _name = UiStyle.Label("", 13, bold: true);
                _stats = UiStyle.Label("", 13, UiStyle.Text);
                _caption = UiStyle.Label("", 11, UiStyle.TextDim);
                _caption.style.display = DisplayStyle.None;
                Root.Add(_name);
                Root.Add(_caption);
                Root.Add(_stats);
                Root.RegisterCallback<ClickEvent>(_ =>
                {
                    if (ClickOverride != null)
                        ClickOverride();
                    else
                        _screen.OnCardClicked(_card, _visible, Root, refresh, Zone);
                });
                Set(null, false);
            }

            /// <summary>오른쪽 아래에 크게 띄우는 표시 (순서 정하기의 번호). null이나 빈 문자열이면 숨긴다.</summary>
            public void SetBadge(string text)
            {
                if (_badge == null)
                {
                    if (string.IsNullOrEmpty(text))
                        return;
                    _badge = UiStyle.Label("", 20, UiStyle.Background, bold: true);
                    _badge.style.position = Position.Absolute;
                    _badge.style.right = 4;
                    _badge.style.bottom = 4;
                    _badge.style.backgroundColor = Chosen;
                    _badge.style.paddingLeft = 7;
                    _badge.style.paddingRight = 7;
                    _badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                    UiStyle.Radius(_badge, 4);
                    Root.Add(_badge);
                }
                _badge.text = text ?? "";
                _badge.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            public void SetEmptyText(string text)
            {
                _emptyText = text;
                if (_card == null) _name.text = text;
            }

            /// <summary>빈칸에 잠깐 띄우는 안내 (칸 고르기). 다음 Set에서 원래 문구로 돌아간다.</summary>
            public void ShowEmptyHint(string text, Color color)
            {
                if (_card != null) return;
                _name.text = text;
                _name.style.color = color;
            }

            public void SetCaption(string text)
            {
                _caption.text = text;
                _caption.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            public void Set(CardInfo card, bool visible, string extra = null)
            {
                _card = card;
                _visible = visible;
                if (card == null)
                {
                    _name.text = _emptyText;
                    _name.style.color = UiStyle.TextDim;
                    _stats.text = "";
                    Root.style.backgroundColor = UiStyle.ZoneEmpty;
                    SetBorder(UiStyle.Border, 1);
                    return;
                }
                bool onField = card.Location.IsOnField;
                bool faceDown = onField && !card.IsFaceUp;
                _name.style.color = UiStyle.Text;
                _name.text = visible ? _screen._text.CardName(card.Code) : "뒷면";
                string stats;
                if (!visible)
                    stats = onField && (card.Position & OcgPosition.Defense) != 0 ? "수비" : "";
                else if (card.IsMonster)
                    stats = $"{card.Attack}/{card.Defense}" + (onField ? ((card.Position & OcgPosition.Defense) != 0 ? " 수비" : " 공격") : "") + (faceDown ? " 세트" : "");
                else
                    stats = ((card.Type & OcgCardType.Spell) != 0 ? "마법" : "함정") + (faceDown ? " 세트" : "");
                _stats.text = string.IsNullOrEmpty(extra) ? stats : $"{stats} {extra}";
                Root.style.backgroundColor = !visible || faceDown ? UiStyle.FaceDown : CardColor(card.Type);
                var owner = card.Location.Controller == Me ? UiStyle.Mine : UiStyle.Opponent;
                SetBorder(onField ? owner : UiStyle.Border, onField ? 2 : 1);
            }

            /// <summary>행동·선택 강조. null이면 원래 테두리로 돌린다.</summary>
            public void SetHighlight(Color? color)
            {
                if (color.HasValue)
                    UiStyle.BorderColor(Root, color.Value, 4);
                else
                    UiStyle.BorderColor(Root, _border, _borderWidth);
            }

            void SetBorder(Color color, float width)
            {
                _border = color;
                _borderWidth = width;
                UiStyle.BorderColor(Root, color, width);
            }

            static Color CardColor(OcgCardType type)
            {
                if ((type & OcgCardType.Spell) != 0) return UiStyle.Spell;
                if ((type & OcgCardType.Trap) != 0) return UiStyle.Trap;
                if ((type & (OcgCardType.Fusion | OcgCardType.Synchro | OcgCardType.Xyz | OcgCardType.Ritual)) != 0) return UiStyle.ExtraMonster;
                if ((type & OcgCardType.Normal) != 0) return UiStyle.NormalMonster;
                return UiStyle.EffectMonster;
            }
        }

        sealed class PileCell
        {
            public readonly VisualElement Root;
            readonly Label _label;

            public PileCell(float width, float height, float radius, Action onClick)
            {
                Root = UiStyle.Box();
                Root.style.width = width;
                Root.style.height = height;
                UiStyle.Margin(Root, 3);
                UiStyle.Radius(Root, radius);
                Root.style.backgroundColor = UiStyle.PanelLight;
                UiStyle.BorderColor(Root, UiStyle.Border, 1);
                Root.style.justifyContent = Justify.Center;
                Root.style.alignItems = Align.Center;
                _label = UiStyle.Label("", 13, UiStyle.TextDim);
                _label.style.unityTextAlign = TextAnchor.MiddleCenter;
                Root.Add(_label);
                Root.RegisterCallback<ClickEvent>(_ => onClick());
            }

            public void Set(string text, bool highlight)
            {
                _label.text = text;
                UiStyle.BorderColor(Root, highlight ? Actionable : UiStyle.Border, highlight ? 4 : 1);
            }
        }
    }
}
