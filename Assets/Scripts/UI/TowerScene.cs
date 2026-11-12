// 탑 장면: 층 사이 화면(허브)의 바탕 그림. docs/UI.md "탑 (허브)".
// 1920×1080 좌표로 그린다. 하늘·구름·땅(뒤)과 탑·계단·문·말(앞)을 따로 그려서, 가만히 있을 때는 구름만 다시 그린다.
// 색이 이어서 바뀌는 곳(하늘, 탑 몸통의 명암, 불빛)은 꼭짓점 색을 넣은 사각형으로, 나머지 도형은 Painter2D로 그린다.
// 두 방식을 한 요소에서 섞지 않는다 (요소마다 한 가지 방식만 써서 그리는 순서를 요소 순서로 정한다).
using System;
using System.Collections.Generic;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    internal sealed class TowerScene : VisualElement
    {
        public const float Width = 1920, Height = 1080;
        /// <summary>한 층의 높이, 탑 중심 x, 탑 반지름, 지금 층 바닥의 y.</summary>
        public const float FloorHeight = 150, CenterX = 700, Radius = 290, BaseY = 830;

        static readonly (int Floor, Color Top, Color Bottom)[] Sky =
        {
            (1, UiStyle.Hex(0x7fb6c9), UiStyle.Hex(0xf4cf95)),
            (11, UiStyle.Hex(0x5aa2d6), UiStyle.Hex(0xcfe8ee)),
            (21, UiStyle.Hex(0x2f72b8), UiStyle.Hex(0xa9d6ee)),
            (31, UiStyle.Hex(0x3a2f70), UiStyle.Hex(0xef8f62)),
            (41, UiStyle.Hex(0x070b24), UiStyle.Hex(0x2b3272)),
            (51, UiStyle.Hex(0x03040f), UiStyle.Hex(0x151a4a)),
        };

        static readonly (float At, Color Color)[] BodyShade =
        {
            (0f, UiStyle.Hex(0x57472f)), (0.1f, UiStyle.Hex(0xa28a63)), (0.34f, UiStyle.Hex(0xe6d3aa)),
            (0.5f, UiStyle.Hex(0xdcc79c)), (0.8f, UiStyle.Hex(0x927c58)), (1f, UiStyle.Hex(0x4b3d2c)),
        };

        static readonly (float At, Color Color)[] EaveShade =
        {
            (0f, UiStyle.Hex(0x7b6646)), (0.34f, UiStyle.Hex(0xf5e8c6)), (1f, UiStyle.Hex(0x6a5639)),
        };

        static readonly float[] Ridge = { 0, -40, -110, -60, -150, -90, -30, -120, -170, -80, -130, -50, -100, 0 };
        static readonly (float X, float H)[] Pillars = { (150, 70), (230, 110), (1250, 90), (1700, 120), (1790, 60) };
        static readonly (float Cx, float Cy, float Rx, float Ry)[] CloudLobes =
        {
            (0.3f, 0f, 0.3f, 1f), (0.55f, -0.55f, 0.26f, 1.1f), (0.75f, 0f, 0.25f, 0.9f), (0.5f, 0.75f, 0.44f, 0.62f),
        };
        static readonly (float X, float Y, float W, float V)[] HighClouds =
        {
            (0, 150, 520, 0.0035f), (980, 128, 430, 0.005f), (1560, 350, 470, 0.004f), (440, 318, 380, 0.0045f),
        };
        static readonly float[] BirdX = { 0, 38, -34, 70, -72, 104 }, BirdY = { 0, 17, 21, 38, 44, 60 };

        static readonly Color MineFill = UiStyle.Hex(0x1b2748), OpponentFill = UiStyle.Hex(0x3d1611);

        readonly VisualElement _sky, _far, _body, _detail, _light, _figures;
        float _position = 1, _opponent;

        public TowerScene()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = 0;
            style.width = Width;
            style.height = Height;
            style.overflow = Overflow.Hidden;
            _sky = Layer(DrawSky);
            _far = Layer(DrawFar);
            _body = Layer(DrawBody);
            _detail = Layer(DrawDetail);
            _light = Layer(DrawLight);
            _figures = Layer(DrawFigures);
            // 구름과 새는 가만히 있을 때도 천천히 움직인다
            schedule.Execute(() => _far.MarkDirtyRepaint()).Every(50);
        }

        /// <summary>
        /// 내 말이 있는 높이. 정수면 그 층의 문 옆에 서 있고, 그 사이면 계단을 오르는 중이다.
        /// 0은 탑 앞 땅 (1층에 들어가기 전), 0과 1 사이는 문으로 걸어가는 중.
        /// </summary>
        public float Marker
        {
            get => _position;
            set
            {
                if (Mathf.Approximately(_position, value))
                    return;
                _position = value;
                foreach (var layer in new[] { _sky, _far, _body, _detail, _light, _figures })
                    layer.MarkDirtyRepaint();
            }
        }

        /// <summary>상대가 문 옆에 나타난 정도 (0 = 없음, 1 = 다 나타남).</summary>
        public float Opponent
        {
            get => _opponent;
            set
            {
                _opponent = Mathf.Clamp01(value);
                _figures.MarkDirtyRepaint();
            }
        }

        /// <summary>화면이 따라가는 높이 (1층 아래로는 내려가지 않는다).</summary>
        float Altitude => Mathf.Max(1f, _position);

        /// <summary>층 f의 바닥(문이 서는 줄)의 화면 y.</summary>
        public static float FloorY(int floor, float position) => BaseY - (floor - Mathf.Max(1f, position)) * FloorHeight;

        VisualElement Layer(Action<MeshGenerationContext> draw)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            layer.generateVisualContent += draw;
            Add(layer);
            return layer;
        }

        static float Now => Time.realtimeSinceStartup * 1000f;

        static Color Mix(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);

        static void SkyAt(float altitude, out Color top, out Color bottom)
        {
            int i = 0;
            while (i < Sky.Length - 2 && altitude >= Sky[i + 1].Floor)
                i++;
            float t = Mathf.Clamp01((altitude - Sky[i].Floor) / (Sky[i + 1].Floor - Sky[i].Floor));
            top = Mix(Sky[i].Top, Sky[i + 1].Top, t);
            bottom = Mix(Sky[i].Bottom, Sky[i + 1].Bottom, t);
        }

        static float Night(float altitude) => Mathf.Clamp01((altitude - 30f) / 11f);

        // ───── 하늘: 세로로 이어지는 색과 별 ─────

        void DrawSky(MeshGenerationContext context)
        {
            float a = Altitude;
            SkyAt(a, out var top, out var bottom);
            var quads = new QuadBatch();
            // 색은 화면 색(sRGB) 기준으로 섞어야 시안과 같아 보인다. 띠를 잘게 나눠 그 사이만 그래픽 카드가 잇게 한다.
            const int bands = 18;
            for (int i = 0; i < bands; i++)
            {
                float t0 = i / (float)bands, t1 = (i + 1) / (float)bands;
                Color c0 = Mix(top, bottom, t0), c1 = Mix(top, bottom, t1);
                quads.Add(0, Height * t0, Width, Height * t1, c0, c0, c1, c1);
            }
            float night = Night(a);
            if (night > 0)
            {
                var star = new Color(1, 1, 1, night * 0.9f);
                for (int i = 0; i < 90; i++)
                {
                    float x = i * 733 % 1920, y = i * 397 % 900, r = 1 + i % 3;
                    quads.Add(x, y, x + r, y + r, star);
                }
            }
            quads.Flush(context);
        }

        // ───── 해, 먼 산, 땅, 구름, 새, 구름 바다 ─────

        void DrawFar(MeshGenerationContext context)
        {
            float a = Altitude, now = Now, night = Night(a);
            SkyAt(a, out _, out var skyBottom);
            var p = context.painter2D;

            // 해와 해무리 두 겹
            var sun = night > 0.5f ? UiStyle.Hex(0xf1eedc) : UiStyle.Hex(0xfff3cf);
            float sunY = 230 + Mathf.Min(a, 50f) * 3;
            // 밤에는 해무리를 옅게 한다 (어두운 하늘 위에서는 같은 투명도라도 훨씬 밝게 보인다)
            float halo = 1f - night * 0.7f;
            Disc(p, 1500, sunY, 168, UiStyle.Alpha(sun, 0.05f * halo));
            Disc(p, 1500, sunY, 112, UiStyle.Alpha(sun, 0.09f * halo));
            Disc(p, 1500, sunY, 62, UiStyle.Alpha(sun, 0.92f));

            // 먼 산과 땅: 탑보다 천천히 내려간다
            float ridgeY = 800 + (a - 1) * 13;
            if (ridgeY < 1100)
            {
                p.fillColor = Mix(skyBottom, UiStyle.Hex(0x55606e), 0.45f);
                p.BeginPath();
                p.MoveTo(new Vector2(0, Height));
                for (int i = 0; i < Ridge.Length; i++)
                    p.LineTo(new Vector2(i * 150, ridgeY + Ridge[i]));
                p.LineTo(new Vector2(Width, Height));
                p.ClosePath();
                p.Fill();
            }
            float groundY = 790 + (a - 1) * 40;
            if (groundY < 1100)
            {
                FillRect(p, 0, groundY, Width, Height - groundY + 40, UiStyle.Hex(0xb98f5c));
                p.fillColor = UiStyle.Hex(0xa67d4e);
                for (int i = 0; i < 7; i++)
                {
                    p.BeginPath();
                    Ellipse(p, 120 + i * 310, groundY + 34 + i % 2 * 26, 230, 26);
                    p.Fill();
                }
                // 무너진 기둥
                var pillar = UiStyle.Hex(0x8a6a44);
                foreach (var (x, h) in Pillars)
                {
                    FillRect(p, x, groundY - h + 10, 26, h, pillar);
                    FillRect(p, x - 6, groundY - h + 2, 38, 10, pillar);
                }
            }

            // 구름: 높이마다 떠 있고 가로로 천천히 흐른다. 노을 높이에서는 살구색이 돌고 밤에는 옅어진다.
            var cloud = Mix(Color.white, UiStyle.Hex(0xffd9b8), Mathf.Clamp01((a - 26f) / 8f) * (1 - night));
            var drifting = UiStyle.Alpha(cloud, 0.9f - night * 0.62f);
            for (int k = 0; k < 34; k++)
            {
                float altitude = 4.5f + k * 2.7f;
                float y = BaseY - 200 - (altitude - a) * FloorHeight * 0.55f;
                if (y < -120 || y > 1200)
                    continue;
                float speed = 0.006f + k % 3 * 0.004f, width = 260 + k * 53 % 260;
                Cloud(p, (k * 611 % 2300 + now * speed) % 2500 - 320, y, width, drifting);
            }
            // 위쪽 하늘에 늘 떠 있는 옅은 구름
            var high = UiStyle.Alpha(cloud, 0.34f - night * 0.24f);
            foreach (var (x, y, w, v) in HighClouds)
                Cloud(p, (x + now * v) % 2700 - 460, y, w, high);

            // 새 떼 (밤에는 보이지 않는다)
            if (night < 0.6f)
            {
                p.strokeColor = new Color(44 / 255f, 38 / 255f, 50 / 255f, 0.8f * (1 - night));
                p.lineWidth = 3.5f;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                // 층 팻말(x 1032~1380쯤)과 겹치지 않게 오른쪽 하늘에서 논다
                float bx = 1520 + 140 * Mathf.Sin(now * 0.00013f), by = 176 + 30 * Mathf.Sin(now * 0.00031f);
                for (int i = 0; i < 6; i++)
                {
                    float x = bx + BirdX[i], y = by + BirdY[i], flap = 7 + 5 * Mathf.Sin(now * 0.008f + i);
                    p.BeginPath();
                    p.MoveTo(new Vector2(x - 13, y - flap));
                    p.LineTo(new Vector2(x, y));
                    p.LineTo(new Vector2(x + 13, y - flap));
                    p.Stroke();
                }
                p.lineCap = LineCap.Butt;
            }

            // 구름 바다: 17~23층에 걸쳐 아래에서 올라온다
            float sea = Mathf.Clamp01((a - 17f) / 6f);
            if (sea > 0)
            {
                float seaY = 1190 - sea * 250;
                for (int i = 0; i < 9; i++)
                    Cloud(p, i * 250 - 140 + Mathf.Sin(now * 0.0002f + i) * 30, seaY + i % 2 * 26, 420, cloud);
                FillRect(p, 0, seaY + 40, Width, 400, cloud);
            }
        }

        // ───── 탑 몸통: 가로 명암과 돌 줄눈 ─────

        void DrawBody(MeshGenerationContext context)
        {
            float a = Altitude;
            var quads = new QuadBatch();
            float top = -30, firstY = FloorY(1, a), bottom = Mathf.Min(1110, firstY);
            quads.AddShade(CenterX - Radius, top, Radius * 2, bottom - top, BodyShade);
            if (firstY < 1110)
            {
                // 받침돌
                quads.Add(CenterX - Radius - 36, firstY, CenterX + Radius + 36, firstY + 60, UiStyle.Hex(0x6f5b40));
                quads.Add(CenterX - Radius - 36, firstY, CenterX + Radius + 36, firstY + 10, UiStyle.Hex(0x8f7752));
            }
            // 돌 줄눈: 가장자리로 갈수록 간격이 좁아져 원통으로 보인다
            var joint = new Color(70 / 255f, 52 / 255f, 28 / 255f, 0.2f);
            FloorRange(a, out int first, out int last);
            for (int f = first; f <= last; f++)
            {
                float y0 = FloorY(f, a);
                for (int j = 0; j < 4; j++)
                {
                    float y = y0 - j * FloorHeight / 4;
                    if (j > 0)
                        quads.Add(CenterX - Radius, y - 1, CenterX + Radius, y + 1, joint);
                    for (int k = -5; k <= 5; k++)
                    {
                        float angle = (k + (j % 2 == 1 ? 0.5f : 0f)) * 17f * Mathf.Deg2Rad;
                        if (Mathf.Abs(angle) >= 1.5f)
                            continue;
                        float x = CenterX + Radius * Mathf.Sin(angle);
                        quads.Add(x - 1, y - FloorHeight / 4, x + 1, y, joint);
                    }
                }
            }
            quads.Flush(context);
        }

        static void FloorRange(float altitude, out int first, out int last)
        {
            first = Mathf.Max(1, Mathf.FloorToInt(altitude) - 3);
            last = Mathf.FloorToInt(altitude) + 7;
        }

        /// <summary>불이 켜진 층: 내 말이 서 있는 층 (오르는 중이거나 탑 앞에 있으면 없음).</summary>
        int LitFloor()
        {
            int floor = Mathf.RoundToInt(_position);
            return _position >= 1 && Mathf.Abs(_position - floor) < 0.02f ? floor : 0;
        }

        // ───── 계단, 창, 문 ─────

        void DrawDetail(MeshGenerationContext context)
        {
            float a = Altitude;
            int lit = LitFloor();
            var p = context.painter2D;
            FloorRange(a, out int first, out int last);
            for (int f = first; f <= last; f++)
            {
                float y0 = FloorY(f, a);
                if (y0 < -40 || y0 - FloorHeight > Height + 40)
                    continue;
                bool isLit = f == lit, boss = Tower.FixedKind(f) == FloorKind.Boss;

                // 계단: 문에서 오른쪽으로 돌아 탑 뒤를 지나, 왼쪽에서 다음 층 문으로
                p.lineCap = LineCap.Butt;
                p.lineJoin = LineJoin.Round;
                foreach (var (from, to) in new[] { (0, 90), (270, 360) })
                {
                    foreach (var (width, color, dy) in new[] { (13f, UiStyle.Hex(0x5b4a33), 0f), (4f, UiStyle.Hex(0xf4e6c2), -6f) })
                    {
                        p.strokeColor = color;
                        p.lineWidth = width;
                        p.BeginPath();
                        for (int angle = from; angle <= to; angle += 6)
                        {
                            var point = new Vector2(CenterX + Radius * Mathf.Sin(angle * Mathf.Deg2Rad), y0 - FloorHeight * angle / 360f + dy - 8);
                            if (angle == from) p.MoveTo(point);
                            else p.LineTo(point);
                        }
                        p.Stroke();
                    }
                }

                // 창 두 개 (가장자리 쪽이라 폭이 좁다)
                p.fillColor = isLit ? UiStyle.Hex(0xffe1a0) : UiStyle.Hex(0x2c2114);
                foreach (var (angle, rise) in new[] { (-44f, 0.34f), (44f, 0.66f) })
                {
                    float x = CenterX + Radius * Mathf.Sin(angle * Mathf.Deg2Rad);
                    p.BeginPath();
                    Arch(p, x, y0 - FloorHeight * rise + 26, 28 * Mathf.Cos(angle * Mathf.Deg2Rad), 50);
                    p.Fill();
                }

                // 문 (보스 층은 크고 문틀이 금색)
                float doorWidth = boss ? 86 : 60, doorHeight = boss ? 112 : 84;
                p.fillColor = isLit ? UiStyle.Hex(0xffdf9a) : UiStyle.Hex(0x231a10);
                p.BeginPath();
                Arch(p, CenterX, y0 - 8, doorWidth, doorHeight);
                p.Fill();
                p.strokeColor = boss ? UiStyle.Gold : UiStyle.Hex(0x8c6f3f);
                p.lineWidth = 6;
                p.lineJoin = LineJoin.Miter;
                p.BeginPath();
                Arch(p, CenterX, y0 - 8, doorWidth, doorHeight);
                p.Stroke();
            }
        }

        // ───── 불빛, 처마, 위쪽 안개 ─────

        void DrawLight(MeshGenerationContext context)
        {
            float a = Altitude;
            int lit = LitFloor();
            var quads = new QuadBatch();
            FloorRange(a, out int first, out int last);
            for (int f = first; f <= last; f++)
            {
                float y0 = FloorY(f, a);
                if (f == lit)
                    quads.AddGlow(CenterX, y0 - 50, 170, new Color(1f, 214 / 255f, 130 / 255f, 0.3f));
                quads.Add(CenterX - Radius, y0 + 6, CenterX + Radius, y0 + 15, new Color(40 / 255f, 28 / 255f, 12 / 255f, 0.28f));
                quads.AddShade(CenterX - Radius - 16, y0 - 8, Radius * 2 + 32, 14, EaveShade);
            }
            // 위쪽 안개: 탑이 하늘 속으로 이어진다
            SkyAt(a, out var top, out _);
            Color haze = UiStyle.Alpha(top, 0.4f), clear = UiStyle.Alpha(top, 0f);
            quads.Add(0, 0, Width, 60, haze);
            quads.Add(0, 60, Width, 190, haze, haze, clear, clear);
            quads.Flush(context);
        }

        // ───── 내 말과 상대 ─────

        void DrawFigures(MeshGenerationContext context)
        {
            float a = Altitude;
            var p = context.painter2D;
            if (_position < 1)
            {
                // 탑 앞 땅에서 1층 문으로 걸어간다
                float t = Mathf.Clamp01(_position);
                Figure(p, Mathf.Lerp(CenterX - Radius - 110, CenterX - 58, t), FloorY(1, a) - 8, MineFill, UiStyle.Mine, 1f);
            }
            else
            {
                int floor = Mathf.FloorToInt(_position + 1e-4f);
                float turn = _position - floor, angle = turn * 2 * Mathf.PI;
                // 탑 뒤를 도는 동안에는 보이지 않는다
                if (turn < 0.26f || turn > 0.74f)
                    Figure(p, CenterX + Radius * Mathf.Sin(angle) - 58 * Mathf.Cos(angle), FloorY(floor, a) - FloorHeight * turn - 8, MineFill, UiStyle.Mine, 1f);
            }
            if (_opponent > 0)
                Figure(p, CenterX + 62 + (1 - _opponent) * 46, FloorY(Mathf.RoundToInt(a), a) - 8, OpponentFill, UiStyle.Opponent, _opponent);
        }

        /// <summary>사람 모양 말: 몸통과 머리. (x, y)는 발 가운데.</summary>
        static void Figure(Painter2D p, float x, float y, Color fill, Color line, float alpha)
        {
            p.fillColor = UiStyle.Alpha(fill, alpha);
            p.strokeColor = UiStyle.Alpha(line, alpha);
            p.lineWidth = 4;
            p.lineJoin = LineJoin.Round;
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x - 17, y));
                p.LineTo(new Vector2(x - 11, y - 36));
                p.QuadraticCurveTo(new Vector2(x, y - 46), new Vector2(x + 11, y - 36));
                p.LineTo(new Vector2(x + 17, y));
                p.ClosePath();
                if (pass == 0) p.Fill();
                else p.Stroke();
            }
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                Ellipse(p, x, y - 50, 11, 11);
                if (pass == 0) p.Fill();
                else p.Stroke();
            }
        }

        // ───── 도형 ─────

        const float Kappa = 0.5522848f;

        /// <summary>타원 하나를 지금 경로에 더한다 (시계 방향). 여러 개를 한 경로에 넣고 한 번에 칠할 수 있다.</summary>
        static void Ellipse(Painter2D p, float cx, float cy, float rx, float ry)
        {
            float kx = rx * Kappa, ky = ry * Kappa;
            p.MoveTo(new Vector2(cx + rx, cy));
            p.BezierCurveTo(new Vector2(cx + rx, cy + ky), new Vector2(cx + kx, cy + ry), new Vector2(cx, cy + ry));
            p.BezierCurveTo(new Vector2(cx - kx, cy + ry), new Vector2(cx - rx, cy + ky), new Vector2(cx - rx, cy));
            p.BezierCurveTo(new Vector2(cx - rx, cy - ky), new Vector2(cx - kx, cy - ry), new Vector2(cx, cy - ry));
            p.BezierCurveTo(new Vector2(cx + kx, cy - ry), new Vector2(cx + rx, cy - ky), new Vector2(cx + rx, cy));
            p.ClosePath();
        }

        static void Disc(Painter2D p, float cx, float cy, float r, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            Ellipse(p, cx, cy, r, r);
            p.Fill();
        }

        static void FillRect(Painter2D p, float x, float y, float w, float h, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>
        /// 구름 하나. 타원 네 개를 한 경로로 한 번에 칠한다. 반투명한 도형을 따로 겹쳐 칠하면 겹친 곳이 얼룩진다.
        /// </summary>
        static void Cloud(Painter2D p, float x, float y, float w, Color color)
        {
            float h = w * 0.2f;
            p.fillColor = color;
            p.BeginPath();
            foreach (var (cx, cy, rx, ry) in CloudLobes)
                Ellipse(p, x + w * cx, y + h * cy, w * rx, h * ry);
            p.Fill(FillRule.NonZero);
        }

        /// <summary>위가 둥근 문·창 모양. (x, y)는 바닥 가운데.</summary>
        static void Arch(Painter2D p, float x, float y, float w, float h)
        {
            float r = w / 2, cy = y - h + r, k = r * Kappa;
            p.MoveTo(new Vector2(x - r, y));
            p.LineTo(new Vector2(x - r, cy));
            p.BezierCurveTo(new Vector2(x - r, cy - k), new Vector2(x - k, cy - r), new Vector2(x, cy - r));
            p.BezierCurveTo(new Vector2(x + k, cy - r), new Vector2(x + r, cy - k), new Vector2(x + r, cy));
            p.LineTo(new Vector2(x + r, y));
            p.ClosePath();
        }

        /// <summary>꼭짓점 색을 넣은 사각형을 모아 한 번에 그린다.</summary>
        sealed class QuadBatch
        {
            readonly List<Vertex> _vertices = new List<Vertex>();
            readonly List<ushort> _indices = new List<ushort>();

            public void Add(float x0, float y0, float x1, float y1, Color color) => Add(x0, y0, x1, y1, color, color, color, color);

            /// <summary>색 순서: 왼쪽 위, 오른쪽 위, 오른쪽 아래, 왼쪽 아래.</summary>
            public void Add(float x0, float y0, float x1, float y1, Color topLeft, Color topRight, Color bottomRight, Color bottomLeft)
            {
                if (_vertices.Count > 65000)
                    return;
                int n = _vertices.Count;
                _vertices.Add(At(x0, y0, topLeft));
                _vertices.Add(At(x1, y0, topRight));
                _vertices.Add(At(x1, y1, bottomRight));
                _vertices.Add(At(x0, y1, bottomLeft));
                foreach (int i in new[] { 0, 1, 2, 0, 2, 3 })
                    _indices.Add((ushort)(n + i));
            }

            /// <summary>가로로 색이 이어지는 사각형. 색 사이를 잘게 나눠 화면 색 기준으로 섞는다.</summary>
            public void AddShade(float x, float y, float w, float h, (float At, Color Color)[] stops)
            {
                const int steps = 4;
                for (int i = 0; i + 1 < stops.Length; i++)
                {
                    for (int s = 0; s < steps; s++)
                    {
                        float t0 = s / (float)steps, t1 = (s + 1) / (float)steps;
                        float a0 = Mathf.Lerp(stops[i].At, stops[i + 1].At, t0), a1 = Mathf.Lerp(stops[i].At, stops[i + 1].At, t1);
                        Color c0 = Mix(stops[i].Color, stops[i + 1].Color, t0), c1 = Mix(stops[i].Color, stops[i + 1].Color, t1);
                        Add(x + w * a0, y, x + w * a1, y + h, c0, c1, c1, c0);
                    }
                }
            }

            /// <summary>가운데가 밝고 바깥으로 사라지는 둥근 빛.</summary>
            public void AddGlow(float cx, float cy, float radius, Color color)
            {
                const int rings = 4, slices = 24;
                var clear = UiStyle.Alpha(color, 0f);
                for (int ring = 0; ring < rings; ring++)
                {
                    float r0 = radius * ring / rings, r1 = radius * (ring + 1) / rings;
                    // 바깥으로 갈수록 빨리 옅어지게 (제곱)
                    float f0 = 1f - ring / (float)rings, f1 = 1f - (ring + 1) / (float)rings;
                    Color c0 = Color.Lerp(clear, color, f0 * f0), c1 = Color.Lerp(clear, color, f1 * f1);
                    for (int s = 0; s < slices; s++)
                    {
                        float a0 = s * 2 * Mathf.PI / slices, a1 = (s + 1) * 2 * Mathf.PI / slices;
                        if (_vertices.Count > 65000)
                            return;
                        int n = _vertices.Count;
                        _vertices.Add(At(cx + r0 * Mathf.Cos(a0), cy + r0 * Mathf.Sin(a0), c0));
                        _vertices.Add(At(cx + r1 * Mathf.Cos(a0), cy + r1 * Mathf.Sin(a0), c1));
                        _vertices.Add(At(cx + r1 * Mathf.Cos(a1), cy + r1 * Mathf.Sin(a1), c1));
                        _vertices.Add(At(cx + r0 * Mathf.Cos(a1), cy + r0 * Mathf.Sin(a1), c0));
                        foreach (int i in new[] { 0, 1, 2, 0, 2, 3 })
                            _indices.Add((ushort)(n + i));
                    }
                }
            }

            static Vertex At(float x, float y, Color color) =>
                new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = color };

            public void Flush(MeshGenerationContext context)
            {
                if (_vertices.Count == 0)
                    return;
                var mesh = context.Allocate(_vertices.Count, _indices.Count);
                mesh.SetAllVertices(_vertices.ToArray());
                mesh.SetAllIndices(_indices.ToArray());
            }
        }
    }
}
