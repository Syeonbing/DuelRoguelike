// 카드 앞면 (UI Toolkit). 일러스트 없이 직접 디자인한 텍스트 카드 템플릿이다 (설계 문서 "개요": 공식 카드 레이아웃은 따르지 않는다).
// - 틀 색: 카드 종류 (일반 몬스터, 효과 몬스터, 융합·의식·싱크로·엑시즈, 마법, 함정). 듀얼 화면의 카드 색과 같다.
// - 위: 카드명. 가운데 그림 칸: 레벨(랭크)·속성·종족·종류, 마법·함정은 종류. 아래: ATK / DEF.
// - 그림 칸 안 모서리: 오른쪽 위 등급, 오른쪽 아래 장수(×n), 왼쪽 위 짧은 표시(예: "새 카드").
// - 그림 칸: 게임 폴더(에디터에서는 프로젝트 폴더)의 CardImages\카드ID.png 또는 .jpg가 있으면 그 그림을 보여 준다 (CardImages).
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
        static readonly Color Ink = UiStyle.Hex(0xf6efe2);
        static readonly Color InkDim = UiStyle.Hex(0xd9cdb8);
        static readonly Color Cyan = UiStyle.Hex(0x4fd1e8);
        static readonly Color Up = UiStyle.Hex(0x5ee07a);
        static readonly Color Down = UiStyle.Hex(0xff7a6a);
        static readonly Color BackColor = UiStyle.Hex(0x263042);
        static readonly Color BackLine = UiStyle.Hex(0x4a5a78);
        static readonly Color BackMark = UiStyle.Hex(0xb08a3a);

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

        static float ScaleOf(CardFaceSize size)
        {
            switch (size)
            {
                case CardFaceSize.Small: return 1f;
                case CardFaceSize.Medium: return 1.25f;
                case CardFaceSize.Field: return 1.03f;
                default: return 2.4f;
            }
        }

        /// <summary>
        /// 카드 앞면 하나. count가 2 이상이면 ×n, tag가 있으면 왼쪽 위에 짧은 표시.
        /// stats가 있으면 ATK/DEF를 그 값으로 쓴다 (듀얼 중 바뀐 값).
        /// </summary>
        public VisualElement Build(uint code, CardFaceSize size, int count = 0, string tag = null, CardStats? stats = null)
        {
            var record = _text.Record(code);
            var type = record?.Type ?? OcgCardType.None;
            var frame = FrameColor(type);
            var dims = SizeOf(size);
            float scale = ScaleOf(size);

            var card = new VisualElement { name = "card-face" };
            card.pickingMode = PickingMode.Position;
            card.style.width = dims.x;
            card.style.height = dims.y;
            card.style.flexShrink = 0;
            card.style.backgroundColor = frame;
            UiStyle.Radius(card, 4 * scale);
            UiStyle.Padding(card, 4 * scale);
            UiStyle.BorderColor(card, Color.Lerp(frame, Color.black, 0.45f), 1);

            var name = Text(_text.CardName(code), Mathf.RoundToInt(11 * scale), Ink, bold: true);
            name.style.height = Mathf.Round(28 * scale);
            name.style.overflow = Overflow.Hidden;
            name.style.flexShrink = 0;
            card.Add(name);

            var art = new VisualElement();
            art.style.flexGrow = 1;
            art.style.marginTop = art.style.marginBottom = 2 * scale;
            art.style.backgroundColor = Color.Lerp(frame, Color.black, 0.35f);
            art.style.justifyContent = Justify.Center;
            art.style.alignItems = Align.Center;
            UiStyle.Radius(art, 3 * scale);
            art.pickingMode = PickingMode.Ignore;
            var image = _images?.Get(code);
            bool spell = (type & OcgCardType.Spell) != 0, trap = (type & OcgCardType.Trap) != 0;
            if (image != null)
            {
                art.style.backgroundImage = new StyleBackground(image);
                art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }
            else if (record != null && (spell || trap))
            {
                art.Add(Centered(spell ? "마법" : "함정", Mathf.RoundToInt(15 * scale), Ink, bold: true));
                art.Add(Centered(SubType(type), Mathf.RoundToInt(10 * scale), InkDim));
            }
            else if (record != null)
            {
                var data = record.Data;
                bool xyz = (type & OcgCardType.Xyz) != 0;
                art.Add(Centered((xyz ? "☆" : "★") + data.Level, Mathf.RoundToInt(16 * scale), Ink, bold: true));
                art.Add(Centered(DuelText.Attribute((OcgAttribute)data.Attribute), Mathf.RoundToInt(10 * scale), InkDim));
                art.Add(Centered(DuelText.Race(data.Race), Mathf.RoundToInt(10 * scale), InkDim));
                if (size != CardFaceSize.Small)
                    art.Add(Centered(MonsterKind(type), Mathf.RoundToInt(10 * scale), InkDim));
            }
            card.Add(art);

            var foot = new VisualElement();
            foot.style.flexDirection = FlexDirection.Row;
            foot.style.justifyContent = Justify.SpaceBetween;
            foot.style.flexShrink = 0;
            foot.pickingMode = PickingMode.Ignore;
            if (record != null && !spell && !trap)
            {
                int stat = Mathf.RoundToInt(11 * scale);
                if (stats.HasValue)
                {
                    var s = stats.Value;
                    foot.Add(Text(Stat(s.Attack), stat, Compare(s.Attack, s.BaseAttack, Ink), bold: true));
                    foot.Add(Text(Stat(s.Defense), stat, Compare(s.Defense, s.BaseDefense, InkDim), bold: true));
                }
                else
                {
                    foot.Add(Text(Stat(record.Data.Attack), stat, Ink, bold: true));
                    foot.Add(Text(Stat(record.Data.Defense), stat, InkDim, bold: true));
                }
            }
            else if (image != null && record != null)
                foot.Add(Text(SubType(type) + (spell ? " 마법" : " 함정"), Mathf.RoundToInt(10 * scale), InkDim));
            card.Add(foot);

            // 표시는 그림 칸의 모서리 안에 둔다 (카드명·ATK/DEF를 가리지 않게)
            float inset = 2 * scale;
            if (_rarities != null && _rarities.TryGet(code, out var rarity))
                art.Add(Badge(rarity.ToString(), RarityColor(rarity), Position.Absolute, top: inset, right: inset, size: Mathf.RoundToInt(10 * scale)));
            if (count > 1)
                art.Add(Badge("×" + count, UiStyle.Text, Position.Absolute, bottom: inset, right: inset, size: Mathf.RoundToInt(11 * scale)));
            if (!string.IsNullOrEmpty(tag))
                art.Add(Badge(tag, UiStyle.Hex(0x5ee07a), Position.Absolute, top: inset, left: inset, size: Mathf.RoundToInt(10 * scale)));
            return card;
        }

        static Color Compare(int value, int original, Color same) => value > original ? Up : value < original ? Down : same;

        /// <summary>
        /// 카드 뒷면 (직접 디자인: 어두운 바탕, 안쪽 테두리, 가운데 마름모). 공식 카드 뒷면은 따르지 않는다.
        /// </summary>
        public VisualElement BuildBack(CardFaceSize size)
        {
            var dims = SizeOf(size);
            float scale = ScaleOf(size);
            var card = new VisualElement { name = "card-back" };
            card.style.width = dims.x;
            card.style.height = dims.y;
            card.style.flexShrink = 0;
            card.style.backgroundColor = BackColor;
            UiStyle.Radius(card, 4 * scale);
            UiStyle.Padding(card, 4 * scale);
            UiStyle.BorderColor(card, Color.Lerp(BackColor, Color.black, 0.45f), 1);

            var inner = new VisualElement { pickingMode = PickingMode.Ignore };
            inner.style.flexGrow = 1;
            inner.style.justifyContent = Justify.Center;
            inner.style.alignItems = Align.Center;
            UiStyle.Radius(inner, 3 * scale);
            UiStyle.BorderColor(inner, BackLine, Mathf.Max(1, 1.5f * scale));

            var mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.style.width = mark.style.height = Mathf.Round(dims.x * 0.32f);
            mark.style.rotate = new StyleRotate(new Rotate(new Angle(45f, AngleUnit.Degree)));
            mark.style.backgroundColor = Color.Lerp(BackColor, Color.black, 0.25f);
            UiStyle.BorderColor(mark, BackMark, Mathf.Max(1, 2f * scale));
            inner.Add(mark);
            card.Add(inner);
            return card;
        }

        public static Color FrameColor(OcgCardType type)
        {
            if ((type & OcgCardType.Spell) != 0) return UiStyle.Spell;
            if ((type & OcgCardType.Trap) != 0) return UiStyle.Trap;
            if ((type & (OcgCardType.Fusion | OcgCardType.Synchro | OcgCardType.Xyz | OcgCardType.Ritual)) != 0) return UiStyle.ExtraMonster;
            if ((type & OcgCardType.Normal) != 0) return UiStyle.NormalMonster;
            if (type == OcgCardType.None) return UiStyle.FaceDown;
            return UiStyle.EffectMonster;
        }

        public static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.R: return UiStyle.Text;
                case Rarity.SR: return Cyan;
                case Rarity.UR: return UiStyle.Accent;
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

        static string MonsterKind(OcgCardType type)
        {
            if ((type & OcgCardType.Fusion) != 0) return "융합";
            if ((type & OcgCardType.Ritual) != 0) return "의식";
            if ((type & OcgCardType.Synchro) != 0) return "싱크로";
            if ((type & OcgCardType.Xyz) != 0) return "엑시즈";
            if ((type & OcgCardType.Normal) != 0) return "일반";
            return "효과";
        }

        static string Stat(int value) => value < 0 ? "?" : value.ToString();

        static Label Text(string text, int size, Color color, bool bold = false)
        {
            var label = UiStyle.Label(text, size, color, bold);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        static Label Centered(string text, int size, Color color, bool bold = false)
        {
            var label = Text(text, size, color, bold);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        static Label Badge(string text, Color color, Position position, float? top = null, float? right = null, float? bottom = null,
            float? left = null, int size = 10)
        {
            var badge = Text(text, size, color, bold: true);
            badge.style.position = position;
            if (top.HasValue) badge.style.top = top.Value;
            if (right.HasValue) badge.style.right = right.Value;
            if (bottom.HasValue) badge.style.bottom = bottom.Value;
            if (left.HasValue) badge.style.left = left.Value;
            badge.style.backgroundColor = UiStyle.Background;
            UiStyle.BorderColor(badge, color, 1);
            UiStyle.Radius(badge, 3);
            badge.style.paddingLeft = badge.style.paddingRight = 4;
            badge.style.paddingTop = badge.style.paddingBottom = 1;
            return badge;
        }
    }

    /// <summary>
    /// 플레이어가 넣은 카드 그림: 게임 폴더(실행 파일이 있는 폴더, 에디터에서는 프로젝트 폴더)의 CardImages\카드ID.png 또는 .jpg.
    /// 없으면 null (텍스트 카드로 그린다). 한 번 읽은 그림과 없다는 결과는 기억한다.
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
