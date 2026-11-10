// 런 상태를 저장 파일의 "run" 묶음으로 쓰고 읽는다 (GameSave가 부른다).
// 상태를 그대로 적는다 (스냅숏). 듀얼 결과를 다시 돌려 만드는 방식은 등급 표·규칙을 패치로 고치면 결과가 달라질 수 있어서 쓰지 않는다.
// 듀얼 중(InDuel)에도 쓴다: 듀얼을 시작할 때 저장해 두고, 그 상태로 읽으면 GameSave가 그 듀얼을 패배로 처리한다 (ForfeitDuel).
// 듀얼의 상대·시드는 (런 시드, 듀얼 번호)로 정해지므로 따로 적지 않는다.
// 고르는 중인 승리 보상(ChoosingReward)은 후보까지 적어서, 다시 켜도 같은 후보에서 고른다.
//
// 칩교환소 진열(shop), 이벤트 층의 이벤트(floor-event), 남은 보스 보상(boss-bonus), 상점 덱(shop-deck), 적용한 아이템(items: 덱:카드명)도 적는다. 지금 층의 종류(floor-kind)도 적어 둔다
// (나머지 층의 종류는 런 시드로 다시 계산한다).
//
// 형식 1 (2026-11-10 규칙 전): fainted-main·fainted-extra(기절), substituted(등급별 포인트 대체 횟수), 이벤트 CardFainted.
// 읽을 때 봉인으로 옮기고, 포인트 대체 횟수는 골드 환산표(RunRules.GoldFor)로 골드로 바꾼다.
// 형식 3까지: 탑 구조·칩이 없다. 칩 0, 지금 층은 전투 층(10층마다 보스 층)으로 읽는다. 보상을 고르는 중(ChoosingReward)이면
// 층이 이미 올라가 있으므로 1 내린다 (형식 4부터는 보상을 다 받은 뒤에 층이 오른다).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using static System.FormattableString;

namespace DuelRoguelike.Run
{
    public sealed partial class RunState
    {
        static readonly string[] RunKeys =
        {
            "seed", "start-deck", "opponents", "phase", "end-reason", "floor", "duels", "streak", "lp", "wins", "losses", "draws",
            "main", "extra", "side", "sealed-main", "sealed-extra", "gold", "reward", "start-deck-name",
            "floor-kind", "chips", "shop", "floor-event", "boss-bonus", "reward-bonus", "shop-deck", "items",
        };

        /// <summary>형식 2·3</summary>
        static readonly string[] RunKeysV3 =
        {
            "seed", "start-deck", "opponents", "phase", "end-reason", "floor", "duels", "streak", "lp", "wins", "losses", "draws",
            "main", "extra", "side", "sealed-main", "sealed-extra", "gold", "reward", "start-deck-name",
        };

        static readonly string[] RunKeysV1 =
        {
            "seed", "start-deck", "opponents", "phase", "end-reason", "floor", "duels", "streak", "lp", "wins", "losses", "draws",
            "main", "extra", "side", "fainted-main", "fainted-extra", "substituted",
        };

        static readonly string[] RepeatableRunKeys = { "event" };

