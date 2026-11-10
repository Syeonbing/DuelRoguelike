// 탑 이벤트 목록 (견본). 성향마다 몇 개씩, 이벤트 시스템의 형식(사건·선택지·조건·잠긴 선택지·강제 선택·이벤트 전투·
// 덱 전용·한 번만·이번 런에서 잃음)을 한 번씩은 쓰도록 만든 것이다. 글과 수치는 모두 임시다.
// 이벤트를 고치거나 더할 때는 이 파일만 고친다. Id는 저장 파일과 도감에 남으므로 바꾸지 않는다 (지운 이벤트는 도감에서 빠진다).
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Run
{
    public static class TowerEventList
    {
        // 종족·속성 값 (카드 DB의 race·attribute 비트)
        const ulong Dragon = 0x2000;
        const uint Light = 0x10;

        static bool IsMonster(CardRecord r) => (r.Type & OcgCardType.Monster) != 0;
        static bool IsSpell(CardRecord r) => (r.Type & OcgCardType.Spell) != 0;
        static bool IsTrap(CardRecord r) => (r.Type & OcgCardType.Trap) != 0;

        static readonly CardCondition DragonMonsters2 = CardCondition.Spec("드래곤족 몬스터", r => IsMonster(r) && (r.Data.Race & Dragon) != 0, 2);
        static readonly CardCondition LightMonsters3 = CardCondition.Spec("빛 속성 몬스터", r => IsMonster(r) && r.Data.Attribute == Light, 3);
        static readonly CardCondition Spells5 = CardCondition.Spec("마법 카드", IsSpell, 5);
        static readonly CardCondition Traps5 = CardCondition.Spec("함정 카드", IsTrap, 5);
        static readonly CardCondition Traps6 = CardCondition.Spec("함정 카드", IsTrap, 6);
        static readonly CardCondition AnySpell = CardCondition.Spec("마법 카드", IsSpell);
        static readonly CardCondition AnyTrap = CardCondition.Spec("함정 카드", IsTrap);

        /// <summary>아무 일도 없는 이벤트 (꽝). 이벤트를 고를 수 없을 때도 이것이 나온다.</summary>
        public static readonly TowerEvent QuietFloor = TowerEvent.Incident("quiet-floor", "고요한 층",
            "아무도 없는 조용한 층이다. 둘러보았지만 아무 일도 일어나지 않았다.", EventTendency.Neutral, 0,
            "조용히 다음 층으로 올라갔다.");

        public static readonly IReadOnlyList<TowerEvent> All = new[]
        {
            // ---------------- 긍정 ----------------
            TowerEvent.Incident("lucky-coin", "반짝이는 동전",
                "계단 구석에서 무언가 반짝인다. 누군가 떨어뜨린 동전 꾸러미다.", EventTendency.Positive, 0.5,
                "동전을 칩으로 바꿨다.", EventEffect.Chips(40)),

            TowerEvent.Choice("dragon-shrine", "용의 사당",
                "낡은 사당 안쪽에 커다란 용 석상이 잠들어 있다. 제단에는 향이 꺼져 있다.", EventTendency.Positive, 1.5,
                new EventChoice("용의 힘을 빌려 기도한다", "석상의 눈이 빛나더니 봉인 하나가 풀렸다.", EventEffect.Unseal(60)).If(DragonMonsters2),
                new EventChoice("시주한다 (칩 30)", "향을 피우고 잠시 쉬었다. 몸이 가벼워졌다.", EventEffect.Chips(-30), EventEffect.Lp(2000)).IfChips(30),
                new EventChoice("지나간다", "사당을 뒤로하고 계단을 올랐다."))
                .At(EventSlots.Sixth | EventSlots.Late),

            TowerEvent.Choice("old-duelist", "은퇴한 듀얼리스트",
                "벤치에 앉은 노인이 낡은 듀얼 디스크를 닦고 있다. \"젊은이, 한 판 어떤가?\"", EventTendency.Positive, 1.2,
                new EventChoice("한 판 겨뤄 본다", "\"좋은 승부였네!\" 노인이 아끼던 카드를 건네주었다.",
                    EventEffect.Gain(71625222, "시간의 마술사"), EventEffect.Chips(50)).Battle("Joey"),
                new EventChoice("이야기만 듣는다", "옛 대회 이야기를 들으며 푹 쉬었다.", EventEffect.Lp(1000)))
                .At(EventSlots.Sixth).Once(),

            TowerEvent.Choice("white-dragon-legend", "백룡의 전설",
                "벽화 속 하얀 용이 이쪽을 내려다본다. 벽화 아래에 \"푸른 눈을 가진 자만 지나라\"라고 적혀 있다.", EventTendency.Positive, 2,
                new EventChoice("「푸른 눈의 백룡」을 꺼내 보인다", "백룡의 포효가 탑을 울렸다. 벽화 뒤에서 보물이 나왔다.",
                    EventEffect.Gold(5), EventEffect.Chips(80)).If(CardCondition.Named(89631139, "푸른 눈의 백룡")),
                new EventChoice("지나간다", "벽화를 한 번 더 올려다보고 지나갔다."))
                .At(EventSlots.Sixth).ForDeck("Kaiba").Once().Holding(HoldScope.Deck),

            // ---------------- 무성향 ----------------
            QuietFloor,

            TowerEvent.Choice("crossroads", "갈림길",
                "길이 둘로 갈라진다. 왼쪽은 돌투성이 오르막, 오른쪽은 잘 닦인 길이다.", EventTendency.Neutral, 0,
                new EventChoice("험한 길로 간다", "돌부리에 걸려 넘어졌지만 길가에서 칩 주머니를 주웠다.", EventEffect.Chips(100), EventEffect.Seal(1)),
                new EventChoice("편한 길로 간다", "아무 일 없이 다음 층에 닿았다."))
                .At(EventSlots.Early | EventSlots.Late),

            TowerEvent.Choice("barter", "물물교환",
                "보따리 상인이 손을 비빈다. \"카드 한 장만 주면 칩을 넉넉히 쳐 드리지요.\"", EventTendency.Neutral, 0.3,
                new EventChoice("마법 카드 1장을 내놓는다", "상인이 카드를 받고 칩을 듬뿍 건넸다.", EventEffect.Lose(AnySpell), EventEffect.Chips(120)).If(Spells5),
                new EventChoice("함정 카드 1장을 내놓는다", "상인이 카드를 받고 칩을 듬뿍 건넸다.", EventEffect.Lose(AnyTrap), EventEffect.Chips(120)).If(Traps5),
                new EventChoice("거절한다", "상인은 아쉬운 얼굴로 보따리를 쌌다."))
                .Holding(HoldScope.Deck),

            // ---------------- 부정 ----------------
            TowerEvent.Incident("rockfall", "낙석",
                "머리 위에서 우르르 소리가 난다. 돌이 쏟아진다!", EventTendency.Negative, -0.5,
                "피하지 못하고 다쳤다.", EventEffect.Lp(-1500))
                .At(EventSlots.Early | EventSlots.Late),

            TowerEvent.Incident("pickpocket", "소매치기",
                "누군가 어깨를 툭 치고 지나갔다. 주머니가 가볍다.", EventTendency.Negative, -0.5,
                "칩을 도둑맞았다.", EventEffect.Chips(-50)),

            TowerEvent.Choice("cursed-mirror", "저주받은 거울",
                "커다란 거울 속 내 그림자가 혼자 웃고 있다. 그림자가 카드를 노린다.", EventTendency.Negative, -1,
                new EventChoice("빛으로 비춘다", "그림자가 빛에 녹아 사라지고, 거울 뒤에 칩이 떨어져 있었다.", EventEffect.Chips(30)).If(LightMonsters3),
                new EventChoice("거울을 깬다", "깨진 조각이 카드 한 장을 덮쳤다.", EventEffect.Seal(1)))
                .At(EventSlots.Sixth | EventSlots.Late).Holding(HoldScope.Deck),

            TowerEvent.Choice("toll-gate", "통행세",
                "험상궂은 문지기가 창을 가로막는다. \"지나가려면 통행세를 내라.\"", EventTendency.Negative, -1,
                new EventChoice("통행세를 낸다 (칩 60)", "칩을 내고 지나갔다.", EventEffect.Chips(-60)).IfChips(60),
                new EventChoice("함정으로 문을 연다", "함정이 문지기의 발을 묶었다. 그 틈에 지나갔다.").If(Traps6),
                new EventChoice("억지로 지나간다", "문지기에게 붙잡혀 혼쭐이 났다.", EventEffect.Seal(2)).Forced())
                .At(EventSlots.Early | EventSlots.Sixth).Holding(HoldScope.Deck),

            TowerEvent.Choice("ambush", "매복",
                "그림자 속에서 듀얼리스트가 길을 막아선다. \"여기서부터는 못 지나간다!\"", EventTendency.Negative, -0.5,
                new EventChoice("맞서 싸운다", "상대는 분한 얼굴로 길을 비켜 주었다.", EventEffect.Chips(60)).Battle("Kaiba"))
                .At(EventSlots.Late),
        };

        public static TowerEvent Find(string id) => All.FirstOrDefault(e => e.Id == id);
    }
}
