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
