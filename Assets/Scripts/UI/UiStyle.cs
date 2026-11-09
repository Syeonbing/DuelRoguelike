// 최소 듀얼 화면의 색과 요소 만들기 도우미. 정식 화면 디자인 전까지 쓰는 임시 스타일이다.
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    internal static class UiStyle
    {
        public static readonly Color Background = Hex(0x161a21);
        public static readonly Color Panel = Hex(0x222833);
        public static readonly Color PanelLight = Hex(0x2c3441);
        public static readonly Color Border = Hex(0x3d4757);
        public static readonly Color Text = Hex(0xe8e8e8);
        public static readonly Color TextDim = Hex(0x9aa3b2);
        public static readonly Color Mine = Hex(0x4a90e2);
        public static readonly Color Opponent = Hex(0xe2664a);
        public static readonly Color Accent = Hex(0xf0c040);
        public static readonly Color ZoneEmpty = Hex(0x1c2129);
        public static readonly Color NormalMonster = Hex(0x6b5a35);
        public static readonly Color EffectMonster = Hex(0x7a4a2a);
        public static readonly Color ExtraMonster = Hex(0x4a3a6b);
        public static readonly Color Spell = Hex(0x26614f);
        public static readonly Color Trap = Hex(0x6b2a5a);
        public static readonly Color FaceDown = Hex(0x3a3f4a);
        public static readonly Color ButtonColor = Hex(0x34405a);
        public static readonly Color ButtonHoverColor = Hex(0x45557a);
        public static readonly Color ButtonSelectedColor = Hex(0x2f7a4a);

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

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
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        public static Button Button(string text, System.Action onClick, int size = 17)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize = size;
            b.style.color = Text;
            b.style.backgroundColor = ButtonColor;
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.marginTop = b.style.marginBottom = 3;
            b.style.marginLeft = b.style.marginRight = 0;
            b.style.paddingTop = b.style.paddingBottom = 6;
            b.style.paddingLeft = b.style.paddingRight = 10;
            Radius(b, 4);
            BorderColor(b, Border, 1);
            b.RegisterCallback<MouseEnterEvent>(_ => { if (!b.ClassListContains("selected")) b.style.backgroundColor = ButtonHoverColor; });
            b.RegisterCallback<MouseLeaveEvent>(_ => { if (!b.ClassListContains("selected")) b.style.backgroundColor = ButtonColor; });
            return b;
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

        static Font _font;

        /// <summary>
        /// 화면 하나(UIDocument)를 parent 아래 게임 오브젝트로 만들고 루트를 돌려준다. 화면마다 따로 만들고, 숨길 때는 루트의 display를 끈다.
        /// 기준 해상도 1920×1080, 유니티 기본 런타임 테마, 한글 글꼴(맑은 고딕).
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
            // 한글 글꼴: Windows 기본 글꼴(맑은 고딕)을 실행 중에 불러온다. 프로젝트에는 글꼴 파일을 넣지 않는다.
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕" }, 32);
            docRoot.style.unityFontDefinition = FontDefinition.FromFont(_font);
            docRoot.style.color = UiStyle.Text;
            return docRoot;
        }

        public static VisualElement PanelBox(string name = null)
        {
            var e = Box(name);
            e.style.backgroundColor = Panel;
            Radius(e, 6);
            Padding(e, 10);
            BorderColor(e, Border, 1);
            return e;
        }
    }
}
