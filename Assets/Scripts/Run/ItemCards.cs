// 아이템 카드 (규칙 결정 문서 "아이템 카드와 귀속"). 듀얼 효과는 원작 그대로 두고, 탑 진행에서만 작용하는 탑 효과를 따로 가진다.
// - 탑 효과는 카드명 기준이다 (같은 이름이면 어디서 얻었든 같다). 효과가 나는 위치(메인 덱·상점 덱), 중첩, 귀속은 카드마다 정한다.
// - 적용: 플레이어가 덱(메인 덱 / 상점 덱)마다 카드명 단위로 적용한다. 같은 덱의 같은 카드명은 전부 적용된 것으로 본다.
//   메인 덱과 상점 덱은 따로 적용한다. 중첩되지 않는 효과는 두 곳에 적용해도 한 번만 친다.
// - 그 덱에서 같은 카드명이 전부 빠지면(전부 봉인 등) 적용이 풀린다. 봉인된 카드는 탑 효과가 없다.
// - 귀속: 메인 덱에서 적용하면 귀속도 걸려, 봉인될 때까지 메인 덱에서 뺄 수 없다 (상점 덱 카드는 봉인되지 않고 옮길 수도 없다).
// 목록은 견본이다 (규칙 결정 문서: 목록·가격 미정). 이 파일은 듀얼 엔진·Unity를 모른다.
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public enum ItemEffectKind
    {
        /// <summary>탑 미리보기 +Amount층 (바로 위 층부터 몇 층의 종류가 보이는지).</summary>
        PreviewFloors,
        /// <summary>코인 토스에서 이길 확률 +Amount%p.</summary>
        CoinTossWin,
    }

    public sealed class ItemCard
    {
        public uint Code { get; }
        /// <summary>공식 한글 카드명 (카드 정보의 이름과 같은지 시험한다).</summary>
        public string Name { get; }
        public ItemEffectKind Effect { get; }
        public int Amount { get; }
        /// <summary>두 덱(또는 여러 카드명)에 적용하면 효과가 합쳐지는지.</summary>
        public bool Stacks { get; }
        /// <summary>메인 덱에서 적용하면 귀속이 걸리는지.</summary>
        public bool Bound { get; }
        /// <summary>메인 덱 / 상점 덱에서 효과가 나는지.</summary>
        public bool InMain { get; }
        public bool InShop { get; }
        /// <summary>칩교환소 가격 (칩, 임시 값).</summary>
        public int Price { get; }

        internal ItemCard(uint code, string name, ItemEffectKind effect, int amount, bool stacks, bool bound, bool inMain, bool inShop, int price)
        {
            Code = code;
            Name = name;
            Effect = effect;
            Amount = amount;
            Stacks = stacks;
            Bound = bound;
            InMain = inMain;
            InShop = inShop;
            Price = price;
        }

        public string EffectText =>
            Effect == ItemEffectKind.PreviewFloors ? $"탑 미리보기 +{Amount}층" : $"코인 토스에서 이길 확률 +{Amount}%p";

        /// <summary>"탑 미리보기 +1층 · 메인 덱·상점 덱 · 중첩 안 됨 · 귀속"</summary>
        public string Describe()
        {
            var where = new List<string>();
            if (InMain) where.Add("메인 덱");
            if (InShop) where.Add("상점 덱");
            return $"{EffectText} · 효과 위치 {string.Join("·", where)} · {(Stacks ? "중첩됨" : "중첩 안 됨")}" + (Bound ? " · 귀속 (메인 덱에서 적용하면 봉인될 때까지 뺄 수 없음)" : "");
        }
    }

    public static class ItemCards
    {
        public static readonly IReadOnlyList<ItemCard> All = new[]
        {
            new ItemCard(17092736, "고대의 망원경", ItemEffectKind.PreviewFloors, 1, stacks: false, bound: true, inMain: true, inShop: true, price: 60),
            new ItemCard(74137509, "천사의 주사위", ItemEffectKind.CoinTossWin, 20, stacks: false, bound: false, inMain: true, inShop: true, price: 80),
        };

        /// <summary>카드명 기준으로 아이템 카드를 찾는다 (일러스트가 다른 ID도 같은 아이템). 아니면 null.</summary>
        public static ItemCard Of(uint code, CardDatabase database)
        {
            uint name = database != null ? database.NameCode(code) : code;
            return All.FirstOrDefault(i => i.Code == name || i.Code == code);
        }
    }
}
