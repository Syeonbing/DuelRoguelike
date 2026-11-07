// 최소 듀얼 화면 (UI Toolkit). 설계 문서의 화면 구성을 따른다:
// 왼쪽 플레이어 정보(위 상대, 아래 나, 카드 효과문은 내 초상화 자리), 가운데 필드(점대칭, 엑스트라 몬스터 존 없음,
// 패는 필드 위아래, 묘지 옆 필드 가운데 쪽에 원형 제외 칸), 오른쪽 로그와 선택 패널.
// 센터 카드, 초상화 그림, 체인 박스 연출, 카드 크게 보기는 아직 없다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed class DuelScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("듀얼 난수·덱 셔플 시드. 다시 시작할 때마다 1씩 늘어난다.")]
        int duelSeed = 1;

        [SerializeField, Tooltip("체인할지 묻는 방식")]
        ChainPromptMode chainPrompt = ChainPromptMode.Smart;

        const byte Me = 0;
        const byte Opp = 1;
        const int MainZones = 5;
        const int MaxLogLines = 800;

        CardDatabase _db;
        CardScriptSource _scripts;
        DuelSession _session;
        DuelText _text;
        DuelLogFormatter _logFormatter;
        SimpleAutoPlayer _helper;

        VisualElement _root;
        readonly Label[] _lp = new Label[2];
        readonly Label[] _playerInfo = new Label[2];
        readonly CardCell[,] _monsters = new CardCell[2, MainZones];
        readonly CardCell[,] _spells = new CardCell[2, MainZones];
        readonly CardCell[] _fieldZone = new CardCell[2];
        readonly PileCell[] _deck = new PileCell[2];
        readonly PileCell[] _extra = new PileCell[2];
        readonly PileCell[] _grave = new PileCell[2];
        readonly PileCell[] _removed = new PileCell[2];
        readonly VisualElement[] _handRows = new VisualElement[2];
        Label _turnLabel;
        VisualElement _chainBox;
        ScrollView _log;
        DuelPrompt _prompt;
        Label _detailName, _detailType, _detailStats, _detailText;

        void Start()
        {
            CreateDocument();
            Exception loadError = null;
            try
            {
                LoadData();
            }
            catch (Exception e)
            {
                loadError = e;
                _text = new DuelText(new CardDatabase(), Me);
                _logFormatter = new DuelLogFormatter(_text);
            }
            BuildLayout();
            if (loadError != null)
            {
                Debug.LogException(loadError);
                _prompt.ShowText("카드 데이터를 불러오지 못했습니다.",
                    loadError is DllNotFoundException
                        ? $"네이티브 DLL이 없습니다. tools/build-ocgcore.ps1과 tools/fetch-sqlite.ps1을 실행하세요. ({loadError.Message})"
                        : loadError.Message);
                return;
            }
            RunSafely(StartDuel);
        }

        void OnDestroy() => _session?.Dispose();

        // ================= 듀얼 =================

        void LoadData()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _db = new CardDatabase();
            _db.Load(Path.Combine(projectRoot, "External", "BabelCDB", "cards.cdb"));
            _scripts = new CardScriptSource(Path.Combine(projectRoot, "External", "CardScripts"));
            _text = new DuelText(_db, Me);
            _logFormatter = new DuelLogFormatter(_text);
        }

        void StartDuel()
        {
            _session?.Dispose();
            _session = null;
            _log.Clear();
            var deck = TestDecks.Yugi;
            var options = new DuelSessionOptions
            {
                Seed = new ulong[] { (ulong)duelSeed, 2, 3, 4 },
                Decks = new IReadOnlyList<uint>[] { deck, deck },
                DeckShuffleSeed = duelSeed * 100,
            };
            _helper = new SimpleAutoPlayer(activateEffects: true, seed: duelSeed);
            _session = DuelSession.Create(options, _db, _scripts,
                new ManualPlayer { ChainPrompt = chainPrompt },
                new SimpleAutoPlayer(activateEffects: false)); // 임시 상대 (AI는 개발 단계 6)
            _session.MessageReceived += OnMessage;
            _session.EngineLog += OnEngineLog;
            foreach (var (type, text) in _session.StartupLog)
                OnEngineLog(type, text);
            AddLog($"듀얼 시작 (시드 {duelSeed}) — 테스트 덱 40장 대 40장, 나의 선공", UiStyle.Accent);
            _session.Start();
        }

        void Respond(byte[] response) => RunSafely(() => _session.Respond(response));

        void RunSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
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
            for (byte p = 0; p < 2; p++)
            {
                var player = state.Players[p];
                _lp[p].text = $"LP {player.Lp}";
                _playerInfo[p].text = $"패 {player.Hand.Count} · 덱 {player.DeckCount} · 묘지 {player.Grave.Count}";
                for (int i = 0; i < MainZones; i++)
                {
                    _monsters[p, i].Set(player.MonsterZones[i], IsVisible(player.MonsterZones[i]));
                    _spells[p, i].Set(player.SpellZones[i], IsVisible(player.SpellZones[i]));
                }
                _fieldZone[p].Set(player.SpellZones[5], IsVisible(player.SpellZones[5]));
                _deck[p].Set($"덱\n{player.DeckCount}");
                _extra[p].Set($"엑스트라\n{player.ExtraCount}");
                _grave[p].Set($"묘지\n{player.Grave.Count}");
                _removed[p].Set($"제외\n{player.Removed.Count}");
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

            if (state.IsFinished)
            {
                string result = state.Winner == OcgConstants.PlayerNone ? "무승부" : state.Winner == Me ? "승리!" : "패배";
                _prompt.ShowText($"듀얼 종료: {result}", $"{state.Turn}턴, 나 LP {state.Players[Me].Lp} / 상대 LP {state.Players[Opp].Lp}");
                _prompt.AddButton("다시 시작 (새 시드)", () =>
                {
                    duelSeed++;
                    RunSafely(StartDuel);
                });
            }
            else if (_session.PendingRequest != null)
            {
                _prompt.Show(_session.PendingRequest, state, _session.IsRetry);
            }
            else
            {
                _prompt.ShowText("진행 중...");
            }
        }

        void RenderHand(byte p, PlayerState player)
        {
            var row = _handRows[p];
            row.Clear();
            foreach (var card in player.Hand)
            {
                var cell = new CardCell(this, 96, 92);
                cell.Set(card, p == Me || card.IsPublic);
                row.Add(cell.Root);
            }
        }

        bool IsVisible(CardInfo card) =>
            card != null && (card.Location.Controller == Me || card.IsFaceUp || card.IsPublic);

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

        void ShowPile(string title, IEnumerable<CardInfo> cards, Func<CardInfo, bool> visible)
        {
            var list = cards.ToList();
            _detailName.text = $"{title} ({list.Count}장)";
            _detailType.text = "";
            _detailStats.text = "";
            _detailText.text = list.Count == 0 ? "비어 있음" : string.Join("\n", list.Select(c => visible(c) ? _text.CardName(c.Code) : "뒷면 카드"));
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
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "DuelPanel";
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            // 유니티 기본 런타임 테마 (Assets/UI/Resources/DuelTheme.tss). 없으면 버튼·스크롤바 기본 모양이 빠진다.
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("DuelTheme");
            if (panel.themeStyleSheet == null)
                Debug.LogWarning("Assets/UI/Resources/DuelTheme.tss를 찾지 못했습니다.");

            var go = new GameObject("DuelUI");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            go.SetActive(true);

            _root = document.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Row;
            _root.style.backgroundColor = UiStyle.Background;
            UiStyle.Padding(_root, 12);
            // 한글 글꼴: Windows 기본 글꼴(맑은 고딕)을 실행 중에 불러온다. 프로젝트에는 글꼴 파일을 넣지 않는다.
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕" }, 32);
            _root.style.unityFontDefinition = FontDefinition.FromFont(font);
            _root.style.color = UiStyle.Text;
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

            // ---- 오른쪽: 로그 / 선택 ----
            var right = UiStyle.Box("right");
            right.style.width = 500;
            right.style.marginLeft = 12;
            var logPanel = UiStyle.PanelBox("log");
            logPanel.style.flexGrow = 1;
            logPanel.Add(UiStyle.Label("로그", 17, UiStyle.TextDim, bold: true));
            _log = new ScrollView(ScrollViewMode.Vertical);
            _log.style.flexGrow = 1;
            _log.style.marginTop = 6;
            logPanel.Add(_log);
            right.Add(logPanel);
            var promptPanel = UiStyle.PanelBox("prompt");
            promptPanel.style.height = Length.Percent(44);
            promptPanel.style.marginTop = 12;
            _prompt = new DuelPrompt(promptPanel, _text, Respond, request => _helper.Respond(request, _session.State));
            right.Add(promptPanel);
            _root.Add(right);
        }

        VisualElement PlayerPanel(byte p)
        {
            var panel = UiStyle.PanelBox();
            var nameLabel = UiStyle.Label(p == Me ? "나" : "상대", 18, p == Me ? UiStyle.Mine : UiStyle.Opponent, bold: true);
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
            _fieldZone[p] = new CardCell(this, 104, 104);
            for (int i = 0; i < MainZones; i++)
                _monsters[p, i] = new CardCell(this, 104, 104);
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
                _spells[p, i] = new CardCell(this, 104, 92);
            _deck[p] = new PileCell(104, 92, 6);
            _extra[p] = new PileCell(104, 92, 6);
            _extra[p].Root.RegisterCallback<ClickEvent>(_ =>
                ShowPile($"{_text.Player(p)}의 엑스트라 덱", _session.State.Players[p].Extra, c => p == Me || c.IsFaceUp));
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
            _grave[p] = new PileCell(92, 62, 0);
            _grave[p].Root.style.borderTopLeftRadius = _grave[p].Root.style.borderTopRightRadius = 40; // 묘비
            _removed[p] = new PileCell(44, 44, 22);                                                      // 원형
            _grave[p].Root.RegisterCallback<ClickEvent>(_ =>
                ShowPile($"{_text.Player(p)}의 묘지", _session.State.Players[p].Grave, c => true));
            _removed[p].Root.RegisterCallback<ClickEvent>(_ =>
                ShowPile($"{_text.Player(p)}의 제외 존", _session.State.Players[p].Removed, c => p == Me || c.IsFaceUp));
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

        // 양쪽 필드 사이: 턴·페이즈와 체인 목록 (체인 박스 연출은 나중에, 지금은 센터 카드 자리에 목록만).
        VisualElement CenterStrip()
        {
            var strip = UiStyle.Box(null, FlexDirection.Row);
            strip.style.width = Length.Percent(100);
            strip.style.minHeight = 64;
            strip.style.alignItems = Align.Center;
            strip.style.justifyContent = Justify.SpaceBetween;
            strip.style.backgroundColor = UiStyle.PanelLight;
            UiStyle.Radius(strip, 6);
            UiStyle.Padding(strip, 8);
            _turnLabel = UiStyle.Label("듀얼 준비", 20, bold: true);
            _chainBox = UiStyle.Box();
            _chainBox.style.alignItems = Align.FlexEnd;
            strip.Add(_turnLabel);
            strip.Add(_chainBox);
            return strip;
        }

        // ================= 칸 =================

        sealed class CardCell
        {
            public readonly VisualElement Root;
            readonly Label _name;
            readonly Label _stats;
            readonly DuelScreen _screen;
            CardInfo _card;
            bool _visible;
            string _emptyText = "";

            public CardCell(DuelScreen screen, float width, float height)
            {
                _screen = screen;
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
                Root.Add(_name);
                Root.Add(_stats);
                Root.RegisterCallback<ClickEvent>(_ => _screen.ShowCard(_card, _visible));
                Set(null, false);
            }

            public void SetEmptyText(string text)
            {
                _emptyText = text;
                if (_card == null) _name.text = text;
            }

            public void Set(CardInfo card, bool visible)
            {
                _card = card;
                _visible = visible;
                if (card == null)
                {
                    _name.text = _emptyText;
                    _name.style.color = UiStyle.TextDim;
                    _stats.text = "";
                    Root.style.backgroundColor = UiStyle.ZoneEmpty;
                    UiStyle.BorderColor(Root, UiStyle.Border, 1);
                    return;
                }
                bool onField = card.Location.IsOnField;
                bool faceDown = onField && !card.IsFaceUp;
                _name.style.color = UiStyle.Text;
                _name.text = visible ? _screen._text.CardName(card.Code) : "뒷면";
                if (!visible)
                    _stats.text = onField && (card.Position & OcgPosition.Defense) != 0 ? "수비" : "";
                else if (card.IsMonster)
                    _stats.text = $"{card.Attack}/{card.Defense}" + (onField ? ((card.Position & OcgPosition.Defense) != 0 ? " 수비" : " 공격") : "") + (faceDown ? " 세트" : "");
                else
                    _stats.text = ((card.Type & OcgCardType.Spell) != 0 ? "마법" : "함정") + (faceDown ? " 세트" : "");
                Root.style.backgroundColor = !visible || faceDown ? UiStyle.FaceDown : CardColor(card.Type);
                var owner = card.Location.Controller == Me ? UiStyle.Mine : UiStyle.Opponent;
                UiStyle.BorderColor(Root, onField ? owner : UiStyle.Border, onField ? 2 : 1);
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

            public PileCell(float width, float height, float radius)
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
            }

            public void Set(string text) => _label.text = text;
        }
    }

    /// <summary>임시 테스트 덱. 정식 스타터 덱 4개는 개발 단계 5에서 정한다.</summary>
    public static class TestDecks
    {
        /// <summary>스타터 덱 —유희 편— 카드 일부로 짠 40장 (카드 DB 영문명 기준).</summary>
        public static readonly IReadOnlyList<uint> Yugi = new (uint Code, int Count)[]
        {
            (46986414, 2), // Dark Magician
            (70781052, 2), // Summoned Skull
            (6368038, 2),  // Gaia The Fierce Knight
            (28279543, 3), // Curse of Dragon
            (91152256, 3), // Celtic Guardian
            (15025844, 3), // Mystical Elf
            (41392891, 3), // Feral Imp
            (13039848, 3), // Giant Soldier of Stone
            (32452818, 3), // Beaver Warrior
            (90357090, 3), // Silver Fang
            (53129443, 1), // Dark Hole
            (83764718, 1), // Monster Reborn
            (72302403, 2), // Swords of Revealing Light
            (84257639, 3), // Dian Keto the Cure Master
            (66788016, 2), // Fissure
            (4206964, 2),  // Trap Hole
            (50045299, 2), // Dragon Capture Jar
        }.SelectMany(e => Enumerable.Repeat(e.Code, e.Count)).ToList();
    }
}
