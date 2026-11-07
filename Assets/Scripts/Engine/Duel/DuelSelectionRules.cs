// 여러 장을 고르는 요청이 엔진에 받아들여질지 미리 확인한다 (규칙은 ygopro-core playerop.cpp와 같다).
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuelRoguelike.Engine.Duel
{
    public static class DuelSelectionRules
    {
        public static bool IsValidCards(SelectCardRequest r, IReadOnlyCollection<int> chosen) =>
            chosen.Count >= r.Min && chosen.Count <= r.Max;

        /// <summary>릴리스: 장수는 Max 이하, 릴리스 수(ReleaseParam 합)는 Min 이상.</summary>
        public static bool IsValidTribute(SelectTributeRequest r, IReadOnlyCollection<int> chosen) =>
            chosen.Count <= r.Max && chosen.Sum(i => r.Cards[i].ReleaseParam) >= r.Min;

        public static bool IsValidSum(SelectSumRequest r, IReadOnlyCollection<int> chosen)
        {
            var all = r.MustSelect.Concat(chosen.Select(i => r.Selectable[i])).ToList();
            if (r.Exact)
                return chosen.Count >= r.Min && chosen.Count <= r.Max && CanSumExactly(all, 0, r.Target);

            int sum = 0, smallest = int.MaxValue, largestTotal = 0;
            foreach (var o in all)
            {
                int o1 = (int)(o.Param & 0xffff), o2 = (int)(o.Param >> 16);
                int low = (o2 != 0 && o2 < o1) ? o2 : o1;
                sum += low;
                largestTotal += Math.Max(o1, o2);
                smallest = Math.Min(smallest, low);
            }
            return all.Count > 0 && largestTotal >= r.Target && sum - smallest < r.Target;
        }

        /// <summary>조건에 맞는 조합 하나를 찾는다 (선택지가 적다고 보고 전부 시도한다). 없으면 null.</summary>
        public static List<int> FindSum(SelectSumRequest r)
        {
            int n = Math.Min(r.Selectable.Count, 20);
            for (int mask = 1; mask < (1 << n); mask++)
            {
                var chosen = new List<int>();
                for (int i = 0; i < n; i++)
                    if ((mask & (1 << i)) != 0)
                        chosen.Add(i);
                if (IsValidSum(r, chosen))
                    return chosen;
            }
            return null;
        }

        static bool CanSumExactly(List<SumOption> cards, int index, int left)
        {
            if (index == cards.Count)
                return left == 0;
            int o1 = (int)(cards[index].Param & 0xffff), o2 = (int)(cards[index].Param >> 16);
            if (left >= o1 && CanSumExactly(cards, index + 1, left - o1))
                return true;
            return o2 > 0 && left >= o2 && CanSumExactly(cards, index + 1, left - o2);
        }
    }
}
