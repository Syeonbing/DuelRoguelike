// 런의 탑 진행: 층 오르기, 비전투 층(칩교환소·이벤트·휴식), 보스 보상, 칩, 런 종료 골드 정산 (규칙 결정 문서 2026-11-10
// "탑 구조", "재화", "칩교환소", "봉인·봉인 해제·봉인 대상 교체"). 층 종류는 Tower.cs, 수치는 RunRules.cs.
// - 층에 들어가면(EnterFloor) 종류를 정하고: 칩교환소면 진열을, 이벤트 층이면 나올 이벤트를 정하고, 휴식 층이면 LP를 8000으로 맞춘다.
// - 칩교환소·휴식 층은 LeaveFloor로, 이벤트 층은 선택지를 고르면(ChooseEventOption) 다음 층으로 간다.
// - 봉인 해제·봉인 대상 교체로 풀린 카드는 사이드 덱으로 간다 (덱에 다시 넣는 것은 교환으로).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public sealed partial class RunState
    {
        List<ShopOffer> _shop;

        /// <summary>보스를 이겼고, 승리 보상을 고른 뒤에 보스 보상(봉인 해제 또는 보상 1장 더)이 남아 있음.</summary>
        bool _bossBonusPending;

        /// <summary>지금 층이 이벤트 층이면 나온 이벤트의 Id (아직 선택지를 고르지 않음). 아니면 null.</summary>
        public string CurrentEventId { get; private set; }

        public TowerEvent CurrentEvent => CurrentEventId == null ? null : TowerEvents.Find(CurrentEventId);

        /// <summary>지금 층이 칩교환소면 진열된 상품 (진열 순서). 아니면 빈 목록.</summary>
        public IReadOnlyList<ShopOffer> Shop => (IReadOnlyList<ShopOffer>)_shop ?? Array.Empty<ShopOffer>();

        /// <summary>런 종료 골드 정산 (런이 끝났을 때만, 아니면 0). GameSave가 런의 Gold와 함께 누적에 더한다.</summary>
        public int SettlementGold => Phase == RunPhase.Ended ? RunRules.SettlementGold(Floor, ChipsEarned, Chips) : 0;

        public int SealedCount => _sealedMain.Count + _sealedExtra.Count;

        // ---------------- 층 오르기 ----------------

        /// <summary>층에 들어간다: 종류를 정하고, 칩교환소 진열·이벤트를 정하고, 휴식 층이면 LP를 맞춘다.</summary>
        void EnterFloor()
        {
            FloorKind = Tower.KindOf(Seed, Floor);
            _shop = FloorKind == FloorKind.ChipExchange ? ChipExchange.Stock(Seed, Floor) : null;
            CurrentEventId = FloorKind == FloorKind.Event ? TowerEvents.Pick().Id : null;
            AddEvent(RunEventKind.FloorEntered, DuelsPlayed, e =>
            {
                e.FloorKind = FloorKind;
                e.EventId = CurrentEventId;
            });
            if (FloorKind == FloorKind.Rest && Lp != RunRules.FullLp)
                ChangeLp(RunRules.FullLp, LpRestoreReason.Rest, DuelsPlayed);
        }

        void AdvanceFloor()
        {
            Floor++;
            EnterFloor();
        }

        /// <summary>
        /// 승리 뒤 고르기 하나가 끝날 때마다 부른다. 아직 고를 것이 있으면(보상 후보) 기다리고, 보스 보상이 남았으면 그것을 열고,
        /// 다 끝났으면 다음 층으로 올라간다.
        /// </summary>
        void ContinueAfterWin()
        {
            if (Phase != RunPhase.Ready)
                return;
            if (_bossBonusPending)
            {
                _bossBonusPending = false;
                if (SealedCount > 0)
                {
                    Phase = RunPhase.ChoosingUnseal;
                    AddEvent(RunEventKind.UnsealOffered, DuelsPlayed, e => e.Count = SealedCount);
                    return;
                }
                OfferReward(DuelsPlayed, Floor, OpponentFor(DuelsPlayed).ToCardList(), bossBonus: true);
                if (Phase != RunPhase.Ready)
                    return;
            }
            AdvanceFloor();
        }

        /// <summary>지금 층을 떠나 다음 층으로 갈 수 있는지 (칩교환소·휴식 층에서 Ready일 때만).</summary>
        public bool CanLeaveFloor(out string reason)
        {
            reason = null;
            if (Phase != RunPhase.Ready)
                reason = PhaseReason();
            else if (Tower.IsBattle(FloorKind))
                reason = "전투 층은 듀얼에서 이겨야 다음 층으로 갑니다";
            else if (FloorKind == FloorKind.Event)
                reason = "이벤트의 선택지를 골라야 다음 층으로 갑니다";
            return reason == null;
        }

        /// <summary>칩교환소·휴식 층을 떠나 다음 층으로 간다.</summary>
        public void LeaveFloor()
        {
            if (!CanLeaveFloor(out string reason))
                throw new InvalidOperationException($"층을 떠날 수 없습니다: {reason}");
            AdvanceFloor();
        }

        /// <summary>이벤트의 선택지를 고른다 (이벤트 층에서 Ready일 때만). 결과를 적용하고 다음 층으로 간다 (지금 이벤트는 결과가 없다).</summary>
        public void ChooseEventOption(int index)
        {
            RequirePhase(RunPhase.Ready, "이벤트 선택지를 고를");
            var ev = CurrentEvent;
            if (FloorKind != FloorKind.Event || ev == null)
                throw new InvalidOperationException($"이벤트 선택지를 고를 수 없습니다: {Floor}층은 {Tower.KindName(FloorKind)} 층");
            if (index < 0 || index >= ev.Choices.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"선택지는 0~{ev.Choices.Count - 1}입니다.");
            AddEvent(RunEventKind.EventResolved, DuelsPlayed, e =>
            {
                e.EventId = ev.Id;
                e.Count = index;
            });
            AdvanceFloor();
        }

        // ---------------- 보스 보상 ----------------

        /// <summary>봉인 해제할 수 있는 카드 (봉인된 카드, 같은 카드는 한 번: 메인 덱 것부터).</summary>
        public List<uint> SealedCards() => _sealedMain.Concat(_sealedExtra).Distinct().ToList();

        /// <summary>보스 보상: 봉인된 카드 1장을 골라 봉인 해제한다 (ChoosingUnseal에서만). 풀린 카드는 사이드 덱으로, 그리고 다음 층으로.</summary>
        public void ChooseUnseal(uint code)
        {
            RequirePhase(RunPhase.ChoosingUnseal, "보스 보상을 고를");
            if (!_sealedMain.Contains(code) && !_sealedExtra.Contains(code))
                throw new InvalidOperationException($"{_database.GetName(code)}은(는) 봉인된 카드가 아닙니다.");
            Unseal(code, bossReward: true);
            Phase = RunPhase.Ready;
            ContinueAfterWin();
        }

        void Unseal(uint code, bool bossReward)
        {
            bool fromMain = _sealedMain.Remove(code);
            if (!fromMain && !_sealedExtra.Remove(code))
                throw new InvalidOperationException($"{_database.GetName(code)}은(는) 봉인된 카드가 아닙니다.");
            _side.Add(code);
            AddEvent(RunEventKind.CardUnsealed, DuelsPlayed, e =>
            {
                e.Code = code;
                e.Pile = fromMain ? RunPile.Main : RunPile.Extra;
                e.IsBonus = bossReward;
            });
        }

        // ---------------- 칩 ----------------

        void GainChips(int amount, int duelNumber)
        {
            Chips += amount;
            ChipsEarned += amount;
            AddEvent(RunEventKind.ChipsGained, duelNumber, e =>
            {
                e.Count = amount;
                e.Chips = Chips;
            });
        }

        // ---------------- 칩교환소 ----------------

        ShopOffer OfferOf(ShopProduct product) => _shop?.FirstOrDefault(o => o.Product == product);

        /// <summary>이 상품을 지금 살 수 있는지. 못 사면 reason에 까닭.</summary>
        public bool CanBuy(ShopProduct product, out string reason)
        {
            reason = null;
            var offer = OfferOf(product);
            if (Phase != RunPhase.Ready)
                reason = PhaseReason();
            else if (FloorKind != FloorKind.ChipExchange)
                reason = "칩교환소가 아님";
            else if (offer == null)
                reason = "이번 칩교환소에는 없는 상품";
            else if (offer.Sold)
                reason = "이번 칩교환소에서 이미 삼";
            else if (Chips < offer.Price)
                reason = $"칩이 모자람 (가격 {offer.Price}, 보유 {Chips})";
            else if (SealedCount == 0)
                reason = "봉인된 카드가 없음";
            else if (ReleaseCandidates(product).Count == 0)
                reason = ChipExchange.SameRarity(product)
                    ? "봉인된 카드와 같은 덱·같은 레어도의 카드가 덱에 없음 (또는 교체하면 메인 덱을 채울 수 없음)"
                    : "봉인된 카드와 같은 덱의 카드가 없음 (또는 교체하면 메인 덱을 채울 수 없음)";
            return reason == null;
        }

        /// <summary>이 상품으로 봉인을 풀 수 있는 카드 (봉인된 카드, 같은 카드는 한 번). 교체는 대신 봉인할 카드가 있고 메인 덱을 채울 수 있는 것만.</summary>
        public List<uint> ReleaseCandidates(ShopProduct product)
        {
            var sealedCards = SealedCards();
            if (!ChipExchange.IsSwap(product))
                return sealedCards;
            return sealedCards.Where(code => NewSealCandidates(product, code).Count > 0 && SwapKeepsMainFillable(code)).ToList();
        }

        /// <summary>봉인 대상 교체에서 release를 풀면 대신 봉인될 수 있는 카드 (같은 덱, 같은 레어도 상품이면 같은 레어도. 같은 카드는 한 번).</summary>
        public List<uint> NewSealCandidates(ShopProduct product, uint release) => SwapPool(product, release).Distinct().ToList();

        /// <summary>대신 봉인될 수 있는 카드 (덱에 든 장수만큼, 무작위로 고를 때 쓴다).</summary>
        List<uint> SwapPool(ShopProduct product, uint release)
        {
            if (!ChipExchange.IsSwap(product))
                return new List<uint>();
            var deck = IsExtraDeckCard(release) ? _extra : _main;
            if (!ChipExchange.SameRarity(product))
                return deck.ToList();
            var rarity = RarityOf(release);
            return deck.Where(code => RarityOf(code) == rarity).ToList();
        }

        Rarity? RarityOf(uint code) => _rarities.TryGet(code, out var rarity) ? rarity : (Rarity?)null;

        /// <summary>
        /// 메인 덱 카드를 풀고 메인 덱 카드를 대신 봉인하면 메인 덱이 1장 준다. 40장 아래가 되면 사이드 덱(풀린 카드 포함)으로 채울 수 있어야 한다.
        /// 같은 카드명 장수는 지금 덱으로 센다 (덱에서 1장 빠지면 제한이 느슨해질 뿐이라, 이것으로 채울 수 있으면 실제로도 채울 수 있다).
        /// </summary>
        bool SwapKeepsMainFillable(uint release)
        {
            if (IsExtraDeckCard(release))
                return true;
            int after = _main.Count - 1;
            int needed = RunRules.MinMainDeck - after;
            return needed <= 0 || RefillableCount(_side.Append(release), after) >= needed;
        }

        /// <summary>봉인 해제를 산다: code(봉인된 카드)의 봉인을 풀어 사이드 덱으로.</summary>
        public void BuyUnseal(uint code)
        {
            if (!CanBuy(ShopProduct.Unseal, out string reason))
                throw new InvalidOperationException($"봉인 해제를 살 수 없습니다: {reason}");
            if (!ReleaseCandidates(ShopProduct.Unseal).Contains(code))
                throw new InvalidOperationException($"{_database.GetName(code)}은(는) 봉인된 카드가 아닙니다.");
            Pay(ShopProduct.Unseal);
            Unseal(code, bossReward: false);
        }

        /// <summary>
        /// 봉인 대상 교체를 산다: release(봉인된 카드)의 봉인을 풀어 사이드 덱으로 보내고, 같은 덱의 카드 하나를 대신 봉인한다.
        /// 직접 고르는 상품이면 newSeal(덱의 카드)을, 무작위 상품이면 newSeal은 0. 메인 덱이 40장 아래가 되면 보충(NeedsRefill).
        /// </summary>
        public void BuySwap(ShopProduct product, uint release, uint newSeal = 0)
        {
            if (!ChipExchange.IsSwap(product))
                throw new ArgumentException($"{product}은(는) 봉인 대상 교체가 아닙니다.", nameof(product));
            if (!CanBuy(product, out string reason))
                throw new InvalidOperationException($"{ChipExchange.Name(product)}을(를) 살 수 없습니다: {reason}");
            if (!ReleaseCandidates(product).Contains(release))
                throw new InvalidOperationException($"{_database.GetName(release)}은(는) 이 상품으로 풀 수 없습니다.");
            var pool = SwapPool(product, release);
            uint target;
            if (ChipExchange.ChoosesNewSeal(product))
            {
                if (!pool.Contains(newSeal))
                    throw new InvalidOperationException($"새로 봉인할 카드 {newSeal}을(를) 고를 수 없습니다.");
                target = newSeal;
            }
            else
            {
                if (newSeal != 0)
                    throw new ArgumentException("무작위 교체에는 새로 봉인할 카드를 주지 않습니다.", nameof(newSeal));
                target = pool[RunRandom.For(Seed, Floor * 8 + (int)product, RunRandomPurpose.ShopSwap).Next(pool.Count)];
            }
            bool extra = IsExtraDeckCard(release);
            Pay(product);
            Unseal(release, bossReward: false);
            (extra ? _extra : _main).Remove(target);
            (extra ? _sealedExtra : _sealedMain).Add(target);
            AddEvent(RunEventKind.CardSealed, DuelsPlayed, e =>
            {
                e.Code = target;
                e.Pile = extra ? RunPile.Extra : RunPile.Main;
            });
            CheckMainDeckSize();
        }

        void Pay(ShopProduct product)
        {
            var offer = OfferOf(product);
            Chips -= offer.Price;
            offer.Sold = true;
            AddEvent(RunEventKind.ShopBought, DuelsPlayed, e =>
            {
                e.Product = product;
                e.Count = offer.Price;
                e.Chips = Chips;
            });
        }

        // ---------------- 런 종료 ----------------

        void EndRun(RunEndReason reason)
        {
            PendingReward = null;
            _bossBonusPending = false;
            Phase = RunPhase.Ended;
            EndReason = reason;
            AddEvent(RunEventKind.RunEnded, DuelsPlayed, e => e.EndReason = reason);
            AddEvent(RunEventKind.GoldSettled, DuelsPlayed, e =>
            {
                e.Count = SettlementGold;
                e.Chips = Chips;
                e.ChipsEarned = ChipsEarned;
            });
        }
    }
}