        internal void WriteTo(SaveWriter w)
        {
            w.Line("seed", Seed);
            w.Line("start-deck", StartDeckId);
            w.Line("start-deck-name", SaveEscape.Encode(StartDeckName));
            w.Line("opponents", Opponents.Select(d => (object)d.Id));
            w.Line("phase", Phase);
            w.Line("end-reason", EndReason);
            w.Line("floor", Floor);
            w.Line("duels", DuelsPlayed);
            w.Line("streak", LosingStreak);
            w.Line("lp", Lp);
            w.Line("wins", Wins);
            w.Line("losses", Losses);
            w.Line("draws", Draws);
            w.Codes("main", _main);
            w.Codes("extra", _extra);
            w.Codes("side", _side);
            w.Codes("sealed-main", _sealedMain);
            w.Codes("sealed-extra", _sealedExtra);
            w.Line("gold", Gold);
            w.Line("floor-kind", FloorKind);
            w.Line("chips", Chips, ChipsEarned);
            if (_shop != null)
                w.Line("shop", _shop.Select(o => (object)(Invariant($"{o.Product}:{o.Price}:{(o.Sold ? 1 : 0)}") + (o.Product == ShopProduct.ItemCard ? Invariant($":{o.Card}") : ""))));
            if (_shopDeck.Count > 0)
                w.Codes("shop-deck", _shopDeck);
            if (_applied.Count > 0)
                w.Line("items", _applied.Select(a => (object)Invariant($"{a.Pile}:{a.Name}")));
            if (CurrentEventId != null)
            {
                if (EventBattlePending)
                    w.Line("floor-event", CurrentEventId, "battle", _eventBattleChoice);
                else if (EventDone)
                    w.Line("floor-event", CurrentEventId, "done");
                else
                    w.Line("floor-event", CurrentEventId);
            }
            if (_bossBonusPending)
                w.Line("boss-bonus");
            if (PendingReward != null)
            {
                var o = PendingReward;
                w.Line("reward", new object[] { o.DuelNumber, o.Floor, o.Rarity, o.Gold }.Concat(o.Cards.Cast<object>()));
                if (o.IsBossBonus)
                    w.Line("reward-bonus");
            }
            foreach (var e in _events)
                w.Line("event", EventValues(e));
        }

