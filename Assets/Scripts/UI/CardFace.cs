// 카드 앞면과 뒷면 (UI Toolkit). 규칙은 docs/UI.md "카드".
// - 공식 카드에서 참조하는 것은 정보 순서(카드명·속성 → 레벨 → 그림 칸 → 종족 줄 → 효과문 → ATK / DEF)와 종류별 색 구분뿐이다.
//   틀 모양, 속성·종류 아이콘, 뒷면은 이 프로젝트에서 직접 그린 것이다.
// - 작은 카드: 108×158 하나로 만들고, 놓는 곳에 따라 배율만 바꾼다 (Build). 필드, 패, 덱 격자에 쓴다.
// - 상세 카드: 세로로 긴 틀에 효과문까지 넣는다 (BuildDetail). 크게 보기와 보상 고르기에 쓴다.
// - 그림 칸: 게임 폴더(에디터에서는 프로젝트 폴더)의 CardImages\카드ID.png 또는 .jpg가 있으면 그 그림을 보여 준다 (CardImages).
//   없으면 직접 그린 픽토그램을 보여 준다 (몬스터는 종족, 마법·함정은 종류).
//   이미지는 게임에 넣지 않고 배포하지도 않는다. 플레이어가 직접 넣는 경우만 읽는다 (저장소에서는 .gitignore로 뺀다).
using System;
using System.Collections.Generic;
using System.IO;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    /// <summary>작은 카드를 놓는 곳. 지금 화면 배치가 쓰는 크기다 (화면을 새로 짜면서 docs/UI.md의 배율로 바꾼다).</summary>
    public enum CardFaceSize
    {
        /// <summary>덱 격자</summary>
        Small,
        /// <summary>컬렉션·보충 후보, 듀얼 손패와 카드 목록 창</summary>
        Medium,
        /// <summary>카드 정보 (크게 보기)</summary>
        Large,
        /// <summary>듀얼 필드의 칸과 카드 더미</summary>
        Field,
    }

    /// <summary>듀얼 중의 지금 공격력·수비력. 원래 값보다 높으면 초록, 낮으면 빨강으로 쓴다.</summary>
    public struct CardStats
    {
        public int Attack, Defense, BaseAttack, BaseDefense;
    }

    public sealed class CardFaces
    {
        /// <summary>작은 카드의 기준 크기. 모든 작은 카드는 이 크기로 만든 뒤 배율로 맞춘다.</summary>
        public const float BaseWidth = 108f, BaseHeight = 158f;

        static readonly Color Ink = UiStyle.Text;
        static readonly Color InkDim = UiStyle.TextDim;
        static readonly Color StarColor = UiStyle.Hex(0xffd75e);
        static readonly Color RankColor = UiStyle.Hex(0xe8e2d4);
        static readonly Color EmblemInk = UiStyle.Hex(0x1b140b);
        static readonly Color PictogramInk = UiStyle.Hex(0xf6efde);
        static readonly Color SpellEmblem = UiStyle.Hex(0x3fb59a);
        static readonly Color TrapEmblem = UiStyle.Hex(0xcf6a9c);
        static readonly Color Shade = new Color(0f, 0f, 0f, 0.66f);
        static readonly Color PaperUp = UiStyle.Hex(0x1c7a35);
        static readonly Color PaperDown = UiStyle.Hex(0xb3261a);
        static readonly Color Cyan = UiStyle.Hex(0x7fe3f0);
        static readonly Color RarityUr = UiStyle.Hex(0xffd260);
        static readonly Color RarityR = UiStyle.Hex(0xd7cfbd);

        /// <summary>종류별 틀 색: 테두리, 틀, 이름 줄, 그림 칸 바탕.</summary>
        readonly struct Frame
        {
            public readonly Color Edge, Fill, Bar, Art;

            public Frame(int edge, int fill, int bar, int art)
            {
                Edge = UiStyle.Hex(edge);
                Fill = UiStyle.Hex(fill);
                Bar = UiStyle.Hex(bar);
                Art = UiStyle.Hex(art);
            }
        }

        static readonly Frame NormalFrame = new Frame(0x46360f, 0xb78f47, 0x6b5222, 0x2a2112);
        static readonly Frame EffectFrame = new Frame(0x4a240d, 0xbf6c38, 0x733916, 0x2b170c);
        static readonly Frame FusionFrame = new Frame(0x2c1c50, 0x8767c2, 0x4a3380, 0x1d1530);
        static readonly Frame RitualFrame = new Frame(0x142c58, 0x5a8dd4, 0x294d8c, 0x101c33);
        // 싱크로·엑시즈 틀 색은 아직 확정 전이다 (docs/UI.md "아직 정하지 않은 것"). 지금 값은 제안이다.
        static readonly Frame SynchroFrame = new Frame(0x3f3e3a, 0xc9c5ba, 0x6f6c65, 0x262523);
        static readonly Frame XyzFrame = new Frame(0x050506, 0x3c3c45, 0x1b1b20, 0x101014);
        static readonly Frame SpellFrame = new Frame(0x0c362d, 0x2e9a80, 0x155548, 0x0d2620);
        static readonly Frame TrapFrame = new Frame(0x461433, 0xb14e7d, 0x6a224e, 0x2a0f20);
        static readonly Frame UnknownFrame = new Frame(0x1a1714, 0x4b443b, 0x2e2923, 0x1d1915);
        static readonly Frame BackFrame = new Frame(0x10121d, 0x272c42, 0x5a6690, 0x1c2032);
        static readonly Color BackMark = UiStyle.Hex(0xc39a45);

        readonly DuelText _text;
        readonly RarityTable _rarities;
        readonly CardImages _images;

        public CardFaces(DuelText text, RarityTable rarities, CardImages images)
        {
            _text = text;
            _rarities = rarities;
            _images = images;
        }

        public static Vector2 SizeOf(CardFaceSize size)
        {
            switch (size)
            {
                case CardFaceSize.Small: return new Vector2(82, 120);
                case CardFaceSize.Medium: return new Vector2(108, 158);
                case CardFaceSize.Field: return new Vector2(84, 122);
                default: return new Vector2(250, 365);
            }
        }

        /// <summary>
        /// 작은 카드 하나. count가 2 이상이면 ×n, tag가 있으면 그림 칸 왼쪽 아래에 짧은 표시.
        /// stats가 있으면 ATK/DEF를 그 값으로 쓴다 (듀얼 중 바뀐 값).
        /// </summary>
        public VisualElement Build(uint code, CardFaceSize size, int count = 0, string tag = null, CardStats? stats = null) =>
            Scaled("card-face", SizeOf(size), BaseCard(code, count, tag, stats));

        /// <summary>
        /// 카드 뒷면 (직접 디자인: 어두운 남색 바탕, 안쪽 테두리, 가운데 금색 마름모). 공식 카드 뒷면은 따르지 않는다.
        /// </summary>
        public VisualElement BuildBack(CardFaceSize size) => Scaled("card-back", SizeOf(size), BaseBack());

        /// <summary>기준 크기(108×158)로 만든 카드를 size 크기의 상자에 넣고 배율로 맞춘다.</summary>
        static VisualElement Scaled(string name, Vector2 size, VisualElement card)
        {
            var root = new VisualElement { name = name };
            root.pickingMode = PickingMode.Position;
            root.style.width = size.x;
            root.style.height = size.y;
            root.style.flexShrink = 0;
            float k = size.x / BaseWidth;
            card.style.position = Position.Absolute;
            card.style.left = 0;
            card.style.top = 0;
            card.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
            card.style.scale = new Scale(new Vector3(k, k, 1f));
            root.Add(card);
            return root;
        }

        VisualElement BaseCard(uint code, int count, string tag, CardStats? stats)
        {
            var record = _text.Record(code);
            var type = record?.Type ?? OcgCardType.None;
            var frame = FrameOf(type);
            bool spell = (type & OcgCardType.Spell) != 0, trap = (type & OcgCardType.Trap) != 0;
            bool monster = record != null && !spell && !trap;

            var card = new VisualElement();
            card.style.width = BaseWidth;
            card.style.height = BaseHeight;
            card.style.flexShrink = 0;
            card.style.backgroundColor = frame.Fill;
            UiStyle.Radius(card, 7);
            UiStyle.Padding(card, 4);
            UiStyle.BorderColor(card, frame.Edge, 2);

            // 1) 카드명 줄: 카드명과 속성 문장
            var bar = UiStyle.Box(null, FlexDirection.Row);
            bar.style.height = 24;
            bar.style.flexShrink = 0;
            bar.style.alignItems = Align.Center;
            bar.style.backgroundColor = frame.Bar;
            bar.style.paddingLeft = 4;
            bar.style.paddingRight = 2;
            UiStyle.Radius(bar, 3);
            bar.Add(NameBox(_text.CardName(code), 15, 24, Ink, black: false));
            var emblem = Emblem(record, type, 19);
            if (emblem != null)
                bar.Add(emblem);
            card.Add(bar);

            // 2) 그림 칸: 플레이어가 넣은 그림 또는 픽토그램. 모서리에 레벨·등급·장수·표시.
            var art = ArtBox(code, record, type, frame, 3, 1);
            art.style.flexGrow = 1;
            art.style.marginTop = art.style.marginBottom = 2;
            if (monster)
            {
                bool xyz = (type & OcgCardType.Xyz) != 0;
                var level = Chip(null, 0, Ink);
                level.style.flexDirection = FlexDirection.Row;
                level.style.alignItems = Align.Center;
                level.style.left = 3;
                level.style.top = 3;
                var star = new StarRow(1, xyz ? RankColor : StarColor);
                star.style.width = star.style.height = 12;
                level.Add(star);
                var number = Plain(record.Data.Level.ToString(), 15, xyz ? RankColor : StarColor);
                UiStyle.Black(number);
                number.style.marginLeft = 1;
                level.Add(number);
                art.Add(level);
            }
            if (_rarities != null && _rarities.TryGet(code, out var rarity) && rarity != Rarity.N)
            {
                var chip = Chip(rarity.ToString(), 13, RarityColor(rarity));
                chip.style.right = 3;
                chip.style.top = 3;
                art.Add(chip);
            }
            if (count > 1)
            {
                var chip = Chip("×" + count, 14, Ink);
                chip.style.right = 3;
                chip.style.bottom = 3;
                art.Add(chip);
            }
            if (!string.IsNullOrEmpty(tag))
            {
                var chip = Chip(tag, 12, UiStyle.Good);
                chip.style.left = 3;
                chip.style.bottom = 3;
                art.Add(chip);
            }
            card.Add(art);

            // 3) 수치 줄: 몬스터는 ATK / DEF, 마법·함정은 종류
            var foot = UiStyle.Box(null, FlexDirection.Row);
            foot.style.height = 24;
            foot.style.flexShrink = 0;
            foot.style.alignItems = Align.Center;
            foot.style.overflow = Overflow.Hidden;
            foot.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            foot.style.paddingLeft = foot.style.paddingRight = 5;
            UiStyle.Radius(foot, 3);
            if (monster)
            {
                StatValues(record, stats, out int attack, out int defense, out int baseAttack, out int baseDefense);
                var inner = UiStyle.Box(null, FlexDirection.Row);
                inner.style.flexShrink = 0;
                inner.style.minWidth = Length.Percent(100);
                inner.style.justifyContent = Justify.SpaceBetween;
                inner.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
                var atk = Plain(Stat(attack), 17, Compare(attack, baseAttack, Ink, UiStyle.Good, UiStyle.Bad));
                var def = Plain(Stat(defense), 17, Compare(defense, baseDefense, InkDim, UiStyle.Good, UiStyle.Bad));
                UiStyle.Black(atk);
                UiStyle.Black(def);
                def.style.marginLeft = 7;
                inner.Add(atk);
                inner.Add(def);
                foot.Add(inner);
                SqueezeToFit(foot, inner);
            }
            else if (record != null)
            {
                var kind = Plain(SubType(type) + (spell ? " 마법" : " 함정"), 14, Ink);
                UiStyle.Bold(kind);
                kind.style.flexGrow = 1;
                kind.style.unityTextAlign = TextAnchor.MiddleCenter;
                foot.Add(kind);
            }
            card.Add(foot);

            IgnorePicking(card);
            return card;
        }

        static VisualElement BaseBack()
        {
            var card = new VisualElement();
            card.style.width = BaseWidth;
            card.style.height = BaseHeight;
            card.style.flexShrink = 0;
            card.style.backgroundColor = BackFrame.Fill;
            UiStyle.Radius(card, 7);
            UiStyle.Padding(card, 4);
            UiStyle.BorderColor(card, BackFrame.Edge, 2);

            var inner = new VisualElement();
            inner.style.flexGrow = 1;
            inner.style.justifyContent = Justify.Center;
            inner.style.alignItems = Align.Center;
            UiStyle.Radius(inner, 3);
            UiStyle.BorderColor(inner, BackFrame.Bar, 2);

            var mark = new VisualElement();
            mark.style.width = mark.style.height = 34;
            mark.style.rotate = new StyleRotate(new Rotate(new Angle(45f, AngleUnit.Degree)));
            mark.style.backgroundColor = BackFrame.Art;
            UiStyle.BorderColor(mark, BackMark, 3);
            inner.Add(mark);
            card.Add(inner);
            IgnorePicking(card);
            return card;
        }

        /// <summary>
        /// 상세 카드: 세로로 긴 틀. 위에서부터 카드명·속성, 레벨(마법·함정은 종류), 그림 칸, 글 칸(종족 줄, 효과문, ATK / DEF).
        /// 효과문이 길면 글 칸 안에서 스크롤한다. 높이는 놓는 곳에 맞춘다 (docs/UI.md: 듀얼 752, 덱 창 790, 보상 640).
        /// </summary>
        public VisualElement BuildDetail(uint code, float width, float height, CardStats? stats = null)
        {
            var record = _text.Record(code);
            var type = record?.Type ?? OcgCardType.None;
            var frame = FrameOf(type);
            bool spell = (type & OcgCardType.Spell) != 0, trap = (type & OcgCardType.Trap) != 0;
            bool monster = record != null && !spell && !trap;

            var card = new VisualElement { name = "card-detail-face" };
            card.style.width = width;
            card.style.height = height;
            card.style.flexShrink = 0;
            card.style.backgroundColor = frame.Fill;
            UiStyle.Radius(card, 10);
            UiStyle.Padding(card, 8);
            UiStyle.BorderColor(card, frame.Edge, 3);

            // 1) 카드명 줄
            var bar = UiStyle.Box(null, FlexDirection.Row);
            bar.style.height = 50;
            bar.style.flexShrink = 0;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;
            bar.style.backgroundColor = frame.Bar;
            bar.style.paddingLeft = 12;
            bar.style.paddingRight = 7;
            UiStyle.Radius(bar, 5);
            bar.Add(NameBox(_text.CardName(code), 29, 50, Ink, black: true));
            var emblem = Emblem(record, type, 36);
            if (emblem != null)
                bar.Add(emblem);
            card.Add(bar);

            // 2) 레벨 줄: 별을 레벨 수만큼 오른쪽 정렬. 마법·함정은 종류 그림과 이름.
            var row = UiStyle.Box(null, FlexDirection.Row);
            row.style.height = 26;
            row.style.flexShrink = 0;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginBottom = 6;
            row.style.paddingLeft = row.style.paddingRight = 6;
            if (monster)
            {
                bool xyz = (type & OcgCardType.Xyz) != 0;
                var stars = new StarRow((int)record.Data.Level, xyz ? RankColor : StarColor, alignRight: !xyz);
                stars.style.flexGrow = 1;
                stars.style.height = 22;
                row.Add(stars);
            }
            else if (record != null)
            {
                var icon = new PictogramView(Pictograms.Get((spell ? "마법:" : "함정:") + SubType(type)), Ink);
                icon.style.width = icon.style.height = 22;
                row.Add(icon);
                var kind = Plain(SubType(type) + (spell ? " 마법" : " 함정"), 19, Ink);
                UiStyle.Bold(kind);
                kind.style.marginLeft = 6;
                row.Add(kind);
            }
            card.Add(row);

            // 3) 그림 칸
            float side = Mathf.Max(60f, Mathf.Min(232f, width - 92f));
            var art = ArtBox(code, record, type, frame, 4, 2);
            art.style.width = art.style.height = side;
            art.style.flexShrink = 0;
            art.style.alignSelf = Align.Center;
            art.style.marginBottom = 6;
            card.Add(art);

            // 4) 글 칸: 종족 줄, 효과문, ATK / DEF
            var box = new VisualElement();
            box.style.flexGrow = 1;
            box.style.flexShrink = 1;
            box.style.minHeight = 0;
            box.style.backgroundColor = UiStyle.Paper;
            box.style.paddingLeft = box.style.paddingRight = 12;
            box.style.paddingTop = box.style.paddingBottom = 8;
            UiStyle.Radius(box, 5);
            UiStyle.BorderColor(box, frame.Edge, 2);
            if (monster)
            {
                var line = Plain($"[ {DuelText.Race(record.Data.Race)} / {KindLine(type)} ]", 19, UiStyle.PaperInk);
                UiStyle.Black(line);
                line.style.flexShrink = 0;
                box.Add(line);
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.minHeight = 0;
            scroll.style.marginTop = 2;
            var text = Plain(record?.Description ?? "", 19, UiStyle.PaperInk);
            text.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(text);
            box.Add(scroll);
            if (monster)
            {
                StatValues(record, stats, out int attack, out int defense, out int baseAttack, out int baseDefense);
                var foot = UiStyle.Box(null, FlexDirection.Row);
                foot.style.flexShrink = 0;
                foot.style.justifyContent = Justify.FlexEnd;
                foot.style.overflow = Overflow.Hidden;
                foot.style.marginTop = 4;
                foot.style.paddingTop = 4;
                foot.style.borderTopWidth = 2;
                foot.style.borderTopColor = UiStyle.PaperInk;
                var inner = UiStyle.Box(null, FlexDirection.Row);
                inner.style.flexShrink = 0;
                inner.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(50));
                var atk = Plain("ATK " + Stat(attack), 24, Compare(attack, baseAttack, UiStyle.PaperInk, PaperUp, PaperDown));
                var def = Plain("DEF " + Stat(defense), 24, Compare(defense, baseDefense, UiStyle.PaperInk, PaperUp, PaperDown));
                UiStyle.Black(atk);
                UiStyle.Black(def);
                def.style.marginLeft = 18;
                inner.Add(atk);
                inner.Add(def);
                foot.Add(inner);
                SqueezeToFit(foot, inner);
                box.Add(foot);
            }
            card.Add(box);
            return card;
        }

        // ───── 부분 ─────

        /// <summary>그림 칸. 플레이어가 넣은 그림이 있으면 칸에 꽉 채우고, 없으면 속성 색 바탕 위에 픽토그램을 얹는다.</summary>
        VisualElement ArtBox(uint code, CardRecord record, OcgCardType type, Frame frame, float radius, float border)
        {
            var art = new VisualElement();
            art.style.backgroundColor = frame.Art;
            art.style.justifyContent = Justify.Center;
            art.style.alignItems = Align.Center;
            art.style.overflow = Overflow.Hidden;
            UiStyle.Radius(art, radius);
            UiStyle.BorderColor(art, new Color(0f, 0f, 0f, 0.5f), border);
            var image = _images?.Get(code);
            if (image != null)
            {
                art.style.backgroundImage = new StyleBackground(image);
                art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                return art;
            }
            if (record == null)
                return art;

            bool spell = (type & OcgCardType.Spell) != 0, trap = (type & OcgCardType.Trap) != 0;
            string attribute = DuelText.Attribute((OcgAttribute)record.Data.Attribute);
            Color tint = spell ? SpellEmblem : trap ? TrapEmblem : AttributeColor(attribute);
            var glow = new VisualElement();
            glow.style.position = Position.Absolute;
            glow.style.left = glow.style.top = glow.style.right = glow.style.bottom = 0;
            glow.style.backgroundImage = new StyleBackground(UiStyle.Radial);
            glow.style.unityBackgroundImageTintColor = UiStyle.Alpha(tint, 0.4f);
            art.Add(glow);

            string name = spell ? "마법:" + SubType(type) : trap ? "함정:" + SubType(type) : DuelText.Race(record.Data.Race);
            var pictogram = Pictograms.Get(name) ?? (spell ? Pictograms.Get("마법:일반") : trap ? Pictograms.Get("함정:일반") : null);
            if (pictogram != null)
            {
                var view = new PictogramView(pictogram, PictogramInk);
                view.style.width = view.style.height = Length.Percent(64);
                art.Add(view);
            }
            else
            {
                // 픽토그램이 없는 종족은 글자로 쓴다
                var label = Plain(name, 14, PictogramInk);
                UiStyle.Bold(label);
                art.Add(label);
            }
            return art;
        }

        /// <summary>속성 문장: 속성 색 원 위에 어두운 픽토그램. 마법·함정은 마법·함정 문장.</summary>
        static VisualElement Emblem(CardRecord record, OcgCardType type, float diameter)
        {
            if (record == null)
                return null;
            bool spell = (type & OcgCardType.Spell) != 0, trap = (type & OcgCardType.Trap) != 0;
            string attribute = DuelText.Attribute((OcgAttribute)record.Data.Attribute);
            Color color = spell ? SpellEmblem : trap ? TrapEmblem : AttributeColor(attribute);
            var pictogram = Pictograms.Get(spell ? "마법:일반" : trap ? "함정:일반" : "속성:" + attribute);

            var emblem = new VisualElement();
            emblem.style.width = emblem.style.height = diameter;
            emblem.style.flexShrink = 0;
            emblem.style.marginLeft = 3;
            emblem.style.backgroundColor = color;
            UiStyle.Radius(emblem, diameter / 2);
            UiStyle.BorderColor(emblem, new Color(0f, 0f, 0f, 0.5f), diameter >= 30 ? 2 : 1);
            UiStyle.Padding(emblem, Mathf.Round(diameter * 0.14f));
            var view = new PictogramView(pictogram, EmblemInk);
            view.style.flexGrow = 1;
            emblem.Add(view);
            return emblem;
        }

        static Color AttributeColor(string attribute)
        {
            switch (attribute)
            {
                case "빛": return UiStyle.Hex(0xf3d27a);
                case "어둠": return UiStyle.Hex(0xa98be4);
                case "땅": return UiStyle.Hex(0xc79a66);
                case "물": return UiStyle.Hex(0x66b1f2);
                case "화염": return UiStyle.Hex(0xf17d50);
                case "바람": return UiStyle.Hex(0x76d2a3);
                case "신": return UiStyle.Hex(0xf6e7b0);
                default: return UiStyle.Stone4;
            }
        }

        /// <summary>
        /// 카드명 칸. 칸의 높이는 바뀌지 않는다 (아래 칸이 밀리지 않는다). 넘치면 차례로:
        /// 글자 폭을 62%까지 누른다 → 글자 크기를 80%로 줄이고 폭을 62%까지 누른다 → 같은 칸 안에서 두 줄로 나누고 폭을 50%까지 누른다.
        /// 한 줄에서 62%보다 더 누르면 두 줄로 쓴 것보다 읽기 어렵다.
        /// </summary>
        static VisualElement NameBox(string name, float fontSize, float barHeight, Color color, bool black)
        {
            var box = new VisualElement();
            box.style.flexGrow = 1;
            box.style.flexBasis = 0;
            box.style.height = barHeight;
            box.style.overflow = Overflow.Hidden;
            box.style.justifyContent = Justify.Center;
            var label = NameLabel(name, fontSize, color, black);
            box.Add(label);

            bool fitted = false;
            box.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (fitted)
                    return;
                float width = box.contentRect.width;
                float natural = TextWidth(label, name);
                if (float.IsNaN(width) || width <= 1 || float.IsNaN(natural) || natural <= 0)
                    return;
                fitted = true;
                float k = width / natural;
                if (k >= 1f)
                    return;
                if (k >= 0.62f)
                {
                    SqueezeX(label, k);
                    return;
                }
                float smaller = width / (natural * 0.8f);
                if (smaller >= 0.62f)
                {
                    label.style.fontSize = fontSize * 0.8f;
                    SqueezeX(label, Mathf.Min(1f, smaller));
                    return;
                }
                // 두 줄: 가운데에 가장 가까운 띄어쓰기나 붙임표에서 나눈다 (붙임표는 윗줄 끝에 남긴다)
                int cut = SplitIndex(name);
                string first = name.Substring(0, name[cut] == ' ' ? cut : cut + 1), second = name.Substring(cut + 1);
                float lineFont = Mathf.Round(barHeight * 0.46f);
                float longest = Mathf.Max(TextWidth(label, first), TextWidth(label, second)) * lineFont / fontSize;
                float squeeze = longest > 0 ? Mathf.Clamp(width / longest, 0.5f, 1f) : 1f;
                box.Clear();
                foreach (string part in new[] { first, second })
                {
                    var line = NameLabel(part, lineFont, color, black);
                    line.style.height = barHeight / 2;
                    line.style.unityTextAlign = TextAnchor.MiddleLeft;
                    SqueezeX(line, squeeze);
                    box.Add(line);
                }
            });
            return box;
        }

        static Label NameLabel(string text, float fontSize, Color color, bool black)
        {
            var label = Plain(text, fontSize, color);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.flexShrink = 0;
            label.style.alignSelf = Align.FlexStart;
            label.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            if (black) UiStyle.Black(label);
            else UiStyle.Bold(label);
            return label;
        }

        static int SplitIndex(string name)
        {
            int cut = -1;
            float best = float.MaxValue, middle = name.Length / 2f;
            for (int i = 1; i < name.Length - 1; i++)
            {
                char c = name[i];
                if (c != ' ' && c != '-' && c != '－' && c != '·')
                    continue;
                float distance = Mathf.Abs(i - middle);
                if (distance < best)
                {
                    best = distance;
                    cut = i;
                }
            }
            return cut >= 0 ? cut : Mathf.Max(0, name.Length / 2 - 1);
        }

        static float TextWidth(Label label, string text) =>
            label.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;

        static void SqueezeX(VisualElement e, float k)
        {
            if (k < 0.999f)
                e.style.scale = new Scale(new Vector3(Mathf.Max(0.5f, k), 1f, 1f));
        }

        /// <summary>inner가 outer보다 넓으면 가로로 눌러 맞춘다 (다섯 자리 수치). 가장 좁게는 50%.</summary>
        static void SqueezeToFit(VisualElement outer, VisualElement inner)
        {
            outer.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float width = outer.contentRect.width, natural = inner.layout.width;
                if (float.IsNaN(width) || float.IsNaN(natural) || width <= 1 || natural <= 0)
                    return;
                SqueezeX(inner, width / natural);
            });
        }

        /// <summary>그림 칸 모서리의 작은 표시 (레벨, 등급, 장수, 짧은 표시).</summary>
        static VisualElement Chip(string text, float fontSize, Color color)
        {
            var chip = new VisualElement();
            chip.style.position = Position.Absolute;
            chip.style.height = 18;
            chip.style.justifyContent = Justify.Center;
            chip.style.backgroundColor = Shade;
            chip.style.paddingLeft = chip.style.paddingRight = 4;
            UiStyle.Radius(chip, 3);
            if (text != null)
            {
                var label = Plain(text, fontSize, color);
                UiStyle.Black(label);
                chip.Add(label);
            }
            return chip;
        }

        /// <summary>여백이 없고 서식 글자를 해석하지 않는 글자 요소.</summary>
        static Label Plain(string text, float fontSize, Color color)
        {
            var label = new Label(text);
            label.enableRichText = false;
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            return label;
        }

        static void IgnorePicking(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            root.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
        }

        static void StatValues(CardRecord record, CardStats? stats, out int attack, out int defense, out int baseAttack, out int baseDefense)
        {
            if (stats.HasValue)
            {
                var s = stats.Value;
                attack = s.Attack;
                defense = s.Defense;
                baseAttack = s.BaseAttack;
                baseDefense = s.BaseDefense;
            }
            else
            {
                attack = baseAttack = record.Data.Attack;
                defense = baseDefense = record.Data.Defense;
            }
        }

        static Color Compare(int value, int original, Color same, Color up, Color down) => value > original ? up : value < original ? down : same;

        static Frame FrameOf(OcgCardType type)
        {
            if ((type & OcgCardType.Spell) != 0) return SpellFrame;
            if ((type & OcgCardType.Trap) != 0) return TrapFrame;
            if (type == OcgCardType.None) return UnknownFrame;
            if ((type & OcgCardType.Xyz) != 0) return XyzFrame;
            if ((type & OcgCardType.Synchro) != 0) return SynchroFrame;
            if ((type & OcgCardType.Fusion) != 0) return FusionFrame;
            if ((type & OcgCardType.Ritual) != 0) return RitualFrame;
            if ((type & OcgCardType.Normal) != 0) return NormalFrame;
            return EffectFrame;
        }

        /// <summary>카드 종류의 틀 바탕색.</summary>
        public static Color FrameColor(OcgCardType type) => FrameOf(type).Fill;

        public static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.R: return RarityR;
                case Rarity.SR: return Cyan;
                case Rarity.UR: return RarityUr;
                case Rarity.FR: return UiStyle.Opponent;
                default: return UiStyle.TextDim;
            }
        }

        /// <summary>마법·함정의 세부 종류 (일반, 속공, 지속 …).</summary>
        public static string SubType(OcgCardType type)
        {
            if ((type & OcgCardType.QuickPlay) != 0) return "속공";
            if ((type & OcgCardType.Continuous) != 0) return "지속";
            if ((type & OcgCardType.Equip) != 0) return "장착";
            if ((type & OcgCardType.Field) != 0) return "필드";
            if ((type & OcgCardType.Counter) != 0) return "카운터";
            if ((type & OcgCardType.Ritual) != 0) return "의식";
            return "일반";
        }

        /// <summary>상세 카드의 종족 줄 뒷부분. 예: "일반", "효과", "융합 / 효과", "튜너 / 효과".</summary>
        static string KindLine(OcgCardType type)
        {
            var parts = new List<string>();
            if ((type & OcgCardType.Fusion) != 0) parts.Add("융합");
            if ((type & OcgCardType.Ritual) != 0) parts.Add("의식");
            if ((type & OcgCardType.Synchro) != 0) parts.Add("싱크로");
            if ((type & OcgCardType.Xyz) != 0) parts.Add("엑시즈");
            if ((type & OcgCardType.Toon) != 0) parts.Add("툰");
            if ((type & OcgCardType.Spirit) != 0) parts.Add("스피릿");
            if ((type & OcgCardType.Union) != 0) parts.Add("유니온");
            if ((type & OcgCardType.Gemini) != 0) parts.Add("듀얼");
            if ((type & OcgCardType.Tuner) != 0) parts.Add("튜너");
            if ((type & OcgCardType.Flip) != 0) parts.Add("리버스");
            if ((type & OcgCardType.Effect) != 0) parts.Add("효과");
            else if (parts.Count == 0) parts.Add("일반");
            return string.Join(" / ", parts);
        }

        static string Stat(int value) => value < 0 ? "?" : value.ToString();
    }

    /// <summary>
    /// 플레이어가 넣은 카드 그림: 게임 폴더(실행 파일이 있는 폴더, 에디터에서는 프로젝트 폴더)의 CardImages\카드ID.png 또는 .jpg.
    /// 없으면 null (픽토그램을 그린다). 한 번 읽은 그림과 없다는 결과는 기억한다.
    /// </summary>
    public sealed class CardImages
    {
        public const string FolderName = "CardImages";

        readonly string _folder;
        readonly Dictionary<uint, Texture2D> _cache = new Dictionary<uint, Texture2D>();

        public CardImages(string folder) => _folder = folder;

        /// <summary>게임 폴더의 CardImages (Application.dataPath의 부모 = 실행 파일 폴더, 에디터에서는 프로젝트 폴더).</summary>
        public static CardImages FromGameFolder() =>
            new CardImages(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), FolderName));

        public string Folder => _folder;

        public bool FolderExists => Directory.Exists(_folder);

        public Texture2D Get(uint code)
        {
            if (_cache.TryGetValue(code, out var cached))
                return cached;
            Texture2D texture = null;
            if (FolderExists)
            {
                foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
                {
                    string path = Path.Combine(_folder, code + ext);
                    if (!File.Exists(path))
                        continue;
                    try
                    {
                        var t = new Texture2D(2, 2) { name = code.ToString() };
                        if (t.LoadImage(File.ReadAllBytes(path)))
                        {
                            texture = t;
                            break;
                        }
                        UnityEngine.Object.Destroy(t);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"카드 그림을 읽지 못했습니다 ({path}): {e.Message}");
                    }
                }
            }
            _cache[code] = texture;
            return texture;
        }
    }
}
