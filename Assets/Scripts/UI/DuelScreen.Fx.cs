// 듀얼 화면의 연출. docs/UI.md "연출".
// 엔진 메시지를 한 단계씩 보여 줄 때(DuelScreen.cs의 "한 단계씩 보여 주기"), 그 단계가 멈춰 있는 동안 연출을 틀어 준다.
// 상태는 메시지를 받은 시점에 이미 바뀌어 있다 (카드는 벌써 도착한 자리에 있다). 그래서 카드가 날아가는 동안에는
// 도착한 칸의 카드를 숨기고, 연출 층(fx-layer) 위에서 카드 그림 한 장(ghost)을 출발한 자리에서 도착한 자리로 옮긴다.
// 시간은 속도 "보통" 기준이고 속도 설정(SpeedFactor)에 따라 함께 줄거나 는다. 건너뛰면 연출을 모두 지우고 결과만 남긴다.
// "자동으로 끝까지"(개발용)에서는 연출을 틀지 않는다.
// 조각 연출: 카드 그림을 overflow: hidden 상자 여러 개에 나눠 담고 상자마다 다른 방향으로 옮기고 돌리며 투명하게 만든다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class DuelScreen
    {
        const float MoveSeconds = 0.45f;
        const float ChainMiniWidth = 81; // 체인 상자의 카드: 108의 75%
        static readonly Vector2 Showcase = new Vector2(FieldCenterX, 470);
        static readonly Color SpecialRing = UiStyle.Hex(0xa6ecff);
        static readonly Color BanishRing = UiStyle.Hex(0xb9a0f2);
        static readonly Color ArrowColor = UiStyle.Hex(0xff5a5a);

        sealed class Fx
        {
            public float Start, End;
            public Action<float> Step;
            public Action Done;
            public bool Linear, Finished;
        }

        readonly List<Fx> _fx = new List<Fx>();
        /// <summary>카드가 날아오는 동안 카드를 숨겨 둘 칸과 그것이 풀리는 때.</summary>
        readonly Dictionary<CardKey, float> _hiddenUntil = new Dictionary<CardKey, float>();
        /// <summary>LP 막대가 줄어드는 연출 중 (그동안은 Render가 막대 길이를 건드리지 않는다).</summary>
        readonly bool[] _lpBarBusy = new bool[2];
        VisualElement _fxLayer, _fieldLayer, _banner, _chainCenter, _chainRow;
        Label _bannerText;
        readonly List<VisualElement> _chainMinis = new List<VisualElement>();
        /// <summary>가운데 띠가 체인 상자의 맨 위 줄로 올라가 있는지 (글자를 작게 쓴다).</summary>
        bool _promptCompact;

        // ================= 움직임 도구 =================

        /// <summary>delay 뒤에 seconds 동안 step(0→1)을 부른다. 끝나거나 지워질 때 done을 한 번 부른다. 시간은 속도 설정을 따른다.</summary>
        void Play(float delay, float seconds, Action<float> step, Action done = null, bool linear = false)
        {
            float k = SpeedFactor, now = Time.realtimeSinceStartup;
            _fx.Add(new Fx { Start = now + delay * k, End = now + (delay + seconds) * k, Step = step, Done = done, Linear = linear });
        }

        /// <summary>빠르게 출발해 부드럽게 멈춘다.</summary>
        static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        void UpdateFx()
        {
            float now = Time.realtimeSinceStartup;
            // 연출이 도는 중에 새 연출이 더해질 수 있어서 개수를 미리 잡아 둔다
            for (int i = 0, n = _fx.Count; i < n; i++)
            {
                var fx = _fx[i];
                if (fx.Finished || now < fx.Start)
                    continue;
                float t = fx.End <= fx.Start ? 1f : Mathf.Clamp01((now - fx.Start) / (fx.End - fx.Start));
                fx.Step?.Invoke(fx.Linear ? t : EaseOut(t));
                if (t >= 1f)
                {
                    fx.Finished = true;
                    fx.Done?.Invoke();
                }
            }
            _fx.RemoveAll(f => f.Finished);
            if (_hiddenUntil.Count == 0)
                return;
            var shown = _hiddenUntil.Where(h => now >= h.Value).Select(h => h.Key).ToList();
            foreach (var key in shown)
                _hiddenUntil.Remove(key);
            if (shown.Count > 0 && _session != null)
                Render(); // 숨겨 둔 카드를 다시 보인다
        }

        /// <summary>틀고 있던 연출을 모두 지우고 결과만 남긴다 (건너뛰기, 듀얼을 새로 시작할 때, 화면을 다시 지을 때).</summary>
        void ClearFx(bool render = true)
        {
            var running = _fx.ToList();
            _fx.Clear();
            foreach (var fx in running)
                if (!fx.Finished)
                    fx.Done?.Invoke();
            _fx.Clear();
            _fxLayer?.Clear();
            bool hidden = _hiddenUntil.Count > 0;
            _hiddenUntil.Clear();
            _lpBarBusy[0] = _lpBarBusy[1] = false;
            if (_fieldLayer != null)
                _fieldLayer.style.translate = StyleKeyword.Null;
            if (_banner != null)
                _banner.style.display = DisplayStyle.None;
            if (render && hidden && _session != null)
                Render();
        }

        /// <summary>연출 층 위의 카드 그림 한 장 (앞면 또는 뒷면). 가운데 기준으로 놓는다 (Pose).</summary>
        VisualElement Ghost(uint code, bool face)
        {
            var ghost = face && code != 0 ? _faces.Build(code, CardFaces.BaseWidth) : _faces.BuildBack(CardFaces.BaseWidth);
            ghost.pickingMode = PickingMode.Ignore;
            ghost.style.position = Position.Absolute;
            _fxLayer.Add(ghost);
            return ghost;
        }

        static void Pose(VisualElement e, Vector2 center, float scale, float angle = 0f, float opacity = 1f, float scaleX = 1f)
        {
            e.style.left = center.x - CardFaces.BaseWidth / 2;
            e.style.top = center.y - CardFaces.BaseHeight / 2;
            e.style.scale = new Scale(new Vector3(scale * scaleX, scale, 1f));
            e.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
            e.style.opacity = opacity;
        }

        static void Remove(VisualElement e) => e?.RemoveFromHierarchy();

        /// <summary>
        /// 그 자리의 가운데(판 좌표)와 거기 놓인 카드의 배율. 화면에 자리가 없는 곳이면 false.
        /// left: 카드가 방금 그 패를 떠났다 (패의 장수를 한 장 더 많게 보고 자리를 구한다).
        /// </summary>
        bool FxPoint(CardLocation loc, bool left, out Vector2 point, out float scale)
        {
            point = default;
            scale = 1f;
            byte p = loc.Controller;
            if (p > 1 || loc.IsOverlay || _session == null)
                return false;
            bool mine = p == Me;
            // 양옆 칸의 카드는 이름표 반대쪽에 붙어 있다 (내 쪽은 위, 상대 쪽은 아래)
            var side = new Vector2(Tile / 2, mine ? 70 : Tile - 70);
            switch (loc.Location)
            {
                case OcgLocation.Hand:
                {
                    int n = _session.State.Players[p].Hand.Count + (left ? 1 : 0);
                    float offset = Mathf.Clamp((int)loc.Sequence, 0, Mathf.Max(0, n - 1)) - (n - 1) / 2f;
                    if (mine)
                    {
                        point = new Vector2(FieldCenterX + offset * Mathf.Min(164f, 1000f / Mathf.Max(1, n)), HandY + offset * offset * 3f);
                        scale = HandScale;
                    }
                    else
                        point = new Vector2(FieldCenterX - offset * Mathf.Min(120f, 720f / Mathf.Max(1, n)), OppHandY);
                    return true;
                }
                case OcgLocation.MonsterZone:
                    if (loc.Sequence >= MainZones)
                        return false;
                    point = ZonePosition(p, 1 + (int)loc.Sequence, MyMonsterY) + TileSize / 2;
                    return true;
                case OcgLocation.SpellTrapZone:
                    if (loc.Sequence < MainZones)
                    {
                        point = ZonePosition(p, 1 + (int)loc.Sequence, MySpellY) + TileSize / 2;
                        return true;
                    }
                    point = ZonePosition(p, 0, MyMonsterY) + side;
                    scale = SideCardWidth / CardFaces.BaseWidth;
                    return true;
                case OcgLocation.Deck:
                    point = ZonePosition(p, 6, MySpellY) + side;
                    scale = SideCardWidth / CardFaces.BaseWidth;
                    return true;
                case OcgLocation.Extra:
                    point = ZonePosition(p, 0, MySpellY) + side;
                    scale = SideCardWidth / CardFaces.BaseWidth;
                    return true;
                case OcgLocation.Grave:
                    point = ZonePosition(p, 6, MyMonsterY) + side;
                    scale = SideCardWidth / CardFaces.BaseWidth;
                    return true;
                case OcgLocation.Removed:
                    point = new Vector2(ColumnX(mine ? 6 : 0) + Tile / 2, FieldCenterY);
                    scale = 0.3f;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>그 자리에 있을 때 내가 카드의 앞면을 볼 수 있는지.</summary>
        bool FaceVisibleAt(uint code, CardLocation loc)
        {
            if (code == 0)
                return false;
            bool faceUp = (loc.Position & OcgPosition.FaceDown) == 0;
            switch (loc.Location)
            {
                case OcgLocation.Hand: return loc.Controller == Me;
                case OcgLocation.MonsterZone:
                case OcgLocation.SpellTrapZone: return faceUp || loc.Controller == Me;
                case OcgLocation.Grave: return true;
                case OcgLocation.Removed: return faceUp || loc.Controller == Me;
                default: return false; // 덱, 엑스트라 덱
            }
        }

        CardCell CellAt(CardLocation loc)
        {
            byte p = loc.Controller;
            if (p > 1)
                return null;
            switch (loc.Location)
            {
                case OcgLocation.MonsterZone: return loc.Sequence < MainZones ? _monsters[p, loc.Sequence] : null;
                case OcgLocation.SpellTrapZone: return loc.Sequence < MainZones ? _spells[p, loc.Sequence] : loc.Sequence == 5 ? _fieldZone[p] : null;
                case OcgLocation.Hand: return loc.Sequence < _handCells[p].Count ? _handCells[p][(int)loc.Sequence] : null;
                default: return null;
            }
        }

        /// <summary>카드가 날아오는 동안 그 칸의 카드를 숨긴다 (seconds는 속도 "보통" 기준).</summary>
        void HideUntilArrival(CardLocation loc, float seconds)
        {
            if (loc.Controller > 1 || !(loc.IsOnField || loc.Location == OcgLocation.Hand))
                return;
            _hiddenUntil[new CardKey(loc)] = Time.realtimeSinceStartup + seconds * SpeedFactor;
        }

        bool IsHidden(CardInfo card) => card != null && _hiddenUntil.Count > 0 && _hiddenUntil.ContainsKey(new CardKey(card.Location));

        // ================= 화면 구성 =================

        VisualElement BuildFxLayer()
        {
            _fxLayer = new VisualElement { name = "fx-layer", pickingMode = PickingMode.Ignore };
            UiStyle.Place(_fxLayer, 0, 0, StageWidth, StageHeight);
            return _fxLayer;
        }

        /// <summary>띠 알림: 필드를 가로지르는 띠 (턴 넘김, 승리·패배). 누를 필요가 없다.</summary>
        VisualElement BuildBanner()
        {
            _banner = UiStyle.Box("duel-banner", FlexDirection.Row);
            _banner.pickingMode = PickingMode.Ignore;
            UiStyle.Place(_banner, 340, FieldCenterY - 58, 1240, 116);
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
            return _banner;
        }

        /// <summary>체인 상자: 체인이 있는 동안 필드 한가운데에 뜬다. 맨 위 줄에는 가운데 띠의 글이 올라오고, 그 아래에 체인에 오른 카드가 번호를 달고 왼쪽부터 쌓인다.</summary>
        VisualElement BuildChainBox()
        {
            _chainCenter = new VisualElement { name = "chain-box", pickingMode = PickingMode.Ignore };
            UiStyle.Place(_chainCenter, 760, 372, 400, 196);
            _chainCenter.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.88f);
            UiStyle.BorderColor(_chainCenter, UiStyle.GoldDeep, 3);
            _chainCenter.style.display = DisplayStyle.None;
            _chainRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _chainRow.style.position = Position.Absolute;
            _chainRow.style.left = _chainRow.style.right = 0;
            _chainRow.style.top = 46;
            _chainRow.style.flexDirection = FlexDirection.Row;
            _chainRow.style.justifyContent = Justify.Center;
            _chainCenter.Add(_chainRow);
            return _chainCenter;
        }

        /// <summary>
        /// 체인 상자를 지금 체인대로 다시 그린다. 처리 중인 카드는 조금 크게, 처리가 끝났거나 무효가 된 카드는 흐리게.
        /// 체인이 있으면 가운데 띠를 상자의 맨 위 줄로 올린다 (카드와 겹치지 않게).
        /// </summary>
        void RenderChainBox(DuelState state)
        {
            if (_chainCenter == null)
                return;
            int n = state.Chain.Count;
            bool on = n > 0;
            _chainCenter.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _promptCompact = on;
            var strip = _promptLabel.parent;
            strip.style.top = on ? 375 : BandY;
            strip.style.height = on ? 38 : BandHeight;
            _chainRow.Clear();
            _chainMinis.Clear();
            if (!on)
                return;
            // 네 장까지는 간격을 두고, 넘으면 겹쳐 놓는다
            float pitch = Mathf.Min(ChainMiniWidth + 10, (400 - 30 - ChainMiniWidth) / Mathf.Max(1, n - 1));
            for (int i = 0; i < n; i++)
            {
                var link = state.Chain[i];
                int number = i + 1;
                string status = ChainStatusOf(number);
                bool done = status != "대기" && status != "처리 중";
                var mini = new VisualElement { name = $"chain-card-{number}", pickingMode = PickingMode.Ignore };
                mini.style.width = ChainMiniWidth;
                mini.style.height = ChainMiniWidth * CardFaces.BaseHeight / CardFaces.BaseWidth;
                mini.style.flexShrink = 0;
                mini.style.marginLeft = i == 0 ? 0 : pitch - ChainMiniWidth;
                mini.style.opacity = done ? 0.4f : 1f;
                if (status == "처리 중")
                    mini.style.scale = new Scale(new Vector3(1.12f, 1.12f, 1f));
                var face = link.Code != 0 ? _faces.Build(link.Code, ChainMiniWidth) : _faces.BuildBack(ChainMiniWidth);
                face.pickingMode = PickingMode.Ignore;
                mini.Add(face);
                var circle = UiStyle.Label(number.ToString(), 19, UiStyle.Hex(0x22180a), bold: true);
                circle.pickingMode = PickingMode.Ignore;
                UiStyle.Place(circle, -8, -10, 36, 36);
                circle.style.unityTextAlign = TextAnchor.MiddleCenter;
                circle.style.backgroundColor = link.Location.Controller == Opp ? FocusActor : UiStyle.Gold;
                UiStyle.Radius(circle, 18);
                UiStyle.BorderColor(circle, UiStyle.Stone9, 2);
                mini.Add(circle);
                _chainRow.Add(mini);
                _chainMinis.Add(mini);
            }
        }

        // ================= 메시지마다 =================

        /// <summary>이 메시지의 연출을 튼다 (한 단계씩 보여 주는 중이고 건너뛰는 중이 아닐 때만 부른다).</summary>
        void BuildFx(DuelMessage message)
        {
            if (_fxLayer == null || _session == null)
                return;
            switch (message)
            {
                case DrawMessage m: FxDraw(m); break;
                case MoveMessage m: FxMove(m); break;
                case SummoningMessage m: FxSummon(m); break;
                case ChainingMessage m: FxActivate(m); break;
                case CardListMessage m when m.Type == OcgMessageType.BecomeTarget: FxTargets(m); break;
                case AttackMessage m: FxAttack(m); break;
                case LpChangeMessage m: FxLp(m); break;
                case PositionChangeMessage m: FxPosition(m); break;
                case NewTurnMessage m:
                    ShowBanner($"{_session.State.Turn}턴 · {_text.Player(m.Player)}의 턴", m.Player == Me ? UiStyle.Mine : UiStyle.Opponent, 0.45f);
                    break;
                case WinMessage m:
                    ShowBanner(m.Winner == OcgConstants.PlayerNone ? "[ 무승부. ]" : m.Winner == Me ? "[ 승리. ]" : "[ 패배. ]", UiStyle.Gold, 1.2f);
                    break;
                case ChainEventMessage m when m.Type == OcgMessageType.ChainNegated || m.Type == OcgMessageType.ChainDisabled:
                    FxChainNegated(m.ChainCount);
                    break;
            }
        }

        /// <summary>드로우: 덱에서 패로 한 장씩 날아온다.</summary>
        void FxDraw(DrawMessage m)
        {
            byte p = m.Player;
            if (p > 1)
                return;
            int hand = _session.State.Players[p].Hand.Count, count = m.Codes.Count;
            for (int i = 0; i < count; i++)
            {
                var to = new CardLocation(p, OcgLocation.Hand, (uint)Mathf.Max(0, hand - count + i));
                uint code = m.Codes[i];
                float delay = i * 0.08f;
                if (!FxPoint(new CardLocation(p, OcgLocation.Deck, 0), false, out var a, out float sa) || !FxPoint(to, false, out var b, out float sb))
                    continue;
                HideUntilArrival(to, delay + MoveSeconds);
                var ghost = Ghost(code, p == Me);
                Pose(ghost, a, sa);
                Play(delay, MoveSeconds, t => Pose(ghost, Vector2.Lerp(a, b, t), Mathf.Lerp(sa, sb, t)), () => Remove(ghost));
            }
        }

        /// <summary>
        /// 카드 이동: 파괴(번쩍이고 조각난다), 제외(돌면서 제외 칸으로 빨려 든다), 버리기(기울어 날아가며 어두워진다),
        /// 그 밖(소환·세트·패로 되돌리기·컨트롤 변경 등)은 출발한 자리에서 도착한 자리로 날아간다.
        /// </summary>
        void FxMove(MoveMessage m)
        {
            var from = m.From;
            var to = m.To;
            bool hasFrom = FxPoint(from, true, out var a, out float sa);
            bool hasTo = FxPoint(to, false, out var b, out float sb);
            if (!hasFrom && !hasTo)
                return;
            if (!hasFrom)
            {
                // 새로 생긴 카드 (토큰 등): 도착한 자리에서 나타난다
                a = b;
                sa = sb * 0.6f;
            }
            if (!hasTo)
            {
                b = a;
                sb = sa * 0.6f;
            }
            bool faceFrom = FaceVisibleAt(m.Code, from), faceTo = FaceVisibleAt(m.Code, to);
            bool fromField = from.IsOnField, toField = to.IsOnField;

            if (to.Location == OcgLocation.Grave && fromField && (m.Reason & OcgReason.Destroy) != 0)
            {
                FxDestroy(m.Code, faceFrom || faceTo, a, sa);
                return;
            }
            if (to.Location == OcgLocation.Removed)
            {
                FxBanish(m.Code, faceFrom || faceTo, a, sa, b);
                return;
            }

            var ghost = Ghost(m.Code, faceTo || (faceFrom && !toField && to.Location != OcgLocation.Hand));
            Pose(ghost, a, sa);
            HideUntilArrival(to, MoveSeconds);
            bool discard = to.Location == OcgLocation.Grave && from.Location == OcgLocation.Hand;
            bool toPile = !toField && to.Location != OcgLocation.Hand;
            // 묘지 등에서 필드로 나오는 카드(특수 소환)는 떠오르며 커졌다가 내려앉는다
            bool rise = toField && !fromField && from.Location != OcgLocation.Hand;
            bool defense = to.Location == OcgLocation.MonsterZone && (to.Position & OcgPosition.Defense) != 0;
            bool control = fromField && toField && from.Controller != to.Controller;
            if (control)
                Ring(a, BanishRing, round: false, delay: 0f);
            Play(0f, rise ? MoveSeconds + 0.1f : MoveSeconds, t =>
            {
                float grow = rise ? Mathf.Sin(t * Mathf.PI) * 0.35f : 0f;
                Pose(ghost, Vector2.Lerp(a, b, t), Mathf.Lerp(sa, sb, t) + grow,
                    angle: discard ? t * 24f : defense ? t * 90f : 0f,
                    opacity: toPile ? 1f - t * (discard ? 0.6f : 0.35f) : 1f);
            }, () => Remove(ghost));
            if (control)
                Ring(b, BanishRing, round: false, delay: MoveSeconds);
        }

        /// <summary>파괴: 하얗게 번쩍이고 (0.24초) 조각으로 갈라져 흩어진다 (0.8초). 흰 빛이 터지고 화면이 짧게 흔들린다 (0.32초).</summary>
        void FxDestroy(uint code, bool face, Vector2 at, float scale)
        {
            var ghost = Ghost(code, face);
            Pose(ghost, at, scale);
            var white = new VisualElement { pickingMode = PickingMode.Ignore };
            white.style.position = Position.Absolute;
            white.style.left = white.style.top = white.style.right = white.style.bottom = 0;
            white.style.backgroundColor = Color.white;
            white.style.opacity = 0;
            ghost.Add(white);
            Play(0f, 0.24f, t => white.style.opacity = t * 0.9f, () => Remove(ghost), linear: true);
            Shatter(code, face, at, scale, 0.24f);
            Flash(at, 0.24f);
            Shake(0.24f);
        }

        /// <summary>카드를 4×3 조각으로 갈라 흩뜨린다.</summary>
        void Shatter(uint code, bool face, Vector2 at, float scale, float delay)
        {
            const int columns = 4, rows = 3;
            float w = CardFaces.BaseWidth / columns, h = CardFaces.BaseHeight / rows;
            var random = new System.Random((int)code + 7);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var piece = new VisualElement { pickingMode = PickingMode.Ignore };
                    piece.style.position = Position.Absolute;
                    piece.style.width = w;
                    piece.style.height = h;
                    piece.style.overflow = Overflow.Hidden;
                    piece.style.display = DisplayStyle.None;
                    var picture = face && code != 0 ? _faces.Build(code, CardFaces.BaseWidth) : _faces.BuildBack(CardFaces.BaseWidth);
                    picture.pickingMode = PickingMode.Ignore;
                    picture.style.position = Position.Absolute;
                    picture.style.left = -c * w;
                    picture.style.top = -r * h;
                    piece.Add(picture);
                    _fxLayer.Add(piece);
                    // 조각의 제자리(카드 가운데에서 본 자리)와 흩어질 방향
                    var home = new Vector2((c + 0.5f) * w - CardFaces.BaseWidth / 2, (r + 0.5f) * h - CardFaces.BaseHeight / 2) * scale;
                    var fly = (home.sqrMagnitude > 1f ? home.normalized : Vector2.up) * (70f + (float)random.NextDouble() * 90f)
                              + new Vector2((float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 40f - 20f);
                    float spin = (float)random.NextDouble() * 240f - 120f;
                    Play(delay, 0.8f, t =>
                    {
                        piece.style.display = DisplayStyle.Flex;
                        var center = at + home + fly * t + new Vector2(0, 70f * t * t);
                        piece.style.left = center.x - w / 2;
                        piece.style.top = center.y - h / 2;
                        piece.style.scale = new Scale(new Vector3(scale, scale, 1f));
                        piece.style.rotate = new StyleRotate(new Rotate(new Angle(spin * t, AngleUnit.Degree)));
                        piece.style.opacity = 1f - t;
                    }, () => Remove(piece));
                }
            }
        }

        /// <summary>흰 빛이 터진다.</summary>
        void Flash(Vector2 at, float delay)
        {
            var light = new VisualElement { pickingMode = PickingMode.Ignore };
            light.style.position = Position.Absolute;
            light.style.width = light.style.height = 220;
            light.style.left = at.x - 110;
            light.style.top = at.y - 110;
            light.style.backgroundImage = new StyleBackground(UiStyle.Radial);
            light.style.display = DisplayStyle.None;
            _fxLayer.Add(light);
            Play(delay, 0.4f, t =>
            {
                light.style.display = DisplayStyle.Flex;
                float s = 0.5f + t * 1.3f;
                light.style.scale = new Scale(new Vector3(s, s, 1f));
                light.style.opacity = (1f - t) * 0.8f;
            }, () => Remove(light));
        }

        /// <summary>화면이 짧게 흔들린다.</summary>
        void Shake(float delay)
        {
            if (_fieldLayer == null)
                return;
            var random = new System.Random(3);
            Play(delay, 0.32f, t =>
            {
                float power = (1f - t) * 9f;
                _fieldLayer.style.translate = new Translate(((float)random.NextDouble() * 2f - 1f) * power, ((float)random.NextDouble() * 2f - 1f) * power);
            }, () => _fieldLayer.style.translate = StyleKeyword.Null, linear: true);
        }

        /// <summary>제외: 색이 빠지고 (0.26초) 돌면서 작아져 제외 칸으로 빨려 들어간다 (0.48초). 제외 칸에 보라색 원이 퍼진다.</summary>
        void FxBanish(uint code, bool face, Vector2 from, float scale, Vector2 circle)
        {
            var ghost = Ghost(code, face);
            Pose(ghost, from, scale);
            // 색을 뺄 수는 없어서 회색 막을 덮어 바랜 것처럼 보이게 한다
            var gray = new VisualElement { pickingMode = PickingMode.Ignore };
            gray.style.position = Position.Absolute;
            gray.style.left = gray.style.top = gray.style.right = gray.style.bottom = 0;
            gray.style.backgroundColor = UiStyle.Hex(0x8a8791);
            gray.style.opacity = 0;
            ghost.Add(gray);
            Play(0f, 0.26f, t => gray.style.opacity = t * 0.6f, linear: true);
            Play(0.26f, 0.48f, t => Pose(ghost, Vector2.Lerp(from, circle, t), Mathf.Lerp(scale, 0.12f, t), angle: t * 300f, opacity: 1f - t * 0.5f),
                () => Remove(ghost), linear: true);
            Ring(circle, BanishRing, round: true, delay: 0.7f, size: 56);
        }

        /// <summary>테가 퍼진다: 네모(존 크기)나 원이 커지며 사라진다 (0.6초).</summary>
        void Ring(Vector2 at, Color color, bool round, float delay, float size = Tile)
        {
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.style.position = Position.Absolute;
            ring.style.width = ring.style.height = size;
            ring.style.left = at.x - size / 2;
            ring.style.top = at.y - size / 2;
            UiStyle.BorderColor(ring, color, 5);
            UiStyle.Radius(ring, round ? size / 2 : 8);
            ring.style.display = DisplayStyle.None;
            _fxLayer.Add(ring);
            Play(delay, 0.6f, t =>
            {
                ring.style.display = DisplayStyle.Flex;
                float s = 1f + t * (round ? 1.6f : 0.45f);
                ring.style.scale = new Scale(new Vector3(s, s, 1f));
                ring.style.opacity = 1f - t;
            }, () => Remove(ring));
        }

        /// <summary>소환: 놓인 자리에 금색 네모 테가 퍼진다. 특수 소환은 하늘색 테가 두 번 퍼진다. 반전 소환은 카드가 접혔다가 펴진다.</summary>
        void FxSummon(SummoningMessage m)
        {
            if (!FxPoint(m.Location, false, out var at, out _))
                return;
            if (m.Kind == SummonKind.Special)
            {
                Ring(at, SpecialRing, round: false, delay: 0f);
                Ring(at, SpecialRing, round: false, delay: 0.2f);
                return;
            }
            Ring(at, UiStyle.Gold, round: false, delay: 0f);
            if (m.Kind == SummonKind.Flip)
                Unfold(CellAt(m.Location));
        }

        /// <summary>반전: 가로 폭이 0으로 접혔다가 앞면으로 펴진다.</summary>
        void Unfold(CardCell cell)
        {
            var slot = cell?.Slot;
            if (slot == null)
                return;
            Play(0f, 0.36f, t => slot.style.scale = new Scale(new Vector3(Mathf.Abs(Mathf.Cos(t * Mathf.PI)), 1f, 1f)),
                () => slot.style.scale = StyleKeyword.Null, linear: true);
        }

        /// <summary>
        /// 발동: 발동한 카드가 화면 가운데에 크게 떴다가 제자리로 돌아간다. 세트된 카드는 뒤집히며 떠오른다.
        /// 체인 상자에 새로 오른 카드는 작게 나타났다가 제 크기가 된다.
        /// </summary>
        void FxActivate(ChainingMessage m)
        {
            int index = m.ChainCount - 1;
            Play(0f, 0.3f, t =>
            {
                if (index >= 0 && index < _chainMinis.Count)
                    _chainMinis[index].style.scale = new Scale(new Vector3(0.55f + 0.45f * t, 0.55f + 0.45f * t, 1f));
            }, () =>
            {
                if (index >= 0 && index < _chainMinis.Count)
                    _chainMinis[index].style.scale = StyleKeyword.Null;
            });
            if (m.Code == 0 || !FxPoint(m.Location, false, out var home, out float homeScale))
                return;
            bool spellZone = m.Location.Location == OcgLocation.SpellTrapZone;
            float big = spellZone ? 2.4f : 2.0f;
            var ghost = Ghost(m.Code, true);
            Pose(ghost, home, homeScale, scaleX: 0.1f);
            // 떠오르기 (뒤집히며) → 머물기 → 제자리로
            Play(0f, 0.26f, t => Pose(ghost, Vector2.Lerp(home, Showcase, t), Mathf.Lerp(homeScale, big, t), scaleX: Mathf.Lerp(0.1f, 1f, t)));
            Play(0.7f, 0.28f, t => Pose(ghost, Vector2.Lerp(Showcase, home, t), Mathf.Lerp(big, homeScale, t), opacity: 1f - t * t), () => Remove(ghost));
        }

        /// <summary>대상 지정: 빨간 화살이 발동한 카드에서 대상까지 뻗는다.</summary>
        void FxTargets(CardListMessage m)
        {
            if (!_lastChaining.HasValue || !FxPoint(_lastChaining.Value, false, out var from, out _))
                return;
            foreach (var target in m.Cards)
                if (FxPoint(target, false, out var to, out _) && (to - from).sqrMagnitude > 400f)
                    Arrow(from, to, 0f, 0.5f, 0.65f);
        }

        /// <summary>화살: grow 동안 뻗고, total이 되면 사라진다.</summary>
        void Arrow(Vector2 from, Vector2 to, float delay, float grow, float total)
        {
            var arrow = new ArrowView(from, to, ArrowColor);
            UiStyle.Place(arrow, 0, 0, StageWidth, StageHeight);
            arrow.style.display = DisplayStyle.None;
            _fxLayer.Add(arrow);
            Play(delay, total, t =>
            {
                arrow.style.display = DisplayStyle.Flex;
                float at = t * total;
                arrow.Reach = Mathf.Clamp01(at / grow);
                arrow.style.opacity = at > total - 0.12f ? Mathf.Clamp01((total - at) / 0.12f) : 1f;
            }, () => Remove(arrow), linear: true);
        }

        /// <summary>공격: 화살로 대상을 가리킨 뒤, 공격하는 카드가 대상 쪽으로 돌진했다가 돌아온다. 직접 공격은 상대 쪽 끝으로 뻗고 번쩍인다.</summary>
        void FxAttack(AttackMessage m)
        {
            if (!FxPoint(m.Attacker, false, out var from, out _))
                return;
            Vector2 to;
            if (m.IsDirect)
                to = new Vector2(from.x, m.Attacker.Controller == Me ? 30 : 880);
            else if (!FxPoint(m.Target, false, out to, out _))
                return;
            Arrow(from, to, 0f, 0.3f, 0.75f);
            var root = CellAt(m.Attacker)?.Root;
            if (root != null)
            {
                var lunge = (to - from) * 0.36f;
                Play(0.32f, 0.26f, t =>
                {
                    float there = Mathf.Sin(t * Mathf.PI);
                    root.style.translate = new Translate(lunge.x * there, lunge.y * there);
                }, () => root.style.translate = StyleKeyword.Null, linear: true);
            }
            if (m.IsDirect)
                Flash(to, 0.45f);
        }

        /// <summary>데미지·회복: LP 숫자 옆에 바뀐 양이 크게 떠오르고 (1.3초) LP 막대가 새 길이로 미끄러진다 (0.5초).</summary>
        void FxLp(LpChangeMessage m)
        {
            byte p = m.Player;
            if (p > 1 || m.Kind == LpChangeKind.Set)
                return;
            int delta = m.Kind == LpChangeKind.Recover ? (int)m.Amount : -(int)m.Amount;
            if (delta == 0)
                return;
            int now = Mathf.Max(0, _session.State.Players[p].Lp);
            float to = Mathf.Clamp01(now / 8000f) * 100f, from = Mathf.Clamp01((now - delta) / 8000f) * 100f;
            _lpBarBusy[p] = true;
            _lpBar[p].style.width = Length.Percent(from);
            Play(0f, 0.5f, t => _lpBar[p].style.width = Length.Percent(Mathf.Lerp(from, to, t)), () =>
            {
                _lpBarBusy[p] = false;
                _lpBar[p].style.width = Length.Percent(to);
            });

            var number = UiStyle.Label(delta > 0 ? $"+{delta}" : $"−{-delta}", 62, delta > 0 ? UiStyle.Good : UiStyle.Bad);
            UiStyle.Black(number);
            number.pickingMode = PickingMode.Ignore;
            number.style.whiteSpace = WhiteSpace.NoWrap;
            number.style.position = Position.Absolute;
            number.style.left = SidePanelX + 150;
            float y = p == Me ? 880 : 150;
            number.style.top = y;
            _fxLayer.Add(number);
            Play(0f, 1.3f, t =>
            {
                number.style.top = y - 46f * t * (p == Me ? 1f : -1f);
                number.style.opacity = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            }, () => Remove(number));
        }

        /// <summary>표시 형식 변경: 카드가 제자리에서 90도 돈다. 뒷면에서 앞면이 되면 접혔다가 펴진다.</summary>
        void FxPosition(PositionChangeMessage m)
        {
            var cell = CellAt(m.Location);
            var slot = cell?.Slot;
            if (slot == null)
                return;
            bool wasDown = (m.Previous & OcgPosition.FaceDown) != 0, isDown = (m.Current & OcgPosition.FaceDown) != 0;
            if (wasDown != isDown)
            {
                Unfold(cell);
                return;
            }
            bool wasDefense = (m.Previous & OcgPosition.Defense) != 0, isDefense = (m.Current & OcgPosition.Defense) != 0;
            if (wasDefense == isDefense || m.Location.Location != OcgLocation.MonsterZone)
                return;
            // 카드는 벌써 새 표시 형식으로 그려져 있다: 옛 각도에서 새 각도로 돌린다
            float start = isDefense ? -90f : 90f;
            Play(0f, 0.3f, t => slot.style.rotate = new StyleRotate(new Rotate(new Angle(start * (1f - t), AngleUnit.Degree))),
                () => slot.style.rotate = StyleKeyword.Null);
        }

        /// <summary>체인에서 무효가 된 카드: 체인 상자 안에서 조각난다.</summary>
        void FxChainNegated(int number)
        {
            int index = number - 1;
            if (index < 0 || index >= _chainMinis.Count || index >= _session.State.Chain.Count)
                return;
            var mini = _chainMinis[index];
            if (mini.panel == null)
                return;
            var at = _fxLayer.WorldToLocal(mini.worldBound.center);
            Shatter(_session.State.Chain[index].Code, true, at, ChainMiniWidth / CardFaces.BaseWidth, 0f);
        }

        /// <summary>띠 알림을 띄운다: 0.16초에 펴지고 hold 동안 머문 뒤 0.2초에 사라진다.</summary>
        void ShowBanner(string text, Color color, float hold)
        {
            if (_banner == null)
                return;
            _bannerText.text = text;
            _bannerText.style.color = color;
            Play(0f, 0.16f, t =>
            {
                _banner.style.display = DisplayStyle.Flex;
                _banner.style.opacity = t;
                _banner.style.scale = new Scale(new Vector3(1f, 0.2f + 0.8f * t, 1f));
            }, linear: true);
            Play(0.16f + hold, 0.2f, t => _banner.style.opacity = 1f - t, () => _banner.style.display = DisplayStyle.None, linear: true);
        }

        /// <summary>화살 그림: from에서 to 쪽으로 Reach(0~1)만큼 뻗은 굵은 선과 화살촉.</summary>
        sealed class ArrowView : VisualElement
        {
            readonly Vector2 _from, _to;
            readonly Color _color;
            float _reach;

            public ArrowView(Vector2 from, Vector2 to, Color color)
            {
                _from = from;
                _to = to;
                _color = color;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public float Reach
            {
                set
                {
                    _reach = value;
                    MarkDirtyRepaint();
                }
            }

            void Draw(MeshGenerationContext context)
            {
                if (_reach <= 0.01f)
                    return;
                Vector2 direction = (_to - _from).normalized, side = new Vector2(-direction.y, direction.x);
                Vector2 tip = Vector2.Lerp(_from, _to, _reach);
                const float head = 30f, half = 17f;
                var p = context.painter2D;
                p.strokeColor = _color;
                p.lineWidth = 9f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(_from);
                p.LineTo(tip - direction * Mathf.Min(head * 0.6f, (tip - _from).magnitude));
                p.Stroke();
                p.fillColor = _color;
                p.BeginPath();
                p.MoveTo(tip);
                p.LineTo(tip - direction * head + side * half);
                p.LineTo(tip - direction * head - side * half);
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
