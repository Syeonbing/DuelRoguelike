// 탑 구조 (규칙 결정 문서 2026-11-10 "탑 구조"). 탑은 10층 묶음이 반복되고, 층 번호의 끝자리로 층 종류가 정해진다.
//   1~3층 전투 (층마다 10% 확률로 이벤트), 4층 전투 (30% 확률로 이벤트), 5층 칩교환소, 6층 이벤트(반드시),
//   7~8층 전투 (층마다 5% 확률로 이벤트), 9층 휴식 (LP를 8000으로 맞춘다), 10층 보스.
// 최소 전투 층: 10층 묶음마다 1~4층 중 1개 이상, 7~8층 중 1개 이상은 전투 층이다 (모두 이벤트로 뽑히면 그중 하나를 무작위로 전투로 되돌린다).
// 층 종류는 (런 시드, 묶음 첫 층)으로 정해진다: 같은 시드면 늘 같다. 이벤트 내용은 층에 들어갈 때 정한다 (RunState.EnterFloor).
// 비전투 층도 층 번호에 센다 (봉인 공식의 층 항, 승리 보상 확률표, 보스 주기가 모두 이 번호를 따른다).
//
// 이벤트: 이벤트 층에 들어가면 성향과 태그로 이벤트를 고른다 (TowerEvent.cs, TowerEventList.cs, RunStateEvent.cs).
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuelRoguelike.Run
{
    public enum FloorKind
    {
        /// <summary>전투 층: 단판 듀얼 하나. 상대 덱은 무작위.</summary>
        Battle,
        /// <summary>이벤트 층 (6층, 또는 확률로 1~4층·7~8층)</summary>
        Event,
        /// <summary>칩교환소 (5층): 칩을 쓰는 곳</summary>
        ChipExchange,
        /// <summary>휴식 (9층): LP를 8000으로 맞춘다</summary>
        Rest,
        /// <summary>보스 층 (10층): 전투 층. 이기면 보스 보상</summary>
        Boss,
    }

    public static class Tower
    {
        public const int BlockSize = RunRules.BossFloorInterval;

        /// <summary>10층 묶음 안의 자리 (끝자리, 1~10).</summary>
        public static int Position(int floor)
        {
            if (floor < 1)
                throw new ArgumentOutOfRangeException(nameof(floor), floor, "층은 1부터입니다.");
            return (floor - 1) % BlockSize + 1;
        }

        /// <summary>이 층이 든 10층 묶음의 첫 층 (1, 11, 21…).</summary>
        public static int BlockStart(int floor) => floor - Position(floor) + 1;

        /// <summary>듀얼이 일어나는 층인지 (전투 층, 보스 층).</summary>
        public static bool IsBattle(FloorKind kind) => kind == FloorKind.Battle || kind == FloorKind.Boss;

        /// <summary>확률 이벤트를 보기 전의 층 종류: 5층 칩교환소, 6층 이벤트, 9층 휴식, 10층 보스, 나머지 전투.</summary>
        public static FloorKind FixedKind(int floor)
        {
            switch (Position(floor))
            {
                case 5: return FloorKind.ChipExchange;
                case 6: return FloorKind.Event;
                case 9: return FloorKind.Rest;
                case 10: return FloorKind.Boss;
                default: return FloorKind.Battle;
            }
        }

        /// <summary>이 층이 확률로 이벤트 층이 될 확률(%). 0이면 종류가 정해진 층.</summary>
        public static int RandomEventPercent(int floor) => RunRules.RandomEventPercent[Position(floor)];

        /// <summary>floor가 든 10층 묶음의 층 종류 10개 (칸 0 = 묶음 첫 층).</summary>
        public static FloorKind[] Block(ulong seed, int floor)
        {
            int start = BlockStart(floor);
            var kinds = new FloorKind[BlockSize];
            var random = RunRandom.For(seed, start, RunRandomPurpose.Tower);
            for (int position = 1; position <= BlockSize; position++)
            {
                int f = start + position - 1;
                kinds[position - 1] = FixedKind(f);
                int percent = RandomEventPercent(f);
                if (percent > 0 && random.Next(100) < percent)
                    kinds[position - 1] = FloorKind.Event;
            }
            KeepOneBattle(kinds, 1, 4, random);
            KeepOneBattle(kinds, 7, 8, random);
            return kinds;
        }

        /// <summary>자리 from~to가 모두 이벤트로 뽑혔으면 그중 하나를 무작위로 전투 층으로 되돌린다.</summary>
        static void KeepOneBattle(FloorKind[] kinds, int from, int to, RunRandom random)
        {
            for (int p = from; p <= to; p++)
                if (kinds[p - 1] == FloorKind.Battle)
                    return;
            kinds[from + random.Next(to - from + 1) - 1] = FloorKind.Battle;
        }

        /// <summary>이 런(시드)에서 이 층의 종류.</summary>
        public static FloorKind KindOf(ulong seed, int floor) => Block(seed, floor)[Position(floor) - 1];

        public static string KindName(FloorKind kind)
        {
            switch (kind)
            {
                case FloorKind.Battle: return "전투";
                case FloorKind.Event: return "이벤트";
                case FloorKind.ChipExchange: return "칩교환소";
                case FloorKind.Rest: return "휴식";
                case FloorKind.Boss: return "보스";
                default: return kind.ToString();
            }
        }
    }
}
