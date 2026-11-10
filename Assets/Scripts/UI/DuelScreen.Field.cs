// 듀얼 화면의 화면 구성: 필드 칸, 손패, 카드 더미(덱·엑스트라·묘지·제외), 왼쪽 카드 정보.
// 듀얼 진행과 조작(누르기·메뉴·창)은 DuelScreen.cs에 있다.
//
// 배치 (기준 해상도 1920×1080, 가운데 열 1072 너비):
//   위에서부터 상대 손패(카드 아래쪽만 보이게 잘림) / 상대 필드 / 가운데 줄(턴·체인·안내·버튼) / 내 필드 / 내 손패.
//   필드 반쪽(1072×300): 가운데 5×2 격자(정사각형 칸; 내 쪽은 위가 몬스터 존, 아래가 마법·함정 존)와 격자 바깥의
//   필드 존·엑스트라 덱(왼쪽), 묘지·제외·덱(오른쪽). 바깥 칸은 격자 줄에 맞추지 않고 일부러 조금씩 어긋나게 둔다.
//   상대 쪽은 화면 가운데를 중심으로 점대칭이다.
// 카드: 카드 앞면(CardFaces)으로 그린다. 상대의 뒷면 카드는 카드 뒷면, 나의 세트 카드는 흐린 앞면 + "세트" 표시(나만 본다).
//   수비 표시는 90도 돌린다. 필드의 공격력·수비력이 원래 값과 다르면 숫자 색(높으면 초록, 낮으면 빨강)으로 보인다.
// 카드 더미: 장수에 따라 두꺼워지는 카드 묶음 + 이름·장수 표시. 덱·엑스트라 덱 위는 뒷면, 묘지·제외 위는 마지막 카드.
//   제외는 묘지 옆(양쪽 필드 사이 쪽)에 조금 작게 둔다.
// 카드 정보(왼쪽): 카드에 마우스를 올리면 크게 보이고, 누른 카드는 마우스를 떼도 남는다.
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
        const float HalfWidth = 1072;
        const float HalfHeight = 300;
        const float Tile = 128;
        const float TileGap = 8;
        const float GridX = 200;
        const float MonsterRowY = 8;
        const float SpellRowY = MonsterRowY + Tile + TileGap;
        const float OppHandHeight = 64;
        const float HandRaise = 14;
        const float RemovedScale = 0.74f;

        static readonly Vector2 TileSize = new Vector2(Tile, Tile);
        static readonly Vector2 FieldZoneSize = new Vector2(96, 134);
        static readonly Color PileBack = UiStyle.Hex(0x1d2533);
        static readonly Color PileBackEdge = UiStyle.Hex(0x4a5a78);
        static readonly Color PileFace = UiStyle.Hex(0x2a303b);

        VisualElement _detailFace;
        Label _detailType, _detailStats, _detailText;
        CardInfo _pinnedCard;
        bool _pinnedVisible;

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

        /// <summary>손패. 칸은 다시 쓰고(같은 카드면 다시 그리지 않는다), 너무 많으면 겹쳐 놓는다.</summary>
        void RenderHand(byte p, PlayerState player)
        {
            var row = _handRows[p];
            var cells = _handCells[p];
            var size = p == Me ? CardFaceSize.Medium : CardFaceSize.Field;
            while (cells.Count < player.Hand.Count)
            {
                var cell = new CardCell(this, size, raise: p == Me);
                cells.Add(cell);
                row.Add(cell.Root);
            }
            while (cells.Count > player.Hand.Count)
            {
                cells[cells.Count - 1].Root.RemoveFromHierarchy();
                cells.RemoveAt(cells.Count - 1);
            }
            float width = CardFaces.SizeOf(size).x;
            float step = cells.Count <= 1 ? 0 : Mathf.Min(width + 8, (HalfWidth - 40 - width) / (cells.Count - 1));
            for (int i = 0; i < cells.Count; i++)
            {
                var card = player.Hand[i];
                cells[i].Root.name = $"hand-{p}-{i}";
                cells[i].Root.style.marginLeft = i == 0 ? 0 : step - width;
                SetCell(cells[i], card, p == Me || card.IsPublic);
            }
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

        // ================= 카드 정보 =================

        /// <summary>누른 카드: 카드 정보에 남긴다.</summary>
        void ShowCard(CardInfo card, bool visible)
        {
            if (card == null)
                return;
            _pinnedCard = card;
            _pinnedVisible = visible;
            RenderDetail(card, visible);
        }

        /// <summary>마우스를 올린 카드: 떼면 누른 카드로 돌아간다.</summary>
        void PreviewCard(CardInfo card, bool visible)
        {
            if (card == null)
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
            _detailType.text = "카드에 마우스를 올리면 여기에 크게 보인다.";
            _detailStats.text = "";
            _detailText.text = "";
        }

        void RenderDetail(CardInfo card, bool visible)
        {
            _detailFace.Clear();
            if (!visible)
            {
                _detailFace.Add(_faces.BuildBack(CardFaceSize.Large));
                _detailType.text = $"뒷면 카드 · {_text.Player(card.Location.Controller)}의 {DuelText.Location(card.Location)}";
                _detailStats.text = "";
                _detailText.text = "";
                return;
            }
            bool onField = card.Location.IsOnField;
            _detailFace.Add(_faces.Build(card.Code, CardFaceSize.Large, stats: onField && card.IsMonster ? StatsOf(card) : (CardStats?)null));
            var record = _text.Record(card.Code);
            _detailType.text = DuelText.TypeLine(card.Type != 0 ? card.Type : record?.Type ?? OcgCardType.None,
                card.IsMonster && (card.Type & OcgCardType.Xyz) != 0 ? card.Rank : card.Level, card.Attribute, card.Race);
            _detailStats.text = card.IsMonster
                ? $"ATK {card.Attack} / DEF {card.Defense}" + (card.Attack != card.BaseAttack || card.Defense != card.BaseDefense ? $"  (원래 {card.BaseAttack} / {card.BaseDefense})" : "")
                  + (onField ? $" · {DuelText.Position(card.Position)}" : "")
                : onField ? DuelText.Position(card.Position) : "";
            _detailText.text = record?.Description ?? "";
        }

        // ================= 화면 구성 =================

        void BuildLayout()
        {
            // ---- 왼쪽: 상대 정보 / 카드 정보(내 초상화 자리) / 내 정보 ----
            var left = UiStyle.Box("left");
            left.style.width = 340;
            left.style.flexShrink = 0;
            left.style.marginRight = 12;
            left.Add(PlayerPanel(Opp));
            var detail = UiStyle.PanelBox("card-detail");
            detail.style.flexGrow = 1;
            detail.style.marginTop = detail.style.marginBottom = 12;
            _detailFace = new VisualElement { name = "detail-face", pickingMode = PickingMode.Ignore };
            _detailFace.style.alignItems = Align.Center;
            _detailFace.style.flexShrink = 0;
            _detailFace.style.minHeight = CardFaces.SizeOf(CardFaceSize.Large).y;
            _detailType = UiStyle.Label("", 15, UiStyle.TextDim);
            _detailStats = UiStyle.Label("", 16, UiStyle.Accent);
            _detailType.style.marginTop = 10;
            _detailStats.style.marginTop = 4;
            var textScroll = new ScrollView(ScrollViewMode.Vertical);
            textScroll.style.flexGrow = 1;
            textScroll.style.marginTop = 8;
            _detailText = UiStyle.Label("", 15);
            textScroll.Add(_detailText);
            detail.Add(_detailFace);
            detail.Add(_detailType);
            detail.Add(_detailStats);
            detail.Add(textScroll);
            left.Add(detail);
            left.Add(PlayerPanel(Me));
            _root.Add(left);
            ClearDetail();

            // ---- 가운데: 필드 ----
            var center = UiStyle.Box("field");
            center.RegisterCallback<PointerDownEvent>(OnFieldPointerDown, TrickleDown.TrickleDown);
            _statusBadge = BuildStatusBadge();
            center.style.flexGrow = 1;
            center.style.flexShrink = 0;
            center.style.minWidth = HalfWidth;
            center.style.justifyContent = Justify.SpaceBetween;
            center.style.alignItems = Align.Center;
            center.Add(_handRows[Opp] = HandRow(Opp));
            center.Add(FieldHalf(Opp));
            center.Add(CenterStrip());
            center.Add(FieldHalf(Me));
            center.Add(_handRows[Me] = HandRow(Me));
            center.Add(_statusBadge); // 다른 것 위에 겹쳐 그린다 (오른쪽 위 모서리)
            _root.Add(center);

            // ---- 오른쪽: 로그 ----
            var right = UiStyle.PanelBox("log");
            right.style.width = 460;
            right.style.minWidth = 320;
            right.style.flexShrink = 1;
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
            _lpDelta[p] = UiStyle.Label("", 24, FocusTarget, bold: true);
            _lpDelta[p].name = $"lp-delta-{p}";
            _lpDelta[p].style.marginLeft = 12;
            _lpDelta[p].style.marginBottom = 5;
            _playerInfo[p] = UiStyle.Label("", 15, UiStyle.TextDim);
            var lpRow = UiStyle.Box(null, FlexDirection.Row);
            lpRow.style.alignItems = Align.FlexEnd;
            lpRow.Add(_lp[p]);
            lpRow.Add(_lpDelta[p]);
            panel.Add(nameLabel);
            panel.Add(lpRow);
            panel.Add(_playerInfo[p]);
            return panel;
        }

        /// <summary>
        /// 손패 줄. 내 손패는 카드 전체(마우스를 올리면 조금 올라온다),
        /// 상대 손패는 화면 위 가장자리에 걸쳐 카드 아래쪽만 보인다.
        /// </summary>
        static VisualElement HandRow(byte p)
        {
            var row = UiStyle.Box(p == Me ? "my-hand" : "opp-hand", FlexDirection.Row);
            row.style.width = HalfWidth;
            row.style.flexShrink = 0;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.FlexEnd;
            if (p == Me)
                row.style.height = CardFaces.SizeOf(CardFaceSize.Medium).y + HandRaise;
            else
            {
                row.style.height = OppHandHeight;
                row.style.overflow = Overflow.Hidden;
            }
            return row;
        }

        /// <summary>
        /// 필드 반쪽. 좌표는 내 쪽 기준이고, 상대 쪽은 점대칭으로 옮긴다 (x' = 너비 - x - w, y' = 높이 - y - h).
        /// 바깥 칸(필드 존·카드 더미)은 격자 줄과 높이·간격을 일부러 맞추지 않는다.
        /// </summary>
        VisualElement FieldHalf(byte p)
        {
            var half = new VisualElement { name = p == Me ? "my-field" : "opp-field" };
            half.style.width = HalfWidth;
            half.style.height = HalfHeight;
            half.style.flexShrink = 0;

            void Place(VisualElement e, Vector2 size, float x, float y)
            {
                if (p == Opp)
                {
                    x = HalfWidth - x - size.x;
                    y = HalfHeight - y - size.y;
                }
                e.style.position = Position.Absolute;
                e.style.left = x;
                e.style.top = y;
                half.Add(e);
            }

            for (int i = 0; i < MainZones; i++)
            {
                float x = GridX + i * (Tile + TileGap);
                _monsters[p, i] = new CardCell(this, CardFaceSize.Field, TileSize, new CardKey(p, OcgLocation.MonsterZone, (uint)i),
                    name: $"zone-{p}-m{i}");
                Place(_monsters[p, i].Root, TileSize, x, MonsterRowY);
                _spells[p, i] = new CardCell(this, CardFaceSize.Field, TileSize, new CardKey(p, OcgLocation.SpellTrapZone, (uint)i),
                    name: $"zone-{p}-s{i}");
                Place(_spells[p, i].Root, TileSize, x, SpellRowY);
            }
            _fieldZone[p] = new CardCell(this, CardFaceSize.Field, FieldZoneSize, new CardKey(p, OcgLocation.SpellTrapZone, 5),
                name: $"zone-{p}-field");
            _fieldZone[p].SetEmptyText("필드");
            Place(_fieldZone[p].Root, FieldZoneSize, 60, 2);

            _extra[p] = new PileCell(this, PileKind.Extra, $"extra-{p}", 1f, () => OpenPileWindow(p, OcgLocation.Extra));
            Place(_extra[p].Root, _extra[p].Size, 46, 160);
            _grave[p] = new PileCell(this, PileKind.Grave, $"grave-{p}", 1f, () => OpenPileWindow(p, OcgLocation.Grave));
            Place(_grave[p].Root, _grave[p].Size, 908, 14);
            // 제외: 묘지 옆, 양쪽 필드 사이 쪽(내 쪽은 위)에 조금 작게
            _removed[p] = new PileCell(this, PileKind.Removed, $"removed-{p}", RemovedScale, () => OpenPileWindow(p, OcgLocation.Removed));
            Place(_removed[p].Root, _removed[p].Size, 1002, 0);
            _deck[p] = new PileCell(this, PileKind.Deck, $"deck-{p}", 1f, () => OpenPileWindow(p, OcgLocation.Deck));
            Place(_deck[p].Root, _deck[p].Size, 922, 160);
            return half;
        }

        // 양쪽 필드 사이: 위 줄은 턴·페이즈와 체인 목록(센터 카드 자리), 아래 줄은 안내 문구와 버튼(페이즈 넘기기, 확인 등).
        VisualElement CenterStrip()
        {
            var strip = UiStyle.Box("center-strip");
            strip.style.width = Length.Percent(100);
            strip.style.backgroundColor = UiStyle.PanelLight;
            UiStyle.Radius(strip, 6);
            UiStyle.Padding(strip, 8);

            var top = UiStyle.Box(null, FlexDirection.Row);
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            _turnLabel = UiStyle.Label("듀얼 준비", 19, bold: true);
            _turnLabel.name = "turn-label";
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
            _promptLabel.name = "prompt-label";
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

        // ================= 카드 칸 =================

        /// <summary>
        /// 카드 한 장 자리. 필드 칸(tile)이면 정사각형(필드 존은 카드 모양) 칸 가운데에 카드를 놓고, 비어도 칸이 보인다.
        /// 손패·창 안의 카드는 카드 크기 그대로이고, 창에서는 아래에 위치 설명을 붙인다.
        /// </summary>
        sealed class CardCell
        {
            public readonly VisualElement Root;
            /// <summary>필드 칸이면 그 자리 (칸 고르기용). 패·창 안의 카드는 null.</summary>
            public readonly CardKey? Zone;
            public readonly Vector2 Size;
            /// <summary>있으면 누를 때 OnCardClicked 대신 부른다 (목록 창의 후보).</summary>
            public Action ClickOverride;

            readonly DuelScreen _screen;
            readonly CardFaceSize _faceSize;
            readonly bool _raise;
            readonly VisualElement _holder, _slot, _ring, _markRow;
            readonly Label _hint, _mark, _caption;
            Label _badge;
            CardInfo _card;
            bool _visible;
            string _signature;
            string _emptyText = "";

            public CardCell(DuelScreen screen, CardFaceSize faceSize, Vector2? tile = null, CardKey? zone = null,
                Action refresh = null, bool raise = false, string name = null)
            {
                _screen = screen;
                _faceSize = faceSize;
                _raise = raise;
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
                {
                    _holder.style.backgroundColor = UiStyle.ZoneEmpty;
                    var side = zone.HasValue && zone.Value.Controller == Opp ? UiStyle.Opponent : UiStyle.Mine;
                    UiStyle.BorderColor(_holder, Color.Lerp(UiStyle.Border, side, 0.3f), 1);
                    UiStyle.Radius(_holder, 6);
                }
                _slot = new VisualElement { pickingMode = PickingMode.Ignore };
                _holder.Add(_slot);

                _hint = UiStyle.Label("", 13, UiStyle.TextDim, bold: true);
                _hint.pickingMode = PickingMode.Ignore;
                _hint.style.position = Position.Absolute;
                _hint.style.left = _hint.style.top = _hint.style.right = _hint.style.bottom = 0;
                _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                _holder.Add(_hint);

                _markRow = Chip(12, UiStyle.Text, out _, out _mark);
                _markRow.style.bottom = 6;
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
                        if (_raise && _card != null)
                            _holder.style.translate = new Translate(0, -HandRaise);
                    },
                    onLeave: () =>
                    {
                        _screen.EndPreview();
                        if (_raise)
                            _holder.style.translate = StyleKeyword.Null;
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
                        var face = visible
                            ? faces.Build(card.Code, _faceSize, stats: stats ? StatsOf(card) : (CardStats?)null)
                            : faces.BuildBack(_faceSize);
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
                if (card == null && _raise)
                    _holder.style.translate = StyleKeyword.Null;
            }

            /// <summary>행동·선택 강조. null이면 끈다.</summary>
            public void SetHighlight(Color? color) => ShowRing(_ring, color);

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

            Label _note;

            /// <summary>카드 가운데에 겹쳐 띄우는 짧은 글 (공격 대상의 예상 결과). null이나 빈 문자열이면 숨긴다.</summary>
            public void SetNote(string text)
            {
                if (_note == null)
                {
                    if (string.IsNullOrEmpty(text))
                        return;
                    _note = UiStyle.Label("", 14, UiStyle.Text, bold: true);
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
        /// 덱·엑스트라 덱·묘지·제외: 장수에 따라 두꺼워지는 카드 묶음 + 아래 가장자리에 걸친 이름·장수 표시.
        /// 위 카드는 덱·엑스트라 덱이면 뒷면, 묘지·제외면 마지막 카드(뒷면으로 제외된 카드는 뒷면).
        /// </summary>
        sealed class PileCell
        {
            const int MaxLayers = 4;

            public readonly VisualElement Root;
            public readonly Vector2 Size;
            readonly DuelScreen _screen;
            readonly PileKind _kind;
            readonly float _scale;
            readonly Vector2 _card;
            readonly float _step;
            readonly VisualElement _layers, _top, _ring;
            readonly Label _count;
            string _signature;
            CardInfo _topCard;
            bool _topVisible;

            public PileCell(DuelScreen screen, PileKind kind, string name, float scale, Action onClick)
            {
                _screen = screen;
                _kind = kind;
                _scale = scale;
                _card = CardFaces.SizeOf(CardFaceSize.Field) * scale;
                _step = Mathf.Round(2 * scale);
                Size = _card + Vector2.one * (_step * MaxLayers);

                Root = new VisualElement { name = name };
                Root.style.width = Size.x;
                Root.style.height = Size.y;
                _layers = new VisualElement { pickingMode = PickingMode.Ignore };
                _layers.style.position = Position.Absolute;
                _layers.style.left = _layers.style.top = 0;
                Root.Add(_layers);
                _top = new VisualElement { pickingMode = PickingMode.Ignore };
                _top.style.position = Position.Absolute;
                _top.style.left = _top.style.top = 0;
                _top.style.width = _card.x;
                _top.style.height = _card.y;
                Root.Add(_top);
                _ring = Ring(_card, 7);
                Root.Add(_ring);

                var chipRow = Chip(13, UiStyle.TextDim, out var chip, out _count);
                chipRow.style.bottom = -9;
                chip.Insert(0, Icon(kind));
                Root.Add(chipRow);

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

            /// <summary>top: 위 카드 (덱·엑스트라 덱은 null → 뒷면). visible: 위 카드를 앞면으로 보일지.</summary>
            public void Set(int count, CardInfo top, bool visible, bool highlight)
            {
                _topCard = top;
                _topVisible = visible;
                _count.text = $"{Title} {count}";
                ShowRing(_ring, highlight ? Actionable : (Color?)null);
                bool faceDown = top != null && !top.IsFaceUp && _kind == PileKind.Removed;
                string signature = $"{count}|{top?.Code}|{visible}|{faceDown}";
                if (signature == _signature)
                    return;
                _signature = signature;
                _layers.Clear();
                _top.Clear();
                if (count == 0)
                {
                    var empty = new VisualElement { pickingMode = PickingMode.Ignore };
                    empty.style.width = _card.x;
                    empty.style.height = _card.y;
                    empty.style.backgroundColor = UiStyle.ZoneEmpty;
                    UiStyle.BorderColor(empty, UiStyle.Border, 1);
                    UiStyle.Radius(empty, 4 * _scale);
                    _top.Add(empty);
                    return;
                }

                bool back = top == null || !visible || faceDown;
                // 두께: 2장부터 한 겹, 10장마다 한 겹 더 (최대 MaxLayers겹). 아래 카드일수록 오른쪽 아래로 비켜 보인다.
                int layers = count <= 1 ? 0 : Mathf.Min(MaxLayers, 1 + count / 10);
                for (int k = layers; k >= 1; k--)
                {
                    var layer = new VisualElement { pickingMode = PickingMode.Ignore };
                    layer.style.position = Position.Absolute;
                    layer.style.left = layer.style.top = k * _step;
                    layer.style.width = _card.x;
                    layer.style.height = _card.y;
                    layer.style.backgroundColor = top == null ? PileBack : PileFace;
                    UiStyle.BorderColor(layer, top == null ? PileBackEdge : UiStyle.Border, 1);
                    UiStyle.Radius(layer, 4 * _scale);
                    _layers.Add(layer);
                }

                var faces = _screen._faces;
                var face = back && (top == null || !visible) ? faces.BuildBack(CardFaceSize.Field)
                    : faces.Build(top.Code, CardFaceSize.Field);
                face.pickingMode = PickingMode.Ignore;
                if (faceDown && visible)
                    face.style.opacity = 0.55f; // 나의 뒷면 제외 카드
                if (!Mathf.Approximately(_scale, 1f))
                {
                    face.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
                    face.style.scale = new Scale(new Vector2(_scale, _scale));
                }
                _top.Add(face);
            }

            static VisualElement Icon(PileKind kind)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.style.marginRight = 5;
                icon.style.flexShrink = 0;
                switch (kind)
                {
                    case PileKind.Grave: // 묘비
                        icon.style.width = 10;
                        icon.style.height = 12;
                        icon.style.backgroundColor = UiStyle.TextDim;
                        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius = 5;
                        icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 1;
                        break;
                    case PileKind.Removed: // 원
                        icon.style.width = icon.style.height = 11;
                        UiStyle.Radius(icon, 6);
                        UiStyle.BorderColor(icon, UiStyle.TextDim, 2);
                        break;
                    default: // 카드
                        icon.style.width = 9;
                        icon.style.height = 12;
                        icon.style.backgroundColor = kind == PileKind.Extra ? UiStyle.ExtraMonster : PileBack;
                        UiStyle.Radius(icon, 2);
                        UiStyle.BorderColor(icon, UiStyle.TextDim, 1);
                        break;
                }
                return icon;
            }
        }
    }
}
