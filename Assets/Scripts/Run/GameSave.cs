// 저장 (규칙 결정 문서 "저장·무작위 결과"). 파일 하나에 다음을 담는다:
// - 컬렉션: 한 번이라도 얻은 카드와 장수 (영구). 새 저장(Blank)은 비어 있고, 플레이어가 고른 공통 시작 덱 하나(StructureDecks.StartVersions)의
//   40장을 지급받아 시작한다 (ChooseStartDeck). 시작 덱을 고르기 전에는 NeedsStartDeck이다.
//   스타터 덱 4개를 지급한 NewGame()은 시험·헤드리스 실행과 이전 저장 형식 읽기에 남겨 둔다.
// - 골드: 영구 재화. 끝난 런들에서 얻은 골드(보상 빈 자리·등급 대체 + 런 종료 정산)를 더한 것.
//   쓰는 곳 (골드 상점, docs/ECONOMY.md): 사이드 덱 영구 확장(BuySideDeckExpansion), 런 시작 때의 임시 칸(StartRun의 runSideSlots).
//   얻는 곳이 하나 더 있다: 컬렉션 분해(Disassemble. 카드 ID마다 3장을 넘는 분만).
// - 시작 사이드 덱의 영구 한도 (SideDeckLimit, 3 → 10장). 형식 6부터 side-limit 줄에 쓴다. 없으면 3장.
// - 덱 레시피 (PlayerRecipe): 이름, 메인·엑스트라 덱, 시작 사이드 덱. 고른 시작 덱이 첫 레시피가 된다. 마지막으로 런을 시작한 레시피.
// - 진행 중인 런 (있으면, RunStateSave.cs). 듀얼 중에 저장된 런을 읽으면 그 듀얼을 패배로 처리한다 (ForfeitedDuel).
// 화면은 층을 오를 때, 듀얼을 시작할 때, 보상 후보를 뽑았을 때(듀얼 결과), 보상을 골랐을 때(컬렉션), 교환·보충마다, 레시피를 저장·지울 때 저장한다.
// 그래서 무작위 결과(상대, 보상 후보, 봉인할 카드)를 다시 켜서 다시 뽑을 수 없다.
// 파일 형식은 SaveText.cs (줄 단위 텍스트). 파일 위치는 부르는 쪽이 정한다 (게임: 유니티 persistentDataPath).
// 형식 1(포인트 대체 횟수, 기절)도 읽는다: 포인트는 골드 환산표로 골드로 바꾸고, 기절은 봉인으로 옮긴다.
// 형식 2까지(레시피 없음)는 스타터 덱 4개를 레시피로 만들고, 시작 덱별 마지막 시작 사이드 덱(last-side-deck)을 그 레시피의 사이드 덱으로 옮긴다.
// 형식 3까지(탑 구조·칩 없음)의 런은 칩 0, 지금 층은 전투 층(10층마다 보스 층)으로 읽는다 (RunStateSave.cs).
// 형식 5까지(side-limit 없음)는 시작 사이드 덱 한도 3장으로 읽는다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public sealed class GameSave
    {
        /// <summary>저장 파일 형식 버전. 형식을 바꾸면 올리고, 이전 버전 파일을 읽는 길을 남긴다.</summary>
        public const int FormatVersion = 6;

        static readonly string[] ProfileKeys = { "format", "gold", "side-limit", "last-start-deck" };
        static readonly string[] ProfileKeysV5 = { "format", "gold", "last-start-deck" };
        static readonly string[] RepeatableProfileKeysV2 = { "card", "last-side-deck" };
        static readonly string[] ProfileKeysV1 = { "format", "points", "last-start-deck" };
        static readonly string[] RepeatableProfileKeys = { "card", "recipe", "recipe-deck", "recipe-side", "event-seen" };

        /// <summary>새로 만든 레시피 Id의 앞부분 (뒤에 번호).</summary>
        public const string NewRecipePrefix = "deck-";

        readonly List<PlayerRecipe> _recipes = new List<PlayerRecipe>();

        GameSave(CardCollection collection) => Collection = collection;

        public CardCollection Collection { get; }

        /// <summary>이벤트 도감 (런을 넘어 모음). 진행 중인 런의 기록은 저장할 때와 런을 정리할 때 더한다.</summary>
        public EventCodex Codex { get; } = new EventCodex();

        /// <summary>골드 누적 (끝난 런의 Gold를 더한 것). 진행 중인 런의 골드는 런이 끝날 때 더한다. 골드 상점에서 쓰고, 분해로 얻는다.</summary>
        public int Gold { get; private set; }

        /// <summary>
        /// 시작 사이드 덱의 영구 한도: 런을 시작할 때 사이드 덱에 넣을 수 있는 장수 (RunRules.StartingSideDeckMax ~ PermanentSideDeckMax).
        /// 골드로 한 장씩 늘린다 (BuySideDeckExpansion). 진행 중인 런에는 영향이 없다 (런 중의 사이드 덱에는 장수 제한이 없다).
        /// </summary>
        public int SideDeckLimit { get; private set; } = RunRules.StartingSideDeckMax;

        /// <summary>진행 중인 런. 없으면 null. 끝난 런은 CloseEndedRun으로 정리한다.</summary>
        public RunState Run { get; private set; }

        /// <summary>
        /// 읽은 저장 파일의 런이 듀얼 중이라(듀얼 도중에 게임이 꺼짐) 그 듀얼을 패배로 처리했으면 그 듀얼 번호, 아니면 0.
        /// 화면은 이것을 알리고 곧바로 다시 저장한다.
        /// </summary>
        public int ForfeitedDuel { get; private set; }

        /// <summary>마지막으로 런을 시작한 레시피 Id (없으면 null). 그 레시피를 지웠으면 남아 있지 않을 수 있다.</summary>
        public string LastStartDeckId { get; private set; }

        /// <summary>덱 레시피 (만든 순서).</summary>
        public IReadOnlyList<PlayerRecipe> Recipes => _recipes;

        public PlayerRecipe FindRecipe(string id) => _recipes.FirstOrDefault(r => r.Id == id);

        /// <summary>
        /// 시작 덱을 지급한 저장 (시험·헤드리스 실행용. 게임이 새로 만드는 저장은 Blank다): 진행 중인 런 없음. startDeck이 null이면 스타터 덱 4개를 지급한 컬렉션과 그 4개의 레시피,
        /// 아니면 그 덱 하나(예: 고른 공통 시작 덱 StructureDecks.StartVersions)만 지급한 컬렉션과 그 레시피.
        /// </summary>
        public static GameSave NewGame(DeckRecipe startDeck = null)
        {
            var decks = startDeck == null ? StarterDecks.All : new[] { startDeck };
            var save = new GameSave(CardCollection.FromRecipes(decks));
            save._recipes.AddRange(decks.Select(d => PlayerRecipe.FromStarter(d)));
            return save;
        }

        /// <summary>
        /// 게임이 새로 만드는 저장 (저장 파일이 없을 때, 하드 리셋): 시작 덱을 아직 고르지 않았다 (빈 컬렉션, 레시피 없음, 골드 0).
        /// 화면은 NeedsStartDeck이면 시작 덱 고르기 창을 띄우고, 고른 덱을 ChooseStartDeck으로 지급한다.
        /// </summary>
        public static GameSave Blank() => new GameSave(new CardCollection());

        /// <summary>시작 덱을 아직 고르지 않았는지: 컬렉션이 비어 있다 (분해는 카드마다 3장을 남기므로 고른 뒤에는 비지 않는다).</summary>
        public bool NeedsStartDeck => Collection.TotalCount == 0 && Run == null;

        /// <summary>
        /// 시작 덱을 고른다: 그 덱(공통 시작 덱 StructureDecks.StartVersions 중 하나)의 카드를 컬렉션에 넣고 그 레시피를 더한다.
        /// 시작 덱을 이미 받았거나(NeedsStartDeck이 아님) 공통 시작 덱이 아니면 InvalidOperationException·ArgumentException.
        /// </summary>
        public void ChooseStartDeck(DeckRecipe deck)
        {
            if (!NeedsStartDeck)
                throw new InvalidOperationException("시작 덱을 이미 받았습니다. 다시 고르려면 게임 데이터를 초기화하세요.");
            if (deck == null || !StructureDecks.StartVersions.Contains(deck))
                throw new ArgumentException("공통 시작 덱(StructureDecks.StartVersions)이 아닙니다: " + (deck?.Id ?? "null"), nameof(deck));
            foreach (var (code, count) in deck.Entries)
                Collection.Add(code, count);
            _recipes.Add(PlayerRecipe.FromStarter(deck));
        }

        /// <summary>새 레시피에 줄 Id: deck-번호 (지금 있는 deck-번호 중 가장 큰 것 + 1).</summary>
        public string NewRecipeId()
        {
            int max = 0;
            foreach (var recipe in _recipes)
                if (recipe.Id.StartsWith(NewRecipePrefix, StringComparison.Ordinal) &&
                    int.TryParse(recipe.Id.Substring(NewRecipePrefix.Length), out int n) && n > max)
                    max = n;
            return NewRecipePrefix + (max + 1);
        }

        /// <summary>
        /// 레시피를 저장한다 (같은 Id가 있으면 바꾸고, 없으면 끝에 더한다). 저장 규칙(RecipeRules.SaveProblems)을 어기면 저장하지 않고 그 문제 목록을 돌려준다.
        /// 저장했으면 빈 목록. 파일에 쓰는 것은 부르는 쪽(GameSaveFile.Save)이 한다.
        /// </summary>
        public List<string> SaveRecipe(PlayerRecipe recipe, CardDatabase database)
        {
            var problems = RecipeRules.SaveProblems(recipe, database);
            if (problems.Count > 0)
                return problems;
            int index = _recipes.FindIndex(r => r.Id == recipe.Id);
            if (index >= 0)
                _recipes[index] = recipe;
            else
                _recipes.Add(recipe);
            return problems;
        }

        /// <summary>레시피를 지운다. 진행 중인 런은 영향을 받지 않는다 (런은 시작할 때 덱을 복사해 들고 있다). 지웠으면 true.</summary>
        public bool DeleteRecipe(string id) => _recipes.RemoveAll(r => r.Id == id) > 0;

        /// <summary>런을 시작한다. 진행 중인 런이 있으면 InvalidOperationException (먼저 AbandonRun).</summary>
        public RunState StartRun(RunSetup setup, CardDatabase database, RarityTable rarities)
        {
            if (Run != null)
                throw new InvalidOperationException("진행 중인 런이 있습니다. 먼저 포기(AbandonRun)하거나 끝난 런을 정리(CloseEndedRun)하세요.");
            Run = RunState.Start(setup, database, rarities, Collection);
            LastStartDeckId = setup.StartDeck.Id;
            return Run;
        }

        /// <summary>
        /// 레시피로 런을 시작한다. 시작할 수 없는 레시피면 ArgumentException (RunState.CheckSetup의 문제 목록).
        /// runSideSlots는 이번 런에 살 사이드 덱 임시 칸 수다 (0 ~ RunSideSlotsMax). 그만큼 임시 칸의 카드를 앞에서부터 가져가고,
        /// 런이 시작되면 1장에 골드 RunRules.RunSideDeckSlotPrice를 낸다. 살 수 없으면 ArgumentException (RunSideSlotsProblem).
        /// </summary>
        public RunState StartRun(PlayerRecipe recipe, ulong seed, IReadOnlyList<DeckRecipe> opponents, CardDatabase database, RarityTable rarities,
            int runSideSlots = 0)
        {
            // 임시 칸까지 넘친 시작 사이드 덱은 여기서 막는다 (ToSetup은 앞에서부터 한도만큼만 가져가므로 CheckSetup은 넘친 것을 모른다).
            var problems = RecipeRules.StartOnlyProblems(recipe, database, null, SideDeckLimit);
            string problem = RunSideSlotsProblem(recipe, runSideSlots);
            if (problem != null)
                problems.Add(problem);
            if (problems.Count > 0)
                throw new ArgumentException("런을 시작할 수 없습니다: " + string.Join("; ", problems));
            var run = StartRun(recipe.ToSetup(seed, opponents, SideDeckLimit, runSideSlots), database, rarities);
            Gold -= RunRules.RunSideDeckSlotsPrice(runSideSlots);
            return run;
        }

        // ---------------- 골드 상점: 사이드 덱 ----------------

        /// <summary>사이드 덱 영구 확장을 끝까지 샀는지 (10장).</summary>
        public bool SideDeckLimitMaxed => SideDeckLimit >= RunRules.PermanentSideDeckMax;

        /// <summary>다음 사이드 덱 영구 확장의 값 (n번째 = 50 × n). 끝까지 샀으면 0.</summary>
        public int SideDeckExpansionPrice =>
            SideDeckLimitMaxed ? 0 : RunRules.SideDeckExpansionPrice(SideDeckLimit - RunRules.StartingSideDeckMax + 1);

        /// <summary>사이드 덱 영구 확장을 살 수 없는 까닭. 살 수 있으면 null.</summary>
        public string SideDeckExpansionProblem()
        {
            if (SideDeckLimitMaxed)
                return $"시작 사이드 덱은 {RunRules.PermanentSideDeckMax}장까지";
            int price = SideDeckExpansionPrice;
            return Gold < price ? $"골드가 {price - Gold} 모자람" : null;
        }

        /// <summary>골드를 내고 시작 사이드 덱의 영구 한도를 1장 늘린다. 샀으면 true (못 사면 아무것도 바뀌지 않는다).</summary>
        public bool BuySideDeckExpansion()
        {
            if (SideDeckExpansionProblem() != null)
                return false;
            Gold -= SideDeckExpansionPrice;
            SideDeckLimit++;
            return true;
        }

        /// <summary>이 레시피로 런을 시작할 때 살 수 있는 임시 칸의 최대 수: 임시 칸에 든 카드 수 (골드는 보지 않는다).</summary>
        public int RunSideSlotsMax(PlayerRecipe recipe) => recipe == null ? 0 : recipe.TempSideCards(SideDeckLimit);

        /// <summary>지금 골드로 살 수 있는 임시 칸 수 (RunSideSlotsMax 이하).</summary>
        public int RunSideSlotsAffordable(PlayerRecipe recipe) => Math.Min(RunSideSlotsMax(recipe), Gold / RunRules.RunSideDeckSlotPrice);

        /// <summary>임시 칸 slots장을 살 수 없는 까닭. 살 수 있으면 null.</summary>
        public string RunSideSlotsProblem(PlayerRecipe recipe, int slots)
        {
            if (slots < 0)
                return $"사이드 덱 임시 칸 {slots}장";
            int max = RunSideSlotsMax(recipe);
            if (slots > max)
                return $"사이드 덱 임시 칸 {slots}장 (임시 칸에 든 카드는 {max}장)";
            int price = RunRules.RunSideDeckSlotsPrice(slots);
            return Gold < price ? $"골드가 {price - Gold} 모자람 (사이드 덱 임시 칸 {slots}장에 골드 {price})" : null;
        }

        // ---------------- 골드 상점: 컬렉션 분해 ----------------

        /// <summary>이 카드 1장을 분해하면 받는 골드. 등급 표에 없는 카드는 값을 매길 수 없어 0 (분해할 수 없다).</summary>
        public static int DisassemblyGold(uint code, RarityTable rarities) =>
            rarities != null && rarities.TryGet(code, out var rarity) ? RunRules.DisassemblyGoldFor(rarity) : 0;

        /// <summary>이 카드에서 분해할 수 있는 장수: 컬렉션에서 RunRules.DisassemblyKeep(3)장을 넘는 분. 등급 표에 없는 카드는 0.</summary>
        public int DisassemblableCount(uint code, RarityTable rarities) =>
            DisassemblyGold(code, rarities) > 0 ? Math.Max(0, Collection.Count(code) - RunRules.DisassemblyKeep) : 0;

        /// <summary>분해할 수 있는 카드 전부 (카드 코드 순): 카드, 넘는 장수, 1장의 골드.</summary>
        public List<(uint Code, int Count, int GoldEach)> Disassemblable(RarityTable rarities)
        {
            var list = new List<(uint, int, int)>();
            foreach (uint code in Collection.Cards.Keys.OrderBy(c => c))
            {
                int count = DisassemblableCount(code, rarities);
                if (count > 0)
                    list.Add((code, count, DisassemblyGold(code, rarities)));
            }
            return list;
        }

        /// <summary>
        /// 컬렉션의 카드 count장을 분해해 골드로 바꾼다. 받은 골드를 돌려준다.
        /// 분해할 수 없으면 (count가 1보다 작음, 3장 아래로 내려감, 등급 표에 없는 카드) 아무것도 바꾸지 않고 0을 돌려준다.
        /// 진행 중인 런의 덱은 바뀌지 않는다 (런은 시작할 때 덱을 복사해 들고 있다).
        /// </summary>
        public int Disassemble(uint code, int count, RarityTable rarities)
        {
            if (count < 1 || count > DisassemblableCount(code, rarities))
                return 0;
            int gold = DisassemblyGold(code, rarities) * count;
            Collection.Remove(code, count);
            Gold += gold;
            return gold;
        }

        /// <summary>분해할 수 있는 카드를 모두 분해한다. 받은 골드를 돌려주고, cards에 분해한 장수를 적는다.</summary>
        public int DisassembleAll(RarityTable rarities, out int cards)
        {
            int gold = 0;
            cards = 0;
            foreach (var (code, count, _) in Disassemblable(rarities))
            {
                gold += Disassemble(code, count, rarities);
                cards += count;
            }
            return gold;
        }

        /// <summary>진행 중인 런을 포기하고 정리한다 (런 종료로 처리). 런이 없으면 아무것도 하지 않는다.</summary>
        public void AbandonRun()
        {
            if (Run == null)
                return;
            if (Run.Phase != RunPhase.Ended)
                Run.Abandon();
            CloseEndedRun();
        }

        /// <summary>런이 끝났으면 그 런의 골드(보상 골드 + 종료 정산)를 누적에 더하고 진행 중인 런을 비운다. 정리했으면 true.</summary>
        public bool CloseEndedRun()
        {
            if (Run == null || Run.Phase != RunPhase.Ended)
                return false;
            Codex.Absorb(Run);
            Gold += Run.Gold + Run.SettlementGold;
            Run = null;
            return true;
        }

        // ---------------- 쓰기 ----------------

        /// <summary>저장 파일 내용. 듀얼 중인 런도 쓴다 (읽으면 그 듀얼은 패배로 처리된다).</summary>
        public string Write()
        {
            var w = new SaveWriter();
            w.Comment("DuelRoguelike 저장 파일. 게임이 저절로 쓴다 (직접 고치면 읽지 못할 수 있음).");
            w.Line("format", FormatVersion);
            w.Line("gold", Gold);
            w.Line("side-limit", SideDeckLimit);
            if (LastStartDeckId != null)
                w.Line("last-start-deck", LastStartDeckId);
            w.Comment($"덱 레시피 {_recipes.Count}개: recipe Id 이름(공백은 %20) / recipe-deck Id 메인·엑스트라 카드 / recipe-side Id 시작 사이드 덱 카드");
            foreach (var recipe in _recipes)
            {
                w.Line("recipe", recipe.Id, SaveEscape.Encode(recipe.Name));
                w.Line("recipe-deck", new object[] { recipe.Id }.Concat(recipe.Deck.Cast<object>()));
                w.Line("recipe-side", new object[] { recipe.Id }.Concat(recipe.Side.Cast<object>()));
            }
            w.Comment($"컬렉션 {Collection.Cards.Count}종 {Collection.TotalCount}장: card 카드코드 장수");
            foreach (var pair in Collection.Cards.OrderBy(p => p.Key))
                w.Line("card", pair.Key, pair.Value);
            Codex.Absorb(Run);
            Codex.Write(w);
            if (Run != null)
            {
                w.Comment(Run.Phase == RunPhase.InDuel ? "진행 중인 런 (듀얼 중: 이대로 읽으면 그 듀얼은 패배)" : "진행 중인 런");
                w.Line("run");
                Run.WriteTo(w);
                w.Line("end-run");
            }
            return w.ToString();
        }

        // ---------------- 읽기 ----------------

        /// <summary>저장 파일 내용을 읽는다. 형식이 틀리면 FormatException (파일 이름과 줄 번호 포함).</summary>
        public static GameSave Parse(string text, CardDatabase database, RarityTable rarities, string sourceName = GameSaveFile.FileName)
        {
            var r = new SaveReader(sourceName);
            var lines = SaveReader.Split(text ?? "");
            if (lines.Count == 0 || lines[0].Key != "format")
                throw r.Error("첫 줄이 \"format 버전\"이 아님");
            r.ExpectCount(lines[0], 1);
            int version = r.Int(lines[0], lines[0].Values[0], 1);
            if (version > FormatVersion)
                throw r.Error(lines[0], $"이 게임보다 새 버전이 쓴 저장 파일 (형식 {version}, 이 게임은 {FormatVersion}까지 읽음)");

            // 런 묶음(run … end-run)을 먼저 떼어 낸다. 런은 컬렉션을 들고 있으므로 컬렉션을 다 읽은 뒤에 만든다.
            var profile = new List<SaveLine>();
            List<SaveLine> runLines = null;
            SaveLine runStart = default;
            bool inRun = false;
            foreach (var line in lines)
            {
                if (inRun)
                {
                    if (line.Key == "end-run")
                    {
                        r.ExpectCount(line, 0);
                        inRun = false;
                    }
                    else
                        runLines.Add(line);
                    continue;
                }
                if (line.Key == "run")
                {
                    r.ExpectCount(line, 0);
                    if (runLines != null)
                        throw r.Error(line, "진행 중인 런이 두 번 나옴");
                    runLines = new List<SaveLine>();
                    runStart = line;
                    inRun = true;
                    continue;
                }
                if (line.Key == "end-run")
                    throw r.Error(line, "run 없이 end-run이 나옴");
                profile.Add(line);
            }
            if (inRun)
                throw r.Error(runStart, "end-run이 없음");

            var map = r.Unique(profile, version < 2 ? ProfileKeysV1 : version < 6 ? ProfileKeysV5 : ProfileKeys,
                version < 3 ? RepeatableProfileKeysV2 : RepeatableProfileKeys);
            var collection = new CardCollection();
            foreach (var line in profile.Where(l => l.Key == "card"))
            {
                r.ExpectCount(line, 2);
                uint code = r.Code(line, line.Values[0]);
                if (collection.Count(code) > 0)
                    throw r.Error(line, $"카드 {code}가 두 번 나옴");
                collection.Add(code, r.Int(line, line.Values[1], 1));
            }

            var save = new GameSave(collection);
            if (map.TryGetValue("points", out var points))
            {
                // 형식 1: 등급별 포인트 대체 횟수 → 골드 환산표로 골드
                r.ExpectCount(points, RarityTable.RarityCount);
                for (int i = 0; i < RarityTable.RarityCount; i++)
                    save.Gold += r.Int(points, points.Values[i], 0) * RunRules.GoldFor((Rarity)i);
            }
            if (map.TryGetValue("gold", out var gold))
            {
                r.ExpectCount(gold, 1);
                save.Gold = r.Int(gold, gold.Values[0], 0);
            }
            if (map.TryGetValue("side-limit", out var sideLimit))
            {
                // 형식 6부터. 없으면 처음 값(3장) 그대로다.
                r.ExpectCount(sideLimit, 1);
                int limit = r.Int(sideLimit, sideLimit.Values[0], RunRules.StartingSideDeckMax);
                if (limit > RunRules.PermanentSideDeckMax)
                    throw r.Error(sideLimit, $"시작 사이드 덱 한도 {limit}장 ({RunRules.PermanentSideDeckMax}장까지)");
                save.SideDeckLimit = limit;
            }
            if (map.TryGetValue("last-start-deck", out var lastDeck))
            {
                r.ExpectCount(lastDeck, 1);
                save.LastStartDeckId = lastDeck.Values[0];
            }
            if (version < 3)
                ReadRecipesV2(profile, r, save);
            else
                ReadRecipes(profile, r, save);
            foreach (var line in profile.Where(l => l.Key == "event-seen"))
                save.Codex.Read(line, r);
            if (runLines != null)
                save.Run = RunState.ReadFrom(runLines, r, database, rarities, collection, version);
            if (save.Run != null && save.Run.Phase == RunPhase.InDuel)
            {
                // 듀얼 도중에 게임이 꺼졌다: 그 듀얼은 패배 (규칙 결정 문서 "저장·무작위 결과").
                save.ForfeitedDuel = save.Run.CurrentDuel.DuelNumber;
                save.Run.ForfeitDuel();
            }
            return save;
        }

        /// <summary>형식 3: recipe(Id, 이름) 줄마다 recipe-deck·recipe-side 줄이 하나씩. 카드 규칙은 보지 않는다 (편집 화면이 알려 준다).</summary>
        static void ReadRecipes(List<SaveLine> profile, SaveReader r, GameSave save)
        {
            var names = new List<(SaveLine Line, string Id, string Name)>();
            foreach (var line in profile.Where(l => l.Key == "recipe"))
            {
                r.ExpectCount(line, 2);
                string id = line.Values[0];
                if (names.Any(n => n.Id == id))
                    throw r.Error(line, $"레시피 {id}가 두 번 나옴");
                names.Add((line, id, SaveEscape.Decode(line.Values[1], r, line)));
            }
            Dictionary<string, List<uint>> Cards(string key)
            {
                var cards = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
                foreach (var line in profile.Where(l => l.Key == key))
                {
                    r.ExpectAtLeast(line, 1);
                    string id = line.Values[0];
                    if (names.All(n => n.Id != id))
                        throw r.Error(line, $"{key}의 레시피 {id}가 recipe 줄에 없음");
                    if (cards.ContainsKey(id))
                        throw r.Error(line, $"레시피 {id}의 {key}가 두 번 나옴");
                    cards[id] = r.Codes(line, 1);
                }
                return cards;
            }
            var decks = Cards("recipe-deck");
            var sides = Cards("recipe-side");
            foreach (var (line, id, name) in names)
            {
                if (!decks.TryGetValue(id, out var deck))
                    throw r.Error(line, $"레시피 {id}의 recipe-deck 줄이 없음");
                try
                {
                    save._recipes.Add(new PlayerRecipe(id, name, deck, sides.TryGetValue(id, out var side) ? side : null));
                }
                catch (ArgumentException e)
                {
                    throw r.Error(line, e.Message);
                }
            }
        }

        /// <summary>형식 2까지: 레시피가 없다. 스타터 덱 4개를 레시피로 만들고, 시작 덱별 마지막 시작 사이드 덱을 그 사이드 덱으로 옮긴다.</summary>
        static void ReadRecipesV2(List<SaveLine> profile, SaveReader r, GameSave save)
        {
            var lastSide = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
            foreach (var line in profile.Where(l => l.Key == "last-side-deck"))
            {
                r.ExpectAtLeast(line, 1);
                string id = line.Values[0];
                if (lastSide.ContainsKey(id))
                    throw r.Error(line, $"시작 덱 {id}가 두 번 나옴");
                lastSide[id] = r.Codes(line, 1);
            }
            foreach (var deck in StarterDecks.All)
                save._recipes.Add(PlayerRecipe.FromStarter(deck, lastSide.TryGetValue(deck.Id, out var side) ? side : null));
        }
    }

    /// <summary>저장 파일 읽기·쓰기 (폴더 하나에 save.txt, 바로 전 저장 save.txt.bak).</summary>
    public static class GameSaveFile
    {
        public const string FileName = "save.txt";

        public static string FilePath(string directory) => Path.Combine(directory, FileName);

        /// <summary>바로 전에 저장했던 파일 (쓸 때마다 이전 save.txt를 여기로 옮긴다).</summary>
        public static string BackupPath(string directory) => FilePath(directory) + ".bak";

        /// <summary>
        /// 저장을 읽는다. 파일이 없으면 시작 덱을 고르기 전의 새 저장(GameSave.Blank, notice는 null).
        /// 파일이 깨져서 못 읽으면 save-broken-날짜.txt로 옮겨 두고, 바로 전 저장(.bak)을 읽을 수 있으면 그것으로, 아니면 새 저장(Blank)으로 시작한다.
        /// 그때 notice에 무슨 일이 있었는지 적는다 (화면에 보여 준다).
        /// </summary>
        public static GameSave Load(string directory, CardDatabase database, RarityTable rarities, out string notice)
        {
            notice = null;
            string path = FilePath(directory);
            if (!File.Exists(path))
                return GameSave.Blank();
            try
            {
                return GameSave.Parse(File.ReadAllText(path, Encoding.UTF8), database, rarities, FileName);
            }
            catch (FormatException e)
            {
                string broken = BrokenPath(directory);
                File.Move(path, broken);
                string backup = BackupPath(directory);
                if (File.Exists(backup))
                {
                    try
                    {
                        var save = GameSave.Parse(File.ReadAllText(backup, Encoding.UTF8), database, rarities, Path.GetFileName(backup));
                        notice = $"저장 파일을 읽지 못해 {Path.GetFileName(broken)}(으)로 옮겨 두고, 바로 전 저장({Path.GetFileName(backup)})으로 이어 갑니다. ({e.Message})";
                        return save;
                    }
                    catch (FormatException)
                    {
                    }
                }
                notice = $"저장 파일을 읽지 못해 {Path.GetFileName(broken)}(으)로 옮겨 두고 새로 시작합니다. ({e.Message})";
                return GameSave.Blank();
            }
        }

        /// <summary>저장한다. 임시 파일에 다 쓴 뒤 바꿔 끼우므로, 쓰다가 꺼져도 이전 파일이 남는다.</summary>
        public static void Save(GameSave save, string directory)
        {
            string text = save.Write();
            Directory.CreateDirectory(directory);
            string path = FilePath(directory);
            string temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temp, path, BackupPath(directory));
            else
                File.Move(temp, path);
        }

        /// <summary>
        /// 하드 리셋: 시작 덱을 고르기 전의 새 저장(GameSave.Blank)을 저장하고, 예전 게임이 남은 파일(바로 전 저장 .bak, 쓰다 남은 .tmp, 깨진 저장 save-broken-*.txt)을 모두 지운다.
        /// .bak을 남기면 새 save.txt가 깨졌을 때 예전 게임으로 돌아가므로 함께 지운다. 음량 등 설정(PlayerPrefs)은 저장 파일 밖이라 그대로다.
        /// 새 save.txt를 먼저 다 쓴 뒤에 지우므로, 도중에 꺼져도 save.txt는 예전 것이거나 새 것 중 하나다.
        /// </summary>
        public static GameSave Reset(string directory)
        {
            var save = GameSave.Blank();
            Directory.CreateDirectory(directory);
            string path = FilePath(directory);
            string temp = path + ".tmp";
            File.WriteAllText(temp, save.Write(), new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
            if (File.Exists(BackupPath(directory)))
                File.Delete(BackupPath(directory));
            foreach (string broken in Directory.GetFiles(directory, "save-broken-*.txt"))
                File.Delete(broken);
            return save;
        }

        static string BrokenPath(string directory)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = Path.Combine(directory, $"save-broken-{stamp}.txt");
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(directory, $"save-broken-{stamp}-{i}.txt");
            return path;
        }
    }
}
