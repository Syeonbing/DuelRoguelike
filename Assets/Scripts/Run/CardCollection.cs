// 컬렉션: 한 번이라도 얻은 카드와 장수. 영구히 유지되고, 얻은 장수만큼 시작 덱(메인·엑스트라·사이드 합)에 넣을 수 있다.
// 저장은 아직 없어서 메모리에만 둔다. 카드 코드와 장수만 들고 등급은 들지 않는다 (등급은 바뀔 수 있다).
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public sealed class CardCollection
    {
        readonly Dictionary<uint, int> _counts = new Dictionary<uint, int>();

        public IReadOnlyDictionary<uint, int> Cards => _counts;

        /// <summary>모든 카드의 장수 합.</summary>
        public int TotalCount => _counts.Values.Sum();

        public int Count(uint code) => _counts.TryGetValue(code, out int n) ? n : 0;

        public void Add(uint code, int count = 1)
        {
            if (count <= 0)
                return;
            _counts[code] = Count(code) + count;
        }

        /// <summary>레시피의 카드를 장수만큼 모두 더한 컬렉션. 첫 버전은 스타터 덱 4개를 지급한 것으로 시작한다 (임시).</summary>
        public static CardCollection FromRecipes(IEnumerable<DeckRecipe> recipes)
        {
            var collection = new CardCollection();
            foreach (var recipe in recipes)
                foreach (var (code, count) in recipe.Entries)
                    collection.Add(code, count);
            return collection;
        }

        /// <summary>카드 목록(시작 덱 + 사이드 덱)이 컬렉션 장수를 넘는 카드. 문제가 없으면 빈 목록.</summary>
        public List<string> CheckCards(IEnumerable<uint> cards, CardDatabase database)
        {
            var problems = new List<string>();
            foreach (var group in cards.GroupBy(c => c).OrderBy(g => g.Key))
            {
                int owned = Count(group.Key);
                if (group.Count() > owned)
                    problems.Add($"{group.Key} {database.GetName(group.Key)}: {group.Count()}장을 넣었지만 컬렉션에는 {owned}장");
            }
            return problems;
        }
    }
}
