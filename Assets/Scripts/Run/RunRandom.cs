// 런에서 쓰는 난수. 런 시드 + 듀얼 번호 + 용도로 그때그때 새 난수열을 만든다 (SplitMix64).
// - 앞에서 난수를 얼마나 썼는지와 상관없이 같은 (시드, 듀얼 번호, 용도)면 늘 같은 값이 나온다.
// - 나중에 층 단위로 저장할 때 런 시드와 듀얼 번호만 있으면 이어서 같은 결과를 낼 수 있다.
// System.Random은 상태를 저장할 수 없고 구현에 따라 값이 바뀔 수 있어서 쓰지 않는다.
using System;

namespace DuelRoguelike.Run
{
    /// <summary>난수를 쓰는 곳. 값을 바꾸면 같은 시드의 런 결과가 바뀌므로 늘리기만 한다.</summary>
    public enum RunRandomPurpose
    {
        /// <summary>그 층의 상대 덱 고르기</summary>
        Opponent = 1,
        /// <summary>엔진 듀얼 시드 (ocgcore 시드 4개, 덱 섞기)</summary>
        Duel = 2,
        /// <summary>승리 보상 (등급 뽑기, 카드 고르기)</summary>
        Reward = 3,
        /// <summary>패배 시 기절할 카드 고르기</summary>
        Faint = 4,
        /// <summary>런 규칙 밖에서 쓰는 난수 (시험용 자동 플레이어 등)</summary>
        External = 5,
    }

    public sealed class RunRandom
    {
        const ulong Gamma = 0x9E3779B97F4A7C15UL;

        ulong _state;

        public RunRandom(ulong seed)
        {
            _state = seed;
        }

        /// <summary>(런 시드, 듀얼 번호, 용도)에서 난수열을 만든다. 같은 값이면 늘 같은 난수열이다.</summary>
        public static RunRandom For(ulong runSeed, int duelNumber, RunRandomPurpose purpose)
        {
            ulong seed = Mix(runSeed + Gamma * (ulong)(uint)duelNumber);
            seed = Mix(seed ^ ((ulong)purpose * 0xD1B54A32D192ED03UL));
            return new RunRandom(seed);
        }

        public ulong NextULong()
        {
            _state += Gamma;
            return Mix(_state);
        }

        /// <summary>0 이상 maxExclusive 미만의 정수.</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "1 이상이어야 합니다.");
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>0 이상 int.MaxValue / 2 미만의 정수 (다른 난수 생성기의 시드로 넘길 때, 더해도 넘치지 않게).</summary>
        public int NextSeed() => (int)(NextULong() >> 34);

        static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
