// 덱 편집: 레시피 하나를 컬렉션으로 고치는 작업본 (런 시작 전 덱 편집 화면이 쓴다. 화면 코드 없음).
// 고치는 동안에는 규칙을 어긴 상태(메인 덱 40장 미만 등)도 둔다. 저장할 때 PlayerRecipe 규칙(RecipeRules)을 본다.
// 넣기를 막는 것: 메인 덱 60장·엑스트라 덱 15장·시작 사이드 덱 3장이 가득 참, 같은 카드명 3장 (덱 + 사이드 합쳐),
// 카드 DB에 없는 카드. 컬렉션보다 많이 넣는 것은 막지 않는다 (저장은 되고 런 시작만 막힌다, 사용자 결정 2026-11-10).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public sealed class RecipeEditor
    {
        readonly CardDatabase _database;
        readonly CardCollection _collection;
        readonly List<uint> _deck = new List<uint>();
        readonly List<uint> _side = new List<uint>();
        PlayerRecipe _saved;

        /// <summary>source를 고친다. source가 null이면 빈 새 레시피 (Id는 저장할 때 정한다).</summary>
        public RecipeEditor(PlayerRecipe source, CardDatabase database, CardCollection collection, string newName = "새 레시피")
        {
            _database = database;
            _collection = collection;
            _saved = source;
            Name = source?.Name ?? newName;
            if (source != null)
            {
                _deck.AddRange(source.Deck);
                _side.AddRange(source.Side);
            }
        }

        /// <summary>source를 복사한 새 레시피 (저장하면 새 Id를 받는다. source는 그대로).</summary>
        public static RecipeEditor CopyOf(PlayerRecipe source, CardDatabase database, CardCollection collection, string name)
        {
            var editor = new RecipeEditor(source, database, collection) { Name = name };
            editor._saved = null;
            return editor;
        }

        /// <summary>고치는 레시피의 Id. 아직 저장하지 않은 새 레시피면 null.</summary>
        public string Id => _saved?.Id;

        public string Name { get; set; }

        public IReadOnlyList<uint> Deck => _deck;
        public IReadOnlyList<uint> Side => _side;
        public IEnumerable<uint> MainDeck => _deck.Where(c => !IsExtraDeckCard(c));
        public IEnumerable<uint> ExtraDeck => _deck.Where(IsExtraDeckCard);
        public int MainCount => _deck.Count(c => !IsExtraDeckCard(c));
        public int ExtraCount => _deck.Count(IsExtraDeckCard);

        /// <summary>마지막으로 저장(또는 연) 뒤에 바뀌었는지. 새 레시피는 늘 true.</summary>
        public bool IsDirty =>
            _saved == null || (Name ?? "").Trim() != _saved.Name || !Same(_deck, _saved.Deck) || !Same(_side, _saved.Side);

        static bool Same(IReadOnlyList<uint> a, IReadOnlyList<uint> b) =>
            a.Count == b.Count && a.OrderBy(c => c).SequenceEqual(b.OrderBy(c => c));

        public bool IsExtraDeckCard(uint code) => _database.TryGet(code, out var record) && DeckRecipe.IsExtraDeckType(record.Type);

        /// <summary>컬렉션 장수.</summary>
        public int Owned(uint code) => _collection?.Count(code) ?? 0;

        /// <summary>이 레시피에 든 장수 (덱 + 사이드, 같은 ID).</summary>
        public int Used(uint code) => _deck.Count(c => c == code) + _side.Count(c => c == code);

        /// <summary>컬렉션에서 아직 넣지 않은 장수 (넘쳤으면 음수).</summary>
        public int Left(uint code) => Owned(code) - Used(code);

        /// <summary>덱 + 사이드에 든 같은 카드명의 장수.</summary>
        public int NameCopies(uint code)
        {
            uint name = _database.NameCode(code);
            return _deck.Concat(_side).Count(c => _database.NameCode(c) == name);
        }

        /// <summary>컬렉션보다 많이 넣은 카드 (같은 ID 기준).</summary>
        public List<uint> ShortCards() => _deck.Concat(_side).Distinct().Where(c => Left(c) < 0).ToList();

        /// <summary>컬렉션에서 덱(toSide면 시작 사이드 덱)에 1장 넣을 수 없는 까닭. 넣을 수 있으면 null.</summary>
        public string AddReason(uint code, bool toSide)
        {
            if (!_database.TryGet(code, out _))
                return "카드 DB에 없음";
            if (NameCopies(code) >= RunRules.MaxCopies)
                return $"같은 카드명은 덱과 사이드 덱을 합쳐 {RunRules.MaxCopies}장까지";
            return toSide ? SideFullReason() : DeckFullReason(code);
        }

        /// <summary>덱 ↔ 시작 사이드 덱으로 옮길 수 없는 까닭 (toSide: 덱 → 사이드). 옮길 수 있으면 null.</summary>
        public string MoveReason(uint code, bool toSide)
        {
            if (!(toSide ? _deck : _side).Contains(code))
                return toSide ? "덱에 없는 카드" : "사이드 덱에 없는 카드";
            return toSide ? SideFullReason() : DeckFullReason(code);
        }

        string SideFullReason() =>
            _side.Count >= RunRules.StartingSideDeckMax ? $"시작 사이드 덱은 {RunRules.StartingSideDeckMax}장까지" : null;

        string DeckFullReason(uint code) =>
            IsExtraDeckCard(code)
                ? ExtraCount >= RunRules.MaxExtraDeck ? $"엑스트라 덱은 {RunRules.MaxExtraDeck}장까지" : null
                : MainCount >= RunRules.MaxMainDeck ? $"메인 덱은 {RunRules.MaxMainDeck}장까지" : null;

        public bool Add(uint code, bool toSide)
        {
            if (AddReason(code, toSide) != null)
                return false;
            (toSide ? _side : _deck).Add(code);
            return true;
        }

        public bool Move(uint code, bool toSide)
        {
            if (MoveReason(code, toSide) != null)
                return false;
            (toSide ? _deck : _side).Remove(code);
            (toSide ? _side : _deck).Add(code);
            return true;
        }

        /// <summary>덱(fromSide면 시작 사이드 덱)에서 1장 빼 컬렉션으로 돌린다.</summary>
        public bool Remove(uint code, bool fromSide) => (fromSide ? _side : _deck).Remove(code);

        public void ClearSide() => _side.Clear();

        /// <summary>지금 내용의 레시피 (id: 저장할 Id).</summary>
        public PlayerRecipe Build(string id) => new PlayerRecipe(id, (Name ?? "").Trim(), _deck, _side);

        PlayerRecipe Preview() => Build(Id ?? "new");

        public List<string> SaveProblems() => RecipeRules.SaveProblems(Preview(), _database);

        public List<string> StartOnlyProblems() => RecipeRules.StartOnlyProblems(Preview(), _database, _collection);

        /// <summary>저장했다: 이제 이 레시피를 고치는 중으로 친다 (IsDirty가 false가 된다).</summary>
        public void MarkSaved(PlayerRecipe saved) => _saved = saved ?? throw new ArgumentNullException(nameof(saved));
    }
}
