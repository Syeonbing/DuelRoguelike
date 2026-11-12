// 화면의 색, 글꼴, 부품 (docs/UI.md "색", "글꼴", "부품").
// 화면 배치는 아직 예전 그대로다. 여기서는 색·글꼴·돌판·돌 버튼만 새 규칙으로 바꿨다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace DuelRoguelike.UI
{
    internal static class UiStyle
    {
        // 돌: 9가 가장 어둡다
        public static readonly Color Stone9 = Hex(0x0e0c0a);
        public static readonly Color Stone8 = Hex(0x1d1915);
        public static readonly Color Stone7 = Hex(0x28231e);
        public static readonly Color Stone6 = Hex(0x383129);
        public static readonly Color Stone5 = Hex(0x564b3f);
        public static readonly Color Stone4 = Hex(0x7c6d5a);
        public static readonly Color SlabTop = Hex(0x3d362d);
        public static readonly Color Gold = Hex(0xe5bc62);
        public static readonly Color GoldDeep = Hex(0x9d7733);
        public static readonly Color Good = Hex(0x80dd8d);
        public static readonly Color Bad = Hex(0xf1654f);
        public static readonly Color Paper = Hex(0xefe6cf);
        public static readonly Color PaperInk = Hex(0x241b10);

        public static readonly Color Background = Hex(0x14110e);
        public static readonly Color Panel = Stone8;
        public static readonly Color PanelLight = Stone7;
        public static readonly Color Border = Stone5;
        public static readonly Color Text = Hex(0xf6efde);
        public static readonly Color TextDim = Hex(0xc2b6a0);
        public static readonly Color Mine = Hex(0x7fbaff);
        public static readonly Color Opponent = Hex(0xff8e70);
        public static readonly Color Accent = Gold;
        public static readonly Color ZoneEmpty = Stone9;
        // 카드 틀 색 (CardFaces가 종류별 색 묶음을 따로 갖는다. 여기 값은 틀의 바탕색이다)
        public static readonly Color NormalMonster = Hex(0xb78f47);
        public static readonly Color EffectMonster = Hex(0xbf6c38);
        public static readonly Color ExtraMonster = Hex(0x8767c2);
        public static readonly Color Spell = Hex(0x2e9a80);
        public static readonly Color Trap = Hex(0xb14e7d);
        public static readonly Color FaceDown = Hex(0x272c42);
        public static readonly Color ButtonColor = Stone6;
        public static readonly Color ButtonHoverColor = Hex(0x473f35);
        public static readonly Color ButtonSelectedColor = Hex(0x7a5c1e);

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>
        /// 화면의 키 표시가 따를 자리: 마지막에 쓴 이동 키가 W A S D면 true (결정·취소를 J·K로 쓴다), 방향키면 false (Z·X).
        /// 런 화면과 듀얼 화면이 함께 쓴다.
        /// </summary>
        public static bool WasdLayout;

        public static VisualElement Box(string name = null, FlexDirection direction = FlexDirection.Column)
        {
            var e = new VisualElement { name = name };
            e.style.flexDirection = direction;
            return e;
        }

        public static Label Label(string text, int size = 18, Color? color = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color ?? Text;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            if (bold) Bold(l);
            return l;
        }

        /// <summary>
        /// 돌 버튼: 위쪽 모서리는 밝고 아래쪽 테두리는 두꺼워서 두께가 있어 보인다. 누르면 조금 내려간다.
        /// </summary>
        /// <summary>누르면 결정 소리를 내고 onClick을 부른다 (마우스·결정 키·화면 시험 모두). 버튼을 만드는 곳은 이것으로 감싼다.</summary>
        public static System.Action Clicked(System.Action onClick) => () =>
        {
            Sound.Play(Sfx.Confirm);
            onClick?.Invoke();
        };

        public static Button Button(string text, System.Action onClick, int size = 17)
        {
            var b = new Button(Clicked(onClick)) { text = text };
            b.style.fontSize = size;
            b.style.color = Text;
            b.style.backgroundColor = ButtonColor;
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.marginTop = b.style.marginBottom = 3;
            b.style.marginLeft = b.style.marginRight = 0;
            b.style.paddingTop = b.style.paddingBottom = 5;
            b.style.paddingLeft = b.style.paddingRight = 10;
            Radius(b, 6);
            BorderColor(b, Stone9, 2);
            b.style.borderTopColor = Stone4;
            b.style.borderBottomWidth = 4;
            Bold(b);
            b.RegisterCallback<MouseEnterEvent>(_ => { if (!b.ClassListContains("selected")) b.style.backgroundColor = ButtonHoverColor; });
            b.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (!b.ClassListContains("selected")) b.style.backgroundColor = ButtonColor;
                b.style.translate = StyleKeyword.Null;
            });
            b.RegisterCallback<PointerDownEvent>(_ => b.style.translate = new Translate(0, 2), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => b.style.translate = StyleKeyword.Null, TrickleDown.TrickleDown);
            return b;
        }

        static readonly Color GoldInk = Hex(0x22180a), GoldEdge = Hex(0x5f4519), GoldTop = Hex(0xf6dc9b), GoldHover = Hex(0xf0cb78);

        /// <summary>
        /// 돌 버튼 (docs/UI.md "부품"): 높이 64, 글자 26 Bold, 가운데 정렬. small이면 높이 48, 글자 19.
        /// gold면 그 화면에서 가장 중요한 버튼. 키보드 포커스는 받지 않는다 (키보드는 화면이 직접 읽는다).
        /// </summary>
        public static Button StoneButton(string text, System.Action onClick, bool gold = false, bool small = false)
        {
            var b = new Button(Clicked(onClick)) { text = text, focusable = false };
            Color face = gold ? Gold : ButtonColor, hover = gold ? GoldHover : ButtonHoverColor;
            b.style.fontSize = small ? 19 : 26;
            b.style.color = gold ? GoldInk : Text;
            b.style.backgroundColor = face;
            b.style.height = small ? 48 : 64;
            b.style.flexShrink = 0;
            b.style.whiteSpace = WhiteSpace.NoWrap;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            Margin(b, 0);
            b.style.paddingTop = b.style.paddingBottom = 0;
            b.style.paddingLeft = b.style.paddingRight = small ? 12 : 24;
            Radius(b, 8);
            BorderColor(b, gold ? GoldEdge : Stone9, 2);
            b.style.borderTopColor = gold ? GoldTop : Stone4;
            b.style.borderBottomWidth = small ? 5 : 7;
            Bold(b);
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = hover);
            b.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                b.style.backgroundColor = face;
                b.style.translate = StyleKeyword.Null;
            });
            b.RegisterCallback<PointerDownEvent>(_ => b.style.translate = new Translate(0, 4), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => b.style.translate = StyleKeyword.Null, TrickleDown.TrickleDown);
            return b;
        }

        /// <summary>키 표시: 버튼과 도움말 줄에 쓰는 작은 키 모양.</summary>
        public static Label KeyCap(string text)
        {
            var l = Label(text, 15, TextDim, bold: true);
            l.style.backgroundColor = Stone9;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.paddingLeft = l.style.paddingRight = 7;
            l.style.paddingTop = l.style.paddingBottom = 1;
            l.style.flexShrink = 0;
            Radius(l, 5);
            BorderColor(l, Stone5, 2);
            l.style.borderBottomWidth = 4;
            return l;
        }

        /// <summary>요소를 부모 안의 정해진 자리에 놓는다 (1920×1080 기준 좌표로 짠 화면용).</summary>
        public static T Place<T>(T e, float left, float top, float width = float.NaN, float height = float.NaN) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            e.style.left = left;
            e.style.top = top;
            if (!float.IsNaN(width)) e.style.width = width;
            if (!float.IsNaN(height)) e.style.height = height;
            return e;
        }

        public static void SetSelected(Button b, bool selected)
        {
            b.EnableInClassList("selected", selected);
            b.style.backgroundColor = selected ? ButtonSelectedColor : ButtonColor;
        }

        public static void Padding(VisualElement e, float all)
        {
            e.style.paddingLeft = e.style.paddingRight = e.style.paddingTop = e.style.paddingBottom = all;
        }

        public static void Margin(VisualElement e, float all)
        {
            e.style.marginLeft = e.style.marginRight = e.style.marginTop = e.style.marginBottom = all;
        }

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        public static void BorderColor(VisualElement e, Color c, float width)
        {
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
        }

        // ───── 글꼴: Pretendard (SIL OFL 1.1). Assets/UI/Resources/Fonts 에 Regular·Bold·Black 세 굵기가 있다 ─────

        static FontAsset _regular, _bold, _black;
        static bool _fontsTried;

        static void LoadFonts()
        {
            if (_fontsTried && _regular != null)
                return;
            _fontsTried = true;
            // Pretendard에 없는 기호는 Windows 기본 글꼴(맑은 고딕)에서 찾는다
            FontAsset fallback = null;
            var os = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕" }, 32);
            if (os != null)
                fallback = FontAsset.CreateFontAsset(os);
            _regular = LoadFont("Fonts/Pretendard-Regular", fallback);
            _bold = LoadFont("Fonts/Pretendard-Bold", fallback);
            _black = LoadFont("Fonts/Pretendard-Black", fallback);
            if (_regular == null)
            {
                Debug.LogWarning("Assets/UI/Resources/Fonts 의 Pretendard 글꼴을 찾지 못했습니다. Windows 기본 글꼴을 씁니다.");
                _regular = fallback;
            }
            // 굵게(Bold) 스타일을 쓰는 글자도 진짜 Bold 글꼴로 그리게 한다
            if (_regular != null && _bold != null && _regular.fontWeightTable != null && _regular.fontWeightTable.Length > 7)
                _regular.fontWeightTable[7].regularTypeface = _bold;
        }

        static FontAsset LoadFont(string path, FontAsset fallback)
        {
            var font = Resources.Load<Font>(path);
            if (font == null)
                return null;
            var asset = FontAsset.CreateFontAsset(font);
            if (asset == null)
                return null;
            asset.name = font.name;
            if (fallback != null)
            {
                if (asset.fallbackFontAssetTable == null)
                    asset.fallbackFontAssetTable = new List<FontAsset>();
                asset.fallbackFontAssetTable.Add(fallback);
            }
            return asset;
        }

        static void SetFont(VisualElement e, FontAsset font)
        {
            if (font != null)
                e.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
        }

        /// <summary>Bold(700) 글꼴로 쓴다.</summary>
        public static void Bold(VisualElement e)
        {
            LoadFonts();
            if (_bold != null) SetFont(e, _bold);
            else e.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        /// <summary>Black(900) 글꼴로 쓴다. 큰 숫자와 제목에 쓴다.</summary>
        public static void Black(VisualElement e)
        {
            LoadFonts();
            if (_black != null) SetFont(e, _black);
            else Bold(e);
        }

        // ───── 실행 중에 만드는 바탕 그림 ─────

        static Texture2D _grain, _radial;

        /// <summary>
        /// 돌 질감: 96×96 무작위 검은 점 (투명도 9% 이하). 타일로 깐다.
        /// 밝은 점은 쓰지 않는다. 이 프로젝트는 선형 색 공간이라, 어두운 바탕에 밝은 점을 조금만 섞어도 눈에는 훨씬 밝게 튄다.
        /// </summary>
        public static Texture2D Grain
        {
            get
            {
                if (_grain != null)
                    return _grain;
                const int n = 96;
                var pixels = new Color32[n * n];
                var random = new System.Random(7);
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(0, 0, 0, (byte)random.Next(0, 24));
                _grain = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "ui-grain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                _grain.SetPixels32(pixels);
                _grain.Apply();
                return _grain;
            }
        }

        /// <summary>가운데가 진하고 바깥으로 사라지는 흰 원. 색을 입혀 카드 그림 칸의 바탕으로 쓴다.</summary>
        public static Texture2D Radial
        {
            get
            {
                if (_radial != null)
                    return _radial;
                const int n = 64;
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.54f; // 텍스처는 아래가 0이라 가운데를 조금 위(화면 기준)로 올린다
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 0.51f);
                        pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
                    }
                _radial = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "ui-radial", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                _radial.SetPixels32(pixels);
                _radial.Apply();
                return _radial;
            }
        }

        public static void GrainOn(VisualElement e)
        {
            e.style.backgroundImage = new StyleBackground(Grain);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            e.style.backgroundSize = new BackgroundSize(new Length(96), new Length(96));
        }

        // ───── 부품 ─────

        const float SlabBorder = 3, SlabLineInset = 5, SlabLineWidth = 2;
        const string SlabClass = "slab";

        /// <summary>
        /// 돌판: 창과 패널의 틀. 어두운 돌 바탕에 잡티 질감, 두꺼운 테두리(위쪽만 밝게), 안쪽에 금색 선.
        /// 금색 선은 요소 자신이 그린다 (자식 요소가 아니라서 Clear()로 지워지지 않는다).
        /// </summary>
        public static void Slab(VisualElement e)
        {
            e.AddToClassList(SlabClass);
            e.style.backgroundColor = Panel;
            GrainOn(e);
            Radius(e, 0);
            BorderColor(e, Stone9, SlabBorder);
            e.style.borderTopColor = SlabTop;
            e.generateVisualContent += DrawSlabLine;
        }

        static void DrawSlabLine(MeshGenerationContext context)
        {
            var size = context.visualElement.layout.size;
            // 작은 상자에는 선을 긋지 않는다 (목록의 줄 같은 곳이 지저분해진다)
            if (float.IsNaN(size.x) || size.x < 140 || size.y < 70)
                return;
            // 돌판 안에 든 돌판에도 긋지 않는다. 금색 선은 가장 바깥 틀에만 둔다.
            for (var parent = context.visualElement.hierarchy.parent; parent != null; parent = parent.hierarchy.parent)
                if (parent.ClassListContains(SlabClass))
                    return;
            float d = SlabBorder + SlabLineInset + SlabLineWidth / 2;
            var p = context.painter2D;
            // 선형 색 공간에서는 반투명한 밝은 색이 더 밝게 보여서, 문서의 30%보다 낮춘 값을 쓴다
            p.strokeColor = Alpha(Gold, 0.16f);
            p.lineWidth = SlabLineWidth;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            p.MoveTo(new Vector2(d, d));
            p.LineTo(new Vector2(size.x - d, d));
            p.LineTo(new Vector2(size.x - d, size.y - d));
            p.LineTo(new Vector2(d, size.y - d));
            p.ClosePath();
            p.Stroke();
        }

        /// <summary>
        /// 화면 하나(UIDocument)를 parent 아래 게임 오브젝트로 만들고 루트를 돌려준다. 화면마다 따로 만들고, 숨길 때는 루트의 display를 끈다.
        /// 기준 해상도 1920×1080, 유니티 기본 런타임 테마, 글꼴 Pretendard.
        /// </summary>
        public static VisualElement CreateDocument(Transform parent, string name)
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = name + "Panel";
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            // 유니티 기본 런타임 테마 (Assets/UI/Resources/DuelTheme.tss). 없으면 버튼·스크롤바 기본 모양이 빠진다.
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("DuelTheme");
            if (panel.themeStyleSheet == null)
                Debug.LogWarning("Assets/UI/Resources/DuelTheme.tss를 찾지 못했습니다.");

            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            go.SetActive(true);

            var docRoot = document.rootVisualElement;
            LoadFonts();
            SetFont(docRoot, _regular);
            docRoot.style.color = UiStyle.Text;
            return docRoot;
        }

        public static VisualElement PanelBox(string name = null)
        {
            var e = Box(name);
            Slab(e);
            Padding(e, 10);
            return e;
        }
    }
}
