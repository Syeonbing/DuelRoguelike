// 듀얼 화면의 화면 구성: 필드 칸, 패, 카드 더미(덱·엑스트라 덱·묘지·제외), 왼쪽 상세 카드, 오른쪽 패널, 로그 서랍.
// 듀얼 진행과 조작(누르기·메뉴·창)은 DuelScreen.cs에 있다. 배치는 docs/UI.md "듀얼"을 따른다.
//
// 배치 (1920×1080 좌표로 짠 판 하나를 화면 크기에 맞춰 놓는다):
//   가운데: 필드. 존 164×164, 간격 10, 일곱 열 (왼쪽 끝 x 356). 가운데 다섯 열이 몬스터 존과 마법·함정 존이다.
//     내 쪽과 상대 쪽은 필드 중심 (960, 460)에 대해 점대칭이다 (칸 자리, 칸 안의 카드와 이름표 자리, 더미가 쌓이는 방향).
//     양옆 열은 내 쪽 기준으로 왼쪽 위 필드 마법, 왼쪽 아래 엑스트라 덱, 오른쪽 위 묘지, 오른쪽 아래 덱 (카드 86%, 이름과 장수).
//     제외는 양끝 열 한가운데의 원 (왼쪽 끝이 상대, 오른쪽 끝이 나). 두 필드 사이의 가운데 띠에 방금 일어난 일을 한 줄로 쓴다.
//     내 패는 화면 아래에 부채꼴로, 상대 패는 화면 위에 뒷면으로 아래쪽만 보이게 놓는다.
//   왼쪽 (x 16, 폭 324): 상대 정보 / 올려 본 카드의 상세 카드 / 내 정보.
//   오른쪽 (x 1580, 폭 324): 턴, 페이즈 여섯 줄, 버튼, (체인이 있으면 체인 목록), 방금 일어난 일, "로그 전체" 버튼.
//   로그 서랍: 오른쪽에서 나온다 (x 1280, 폭 640). 닫기 버튼은 "로그 전체" 버튼과 화면에서 같은 자리에 있다.
// 카드: 카드 앞면(CardFaces)으로 그린다. 상대의 뒷면 카드는 카드 뒷면, 나의 세트 카드는 흐린 앞면 + "세트" 표시(나만 본다).
//   수비 표시는 90도 돌린다. 필드의 공격력·수비력이 원래 값과 다르면 숫자 색(높으면 초록, 낮으면 빨강)으로 보인다.
// 상세 카드(왼쪽): 카드에 마우스를 올리면 뜨고, 누른 카드는 마우스를 떼도 남는다. 뒷면인 상대 카드는 뜨지 않는다.
using System;
using System.Collections.Generic;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class DuelScreen
    {
        const float StageWidth = 1920, StageHeight = 1080;
        const float Tile = 164, TileGap = 10, FieldLeft = 356;
        /// <summary>내 쪽 줄의 y. 상대 쪽은 필드 중심에 대해 점대칭 (상대 몬스터 258, 상대 마법·함정 84).</summary>
        const float MyMonsterY = 498, MySpellY = 672;
        const float FieldCenterX = 960, FieldCenterY = 460;
        const float BandY = 432, BandHeight = 56;
        /// <summary>양옆 칸(필드 마법, 엑스트라 덱, 묘지, 덱)의 카드 폭: 108의 86%.</summary>
        const float SideCardWidth = 93, SideLabelHeight = 20;
        const float HandY = 962, HandRaisedY = 878, HandScale = 1.39f, HandRaisedScale = 1.7f;
        const float OppHandY = -10;
        const float SidePanelX = 16, SidePanelWidth = 324, RightPanelX = 1580;
        const float DrawerX = 1280, DrawerWidth = 640, LogButtonY = 1010, LogButtonHeight = 54;
        const int RecentLogLines = 7;

        static readonly Vector2 TileSize = new Vector2(Tile, Tile);
        static readonly Color Floor = UiStyle.Hex(0x3b342c);
        static readonly Color PileBack = UiStyle.Hex(0x1d2533);
        static readonly Color PileBackEdge = UiStyle.Hex(0x4a5a78);
        static readonly Color PileFace = UiStyle.Hex(0x2a303b);
        static readonly string[] PhaseNames = { "드로우", "스탠바이", "메인 1", "배틀", "메인 2", "엔드" };

        VisualElement _stage, _detailFace, _logDrawer, _recentLog, _smallButtons;
        bool _stageFitHooked;
        Label _detailCaption, _turnSide;
        readonly Label[] _phaseLabels = new Label[6];
        readonly VisualElement[] _lpBar = new VisualElement[2];
        readonly List<(string Line, Color Color)> _recentLines = new List<(string, Color)>();
        CardInfo _pinnedCard;
        bool _pinnedVisible;
        int _hoveredHand = -1;
        bool _drawerOpen;
        /// <summary>로그 서랍이 오른쪽으로 밀려 있는 거리 (0 = 다 열림, 서랍 폭 = 닫힘).</summary>
        float _drawerShift = DrawerWidth;

        static float ColumnX(int column) => FieldLeft + column * (Tile + TileGap);

        /// <summary>칸의 왼쪽 위 자리. 좌표는 내 쪽 기준이고 상대 쪽은 필드 중심에 대해 점대칭으로 옮긴다.</summary>
        Vector2 ZonePosition(byte p, int column, float myY) =>
            p == Me ? new Vector2(ColumnX(column), myY) : new Vector2(ColumnX(6 - column), FieldCenterY * 2 - myY - Tile);

        // ================= 그리기 =================

        void RenderPiles(byte p, PlayerState player)
        {
            _deck[p].Set(player.DeckCount, null, false, Touches(p, OcgLocation.Deck));
            _extra[p].Set(player.ExtraCount, null, false, Touches(p, OcgLocation.Extra));
            var grave = player.Grave.Count > 0 ? player.Grave[player.Grave.Count - 1] : null;
            _grave[p].Set(player.Grave.Count, grave, true, Touches(p, OcgLocation.Grave));
            var removed = player.Removed.Count > 0 ? player.Removed[player.Removed.Count - 1] : null;
            _removed[p].Set(player.Removed.Count, removed, removed != null && (removed.IsFaceUp || p == Me),
                Touches(p, OcgLocation.Removed));
        }

        /// <summary>패. 칸은 다시 쓰고(같은 카드면 다시 그리지 않는다), 자리는 LayHand가 정한다.</summary>
        void RenderHand(byte p, PlayerState player)
        {
            var row = _handRows[p];
            var cells = _handCells[p];
            while (cells.Count < player.Hand.Count)
            {
                var cell = new CardCell(this, CardFaceSize.Medium);
                if (p == Me)
                {
                    var mine = cell;
                    cell.Hover = on => HoverHand(cells.IndexOf(mine), on);
                }
                cells.Add(cell);
                row.Add(cell.Root);
            }
            while (cells.Count > player.Hand.Count)
            {
                cells[cells.Count - 1].Root.RemoveFromHierarchy();
                cells.RemoveAt(cells.Count - 1);
            }
            for (int i = 0; i < cells.Count; i++)
            {
                var card = player.Hand[i];
                cells[i].Root.name = $"hand-{p}-{i}";
                SetCell(cells[i], card, p == Me || card.IsPublic);
            }
            if (p == Me && _hoveredHand >= cells.Count)
                _hoveredHand = -1;
            LayHand(p);
        }

        /// <summary>
        /// 패의 카드 자리를 정한다. 내 패는 부채꼴: 가운데가 x 960, 간격은 164와 1000÷장수 중 작은 값,
        /// 가운데에서 멀수록 조금 내려가고(거리의 제곱 × 3) 조금 기운다(한 장에 2.2도). 올려 본 카드는 위로 올라오며 커진다.
        /// 상대 패는 화면 위 가장자리에 걸쳐 카드 아래쪽만 보인다.
        /// </summary>
        void LayHand(byte p)
        {
            var cells = _handCells[p];
            int n = cells.Count;
            var card = CardFaces.SizeOf(CardFaceSize.Medium);
            float spacing = p == Me ? Mathf.Min(164f, 1000f / Mathf.Max(1, n)) : Mathf.Min(120f, 720f / Mathf.Max(1, n));
            for (int i = 0; i < n; i++)
            {
                float offset = i - (n - 1) / 2f;
                var root = cells[i].Root;
                root.style.position = Position.Absolute;
                if (p != Me)
                {
                    root.style.left = FieldCenterX - offset * spacing - card.x / 2;
                    root.style.top = OppHandY - card.y / 2;
                    continue;
                }
                bool raised = i == _hoveredHand;
                root.style.left = FieldCenterX + offset * spacing - card.x / 2;
                root.style.top = (raised ? HandRaisedY : HandY + offset * offset * 3f) - card.y / 2;
                float scale = raised ? HandRaisedScale : HandScale;
                // 올라온 카드는 원래 자리의 아래쪽을 비우므로, 마우스가 거기 있어도 떨어지지 않게 누를 수 있는 자리를 아래로 늘린다
                cells[i].ExtendDown(raised ? (StageHeight - HandRaisedY) / HandRaisedScale - card.y / 2 + 4 : 0);
                root.style.scale = new Scale(new Vector3(scale, scale, 1f));
                root.style.rotate = new StyleRotate(new Rotate(new Angle(raised ? 0f : offset * 2.2f, AngleUnit.Degree)));
            }
            if (p != Me)
                return;
            // 겹치는 순서: 왼쪽부터 차례로, 올려 본 카드는 맨 앞
            for (int i = 0; i < n; i++)
                cells[i].Root.BringToFront();
            if (_hoveredHand >= 0 && _hoveredHand < n)
                cells[_hoveredHand].Root.BringToFront();
        }

        /// <summary>마우스가 패의 카드에 올라오거나 나갔다. 마우스를 움직여 올린 것이면 키보드 커서는 풀린다.</summary>
        void HoverHand(int index, bool on)
        {
            if (_cursorOn)
            {
                if (!on || !MouseMovedLately)
                    return; // 키보드로 고르는 중: 가만히 있는 마우스 아래로 카드가 지나가는 것은 무시한다
                _cursorOn = false;
            }
            int hovered = on ? index : _hoveredHand == index ? -1 : _hoveredHand;
            if (hovered == _hoveredHand)
                return;
            _hoveredHand = hovered;
            LayHand(Me);
        }

        /// <summary>창(카드 목록·고르기) 안의 카드 한 장.</summary>
        CardCell WindowCell(string name, Action refresh = null)
        {
            var cell = new CardCell(this, CardFaceSize.Medium, refresh: refresh, name: name);
            cell.Root.style.marginRight = 8;
            cell.Root.style.marginBottom = 10;
            return cell;
        }

        static CardStats StatsOf(CardInfo card) => new CardStats
        {
            Attack = card.Attack, Defense = card.Defense, BaseAttack = card.BaseAttack, BaseDefense = card.BaseDefense,
        };

        // ================= 상세 카드 =================

        /// <summary>누른 카드: 상세 카드에 남긴다. 뒷면인 상대 카드는 남기지 않는다.</summary>
        void ShowCard(CardInfo card, bool visible)
        {
            if (card == null || !visible)
                return;
            _pinnedCard = card;
            _pinnedVisible = visible;
            RenderDetail(card, visible);
        }

        /// <summary>마우스를 올린 카드: 떼면 누른 카드로 돌아간다. 뒷면인 상대 카드는 뜨지 않는다.</summary>
        void PreviewCard(CardInfo card, bool visible)
        {
            if (card == null || !visible)
                return;
            _previewing = true;
            RenderDetail(card, visible);
        }

        void EndPreview()
        {
            _previewing = false;
            if (_pinnedCard != null)
                RenderDetail(_pinnedCard, _pinnedVisible);
            else
                ClearDetail();
        }

        void ClearDetail()
        {
            _pinnedCard = null;
            if (_detailFace == null)
                return;
            _detailFace.Clear();
            var hint = UiStyle.Label("카드에 마우스를 올리면 여기에 크게 보인다.", 19, UiStyle.TextDim);
            hint.style.marginTop = 24;
            hint.style.paddingLeft = hint.style.paddingRight = 18;
            hint.style.unityTextAlign = TextAnchor.UpperCenter;
            _detailFace.Add(hint);
            _detailCaption.text = "";
        }

        void RenderDetail(CardInfo card, bool visible)
        {
            if (!visible)
                return;
            _detailFace.Clear();
            bool onField = card.Location.IsOnField;
            _detailFace.Add(_faces.BuildDetail(card.Code, SidePanelWidth, 718, onField && card.IsMonster ? StatsOf(card) : (CardStats?)null));
            // 어디에 있는 카드인지와 표시 형식 (상세 카드 아래 한 줄)
            string where = $"{_text.Player(card.Location.Controller)}의 {DuelText.Location(card.Location)}";
            _detailCaption.text = onField ? $"{where} · {DuelText.Position(card.Position)}" : where;
        }

        /// <summary>상세 카드의 효과문을 한 쪽 넘긴다 (C 또는 I). 끝이면 처음으로 돌아간다.</summary>
        void PageDetailText()
        {
            var scroll = _detailFace?.Q<ScrollView>();
            if (scroll == null)
                return;
            float page = scroll.contentViewport.layout.height, total = scroll.contentContainer.layout.height;
            if (float.IsNaN(page) || float.IsNaN(total) || total <= page + 1)
                return;
            float y = scroll.scrollOffset.y;
            scroll.scrollOffset = new Vector2(0, y + page >= total - 1 ? 0 : Mathf.Min(y + page * 0.9f, total - page));
        }

        // ================= 화면 구성 =================

        void BuildLayout()
        {
            _stage = new VisualElement { name = "duel-stage" };
            UiStyle.Place(_stage, 0, 0, StageWidth, StageHeight);
            _stage.style.overflow = Overflow.Hidden;
            _stage.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
            _stage.style.backgroundColor = Floor;
            UiStyle.GrainOn(_stage);
            _root.Add(_stage);
            if (!_stageFitHooked)
            {
                _stageFitHooked = true;
                _root.RegisterCallback<GeometryChangedEvent>(_ => FitStage());
            }
            FitStage();

            // ---- 가운데: 필드 (판 전체를 덮는 층. 여기를 누르면 보여 주기를 건너뛴다) ----
            var field = new VisualElement { name = "field" };
            UiStyle.Place(field, 0, 0, StageWidth, StageHeight);
            field.RegisterCallback<PointerDownEvent>(OnFieldPointerDown, TrickleDown.TrickleDown);
            // 듀얼 자리를 구분하는 한 단 어두운 판
            var mat = new VisualElement { name = "field-mat", pickingMode = PickingMode.Ignore };
            UiStyle.Place(mat, 338, 66, 1244, 788);
            mat.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            UiStyle.BorderColor(mat, UiStyle.Alpha(UiStyle.Stone9, 0.7f), 3);
            field.Add(mat);
            field.Add(_handRows[Opp] = HandRow(Opp));
            field.Add(FieldSide(Opp));
            field.Add(FieldSide(Me));
            field.Add(CenterStrip());
            field.Add(_handRows[Me] = HandRow(Me));
            // 키 도움말: 필드 위쪽 빈 띠 (상대 패의 양옆). 왼쪽에 이동·결정·취소, 오른쪽에 다음 카드·건너뛰기·효과문.
            _keyCaps.Clear();
            var leftHints = Hints(("← ↑ ↓ →", "W A S D", "이동"), ("Z", "J", "결정"), ("X", "K", "취소"));
            UiStyle.Place(leftHints, FieldLeft, 26);
            field.Add(leftHints);
            var rightHints = Hints(("Tab", "Tab", "다음 카드"), ("F", "F", "패↔필드"), ("C", "I", "효과문"));
            rightHints.style.position = Position.Absolute;
            rightHints.style.right = StageWidth - (ColumnX(6) + Tile);
            rightHints.style.top = 26;
            field.Add(rightHints);
            _statusBadge = BuildStatusBadge();
            field.Add(_statusBadge); // 다른 것 위에 겹쳐 그린다 (필드 오른쪽 위 모서리)
            _stage.Add(field);

            // ---- 왼쪽: 상대 정보 / 상세 카드 / 내 정보 ----
            _stage.Add(UiStyle.Place(PlayerPanel(Opp), SidePanelX, 16, SidePanelWidth, 140));
            var detail = new VisualElement { name = "card-detail", pickingMode = PickingMode.Ignore };
            UiStyle.Place(detail, SidePanelX, 164, SidePanelWidth, 752);
            _detailFace = new VisualElement { name = "detail-face" };
            _detailFace.style.height = 718;
            _detailFace.style.flexShrink = 0;
            _detailCaption = UiStyle.Label("", 17, UiStyle.TextDim);
            _detailCaption.style.whiteSpace = WhiteSpace.NoWrap;
            _detailCaption.style.overflow = Overflow.Hidden;
            _detailCaption.style.marginTop = 6;
            _detailCaption.style.unityTextAlign = TextAnchor.MiddleCenter;
            detail.Add(_detailFace);
            detail.Add(_detailCaption);
            _stage.Add(detail);
            _stage.Add(UiStyle.Place(PlayerPanel(Me), SidePanelX, 924, SidePanelWidth, 140));
            ClearDetail();

            // ---- 오른쪽: 턴, 페이즈, 버튼, 체인 목록, 방금 일어난 일, 로그 전체 ----
            _stage.Add(RightPanel());

            // ---- 로그 서랍 (닫혀 있으면 화면 오른쪽 밖) ----
            _stage.Add(LogDrawer());
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

        /// <summary>이름, LP(큰 숫자)와 바뀐 양, LP 막대, 패·덱·묘지·제외 장수.</summary>
        VisualElement PlayerPanel(byte p)
        {
            var side = p == Me ? UiStyle.Mine : UiStyle.Opponent;
            var panel = UiStyle.PanelBox(p == Me ? "my-info" : "opp-info");
            panel.style.paddingLeft = panel.style.paddingRight = 16;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            var nameLabel = _playerName[p] = UiStyle.Label(_playerNames[p], 19, side, bold: true);
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.overflow = Overflow.Hidden;
            panel.Add(nameLabel);

            var lpRow = UiStyle.Box(null, FlexDirection.Row);
            lpRow.style.alignItems = Align.FlexEnd;
            var small = UiStyle.Label("LP", 17, UiStyle.TextDim);
            small.style.marginRight = 8;
            small.style.marginBottom = 8;
            lpRow.Add(small);
            _lp[p] = UiStyle.Label("8000", 46);
            UiStyle.Black(_lp[p]);
            _lp[p].style.marginTop = -6;
            _lp[p].style.marginBottom = -4;
            lpRow.Add(_lp[p]);
            _lpDelta[p] = UiStyle.Label("", 26, FocusTarget, bold: true);
            _lpDelta[p].name = $"lp-delta-{p}";
            _lpDelta[p].style.marginLeft = 12;
            _lpDelta[p].style.marginBottom = 6;
            lpRow.Add(_lpDelta[p]);
            panel.Add(lpRow);

            var bar = new VisualElement();
            bar.style.height = 8;
            bar.style.marginTop = 3;
            bar.style.backgroundColor = UiStyle.Stone9;
            _lpBar[p] = new VisualElement();
            _lpBar[p].style.height = Length.Percent(100);
            _lpBar[p].style.width = Length.Percent(100);
            _lpBar[p].style.backgroundColor = side;
            bar.Add(_lpBar[p]);
            panel.Add(bar);

            _playerInfo[p] = UiStyle.Label("", 16, UiStyle.TextDim);
            _playerInfo[p].style.whiteSpace = WhiteSpace.NoWrap;
            _playerInfo[p].style.marginTop = 5;
            panel.Add(_playerInfo[p]);
            return panel;
        }

        /// <summary>패가 놓이는 층 (판 전체). 카드 자리는 LayHand가 정한다.</summary>
        VisualElement HandRow(byte p)
        {
            var row = new VisualElement { name = p == Me ? "my-hand" : "opp-hand", pickingMode = PickingMode.Ignore };
            UiStyle.Place(row, 0, 0, StageWidth, StageHeight);
            return row;
        }

        /// <summary>
        /// 한쪽의 필드 칸들. 좌표는 내 쪽 기준이고, 상대 쪽은 필드 중심에 대해 점대칭으로 옮긴다 (ZonePosition).
        /// </summary>
        VisualElement FieldSide(byte p)
        {
            var side = new VisualElement { name = p == Me ? "my-field" : "opp-field", pickingMode = PickingMode.Ignore };
            UiStyle.Place(side, 0, 0, StageWidth, StageHeight);
            bool flip = p != Me;

            void Put(VisualElement e, int column, float myY)
            {
                var at = ZonePosition(p, column, myY);
                UiStyle.Place(e, at.x, at.y);
                side.Add(e);
            }

            for (int i = 0; i < MainZones; i++)
            {
                _monsters[p, i] = new CardCell(this, CardFaceSize.Medium, TileSize, new CardKey(p, OcgLocation.MonsterZone, (uint)i),
                    name: $"zone-{p}-m{i}");
                Put(_monsters[p, i].Root, 1 + i, MyMonsterY);
                _spells[p, i] = new CardCell(this, CardFaceSize.Medium, TileSize, new CardKey(p, OcgLocation.SpellTrapZone, (uint)i),
                    name: $"zone-{p}-s{i}");
                Put(_spells[p, i].Root, 1 + i, MySpellY);
            }
            // 양옆 열: 왼쪽 위 필드 마법, 왼쪽 아래 엑스트라 덱, 오른쪽 위 묘지, 오른쪽 아래 덱 (내 쪽 기준)
            _fieldZone[p] = new CardCell(this, CardFaceSize.Medium, TileSize, new CardKey(p, OcgLocation.SpellTrapZone, 5),
                name: $"zone-{p}-field", faceWidth: SideCardWidth, label: "필드", flip: flip);
            Put(_fieldZone[p].Root, 0, MyMonsterY);
            _extra[p] = new PileCell(this, PileKind.Extra, $"extra-{p}", flip, p == Opp, () => OpenPileWindow(p, OcgLocation.Extra));
            Put(_extra[p].Root, 0, MySpellY);
            _grave[p] = new PileCell(this, PileKind.Grave, $"grave-{p}", flip, p == Opp, () => OpenPileWindow(p, OcgLocation.Grave));
            Put(_grave[p].Root, 6, MyMonsterY);
            _deck[p] = new PileCell(this, PileKind.Deck, $"deck-{p}", flip, p == Opp, () => OpenPileWindow(p, OcgLocation.Deck));
            Put(_deck[p].Root, 6, MySpellY);

            // 제외: 양끝 열의 한가운데(가운데 띠 높이)에 놓는 원. 오른쪽 끝이 나, 왼쪽 끝이 상대. 이름표는 원의 안쪽(필드 가운데 쪽).
            _removed[p] = new PileCell(this, PileKind.Removed, $"removed-{p}", flip, p == Opp, () => OpenPileWindow(p, OcgLocation.Removed));
            float circleX = ColumnX(flip ? 0 : 6) + Tile / 2;
            UiStyle.Place(_removed[p].Root, flip ? circleX - PileCell.Circle / 2 : circleX + PileCell.Circle / 2 - PileCell.RemovedWidth,
                FieldCenterY - PileCell.Circle / 2);
            side.Add(_removed[p].Root);
            return side;
        }

        /// <summary>두 필드 사이의 가운데 띠: 방금 일어난 일(또는 지금 물어보는 것)을 한 줄로, 그 아래에 작은 설명.</summary>
        VisualElement CenterStrip()
        {
            var strip = UiStyle.Box("center-strip");
            strip.pickingMode = PickingMode.Ignore;
            UiStyle.Place(strip, 530, BandY, 860, BandHeight);
            strip.style.justifyContent = Justify.Center;
            strip.style.alignItems = Align.Center;
            strip.style.overflow = Overflow.Hidden;
            _promptLabel = UiStyle.Label("", 27, UiStyle.Accent);
            _promptLabel.name = "prompt-label";
            UiStyle.Black(_promptLabel);
            _promptLabel.pickingMode = PickingMode.Ignore;
            _promptLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _promptDetail = UiStyle.Label("", 14, UiStyle.TextDim);
            _promptDetail.pickingMode = PickingMode.Ignore;
            _promptDetail.style.whiteSpace = WhiteSpace.NoWrap;
            _promptDetail.style.marginTop = -3;
            strip.Add(_promptLabel);
            strip.Add(_promptDetail);
            return strip;
        }

        /// <summary>가운데 띠의 글을 바꾼다. 길면 글자를 줄이고, 설명이 없으면 그 줄을 숨긴다.</summary>
        void SetPrompt(string text, string detail)
        {
            text = text ?? "";
            _promptLabel.text = text;
            _promptLabel.style.fontSize = text.Length > 40 ? 18 : text.Length > 30 ? 22 : 27;
            _promptDetail.text = detail ?? "";
            _promptDetail.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        VisualElement RightPanel()
        {
            var right = UiStyle.Box("right");
            UiStyle.Place(right, RightPanelX, 16, SidePanelWidth, StageHeight - 32);

            // 턴 숫자와 누구 턴인지
            var turn = UiStyle.PanelBox("turn-box");
            turn.style.flexDirection = FlexDirection.Row;
            turn.style.alignItems = Align.Center;
            turn.style.height = 98;
            turn.style.flexShrink = 0;
            turn.style.paddingLeft = turn.style.paddingRight = 18;
            _turnLabel = UiStyle.Label("듀얼 준비", 46);
            _turnLabel.name = "turn-label";
            UiStyle.Black(_turnLabel);
            _turnLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _turnSide = UiStyle.Label("", 22, UiStyle.Text, bold: true);
            _turnSide.name = "turn-side";
            _turnSide.style.whiteSpace = WhiteSpace.NoWrap;
            _turnSide.style.marginLeft = 14;
            turn.Add(_turnLabel);
            turn.Add(_turnSide);
            right.Add(turn);

            // 페이즈 여섯 줄: 지난 페이즈는 흐린 글자, 지금은 금색 바탕, 남은 페이즈는 꺼진 글자
            var phases = UiStyle.PanelBox("phases");
            phases.style.marginTop = 8;
            phases.style.flexShrink = 0;
            for (int i = 0; i < PhaseNames.Length; i++)
            {
                var label = _phaseLabels[i] = UiStyle.Label(PhaseNames[i], 22, UiStyle.Stone4, bold: true);
                label.name = $"phase-{i}";
                label.style.height = 40;
                label.style.paddingLeft = 14;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                UiStyle.Radius(label, 4);
                phases.Add(label);
            }
            right.Add(phases);

            // 버튼: 큰 버튼(지금 물음에 답하는 것)은 한 줄에 하나씩, 작은 버튼(자동·속도·체인 확인)은 아래에 나란히
            var actions = UiStyle.Box("actions");
            actions.style.marginTop = 8;
            actions.style.flexShrink = 0;
            _stripButtons = UiStyle.Box();
            _smallButtons = UiStyle.Box(null, FlexDirection.Row);
            _smallButtons.style.flexWrap = Wrap.Wrap;
            actions.Add(_stripButtons);
            actions.Add(_smallButtons);
            right.Add(actions);

            // 체인이 있을 때만 체인 목록을 띄운다 (방금 일어난 일 위).
            _chainPanel = UiStyle.PanelBox("chain-panel");
            _chainPanel.style.marginTop = 8;
            _chainPanel.style.flexShrink = 0;
            _chainPanel.style.display = DisplayStyle.None;
            right.Add(_chainPanel);

            // 방금 일어난 일: 최근 로그 몇 줄 (새 줄이 아래. 넘치면 위쪽이 잘린다)
            var recent = UiStyle.PanelBox("recent-log");
            recent.style.marginTop = 8;
            recent.style.flexGrow = 1;
            recent.style.flexShrink = 1;
            recent.style.minHeight = 60;
            recent.style.overflow = Overflow.Hidden;
            var title = UiStyle.Label("방금 일어난 일", 17, UiStyle.TextDim, bold: true);
            title.style.flexShrink = 0;
            recent.Add(title);
            _recentLog = UiStyle.Box();
            _recentLog.style.flexGrow = 1;
            _recentLog.style.flexShrink = 1;
            _recentLog.style.overflow = Overflow.Hidden;
            _recentLog.style.justifyContent = Justify.FlexEnd;
            recent.Add(_recentLog);
            right.Add(recent);

            var openLog = LogButton("로그 전체", "open-log");
            openLog.style.marginTop = 8;
            right.Add(openLog);
            RenderRecentLog();
            return right;
        }

        /// <summary>로그 서랍을 여닫는 버튼. 여는 버튼과 닫는 버튼을 화면에서 같은 자리에 둔다 (마우스를 옮기지 않고 다시 누를 수 있게).</summary>
        Button LogButton(string text, string name)
        {
            var button = UiStyle.StoneButton(text, ToggleLogDrawer, small: true);
            button.name = name;
            button.style.height = LogButtonHeight;
            button.style.paddingRight = 44;
            var key = KeyCap("R", "L");
            key.pickingMode = PickingMode.Ignore;
            key.style.position = Position.Absolute;
            key.style.right = 12;
            key.style.top = 11;
            button.Add(key);
            return button;
        }

        VisualElement LogDrawer()
        {
            _logDrawer = UiStyle.PanelBox("log-drawer");
            UiStyle.Place(_logDrawer, DrawerX, 0, DrawerWidth, StageHeight);
            _logDrawer.style.paddingLeft = _logDrawer.style.paddingRight = 20;
            _logDrawer.style.paddingTop = 16;
            var title = UiStyle.Label("로그", 26, UiStyle.Gold);
            UiStyle.Black(title);
            _logDrawer.Add(title);
            _log = new ScrollView(ScrollViewMode.Vertical);
            _log.style.flexGrow = 1;
            _log.style.marginTop = 8;
            _log.style.marginBottom = StageHeight - LogButtonY + 6; // 닫기 버튼 자리
            _logDrawer.Add(_log);
            // 닫기 버튼: "로그 전체" 버튼과 화면에서 같은 자리 (서랍의 테두리 3을 뺀다)
            var close = LogButton("로그 닫기", "close-log");
            UiStyle.Place(close, RightPanelX - DrawerX - 3, LogButtonY - 3, SidePanelWidth);
            _logDrawer.Add(close);
            _drawerOpen = false;
            _drawerShift = DrawerWidth;
            _logDrawer.style.translate = new Translate(DrawerWidth, 0);
            _logDrawer.style.display = DisplayStyle.None;
            return _logDrawer;
        }

        void ToggleLogDrawer() => SetLogDrawer(!_drawerOpen);

        /// <summary>로그 서랍을 열거나 닫는다. 0.28초에 걸쳐 오른쪽에서 미끄러져 나온다.</summary>
        void SetLogDrawer(bool open, bool instant = false)
        {
            if (_logDrawer == null || (open == _drawerOpen && !instant))
                return;
            _drawerOpen = open;
            _logDrawer.style.display = DisplayStyle.Flex;
            float from = _drawerShift, to = open ? 0f : DrawerWidth;
            if (open)
                _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0, float.MaxValue)).ExecuteLater(10);
            if (instant)
            {
                _drawerShift = to;
                _logDrawer.style.translate = new Translate(to, 0);
                _logDrawer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }
            float started = Time.realtimeSinceStartup;
            IVisualElementScheduledItem slide = null;
            slide = _logDrawer.schedule.Execute(() =>
            {
                if (_drawerOpen != open)
                {
                    slide.Pause(); // 그 사이에 다시 눌렀다: 새 움직임이 이어받는다
                    return;
                }
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / 0.28f);
                float eased = 1f - (1f - t) * (1f - t);
                _drawerShift = Mathf.Lerp(from, to, eased);
                _logDrawer.style.translate = new Translate(_drawerShift, 0);
                if (t < 1f)
                    return;
                slide.Pause();
                if (!open)
                    _logDrawer.style.display = DisplayStyle.None;
            }).Every(16);
        }

        /// <summary>오른쪽 "방금 일어난 일"을 최근 로그 몇 줄로 다시 채운다.</summary>
        void RenderRecentLog()
        {
            if (_recentLog == null)
                return;
            _recentLog.Clear();
            for (int i = 0; i < _recentLines.Count; i++)
            {
                var (line, color) = _recentLines[i];
                var label = UiStyle.Label(line, 17, color);
                label.style.marginTop = 3;
                label.style.flexShrink = 0;
                label.style.opacity = i == _recentLines.Count - 1 ? 1f : 0.78f;
                _recentLog.Add(label);
            }
        }

        // ================= 칸 도우미 =================

        /// <summary>행동·선택 강조 테두리. 대상 바깥에 겹쳐 그려서 카드 배치를 바꾸지 않는다.</summary>
        static VisualElement Ring(Vector2 size, float radius)
        {
            var ring = new VisualElement { name = "highlight", pickingMode = PickingMode.Ignore };
            ring.style.position = Position.Absolute;
            ring.style.left = ring.style.top = -5;
            ring.style.width = size.x + 10;
            ring.style.height = size.y + 10;
            UiStyle.Radius(ring, radius);
            ring.style.display = DisplayStyle.None;
            return ring;
        }

        static void ShowRing(VisualElement ring, Color? color)
        {
            ring.style.display = color.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (color.HasValue)
                UiStyle.BorderColor(ring, color.Value, 3);
        }

        /// <summary>가로 가운데에 놓는 작은 표시 (left·right 0인 줄 안의 둥근 칸). top/bottom은 부르는 쪽이 정한다.</summary>
        static VisualElement Chip(int size, Color color, out VisualElement chip, out Label label)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute;
            row.style.left = row.style.right = 0;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.flexShrink = 0;
            var bg = UiStyle.Background;
            chip.style.backgroundColor = new Color(bg.r, bg.g, bg.b, 0.92f);
            UiStyle.BorderColor(chip, UiStyle.Border, 1);
            UiStyle.Radius(chip, 8);
            chip.style.paddingLeft = chip.style.paddingRight = 7;
            chip.style.paddingTop = chip.style.paddingBottom = 1;
            label = UiStyle.Label("", size, color, bold: true);
            label.pickingMode = PickingMode.Ignore;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            chip.Add(label);
            row.Add(chip);
            return row;
        }

        /// <summary>필드 칸의 바탕과 테두리: 바탕 돌 9(52%), 테두리 2px (내 존은 나 색, 상대 존은 상대 색).</summary>
        static void ZoneBox(VisualElement e, bool opponent)
        {
            e.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.52f);
            // 선형 색 공간에서는 반투명한 밝은 색이 더 밝게 보여서 문서의 34%보다 낮춘다
            UiStyle.BorderColor(e, UiStyle.Alpha(opponent ? UiStyle.Opponent : UiStyle.Mine, 0.22f), 2);
            UiStyle.Radius(e, 6);
        }

        /// <summary>양옆 칸의 이름표 (이름과 장수). flip이면 칸의 위쪽, 아니면 아래쪽.</summary>
        static Label SideLabel(bool flip)
        {
            var label = UiStyle.Label("", 16, UiStyle.TextDim, bold: true);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.left = label.style.right = 0;
            label.style.height = SideLabelHeight;
            if (flip) label.style.top = 0;
            else label.style.bottom = 0;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        // ================= 카드 칸 =================

        /// <summary>
        /// 카드 한 장 자리. 필드 칸(tile)이면 정사각형 칸 가운데에 카드를 놓고, 비어도 칸이 보인다.
        /// 양옆 칸(label이 있는 필드 마법 칸)은 카드를 줄여 한쪽에 놓고 반대쪽에 이름표를 단다.
        /// 패·창 안의 카드는 카드 크기 그대로이고, 창에서는 아래에 위치 설명을 붙인다.
        /// </summary>
        sealed class CardCell
        {
            public readonly VisualElement Root;
            /// <summary>필드 칸이면 그 자리 (칸 고르기용). 패·창 안의 카드는 null.</summary>
            public readonly CardKey? Zone;
            public readonly Vector2 Size;
            /// <summary>있으면 누를 때 OnCardClicked 대신 부른다 (목록 창의 후보).</summary>
            public Action ClickOverride;
            /// <summary>마우스가 올라오거나 나갈 때 (패의 카드를 올려 보이는 데 쓴다).</summary>
            public Action<bool> Hover;

            readonly DuelScreen _screen;
            readonly CardFaceSize _faceSize;
            readonly float _faceWidth;
            readonly VisualElement _holder, _slot, _ring, _markRow;
            readonly Label _hint, _mark, _caption;
            Label _badge;
            CardInfo _card;
            bool _visible;
            string _signature;
            string _emptyText = "";

            /// <param name="faceWidth">0보다 크면 카드 폭을 직접 정한다 (양옆 칸의 86% 카드).</param>
            /// <param name="label">양옆 칸의 이름표. flip이면 이름표가 위, 카드가 아래 (상대 쪽).</param>
            public CardCell(DuelScreen screen, CardFaceSize faceSize, Vector2? tile = null, CardKey? zone = null,
                Action refresh = null, string name = null, float faceWidth = 0, string label = null, bool flip = false)
            {
                _screen = screen;
                _faceSize = faceSize;
                _faceWidth = faceWidth;
                Zone = zone;
                Size = tile ?? CardFaces.SizeOf(faceSize);

                Root = new VisualElement { name = name };
                Root.style.flexShrink = 0;
                Root.style.alignItems = Align.Center;

                _holder = new VisualElement();
                _holder.style.width = Size.x;
                _holder.style.height = Size.y;
                _holder.style.justifyContent = Justify.Center;
                _holder.style.alignItems = Align.Center;
                if (tile.HasValue)
                    ZoneBox(_holder, zone.HasValue && zone.Value.Controller == screen.Opp);
                _slot = new VisualElement { pickingMode = PickingMode.Ignore };
                _holder.Add(_slot);
                if (label != null)
                {
                    // 카드를 이름표 반대쪽에 붙인다
                    _holder.style.justifyContent = flip ? Justify.FlexEnd : Justify.FlexStart;
                    if (flip) _slot.style.marginBottom = 1;
                    else _slot.style.marginTop = 1;
                    var title = SideLabel(flip);
                    title.text = label;
                    _holder.Add(title);
                }

                _hint = UiStyle.Label("", 15, UiStyle.TextDim, bold: true);
                _hint.pickingMode = PickingMode.Ignore;
                _hint.style.position = Position.Absolute;
                _hint.style.left = _hint.style.top = _hint.style.right = _hint.style.bottom = 0;
                _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                _holder.Add(_hint);

                _markRow = Chip(13, UiStyle.Text, out _, out _mark);
                _markRow.style.bottom = label != null && !flip ? SideLabelHeight + 4 : 8;
                _markRow.style.display = DisplayStyle.None;
                _holder.Add(_markRow);

                _ring = Ring(Size, 8);
                _holder.Add(_ring);
                Root.Add(_holder);

                _caption = UiStyle.Label("", 12, UiStyle.TextDim);
                _caption.pickingMode = PickingMode.Ignore;
                _caption.style.width = Size.x;
                _caption.style.marginTop = 3;
                _caption.style.unityTextAlign = TextAnchor.UpperCenter;
                _caption.style.display = DisplayStyle.None;
                Root.Add(_caption);

                CardInput.Attach(Root, null,
                    onHover: () =>
                    {
                        if (_card != null)
                            _screen.PreviewCard(_card, _visible);
                        if (_card != null)
                            Hover?.Invoke(true);
                    },
                    onLeave: () =>
                    {
                        _screen.EndPreview();
                        Hover?.Invoke(false);
                    },
                    onClick: () =>
                    {
                        if (ClickOverride != null)
                            ClickOverride();
                        else
                            _screen.OnCardClicked(_card, _visible, Root, refresh, Zone);
                    });
                Set(null, false);
            }

            public void Set(CardInfo card, bool visible)
            {
                _card = card;
                _visible = visible;
                bool onField = card != null && card.Location.IsOnField;
                bool faceDown = onField && !card.IsFaceUp;
                // 수비 표시는 몬스터 존에서만 돌린다 (마법·함정의 표시 형식 값에도 수비 비트가 들어 있다: 세트 = FaceDown, 앞면 = FaceUp)
                bool defense = card != null && card.Location.Location == OcgLocation.MonsterZone && (card.Position & OcgPosition.Defense) != 0;
                bool stats = onField && card.IsMonster;
                string signature = card == null ? null
                    : $"{card.Code}|{visible}|{faceDown}|{defense}|" +
                      (stats ? $"{card.Attack}/{card.Defense}/{card.BaseAttack}/{card.BaseDefense}" : "");
                if (signature != _signature)
                {
                    _signature = signature;
                    _slot.Clear();
                    if (card != null)
                    {
                        var faces = _screen._faces;
                        var cardStats = stats ? StatsOf(card) : (CardStats?)null;
                        var face = !visible ? (_faceWidth > 0 ? faces.BuildBack(_faceWidth) : faces.BuildBack(_faceSize))
                            : _faceWidth > 0 ? faces.Build(card.Code, _faceWidth, stats: cardStats)
                            : faces.Build(card.Code, _faceSize, stats: cardStats);
                        if (visible && faceDown)
                            face.style.opacity = 0.55f; // 나의 세트 카드: 나만 앞면을 흐리게 본다
                        if (defense)
                            face.style.rotate = new StyleRotate(new Rotate(new Angle(90f, AngleUnit.Degree)));
                        _slot.Add(face);
                    }
                }

                int materials = onField ? card.OverlayCodes.Count : 0;
                string mark = card == null ? null : visible && faceDown ? "세트" : materials > 0 ? $"소재 {materials}" : null;
                _mark.text = mark ?? "";
                _markRow.style.display = mark != null ? DisplayStyle.Flex : DisplayStyle.None;
                _hint.text = card == null ? _emptyText : "";
                _hint.style.color = UiStyle.TextDim;
            }

            VisualElement _foot;

            /// <summary>카드 아래로 보이지 않는 자리를 늘린다 (마우스 올리기·누르기가 이어진다). 0이면 없앤다.</summary>
            public void ExtendDown(float height)
            {
                if (_foot == null)
                {
                    if (height <= 0)
                        return;
                    _foot = new VisualElement();
                    _foot.style.position = Position.Absolute;
                    _foot.style.left = _foot.style.right = 0;
                    _foot.style.top = Size.y;
                    Root.Add(_foot);
                }
                _foot.style.height = Mathf.Max(0, height);
                _foot.style.display = height > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public CardInfo Card => _card;
            public bool Visible => _visible;
            /// <summary>강조 테두리가 켜져 있는지 (할 수 있는 일이 있거나 고를 수 있는 카드·칸). Tab으로 도는 대상이다.</summary>
            public bool Highlighted { get; private set; }

            /// <summary>행동·선택 강조. null이면 끈다.</summary>
            public void SetHighlight(Color? color)
            {
                Highlighted = color.HasValue;
                ShowRing(_ring, color);
            }

            public void SetEmptyText(string text)
            {
                _emptyText = text;
                if (_card == null)
                    _hint.text = text;
            }

            /// <summary>빈칸에 잠깐 띄우는 안내 (칸 고르기). 다음 Set에서 원래 문구로 돌아간다.</summary>
            public void ShowEmptyHint(string text, Color color)
            {
                if (_card != null)
                    return;
                _hint.text = text;
                _hint.style.color = color;
            }

            public void SetCaption(string text)
            {
                _caption.text = text ?? "";
                _caption.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            Label _note, _chainNumber;

            /// <summary>체인 번호 (왼쪽 위 동그라미, 쪽 색). 0이면 숨긴다.</summary>
            public void SetChainNumber(int number, Color color)
            {
                if (_chainNumber == null)
                {
                    if (number <= 0)
                        return;
                    _chainNumber = UiStyle.Label("", 19, UiStyle.Background, bold: true);
                    _chainNumber.name = "chain-number";
                    _chainNumber.pickingMode = PickingMode.Ignore;
                    _chainNumber.style.position = Position.Absolute;
                    _chainNumber.style.left = -8;
                    _chainNumber.style.top = -8;
                    _chainNumber.style.width = _chainNumber.style.height = 36;
                    _chainNumber.style.unityTextAlign = TextAnchor.MiddleCenter;
                    UiStyle.Radius(_chainNumber, 18);
                    UiStyle.BorderColor(_chainNumber, UiStyle.Background, 2);
                    _holder.Add(_chainNumber);
                }
                _chainNumber.text = number > 0 ? number.ToString() : "";
                _chainNumber.style.backgroundColor = color;
                _chainNumber.style.display = number > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            /// <summary>카드 가운데에 겹쳐 띄우는 짧은 글 (공격 대상의 예상 결과). null이나 빈 문자열이면 숨긴다.</summary>
            public void SetNote(string text)
            {
                if (_note == null)
                {
                    if (string.IsNullOrEmpty(text))
                        return;
                    _note = UiStyle.Label("", 16, UiStyle.Text, bold: true);
                    _note.name = "attack-preview";
                    _note.pickingMode = PickingMode.Ignore;
                    _note.style.position = Position.Absolute;
                    _note.style.left = 2;
                    _note.style.right = 2;
                    _note.style.top = Length.Percent(32);
                    _note.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.85f);
                    _note.style.paddingTop = _note.style.paddingBottom = 3;
                    _note.style.unityTextAlign = TextAnchor.MiddleCenter;
                    _note.style.whiteSpace = WhiteSpace.Normal;
                    UiStyle.Radius(_note, 4);
                    _holder.Add(_note);
                }
                _note.text = text ?? "";
                _note.style.color = text != null && text.StartsWith("파괴") ? Chosen
                    : text != null && (text.Contains("나 -") || text.StartsWith("내 몬스터")) ? FocusTarget : UiStyle.Text;
                _note.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            /// <summary>오른쪽 아래에 크게 띄우는 표시 (순서 정하기의 번호). null이나 빈 문자열이면 숨긴다.</summary>
            public void SetBadge(string text)
            {
                if (_badge == null)
                {
                    if (string.IsNullOrEmpty(text))
                        return;
                    _badge = UiStyle.Label("", 20, UiStyle.Background, bold: true);
                    _badge.pickingMode = PickingMode.Ignore;
                    _badge.style.position = Position.Absolute;
                    _badge.style.right = 4;
                    _badge.style.bottom = 4;
                    _badge.style.backgroundColor = Chosen;
                    _badge.style.paddingLeft = 7;
                    _badge.style.paddingRight = 7;
                    _badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                    UiStyle.Radius(_badge, 4);
                    _holder.Add(_badge);
                }
                _badge.text = text ?? "";
                _badge.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        // ================= 카드 더미 =================

        enum PileKind { Deck, Extra, Grave, Removed }

        /// <summary>
        /// 덱·엑스트라 덱·묘지: 양옆 칸 하나. 카드 묶음(86%, 장수에 따라 두꺼워진다)과 이름·장수 이름표.
        /// 내 쪽은 카드가 위, 이름표가 아래. 상대 쪽(flip)은 이름표가 위, 카드가 아래이고 묶음이 쌓이는 방향도 반대다.
        /// 위 카드는 덱·엑스트라 덱이면 뒷면, 묘지면 마지막 카드(어둡게).
        /// 제외: 원(지름 48) 안에 장수, 옆(필드 가운데 쪽)에 이름표.
        /// </summary>
        sealed class PileCell
        {
            const int MaxLayers = 3;
            const float Step = 1.5f; // 카드(136)와 이름표(20) 사이에 남는 자리가 4.5뿐이다
            public const float Circle = 48, RemovedWidth = 104;

            public readonly VisualElement Root;
            readonly DuelScreen _screen;
            readonly PileKind _kind;
            readonly bool _flip;
            readonly Vector2 _card;
            readonly VisualElement _layers, _top, _ring;
            readonly Label _count;
            string _signature;
            CardInfo _topCard;
            bool _topVisible;

            public PileCell(DuelScreen screen, PileKind kind, string name, bool flip, bool opponent, Action onClick)
            {
                _screen = screen;
                _kind = kind;
                _flip = flip;
                _card = new Vector2(SideCardWidth, SideCardWidth * CardFaces.BaseHeight / CardFaces.BaseWidth);
                Root = new VisualElement { name = name };

                if (kind == PileKind.Removed)
                {
                    // [원][제외] (상대, 왼쪽 끝) 또는 [제외][원] (나, 오른쪽 끝)
                    Root.style.flexDirection = flip ? FlexDirection.Row : FlexDirection.RowReverse;
                    Root.style.alignItems = Align.Center;
                    Root.style.width = RemovedWidth;
                    Root.style.height = Circle;
                    var circle = new VisualElement { pickingMode = PickingMode.Ignore };
                    circle.style.width = circle.style.height = Circle;
                    circle.style.flexShrink = 0;
                    circle.style.alignItems = Align.Center;
                    circle.style.justifyContent = Justify.Center;
                    circle.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.6f);
                    UiStyle.Radius(circle, Circle / 2);
                    UiStyle.BorderColor(circle, UiStyle.Alpha(opponent ? UiStyle.Opponent : UiStyle.Mine, 0.45f), 2);
                    _count = UiStyle.Label("0", 19, UiStyle.Text, bold: true);
                    _count.pickingMode = PickingMode.Ignore;
                    circle.Add(_count);
                    _ring = Ring(new Vector2(Circle, Circle), Circle / 2 + 5);
                    circle.Add(_ring);
                    Root.Add(circle);
                    var title = UiStyle.Label("제외", 17, UiStyle.TextDim, bold: true);
                    title.pickingMode = PickingMode.Ignore;
                    title.style.whiteSpace = WhiteSpace.NoWrap;
                    if (flip) title.style.marginLeft = 8;
                    else title.style.marginRight = 8;
                    Root.Add(title);
                }
                else
                {
                    Root.style.width = Tile;
                    Root.style.height = Tile;
                    ZoneBox(Root, opponent);
                    var stack = new VisualElement { pickingMode = PickingMode.Ignore };
                    stack.style.position = Position.Absolute;
                    stack.style.left = (Tile - 4 - _card.x) / 2; // 테두리 2씩
                    stack.style.width = _card.x;
                    stack.style.height = _card.y;
                    if (flip) stack.style.bottom = 0;
                    else stack.style.top = 0;
                    _layers = new VisualElement { pickingMode = PickingMode.Ignore };
                    stack.Add(_layers);
                    _top = new VisualElement { pickingMode = PickingMode.Ignore };
                    _top.style.position = Position.Absolute;
                    _top.style.left = _top.style.top = 0;
                    _top.style.width = _card.x;
                    _top.style.height = _card.y;
                    stack.Add(_top);
                    _ring = Ring(_card, 7);
                    stack.Add(_ring);
                    Root.Add(stack);
                    _count = SideLabel(flip);
                    Root.Add(_count);
                }

                CardInput.Attach(Root, null,
                    onHover: () =>
                    {
                        if (_topCard != null)
                            _screen.PreviewCard(_topCard, _topVisible);
                    },
                    onLeave: _screen.EndPreview,
                    onClick: onClick);
            }

            string Title
            {
                get
                {
                    switch (_kind)
                    {
                        case PileKind.Deck: return "덱";
                        case PileKind.Extra: return "엑스트라";
                        case PileKind.Grave: return "묘지";
                        default: return "제외";
                    }
                }
            }

            public CardInfo TopCard => _topCard;
            public bool TopVisible => _topVisible;
            public bool Highlighted { get; private set; }

            /// <summary>top: 위 카드 (덱·엑스트라 덱은 null → 뒷면). visible: 위 카드를 앞면으로 보일지.</summary>
            public void Set(int count, CardInfo top, bool visible, bool highlight)
            {
                _topCard = top;
                _topVisible = visible;
                Highlighted = highlight;
                ShowRing(_ring, highlight ? Actionable : (Color?)null);
                if (_kind == PileKind.Removed)
                {
                    _count.text = count.ToString();
                    return;
                }
                _count.text = $"{Title} {count}";
                string signature = $"{count}|{top?.Code}|{visible}";
                if (signature == _signature)
                    return;
                _signature = signature;
                _layers.Clear();
                _top.Clear();
                if (count == 0)
                    return;

                // 두께: 2장부터 한 겹, 10장마다 한 겹 더 (최대 MaxLayers겹). 내 쪽은 오른쪽 아래로, 상대 쪽은 왼쪽 위로 비켜 쌓인다.
                int layers = count <= 1 ? 0 : Mathf.Min(MaxLayers, 1 + count / 10);
                float direction = _flip ? -1f : 1f;
                for (int k = layers; k >= 1; k--)
                {
                    var layer = new VisualElement { pickingMode = PickingMode.Ignore };
                    layer.style.position = Position.Absolute;
                    layer.style.left = layer.style.top = direction * k * Step;
                    layer.style.width = _card.x;
                    layer.style.height = _card.y;
                    layer.style.backgroundColor = top == null ? PileBack : PileFace;
                    UiStyle.BorderColor(layer, top == null ? PileBackEdge : UiStyle.Border, 1);
                    UiStyle.Radius(layer, 4);
                    _layers.Add(layer);
                }

                var faces = _screen._faces;
                var face = top == null || !visible ? faces.BuildBack(SideCardWidth) : faces.Build(top.Code, SideCardWidth);
                face.pickingMode = PickingMode.Ignore;
                if (_kind == PileKind.Grave)
                    face.style.opacity = 0.62f; // 묘지는 맨 위 카드를 어둡게 보여 준다
                _top.Add(face);
            }
        }
    }
}
