// 벡터 그림을 그리는 요소들 (Painter2D): 픽토그램, 레벨 별.
// 픽토그램 자료는 Assets/UI/Resources/Pictograms.txt (48×48 좌표, 직접 그린 그림). 읽는 쪽은 PictogramData.cs.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    /// <summary>이름으로 픽토그램을 찾는다. 예: "마법사족", "마법:속공", "함정:카운터", "속성:어둠".</summary>
    internal static class Pictograms
    {
        static Dictionary<string, Pictogram> _all;

        public static Pictogram Get(string name)
        {
            if (_all == null)
            {
                var asset = Resources.Load<TextAsset>("Pictograms");
                if (asset == null)
                    Debug.LogWarning("Assets/UI/Resources/Pictograms.txt를 찾지 못했습니다. 카드 그림 칸에 글자를 씁니다.");
                _all = asset != null ? PictogramParser.Parse(asset.text) : new Dictionary<string, Pictogram>();
            }
            return name != null && _all.TryGetValue(name, out var pictogram) ? pictogram : null;
        }
    }

    /// <summary>픽토그램 하나를 한 가지 색으로 그린다. 요소 크기에 맞춰 가운데에 놓는다.</summary>
    internal sealed class PictogramView : VisualElement
    {
        const float Grid = 48f;

        readonly Pictogram _pictogram;
        readonly Color _color;

        public PictogramView(Pictogram pictogram, Color color)
        {
            _pictogram = pictogram;
            _color = color;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            if (_pictogram == null)
                return;
            var rect = contentRect;
            float s = Mathf.Min(rect.width, rect.height) / Grid;
            if (float.IsNaN(s) || s <= 0)
                return;
            float ox = rect.x + (rect.width - Grid * s) / 2, oy = rect.y + (rect.height - Grid * s) / 2;
            var p = context.painter2D;
            foreach (var shape in _pictogram.Shapes)
            {
                var points = shape.Path.Points;
                int i = 0;
                p.BeginPath();
                foreach (char op in shape.Path.Ops)
                {
                    switch (op)
                    {
                        case 'M': p.MoveTo(At(points, i, ox, oy, s)); i += 2; break;
                        case 'L': p.LineTo(At(points, i, ox, oy, s)); i += 2; break;
                        case 'C':
                            p.BezierCurveTo(At(points, i, ox, oy, s), At(points, i + 2, ox, oy, s), At(points, i + 4, ox, oy, s));
                            i += 6;
                            break;
                        case 'Q':
                            p.QuadraticCurveTo(At(points, i, ox, oy, s), At(points, i + 2, ox, oy, s));
                            i += 4;
                            break;
                        case 'Z': p.ClosePath(); break;
                    }
                }
                if (shape.Stroke)
                {
                    p.strokeColor = _color;
                    p.lineWidth = shape.Width * s;
                    p.lineCap = LineCap.Round;
                    p.lineJoin = LineJoin.Round;
                    p.Stroke();
                }
                else
                {
                    p.fillColor = _color;
                    p.Fill(FillRule.OddEven);
                }
            }
        }

        static Vector2 At(List<float> points, int i, float ox, float oy, float s) => new Vector2(ox + points[i] * s, oy + points[i + 1] * s);
    }

    /// <summary>
    /// 동전: 앞면은 금색 바탕에 "앞", 뒷면은 돌 바탕에 "뒤". SetSpin으로 돌린다 (가로 폭이 접혔다 펴지며 면이 바뀐다).
    /// 듀얼 전 코인 토스와 듀얼 중 카드 효과의 코인 토스에 쓴다.
    /// </summary>
    internal sealed class CoinView : VisualElement
    {
        static readonly Color FrontInk = UiStyle.Hex(0x22180a), FrontEdge = UiStyle.Hex(0x5f4519);
        readonly VisualElement _disc;
        readonly Label _text;

        public CoinView(float size)
        {
            pickingMode = PickingMode.Ignore;
            style.width = style.height = size;
            style.flexShrink = 0;
            _disc = new VisualElement { pickingMode = PickingMode.Ignore };
            _disc.style.width = _disc.style.height = size;
            _disc.style.alignItems = Align.Center;
            _disc.style.justifyContent = Justify.Center;
            UiStyle.Radius(_disc, size / 2);
            _text = UiStyle.Label("", Mathf.RoundToInt(size * 0.46f));
            UiStyle.Black(_text);
            _text.pickingMode = PickingMode.Ignore;
            _disc.Add(_text);
            Add(_disc);
            SetFace(true);
        }

        public void SetFace(bool front)
        {
            _text.text = front ? "앞" : "뒤";
            _text.style.color = front ? FrontInk : UiStyle.Gold;
            _disc.style.backgroundColor = front ? UiStyle.Gold : UiStyle.Stone6;
            float width = Mathf.Max(3f, _disc.style.width.value.value * 0.05f);
            UiStyle.BorderColor(_disc, front ? FrontEdge : UiStyle.GoldDeep, width);
        }

        /// <summary>반 바퀴를 halfTurns번 돈 모습 (0 = 앞면이 보인다, 1 = 뒷면, 2 = 앞면 …).</summary>
        public void SetSpin(float halfTurns)
        {
            float c = Mathf.Cos(halfTurns * Mathf.PI);
            SetFace(c >= 0f);
            _disc.style.scale = new Scale(new Vector3(Mathf.Max(0.06f, Mathf.Abs(c)), 1f, 1f));
        }
    }

    /// <summary>주사위 한 면: 양피지 색 둥근 네모에 눈 1~6개. 듀얼 중 카드 효과의 주사위에 쓴다.</summary>
    internal sealed class DieView : VisualElement
    {
        // 눈의 자리 (3×3 격자: 0 = 왼쪽·위, 1 = 가운데, 2 = 오른쪽·아래)
        static readonly (int X, int Y)[][] Pips =
        {
            new[] { (1, 1) },
            new[] { (0, 0), (2, 2) },
            new[] { (0, 0), (1, 1), (2, 2) },
            new[] { (0, 0), (2, 0), (0, 2), (2, 2) },
            new[] { (0, 0), (2, 0), (1, 1), (0, 2), (2, 2) },
            new[] { (0, 0), (2, 0), (0, 1), (2, 1), (0, 2), (2, 2) },
        };
        int _value = 1;

        public DieView(float size)
        {
            pickingMode = PickingMode.Ignore;
            style.width = style.height = size;
            style.flexShrink = 0;
            style.backgroundColor = UiStyle.Paper;
            UiStyle.Radius(this, size * 0.18f);
            UiStyle.BorderColor(this, UiStyle.Stone9, Mathf.Max(3f, size * 0.04f));
            generateVisualContent += Draw;
        }

        /// <summary>보이는 눈 (1~6).</summary>
        public int Value
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp(value, 1, 6);
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 0)
                return;
            float r = rect.width * 0.1f;
            var p = context.painter2D;
            p.fillColor = UiStyle.PaperInk;
            foreach (var (x, y) in Pips[_value - 1])
            {
                var center = new Vector2(rect.x + rect.width * (0.24f + 0.26f * x), rect.y + rect.height * (0.24f + 0.26f * y));
                p.BeginPath();
                p.Arc(center, r, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
                p.ClosePath();
                p.Fill();
            }
        }
    }

    /// <summary>별을 가로로 늘어놓는다 (레벨, 랭크). 글꼴에 별 글자가 있는지와 상관없이 그린다.</summary>
    internal sealed class StarRow : VisualElement
    {
        readonly int _count;
        readonly Color _color;
        readonly bool _alignRight;

        /// <summary>별의 지름은 요소의 높이다. 별이 칸보다 많으면 겹치지 않게 줄여 그린다.</summary>
        public StarRow(int count, Color color, bool alignRight = true)
        {
            _count = count;
            _color = color;
            _alignRight = alignRight;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (_count <= 0 || float.IsNaN(rect.width) || rect.width <= 0 || rect.height <= 0)
                return;
            float size = Mathf.Min(rect.height, rect.width / _count);
            float total = size * _count;
            float x = rect.x + (_alignRight ? rect.width - total : 0), cy = rect.y + rect.height / 2;
            var p = context.painter2D;
            p.fillColor = _color;
            for (int n = 0; n < _count; n++)
            {
                float cx = x + size * (n + 0.5f), outer = size * 0.5f, inner = outer * 0.42f;
                p.BeginPath();
                for (int k = 0; k < 10; k++)
                {
                    float r = k % 2 == 0 ? outer : inner, a = (-90f + k * 36f) * Mathf.Deg2Rad;
                    var point = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
                    if (k == 0) p.MoveTo(point);
                    else p.LineTo(point);
                }
                p.ClosePath();
                p.Fill(FillRule.NonZero);
            }
        }
    }
}
