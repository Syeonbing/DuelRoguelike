// 칩교환소 (규칙 결정 문서 2026-11-10 "칩교환소", "봉인·봉인 해제·봉인 대상 교체"). 10층 묶음의 5층마다 나오는 비전투 층이고, 칩은 여기서 쓴다.
// 상품 (가격은 RunRules.ShopPrice, 임시 값):
// - 봉인 대상 교체: 봉인된 카드 하나를 골라 풀고(사이드 덱으로), 같은 덱(메인↔메인, 엑스트라↔엑스트라)의 다른 카드 하나를 대신 봉인한다.
//   같은 레어도끼리 / 레어도 상관없음 두 종류, 새로 봉인할 카드를 무작위로 / 직접 고르는 것 두 종류 = 상품 4개.
//   교체하면 메인 덱이 1장 줄어 40장 아래가 될 수 있다 (그러면 보충). 보충할 수 없게 되는 교체는 팔지 않는다.
// - 봉인 해제: 봉인된 카드 하나를 골라 풀어 사이드 덱으로. 매우 귀하게: 가격이 높고, 칩교환소마다 확률로만 나온다.
// - 아이템 카드: 자리만 있다 (목록·탑 효과·상점 덱은 다음 단계).
// 상품마다 한 칩교환소에서 한 번만 살 수 있다. 진열(어떤 상품이 나왔는지)은 (런 시드, 층)으로 정해진다.
using System.Collections.Generic;

namespace DuelRoguelike.Run
{
    /// <summary>칩교환소 상품. 저장 파일에 이름으로 적는다 (이름은 바꾸지 않는다).</summary>
    public enum ShopProduct
    {
        /// <summary>봉인 대상 교체: 같은 레어도, 새로 봉인할 카드는 무작위</summary>
        SwapSameRarity,
        /// <summary>봉인 대상 교체: 같은 레어도, 새로 봉인할 카드를 직접 고름</summary>
        SwapSameRarityChosen,
        /// <summary>봉인 대상 교체: 레어도 상관없음, 무작위</summary>
        SwapAny,
        /// <summary>봉인 대상 교체: 레어도 상관없음, 직접 고름</summary>
        SwapAnyChosen,
        /// <summary>봉인 해제</summary>
        Unseal,
    }

    /// <summary>칩교환소에 진열된 상품 하나.</summary>
    public sealed class ShopOffer
    {
        public ShopProduct Product { get; }

        /// <summary>진열할 때의 가격 (칩).</summary>
        public int Price { get; }

        /// <summary>이 칩교환소에서 이미 샀는지.</summary>
        public bool Sold { get; internal set; }

        internal ShopOffer(ShopProduct product, int price, bool sold = false)
        {
            Product = product;
            Price = price;
            Sold = sold;
        }
    }

    public static class ChipExchange
    {
        /// <summary>아이템 카드 칸에 보여 줄 글 (팔지 않는 자리).</summary>
        public const string ItemCardSlotTitle = "아이템 카드";
        public const string ItemCardSlotText = "준비 중입니다. 아이템 카드(탑 효과가 있는 카드)와 상점 덱은 다음 단계에서 들어옵니다.";

        public static bool IsSwap(ShopProduct product) => product != ShopProduct.Unseal;

        /// <summary>새로 봉인할 카드가 풀 카드와 같은 레어도여야 하는지.</summary>
        public static bool SameRarity(ShopProduct product) => product == ShopProduct.SwapSameRarity || product == ShopProduct.SwapSameRarityChosen;

        /// <summary>새로 봉인할 카드를 플레이어가 고르는지 (아니면 무작위).</summary>
        public static bool ChoosesNewSeal(ShopProduct product) => product == ShopProduct.SwapSameRarityChosen || product == ShopProduct.SwapAnyChosen;

        public static string Name(ShopProduct product)
        {
            switch (product)
            {
                case ShopProduct.SwapSameRarity: return "봉인 대상 교체 (같은 레어도)";
                case ShopProduct.SwapSameRarityChosen: return "봉인 대상 교체 (같은 레어도, 직접 고름)";
                case ShopProduct.SwapAny: return "봉인 대상 교체 (레어도 상관없음)";
                case ShopProduct.SwapAnyChosen: return "봉인 대상 교체 (레어도 상관없음, 직접 고름)";
                case ShopProduct.Unseal: return "봉인 해제";
                default: return product.ToString();
            }
        }

        public static string Description(ShopProduct product)
        {
            if (product == ShopProduct.Unseal)
                return "봉인된 카드 1장을 골라 봉인을 풉니다. 풀린 카드는 사이드 덱으로 갑니다.";
            string rarity = SameRarity(product) ? "같은 레어도의 " : "";
            string pick = ChoosesNewSeal(product) ? "직접 골라" : "무작위로";
            return $"봉인된 카드 1장을 골라 풀고(사이드 덱으로), 같은 덱의 {rarity}다른 카드 1장을 {pick} 대신 봉인합니다. 봉인된 장수는 그대로입니다.";
        }

        /// <summary>이 칩교환소(층)의 진열: 봉인 대상 교체 4개는 늘, 봉인 해제는 확률로.</summary>
        public static List<ShopOffer> Stock(ulong seed, int floor)
        {
            var stock = new List<ShopOffer>();
            foreach (var product in new[] { ShopProduct.SwapSameRarity, ShopProduct.SwapSameRarityChosen, ShopProduct.SwapAny, ShopProduct.SwapAnyChosen })
                stock.Add(new ShopOffer(product, RunRules.ShopPrice(product)));
            if (RunRandom.For(seed, floor, RunRandomPurpose.Shop).Next(100) < RunRules.UnsealOfferPercent)
                stock.Add(new ShopOffer(ShopProduct.Unseal, RunRules.ShopPrice(ShopProduct.Unseal)));
            return stock;
        }
    }
}
