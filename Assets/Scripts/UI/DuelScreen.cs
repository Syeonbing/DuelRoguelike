// 듀얼 화면 (UI Toolkit). docs/UI.md "듀얼"의 배치를 따른다:
// 가운데 필드(점대칭, 엑스트라 몬스터 존 없음, 내 패는 아래 부채꼴·상대 패는 위), 왼쪽에 상대 정보·상세 카드·내 정보,
// 오른쪽에 턴·페이즈·버튼·방금 일어난 일, 오른쪽에서 나오는 로그 서랍. 필드·패·카드 더미·상세 카드·패널은 DuelScreen.Field.cs에 있다.
// 조작: 카드를 직접 눌러 행동을 고른다. 행동할 수 있는 카드·고를 수 있는 카드·고를 수 있는 칸은 테두리로 강조한다.
// 칸 고르기(놓을 칸·무효로 할 칸)는 필드 칸(빈칸 포함)을 눌러서 고른다.
// 진행: 엔진 메시지를 한 단계씩 보여 준다. 발동·공격·데미지 계산·LP·파괴 등에서는 잠깐 멈추고, 그 카드에 테두리를 치고,
// 가운데 띠에 그 단계를 띄운다 (속도: 빠름·보통·느림, 필드를 누르면 다음 입력까지 건너뜀).
// 체인: 오른쪽 패널의 체인 목록(번호·카드·효과·처리 상태)과 필드 카드 왼쪽 위 체인 번호. 센터 카드, 카드가 움직이는 연출은 아직 없다.
// 보조 키: R·L 로그 서랍, C·I 효과문 넘기기, Q 두 번 다음 페이즈, E 두 번 턴 종료, 1·2·3 자동·속도·체인 확인.
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
    public sealed partial class DuelScreen : MonoBehaviour
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

        /// <summary>엔진에서 나의 팀 (선공 = 0). 런에서는 코인 토스로 후공이면 1이다 (SetSeat).</summary>
        byte Me = 0;
        byte Opp = 1;
        const int MainZones = 5;
        const int MaxLogLines = 800;

        static readonly Color Actionable = UiStyle.Accent;
        static readonly Color Selectable = UiStyle.Hex(0x4fd1e8);
        static readonly Color Chosen = UiStyle.Hex(0x5ee07a);

        CardDatabase _db;
        CardScriptSource _scripts;
        DuelSession _session;
        DuelText _text;
        CardFaces _faces;
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

        // 한 단계씩 보여 주기: 엔진 메시지를 하나씩 반영하고(DuelSession.Step), 중요한 메시지(발동·공격·데미지 계산·LP·파괴 등)에서는
        // 잠깐 멈춰 그 단계의 필드와 가운데 줄을 보여 준다. 멈추는 시간은 이 화면이 정한다. 필드를 누르면 다음 입력까지 건너뛴다.
        // "자동으로 끝까지"는 멈추지 않는다 (DuelSession.Respond로 한 번에 진행한다).
        enum PlaybackSpeed { Fast, Normal, Slow }
        const string SpeedPrefKey = "duel.playback-speed";
        const float PlaybackSecondsPerFrame = 0.02f;
        static readonly Color FocusActor = UiStyle.Hex(0xff9a3c);
        static readonly Color FocusTarget = UiStyle.Hex(0xff5a5a);
        static readonly Color OpponentLine = UiStyle.Hex(0xf2a58e);
        PlaybackSpeed _speed = PlaybackSpeed.Normal;
        bool _pacing;              // Step으로 한 단계씩 진행하는 중 (OnMessage가 멈출 단계를 고른다)
        bool _skipping;            // 필드를 눌러 다음 입력까지 건너뛰는 중
        bool _stalled;             // 진행 중 오류가 나서 멈췄다
        PlaybackFocus _focus;      // 지금 멈춰 보여 주는 단계 (있는 동안은 다음 입력을 묻지 않는다)
        PlaybackFocus _nextFocus;  // 방금 Step에서 OnMessage가 고른 단계
        float _holdUntil;
        CardLocation? _lastChaining;

        // 플레이 편의
        const ulong HintAttackTarget = 549;   // HINTMSG_ATTACKTARGET: 공격할 대상을 고를 때의 안내
        CardLocation? _attacker;              // 배틀 페이즈에 공격하겠다고 고른 내 몬스터 (공격 대상 예상 결과에 쓴다)
        int _solvingChain;                    // 지금 처리 중인 체인 번호 (0이면 없음)
        readonly Dictionary<int, string> _chainStatus = new Dictionary<int, string>(); // 체인 번호 → 처리됨·발동 무효·효과 무효
        VisualElement _chainPanel;            // 오른쪽 위 체인 목록 (체인이 있을 때만)
        ManualPlayer _manual;                 // 나 (체인 확인 방식을 듀얼 중에 바꾼다)
        const string ChainPromptPrefKey = "duel.chain-prompt";
        bool _previewing;                     // 카드에 마우스를 올려 카드 정보를 보는 중

        DuelInteraction _interaction;
        int _interactionSerial = -1;
        bool _windowHidden;
        bool _modalOpen;

        VisualElement _docRoot, _root;
        readonly Label[] _playerName = new Label[2];
        readonly Label[] _lp = new Label[2];
        readonly Label[] _lpDelta = new Label[2];
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
        VisualElement _stripButtons;
        ScrollView _log;
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
            // 혼자 듀얼할 때는 등급을 보이지 않는다 (등급은 런의 것).
            _faces = new CardFaces(_text, null, CardImages.FromGameFolder());
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

        void Awake()
        {
            _speed = (PlaybackSpeed)Mathf.Clamp(PlayerPrefs.GetInt(SpeedPrefKey, (int)PlaybackSpeed.Normal), 0, 2);
            // 듀얼 화면에서 고른 체인 확인 방식이 있으면 인스펙터 값보다 그것을 쓴다.
            if (PlayerPrefs.HasKey(ChainPromptPrefKey))
                chainPrompt = (ChainPromptMode)Mathf.Clamp(PlayerPrefs.GetInt(ChainPromptPrefKey), 0, 2);
        }

        void OnDestroy() => _session?.Dispose();

        void Update()
        {
            HandleKeys();
            if (_autoPlay)
                AutoPlayStep();
            else
                AdvancePlayback();
            UpdateStatusBadge();
            UpdateKeyCursor();
        }

        // ================= 모서리 상태 표시 =================

        Label _statusBadge;
        bool _spaceHeld;
        string _badgeText;
        float _badgeUntil;
        const float BadgeLingerSeconds = 0.3f; // 건너뜀·가속 표시: 끝난 뒤 그대로 두는 시간
        const float BadgeFadeSeconds = 0.6f;   // 그 뒤 흐려지며 사라지는 시간
        const string SkipText = "건너뜀 · 다음 입력까지";
        const string SpeedUpText = "가속 중 · 스페이스";
        const string FieldViewText = "필드 확인 중 · Esc로 선택 창 다시 열기";

        /// <summary>선택 창(예·아니오, 카드 고르기)을 닫고 필드를 보는 중인지.</summary>
        bool InFieldView => !_autoPlay && _interaction != null && !_modalOpen
            && (_interaction.Dialog != null || (_interaction.Selection != null && _interaction.Selection.NeedsWindow && _windowHidden));

        /// <summary>
        /// 필드 오른쪽 위 모서리에 지금 상태를 띄운다: 건너뜀(엔터·필드 누르기) / 가속 중(스페이스) / 필드 확인 중(선택 창을 닫고 필드를 볼 때).
        /// 건너뜀·가속 표시는 끝나도 잠깐 남겼다가 흐려지며 사라진다 (건너뛰기는 한두 프레임에 끝나기도 한다). 필드 확인 중은 바로 사라진다.
        /// </summary>
        void UpdateStatusBadge()
        {
            if (_statusBadge == null)
                return;
            bool playing = _session != null && !_autoPlay && !_stalled && (_focus != null || _session.CanStep);
            string text = null;
            Color color = UiStyle.Accent;
            if (playing && _skipping)
                text = SkipText;
            else if (playing && _spaceHeld)
                text = SpeedUpText;
            else if (_session != null && InFieldView)
            {
                text = FieldViewText;
                color = Selectable;
            }

            float now = Time.realtimeSinceStartup;
            if (text != null)
            {
                if (text != FieldViewText)
                    _badgeUntil = now + BadgeLingerSeconds;
                SetBadge(text, color);
                return;
            }
            if (_badgeText == null || _badgeText == FieldViewText)
            {
                SetBadge(null, color);
                return;
            }
            float fade = (now - _badgeUntil) / BadgeFadeSeconds;
            if (fade >= 1f)
                SetBadge(null, color);
            else
                _statusBadge.style.opacity = Mathf.Clamp01(1f - fade);
        }

        /// <summary>다음 입력까지 남은 연출을 건너뛴다 (엔터·필드 누르기·건너뛰기 버튼). 한 프레임에 끝나도 표시가 보이게 바로 띄운다.</summary>
        void BeginSkip()
        {
            _skipping = true;
            _badgeUntil = Time.realtimeSinceStartup + BadgeLingerSeconds;
            SetBadge(SkipText, UiStyle.Accent);
        }

        /// <summary>표시를 바꾼다 (null이면 숨긴다). 바꿀 때마다 흐려진 것을 다시 또렷하게 한다.</summary>
        void SetBadge(string text, Color color)
        {
            if (_statusBadge == null)
                return;
            _statusBadge.style.opacity = 1f;
            if (text != null)
                _statusBadge.style.backgroundColor = color;
            if (text == _badgeText)
                return;
            _badgeText = text;
            _statusBadge.text = text ?? "";
            _statusBadge.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        Label BuildStatusBadge()
        {
            var badge = UiStyle.Label("", 15, UiStyle.Background, bold: true);
            badge.name = "status-badge";
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.top = 12;
            badge.style.right = StageWidth - 1574; // 필드 판의 오른쪽 위 모서리 (오른쪽 패널 왼쪽)
            badge.style.paddingLeft = badge.style.paddingRight = 10;
            badge.style.paddingTop = badge.style.paddingBottom = 4;
            UiStyle.Radius(badge, 5);
            badge.style.display = DisplayStyle.None;
            return badge;
        }

        /// <summary>
        /// 키보드: 진행을 보여 주는 중에는 스페이스 = 지금 멈춘 단계를 넘긴다 (누르고 있으면 빨리 넘어간다), 엔터 = 다음 입력까지 건너뛴다.
        /// 이동·결정·취소와 Tab·F는 DuelScreen.Keys.cs (취소 = 메뉴·창을 닫는다. 선택 창은 "선택 창 열기"나 취소 키로 다시 연다).
        /// </summary>
        void HandleKeys()
        {
            _spaceHeld = false;
#if ENABLE_INPUT_SYSTEM // 유니티 밖(헤드리스 컴파일)에는 Input System이 없다
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || _session == null || _docRoot == null || _docRoot.style.display == DisplayStyle.None)
                return;
            _spaceHeld = keyboard.spaceKey.isPressed;
            if (_spaceHeld && _focus != null)
                _holdUntil = 0;
            // 창·메뉴가 떠 있으면 엔터는 결정 키다 (건너뛰기로 쓰지 않는다)
            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !_autoPlay && !OverlayOpen
                && (_focus != null || _session.CanStep))
                BeginSkip();
            // 이동·결정·취소·Tab·F (DuelScreen.Keys.cs). Esc도 취소 키로 거기서 받는다.
            HandleCursorKeys();
            // 보조 키 (docs/UI.md "듀얼의 보조 키"). 글자 키는 한/영 상태와 상관없이 키의 자리로 읽는다.
            // 글 입력 칸에 글을 치는 중에는 받지 않는다.
            if (!TypingText())
            {
                if (keyboard.rKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame)
                {
                    if (keyboard.lKey.wasPressedThisFrame)
                        UiStyle.WasdLayout = true;
                    ToggleLogDrawer();
                }
                if (keyboard.cKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame)
                    PageDetailText();
                if (keyboard.digit1Key.wasPressedThisFrame && devAutoButtons && !_session.State.IsFinished)
                    SetAutoPlay(!_autoPlay);
                if (keyboard.digit2Key.wasPressedThisFrame)
                    CycleSpeed();
                if (keyboard.digit3Key.wasPressedThisFrame)
                    CycleChainPrompt();
                if (keyboard.qKey.wasPressedThisFrame)
                    PressPhaseKey('q');
                if (keyboard.eKey.wasPressedThisFrame)
                    PressPhaseKey('e');
            }
            if (_armedKey != '\0' && Time.unscaledTime > _armedUntil)
            {
                _armedKey = '\0';
                Render();
            }
