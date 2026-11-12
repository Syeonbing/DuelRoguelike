// 런 화면: 층 사이 화면(허브)과 보충 고르기 창. docs/UI.md "탑 (허브)". RunScreen.cs의 머리말 참고.
// 화면 전체가 탑 장면(TowerScene)이고 그 위에 위 막대(run-status), 높이 자, 층 팻말(floor-plaque-N), 직전 듀얼 판, 명령 메뉴(hub-menu)를 얹는다.
// 1920×1080 좌표로 짠 판(hub-stage)을 화면 크기에 맞춰 줄이거나 늘려 가운데에 놓는다.
// 층 진행: 런 규칙(RunState)에서는 이기거나 층을 떠나는 순간 이미 다음 층에 들어가 있다. 화면에서는 내 말이 아직 아래층(_standFloor)에 서 있고,
// 메뉴의 "N층으로 올라간다"를 골라야 올라가는 연출이 나온 뒤 그 층의 일(듀얼 창, 칩교환소, 휴식, 이벤트)이 시작된다.
// 연출은 아무 곳이나 누르거나 결정·취소 키를 누르면 건너뛴다 (climb-skip).
// 듀얼 창(duel-window): 모든 전투 직전에 뜬다. "듀얼 시작"(next-duel)과 "덱 조정"(duel-deck). 상대 덱 이름은 가린다.
// 덱 창은 RunScreen.DeckWindow.cs, 키보드는 RunScreen.Keys.cs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        static readonly Pile[] HubPiles = { Pile.Main, Pile.Extra, Pile.Side, Pile.Sealed, Pile.Shop };

        const float StageWidth = TowerScene.Width, StageHeight = TowerScene.Height, TopBarHeight = 72;
        const float ClimbSeconds = 1.9f, EncounterSeconds = 0.45f;
        static readonly Color GoldInk = UiStyle.Hex(0x22180a);
        static readonly Color UnknownFloorColor = UiStyle.Hex(0x8d8170);
        static readonly Color PlaqueInk = UiStyle.Hex(0x1b140b);

        uint _refillSelected;
        ScrollView _deckScroll;
        Vector2 _deckScrollOffset;
        /// <summary>직전에 한 일의 결과 (move-notice). 듀얼을 시작하거나 런을 이어 하면 지운다.</summary>
        string _moveNotice;
        bool _moveNoticeIsError;

        // ---- 허브 ----
        /// <summary>지금 화면이 층 사이 화면인지 (키보드를 받을지).</summary>
        bool _hubShown;
        /// <summary>내 말이 서 있는 층 (0 = 탑 앞). _run.Floor보다 작으면 그 층으로 아직 올라가지 않았다.</summary>
        int _standFloor;
        bool _climbing;
        Coroutine _climbRoutine;
        /// <summary>탑 장면에 그릴 내 말의 높이와 상대가 나타난 정도. 올라가는 연출이 바꾼다.</summary>
        float _scenePosition, _sceneOpponent;
        /// <summary>듀얼 창을 저절로 띄운 층 (같은 층에서 지고 돌아오면 다시 띄우지 않고 메뉴에서 연다).</summary>
        int _duelShownFloor;
        /// <summary>높이 자에 지금 그려져 있는 값 (화면을 다시 그려도 여기서부터 미끄러진다).</summary>
        float _gaugeFloor, _gaugeMax = 10;
        int _hubMenuIndex;

        VisualElement _stage, _banner, _duelWindow;
        bool _stageFitHooked;
        TowerScene _scene;
        Label _bannerText;
        MenuList _hubMenu;
        readonly List<VisualElement> _plaques = new List<VisualElement>();
        Button _duelStart, _duelDeck;
        int _duelChoice;

        bool Arrived => _run != null && _standFloor >= _run.Floor;

        /// <summary>지금 층에서 듀얼을 하는지 (전투·보스 층, 이벤트 전투를 기다리는 이벤트 층).</summary>
        bool BattleNow => _run != null && (Tower.IsBattle(_run.FloorKind) || (_run.FloorKind == FloorKind.Event && _run.EventBattlePending));

        /// <summary>런을 시작하거나 이어 할 때: 말이 설 층을 정하고 허브의 상태를 처음으로 돌린다.</summary>
        void ResetHubView()
        {
            StopClimb();
            // 이 층에서 이미 듀얼을 했거나(졌다) 보상·보충을 고르는 중이면 그 층에 서 있다. 아니면 아래층에서 올라가야 한다.
            bool dueled = _run.Events.Any(e => e.Kind == RunEventKind.DuelStarted && e.Floor == _run.Floor);
            _standFloor = _run.Phase != RunPhase.Ready || dueled ? _run.Floor : _run.Floor - 1;
            _duelShownFloor = _standFloor == _run.Floor ? _run.Floor : 0;
            _deckOpen = _deckFromDuel = false;
            _deckTab = Pile.Main;
            _deckCursor = -1;
            _deckActions = false;
            _hubMenuIndex = 0;
            _gaugeFloor = Mathf.Max(0, _standFloor);
            _gaugeMax = GaugeMax(_standFloor);
        }

        void ShowHub()
        {
            if (_run.Phase == RunPhase.Ended)
            {
                StopClimb();
                ShowEnd();
                return;
            }
            if (_deckScroll != null)
                _deckScrollOffset = _deckScroll.scrollOffset;
            BeginScreen();
            _hubShown = true;
            FixSelection();
            // 보상·보충을 고르는 중이면 그 듀얼을 한 층에 서 있다
            _standFloor = _run.Phase != RunPhase.Ready ? _run.Floor : Mathf.Clamp(_standFloor, 0, _run.Floor);
            if (!_climbing)
            {
                _scenePosition = _standFloor;
                _sceneOpponent = Arrived && BattleNow && _run.Phase == RunPhase.Ready ? 1 : 0;
            }

            _stage = new VisualElement { name = "hub-stage" };
            UiStyle.Place(_stage, 0, 0, StageWidth, StageHeight);
            _stage.style.overflow = Overflow.Hidden;
            _stage.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
            _root.Add(_stage);
            if (!_stageFitHooked)
            {
                _stageFitHooked = true;
                _root.RegisterCallback<GeometryChangedEvent>(_ => FitStage());
            }
            FitStage();

            _scene = new TowerScene { Marker = _scenePosition, Opponent = _sceneOpponent };
            _stage.Add(_scene);
            AddPlaques();
            // 덱 창이 열려 있으면 그 뒤에 비쳐 보이는 것들(높이 자, 직전 듀얼 판, 명령 메뉴)은 만들지 않는다
            bool deck = _deckOpen && !_climbing;
            if (!deck)
                _stage.Add(HeightGauge());
            _stage.Add(HubTopBar());
            if (!_climbing && !deck)
            {
                _stage.Add(LastDuelPanel());
                _stage.Add(HubMenuPanel());
                _stage.Add(HubNotice());
            }
            AddBanner();
            if (_climbing)
                _stage.Add(ClimbSkip());
            if (_deckOpen && !_climbing)
                _stage.Add(DeckWindow());
            ApplyKeyCaps();

            if (_eventResult != null)
                OpenEventResult(); // 확인을 누르면 ShowHub가 다시 불려 아래 창들이 이어서 열린다
            else if (_run.Phase == RunPhase.ChoosingReward)
                OpenReward();
            else if (_run.Phase == RunPhase.ChoosingUnseal)
                OpenUnseal();
            else if (_run.Phase == RunPhase.NeedsRefill)
                OpenRefill();
            else if (_climbing || _deckOpen || !Arrived)
            {
                // 올라가는 중이거나, 덱 창을 보는 중이거나, 아직 그 층에 올라가지 않았다
            }
            else if (BattleNow)
            {
                // 전투 층에 처음 올라섰을 때 한 번 (이벤트 전투도 같다)
                if (_duelShownFloor != _run.Floor)
                    OpenDuelWindow();
            }
            else if (_run.FloorKind == FloorKind.Event && _eventShownFloor != _run.Floor)
            {
                if (!_run.EventDone)
                    OpenEvent(); // 이벤트 층에 처음 올라섰을 때 한 번
            }
            else if (_run.FloorKind == FloorKind.Rest && _restShownFloor != _run.Floor)
                OpenRest(); // 휴식 층도 처음 올라섰을 때 한 번
            else if (_run.FloorKind == FloorKind.ChipExchange && _shopShownFloor != _run.Floor)
                OpenShop(); // 칩교환소도 처음 올라섰을 때 한 번
        }

        /// <summary>1920×1080 판을 화면 안에 꽉 차게 맞춰 가운데에 놓는다 (16:9가 아니면 남는 쪽에 여백).</summary>
        void FitStage()
        {
            if (_stage == null || _stage.parent != _root)
                return;
            float w = _root.layout.width, h = _root.layout.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0)
                return;
            float scale = Mathf.Min(w / StageWidth, h / StageHeight);
            _stage.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _stage.style.left = (w - StageWidth * scale) / 2;
            _stage.style.top = (h - StageHeight * scale) / 2;
        }

        // ================= 층 종류 =================

        /// <summary>탑에서 이 층의 종류로 보여 줄 것. 아직 모르는 층은 null. preview = 미리보기로 보이는 층.</summary>
        FloorKind? ShownKind(int floor, out bool preview)
        {
            preview = false;
            if (floor == _run.Floor)
                return _run.FloorKind;
            if (floor < _run.Floor)
                return Tower.KindOf(_run.Seed, floor);
            if (Tower.RandomEventPercent(floor) == 0)
                return Tower.FixedKind(floor);
            if (floor > _run.Floor + _run.PreviewFloors)
                return null;
            preview = true;
            return Tower.KindOf(_run.Seed, floor);
        }

        static Pictogram KindIcon(FloorKind kind)
        {
            switch (kind)
            {
                case FloorKind.Boss: return Pictograms.Get("언데드족");
                case FloorKind.ChipExchange: return Pictograms.Get("층:칩교환소");
                case FloorKind.Rest: return Pictograms.Get("속성:화염");
                case FloorKind.Event: return Pictograms.Get("마법:일반");
                default: return Pictograms.Get("전사족");
            }
        }

        /// <summary>층 종류 아이콘 (모르는 층은 "?"). size는 그림의 한 변.</summary>
        static VisualElement KindMark(FloorKind? kind, float size, Color color, int questionSize)
        {
            if (kind == null)
            {
                var question = UiStyle.Label("?", questionSize, color);
                UiStyle.Black(question);
                return question;
            }
            var icon = new PictogramView(KindIcon(kind.Value), color);
            icon.style.width = icon.style.height = size;
            return icon;
        }

        // ================= 위 막대 =================

        /// <summary>
        /// 위 막대: 왼쪽에 지금 서 있는 층과 10층 묶음 칸(tower-floor-N), 오른쪽에 LP·칩·골드·연승·봉인.
        /// </summary>
        VisualElement HubTopBar()
        {
            var bar = UiStyle.Box("run-status", FlexDirection.Row);
            UiStyle.Place(bar, 0, 0, StageWidth, TopBarHeight);
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = bar.style.paddingRight = 28;
            bar.style.backgroundColor = UiStyle.Stone8;
            UiStyle.GrainOn(bar);
            bar.style.borderBottomWidth = 3;
            bar.style.borderBottomColor = UiStyle.Stone9;

            var stand = UiStyle.Label(_standFloor >= 1 ? $"{_standFloor}층" : "탑 앞", 38, UiStyle.Gold);
            stand.name = "stand-floor";
            UiStyle.Black(stand);
            stand.style.whiteSpace = WhiteSpace.NoWrap;
            stand.style.marginRight = 34;
            bar.Add(stand);

            var pips = Row();
            pips.name = "tower-pips";
            int start = Tower.BlockStart(_run.Floor);
            for (int floor = start; floor < start + Tower.BlockSize; floor++)
                pips.Add(Pip(floor));
            bar.Add(pips);

            var note = UiStyle.Label($"미리보기: 바로 위 {_run.PreviewFloors}층", 15, UiStyle.TextDim);
            note.name = "tower-preview-note";
            note.style.whiteSpace = WhiteSpace.NoWrap;
            note.style.marginLeft = 18;
            bar.Add(note);

            var space = new VisualElement();
            space.style.flexGrow = 1;
            bar.Add(space);

            bar.Add(Stat("stat-lp", "LP", _run.Lp.ToString(), UiStyle.Text));
            bar.Add(Stat("stat-chips", "칩", _run.Chips.ToString(), UiStyle.Text));
            bar.Add(Stat("stat-gold", "골드", _run.Gold.ToString(), UiStyle.Text));
            int streak = WinningStreak();
            if (_run.LosingStreak > 0)
                bar.Add(Stat("stat-streak", "연패", _run.LosingStreak.ToString(), UiStyle.Bad));
            else
                bar.Add(Stat("stat-streak", "연승", streak.ToString(), streak > 0 ? UiStyle.Good : UiStyle.Text));
            var sealedStat = Stat("stat-sealed", "봉인", (_run.SealedMain.Count + _run.SealedExtra.Count).ToString(), UiStyle.Text);
            sealedStat.style.marginRight = 0;
            bar.Add(sealedStat);
            return bar;
        }

        /// <summary>10층 묶음의 칸 하나: 층 종류 아이콘. 지나온 칸은 흐리게, 서 있는 칸은 금색 테두리.</summary>
        VisualElement Pip(int floor)
        {
            var kind = ShownKind(floor, out bool preview);
            bool now = floor == _standFloor, done = floor < _standFloor;
            var pip = new VisualElement { name = $"tower-floor-{floor}" };
            pip.style.width = pip.style.height = 36;
            pip.style.marginRight = 5;
            pip.style.alignItems = Align.Center;
            pip.style.justifyContent = Justify.Center;
            pip.style.backgroundColor = now ? UiStyle.Hex(0x3a301c) : done ? UiStyle.Stone6 : UiStyle.Stone9;
            UiStyle.Radius(pip, 4);
            UiStyle.BorderColor(pip, now ? UiStyle.Gold : UiStyle.Stone5, 2);
            if (done)
                pip.style.opacity = 0.55f;
            pip.Add(KindMark(kind, 22, now ? UiStyle.Gold : UiStyle.TextDim, 19));
            if (preview)
            {
                // 미리보기로 보이는 층: 오른쪽 위에 작은 금색 점
                var dot = new VisualElement { name = $"tower-preview-{floor}" };
                UiStyle.Place(dot, 25, 1, 7, 7);
                dot.style.backgroundColor = UiStyle.Gold;
                UiStyle.Radius(dot, 4);
                pip.Add(dot);
            }
            return pip;
        }

        static VisualElement Stat(string name, string label, string value, Color color)
        {
            var stat = Row();
            stat.style.alignItems = Align.FlexEnd;
            stat.style.marginRight = 34;
            var small = UiStyle.Label(label, 17, UiStyle.TextDim);
            small.style.marginRight = 8;
            small.style.marginBottom = 5;
            stat.Add(small);
            var number = UiStyle.Label(value, 29, color);
            number.name = name;
            UiStyle.Black(number);
            stat.Add(number);
            return stat;
        }

        /// <summary>지금 이어지고 있는 연승 수 (런 로그에서 센다. 무승부나 패배가 나오면 끊긴다).</summary>
        int WinningStreak()
        {
            int streak = 0;
            for (int i = _run.Events.Count - 1; i >= 0; i--)
            {
                var kind = _run.Events[i].Kind;
                if (kind == RunEventKind.DuelWon)
                    streak++;
                else if (kind == RunEventKind.DuelLost || kind == RunEventKind.DuelForfeited)
                    break;
            }
            return streak;
        }

        // ================= 층 팻말 =================

        void AddPlaques()
        {
            _plaques.Clear();
            var layer = new VisualElement { name = "floor-plaques", pickingMode = PickingMode.Ignore };
            UiStyle.Place(layer, 0, 0, StageWidth, StageHeight);
            for (int i = 0; i < 11; i++)
            {
                var plaque = UiStyle.Box(null, FlexDirection.Row);
                plaque.pickingMode = PickingMode.Ignore;
                UiStyle.Place(plaque, 1032, 0);
                plaque.style.height = 58;
                plaque.style.alignItems = Align.Center;
                plaque.style.paddingLeft = 7;
                plaque.style.paddingRight = 18;
                plaque.style.backgroundColor = UiStyle.Stone8;
                UiStyle.GrainOn(plaque);
                UiStyle.BorderColor(plaque, UiStyle.Stone9, 3);
                _plaques.Add(plaque);
                layer.Add(plaque);
            }
            _stage.Add(layer);
            UpdatePlaques();
        }

        /// <summary>팻말을 장면의 높이에 맞춰 옮기고, 가리키는 층이 바뀐 팻말은 내용을 다시 채운다.</summary>
        void UpdatePlaques()
        {
            float altitude = Mathf.Max(1f, _scenePosition);
            int first = Mathf.FloorToInt(altitude) - 3;
            for (int i = 0; i < _plaques.Count; i++)
            {
                int floor = first + i;
                var plaque = _plaques[i];
                if (floor < 1)
                {
                    plaque.style.display = DisplayStyle.None;
                    continue;
                }
                plaque.style.display = DisplayStyle.Flex;
                plaque.style.top = TowerScene.FloorY(floor, altitude) - TowerScene.FloorHeight / 2 - 29;
                FillPlaque(plaque, floor);
            }
        }

        void FillPlaque(VisualElement plaque, int floor)
        {
            var kind = ShownKind(floor, out bool preview);
            bool now = floor == _standFloor, done = floor < _standFloor, next = floor == _run.Floor && !now;
            string key = $"{floor}|{kind}|{preview}|{now}|{done}|{next}";
            if ((string)plaque.userData == key)
                return;
            plaque.userData = key;
            plaque.name = $"floor-plaque-{floor}";
            plaque.Clear();
            plaque.style.opacity = done ? 0.5f : 1f;
            UiStyle.BorderColor(plaque, now ? UiStyle.Gold : UiStyle.Stone9, 3);
            if (!now)
                plaque.style.borderTopColor = UiStyle.SlabTop;

            // 탑 쪽으로 뻗은 짧은 줄
            var stem = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyle.Place(stem, -32, 24, 26, 3);
            stem.style.backgroundColor = UiStyle.Stone9;
            plaque.Add(stem);

            var icon = new VisualElement();
            icon.style.width = icon.style.height = 40;
            icon.style.marginRight = 12;
            icon.style.alignItems = Align.Center;
            icon.style.justifyContent = Justify.Center;
            icon.style.backgroundColor = kind == null ? UnknownFloorColor : FloorColor(kind.Value);
            UiStyle.Radius(icon, 4);
            icon.Add(KindMark(kind, 28, PlaqueInk, 24));
            plaque.Add(icon);

            var number = UiStyle.Label($"{floor}층", 24, now ? UiStyle.Gold : UiStyle.TextDim);
            number.style.whiteSpace = WhiteSpace.NoWrap;
            number.style.marginRight = 12;
            plaque.Add(number);
            var kindName = UiStyle.Label(kind == null ? "?" : Tower.KindName(kind.Value), 24, now ? UiStyle.Gold : UiStyle.Text, bold: true);
            kindName.style.whiteSpace = WhiteSpace.NoWrap;
            plaque.Add(kindName);
            if (now)
                plaque.Add(PlaqueTag("지금", gold: true));
            else if (next)
                plaque.Add(PlaqueTag("다음", gold: false));
            else if (preview)
                plaque.Add(PlaqueTag("미리보기", gold: false));
        }

        static Label PlaqueTag(string text, bool gold)
        {
            var tag = UiStyle.Label(text, 16, gold ? GoldInk : UiStyle.TextDim, bold: true);
            tag.style.whiteSpace = WhiteSpace.NoWrap;
            tag.style.marginLeft = 12;
            tag.style.paddingLeft = tag.style.paddingRight = 8;
            tag.style.paddingTop = tag.style.paddingBottom = 1;
            UiStyle.Radius(tag, 3);
            if (gold)
                tag.style.backgroundColor = UiStyle.Gold;
            else
                UiStyle.BorderColor(tag, UiStyle.Stone4, 2);
            return tag;
        }

        // ================= 높이 자 =================

        const float GaugeHeight = 740;

        /// <summary>높이 자의 눈금 범위: 1~10층은 0~10, 11층에 올라서면 0~20 …</summary>
        static float GaugeMax(int floor) => Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(floor, 1) / 10f)) * 10;

        /// <summary>
        /// 높이 자: 막대의 길이는 언제나 같고 눈금 범위만 늘어난다. 범위나 층이 바뀌면 0.6초에 걸쳐 새 자리로 미끄러진다.
        /// 탑이 몇 층까지인지는 보여 주지 않는다.
        /// </summary>
        VisualElement HeightGauge()
        {
            var gauge = new VisualElement { name = "height-gauge", pickingMode = PickingMode.Ignore };
            UiStyle.Place(gauge, 34, 118, 20, GaugeHeight);
            gauge.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.6f);
            UiStyle.BorderColor(gauge, UiStyle.Stone9, 3);
            var fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = fill.style.right = fill.style.bottom = 0;
            fill.style.backgroundColor = UiStyle.GoldDeep;
            gauge.Add(fill);

            float targetFloor = Mathf.Max(0, _standFloor), targetMax = GaugeMax(_standFloor);
            var ticks = new List<(int Floor, Label Label)>();
            for (int f = 0; f <= Mathf.Max(targetMax, _gaugeMax); f += 10)
            {
                var tick = UiStyle.Label(f.ToString(), 16, UiStyle.TextDim, bold: true);
                tick.style.position = Position.Absolute;
                tick.style.left = 24;
                tick.style.height = 22;
                tick.style.whiteSpace = WhiteSpace.NoWrap;
                tick.style.unityTextAlign = TextAnchor.MiddleCenter;
                tick.style.paddingLeft = tick.style.paddingRight = 7;
                tick.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.55f);
                UiStyle.Radius(tick, 3);
                gauge.Add(tick);
                ticks.Add((f, tick));
            }
            var here = UiStyle.Label(_standFloor >= 1 ? $"{_standFloor}층" : "탑 앞", 20, GoldInk);
            UiStyle.Black(here);
            here.style.position = Position.Absolute;
            here.style.left = 24;
            here.style.height = 28;
            here.style.whiteSpace = WhiteSpace.NoWrap;
            here.style.unityTextAlign = TextAnchor.MiddleCenter;
            here.style.paddingLeft = here.style.paddingRight = 10;
            here.style.backgroundColor = UiStyle.Gold;
            UiStyle.Radius(here, 3);
            gauge.Add(here);

            // 테두리 안쪽 높이 기준으로 놓는다
            const float inner = GaugeHeight - 6;
            void Lay(float floor, float max)
            {
                fill.style.height = inner * Mathf.Clamp01(floor / max);
                foreach (var (f, label) in ticks)
                {
                    float at = f / max;
                    label.style.bottom = inner * Mathf.Min(at, 1.15f) - 11;
                    label.style.opacity = at <= 1.001f ? 1f : Mathf.Clamp01(1f - (at - 1f) * 8f);
                }
                here.style.bottom = inner * Mathf.Clamp01(floor / max) - 14;
            }

            float fromFloor = _gaugeFloor, fromMax = _gaugeMax;
            if (Mathf.Approximately(fromFloor, targetFloor) && Mathf.Approximately(fromMax, targetMax))
            {
                Lay(targetFloor, targetMax);
                return gauge;
            }
            Lay(fromFloor, fromMax);
            float started = Time.realtimeSinceStartup;
            IVisualElementScheduledItem slide = null;
            slide = gauge.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / 0.6f);
                float eased = 1f - (1f - t) * (1f - t);
                _gaugeFloor = Mathf.Lerp(fromFloor, targetFloor, eased);
                _gaugeMax = Mathf.Lerp(fromMax, targetMax, eased);
                Lay(_gaugeFloor, _gaugeMax);
                if (t >= 1f)
                    slide.Pause();
            }).Every(16);
            return gauge;
        }

        // ================= 직전 듀얼 판 =================

        VisualElement LastDuelPanel()
        {
            var panel = UiStyle.PanelBox("last-duel-panel");
            panel.style.position = Position.Absolute;
            panel.style.left = 28;
            panel.style.bottom = 28;
            panel.style.width = 430;
            panel.style.paddingLeft = panel.style.paddingRight = 22;
            panel.style.paddingTop = panel.style.paddingBottom = 16;
            panel.Add(UiStyle.Label("직전 듀얼", 17, UiStyle.TextDim));
            var result = _run.Events.LastOrDefault(e => e.Kind == RunEventKind.DuelWon || e.Kind == RunEventKind.DuelLost);
            string title = _run.DuelsPlayed == 0 ? "[ 아직 없음. ]"
                : result == null || result.DuelNumber != _run.DuelsPlayed ? "[ 무승부. ]"
                : result.Kind == RunEventKind.DuelWon ? "[ 승리. ]" : "[ 패배. ]";
            var head = UiStyle.Label(title, 34, UiStyle.Gold);
            UiStyle.Black(head);
            panel.Add(head);
            var last = Text(LastDuelLine(), 18, top: 2);
            last.name = "last-duel";
            // 길면 세 줄까지만 보인다 (전부는 런 기록에서 본다). 판이 높이 자를 가리지 않게 한다.
            last.style.maxHeight = 72;
            last.style.overflow = Overflow.Hidden;
            panel.Add(last);
            string record = $"{_run.Wins}승 {_run.Losses}패" + (_run.Draws > 0 ? $" {_run.Draws}무" : "");
            var summary = Text($"{record} · 끝낸 듀얼 {_run.DuelsPlayed}", 17, UiStyle.TextDim, top: 8);
            summary.name = "run-summary";
            panel.Add(summary);
            if (_forfeitNotice != null)
            {
                var forfeit = Text(_forfeitNotice, 15, UiStyle.Opponent, top: 6);
                forfeit.name = "forfeit-notice";
                panel.Add(forfeit);
            }
            if (_saveNotice != null)
                panel.Add(Text(_saveNotice, 15, UiStyle.Opponent, top: 6));
            return panel;
        }

        /// <summary>직전에 한 일의 결과를 위 막대 아래 가운데에 띄운다 (덱 창이 열려 있으면 덱 창 아래 줄에 쓴다).</summary>
        VisualElement HubNotice()
        {
            var line = Row();
            line.pickingMode = PickingMode.Ignore;
            UiStyle.Place(line, 0, TopBarHeight + 14, StageWidth);
            line.style.justifyContent = Justify.Center;
            var notice = NoticeLabel(19);
            notice.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.86f);
            notice.style.paddingLeft = notice.style.paddingRight = 16;
            notice.style.paddingTop = notice.style.paddingBottom = 6;
            notice.style.maxWidth = 1100;
            UiStyle.Radius(notice, 6);
            line.Add(notice);
            return line;
        }

        Label NoticeLabel(int size)
        {
            var notice = Text(_moveNotice ?? "", size, _moveNoticeIsError ? UiStyle.Opponent : Positive);
            notice.name = "move-notice";
            notice.pickingMode = PickingMode.Ignore;
            notice.style.display = _moveNotice == null ? DisplayStyle.None : DisplayStyle.Flex;
            return notice;
        }

        // ================= 명령 메뉴 =================

        /// <summary>
        /// 세로 메뉴 (docs/UI.md "부품"). 고른 항목은 금색 테두리와 왼쪽의 ▶. 마우스를 올리면 그 항목이 골라지고, 누르면 실행한다.
        /// 키보드는 화면이 Move·Activate를 부른다. 항목은 Button이라 화면 시험이 이름으로 찾아 누를 수 있다.
        /// </summary>
        sealed class MenuList
        {
            public readonly VisualElement Root = UiStyle.Box();
            readonly List<(Button Button, Label Mark, Action Run)> _items = new List<(Button, Label, Action)>();

            public int Index { get; private set; } = -1;
            public int Count => _items.Count;

            public Button Add(string name, string text, Action run, string sub = null, string subName = null, bool enabled = true, int height = 60, int size = 27)
            {
                int index = _items.Count;
                var button = new Button(run) { name = name, text = "", focusable = false };
                button.style.flexDirection = FlexDirection.Row;
                button.style.alignItems = Align.Center;
                button.style.height = height;
                button.style.flexShrink = 0;
                UiStyle.Margin(button, 0);
                button.style.marginTop = index == 0 ? 0 : 5;
                button.style.paddingTop = button.style.paddingBottom = 0;
                button.style.paddingLeft = 10;
                button.style.paddingRight = 16;
                button.style.backgroundColor = Color.clear;
                UiStyle.Radius(button, 6);
                UiStyle.BorderColor(button, Color.clear, 2);
                var mark = UiStyle.Label("▶", 17, Color.clear);
                mark.style.width = 20;
                mark.style.marginRight = 12;
                mark.pickingMode = PickingMode.Ignore;
                button.Add(mark);
                var title = UiStyle.Label(text, size, UiStyle.Text, bold: true);
                title.style.whiteSpace = WhiteSpace.NoWrap;
                title.pickingMode = PickingMode.Ignore;
                button.Add(title);
                if (sub != null)
                {
                    var small = UiStyle.Label(sub, 19, UiStyle.TextDim);
                    small.name = subName;
                    small.style.whiteSpace = WhiteSpace.NoWrap;
                    small.style.marginLeft = StyleKeyword.Auto;
                    small.pickingMode = PickingMode.Ignore;
                    button.Add(small);
                }
                button.SetEnabled(enabled);
                button.RegisterCallback<MouseEnterEvent>(_ => Select(index));
                _items.Add((button, mark, run));
                Root.Add(button);
                return button;
            }

            public void Select(int index)
            {
                if (_items.Count == 0)
                    return;
                Index = Mathf.Clamp(index, 0, _items.Count - 1);
                for (int i = 0; i < _items.Count; i++)
                {
                    bool on = i == Index;
                    var (button, mark, _) = _items[i];
                    // 선형 색 공간에서는 반투명한 밝은 색이 더 밝게 보여서 문서의 16%보다 낮춘다
                    button.style.backgroundColor = on ? UiStyle.Alpha(UiStyle.Gold, 0.1f) : Color.clear;
                    UiStyle.BorderColor(button, on ? UiStyle.Gold : Color.clear, 2);
                    mark.style.color = on ? UiStyle.Gold : Color.clear;
                    button.EnableInClassList("selected", on);
                }
            }

            /// <summary>처음 고를 항목: index부터 아래로 켜져 있는 첫 항목.</summary>
            public void SelectEnabled(int index)
            {
                index = Mathf.Clamp(index, 0, Mathf.Max(0, _items.Count - 1));
                for (int n = 0; n < _items.Count; n++)
                {
                    int i = (index + n) % _items.Count;
                    if (_items[i].Button.enabledSelf)
                    {
                        Select(i);
                        return;
                    }
                }
                Select(index);
            }

            /// <summary>위아래로 옮긴다 (끝에서 반대쪽 끝으로 넘어간다). 꺼진 항목은 건너뛴다.</summary>
            public void Move(int delta)
            {
                if (_items.Count == 0)
                    return;
                int i = Index < 0 ? 0 : Index;
                for (int n = 0; n < _items.Count; n++)
                {
                    i = (i + delta + _items.Count) % _items.Count;
                    if (_items[i].Button.enabledSelf)
                        break;
                }
                Select(i);
            }

            public void Activate()
            {
                if (Index >= 0 && Index < _items.Count && _items[Index].Button.enabledInHierarchy)
                    _items[Index].Run?.Invoke();
            }
        }

        /// <summary>
        /// 명령 메뉴: 허브에서 할 수 있는 일을 모두 모은다. 위에 다음 층(또는 지금 층)과 그 층 안내, 아래에 키 도움말.
        /// </summary>
        VisualElement HubMenuPanel()
        {
            var panel = UiStyle.PanelBox("hub-menu");
            panel.style.position = Position.Absolute;
            panel.style.right = 28;
            panel.style.bottom = 28;
            panel.style.width = 470;
            UiStyle.Padding(panel, 16);
            panel.style.paddingBottom = 14;

            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.style.paddingLeft = head.style.paddingRight = 10;
            var prefix = UiStyle.Label(Arrived ? "지금 층" : "다음 층", 18, UiStyle.TextDim);
            prefix.style.marginRight = 12;
            prefix.style.marginBottom = 4;
            head.Add(prefix);
            var floorLabel = UiStyle.Label($"{_run.Floor}층 · {Tower.KindName(_run.FloorKind)}", 30);
            floorLabel.name = "floor-label";
            UiStyle.Black(floorLabel);
            head.Add(floorLabel);
            panel.Add(head);
            var note = Text(FloorNote(), 15, UiStyle.TextDim, top: 4);
            note.name = "floor-note";
            note.style.paddingLeft = note.style.paddingRight = 10;
            note.style.marginBottom = 10;
            panel.Add(note);

            bool ready = _run.Phase == RunPhase.Ready;
            var menu = new MenuList();
            if (!Arrived)
                menu.Add("climb", $"{_run.Floor}층으로 올라간다", () => StartClimb(), enabled: ready);
            else if (BattleNow)
                menu.Add("open-duel", _run.FloorKind == FloorKind.Boss ? "보스 듀얼" : _run.FloorKind == FloorKind.Event ? "이벤트 전투" : "듀얼",
                    OpenDuelWindow, "상대가 기다린다", enabled: ready);
            else if (_run.FloorKind == FloorKind.Event && !_run.EventDone)
                menu.Add("open-event", "이벤트", OpenEvent, enabled: ready);
            else
                menu.Add("leave-floor", $"{_run.Floor + 1}층으로 올라간다", LeaveFloor, enabled: ready);
            if (Arrived && _run.FloorKind == FloorKind.ChipExchange)
                menu.Add("open-shop", "칩교환소", OpenShop, $"칩 {_run.Chips}", enabled: ready);
            if (Arrived && _run.FloorKind == FloorKind.Rest)
                menu.Add("open-rest", "휴식", OpenRest, enabled: ready);
            menu.Add("open-deck", "덱", () => OpenDeck(_deckTab == Pile.Sealed ? Pile.Main : _deckTab),
                $"메인 {_run.MainDeck.Count} · 사이드 {_run.SideDeck.Count}", "deck-counts");
            menu.Add("open-sealed", "봉인", () => OpenDeck(Pile.Sealed), $"{_run.SealedMain.Count + _run.SealedExtra.Count}장");
            menu.Add("open-log", "런 기록", OpenLog);
            menu.Add("to-title", "처음 화면으로", GoToStart);
            menu.SelectEnabled(_hubMenuIndex);
            _hubMenu = menu;
            panel.Add(menu.Root);

            var hint = Hints(("↑", "W", null), ("↓", "S", "고르기"), ("Z", "J", "결정"));
            hint.style.paddingLeft = 10;
            hint.style.marginTop = 10;
            panel.Add(hint);
            return panel;
        }

        // ================= 띠 알림 =================

        void AddBanner()
        {
            _banner = Row();
            _banner.name = "banner";
            _banner.pickingMode = PickingMode.Ignore;
            UiStyle.Place(_banner, 0, 290, StageWidth, 116);
            _banner.style.alignItems = Align.Center;
            _banner.style.justifyContent = Justify.Center;
            _banner.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.92f);
            _banner.style.borderTopWidth = _banner.style.borderBottomWidth = 3;
            _banner.style.borderTopColor = _banner.style.borderBottomColor = UiStyle.GoldDeep;
            _banner.style.display = DisplayStyle.None;
            _bannerText = UiStyle.Label("", 48);
            UiStyle.Black(_bannerText);
            _bannerText.style.whiteSpace = WhiteSpace.NoWrap;
            _banner.Add(_bannerText);
            _stage.Add(_banner);
        }

        /// <summary>띠 알림: 0.16초에 펴지고 1.15초 머문 뒤 사라진다. 누를 필요가 없다.</summary>
        IEnumerator Banner(string text, Color color)
        {
            _bannerText.text = text;
            _bannerText.style.color = color;
            _banner.style.display = DisplayStyle.Flex;
            yield return Tween(0.16f, t =>
            {
                _banner.style.opacity = t;
                _banner.style.scale = new Scale(new Vector3(1f, 0.2f + 0.8f * t, 1f));
            });
            yield return Tween(1.15f, _ => { });
            yield return Tween(0.2f, t => _banner.style.opacity = 1f - t);
            _banner.style.display = DisplayStyle.None;
        }

        static IEnumerator Tween(float seconds, Action<float> step)
        {
            float t = 0;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / seconds);
                step(t);
                yield return null;
            }
        }

        // ================= 올라가는 연출 =================

        /// <summary>연출 중에 화면 전체를 덮는 투명한 버튼: 누르면 연출을 건너뛴다.</summary>
        VisualElement ClimbSkip()
        {
            var skip = new Button(SkipClimb) { name = "climb-skip", text = "", focusable = false };
            UiStyle.Place(skip, 0, 0, StageWidth, StageHeight);
            UiStyle.Margin(skip, 0);
            UiStyle.Padding(skip, 0);
            skip.style.backgroundColor = Color.clear;
            UiStyle.BorderColor(skip, Color.clear, 0);
            skip.style.justifyContent = Justify.FlexEnd;
            skip.style.alignItems = Align.Center;
            var hint = Hints(("Z", "J", "누르면 건너뛴다"));
            hint.pickingMode = PickingMode.Ignore;
            hint.style.marginBottom = 26;
            hint.style.paddingLeft = hint.style.paddingRight = 14;
            hint.style.paddingTop = hint.style.paddingBottom = 6;
            hint.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.7f);
            UiStyle.Radius(hint, 6);
            skip.Add(hint);
            return skip;
        }

        /// <summary>"N층으로 올라간다": 아직 올라가지 않은 지금 층으로 올라가는 연출을 시작한다. 시작하지 못했으면 false.</summary>
        bool StartClimb()
        {
            if (_climbing || _run == null || _run.Phase != RunPhase.Ready || Arrived || !isActiveAndEnabled)
                return false;
            _deckOpen = _deckFromDuel = false;
            _deckScroll = null;
            _climbing = true;
            // 두 층 넘게 벌어져 있으면 (없어야 하지만) 마지막 한 층만 보여 준다
            _scenePosition = Mathf.Max(_standFloor, _run.Floor - 1);
            _sceneOpponent = 0;
            _moveNotice = null; // 앞 층에서 한 일의 알림은 여기까지
            ShowHub();
            _climbRoutine = StartCoroutine(ClimbRoutine());
            return true;
        }

        IEnumerator ClimbRoutine()
        {
            int to = _run.Floor;
            float from = _scenePosition;
            yield return Tween(ClimbSeconds, t =>
            {
                float eased = t < 0.5f ? 2f * t * t : 1f - (2f - 2f * t) * (2f - 2f * t) / 2f;
                SetScene(Mathf.Lerp(from, to, eased), 0f);
            });
            _standFloor = to;
            _scenePosition = to;
            ShowHub(); // 위 막대·팻말·높이 자를 새 층에 맞춘다. 연출 중이라 창은 아직 뜨지 않는다.
            yield return Banner($"{to}층에 도착했다.", UiStyle.Text);
            if (BattleNow)
            {
                yield return Tween(EncounterSeconds, t => SetScene(to, t));
                yield return Banner(_run.FloorKind == FloorKind.Boss ? "층의 주인이 기다리고 있다." : "누군가 듀얼을 걸어왔다!", UiStyle.Text);
            }
            else
            {
                yield return Banner(_run.FloorKind == FloorKind.ChipExchange ? "칩교환소가 열려 있다."
                    : _run.FloorKind == FloorKind.Rest ? "쉬어 갈 수 있는 곳이다." : "무언가 있다.", UiStyle.Text);
            }
            _climbRoutine = null;
            FinishClimb();
        }

        void SetScene(float position, float opponent)
        {
            _scenePosition = position;
            _sceneOpponent = opponent;
            if (_scene != null)
            {
                _scene.Marker = position;
                _scene.Opponent = opponent;
            }
            UpdatePlaques();
        }

        /// <summary>도착: 말이 지금 층에 서고, 그 층의 창이 뜬다 (ShowHub).</summary>
        void FinishClimb()
        {
            _climbing = false;
            _standFloor = _run.Floor;
            ShowHub();
        }

        /// <summary>연출을 건너뛴다: 곧바로 도착한 상태로 만든다.</summary>
        void SkipClimb()
        {
            if (!_climbing)
                return;
            StopClimb();
            FinishClimb();
        }

        void StopClimb()
        {
            if (_climbRoutine != null)
                StopCoroutine(_climbRoutine);
            _climbRoutine = null;
            _climbing = false;
        }

        // ================= 듀얼 창 =================

        /// <summary>
        /// 듀얼 창: 모든 전투(일반·보스·이벤트 전투) 직전에 뜬다. "듀얼 시작"은 코인 토스로 넘어가고, "덱 조정"은 덱 창을 열었다가 돌아온다.
        /// 상대 덱 이름은 가린다 (상대를 보고 덱을 맞출 수 없게).
        /// </summary>
        void OpenDuelWindow()
        {
            if (_run == null || _run.Phase != RunPhase.Ready || !Arrived || !BattleNow || _climbing)
                return;
            _duelShownFloor = _run.Floor;
            _eventShownFloor = _run.Floor;
            var panel = OpenModal(800);
            panel.name = "duel-window";
            _modalLayer.Q("modal-hints")?.RemoveFromHierarchy(); // 이 창은 제 키 도움말이 따로 있다
            panel.style.alignItems = Align.Center;
            panel.style.paddingLeft = panel.style.paddingRight = 30;
            panel.style.paddingTop = panel.style.paddingBottom = 26;
            UiStyle.BorderColor(panel, UiStyle.Stone9, 3);
            panel.style.borderTopColor = UiStyle.SlabTop;
            _duelWindow = panel;

            bool boss = _run.FloorKind == FloorKind.Boss;
            var eventBattle = _run.FloorKind == FloorKind.Event ? _run.PendingEventBattle : null;
            var title = UiStyle.Label("[ 듀얼! 패배 시 페널티 발생! ]", 34, UiStyle.Gold);
            UiStyle.Black(title);
            panel.Add(title);
            string where = boss ? $"{_run.Floor}층 · 보스" : eventBattle != null ? $"{_run.Floor}층 · 이벤트 전투 「{_run.CurrentEvent?.Title}」" : $"{_run.Floor}층 · 전투";
            panel.Add(Text(where, 19, UiStyle.TextDim, top: 4));
            if (eventBattle != null)
            {
                var story = Text(eventBattle.ResultText ?? "", 18, top: 10);
                story.style.unityTextAlign = TextAnchor.MiddleCenter;
                panel.Add(story);
                var rule = Text("이기면 이벤트의 결과를 받는다 (승리 보상과 칩은 없다). 지면 일반 패배처럼 봉인된다. 어느 쪽이든 다음 층으로 간다.", 16, UiStyle.TextDim, top: 6);
                rule.style.unityTextAlign = TextAnchor.MiddleCenter;
                panel.Add(rule);
            }

            var versus = Row();
            versus.style.alignItems = Align.Center;
            versus.style.marginTop = 18;
            versus.style.marginBottom = 22;
            int lp = boss ? RunRules.FullLp : _run.Lp;
            versus.Add(UiStyle.Label($"나 · LP {lp}", 27, UiStyle.Mine, bold: true));
            var vs = UiStyle.Label("VS", 40, UiStyle.TextDim);
            UiStyle.Black(vs);
            vs.style.marginLeft = vs.style.marginRight = 26;
            versus.Add(vs);
            var opponent = UiStyle.Label("상대 · ???", 27, UiStyle.Opponent, bold: true);
            opponent.name = "duel-opponent";
            versus.Add(opponent);
            panel.Add(versus);

            var buttons = Row();
            _duelStart = UiStyle.StoneButton(boss ? "보스 듀얼 시작" : eventBattle != null ? "이벤트 전투 시작" : "듀얼 시작", StartNextDuel, gold: true);
            _duelStart.name = "next-duel";
            _duelStart.style.width = 300;
            _duelStart.style.marginRight = 16;
            buttons.Add(_duelStart);
            _duelDeck = UiStyle.StoneButton("덱 조정", () => OpenDeck(_deckTab == Pile.Sealed ? Pile.Main : _deckTab, fromDuel: true));
            _duelDeck.name = "duel-deck";
            _duelDeck.style.width = 300;
            buttons.Add(_duelDeck);
            foreach (var button in new[] { _duelStart, _duelDeck })
            {
                var mark = UiStyle.Label("▶", 19, Color.clear);
                mark.name = "choice-mark";
                mark.pickingMode = PickingMode.Ignore;
                UiStyle.Place(mark, 16, 18);
                button.Add(mark);
            }
            panel.Add(buttons);
            _duelStart.RegisterCallback<MouseEnterEvent>(_ => SetDuelChoice(0));
            _duelDeck.RegisterCallback<MouseEnterEvent>(_ => SetDuelChoice(1));
            SetDuelChoice(0);

            var later = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "나중에"));
            later.style.marginTop = 16;
            panel.Add(later);
            ApplyKeyCaps();
        }

        void SetDuelChoice(int choice)
        {
            _duelChoice = choice;
            if (_duelStart == null || _duelDeck == null)
                return;
            _duelStart.Q<Label>("choice-mark").style.color = choice == 0 ? GoldInk : Color.clear;
            _duelDeck.Q<Label>("choice-mark").style.color = choice == 1 ? UiStyle.Gold : Color.clear;
        }

        bool DuelWindowOpen => _duelWindow != null && _duelWindow.panel != null && _modalLayer.style.display != DisplayStyle.None;

        /// <summary>
        /// 직전 듀얼 한 줄: 결과 · 칩 · 보상 · 봉인 … (런 로그의 그 듀얼 부분을 이어 붙인 것). 다음 층에 들어간 뒤의 일(칩교환소 등)은 넣지 않는다.
        /// </summary>
        string LastDuelLine()
        {
            if (_run.DuelsPlayed == 0)
                return "아직 듀얼하지 않았다. 1층부터 시작한다.";
            var events = _run.Events.Where(e => e.DuelNumber == _run.DuelsPlayed)
                .TakeWhile(e => e.Kind != RunEventKind.FloorEntered)
                .Where(e => e.Kind != RunEventKind.DuelStarted && e.Kind != RunEventKind.MovedToDeck && e.Kind != RunEventKind.MovedToSide)
                .Select(e => e.Describe(_text.CardName)).ToList();
            return string.Join(" · ", events);
        }

        void OpenLog()
        {
            var panel = OpenModal(1100);
            panel.name = "run-log";
            panel.style.height = Length.Percent(86);
            var head = Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.Add(UiStyle.Label("런 로그", 26, UiStyle.Accent, bold: true));
            var close = UiStyle.Button("닫기", CloseModal, 18);
            close.name = "close-log";
            head.Add(close);
            panel.Add(head);
            panel.Add(Text($"시작 덱 {_run.StartDeckName} · 시드 {_run.Seed}", 15, UiStyle.TextDim, top: 2));
            panel.Add(EventLog());
        }

        /// <summary>
        /// "지금 덱을 레시피로 저장" 창: 지금 메인·엑스트라 덱을 새 레시피로 (PlayerRecipe.FromRun). 시작 사이드 덱은 비운다
        /// (런 중 사이드 덱은 장수 제한이 없어 시작 사이드 덱 한도와 맞지 않는다). 봉인된 카드는 넣지 않는다.
        /// </summary>
        void OpenSaveRecipe()
        {
            if (_run == null || _run.Phase != RunPhase.Ready)
                return;
            var panel = OpenModal(720);
            panel.name = "save-recipe-panel";
            panel.Add(UiStyle.Label("지금 덱을 레시피로 저장", 26, UiStyle.Accent, bold: true));
            var note = Text($"메인 덱 {_run.MainDeck.Count}장과 엑스트라 덱 {_run.ExtraDeck.Count}장을 새 덱 레시피로 저장합니다. " +
                            "사이드 덱과 봉인된 카드는 넣지 않습니다 (시작 사이드 덱은 런 시작 화면의 덱 편집에서 고릅니다). " +
                            "런에서 얻은 카드도 컬렉션에 들어 있어 다음 런에 쓸 수 있습니다.", 15, UiStyle.TextDim, top: 8);
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);
            panel.Add(SubHeading("레시피 이름"));
            var name = DarkTextField("recipe-name", ClipName($"{_run.StartDeckName} {_run.Floor}층"), 20);
            name.maxLength = PlayerRecipe.MaxNameLength;
            panel.Add(name);
            var error = Text("", 15, UiStyle.Opponent, top: 6);
            error.name = "recipe-error";
            error.style.whiteSpace = WhiteSpace.Normal;
            error.style.display = DisplayStyle.None;
            panel.Add(error);
            var buttons = Row();
            buttons.style.marginTop = 12;
            buttons.style.justifyContent = Justify.FlexEnd;
            var confirm = UiStyle.Button("저장", () => SaveRunAsRecipe(name.value, error), 20);
            confirm.name = "recipe-save-confirm";
            buttons.Add(confirm);
            var cancel = UiStyle.Button("취소", CloseModal, 20);
            cancel.name = "recipe-save-cancel";
            cancel.style.marginLeft = 8;
            buttons.Add(cancel);
            panel.Add(buttons);
        }

        void SaveRunAsRecipe(string name, Label error)
        {
            var recipe = PlayerRecipe.FromRun(_run, _save.NewRecipeId(), (name ?? "").Trim());
            var problems = _save.SaveRecipe(recipe, _data.Database);
            if (problems.Count > 0)
            {
                ShowError(error, "저장할 수 없습니다: " + string.Join(" / ", problems));
                return;
            }
            Persist();
            Debug.Log($"[런] 지금 덱을 레시피로: {recipe.Name} ({recipe.Id}), 덱 {recipe.Deck.Count}장");
            _moveNotice = $"지금 덱을 레시피 「{recipe.Name}」(으)로 저장했습니다. 런 시작 화면의 덱 레시피 목록에 있습니다.";
            _moveNoticeIsError = false;
            ShowHub();
        }

        IReadOnlyList<uint> Cards(Pile pile)
        {
            switch (pile)
            {
                case Pile.Main: return _run.MainDeck;
                case Pile.Extra: return _run.ExtraDeck;
                case Pile.Side: return _run.SideDeck;
                case Pile.Sealed: return _run.SealedMain.Concat(_run.SealedExtra).ToList();
                case Pile.Shop: return _run.ShopDeck;
                default: return Array.Empty<uint>();
            }
        }

        /// <summary>고른 카드가 그 목록에서 사라졌으면 (봉인, 교환) 그 카드가 있는 다른 목록을 고른다. 아무 데도 없으면 고르지 않은 상태.</summary>
        void FixSelection()
        {
            if (_selectedCode == 0 || (HubPiles.Contains(_selectedPile) && Cards(_selectedPile).Contains(_selectedCode)))
                return;
            foreach (Pile pile in HubPiles)
            {
                if (Cards(pile).Contains(_selectedCode))
                {
                    _selectedPile = pile;
                    return;
                }
            }
            _selectedCode = 0;
        }

        void ApplyItem(uint code, RunPile where)
        {
            if (_run == null || !_run.CanApplyItem(code, where, out string reason))
                return;
            _run.ApplyItem(code, where);
            Persist();
            _moveNotice = $"아이템 효과 적용: {_text.CardName(code)} ({RunEvent.PileName(where)}) — {_run.ItemOf(code).EffectText}" + (_run.IsBound(code) ? " · 귀속" : "");
            _moveNoticeIsError = false;
            Debug.Log($"[런] {_moveNotice}");
            ShowHub();
        }

        /// <summary>덱 ↔ 사이드 덱 옮기기 (끌어다 놓기, 두 번 누르기, 버튼). 고른 카드는 옮겨 간 곳에서 계속 고른 상태.</summary>
        void TryMove(Pile from, uint code, bool toDeck)
        {
            if (_run == null || _run.Phase != RunPhase.Ready)
                return;
            if (toDeck ? from != Pile.Side : from != Pile.Main && from != Pile.Extra)
                return; // 같은 칸에 놓았거나 봉인된 카드
            string name = _text.CardName(code);
            bool ok = toDeck ? _run.CanMoveToDeck(code, out string reason) : _run.CanMoveToSide(code, out reason);
            if (!ok)
            {
                SetMoveNotice($"옮길 수 없습니다 — {name}: {reason}", error: true);
                return;
            }
            bool extra = _run.IsExtraDeckCard(code);
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
            Persist();
            _moveNotice = toDeck ? $"{name} → {(extra ? "엑스트라" : "메인")} 덱" : $"{name} → 사이드 덱";
            _moveNoticeIsError = false;
            _selectedPile = toDeck ? (extra ? Pile.Extra : Pile.Main) : Pile.Side;
            _selectedCode = code;
            ShowHub();
        }

        void SetMoveNotice(string text, bool error)
        {
            _moveNotice = text;
            _moveNoticeIsError = error;
            var label = _root.Q<Label>("move-notice");
            if (label == null)
                return;
            label.text = text;
            label.style.color = error ? UiStyle.Opponent : Positive;
            label.style.display = DisplayStyle.Flex;
        }

        // ================= 보충 고르기 =================

        void OpenRefill()
        {
            var candidates = _run.RefillCandidates();
            if (!candidates.Contains(_refillSelected))
                _refillSelected = 0;
            var panel = OpenModal(1500);
            panel.name = "refill";
            panel.style.height = Length.Percent(88);
            panel.Add(UiStyle.Label($"보충: 메인 덱 {_run.MainDeck.Count}장 → {RunRules.MinMainDeck}장", 28, UiStyle.Accent, bold: true));
            panel.Add(Text($"메인 덱이 {RunRules.MinMainDeck}장보다 적어졌습니다. 사이드 덱에서 메인 덱 카드 {_run.RefillNeeded}장을 더 골라 넣으세요. " +
                           $"두 번 누르면 바로 넣습니다. 같은 카드명은 덱에 {RunRules.MaxCopies}장까지 넣을 수 있고, 다 채우면 이 창이 닫힙니다.",
                16, UiStyle.TextDim, top: 6));
            var sealedNames = _run.Events.Where(e => e.DuelNumber == _run.DuelsPlayed && e.Kind == RunEventKind.CardSealed)
                .Select(e => _text.CardName(e.Code)).ToList();
            if (sealedNames.Count > 0)
                panel.Add(Text("이번 패배로 봉인된 카드: " + string.Join(", ", sealedNames), 15, UiStyle.Opponent, top: 6));

            var body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 12;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var grid = Grid(scroll.contentContainer);
            foreach (uint candidate in Sorted(candidates))
            {
                uint code = candidate;
                grid.Add(CardSlot(Pile.Refill, code, CardFaceSize.Medium, count: _run.SideDeck.Count(c => c == code),
                    onDoubleClick: () =>
                    {
                        _refillSelected = code;
                        ConfirmRefill();
                    }, draggable: false));
            }
            body.Add(scroll);

            var detail = UiStyle.PanelBox("refill-detail");
            detail.style.width = 380;
            detail.style.marginLeft = 12;
            detail.style.backgroundColor = UiStyle.PanelLight;
            body.Add(detail);
            panel.Add(body);

            _detail = detail;
            _renderDetail = RenderRefillDetail;
            ShowDetail(Pile.Refill, _refillSelected, pinned: true);
        }

        void RenderRefillDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
            {
                panel.Add(Text("왼쪽에서 넣을 카드를 고르세요.", 17, UiStyle.TextDim));
                return;
            }
            AddCardDetail(panel, code, pinned);
            panel.Add(Text($"사이드 덱 {_run.SideDeck.Count(c => c == code)}장 · 덱 {_run.DeckCopies(code)}장", 14, UiStyle.TextDim, top: 6));
            if (!pinned)
                return;
            var confirm = UiStyle.Button("메인 덱에 넣기", ConfirmRefill, 20);
            confirm.name = "refill-confirm";
            confirm.style.marginTop = 10;
            panel.Add(confirm);
        }

        void ConfirmRefill()
        {
            if (_refillSelected == 0)
                return;
            try
            {
                _run.MoveToDeck(_refillSelected);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Persist();
            ShowHub(); // 아직 모자라면 보충 창을 다시 연다
        }
    }
}
