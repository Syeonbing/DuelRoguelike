// 카드 그림 칸의 픽토그램 데이터 (종족·마법/함정 종류·속성). Unity API를 쓰지 않는 순수 C#이라 도구(미리보기)에서도 그대로 쓴다.
// 원본은 Assets/UI/Resources/Pictograms.txt. 좌표는 48×48 칸 기준이다.
//
// 파일 형식
//   # 이름            픽토그램 하나의 시작 (예: "# 마법사족", "# 마법:속공", "# 속성:빛")
//   f 도형; 도형 ...   채우기. 한 줄의 도형들은 한 경로가 되고 짝홀 규칙(evenodd)으로 채운다. 겹친 곳은 구멍이 된다.
//   s 굵기 도형 ...    선 긋기 (끝과 모서리는 둥글게).
//   // ...             주석
// 도형
//   M.. L.. C.. Q.. Z   SVG 경로 (대문자 절대 좌표만)
//   c x y r             원          e x y rx ry [각도]   타원 (각도는 시계 방향 도)
//   r x y w h           사각형      p x1 y1 x2 y2 ...    다각형
//   star x y n ro ri    별 (꼭짓점 n개, 바깥·안쪽 반지름)
//   gear x y ro ri n    톱니바퀴    drop x y r           물방울 (위로 뾰족)
//   teeth x y n rb rt h 원을 따라 놓인 삼각형 n개 (밑변 반지름 rb, 꼭짓점 반지름 rt, 반각 h도)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DuelRoguelike.UI
{
    public sealed class PictogramPath
    {
        /// <summary>'M' 이동, 'L' 직선, 'C' 3차 곡선(점 3개), 'Q' 2차 곡선(점 2개), 'Z' 닫기.</summary>
        public readonly List<char> Ops = new List<char>();
        /// <summary>Ops 순서대로 점의 x, y를 이어 붙인 값.</summary>
        public readonly List<float> Points = new List<float>();
    }

    public sealed class PictogramShape
    {
        public bool Stroke;
        public float Width;
        public readonly PictogramPath Path = new PictogramPath();
    }

    public sealed class Pictogram
    {
        public string Name;
        public readonly List<PictogramShape> Shapes = new List<PictogramShape>();

        /// <summary>미리보기용 SVG 경로 글자 (도형마다 하나).</summary>
        public static string ToSvg(PictogramPath path)
        {
            var sb = new StringBuilder();
            int p = 0;
            foreach (var op in path.Ops)
            {
                sb.Append(op);
                int n = op == 'C' ? 3 : op == 'Q' ? 2 : op == 'Z' ? 0 : 1;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(path.Points[p].ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                      .Append(path.Points[p + 1].ToString("0.##", CultureInfo.InvariantCulture));
                    p += 2;
                }
            }
            return sb.ToString();
        }
    }

    public static class PictogramParser
    {
        const float K = 0.5523f;

        public static Dictionary<string, Pictogram> Parse(string text)
        {
            var result = new Dictionary<string, Pictogram>();
            Pictogram cur = null;
            int lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                var line = raw.Trim();
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment).Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    cur = new Pictogram { Name = line.Substring(1).Trim() };
                    result[cur.Name] = cur;
                    continue;
                }
                if (cur == null) throw new FormatException($"{lineNo}행: '# 이름' 앞에 도형이 있습니다.");
                var shape = new PictogramShape();
                string rest;
                if (line.StartsWith("f ", StringComparison.Ordinal)) rest = line.Substring(2);
                else if (line.StartsWith("s ", StringComparison.Ordinal))
                {
                    shape.Stroke = true;
                    var body = line.Substring(2).TrimStart();
                    int sp = body.IndexOf(' ');
                    shape.Width = Num(body.Substring(0, sp));
                    rest = body.Substring(sp + 1);
                }
                else throw new FormatException($"{lineNo}행: 'f' 또는 's'로 시작해야 합니다.");
                foreach (var part in rest.Split(';'))
                {
                    var prim = part.Trim();
                    if (prim.Length == 0) continue;
                    try { AddPrimitive(shape.Path, prim); }
                    catch (Exception e) { throw new FormatException($"{lineNo}행 ({cur.Name}): {prim} — {e.Message}"); }
                }
                cur.Shapes.Add(shape);
            }
            return result;
        }

        static float Num(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static float[] Nums(string[] t)
        {
            var v = new float[t.Length - 1];
            for (int i = 1; i < t.Length; i++) v[i - 1] = Num(t[i]);
            return v;
        }

        static void AddPrimitive(PictogramPath path, string prim)
        {
            if (prim[0] == 'M') { ParseSvg(path, prim); return; }
            var t = prim.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var v = Nums(t);
            switch (t[0])
            {
                case "c": Ellipse(path, v[0], v[1], v[2], v[2], 0); break;
                case "e": Ellipse(path, v[0], v[1], v[2], v[3], v.Length > 4 ? v[4] : 0); break;
                case "r": Poly(path, new[] { v[0], v[1], v[0] + v[2], v[1], v[0] + v[2], v[1] + v[3], v[0], v[1] + v[3] }); break;
                case "p": Poly(path, v); break;
                case "star":
                {
                    int n = (int)v[2];
                    var pts = new float[n * 4];
                    for (int i = 0; i < n * 2; i++)
                    {
                        float r = i % 2 == 1 ? v[4] : v[3];
                        double a = (-90 + i * 180.0 / n) * Math.PI / 180;
                        pts[i * 2] = v[0] + r * (float)Math.Cos(a);
                        pts[i * 2 + 1] = v[1] + r * (float)Math.Sin(a);
                    }
                    Poly(path, pts);
                    break;
                }
                case "gear":
                {
                    int n = (int)v[4];
                    var pts = new List<float>();
                    float[][] steps = { new[] { -0.3f, v[3] }, new[] { -0.17f, v[2] }, new[] { 0.17f, v[2] }, new[] { 0.3f, v[3] } };
                    for (int i = 0; i < n; i++)
                        foreach (var s in steps)
                        {
                            double a = (i + s[0]) * 2 * Math.PI / n;
                            pts.Add(v[0] + s[1] * (float)Math.Cos(a));
                            pts.Add(v[1] + s[1] * (float)Math.Sin(a));
                        }
                    Poly(path, pts.ToArray());
                    break;
                }
                case "drop":
                {
                    float x = v[0], y = v[1], r = v[2];
                    Op(path, 'M', x, y - 2.2f * r);
                    Op(path, 'C', x + 0.6f * r, y - 1.2f * r, x + r, y - 0.6f * r, x + r, y);
                    Op(path, 'C', x + r, y + 0.6f * r, x + 0.55f * r, y + r, x, y + r);
                    Op(path, 'C', x - 0.55f * r, y + r, x - r, y + 0.6f * r, x - r, y);
                    Op(path, 'C', x - r, y - 0.6f * r, x - 0.6f * r, y - 1.2f * r, x, y - 2.2f * r);
                    Op(path, 'Z');
                    break;
                }
                case "teeth":
                {
                    int n = (int)v[2];
                    double h = v[5] * Math.PI / 180;
                    for (int i = 0; i < n; i++)
                    {
                        double a = i * 2 * Math.PI / n;
                        Poly(path, new[]
                        {
                            v[0] + v[3] * (float)Math.Cos(a - h), v[1] + v[3] * (float)Math.Sin(a - h),
                            v[0] + v[4] * (float)Math.Cos(a), v[1] + v[4] * (float)Math.Sin(a),
                            v[0] + v[3] * (float)Math.Cos(a + h), v[1] + v[3] * (float)Math.Sin(a + h),
                        });
                    }
                    break;
                }
                default: throw new FormatException("모르는 도형");
            }
        }

        static void Op(PictogramPath path, char op, params float[] p)
        {
            path.Ops.Add(op);
            path.Points.AddRange(p);
        }

        static void Poly(PictogramPath path, float[] v)
        {
            Op(path, 'M', v[0], v[1]);
            for (int i = 2; i + 1 < v.Length; i += 2) Op(path, 'L', v[i], v[i + 1]);
            Op(path, 'Z');
        }

        static void Ellipse(PictogramPath path, float cx, float cy, float rx, float ry, float deg)
        {
            double t = deg * Math.PI / 180;
            float c = (float)Math.Cos(t), s = (float)Math.Sin(t);
            float[] P(float x, float y) => new[] { cx + x * c - y * s, cy + x * s + y * c };
            float[] Cat(params float[][] ps) { var r = new List<float>(); foreach (var q in ps) r.AddRange(q); return r.ToArray(); }
            float a = rx * K, b = ry * K;
            Op(path, 'M', P(-rx, 0));
            Op(path, 'C', Cat(P(-rx, -b), P(-a, -ry), P(0, -ry)));
            Op(path, 'C', Cat(P(a, -ry), P(rx, -b), P(rx, 0)));
            Op(path, 'C', Cat(P(rx, b), P(a, ry), P(0, ry)));
            Op(path, 'C', Cat(P(-a, ry), P(-rx, b), P(-rx, 0)));
            Op(path, 'Z');
        }

        static void ParseSvg(PictogramPath path, string d)
        {
            var nums = new List<float>();
            char op = ' ';
            int i = 0;
            void Flush()
            {
                if (op == ' ') return;
                int per = op == 'C' ? 6 : op == 'Q' ? 4 : op == 'Z' ? 0 : 2;
                if (per == 0) { Op(path, 'Z'); }
                else
                {
                    if (nums.Count == 0 || nums.Count % per != 0) throw new FormatException($"'{op}' 숫자 개수가 맞지 않습니다.");
                    for (int k = 0; k < nums.Count; k += per)
                        Op(path, k > 0 && op == 'M' ? 'L' : op, nums.GetRange(k, per).ToArray());
                }
                nums.Clear();
            }
            while (i < d.Length)
            {
                char ch = d[i];
                if (ch == 'M' || ch == 'L' || ch == 'C' || ch == 'Q' || ch == 'Z')
                {
                    Flush();
                    op = ch;
                    i++;
                }
                else if (ch == '-' || ch == '.' || char.IsDigit(ch))
                {
                    int start = i++;
                    bool dot = ch == '.';
                    while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot)))
                    {
                        if (d[i] == '.') dot = true;
                        i++;
                    }
                    nums.Add(Num(d.Substring(start, i - start)));
                }
                else if (ch == ' ' || ch == ',' || ch == '\t') i++;
                else throw new FormatException($"경로에 쓸 수 없는 글자 '{ch}'");
            }
            Flush();
        }
    }
}