#endif
        }

        /// <summary>다음 페이즈(Q)·턴 종료(E)를 한 번 눌러 둔 키 ('\0'이면 없음)와 그것이 풀리는 때.</summary>
        char _armedKey;
        float _armedUntil;
        const float ArmSeconds = 1.8f;

        /// <summary>이 버튼을 누르는 키: 다음 페이즈(배틀 페이즈, 메인 페이즈 2)는 q, 턴 종료는 e, 그 밖에는 '\0'.</summary>
        static char PhaseKeyOf(string label) =>
            label == "배틀 페이즈" || label == "메인 페이즈 2" ? 'q' : label == "턴 종료" ? 'e' : '\0';

        /// <summary>
        /// 다음 페이즈·턴 종료 키. 되돌릴 수 없는 조작이라 두 번 눌러야 실행한다: 한 번 누르면 버튼에 "한 번 더"가 뜨고,
        /// 1.8초 안에 같은 키를 누르면 실행한다. 시간이 지나거나 다른 쪽 키를 누르면 풀린다. 마우스로 버튼을 누르면 바로 실행한다.
        /// </summary>
        void PressPhaseKey(char key)
        {
            if (_autoPlay || _interaction == null || _modalOpen)
                return;
            var button = _interaction.Buttons.FirstOrDefault(b => PhaseKeyOf(b.Label) == key);
            if (button == null)
            {
                _armedKey = '\0';
                return;
            }
            if (_armedKey == key && Time.unscaledTime <= _armedUntil)
            {
                _armedKey = '\0';
                Respond(button.Response);
                return;
            }
            _armedKey = key;
            _armedUntil = Time.unscaledTime + ArmSeconds;
            Render();
        }

        // ================= 런 화면이 맡기는 듀얼 =================

        /// <summary>런 화면이 맡긴다고 표시한다. Start가 혼자 듀얼을 시작하지 않도록 Start보다 먼저(Awake에서) 부른다.</summary>
        internal void UseExternalDuels() => _external = true;

        /// <summary>런 화면이 읽은 카드 데이터로 화면을 만들고 숨겨 둔다. images: 런 화면과 같이 쓰는 카드 그림 (없으면 새로 만든다).</summary>
        internal void Attach(CardDataSet data, CardImages images = null)
        {
            _external = true;
            if (_docRoot != null)
                return;
            CreateDocument();
            _db = data.Database;
            _scripts = data.Scripts;
            _text = new DuelText(_db, data.Strings, Me);
            _logFormatter = new DuelLogFormatter(_text);
            // 듀얼 중에는 등급을 보이지 않는다 (필드의 카드는 듀얼의 카드로만 보인다).
            _faces = new CardFaces(_text, null, images ?? CardImages.FromGameFolder());
            BuildLayout();
            SetVisible(false);
        }

        internal void SetVisible(bool visible)
        {
            if (_docRoot != null)
                _docRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// 런 화면의 듀얼 하나를 시작하고 화면을 보인다. 상대는 WindBot이다.
        /// myTeam: 엔진에서 나의 팀 (선공 0, 후공 1). 어느 쪽이든 내 필드가 아래에 온다.
        /// 듀얼이 끝나 "결과 보기"를 누르면 화면을 숨기고 onLeave(끝난 듀얼 상태)를 부른다.
        /// </summary>
        internal void BeginExternalDuel(DuelSessionOptions options, int myTeam, int helperSeed, int opponentSeed,
            string myName, string opponentName, string startLine, Action<DuelState> onLeave)
        {
            _onLeave = onLeave;
            SetSeat((byte)myTeam);
            _playerNames[Me] = myName;
            _playerNames[Opp] = opponentName;
            SetVisible(true);
            RunSafely(() => StartSession(options, helperSeed, opponentSeed, startLine));
        }

        /// <summary>나의 팀을 정한다. 바뀌면 필드 배치를 다시 만든다 (내 쪽이 늘 아래, 상대가 위).</summary>
        void SetSeat(byte me)
        {
            if (me > 1)
                throw new ArgumentOutOfRangeException(nameof(me));
            bool changed = me != Me;
            Me = me;
            Opp = (byte)(1 - me);
            if (_text != null)
                _text.LocalPlayer = me;
            if (changed && _root != null && _faces != null)
            {
                _root.Clear();
                // 손패 칸은 그리기 사이에 다시 쓰는 목록이라 옛 배치의 칸이 남지 않게 비운다 (나머지 칸은 BuildLayout이 새로 만든다).
                foreach (var cells in _handCells)
                    cells.Clear();
                BuildLayout();
            }
        }

        void LeaveDuel()
        {
            var state = _session?.State;
            var onLeave = _onLeave;
            if (state == null || !state.IsFinished || onLeave == null)
                return;
            _onLeave = null;
            _autoPlay = false;
            ResetPlayback();
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

        // ================= 한 단계씩 보여 주기 =================

        /// <summary>멈춰 보여 주는 단계 하나: 가운데 줄, 행동한 쪽, 테두리를 칠 카드(칸), LP가 바뀐 쪽, 멈추는 시간.</summary>
        sealed class PlaybackFocus
        {
            public string Line;
            public byte Actor;
            public float Seconds;
            public byte LpPlayer = OcgConstants.PlayerNone;
            public int LpDelta;
            /// <summary>카드 정보에 띄울 카드 (0이면 그대로 둔다).</summary>
            public uint DetailCode;
            public CardLocation DetailLocation;
            public readonly List<(CardKey Key, Color Color)> Cards = new List<(CardKey, Color)>();

            public void Add(CardLocation loc, Color color)
            {
                if (loc.Controller < 2 && !loc.IsOverlay && loc.Location != OcgLocation.None)
                    Cards.Add((new CardKey(loc), color));
            }

            public Color? ColorAt(CardKey? key)
            {
                if (!key.HasValue)
                    return null;
                foreach (var (k, color) in Cards)
                    if (k.Equals(key.Value))
                        return color;
                return null;
            }

            public bool Touches(byte p, OcgLocation location) => Cards.Any(c => c.Key.Controller == p && c.Key.Location == location);
        }

        void ResetPlayback()
        {
            _focus = _nextFocus = null;
            _skipping = _stalled = _pacing = false;
            _lastChaining = null;
            _attacker = null;
            _solvingChain = 0;
        }

        /// <summary>멈춘 단계의 카드(발동·소환·공격)를 카드 정보에 띄운다. 마우스를 올려 보는 중이면 떼었을 때 보인다.</summary>
        void ShowFocusCard(PlaybackFocus f)
        {
            if (f.DetailCode == 0)
                return;
            var card = CardFor(f.DetailCode, f.DetailLocation);
            _pinnedCard = card;
            _pinnedVisible = true;
            if (!_previewing)
                RenderDetail(card, true);
        }

        /// <summary>그 자리의 카드 (지금 상태의 공격력 등). 그 자리에 다른 카드가 있거나 없으면 카드 DB의 값으로 만든다.</summary>
        CardInfo CardFor(uint code, CardLocation loc)
        {
            var card = _session.State.Find(loc);
            if (card != null && card.Code == code)
                return card;
            var record = _text.Record(code);
            card = new CardInfo { Code = code, Location = loc };
            if (record != null)
            {
                card.Type = record.Type;
                card.Level = record.Data.Level & 0xff;
                card.Attribute = (OcgAttribute)record.Data.Attribute;
                card.Race = record.Data.Race;
                card.Attack = card.BaseAttack = record.Data.Attack;
                card.Defense = card.BaseDefense = record.Data.Defense;
            }
            return card;
        }

        /// <summary>
        /// 공격 대상 고르기: 후보 몬스터의 예상 결과. 공격력·수비력만 비교한다 (효과는 따지지 않는다). 해당 없으면 null.
        /// </summary>
        string AttackPreview(CardInfo target)
        {
            if (target == null || !_attacker.HasValue || !(_interaction?.Request is SelectCardRequest r) || r.SelectHint != HintAttackTarget
                || _interaction.Selection?.Find(new CardKey(target.Location)) == null)
                return null;
            var attacker = _session.State.Find(_attacker.Value);
            if (attacker == null || !attacker.IsMonster)
                return null;
            if (!target.IsFaceUp)
                return "뒷면\n결과 모름";
            int atk = attacker.Attack;
            if ((target.Position & OcgPosition.Defense) != 0)
            {
                int def = target.Defense;
                return atk > def ? "파괴" : atk == def ? "변화 없음" : $"나 -{def - atk}";
            }
            int diff = atk - target.Attack;
            return diff > 0 ? $"파괴\n상대 -{diff}" : diff == 0 ? "서로 파괴" : $"내 몬스터 파괴\n나 -{-diff}";
        }

        /// <summary>멈춘 단계가 끝났으면 다음 중요한 메시지까지 진행한다. 한 프레임에 너무 오래 붙잡지 않는다.</summary>
        void AdvancePlayback()
        {
            if (_session == null || _stalled)
                return;
            if (_focus != null)
            {
                if (!_skipping && Time.realtimeSinceStartup < _holdUntil)
                    return;
                _focus = null;
                if (!_session.CanStep)
                {
                    _skipping = false;
                    Render(); // 멈춰 둔 입력을 이제 묻는다
                    return;
                }
            }
            if (!_session.CanStep)
                return;

            float budget = Time.realtimeSinceStartup + PlaybackSecondsPerFrame;
            _pacing = true;
            try
            {
                while (_session.CanStep)
                {
                    _nextFocus = null;
                    _session.Step();
                    if (_nextFocus != null)
                        ShowFocusCard(_nextFocus);
                    if (_nextFocus != null && !_skipping)
                    {
                        _focus = _nextFocus;
                        _holdUntil = Time.realtimeSinceStartup + _focus.Seconds;
                        break;
                    }
                    if (Time.realtimeSinceStartup > budget)
                        break;
                }
            }
            catch (Exception e)
            {
                _stalled = true;
                _focus = null;
                Debug.LogException(e);
                AddLog($"오류: {e.Message}", UiStyle.Opponent);
            }
            finally
            {
                _pacing = false;
                _nextFocus = null;
            }
            if (_focus == null && !_session.CanStep)
                _skipping = false;
            Render();
        }

        float SpeedFactor => _speed == PlaybackSpeed.Fast ? 0.35f : _speed == PlaybackSpeed.Slow ? 1.8f : 1f;

        static string SpeedName(PlaybackSpeed speed) =>
            speed == PlaybackSpeed.Fast ? "빠름" : speed == PlaybackSpeed.Slow ? "느림" : "보통";

        void CycleSpeed()
        {
            _speed = _speed == PlaybackSpeed.Fast ? PlaybackSpeed.Normal : _speed == PlaybackSpeed.Normal ? PlaybackSpeed.Slow : PlaybackSpeed.Fast;
            PlayerPrefs.SetInt(SpeedPrefKey, (int)_speed);
            Render();
        }

        /// <summary>필드를 누르면 다음 입력까지 남은 연출을 건너뛴다 (속도 버튼은 빼고).</summary>
        void OnFieldPointerDown(PointerDownEvent evt)
        {
            if (_autoPlay || _session == null || (evt.target as VisualElement)?.name == "playback-speed")
                return;
            if (_focus != null || _session.CanStep)
                BeginSkip();
        }

        /// <summary>이 메시지에서 멈출지와 무엇을 강조할지. 멈추지 않으면 null.</summary>
        PlaybackFocus BuildFocus(DuelMessage message, string line)
        {
            var f = new PlaybackFocus { Line = line, Actor = _logFormatter.LastActor };
            var state = _session.State;
            float seconds;
            switch (message)
            {
                case ChainingMessage m:
                    seconds = 1.0f;
                    f.Add(m.Location, FocusActor);
                    _lastChaining = m.Location;
                    f.DetailCode = m.Code;
                    f.DetailLocation = m.Location;
                    break;
                case CardListMessage m when m.Type == OcgMessageType.BecomeTarget:
                    seconds = 0.7f;
                    if (_lastChaining.HasValue)
                        f.Add(_lastChaining.Value, FocusActor);
                    foreach (var c in m.Cards)
                        f.Add(c, FocusTarget);
                    break;
                case ChainEventMessage m when m.Type == OcgMessageType.ChainSolving:
                    seconds = 0.45f;
                    if (m.ChainCount >= 1 && m.ChainCount <= state.Chain.Count)
                        f.Add(state.Chain[m.ChainCount - 1].Location, FocusActor);
                    break;
                case ChainEventMessage _:
                case SimpleMessage _ when message.Type == OcgMessageType.AttackDisabled:
                    seconds = 0.8f;
                    break;
                case AttackMessage m:
                {
                    seconds = 1.0f;
                    f.Add(m.Attacker, FocusActor);
                    if (!m.IsDirect)
                        f.Add(m.Target, FocusTarget);
                    var attacker = state.Find(m.Attacker);
                    if (attacker != null && IsVisible(attacker))
                    {
                        f.DetailCode = attacker.Code;
                        f.DetailLocation = m.Attacker;
                    }
                    break;
                }
                case BattleMessage m:
                    seconds = 1.1f;
                    f.Add(m.Attacker, FocusActor);
                    if (!m.Target.IsEmpty)
                        f.Add(m.Target, FocusTarget);
                    break;
                case LpChangeMessage m:
                    seconds = 0.8f;
                    f.LpPlayer = m.Player;
                    f.LpDelta = m.Kind == LpChangeKind.Recover ? (int)m.Amount
                        : m.Kind == LpChangeKind.Damage || m.Kind == LpChangeKind.PayCost ? -(int)m.Amount : 0;
                    break;
                case MoveMessage m:
                    seconds = 0.6f;
                    f.Add(m.To, FocusTarget);
                    break;
                case SummoningMessage m:
                    seconds = 0.6f;
                    f.Add(m.Location, FocusActor);
                    f.DetailCode = m.Code;
                    f.DetailLocation = m.Location;
                    break;
                case SetMessage m:
                    seconds = 0.5f;
                    f.Add(m.Location, FocusActor);
                    break;
                case PositionChangeMessage m:
                    seconds = 0.5f;
                    f.Add(m.Location, FocusActor);
                    break;
                case CardPairMessage m when m.Type == OcgMessageType.Equip:
                    seconds = 0.5f;
                    f.Add(m.Card, FocusActor);
                    f.Add(m.Target, FocusTarget);
                    break;
                case TossMessage _:
                case NewTurnMessage _:
                    seconds = 0.7f;
                    break;
                default:
                    return null;
            }
            f.Seconds = seconds * SpeedFactor;
            return f;
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
            SetSeat(0);
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
            ResetPlayback();
            _log.Clear();
            _recentLines.Clear();
            RenderRecentLog();
            SetLogDrawer(false, instant: true);
            _hoveredHand = -1;
            _cursorOn = false;
            _cursorRow = HandRowIndex;
            _cursorHand = 0;
            CloseMenu();
            CloseModal();
            ClearDetail();
            for (int p = 0; p < 2; p++)
                _playerName[p].text = _playerNames[p];
            _helper = new SimpleAutoPlayer(activateEffects: true, seed: helperSeed);
            _opponentAi = new WindBotPlayer(Opp, _db, seed: opponentSeed, hideOpponentInfo: !opponentSeesHiddenInfo);
            _opponentAi.ProblemReported += text => Debug.LogWarning($"[WindBot] {text}");
            _manual = new ManualPlayer { ChainPrompt = chainPrompt, PlaceSelect = placeSelect };
            _chainStatus.Clear();
            // 팀 0이 선공이다. 나는 Me 팀 (런에서 후공이면 1).
            _session = Me == 0
                ? DuelSession.Create(options, _db, _scripts, _manual, _opponentAi)
                : DuelSession.Create(options, _db, _scripts, _opponentAi, _manual);
            _session.MessageReceived += OnMessage;
            _session.EngineLog += OnEngineLog;
            foreach (var (type, text) in _session.StartupLog)
                OnEngineLog(type, text);
            _resultLogged = false;
            AddLog(startLine, UiStyle.Accent);
            _session.Start(runToFirstRequest: false); // 진행은 Update가 한 단계씩 한다
        }

        void Respond(byte[] response)
        {
            if (response == null)
                return;
            CloseMenu();
            CloseModal();
            // 공격하겠다고 고른 몬스터를 기억해 둔다 (다음 공격 대상 고르기에서 예상 결과를 보인다).
            if (_session?.PendingRequest is SelectBattleCmdRequest battle)
                for (int i = 0; i < battle.Attackers.Count; i++)
                    if (response.SequenceEqual(DuelResponses.Attack(i)))
                        _attacker = battle.Attackers[i].Location;
            RunSafely(() => _session.Answer(response));
            AdvancePlayback(); // 나머지는 Update가 한 단계씩 진행한다
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
            TrackChain(message);
            string line = _logFormatter.Format(message, _session.State);
            if (line == null)
                return;
            if (_pacing)
                _nextFocus = BuildFocus(message, line);
            // 상대 행동 줄은 색을 달리한다.
            Color color = message is NewTurnMessage || message is WinMessage ? UiStyle.Accent
                : message is NewPhaseMessage || message.Type == OcgMessageType.DamageStepStart ? UiStyle.TextDim
                : _logFormatter.LastActor == Opp ? OpponentLine : UiStyle.Text;
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
            // 멈춰 보여 주는 단계가 있으면 그 단계가 끝난 뒤에 묻는다.
            int serial = _session.PendingRequest == null || _focus != null ? -1 : _session.PendingSerial;
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
                int lp = System.Math.Max(0, player.Lp);
                _lp[p].text = lp.ToString();
                // LP 막대: 시작 LP(8000)를 꽉 찬 것으로 본다. 넘으면 꽉 찬 채로 둔다.
                _lpBar[p].style.width = Length.Percent(Mathf.Clamp01(lp / 8000f) * 100f);
                bool lpFocus = _focus != null && _focus.LpPlayer == p;
                _lp[p].style.color = lpFocus ? FocusTarget : UiStyle.Text;
                // LP가 바뀌는 단계: 바뀐 양을 LP 옆에 띄운다.
                _lpDelta[p].text = lpFocus && _focus.LpDelta != 0 ? (_focus.LpDelta > 0 ? $"+{_focus.LpDelta}" : $"−{-_focus.LpDelta}") : "";
                _lpDelta[p].style.color = lpFocus && _focus.LpDelta > 0 ? Chosen : FocusTarget;
                _playerInfo[p].text = $"패 {player.Hand.Count} · 덱 {player.DeckCount} · 묘지 {player.Grave.Count} · 제외 {player.Removed.Count}";
                for (int i = 0; i < MainZones; i++)
                {
                    SetCell(_monsters[p, i], player.MonsterZones[i]);
                    SetCell(_spells[p, i], player.SpellZones[i]);
                }
                SetCell(_fieldZone[p], player.SpellZones[5]);
                RenderPiles(p, player);
                RenderHand(p, player);
            }

            // 오른쪽 위: 턴 숫자와 누구 턴인지, 그 아래 페이즈 여섯 줄 (지난 것은 흐리게, 지금은 금색 바탕, 남은 것은 꺼진 글자)
            _turnLabel.text = state.Turn == 0 ? "듀얼 준비" : $"{state.Turn}턴";
            _turnLabel.style.fontSize = state.Turn == 0 ? 30 : 46;
            _turnSide.text = state.Turn == 0 ? "" : $"{_text.Player(state.TurnPlayer)}의 턴";
            _turnSide.style.color = state.TurnPlayer == Me ? UiStyle.Mine : UiStyle.Opponent;
            int phase = state.Turn == 0 ? -1 : System.Array.IndexOf(PhaseNames, DuelText.ShortPhase(state.Phase));
            for (int i = 0; i < _phaseLabels.Length; i++)
            {
                bool now = i == phase;
                _phaseLabels[i].style.backgroundColor = now ? UiStyle.Gold : Color.clear;
                _phaseLabels[i].style.color = now ? UiStyle.Hex(0x22180a) : i < phase ? UiStyle.TextDim : UiStyle.Stone4;
            }
            // 체인: 오른쪽 체인 목록에 번호·카드·처리 상태를 보인다.
            RenderChain(state);
            _stripButtons.Clear();
            _smallButtons.Clear();
            _firstPrimary = true;
            RenderStrip(state);
            SetPrompt(_promptLabel.text, _promptDetail.text);
        }

        void RenderStrip(DuelState state)
        {
            _promptLabel.style.color = UiStyle.Accent;
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
                // 진행 중: 지금 보여 주는 단계를 가운데에 한 줄로 띄운다.
                _promptLabel.text = _focus?.Line ?? "진행 중...";
                _promptLabel.style.color = _focus == null ? UiStyle.Accent : _focus.Actor == Opp ? OpponentLine : UiStyle.Text;
                _promptDetail.text = _stalled ? "" : "필드를 누르면 다음 입력까지 건너뛴다";
                if (!_stalled && (_focus != null || _session.CanStep))
                    AddStripButton("건너뛰기", BeginSkip, false).name = "skip-playback";
                AddSpeedButton();
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
            if (_attacker.HasValue && _interaction.Request is SelectCardRequest attackTarget && attackTarget.SelectHint == HintAttackTarget
                && _session.State.Find(_attacker.Value) is CardInfo attacker)
                detail = $"「{_text.CardName(attacker.Code)}」 공격력 {attacker.Attack} · 카드 위 예상 결과는 공격력·수비력만 비교한다";
            _promptDetail.text = detail ?? "";

            foreach (var b in _interaction.Buttons)
            {
                var response = b.Response;
                var button = AddStripButton(b.Label, () => Respond(response), true);
                // 다음 페이즈는 Q, 턴 종료는 E (두 번 눌러야 실행한다. 한 번 누르면 "한 번 더"가 뜬다)
                char key = PhaseKeyOf(b.Label);
                if (key == '\0')
                    continue;
                if (_armedKey == key)
                    button.text = $"{b.Label} · 한 번 더";
                var cap = UiStyle.KeyCap(key == 'q' ? "Q" : "E");
                cap.pickingMode = PickingMode.Ignore;
                cap.style.position = Position.Absolute;
                cap.style.right = 10;
                cap.style.top = 12;
                button.Add(cap);
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
            AddSpeedButton();
        }

        void AddSpeedButton()
        {
            AddStripButton($"체인 확인: {ChainPromptName(chainPrompt)}", CycleChainPrompt, false).name = "chain-prompt";
            AddStripButton($"속도: {SpeedName(_speed)}", CycleSpeed, false).name = "playback-speed";
        }

        static string ChainPromptName(ChainPromptMode mode) =>
            mode == ChainPromptMode.Always ? "항상" : mode == ChainPromptMode.Never ? "끔" : "자동";

        /// <summary>체인 확인 방식을 바꾼다 (자동 → 항상 → 끔). 듀얼 중에도 바로 적용하고 저장한다.</summary>
        void CycleChainPrompt()
        {
            chainPrompt = chainPrompt == ChainPromptMode.Smart ? ChainPromptMode.Always
                : chainPrompt == ChainPromptMode.Always ? ChainPromptMode.Never : ChainPromptMode.Smart;
            if (_manual != null)
                _manual.ChainPrompt = chainPrompt;
            PlayerPrefs.SetInt(ChainPromptPrefKey, (int)chainPrompt);
            string how = chainPrompt == ChainPromptMode.Always ? "발동할 수 있는 카드가 있으면 언제나 묻는다 (상대 턴 엔드 페이즈 등에 함정을 쓰고 싶을 때)"
                : chainPrompt == ChainPromptMode.Never ? "묻지 않는다 (꼭 골라야 하는 유발 효과만 묻는다)"
                : "상대 행동에 대응할 수 있을 때만 묻는다";
            AddLog($"체인 확인: {ChainPromptName(chainPrompt)} — {how}", UiStyle.TextDim);
            Render();
        }

        // ================= 체인 =================

        /// <summary>체인 처리 과정을 따라간다: 처리 중인 번호, 처리됨·무효 표시.</summary>
        void TrackChain(DuelMessage message)
        {
            switch (message)
            {
                case ChainingMessage m:
                    _chainStatus.Remove(m.ChainCount);
                    break;
                case ChainEventMessage m when m.Type == OcgMessageType.ChainSolving:
                    _solvingChain = m.ChainCount;
                    break;
                case ChainEventMessage m when m.Type == OcgMessageType.ChainSolved:
                    if (!_chainStatus.ContainsKey(m.ChainCount))
                        _chainStatus[m.ChainCount] = "처리됨";
                    break;
                case ChainEventMessage m when m.Type == OcgMessageType.ChainNegated:
                    _chainStatus[m.ChainCount] = "발동 무효";
                    break;
                case ChainEventMessage m when m.Type == OcgMessageType.ChainDisabled:
                    _chainStatus[m.ChainCount] = "효과 무효";
                    break;
                case SimpleMessage m when m.Type == OcgMessageType.ChainEnd:
                    _solvingChain = 0;
                    _chainStatus.Clear();
                    break;
            }
        }

        /// <summary>체인 번호의 처리 상태: 처리 중 / 처리됨 / 발동 무효 / 효과 무효 / 대기.</summary>
        string ChainStatusOf(int number)
        {
            if (_chainStatus.TryGetValue(number, out var status))
                return status;
            if (number == _solvingChain)
                return "처리 중";
            return _solvingChain > 0 && number > _solvingChain ? "처리됨" : "대기";
        }

        /// <summary>필드의 이 카드가 아직 처리되지 않은 체인에 있으면 그 번호 (가장 위의 것), 없으면 0.</summary>
        int ChainNumberOf(CardInfo card)
        {
            if (card == null || _session == null)
                return 0;
            var chain = _session.State.Chain;
            var key = new CardKey(card.Location);
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                string status = ChainStatusOf(i + 1);
                if ((status == "대기" || status == "처리 중") && new CardKey(chain[i].Location).Equals(key))
                    return i + 1;
            }
            return 0;
        }

        /// <summary>
        /// 체인 목록 (로그 위). 위에서부터 처리하므로 마지막 체인을 맨 위에 둔다. 줄마다 번호(쪽 색), 카드, 효과, 처리 상태.
        /// 줄에 마우스를 올리면 카드 정보에 그 카드가 보이고, 누르면 고정한다.
        /// </summary>
        void RenderChain(DuelState state)
        {
            _chainPanel.Clear();
            _chainPanel.style.display = state.Chain.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (state.Chain.Count == 0)
                return;
            var header = UiStyle.Box(null, FlexDirection.Row);
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;
            header.Add(UiStyle.Label($"체인 {state.Chain.Count}", 19, UiStyle.Accent, bold: true));
            header.Add(UiStyle.Label(_solvingChain > 0 ? $"체인 {_solvingChain} 처리하는 중" : "위에서부터 처리", 15, UiStyle.TextDim));
            _chainPanel.Add(header);

            for (int i = state.Chain.Count - 1; i >= 0; i--)
            {
                var link = state.Chain[i];
                int number = i + 1;
                byte owner = link.Location.Controller;
                Color side = owner == Opp ? FocusActor : UiStyle.Mine;
                string status = ChainStatusOf(number);
                bool done = status != "대기" && status != "처리 중";

                var row = UiStyle.Box($"chain-{number}", FlexDirection.Row);
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = status == "처리 중" ? Color.Lerp(UiStyle.PanelLight, UiStyle.Accent, 0.18f) : UiStyle.PanelLight;
                UiStyle.Radius(row, 5);
                UiStyle.Padding(row, 6);
                row.style.marginBottom = 4;
                row.style.borderLeftWidth = 4;
                row.style.borderLeftColor = side;
                row.style.opacity = done ? 0.5f : 1f;

                var circle = UiStyle.Label(number.ToString(), 18, UiStyle.Background, bold: true);
                circle.style.width = circle.style.height = 30;
                circle.style.flexShrink = 0;
                circle.style.unityTextAlign = TextAnchor.MiddleCenter;
                circle.style.backgroundColor = side;
                UiStyle.Radius(circle, 15);
                circle.style.marginRight = 8;
                row.Add(circle);

                var texts = UiStyle.Box();
                texts.style.flexGrow = 1;
                texts.style.flexShrink = 1;
                var name = UiStyle.Label($"「{_text.CardName(link.Code)}」", 15, UiStyle.Text, bold: true);
                name.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(name);
                string desc = _text.Description(link.Description);
                string where = $"{_text.Player(owner)} · {DuelText.Location(link.Location)}";
                var sub = UiStyle.Label(string.IsNullOrEmpty(desc) || desc.StartsWith(_text.CardName(link.Code)) ? where : $"{where} · {desc}", 13, UiStyle.TextDim);
                sub.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(sub);
                row.Add(texts);

                var statusLabel = UiStyle.Label(status, 14,
                    status == "처리 중" ? UiStyle.Accent : status.EndsWith("무효") ? FocusTarget : UiStyle.TextDim, bold: status == "처리 중");
                statusLabel.style.flexShrink = 0;
                statusLabel.style.marginLeft = 6;
                row.Add(statusLabel);

                var card = CardFor(link.Code, link.Location);
                row.RegisterCallback<PointerEnterEvent>(_ => PreviewCard(card, true));
                row.RegisterCallback<PointerLeaveEvent>(_ => EndPreview());
                row.RegisterCallback<ClickEvent>(_ => ShowCard(card, true));
                _chainPanel.Add(row);
            }
        }

        bool _firstPrimary;

        /// <summary>
        /// 오른쪽 패널의 버튼. primary(지금 물음에 답하는 것)는 한 줄에 하나씩 큰 돌 버튼으로 놓고 맨 위 하나는 금색으로 한다.
        /// 나머지(자동·속도·체인 확인·건너뛰기)는 그 아래에 작은 버튼으로 나란히 놓는다.
        /// </summary>
        Button AddStripButton(string label, Action onClick, bool primary)
        {
            if (primary)
            {
                var big = UiStyle.StoneButton(label, onClick, gold: _firstPrimary);
                _firstPrimary = false;
                big.style.height = 56;
                big.style.fontSize = 24;
                big.style.marginBottom = 6;
                big.style.paddingLeft = big.style.paddingRight = 10;
                _stripButtons.Add(big);
                return big;
            }
            var small = UiStyle.StoneButton(label, onClick, small: true);
            small.style.height = 44;
            small.style.fontSize = 16;
            small.style.flexGrow = 1;
            small.style.marginBottom = 4;
            small.style.marginRight = 4;
            small.style.paddingLeft = small.style.paddingRight = 8;
            small.style.color = UiStyle.TextDim;
            _smallButtons.Add(small);
            return small;
        }

        void SetCell(CardCell cell, CardInfo card, bool? visibleOverride = null)
        {
            bool visible = visibleOverride ?? IsVisible(card);
            cell.Set(card, visible);
            cell.SetNote(AttackPreview(card));
            int chainNumber = ChainNumberOf(card);
            cell.SetChainNumber(chainNumber, chainNumber > 0 && _session.State.Chain[chainNumber - 1].Location.Controller == Opp ? FocusActor : UiStyle.Mine);
            if (_focus != null)
            {
                // 멈춰 보여 주는 단계: 발동한 카드·공격하는 몬스터는 주황, 대상·공격받는 쪽은 빨강 테두리 (빈칸이면 칸에).
                cell.SetHighlight(_focus.ColorAt(card != null ? new CardKey(card.Location) : cell.Zone));
                return;
            }
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

        bool Touches(byte p, OcgLocation location) =>
            _focus != null ? _focus.Touches(p, location) : _interaction != null && _interaction.Touches(p, location);

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

            // 카드 메뉴: 돌판, 폭 270, 항목 높이 54. 엔진이 지금 허용한 행동만 넣고 맨 아래에 "취소".
            var menu = UiStyle.PanelBox("card-menu");
            menu.style.position = Position.Absolute;
            menu.style.width = 270;
            UiStyle.Padding(menu, 8);
            foreach (var a in actions)
            {
                var response = a.Response;
                menu.Add(MenuItem(a.Label, () => Respond(response)));
            }
            var cancel = MenuItem("취소", CloseMenu);
            cancel.name = "menu-cancel";
            cancel.style.color = UiStyle.TextDim;
            menu.Add(cancel);
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

        static Button MenuItem(string label, Action onClick)
        {
            var item = UiStyle.StoneButton(label, onClick, small: true);
            item.style.height = 54;
            item.style.fontSize = 25;
            item.style.marginBottom = 4;
            return item;
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
                close.name = "modal-close";
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
            view.name = "field-view";
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
            int index = 0;
            foreach (var candidate in sel.Candidates)
            {
                var info = MakeInfo(candidate.Card);
                // 순서를 정할 카드는 엔진이 정하는 사람에게 보여 준 카드다 (상대 덱 위의 카드라도).
                bool visible = sel.Ordered ? candidate.Card.Code != 0 : IsVisibleRequestCard(candidate.Card, info);
                var cell = WindowCell($"candidate-{index++}");
                cell.ClickOverride = () => OnCandidateClicked(candidate, info, visible);
                cell.Set(info, visible);
                cell.SetHighlight(sel.IsChosen(candidate) ? Chosen : Selectable);
                string caption = LocationCaption(candidate.Card.Location);
                cell.SetCaption(string.IsNullOrEmpty(candidate.Extra) ? caption : $"{caption} {candidate.Extra}");
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
            view.name = "field-view";
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
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool visible = p == Me || card.IsFaceUp || card.IsPublic || location == OcgLocation.Grave;
                var cell = WindowCell($"pile-card-{i}", refresh: () => OpenPileWindow(p, location));
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
        bool IsVisibleRequestCard(CardRef card, CardInfo info)
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

        // ================= 로그 =================

        void AddLog(string line, Color color)
        {
            var label = UiStyle.Label(line, 18, color);
            label.style.marginBottom = 3;
            _log.Add(label);
            while (_log.contentContainer.childCount > MaxLogLines)
                _log.contentContainer.RemoveAt(0);
            if (_drawerOpen)
                _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0, float.MaxValue)).ExecuteLater(10);
            // 오른쪽 패널의 "방금 일어난 일"
            _recentLines.Add((line, color));
            while (_recentLines.Count > RecentLogLines)
                _recentLines.RemoveAt(0);
            RenderRecentLog();
        }

        // ================= 화면 구성 =================

        void CreateDocument()
        {
            var docRoot = _docRoot = UiStyle.CreateDocument(transform, "DuelUI");

            // 화면 전체. 그 안에 1920×1080 판(duel-stage)을 화면 크기에 맞춰 놓는다 (BuildLayout).
            _root = UiStyle.Box("duel", FlexDirection.Row);
            _root.style.flexGrow = 1;
            _root.style.backgroundColor = UiStyle.Background;
            docRoot.Add(_root);

            // 창(대화창·카드 목록)과 카드 메뉴는 화면 전체를 덮는 층에 띄운다.
            _modalLayer = OverlayLayer(new Color(0, 0, 0, 0.45f));
            _modalLayer.style.justifyContent = Justify.Center;
            _modalLayer.style.alignItems = Align.Center;
            docRoot.Add(_modalLayer);
            _menuLayer = OverlayLayer(Color.clear);
            docRoot.Add(_menuLayer);
            BuildKeyLayer(docRoot);
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

        static int PopCount(ulong v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1) n++;
            return n;
        }
    }
}
