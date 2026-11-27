// 상점 장면: 칩교환소와 골드 상점의 바탕 그림. docs/UI.md "상점".
// - 칩교환소 (ChipExchange): 탑 안의 방. 돌벽과 아치 창(그 층 높이의 하늘), 등불 둘, 계산대 뒤의 주인(청록 챙 모자의 말), 청록 천을 깐 계산대.
//   위 막대(높이 72) 아래부터 그린다. 위 막대 자리는 비워 둔다.
// - 골드 상점 (GoldStall): 탑 앞의 노점. 1층 높이의 하늘과 땅, 왼쪽에 탑 몸통, 줄무늬 차양과 기둥, 주인(금색 챙 넓은 모자의 말), 붉은 천을 깐 좌판.
// 1920×1080 좌표로 그린다. 탑 장면(TowerScene.cs)과 같은 방식이다: 색이 이어서 바뀌는 곳은 꼭짓점 색을 넣은 사각형으로,
// 나머지 도형은 Painter2D로 그리고, 한 요소에서는 한 가지 방식만 쓴다 (그리는 순서 = 요소 순서).
// 상품·카드·글은 화면 코드(RunScreen.Shop.cs, RunScreen.GoldShop.cs)가 이 위에 얹는다. 계산대의 자리는 아래 상수로 맞춘다.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    internal sealed class ShopScene : VisualElement
    {
        public enum Kind { ChipExchange, GoldStall }

        public const float Width = TowerScene.Width, Height = TowerScene.Height;

        // ---- 칩교환소: 계산대 (천을 깐 윗면) ----
        /// <summary>벽이 시작하는 높이 (위 막대 아래).</summary>
        public const float ChipTop = 72;
        /// <summary>계산대 뒤쪽 나무 모서리의 위.</summary>
        public const float ChipCounterY = 322;
        public const float ChipClothTop = 344, ChipClothBottom = 666;
        /// <summary>계산대 앞쪽 나무 모서리의 아래 (그 아래는 어두운 바닥).</summary>
        public const float ChipFloorY = 692;
        /// <summary>주인이 서 있는 자리 (머리 가운데).</summary>
        public const float ChipKeeperX = 960, ChipKeeperY = 196;

        // ---- 골드 상점: 좌판 ----
        public const float StallLeft = 150, StallRight = 1770;
        public const float StallCounterY = 404, StallClothTop = 426, StallClothBottom = 700;
        public const float StallKeeperX = 960, StallKeeperY = 284;

        static readonly Color Teal = UiStyle.Hex(0x70cbb7);
        static readonly Color Wood = UiStyle.Hex(0x5b4027), WoodLight = UiStyle.Hex(0x7b5a37), WoodEdge = UiStyle.Hex(0xa17c50), WoodDark = UiStyle.Hex(0x2a1c10);
        static readonly Color Ink = UiStyle.Hex(0x14100c), Iron = UiStyle.Hex(0x1a120a);

        readonly Kind _kind;
        readonly float _altitude;

        /// <summary>칩교환소 계산대의 세로 점선 (봉인된 카드와 아이템 카드 사이). NaN이면 긋지 않는다.</summary>
        public float DividerX = float.NaN;

        /// <summary>가게 이름 판을 매단 사슬 둘의 x와 판의 위쪽 y (칩교환소). 판은 화면 코드가 놓는다.</summary>
        public float SignLeft = float.NaN, SignRight = float.NaN, SignTop;

        /// <param name="altitude">칩교환소: 그 층 (창밖 하늘의 색을 정한다). 골드 상점은 쓰지 않는다.</param>
        public ShopScene(Kind kind, float altitude = 1)
        {
            _kind = kind;
            _altitude = Mathf.Max(1f, altitude);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = 0;
            style.width = Width;
            style.height = Height;
            style.overflow = Overflow.Hidden;
            if (kind == Kind.ChipExchange)
            {
                Layer(ChipWall);
                Layer(ChipFixtures);
                Layer(ChipCounter);
                Layer(ChipTrim);
            }
            else
            {
                Layer(StallSky);
                Layer(StallFar);
                Layer(StallGround);
                Layer(StallFrame);
                Layer(StallTable);
                Layer(StallTrim);
            }
        }

        void Layer(Action<MeshGenerationContext> draw)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            layer.generateVisualContent += draw;
            Add(layer);
        }

        // ================= 칩교환소 =================

        static readonly float[] ChipWindows = { 166, 1754 }, ChipLanterns = { 420, 1500 };
        const float WindowWidth = 140, WindowTop = 104, WindowBottom = 294;

        /// <summary>돌벽, 줄눈, 창밖 하늘, 등불의 빛.</summary>
        void ChipWall(MeshGenerationContext context)
        {
            var quads = new TowerScene.QuadBatch();
            quads.Add(0, ChipTop, Width, ChipCounterY, UiStyle.Hex(0x2c2620));
            // 줄눈: 가로줄과, 줄마다 반 칸씩 어긋난 세로줄
            var joint = UiStyle.Hex(0x211c17);
            const float row = 46, brick = 132;
            for (int r = 0; ChipTop + r * row < ChipCounterY; r++)
            {
                float y = ChipTop + r * row;
                quads.Add(0, y - 1.5f, Width, y + 1.5f, joint);
                for (float x = r % 2 == 1 ? brick / 2 : 0; x < Width; x += brick)
                    quads.Add(x - 1.5f, y, x + 1.5f, Mathf.Min(y + row, ChipCounterY), joint);
            }
            // 창밖 하늘: 아치 모양을 얇은 띠로 채운다 (위에서 아래로 그 층 하늘의 색)
            TowerScene.SkyAt(_altitude, out var skyTop, out var skyBottom);
            const float strip = 4, radius = WindowWidth / 2;
            foreach (float cx in ChipWindows)
            {
                for (float y = WindowTop; y < WindowBottom; y += strip)
                {
                    float mid = y + strip / 2, half = radius;
                    if (mid < WindowTop + radius)
                    {
                        float dy = WindowTop + radius - mid;
                        half = Mathf.Sqrt(Mathf.Max(0, radius * radius - dy * dy));
                    }
                    float t0 = (y - WindowTop) / (WindowBottom - WindowTop), t1 = (y + strip - WindowTop) / (WindowBottom - WindowTop);
                    Color c0 = TowerScene.Mix(skyTop, skyBottom, 0.15f + 0.75f * t0), c1 = TowerScene.Mix(skyTop, skyBottom, 0.15f + 0.75f * t1);
                    quads.Add(cx - half, y, cx + half, y + strip, c0, c0, c1, c1);
                }
            }
            // 등불의 빛 (선형 색 공간이라 시안의 절반쯤으로 낮춘다)
            foreach (float x in ChipLanterns)
                quads.AddGlow(x, 156, 240, new Color(1f, 207 / 255f, 122 / 255f, 0.15f));
            quads.Flush(context);
        }

        /// <summary>창틀, 등불, 이름 판의 사슬, 주인.</summary>
        void ChipFixtures(MeshGenerationContext context)
        {
            var p = context.painter2D;
            foreach (float cx in ChipWindows)
            {
                p.strokeColor = Ink;
                p.lineWidth = 10;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                TowerScene.Arch(p, cx, WindowBottom, WindowWidth, WindowBottom - WindowTop);
                p.Stroke();
                p.lineWidth = 6;
                Line(p, cx, WindowTop, cx, WindowBottom);
                Line(p, cx - WindowWidth / 2, WindowTop + 100, cx + WindowWidth / 2, WindowTop + 100);
                TowerScene.FillRect(p, cx - WindowWidth / 2 - 14, WindowBottom - 4, WindowWidth + 28, 14, Ink);
            }
            foreach (float x in ChipLanterns)
                Lantern(p, x, 116);
            if (!float.IsNaN(SignLeft))
            {
                p.strokeColor = UiStyle.Stone9;
                p.lineWidth = 4;
                Line(p, SignLeft, ChipTop, SignLeft, SignTop);
                Line(p, SignRight, ChipTop, SignRight, SignTop);
            }
            Keeper(p, ChipKeeperX, ChipKeeperY, UiStyle.Hex(0x12332d), Teal);
            // 챙 모자와 가슴의 마름모
            p.fillColor = Teal;
            p.strokeColor = UiStyle.Stone9;
            p.lineWidth = 3;
            p.lineJoin = LineJoin.Round;
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                p.MoveTo(At(ChipKeeperX, ChipKeeperY, -50, -6));
                p.QuadraticCurveTo(At(ChipKeeperX, ChipKeeperY, 0, -40), At(ChipKeeperX, ChipKeeperY, 50, -6));
                p.LineTo(At(ChipKeeperX, ChipKeeperY, 70, 6));
                p.QuadraticCurveTo(At(ChipKeeperX, ChipKeeperY, 0, -22), At(ChipKeeperX, ChipKeeperY, -70, 6));
                p.ClosePath();
                if (pass == 0) p.Fill();
                else p.Stroke();
            }
            p.BeginPath();
            p.MoveTo(At(ChipKeeperX, ChipKeeperY, 0, 62));
            p.LineTo(At(ChipKeeperX, ChipKeeperY, 13, 77));
            p.LineTo(At(ChipKeeperX, ChipKeeperY, 0, 92));
            p.LineTo(At(ChipKeeperX, ChipKeeperY, -13, 77));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>계산대: 뒤쪽 나무 모서리, 청록 천, 앞쪽 나무 모서리, 그 아래 바닥.</summary>
        void ChipCounter(MeshGenerationContext context)
        {
            var quads = new TowerScene.QuadBatch();
            quads.Add(0, ChipCounterY, Width, ChipClothTop, WoodLight);
            quads.Add(0, ChipCounterY, Width, ChipCounterY + 4, WoodEdge);
            Gradient(quads, 0, ChipClothTop, Width, ChipClothBottom, UiStyle.Hex(0x1b4f46), UiStyle.Hex(0x2a7466));
            quads.Add(0, ChipClothBottom, Width, ChipFloorY, Wood);
            quads.Add(0, ChipClothBottom, Width, ChipClothBottom + 4, UiStyle.Hex(0x8a6a42));
            quads.Add(0, ChipFloorY, Width, Height, UiStyle.Hex(0x17130f));
            quads.Flush(context);
        }

        /// <summary>천의 테두리 선과 칸을 나누는 점선.</summary>
        void ChipTrim(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.strokeColor = UiStyle.Alpha(Teal, 0.2f);
            p.lineWidth = 3;
            p.lineJoin = LineJoin.Miter;
            Frame(p, 16, ChipClothTop + 14, Width - 16, ChipClothBottom - 14);
            if (!float.IsNaN(DividerX))
            {
                p.strokeColor = UiStyle.Alpha(Teal, 0.3f);
                Dashes(p, DividerX, ChipClothTop + 28, ChipClothBottom - 16);
            }
        }

        // ================= 골드 상점 =================

        static readonly float[] StallRidge = { 872, 760, 830, 710, 820, 760, 860, 730, 840, 760, 850, 790, 880 };
        static readonly float[] StallRidgeX = { 0, 130, 250, 400, 570, 700, 890, 1060, 1240, 1390, 1570, 1740, 1920 };
        const float GroundY = 862;

        void StallSky(MeshGenerationContext context)
        {
            var quads = new TowerScene.QuadBatch();
            TowerScene.SkyAt(1, out var top, out var bottom);
            Gradient(quads, 0, 0, Width, Height, top, bottom);
            quads.Flush(context);
        }

        /// <summary>해, 구름, 먼 산.</summary>
        void StallFar(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var sun = UiStyle.Hex(0xfff3cf);
            TowerScene.Disc(p, 1660, 330, 150, UiStyle.Alpha(sun, 0.06f));
            TowerScene.Disc(p, 1660, 330, 100, UiStyle.Alpha(sun, 0.1f));
            TowerScene.Disc(p, 1660, 330, 56, UiStyle.Alpha(sun, 0.92f));
            TowerScene.Cloud(p, 1120, 316, 250, new Color(1, 1, 1, 0.5f));
            TowerScene.Cloud(p, 420, 262, 210, new Color(1, 1, 1, 0.34f));
            p.fillColor = UiStyle.Hex(0xc4a67c);
            p.BeginPath();
            p.MoveTo(new Vector2(0, Height));
            for (int i = 0; i < StallRidge.Length; i++)
                p.LineTo(new Vector2(StallRidgeX[i], StallRidge[i]));
            p.LineTo(new Vector2(Width, Height));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>땅과, 왼쪽에 걸친 탑 몸통.</summary>
        void StallGround(MeshGenerationContext context)
        {
            var quads = new TowerScene.QuadBatch();
            Gradient(quads, 0, GroundY, Width, Height, UiStyle.Hex(0xe3c084), UiStyle.Hex(0xb58a54));
            quads.AddShade(-260, -20, 580, GroundY + 40, TowerScene.BodyShade);
            foreach (float y in new[] { 226f, 526f })
            {
                quads.Add(-260, y + 12, 320, y + 21, new Color(40 / 255f, 28 / 255f, 12 / 255f, 0.28f));
                quads.AddShade(-276, y - 2, 612, 14, TowerScene.EaveShade);
            }
            // 받침돌
            quads.Add(-296, GroundY + 18, 356, GroundY + 78, UiStyle.Hex(0x6f5b40));
            quads.Add(-296, GroundY + 18, 356, GroundY + 28, UiStyle.Hex(0x8f7752));
            quads.Flush(context);
        }

        /// <summary>탑의 문과 창, 노점의 기둥·들보·차양, 주인.</summary>
        void StallFrame(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var dark = UiStyle.Hex(0x2c2114);
            p.fillColor = UiStyle.Hex(0x231a10);
            p.BeginPath();
            TowerScene.Arch(p, 30, GroundY + 18, 72, 212);
            p.Fill();
            p.strokeColor = UiStyle.Hex(0x8c6f3f);
            p.lineWidth = 6;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            TowerScene.Arch(p, 30, GroundY + 18, 72, 212);
            p.Stroke();
            p.fillColor = dark;
            foreach (float y in new[] { 420f, 720f })
            {
                p.BeginPath();
                TowerScene.Arch(p, 214, y, 26, 50);
                p.Fill();
            }

            // 기둥과 들보
            foreach (float x in new[] { 228f, 1666f })
                Board(p, x, 150, 26, StallClothBottom - 150 + 26);
            Board(p, 150, 30, 1620, 40);
            // 차양: 금색과 크림색 줄무늬, 아래는 둥글게
            for (int i = 0; i < 13; i++)
            {
                float x = 180 + i * 120;
                p.fillColor = i % 2 == 0 ? UiStyle.Hex(0xc8922f) : UiStyle.Paper;
                p.strokeColor = UiStyle.Hex(0x3a2a18);
                p.lineWidth = 3;
                p.lineJoin = LineJoin.Round;
                for (int pass = 0; pass < 2; pass++)
                {
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, 60));
                    p.LineTo(new Vector2(x + 120, 60));
                    p.LineTo(new Vector2(x + 120, 168));
                    p.QuadraticCurveTo(new Vector2(x + 60, 206), new Vector2(x, 168));
                    p.ClosePath();
                    if (pass == 0) p.Fill();
                    else p.Stroke();
                }
            }

            Keeper(p, StallKeeperX, StallKeeperY, UiStyle.Hex(0x3a2a10), UiStyle.Gold);
            // 챙 넓은 모자
            p.fillColor = UiStyle.Gold;
            p.strokeColor = UiStyle.Stone9;
            p.lineWidth = 3;
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                p.MoveTo(At(StallKeeperX, StallKeeperY, -84, -20));
                p.QuadraticCurveTo(At(StallKeeperX, StallKeeperY, 0, -48), At(StallKeeperX, StallKeeperY, 84, -20));
                p.QuadraticCurveTo(At(StallKeeperX, StallKeeperY, 0, -4), At(StallKeeperX, StallKeeperY, -84, -20));
                p.ClosePath();
                if (pass == 0) p.Fill();
                else p.Stroke();
            }
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                p.MoveTo(At(StallKeeperX, StallKeeperY, -38, -26));
                p.QuadraticCurveTo(At(StallKeeperX, StallKeeperY, -34, -76), At(StallKeeperX, StallKeeperY, 0, -78));
                p.QuadraticCurveTo(At(StallKeeperX, StallKeeperY, 34, -76), At(StallKeeperX, StallKeeperY, 38, -26));
                p.QuadraticCurveTo(At(StallKeeperX, StallKeeperY, 0, -36), At(StallKeeperX, StallKeeperY, -38, -26));
                p.ClosePath();
                if (pass == 0) p.Fill();
                else p.Stroke();
            }
            p.strokeColor = UiStyle.GoldDeep;
            p.lineWidth = 6;
            p.BeginPath();
            p.MoveTo(At(StallKeeperX, StallKeeperY, -35, -35));
            p.QuadraticCurveTo(At(StallKeeperX, StallKeeperY, 0, -45), At(StallKeeperX, StallKeeperY, 35, -35));
            p.Stroke();
        }

        /// <summary>좌판: 뒤쪽 나무 모서리, 붉은 천, 앞쪽 나무 모서리, 그 아래 판자.</summary>
        void StallTable(MeshGenerationContext context)
        {
            var quads = new TowerScene.QuadBatch();
            quads.Add(StallLeft, StallCounterY, StallRight, StallClothTop, WoodLight);
            quads.Add(StallLeft, StallCounterY, StallRight, StallCounterY + 4, WoodEdge);
            Gradient(quads, StallLeft, StallClothTop, StallRight, StallClothBottom, UiStyle.Hex(0x6d2b22), UiStyle.Hex(0x8f3a2c));
            quads.Add(StallLeft, StallClothBottom, StallRight, StallClothBottom + 26, Wood);
            quads.Add(StallLeft, StallClothBottom, StallRight, StallClothBottom + 4, UiStyle.Hex(0x8a6a42));
            quads.Add(StallLeft + 26, StallClothBottom + 26, StallRight - 26, GroundY + 40, UiStyle.Hex(0x3f2c1b));
            quads.Flush(context);
        }

        void StallTrim(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.strokeColor = UiStyle.Alpha(UiStyle.Gold, 0.24f);
            p.lineWidth = 3;
            p.lineJoin = LineJoin.Miter;
            Frame(p, StallLeft + 16, StallClothTop + 14, StallRight - 16, StallClothBottom - 14);
        }

        // ================= 도형 =================

        static Vector2 At(float x, float y, float dx, float dy) => new Vector2(x + dx, y + dy);

        static void Line(Painter2D p, float x0, float y0, float x1, float y1)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x0, y0));
            p.LineTo(new Vector2(x1, y1));
            p.Stroke();
        }

        static void Frame(Painter2D p, float x0, float y0, float x1, float y1)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x0, y0));
            p.LineTo(new Vector2(x1, y0));
            p.LineTo(new Vector2(x1, y1));
            p.LineTo(new Vector2(x0, y1));
            p.ClosePath();
            p.Stroke();
        }

        /// <summary>세로 점선 (10 긋고 10 띄운다).</summary>
        static void Dashes(Painter2D p, float x, float y0, float y1)
        {
            p.lineCap = LineCap.Butt;
            for (float y = y0; y < y1; y += 20)
                Line(p, x, y, x, Mathf.Min(y + 10, y1));
        }

        /// <summary>테두리가 있는 나무 판 (기둥, 들보).</summary>
        static void Board(Painter2D p, float x, float y, float w, float h)
        {
            TowerScene.FillRect(p, x, y, w, h, Wood);
            p.strokeColor = WoodDark;
            p.lineWidth = 3;
            p.lineJoin = LineJoin.Miter;
            Frame(p, x, y, x + w, y + h);
        }

        /// <summary>세로로 색이 이어지는 사각형 (띠를 잘게 나눠 화면 색 기준으로 섞는다).</summary>
        static void Gradient(TowerScene.QuadBatch quads, float x0, float y0, float x1, float y1, Color top, Color bottom)
        {
            const int bands = 12;
            for (int i = 0; i < bands; i++)
            {
                float t0 = i / (float)bands, t1 = (i + 1) / (float)bands;
                Color c0 = TowerScene.Mix(top, bottom, t0), c1 = TowerScene.Mix(top, bottom, t1);
                quads.Add(x0, Mathf.Lerp(y0, y1, t0), x1, Mathf.Lerp(y0, y1, t1), c0, c0, c1, c1);
            }
        }

        /// <summary>매단 등불: 사슬, 갓, 불 켠 유리, 받침. (x, y)는 갓의 위 가운데.</summary>
        static void Lantern(Painter2D p, float x, float y)
        {
            p.strokeColor = UiStyle.Stone9;
            p.lineWidth = 4;
            Line(p, x, ChipTop, x, y);
            p.fillColor = Iron;
            p.BeginPath();
            p.MoveTo(new Vector2(x - 22, y));
            p.LineTo(new Vector2(x + 22, y));
            p.LineTo(new Vector2(x + 30, y + 12));
            p.LineTo(new Vector2(x - 30, y + 12));
            p.ClosePath();
            p.Fill();
            TowerScene.FillRect(p, x - 24, y + 12, 48, 56, UiStyle.Hex(0xffd98a));
            p.strokeColor = Iron;
            p.lineWidth = 4;
            p.lineJoin = LineJoin.Miter;
            Frame(p, x - 24, y + 12, x + 24, y + 68);
            p.lineWidth = 3;
            Line(p, x, y + 12, x, y + 68);
            Line(p, x - 24, y + 40, x + 24, y + 40);
            p.fillColor = Iron;
            p.BeginPath();
            p.MoveTo(new Vector2(x - 30, y + 68));
            p.LineTo(new Vector2(x + 30, y + 68));
            p.LineTo(new Vector2(x + 20, y + 80));
            p.LineTo(new Vector2(x - 20, y + 80));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>
        /// 주인: 탑 장면의 말과 같은 모양(몸통과 머리)을 크게 그린다. (x, y)는 머리 가운데. 허리 아래는 계산대가 가린다.
        /// </summary>
        static void Keeper(Painter2D p, float x, float y, Color fill, Color line)
        {
            p.fillColor = fill;
            p.strokeColor = line;
            p.lineWidth = 6;
            p.lineJoin = LineJoin.Round;
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x - 42, y + 50));
                p.QuadraticCurveTo(new Vector2(x, y + 28), new Vector2(x + 42, y + 50));
                p.LineTo(new Vector2(x + 84, y + 200));
                p.LineTo(new Vector2(x - 84, y + 200));
                p.ClosePath();
                if (pass == 0) p.Fill();
                else p.Stroke();
            }
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                TowerScene.Ellipse(p, x, y, 46, 46);
                if (pass == 0) p.Fill();
                else p.Stroke();
            }
        }
    }
}
