// 탑 이벤트의 틀 (규칙 결정 문서 "이벤트 시스템").
// - 이벤트는 상황 글 → 선택지 → 결과로 진행한다. 선택지가 없는 사건형은 "확인" 하나뿐인 이벤트로 나타낸다.
// - 선택지 조건은 주사위 대신 카드 보유(카드명 또는 스펙 + 장수)다. 칩이 드는 선택지는 그만큼 가져야 고를 수 있다.
// - 결과는 칩·LP·골드·카드 얻기·봉인·봉인 해제·이번 런에서 잃음·이벤트 전투 중 무엇이든 될 수 있다.
// - 태그: 자리(1~4층 / 6층 / 7~8층), 깊이(최소·최대 층), 성향(+기대값), 형식, 덱(공용 / 특정 시작 덱), 가중치, 보유 범위, 반복.
// 이벤트 목록(견본)은 TowerEventList.cs에 있다. 이 파일은 듀얼 엔진·Unity를 모른다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    /// <summary>이벤트 성향. 성향을 먼저 뽑고(35 : 15 : 50) 그 안에서 이벤트를 고른다.</summary>
    public enum EventTendency
    {
        Positive,
        Neutral,
        Negative,
    }

    /// <summary>이벤트가 나올 수 있는 자리 (10층 묶음 안의 위치). 여러 개 가능.</summary>
    [Flags]
    public enum EventSlots
    {
        None = 0,
        /// <summary>1~4층 (확률 이벤트 층)</summary>
        Early = 1,
        /// <summary>6층 (고정 이벤트 층)</summary>
        Sixth = 2,
        /// <summary>7~8층 (확률 이벤트 층)</summary>
        Late = 4,
        Any = Early | Sixth | Late,
    }

    /// <summary>이벤트 조건에서 "가지고 있다"로 치는 범위. 이벤트마다 정한다.</summary>
    public enum HoldScope
    {
        /// <summary>메인 덱 + 엑스트라 덱</summary>
        Deck,
        /// <summary>메인·엑스트라 덱 + 사이드 덱</summary>
        DeckAndSide,
        /// <summary>메인·엑스트라·사이드 덱 + 상점 덱</summary>
        All,
    }

    /// <summary>카드 보유 조건: 카드명 또는 스펙에 맞는 카드를 Count장 이상 가지고 있다.</summary>
    public sealed class CardCondition
    {
        /// <summary>"드래곤족 몬스터" 같은 카드 설명 (장수·범위는 Describe가 붙인다).</summary>
        public string Text { get; }
        public int Count { get; }
        readonly Func<uint, CardRecord, bool> _match;

        CardCondition(string text, int count, Func<uint, CardRecord, bool> match)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count));
            Text = text;
            Count = count;
            _match = match;
        }

        /// <summary>카드명 조건 (일러스트만 다른 카드도 같은 카드로 친다).</summary>
        public static CardCondition Named(uint code, string name, int count = 1) =>
            new CardCondition($"「{name}」", count, (c, r) => c == code || (r != null && r.Data.Alias == code));

        /// <summary>스펙 조건 ("드래곤족 레벨 7 이상 몬스터" 같은 것).</summary>
        public static CardCondition Spec(string text, Func<CardRecord, bool> match, int count = 1) =>
            new CardCondition(text, count, (c, r) => r != null && match(r));

        public bool Matches(uint code, CardDatabase database)
        {
            database.TryGet(code, out var record);
            return _match(code, record);
        }

        public int CountIn(IEnumerable<uint> cards, CardDatabase database) => cards.Count(c => Matches(c, database));

        /// <summary>"드래곤족 몬스터 1장 (덱·사이드 덱)"</summary>
        public string Describe(HoldScope scope) => $"{Text} {Count}장{(Count > 1 ? " 이상" : "")} ({ScopeName(scope)})";

        public static string ScopeName(HoldScope scope) =>
            scope == HoldScope.Deck ? "덱" : scope == HoldScope.DeckAndSide ? "덱·사이드 덱" : "덱·사이드 덱·상점 덱";
    }

    public enum EventEffectKind
    {
        /// <summary>칩을 얻거나(+) 잃는다(-, 가진 만큼만).</summary>
        Chips,
        /// <summary>LP를 더하거나(+) 뺀다(-, 1 아래로는 내려가지 않는다).</summary>
        Lp,
        /// <summary>골드(영구 재화)를 얻는다.</summary>
        Gold,
        /// <summary>카드 1장을 얻는다 (사이드 덱과 컬렉션으로, 승리 보상과 같다).</summary>
        GainCard,
        /// <summary>메인 덱에서 무작위로 Amount장 봉인한다.</summary>
        Seal,
        /// <summary>봉인된 카드 1장을 무작위로 봉인 해제한다 (사이드 덱으로). 봉인된 카드가 없으면 칩 Amount.</summary>
        Unseal,
        /// <summary>조건에 맞는 카드 1장을 이번 런에서 잃는다 (덱·사이드 덱에서 빠진다. 컬렉션에는 남는다).</summary>
        LoseCard,
    }

    /// <summary>결과 하나.</summary>
    public sealed class EventEffect
    {
        public EventEffectKind Kind { get; }
        public int Amount { get; }
        public uint Card { get; }
        public string CardName { get; }
        public CardCondition Target { get; }

        EventEffect(EventEffectKind kind, int amount, uint card = 0, string cardName = null, CardCondition target = null)
        {
            Kind = kind;
            Amount = amount;
            Card = card;
            CardName = cardName;
            Target = target;
        }

        public static EventEffect Chips(int amount) => new EventEffect(EventEffectKind.Chips, amount);
        public static EventEffect Lp(int amount) => new EventEffect(EventEffectKind.Lp, amount);
        public static EventEffect Gold(int amount) => new EventEffect(EventEffectKind.Gold, amount);
        public static EventEffect Gain(uint code, string name) => new EventEffect(EventEffectKind.GainCard, 1, code, name);
        public static EventEffect Seal(int count) => new EventEffect(EventEffectKind.Seal, count);
        /// <param name="fallbackChips">봉인된 카드가 없을 때 대신 받는 칩.</param>
        public static EventEffect Unseal(int fallbackChips) => new EventEffect(EventEffectKind.Unseal, fallbackChips);
        public static EventEffect Lose(CardCondition target) => new EventEffect(EventEffectKind.LoseCard, 1, target: target);

        /// <summary>결과를 아는 선택지에 보이는 짧은 글. "칩 +50", "봉인 1장"</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case EventEffectKind.Chips: return Amount >= 0 ? $"칩 +{Amount}" : $"칩 {Amount}";
                case EventEffectKind.Lp: return Amount >= 0 ? $"LP +{Amount}" : $"LP {Amount}";
                case EventEffectKind.Gold: return $"골드 +{Amount}";
                case EventEffectKind.GainCard: return $"「{CardName}」 1장";
                case EventEffectKind.Seal: return $"봉인 {Amount}장";
                case EventEffectKind.Unseal: return $"봉인 해제 1장 (봉인된 카드가 없으면 칩 +{Amount})";
                case EventEffectKind.LoseCard: return $"{Target.Text} 1장을 이번 런에서 잃음";
                default: return Kind.ToString();
            }
        }
    }

    /// <summary>선택지 하나: 조건(없을 수 있음), 결과, 결과 글. 이벤트 전투면 이겼을 때 결과를 받는다.</summary>
    public sealed class EventChoice
    {
        public string Label { get; }
        /// <summary>고른 뒤 보이는 글.</summary>
        public string ResultText { get; }
        public CardCondition Requires { get; private set; }
        /// <summary>이만큼 칩을 가져야 고를 수 있다 (0이면 조건 없음). 칩을 쓰는 것은 결과(Chips -n)에 따로 적는다.</summary>
        public int MinChips { get; private set; }
        public IReadOnlyList<EventEffect> Effects { get; }
        /// <summary>이벤트 전투의 상대 (스타터 덱 Id). null이면 전투 없음.</summary>
        public string BattleDeckId { get; private set; }
        /// <summary>다른 선택지가 모두 잠겼을 때만 나오고, 그때는 이것만 고를 수 있다 (선택 강제).</summary>
        public bool IsForced { get; private set; }

        public EventChoice(string label, string resultText, params EventEffect[] effects)
        {
            Label = label;
            ResultText = resultText;
            Effects = effects;
        }

        public bool HasCondition => Requires != null || MinChips > 0;
        public bool IsBattle => BattleDeckId != null;

        public EventChoice If(CardCondition condition) { Requires = condition; return this; }
        public EventChoice IfChips(int chips) { MinChips = chips; return this; }
        /// <summary>이벤트 전투: 상대 덱 Id. 이기면 Effects를 받고(일반 승리 보상 없음), 지면 일반 패배처럼 봉인된다.</summary>
        public EventChoice Battle(string deckId) { BattleDeckId = deckId; return this; }
        public EventChoice Forced() { IsForced = true; return this; }

        /// <summary>결과 요약: "칩 +50, 봉인 1장". 전투면 "이벤트 전투 (상대 …) — 이기면 …".</summary>
        public string DescribeEffects()
        {
            string effects = Effects.Count == 0 ? "아무 일 없음" : string.Join(", ", Effects.Select(e => e.Describe()));
            return IsBattle ? $"이벤트 전투 (상대 {StarterDecks.Find(BattleDeckId)?.Name ?? BattleDeckId}) — 이기면 {effects}" : effects;
        }
    }

    /// <summary>이벤트 하나와 태그.</summary>
    public sealed class TowerEvent
    {
        /// <summary>저장 파일·도감에 적는 이름. 바꾸지 않는다.</summary>
        public string Id { get; }
        public string Title { get; }
        public string Text { get; }
        public EventTendency Tendency { get; }
        /// <summary>손익 기대값 (카드 몇 장어치, 균형 맞추기용. 도감에서 성향과 함께 보인다).</summary>
        public double Value { get; }
        public IReadOnlyList<EventChoice> Choices { get; }
        /// <summary>선택지 없는 사건형: 선택지는 "확인" 하나.</summary>
        public bool IsIncident { get; }

        public EventSlots Slots { get; private set; } = EventSlots.Any;
        public int MinFloor { get; private set; } = 1;
        /// <summary>0이면 위 끝 없음.</summary>
        public int MaxFloor { get; private set; }
        public int Weight { get; private set; } = 1;
        public HoldScope Scope { get; private set; } = HoldScope.DeckAndSide;
        /// <summary>한 런에 다시 나올 수 있는지.</summary>
        public bool Repeatable { get; private set; } = true;
        /// <summary>이 시작 덱으로 한 런에만 나온다 (null이면 공용).</summary>
        public string DeckId { get; private set; }

        TowerEvent(string id, string title, string text, EventTendency tendency, double value, bool incident, EventChoice[] choices)
        {
            Id = id;
            Title = title;
            Text = text;
            Tendency = tendency;
            Value = value;
            IsIncident = incident;
            Choices = choices;
        }

        public static TowerEvent Choice(string id, string title, string text, EventTendency tendency, double value, params EventChoice[] choices) =>
            new TowerEvent(id, title, text, tendency, value, false, choices);

        /// <summary>선택지 없는 사건: 결과는 바로 정해지고 "확인"만 누른다.</summary>
        public static TowerEvent Incident(string id, string title, string text, EventTendency tendency, double value, string resultText,
            params EventEffect[] effects) =>
            new TowerEvent(id, title, text, tendency, value, true, new[] { new EventChoice("확인", resultText, effects) });

        public TowerEvent At(EventSlots slots) { Slots = slots; return this; }
        public TowerEvent Floors(int min, int max = 0) { MinFloor = min; MaxFloor = max; return this; }
        public TowerEvent WithWeight(int weight) { Weight = weight; return this; }
        public TowerEvent Holding(HoldScope scope) { Scope = scope; return this; }
        public TowerEvent Once() { Repeatable = false; return this; }
        public TowerEvent ForDeck(string deckId) { DeckId = deckId; return this; }

        /// <summary>형식: 사건 / 선택지 / 전투 포함.</summary>
        public string FormName => Choices.Any(c => c.IsBattle) ? "전투 포함" : IsIncident ? "사건" : "선택지";

        /// <summary>도감에 쓰는 선택지 수 ("지나간다"처럼 결과 없는 선택지와 강제 선택지는 빼고 센다).</summary>
        public IEnumerable<int> CodexChoices =>
            Enumerable.Range(0, Choices.Count).Where(i => !Choices[i].IsForced && (Choices[i].Effects.Count > 0 || Choices[i].IsBattle));

        /// <summary>등장 조건 설명 (도감): 자리·층·덱·반복.</summary>
        public string DescribeTags()
        {
            var slots = new List<string>();
            if ((Slots & EventSlots.Early) != 0) slots.Add("1~4층");
            if ((Slots & EventSlots.Sixth) != 0) slots.Add("6층");
            if ((Slots & EventSlots.Late) != 0) slots.Add("7~8층");
            string depth = MaxFloor > 0 ? $"{MinFloor}~{MaxFloor}층" : MinFloor > 1 ? $"{MinFloor}층부터" : "모든 층";
            string deck = DeckId == null ? "" : $", {StarterDecks.Find(DeckId)?.Name ?? DeckId}로 한 런에서만";
            return $"자리 {string.Join("·", slots)} (10층 묶음 안), {depth}{deck}{(Repeatable ? "" : ", 한 런에 한 번")}";
        }

        public static string TendencyName(EventTendency tendency) =>
            tendency == EventTendency.Positive ? "긍정" : tendency == EventTendency.Neutral ? "무성향" : "부정";
    }
}
