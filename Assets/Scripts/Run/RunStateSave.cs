// 런 상태를 저장 파일의 "run" 묶음으로 쓰고 읽는다 (GameSave가 부른다).
// 상태를 그대로 적는다 (스냅숏). 듀얼 결과를 다시 돌려 만드는 방식은 등급 표·규칙을 패치로 고치면 결과가 달라질 수 있어서 쓰지 않는다.
// 듀얼 중(InDuel)에도 쓴다: 듀얼을 시작할 때 저장해 두고, 그 상태로 읽으면 GameSave가 그 듀얼을 패배로 처리한다 (ForfeitDuel).
// 듀얼의 상대·시드는 (런 시드, 듀얼 번호)로 정해지므로 따로 적지 않는다.
// 고르는 중인 승리 보상(ChoosingReward)은 후보까지 적어서, 다시 켜도 같은 후보에서 고른다.
//
// 형식 1 (2026-11-10 규칙 전): fainted-main·fainted-extra(기절), substituted(등급별 포인트 대체 횟수), 이벤트 CardFainted.
// 읽을 때 봉인으로 옮기고, 포인트 대체 횟수는 골드 환산표(RunRules.GoldFor)로 골드로 바꾼다.
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
            "main", "extra", "side", "sealed-main", "sealed-extra", "gold", "reward",
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
            if (PendingReward != null)
            {
                var o = PendingReward;
                w.Line("reward", new object[] { o.DuelNumber, o.Floor, o.Rarity, o.Gold }.Concat(o.Cards.Cast<object>()));
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
        }

        /// <param name="version">저장 파일 형식 버전 (GameSave.FormatVersion 이하).</param>
        internal static RunState ReadFrom(IReadOnlyList<SaveLine> lines, SaveReader r, CardDatabase database, RarityTable rarities,
            CardCollection collection, int version)
        {
            bool v1 = version < 2;
            var map = r.Unique(lines, v1 ? RunKeysV1 : RunKeys, RepeatableRunKeys);

            var seedLine = r.Required(map, "seed");
            r.ExpectCount(seedLine, 1);
            var deckLine = r.Required(map, "start-deck");
            r.ExpectCount(deckLine, 1);
            var opponentsLine = r.Required(map, "opponents");
            r.ExpectAtLeast(opponentsLine, 1);
            var opponents = new List<DeckRecipe>();
            foreach (string id in opponentsLine.Values)
                opponents.Add(StarterDecks.Find(id) ?? throw r.Error(opponentsLine, $"모르는 상대 덱 \"{id}\""));

            var run = new RunState(r.ULong(seedLine, seedLine.Values[0]), deckLine.Values[0], opponents, database, rarities, collection);

            var phaseLine = r.Required(map, "phase");
            r.ExpectCount(phaseLine, 1);
            run.Phase = r.Enum<RunPhase>(phaseLine, phaseLine.Values[0]);
            if (v1 && (run.Phase == RunPhase.InDuel || run.Phase == RunPhase.ChoosingReward))
                throw r.Error(phaseLine, $"형식 1에는 {run.Phase} 상태가 없음");
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
            if ((run.Phase == RunPhase.Ready || run.Phase == RunPhase.InDuel || run.Phase == RunPhase.ChoosingReward) && shortMain)
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
            if (run.Phase == RunPhase.ChoosingReward)
            {
                if (!hasReward)
                    throw r.Error(phaseLine, "보상을 고르는 중인데 reward 줄이 없음");
                run.PendingReward = ReadReward(rewardLine, r, database, run);
            }
            else if (hasReward)
                throw r.Error(rewardLine, $"보상을 고르는 중이 아닌데 reward 줄이 있음 ({run.Phase})");

            foreach (var line in lines.Where(l => l.Key == "event"))
                run._events.Add(ReadEvent(line, r, v1));

            if (run.Phase == RunPhase.InDuel)
                run.CurrentDuel = run.CreateDuel(run.DuelsPlayed + 1);
            return run;
        }

        /// <summary>reward 듀얼번호 층 등급 골드 카드…</summary>
        static RewardOffer ReadReward(SaveLine line, SaveReader r, CardDatabase database, RunState run)
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
            return new RewardOffer(duel, floor, rarity, cards, gold);
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
