// 런 상태: 상점 덱과 아이템 카드 (규칙 결정 문서 "사이드 덱·상점 덱·컬렉션", "아이템 카드와 귀속").
// - 상점 덱: 칩교환소에서 산 카드. 듀얼·보충·교환에 쓰지 않고 봉인되지 않는다. 보유 범위 "상점 덱 포함" 이벤트에서는 가진 것으로 친다.
//   같은 카드명 3장, 전체 10장까지. 런이 끝나면 사라지고 컬렉션에도 들어가지 않는다.
// - 아이템 적용: 덱(메인 덱 / 상점 덱)마다 카드명 단위. 그 덱에서 그 카드명이 다 빠지면 풀린다. 귀속 아이템을 메인 덱에서 적용하면 봉인될 때까지 뺄 수 없다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuelRoguelike.Run
{
    public sealed partial class RunState
    {
        readonly List<uint> _shopDeck = new List<uint>();
        /// <summary>적용한 아이템: (덱, 카드명 코드). 덱은 Main 또는 Shop.</summary>
        readonly SortedSet<(RunPile Pile, uint Name)> _applied = new SortedSet<(RunPile, uint)>();

        /// <summary>상점 덱 (산 순서).</summary>
        public IReadOnlyList<uint> ShopDeck => _shopDeck;

        /// <summary>적용 중인 아이템 (덱, 카드명 코드).</summary>
        public IReadOnlyCollection<(RunPile Pile, uint Name)> AppliedItems => _applied;

        uint NameOf(uint code) => _database.NameCode(code);

        /// <summary>이 카드의 아이템 카드 정보 (카드명 기준). 아이템 카드가 아니면 null.</summary>
        public ItemCard ItemOf(uint code) => ItemCards.Of(code, _database);

        List<uint> ItemPile(RunPile pile) => pile == RunPile.Main ? _main : pile == RunPile.Shop ? _shopDeck : null;

        /// <summary>그 덱에서 이 카드(카드명)의 아이템 효과를 적용했는지.</summary>
        public bool IsApplied(RunPile pile, uint code) => _applied.Contains((pile, NameOf(code)));

        /// <summary>귀속: 메인 덱에서 적용한 귀속 아이템. 봉인될 때까지 메인 덱에서 뺄 수 없다.</summary>
        public bool IsBound(uint code) => ItemCards.Of(code, _database) is ItemCard item && item.Bound && IsApplied(RunPile.Main, code);

        /// <summary>상점 덱에 이 카드를 더 넣을 수 있는지 (같은 카드명 3장, 전체 10장).</summary>
        public bool ShopDeckHasRoom(uint code, out string reason)
        {
            reason = null;
            if (_shopDeck.Count >= RunRules.MaxShopDeck)
                reason = $"상점 덱이 꽉 참 ({RunRules.MaxShopDeck}장)";
            else if (_shopDeck.Count(c => NameOf(c) == NameOf(code)) >= RunRules.MaxCopies)
                reason = $"상점 덱에 같은 카드명이 이미 {RunRules.MaxCopies}장";
            return reason == null;
        }

        /// <summary>아이템 효과를 적용할 수 있는지 (듀얼 밖 Ready, 아이템 카드, 그 덱에 있고 그 덱에서 효과가 나며 아직 적용 전).</summary>
        public bool CanApplyItem(uint code, RunPile pile, out string reason)
        {
            reason = null;
            var item = ItemCards.Of(code, _database);
            var cards = ItemPile(pile);
            if (Phase != RunPhase.Ready)
                reason = PhaseReason();
            else if (item == null)
                reason = "아이템 카드가 아님";
            else if (cards == null)
                reason = "아이템은 메인 덱이나 상점 덱에서만 적용함";
            else if (!(pile == RunPile.Main ? item.InMain : item.InShop))
                reason = $"{RunEvent.PileName(pile)}에서는 효과가 나지 않는 아이템";
            else if (!cards.Any(c => NameOf(c) == NameOf(code)))
                reason = $"{RunEvent.PileName(pile)}에 없는 카드";
            else if (IsApplied(pile, code))
                reason = "이미 적용함";
            return reason == null;
        }

        /// <summary>아이템 효과를 적용한다. 귀속 아이템을 메인 덱에서 적용하면 귀속도 걸린다 (되돌릴 수 없음).</summary>
        public void ApplyItem(uint code, RunPile pile)
        {
            if (!CanApplyItem(code, pile, out string reason))
                throw new InvalidOperationException($"{_database.GetName(code)}의 아이템 효과를 적용할 수 없습니다: {reason}");
            uint name = NameOf(code);
            _applied.Add((pile, name));
            AddEvent(RunEventKind.ItemApplied, DuelsPlayed, e =>
            {
                e.Code = name;
                e.Pile = pile;
                e.IsBonus = pile == RunPile.Main && ItemCards.Of(code, _database).Bound;
            });
        }

        /// <summary>그 덱에서 카드명이 다 빠진 적용을 푼다 (봉인, 교환, 잃음 뒤에 부른다).</summary>
        void ReleaseEmptyItems()
        {
            foreach (var entry in _applied.ToList())
            {
                if (ItemPile(entry.Pile).Any(c => NameOf(c) == entry.Name))
                    continue;
                _applied.Remove(entry);
                AddEvent(RunEventKind.ItemReleased, DuelsPlayed, e =>
                {
                    e.Code = entry.Name;
                    e.Pile = entry.Pile;
                });
            }
        }

        /// <summary>지금 켜진 아이템 효과의 합 (봉인된 카드는 효과 없음, 중첩되지 않는 효과는 두 덱에 적용해도 한 번).</summary>
        public int ItemBonus(ItemEffectKind kind)
        {
            int total = 0;
            foreach (var item in ItemCards.All.Where(i => i.Effect == kind))
            {
                int places = _applied.Count(a => a.Name == item.Code);
                total += item.Stacks ? places * item.Amount : places > 0 ? item.Amount : 0;
            }
            return total;
        }

        /// <summary>탑 미리보기: 바로 위 몇 층의 종류가 보이는지 (기본 1층 + 아이템).</summary>
        public int PreviewFloors => RunRules.BasePreviewFloors + ItemBonus(ItemEffectKind.PreviewFloors);
    }
}
