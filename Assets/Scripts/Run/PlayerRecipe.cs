// 덱 레시피 (규칙 결정 문서 "덱 편집·투입 코스트", 설계 문서 "카드풀과 시작 덱": 덱은 레시피로 저장한다).
// 레시피 = 이름 + 덱(메인·엑스트라, DeckRecipe처럼 나누지 않은 한 목록) + 시작 사이드 덱. 런을 시작할 때 레시피 하나를 고른다.
// 레시피는 GameSave에 들고, 컬렉션에서 짠다 (RecipeEditor). 새 게임에는 스타터 덱 4개가 고칠 수 있는 레시피로 들어 있다.
//
// 저장 규칙 (사용자 결정 2026-11-10):
// - 저장할 수 없음 (SaveProblems): 이름 없음, 카드 DB에 없는 카드, 메인 덱 40~60장이 아님, 엑스트라 덱 15장 초과,
//   같은 카드명 4장 이상 (덱과 시작 사이드 덱을 합쳐, 카드 DB의 alias 기준: 에라타 전·후 버전도 합친다).
// - 저장은 되지만 런을 시작할 수 없음 (StartOnlyProblems): 컬렉션보다 많은 장수, 시작 사이드 덱 한도(3장) 초과.
//   투입 코스트가 생기면 코스트 초과도 여기에 든다 (골드로 임시 완화할 수 있으므로).
// 런을 시작할 때의 최종 확인은 RunState.CheckSetup이 한다 (같은 규칙).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public sealed class PlayerRecipe
    {
        /// <summary>레시피 이름의 최대 글자 수.</summary>
        public const int MaxNameLength = 24;

        /// <summary>저장 파일 안의 이름 (공백·#·= 없는 글). 스타터 덱에서 온 레시피는 스타터 덱 Id(Yugi 등), 새 레시피는 deck-번호.</summary>
        public string Id { get; }

        public string Name { get; }

        /// <summary>메인·엑스트라 덱 카드 (장수만큼 반복). 엔진처럼 나누지 않는다: 카드 종류로 엑스트라 덱이 정해진다.</summary>
        public IReadOnlyList<uint> Deck { get; }

        /// <summary>시작 사이드 덱 카드.</summary>
        public IReadOnlyList<uint> Side { get; }

        public PlayerRecipe(string id, string name, IEnumerable<uint> deck, IEnumerable<uint> side)
        {
            if (string.IsNullOrEmpty(id) || id.Any(c => char.IsWhiteSpace(c) || c == '#' || c == '='))
                throw new ArgumentException($"레시피 Id \"{id}\"는 공백·#·= 없는 글이어야 합니다.", nameof(id));
            Id = id;
            Name = name ?? "";
            Deck = (deck ?? Enumerable.Empty<uint>()).ToArray();
            Side = (side ?? Enumerable.Empty<uint>()).ToArray();
        }

        /// <summary>엔진·런에 넘길 덱 (같은 카드는 처음 나온 순서로 묶는다).</summary>
        public DeckRecipe ToDeckRecipe() =>
            new DeckRecipe(Id, Name, Deck.GroupBy(c => c).Select(g => (g.Key, g.Count())).ToList());

        /// <summary>이 레시피로 런을 시작하는 설정.</summary>
        public RunSetup ToSetup(ulong seed, IReadOnlyList<DeckRecipe> opponents) =>
            new RunSetup { Seed = seed, StartDeck = ToDeckRecipe(), SideDeck = Side.ToList(), Opponents = opponents };

        /// <summary>스타터 덱 → 같은 Id·이름의 레시피 (사이드 덱은 비어 있음).</summary>
        public static PlayerRecipe FromStarter(DeckRecipe starter, IEnumerable<uint> side = null) =>
            new PlayerRecipe(starter.Id, starter.Name, starter.ToCardList(), side);

        /// <summary>
        /// 런의 지금 덱 → 레시피 (층 사이 화면의 "지금 덱을 레시피로 저장"). 메인·엑스트라 덱만 담고 시작 사이드 덱은 비운다:
        /// 런 중 사이드 덱은 장수 제한이 없어 시작 사이드 덱 한도와 맞지 않는다. 봉인된 카드는 넣지 않는다.
        /// </summary>
        public static PlayerRecipe FromRun(RunState run, string id, string name) =>
            new PlayerRecipe(id, name, run.MainDeck.Concat(run.ExtraDeck), null);

        public override string ToString() => $"{Name} ({Id})";
    }

    /// <summary>레시피 규칙 (머리말 참고). 문제가 없으면 빈 목록.</summary>
    public static class RecipeRules
    {
        /// <summary>이름의 문제. 없으면 null.</summary>
        public static string NameProblem(string name)
        {
            string trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0)
                return "레시피 이름이 없음";
            if (trimmed.Length > PlayerRecipe.MaxNameLength)
                return $"레시피 이름이 {trimmed.Length}자 ({PlayerRecipe.MaxNameLength}자까지)";
            return null;
        }

        /// <summary>저장할 수 없게 하는 문제: 이름, 카드 DB, 메인 40~60장, 엑스트라 15장, 같은 카드명 3장 (덱 + 사이드 합쳐).</summary>
        public static List<string> SaveProblems(PlayerRecipe recipe, CardDatabase database)
        {
            var problems = new List<string>();
            string name = NameProblem(recipe.Name);
            if (name != null)
                problems.Add(name);
            problems.AddRange(recipe.ToDeckRecipe().Validate(database));
            foreach (uint code in recipe.Side.Distinct())
                if (!database.TryGet(code, out _))
                    problems.Add($"사이드 덱의 {code}: 카드 DB에 없음");
            // 덱만으로 넘친 카드명은 Validate가 이미 적었다. 사이드 덱이 더해져 넘친 것만 더 적는다 (RunState.CheckSetup과 같은 글).
            var sideNames = new HashSet<uint>(recipe.Side.Select(database.NameCode));
            foreach (var group in recipe.Deck.Concat(recipe.Side).GroupBy(database.NameCode))
                if (sideNames.Contains(group.Key) && group.Count() > RunRules.MaxCopies)
                    problems.Add($"{database.GetName(group.Key)}: 시작 덱과 사이드 덱을 합쳐 같은 카드명 {group.Count()}장 ({RunRules.MaxCopies}장까지)");
            return problems;
        }

        /// <summary>저장은 되지만 런을 시작할 수 없게 하는 문제: 시작 사이드 덱 한도, 컬렉션 장수.</summary>
        public static List<string> StartOnlyProblems(PlayerRecipe recipe, CardDatabase database, CardCollection collection)
        {
            var problems = new List<string>();
            if (recipe.Side.Count > RunRules.StartingSideDeckMax)
                problems.Add($"사이드 덱 {recipe.Side.Count}장 ({RunRules.StartingSideDeckMax}장 이하여야 함)");
            if (collection != null)
                problems.AddRange(collection.CheckCards(recipe.Deck.Concat(recipe.Side), database));
            return problems;
        }

        /// <summary>이 레시피로 런을 시작할 수 없게 하는 문제 전부 (저장 규칙 + 시작 규칙).</summary>
        public static List<string> StartProblems(PlayerRecipe recipe, CardDatabase database, CardCollection collection)
        {
            var problems = SaveProblems(recipe, database);
            problems.AddRange(StartOnlyProblems(recipe, database, collection));
            return problems;
        }
    }
}