        /// <summary>이벤트 한 줄: 종류, duel=, floor=, 그리고 기본값이 아닌 칸만.</summary>
        static IEnumerable<object> EventValues(RunEvent e)
        {
            yield return e.Kind;
            yield return Invariant($"duel={e.DuelNumber}");
            yield return Invariant($"floor={e.Floor}");
            if (e.Code != 0) yield return Invariant($"code={e.Code}");
            if (e.Codes.Count > 0) yield return "codes=" + string.Join(",", e.Codes.Select(c => c.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (e.Rarity != default) yield return Invariant($"rarity={e.Rarity}");
            if (e.Pile != default) yield return Invariant($"pile={e.Pile}");
            if (e.DeckId != null) yield return Invariant($"deck={e.DeckId}");
            if (e.DeckName != null) yield return "deck-name=" + SaveEscape.Encode(e.DeckName);
            if (e.Result != default) yield return Invariant($"result={e.Result}");
            if (e.Lp != 0) yield return Invariant($"lp={e.Lp}");
            if (e.PreviousLp != 0) yield return Invariant($"prev-lp={e.PreviousLp}");
            if (e.Count != 0) yield return Invariant($"count={e.Count}");
            if (e.MainCount != 0) yield return Invariant($"main={e.MainCount}");
            if (e.ExtraCount != 0) yield return Invariant($"extra={e.ExtraCount}");
            if (e.SideCount != 0) yield return Invariant($"side={e.SideCount}");
            if (e.LpReason != default) yield return Invariant($"lp-reason={e.LpReason}");
            if (e.EndReason != default) yield return Invariant($"end-reason={e.EndReason}");
            if (e.IsRefill) yield return "refill=1";
            if (e.FloorKind != default) yield return Invariant($"kind={e.FloorKind}");
            if (e.Chips != 0) yield return Invariant($"chips={e.Chips}");
            if (e.ChipsEarned != 0) yield return Invariant($"chips-earned={e.ChipsEarned}");
            if (e.Product != default) yield return Invariant($"product={e.Product}");
            if (e.EventId != null) yield return "event-id=" + SaveEscape.Encode(e.EventId);
            if (e.IsBonus) yield return "bonus=1";
            if (e.Coin != default) yield return Invariant($"coin={e.Coin}");
            if (e.Turn != default) yield return Invariant($"turn={e.Turn}");
        }

        /// <param name="version">저장 파일 형식 버전 (GameSave.FormatVersion 이하).</param>
        internal static RunState ReadFrom(IReadOnlyList<SaveLine> lines, SaveReader r, CardDatabase database, RarityTable rarities,
            CardCollection collection, int version)
        {
            bool v1 = version < 2;
            bool v4 = version >= 4;
            var map = r.Unique(lines, v1 ? RunKeysV1 : v4 ? RunKeys : RunKeysV3, RepeatableRunKeys);

            var seedLine = r.Required(map, "seed");
            r.ExpectCount(seedLine, 1);
            var deckLine = r.Required(map, "start-deck");
            r.ExpectCount(deckLine, 1);
            var opponentsLine = r.Required(map, "opponents");
            r.ExpectAtLeast(opponentsLine, 1);
            var opponents = new List<DeckRecipe>();
            foreach (string id in opponentsLine.Values)
                opponents.Add(StarterDecks.Find(id) ?? throw r.Error(opponentsLine, $"모르는 상대 덱 \"{id}\""));

            string deckName = null; // 형식 2까지는 없다: 스타터 덱 이름이나 Id
            if (map.TryGetValue("start-deck-name", out var deckNameLine))
            {
                r.ExpectCount(deckNameLine, 1);
                deckName = SaveEscape.Decode(deckNameLine.Values[0], r, deckNameLine);
            }
            var run = new RunState(r.ULong(seedLine, seedLine.Values[0]), deckLine.Values[0], deckName, opponents, database, rarities, collection);

            var phaseLine = r.Required(map, "phase");
            r.ExpectCount(phaseLine, 1);
            run.Phase = r.Enum<RunPhase>(phaseLine, phaseLine.Values[0]);
            if (v1 && (run.Phase == RunPhase.InDuel || run.Phase == RunPhase.ChoosingReward))
                throw r.Error(phaseLine, $"형식 1에는 {run.Phase} 상태가 없음");
            if (!v4 && run.Phase == RunPhase.ChoosingUnseal)
                throw r.Error(phaseLine, $"형식 {version}에는 {run.Phase} 상태가 없음");
            if (map.TryGetValue("end-reason", out var reasonLine))
            {
                r.ExpectCount(reasonLine, 1);
                run.EndReason = r.Enum<RunEndReason>(reasonLine, reasonLine.Values[0]);
            }
            if ((run.Phase == RunPhase.Ended) != (run.EndReason != RunEndReason.None))
                throw r.Error(phaseLine, $"상태 {run.Phase}와 종료 이유 {run.EndReason}가 맞지 않음");

            int Number(string key, int min)
            {
                var line = r.Required(map, key);
                r.ExpectCount(line, 1);
                return r.Int(line, line.Values[0], min);
            }
            run.Floor = Number("floor", 1);
            run.DuelsPlayed = Number("duels", 0);
            run.LosingStreak = Number("streak", 0);
            run.Lp = Number("lp", 0);
            run.Wins = Number("wins", 0);
            run.Losses = Number("losses", 0);
            run.Draws = Number("draws", 0);
            if (run.Wins + run.Losses + run.Draws != run.DuelsPlayed)
                throw r.Error($"전적 {run.Wins}승 {run.Losses}패 {run.Draws}무가 끝낸 듀얼 {run.DuelsPlayed}판과 맞지 않음");

            void Cards(string key, List<uint> into)
            {
                var line = r.Required(map, key);
                foreach (uint code in r.Codes(line))
                {
                    if (!database.TryGet(code, out _))
                        throw r.Error(line, $"카드 {code}가 카드 DB에 없음");
                    into.Add(code);
                }
            }
            Cards("main", run._main);
            Cards("extra", run._extra);
            Cards("side", run._side);
            Cards(v1 ? "fainted-main" : "sealed-main", run._sealedMain);
            Cards(v1 ? "fainted-extra" : "sealed-extra", run._sealedExtra);
            bool shortMain = run._main.Count < RunRules.MinMainDeck;
            if (run.Phase == RunPhase.NeedsRefill && !shortMain)
                throw r.Error(phaseLine, $"보충 중인데 메인 덱이 {run._main.Count}장");
            if ((run.Phase == RunPhase.Ready || run.Phase == RunPhase.InDuel || run.Phase == RunPhase.ChoosingReward || run.Phase == RunPhase.ChoosingUnseal)
                && shortMain)
                throw r.Error(phaseLine, $"메인 덱이 {run._main.Count}장인데 보충 중이 아님");

            if (v1)
            {
                var substituted = r.Required(map, "substituted");
                r.ExpectCount(substituted, RarityTable.RarityCount);
                for (int i = 0; i < RarityTable.RarityCount; i++)
                    run.Gold += r.Int(substituted, substituted.Values[i], 0) * RunRules.GoldFor((Rarity)i);
            }
            else
                run.Gold = Number("gold", 0);

            bool hasReward = map.TryGetValue("reward", out var rewardLine);
            bool rewardBonus = map.TryGetValue("reward-bonus", out var rewardBonusLine);
            if (rewardBonus)
            {
                r.ExpectCount(rewardBonusLine, 0);
                if (!hasReward)
                    throw r.Error(rewardBonusLine, "reward 줄 없이 reward-bonus가 있음");
            }
            if (run.Phase == RunPhase.ChoosingReward)
            {
                if (!hasReward)
                    throw r.Error(phaseLine, "보상을 고르는 중인데 reward 줄이 없음");
                run.PendingReward = ReadReward(rewardLine, r, database, run, rewardBonus);
            }
            else if (hasReward)
                throw r.Error(rewardLine, $"보상을 고르는 중이 아닌데 reward 줄이 있음 ({run.Phase})");

            if (v4)
                ReadTower(map, r, run, phaseLine);
            else
            {
                // 형식 3까지: 보상을 고르는 중에도 층이 이미 올라가 있었다. 지금 층은 전투 층으로 둔다.
                if (run.Phase == RunPhase.ChoosingReward)
                {
                    if (run.Floor < 2)
                        throw r.Error(phaseLine, $"보상을 고르는 중인데 {run.Floor}층");
                    run.Floor--;
                }
                run.FloorKind = RunRules.IsBossFloor(run.Floor) ? FloorKind.Boss : FloorKind.Battle;
            }
            if (run.PendingReward != null && run.PendingReward.Floor != run.Floor)
                throw r.Error(rewardLine, $"보상의 층 {run.PendingReward.Floor}이 지금 층 {run.Floor}과 다름");

            foreach (var line in lines.Where(l => l.Key == "event"))
            {
                var e = ReadEvent(line, r, v1);
                // 형식 2까지의 런 시작 이벤트에는 덱 이름이 없다: 런의 시작 덱 이름으로 채운다.
                if (e.Kind == RunEventKind.RunStarted && e.DeckName == null)
                    e.DeckName = run.StartDeckName;
                run._events.Add(e);
            }

            if (run.Phase == RunPhase.InDuel)
            {
                // 선후공은 듀얼 시작 기록에서 (형식 4까지는 늘 선공).
                var started = run._events.LastOrDefault(e => e.Kind == RunEventKind.DuelStarted);
                run.CurrentDuel = run.CreateDuel(run.DuelsPlayed + 1, playerGoesFirst: started?.Turn != TurnOrder.Second);
            }
            return run;
        }

        /// <summary>
        /// floor-event 줄: 이벤트 Id, 그 뒤에 "done"(선택지를 골랐고 층에 남아 있음, 보충 뒤 등) 또는 "battle 번호"(이벤트 전투를 기다림·하는 중).
        /// 형식 4까지는 Id만 있다.
        /// </summary>
        static void ReadFloorEvent(SaveLine line, SaveReader r, RunState run)
        {
            if (line.Values.Length < 1)
                throw r.Error(line, "이벤트 Id가 없음");
            var ev = TowerEventList.Find(line.Values[0]);
            if (ev == null)
                throw r.Error(line, $"모르는 이벤트 \"{line.Values[0]}\"");
            run.CurrentEventId = ev.Id;
            if (line.Values.Length == 1)
            {
                if (run.Phase != RunPhase.Ready && run.Phase != RunPhase.Ended)
                    throw r.Error(line, $"이벤트 선택지를 고르기 전인데 {run.Phase}");
                return;
            }
            switch (line.Values[1])
            {
                case "done":
                    r.ExpectCount(line, 2);
                    run.EventDone = true;
                    break;
                case "battle":
                    r.ExpectCount(line, 3);
                    int index = r.Int(line, line.Values[2], 0);
                    if (index >= ev.Choices.Count || !ev.Choices[index].IsBattle)
                        throw r.Error(line, $"「{ev.Title}」의 선택지 {index}는 이벤트 전투가 아님");
                    if (run.Phase != RunPhase.Ready && run.Phase != RunPhase.InDuel)
                        throw r.Error(line, $"이벤트 전투를 기다리는데 {run.Phase}");
                    run._eventBattleChoice = index;
                    break;
                default:
                    throw r.Error(line, $"floor-event의 둘째 칸은 done 또는 battle이어야 함 (\"{line.Values[1]}\")");
            }
        }

        /// <summary>형식 4: floor-kind, chips, shop, floor-event, boss-bonus.</summary>
        static void ReadTower(Dictionary<string, SaveLine> map, SaveReader r, RunState run, SaveLine phaseLine)
        {
            var kindLine = r.Required(map, "floor-kind");
            r.ExpectCount(kindLine, 1);
            run.FloorKind = r.Enum<FloorKind>(kindLine, kindLine.Values[0]);
            bool choosing = run.Phase == RunPhase.ChoosingReward || run.Phase == RunPhase.ChoosingUnseal;
            if ((choosing || run.Phase == RunPhase.InDuel) && !Tower.IsBattle(run.FloorKind) && run.FloorKind != FloorKind.Event)
                throw r.Error(kindLine, $"{run.Phase}인데 {run.FloorKind} 층");
            if (choosing && !Tower.IsBattle(run.FloorKind))
                throw r.Error(kindLine, $"{run.Phase}인데 {run.FloorKind} 층");

            var chipsLine = r.Required(map, "chips");
            r.ExpectCount(chipsLine, 2);
            run.Chips = r.Int(chipsLine, chipsLine.Values[0], 0);
            run.ChipsEarned = r.Int(chipsLine, chipsLine.Values[1], 0);
            if (run.Chips > run.ChipsEarned)
                throw r.Error(chipsLine, $"가진 칩 {run.Chips}이 얻은 칩 {run.ChipsEarned}보다 많음");

            bool hasShop = map.TryGetValue("shop", out var shopLine);
            if (run.FloorKind == FloorKind.ChipExchange)
            {
                if (!hasShop)
                    throw r.Error(kindLine, "칩교환소인데 shop 줄이 없음");
                run._shop = ReadShop(shopLine, r);
            }
            else if (hasShop)
                throw r.Error(shopLine, $"칩교환소가 아닌데 shop 줄이 있음 ({run.FloorKind})");

            bool hasEvent = map.TryGetValue("floor-event", out var eventLine);
            if (run.FloorKind == FloorKind.Event)
            {
                if (!hasEvent)
                    throw r.Error(kindLine, "이벤트 층인데 floor-event 줄이 없음");
                ReadFloorEvent(eventLine, r, run);
            }
            else if (hasEvent)
                throw r.Error(eventLine, $"이벤트 층이 아닌데 floor-event 줄이 있음 ({run.FloorKind})");
            if (run.Phase == RunPhase.InDuel && run.FloorKind == FloorKind.Event && !run.EventBattlePending)
                throw r.Error(kindLine, "이벤트 층에서 듀얼 중인데 이벤트 전투가 아님");

            if (map.TryGetValue("boss-bonus", out var bonusLine))
            {
                r.ExpectCount(bonusLine, 0);
                if (run.Phase != RunPhase.ChoosingReward || run.FloorKind != FloorKind.Boss || run.PendingReward.IsBossBonus)
                    throw r.Error(bonusLine, $"보스 승리 보상을 고르는 중이 아닌데 boss-bonus가 있음 ({run.Phase}, {run.FloorKind})");
                run._bossBonusPending = true;
            }
            if (run.Phase == RunPhase.ChoosingUnseal && (run.FloorKind != FloorKind.Boss || run.SealedCount == 0))
                throw r.Error(phaseLine, $"보스 보상(봉인 해제)을 고르는 중인데 {run.FloorKind} 층, 봉인 {run.SealedCount}장");
            ReadItems(map, r, run);
        }

        /// <summary>shop-deck 카드… (없으면 빈 상점 덱), items 덱:카드명… (없으면 적용 없음).</summary>
        static void ReadItems(Dictionary<string, SaveLine> map, SaveReader r, RunState run)
        {
            var database = run._database;
            if (map.TryGetValue("shop-deck", out var deckLine))
            {
                r.ExpectAtLeast(deckLine, 1);
                foreach (uint code in r.Codes(deckLine))
                {
                    if (!database.TryGet(code, out _))
                        throw r.Error(deckLine, $"카드 {code}가 카드 DB에 없음");
                    if (!run.ShopDeckHasRoom(code, out string full))
                        throw r.Error(deckLine, full);
                    run._shopDeck.Add(code);
                }
            }
            if (!map.TryGetValue("items", out var itemsLine))
                return;
            r.ExpectAtLeast(itemsLine, 1);
            foreach (string value in itemsLine.Values)
            {
                var parts = value.Split(':');
                if (parts.Length != 2)
                    throw r.Error(itemsLine, $"\"{value}\"는 덱:카드명 꼴이 아님");
                var pile = r.Enum<RunPile>(itemsLine, parts[0]);
                uint name = r.Code(itemsLine, parts[1]);
                var item = ItemCards.Of(name, database);
                if (item == null || item.Code != name)
                    throw r.Error(itemsLine, $"{name}은(는) 아이템 카드명이 아님");
                var cards = run.ItemPile(pile);
                if (cards == null || !(pile == RunPile.Main ? item.InMain : item.InShop))
                    throw r.Error(itemsLine, $"{item.Name}은(는) {pile}에서 적용할 수 없음");
                if (!cards.Any(c => run.NameOf(c) == name))
                    throw r.Error(itemsLine, $"{item.Name}을(를) {pile}에 적용했는데 그 덱에 카드가 없음");
                if (!run._applied.Add((pile, name)))
                    throw r.Error(itemsLine, $"\"{value}\"가 두 번 나옴");
            }
        }

        /// <summary>shop 상품:가격:샀는지(0·1) … (아이템 카드는 ItemCard:가격:샀는지:카드)</summary>
        static List<ShopOffer> ReadShop(SaveLine line, SaveReader r)
        {
            r.ExpectAtLeast(line, 1);
            var offers = new List<ShopOffer>();
            foreach (string value in line.Values)
            {
                var parts = value.Split(':');
                var product = r.Enum<ShopProduct>(line, parts[0]);
                int expected = product == ShopProduct.ItemCard ? 4 : 3;
                if (parts.Length != expected)
                    throw r.Error(line, expected == 4 ? $"\"{value}\"는 ItemCard:가격:샀는지:카드 꼴이 아님" : $"\"{value}\"는 상품:가격:샀는지 꼴이 아님");
                if (offers.Any(o => o.Product == product))
                    throw r.Error(line, $"{product}이(가) 두 번 나옴");
                int price = r.Int(line, parts[1], 0);
                if (parts[2] != "0" && parts[2] != "1")
                    throw r.Error(line, $"\"{value}\"의 샀는지는 0이나 1");
                uint card = 0;
                if (product == ShopProduct.ItemCard)
                {
                    card = r.Code(line, parts[3]);
                    if (ItemCards.All.All(i => i.Code != card))
                        throw r.Error(line, $"{card}은(는) 아이템 카드가 아님");
                }
                offers.Add(new ShopOffer(product, price, parts[2] == "1", card));
            }
            return offers;
        }

        /// <summary>reward 듀얼번호 층 등급 골드 카드… (reward-bonus 줄이 있으면 보스 보상으로 한 번 더 받는 보상)</summary>
        static RewardOffer ReadReward(SaveLine line, SaveReader r, CardDatabase database, RunState run, bool bossBonus)
        {
            r.ExpectAtLeast(line, 5);
            int duel = r.Int(line, line.Values[0], 1);
            int floor = r.Int(line, line.Values[1], 1);
            var rarity = r.Enum<Rarity>(line, line.Values[2]);
            int gold = r.Int(line, line.Values[3], 0);
            var cards = r.Codes(line, 4);
            if (duel != run.DuelsPlayed)
                throw r.Error(line, $"보상의 듀얼 번호 {duel}가 끝낸 듀얼 {run.DuelsPlayed}과 다름");
            if (cards.Count > RunRules.RewardChoices)
                throw r.Error(line, $"후보 {cards.Count}장 ({RunRules.RewardChoices}장 이하여야 함)");
            if (cards.Distinct().Count() != cards.Count)
                throw r.Error(line, "같은 카드가 후보에 두 번 나옴");
            foreach (uint code in cards)
                if (!database.TryGet(code, out _))
                    throw r.Error(line, $"카드 {code}가 카드 DB에 없음");
            return new RewardOffer(duel, floor, rarity, cards, gold, bossBonus);
        }

        static RunEvent ReadEvent(SaveLine line, SaveReader r, bool v1)
        {
            r.ExpectAtLeast(line, 1);
            string kind = line.Values[0];
            if (v1 && kind == "CardFainted")
                kind = nameof(RunEventKind.CardSealed);
            var e = new RunEvent { Kind = r.Enum<RunEventKind>(line, kind) };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string field in line.Values.Skip(1))
            {
                int eq = field.IndexOf('=');
                if (eq <= 0)
                    throw r.Error(line, $"\"{field}\"는 이름=값 꼴이 아님");
                string name = field.Substring(0, eq), value = field.Substring(eq + 1);
                if (!seen.Add(name))
                    throw r.Error(line, $"{name}이(가) 두 번 나옴");
                switch (name)
                {
                    case "duel": e.DuelNumber = r.Int(line, value, 0); break;
                    case "floor": e.Floor = r.Int(line, value, 0); break;
                    case "code": e.Code = r.Code(line, value); break;
                    case "codes": e.Codes = value.Split(',').Select(v => r.Code(line, v)).ToArray(); break;
                    case "rarity": e.Rarity = r.Enum<Rarity>(line, value); break;
                    case "pile": e.Pile = r.Enum<RunPile>(line, value); break;
                    case "deck": e.DeckId = value; break;
                    case "deck-name": e.DeckName = SaveEscape.Decode(value, r, line); break;
                    case "result": e.Result = r.Enum<DuelResult>(line, value); break;
                    case "lp": e.Lp = r.Int(line, value); break;
                    case "prev-lp": e.PreviousLp = r.Int(line, value); break;
                    case "count": e.Count = r.Int(line, value); break;
                    case "main": e.MainCount = r.Int(line, value); break;
                    case "extra": e.ExtraCount = r.Int(line, value); break;
                    case "side": e.SideCount = r.Int(line, value); break;
                    case "lp-reason": e.LpReason = r.Enum<LpRestoreReason>(line, value); break;
                    case "end-reason": e.EndReason = r.Enum<RunEndReason>(line, value); break;
                    case "refill":
                        if (value != "1")
                            throw r.Error(line, "refill은 1만 쓴다");
                        e.IsRefill = true;
                        break;
                    case "kind": e.FloorKind = r.Enum<FloorKind>(line, value); break;
                    case "chips": e.Chips = r.Int(line, value); break;
                    case "chips-earned": e.ChipsEarned = r.Int(line, value); break;
                    case "product": e.Product = r.Enum<ShopProduct>(line, value); break;
                    case "event-id": e.EventId = SaveEscape.Decode(value, r, line); break;
                    case "bonus":
                        if (value != "1")
                            throw r.Error(line, "bonus는 1만 쓴다");
                        e.IsBonus = true;
                        break;
                    case "coin": e.Coin = r.Enum<CoinTossWinner>(line, value); break;
                    case "turn": e.Turn = r.Enum<TurnOrder>(line, value); break;
                    default:
                        throw r.Error(line, $"모르는 이벤트 칸 \"{name}\"");
                }
            }
            if (!seen.Contains("duel") || !seen.Contains("floor"))
                throw r.Error(line, "duel=과 floor=가 있어야 함");
            // 형식 1의 포인트 대체에는 골드가 없다: 지금 환산표로 채운다 (런의 골드도 같은 표로 바꿨다).
            if (v1 && e.Kind == RunEventKind.RewardSubstituted && e.Count == 0)
                e.Count = RunRules.GoldFor(e.Rarity);
            return e;
        }
    }
}
