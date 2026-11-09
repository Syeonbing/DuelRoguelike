// 스타터 덱 4개(—유희 편—, —카이바 편—, —조이 편—, —페가수스 편—)의 정식 레시피. 설계 문서 "카드풀과 시작 덱"의 첫 버전 시작 덱이다.
//
// 출처 (2026-11-08 확인)
// - 카드 목록: Yugipedia의 한국판 세트 목록 4개
//   https://yugipedia.com/wiki/Set_Card_Lists:Starter_Deck:_Yugi_(OCG-KR)
//   https://yugipedia.com/wiki/Set_Card_Lists:Starter_Deck:_Kaiba_(OCG-KR)
//   https://yugipedia.com/wiki/Set_Card_Lists:Starter_Deck:_Joey_(OCG-KR)
//   https://yugipedia.com/wiki/Set_Card_Lists:Starter_Deck:_Pegasus_(OCG-KR)
//   4덱 모두 북미판(TCG-NA)과 카드·순서가 같고, 50종이 1장씩 들어 있다.
// - 카드 ID: YGOPRODeck API(https://db.ygoprodeck.com/api/v7/cardinfo.php?cardset=Starter%20Deck:%20Yugi 등)와 대조하고
//   BabelCDB cards.cdb(서브모듈 85c0539)에서 확인했다. 일러스트만 다른 ID(alias가 있는 ID)는 기본 ID로 바꿨다
//   (붉은 눈의 흑룡, 사우전드 드래곤, 화염의 검사, 융합, 블랙 매지션, 치료의 신 다이안 켓, 죽은 자의 소생, 희생의 제물).
//   데몬 소환(SDY-K004)은 YGOPRODeck에 북미판 번호가 빠져 있어 BabelCDB에서 카드명으로 찾았다.
// - 주석: 한국판 세트 번호, 공식 한글 카드명(EDOPro 한글 패치 DB, 서브모듈 2e11436), 괄호 안은 BabelCDB 영문명.
// - 에라타가 있는 카드(크리터, 검은 숲의 마녀 등)는 cards.cdb의 현재 버전이다. 에라타 이전 버전은 따로 얻는 카드로 둔다.
// 엑스트라 덱 카드(조이 편의 융합 몬스터 2장)도 같은 목록에 넣는다. 엔진(ocgcore field::add_card)이 덱에 넣을 때 엑스트라 덱으로 옮긴다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Data
{
    /// <summary>덱 레시피: 카드 코드와 장수 목록. 메인·엑스트라 덱을 나누지 않는다.</summary>
    public sealed class DeckRecipe
    {
        public const int MinMainDeck = 40;
        public const int MaxMainDeck = 60;
        public const int MaxExtraDeck = 15;
        public const int MaxCopies = 3;

        const OcgCardType ExtraDeckTypes = OcgCardType.Fusion | OcgCardType.Synchro | OcgCardType.Xyz | OcgCardType.Link;

        /// <summary>엑스트라 덱에 들어가는 카드 종류인지 (융합·싱크로·엑시즈·링크).</summary>
        public static bool IsExtraDeckType(OcgCardType type) => (type & ExtraDeckTypes) != 0;

        /// <summary>코드에서 쓰는 이름 (예: Yugi). 화면 밖 시험의 덱 파라미터와 같다.</summary>
        public string Id { get; }

        /// <summary>표시 이름 (예: 스타터 덱 —유희 편—).</summary>
        public string Name { get; }

        public IReadOnlyList<(uint Code, int Count)> Entries { get; }

        public int CardCount => Entries.Sum(e => e.Count);

        public DeckRecipe(string id, string name, IReadOnlyList<(uint Code, int Count)> entries)
        {
            Id = id;
            Name = name;
            Entries = entries;
        }

        /// <summary>엔진에 넣을 카드 코드 목록 (장수만큼 반복, 레시피 순서).</summary>
        public List<uint> ToCardList() => Entries.SelectMany(e => Enumerable.Repeat(e.Code, e.Count)).ToList();

        /// <summary>
        /// 덱 규칙을 확인한다: 모든 카드가 DB에 기본 카드(alias 없음)로 있는지, 메인 40~60장, 엑스트라 15장 이하, 같은 카드 3장 이하.
        /// 문제가 없으면 빈 목록.
        /// </summary>
        public List<string> Validate(CardDatabase database)
        {
            var problems = new List<string>();
            int main = 0, extra = 0;
            var copies = new Dictionary<uint, int>();
            foreach (var (code, count) in Entries)
            {
                if (count <= 0)
                    problems.Add($"{code}: 장수 {count}");
                if (!database.TryGet(code, out var record))
                {
                    problems.Add($"{code}: 카드 DB에 없음");
                    continue;
                }
                if (record.Data.Alias != 0)
                    problems.Add($"{code} {record.Name}: 기본 카드가 아님 (alias {record.Data.Alias})");
                if (IsExtraDeckType(record.Type))
                    extra += count;
                else
                    main += count;
                copies[code] = copies.TryGetValue(code, out int n) ? n + count : count;
            }
            if (main < MinMainDeck || main > MaxMainDeck)
                problems.Add($"메인 덱 {main}장 ({MinMainDeck}~{MaxMainDeck}장이어야 함)");
            if (extra > MaxExtraDeck)
                problems.Add($"엑스트라 덱 {extra}장 ({MaxExtraDeck}장 이하여야 함)");
            foreach (var pair in copies.Where(p => p.Value > MaxCopies))
                problems.Add($"{pair.Key} {database.GetName(pair.Key)}: {pair.Value}장 ({MaxCopies}장 이하여야 함)");
            return problems;
        }

        public override string ToString() => Name;
    }

    /// <summary>첫 버전의 스타터 덱. 인스펙터에서 고르는 값이라 순서를 바꾸지 않는다.</summary>
    public enum StarterDeckId
    {
        Yugi,
        Kaiba,
        Joey,
        Pegasus,
    }

    public static class StarterDecks
    {
        /// <summary>스타터 덱 —유희 편— (SDY-K001~K050)</summary>
        public static readonly DeckRecipe Yugi = new DeckRecipe("Yugi", "스타터 덱 —유희 편—", new (uint, int)[]
        {
            (15025844, 1),  // SDY-K001 홀리 엘프 (Mystical Elf)
            (41392891, 1),  // SDY-K002 그렘린 (Feral Imp)
            (87796900, 1),  // SDY-K003 요새를 지키는 익룡 (Winged Dragon, Guardian of the Fortress #1)
            (70781052, 1),  // SDY-K004 데몬 소환 (Summoned Skull)
            (32452818, 1),  // SDY-K005 루이즈 (Beaver Warrior)
            (46986414, 1),  // SDY-K006 블랙 매지션 (Dark Magician)
            (6368038, 1),   // SDY-K007 암흑 기사 가이아 (Gaia The Fierce Knight)
            (28279543, 1),  // SDY-K008 커스 오브 드래곤 (Curse of Dragon)
            (91152256, 1),  // SDY-K009 엘프 검사 (Celtic Guardian)
            (40374923, 1),  // SDY-K010 맘모스의 무덤 (Mammoth Graveyard)
            (13429800, 1),  // SDY-K011 백상어 (Great White)
            (90357090, 1),  // SDY-K012 실버 팽 (Silver Fang)
            (13039848, 1),  // SDY-K013 암석 거인 (Giant Soldier of Stone)
            (66672569, 1),  // SDY-K014 드래곤 좀비 (Dragon Zombie)
            (16972957, 1),  // SDY-K015 죽음의 침묵천사 도마 (Doma The Angel of Silence)
            (48365709, 1),  // SDY-K016 암살자 (Ansatsu)
            (36304921, 1),  // SDY-K017 데스 사탄 (Witty Phantom)
            (41218256, 1),  // SDY-K018 킬러의 손톱 (Claw Reacher)
            (47060154, 1),  // SDY-K019 마력의 광대 (Mystic Clown)
            (37120512, 1),  // SDY-K020 어둠의 파신검 (Sword of Dark Destruction)
            (91595718, 1),  // SDY-K021 비술서 (Book of Secret Arts)
            (53129443, 1),  // SDY-K022 블랙홀 (Dark Hole)
            (84257639, 1),  // SDY-K023 치료의 신 다이안 켓 (Dian Keto the Cure Master)
            (93221206, 1),  // SDY-K024 고대 엘프 (Ancient Elf)
            (46474915, 1),  // SDY-K025 매지컬 고스트 (Magical Ghost)
            (66788016, 1),  // SDY-K026 갈라진 대지 (Fissure)
            (4206964, 1),   // SDY-K027 함정 속으로 (Trap Hole)
            (83887306, 1),  // SDY-K028 협공 (Two-Pronged Attack)
            (19159413, 1),  // SDY-K029 마법 제거 (De-Spell)
            (83764718, 1),  // SDY-K030 죽은 자의 소생 (Monster Reborn)
            (17814387, 1),  // SDY-K031 지원군 (Reinforcements)
            (4031928, 1),   // SDY-K032 마음의 변화 (Change of Heart)
            (87557188, 1),  // SDY-K033 엄격한 노마술사 (The Stern Mystic)
            (13945283, 1),  // SDY-K034 환상의 벽 (Wall of Illusion)
            (50930991, 1),  // SDY-K035 마법 검사 네오 (Neo the Magic Swordsman)
            (86325596, 1),  // SDY-K036 마검 남작 (Baron of the Fiend Sword)
            (13723605, 1),  // SDY-K037 식인 보석함 (Man-Eating Treasure Chest)
            (49218300, 1),  // SDY-K038 죽음의 마법사 (Sorcerer of the Doomed)
            (85602018, 1),  // SDY-K039 유언장 (Last Will)
            (12607053, 1),  // SDY-K040 화목의 사자 (Waboku)
            (68005187, 1),  // SDY-K041 크로스 소울 (Soul Exchange)
            (72892473, 1),  // SDY-K042 카드 파괴 (Card Destruction)
            (46461247, 1),  // SDY-K043 함정술사 (Trap Master)
            (50045299, 1),  // SDY-K044 드래곤족 봉인항아리 (Dragon Capture Jar)
            (59197169, 1),  // SDY-K045 어둠 (Yami)
            (54652250, 1),  // SDY-K046 식인 곤충 (Man-Eater Bug)
            (77622396, 1),  // SDY-K047 심술꾸러기의 저주 (Reverse Trap)
            (51482758, 1),  // SDY-K048 함정 해제 (Remove Trap)
            (44209392, 1),  // SDY-K049 성벽 (Castle Walls)
            (80604091, 1),  // SDY-K050 희생의 제물 (Ultimate Offering)
        });

        /// <summary>스타터 덱 —카이바 편— (SDK-K001~K050)</summary>
        public static readonly DeckRecipe Kaiba = new DeckRecipe("Kaiba", "스타터 덱 —카이바 편—", new (uint, int)[]
        {
            (89631139, 1),  // SDK-K001 푸른 눈의 백룡 (Blue-Eyes White Dragon)
            (76184692, 1),  // SDK-K002 외눈 거인 (Hitotsu-Me Giant)
            (15303296, 1),  // SDK-K003 가고일 (Ryu-Kishin)
            (6285791, 1),   // SDK-K004 사악한 웜 괴물 (The Wicked Worm Beast)
            (5053103, 1),   // SDK-K005 미노타우로스 (Battle Ox)
            (67724379, 1),  // SDK-K006 데블 드래곤 (Koumori Dragon)
            (30113682, 1),  // SDK-K007 심판자 (Judge Man)
            (91939608, 1),  // SDK-K008 성스러운 인형 (Rogue Doll)
            (1184620, 1),   // SDK-K009 마물 사냥꾼 (Kojikocy)
            (1784619, 1),   // SDK-K010 와일드 랩터 (Uraby)
            (31122090, 1),  // SDK-K011 역전의 여신 (Gyakutenno Megami)
            (68516705, 1),  // SDK-K012 켄타우로스 (Mystic Horseman)
            (63308047, 1),  // SDK-K013 마인 테라 (Terra the Terrible)
            (89494469, 1),  // SDK-K014 악몽의 암흑 마신 (Dark Titan of Terror)
            (41949033, 1),  // SDK-K015 어둠의 암살자 (Dark Assailant)
            (75499502, 1),  // SDK-K016 마스터 & 엑스퍼트 (Master & Expert)
            (97360116, 1),  // SDK-K017 악의 무명전사 (Unknown Warrior of Fiend)
            (47060154, 1),  // SDK-K018 마력의 광대 (Mystic Clown)
            (45121025, 1),  // SDK-K019 암흑 귀왕 (Ogre of the Black Shadow)
            (4614116, 1),   // SDK-K020 어둠의 에너지 (Dark Energy)
            (98374133, 1),  // SDK-K021 각성의 빛 (Invigoration)
            (53129443, 1),  // SDK-K022 블랙홀 (Dark Hole)
            (19523799, 1),  // SDK-K023 대화재 (Ookazi)
            (24611934, 1),  // SDK-K024 마력을 얻은 가고일 (Ryu-Kishin Powered)
            (50005633, 1),  // SDK-K025 스워드 스토커 (Swordstalker)
            (97590747, 1),  // SDK-K026 램프의 요정 라 진 (La Jinn the Mystical Genie of the Lamp)
            (26378150, 1),  // SDK-K027 루드 카이저 (Rude Kaiser)
            (73481154, 1),  // SDK-K028 파괴자 골렘 (Destroyer Golem)
            (10202894, 1),  // SDK-K029 스카이 헌터 (Skull Red Bird)
            (81057959, 1),  // SDK-K030 드래곤 전사 (D. Human)
            (21263083, 1),  // SDK-K031 창백한 야수 (Pale Beast)
            (66788016, 1),  // SDK-K032 갈라진 대지 (Fissure)
            (4206964, 1),   // SDK-K033 함정 속으로 (Trap Hole)
            (83887306, 1),  // SDK-K034 협공 (Two-Pronged Attack)
            (19159413, 1),  // SDK-K035 마법 제거 (De-Spell)
            (83764718, 1),  // SDK-K036 죽은 자의 소생 (Monster Reborn)
            (81820689, 1),  // SDK-K037 어설픈 스파이 (The Inexperienced Spy)
            (17814387, 1),  // SDK-K038 지원군 (Reinforcements)
            (17092736, 1),  // SDK-K039 고대의 망원경 (Ancient Telescope)
            (24068492, 1),  // SDK-K040 자업자득 (Just Desserts)
            (17985575, 1),  // SDK-K041 드래곤의 제왕 (Lord of D.)
            (43973174, 1),  // SDK-K042 드래곤을 부르는 피리 (The Flute of Summoning Dragon)
            (54098121, 1),  // SDK-K043 의문의 인형술사 (Mysterious Puppeteer)
            (46461247, 1),  // SDK-K044 함정술사 (Trap Master)
            (86318356, 1),  // SDK-K045 초원 (Sogen)
            (7089711, 1),   // SDK-K046 하늘하늘 (Hane-Hane)
            (77622396, 1),  // SDK-K047 심술꾸러기의 저주 (Reverse Trap)
            (51482758, 1),  // SDK-K048 함정 해제 (Remove Trap)
            (44209392, 1),  // SDK-K049 성벽 (Castle Walls)
            (80604091, 1),  // SDK-K050 희생의 제물 (Ultimate Offering)
        });

        /// <summary>스타터 덱 —조이 편— (SDJ-KR001~KR050). 메인 48장 + 엑스트라 2장.</summary>
        public static readonly DeckRecipe Joey = new DeckRecipe("Joey", "스타터 덱 —조이 편—", new (uint, int)[]
        {
            (74677422, 1),  // SDJ-KR001 붉은 눈의 흑룡 (Red-Eyes Black Dragon)
            (3573512, 1),   // SDJ-KR002 랜드스타의 검사 (Swordsman of Landstar)
            (88819587, 1),  // SDJ-KR003 베이비 드래곤 (Baby Dragon)
            (80770678, 1),  // SDJ-KR004 하프의 정령 (Spirit of the Harp)
            (4042268, 1),   // SDJ-KR005 아일랜드 터틀 (Island Turtle)
            (34460851, 1),  // SDJ-KR006 화염의 조종자 (Flame Manipulator)
            (44287299, 1),  // SDJ-KR007 전설의 검객 마사키 (Masaki the Legendary Swordsman)
            (23771716, 1),  // SDJ-KR008 레인보우 휘시 (7 Colored Fish)
            (15480588, 1),  // SDJ-KR009 아머 리저드 (Armored Lizard)
            (5388481, 1),   // SDJ-KR010 버닝 솔저 (Darkfire Soldier #1)
            (30532390, 1),  // SDJ-KR011 하드맨 (Sky Scout)
            (423705, 1),    // SDJ-KR012 철의 기사 기어프리드 (Gearfried the Iron Knight)
            (23289281, 1),  // SDJ-KR013 당수 (Karate Man)
            (7489323, 1),   // SDJ-KR014 미리스 레디언트 (Milus Radiant)
            (71625222, 1),  // SDJ-KR015 시간의 마술사 (Time Wizard)
            (93013676, 1),  // SDJ-KR016 마하 바일로 (Maha Vailo)
            (31560081, 1),  // SDJ-KR017 성스러운 마술사 (Magician of Faith)
            (16768387, 1),  // SDJ-KR018 빅 아이 (Big Eye)
            (26202165, 1),  // SDJ-KR019 크리터 (Sangan)
            (51371017, 1),  // SDJ-KR020 검의 여왕 (Princess of Tsurugi)
            (15150365, 1),  // SDJ-KR021 흰 도둑 (White Magical Hat)
            (93920745, 1),  // SDJ-KR022 펭귄 솔저 (Penguin Soldier)
            (41462083, 1),  // SDJ-KR023 사우전드 드래곤 (Thousand Dragon)
            (45231177, 1),  // SDJ-KR024 화염의 검사 (Flame Swordsman)
            (99597615, 1),  // SDJ-KR025 악마의 키스 (Malevolent Nuzzler)
            (53129443, 1),  // SDJ-KR026 블랙홀 (Dark Hole)
            (84257639, 1),  // SDJ-KR027 치료의 신 다이안 켓 (Dian Keto the Cure Master)
            (66788016, 1),  // SDJ-KR028 갈라진 대지 (Fissure)
            (19159413, 1),  // SDJ-KR029 마법 제거 (De-Spell)
            (4031928, 1),   // SDJ-KR030 마음의 변화 (Change of Heart)
            (25880422, 1),  // SDJ-KR031 공격봉인 (Block Attack)
            (42703248, 1),  // SDJ-KR032 허리케인 (Giant Trunade)
            (16430187, 1),  // SDJ-KR033 듬직한 수호자 (The Reliable Guardian)
            (51482758, 1),  // SDJ-KR034 함정 해제 (Remove Trap)
            (83764718, 1),  // SDJ-KR035 죽은 자의 소생 (Monster Reborn)
            (24094653, 1),  // SDJ-KR036 융합 (Polymerization)
            (50913601, 1),  // SDJ-KR037 산 (Mountain)
            (1435851, 1),   // SDJ-KR038 드래곤의 보물 (Dragon Treasure)
            (95051344, 1),  // SDJ-KR039 성불 (Eternal Rest)
            (52097679, 1),  // SDJ-KR040 오른손엔 방패 왼손엔 검 (Shield & Sword)
            (73915051, 1),  // SDJ-KR041 희생양 (Scapegoat)
            (24068492, 1),  // SDJ-KR042 자업자득 (Just Desserts)
            (4206964, 1),   // SDJ-KR043 함정 속으로 (Trap Hole)
            (17814387, 1),  // SDJ-KR044 지원군 (Reinforcements)
            (44209392, 1),  // SDJ-KR045 성벽 (Castle Walls)
            (12607053, 1),  // SDJ-KR046 화목의 사자 (Waboku)
            (80604091, 1),  // SDJ-KR047 희생의 제물 (Ultimate Offering)
            (3819470, 1),   // SDJ-KR048 도적의 7가지 도구 (Seven Tools of the Bandit)
            (3027001, 1),   // SDJ-KR049 가짜 함정 (Fake Trap)
            (77622396, 1),  // SDJ-KR050 심술꾸러기의 저주 (Reverse Trap)
        });

        /// <summary>스타터 덱 —페가수스 편— (SDP-KR001~KR050)</summary>
        public static readonly DeckRecipe Pegasus = new DeckRecipe("Pegasus", "스타터 덱 —페가수스 편—", new (uint, int)[]
        {
            (64631466, 1),  // SDP-KR001 새크리파이스 (Relinquished)
            (65570596, 1),  // SDP-KR002 활을 쏘는 인어 (Red Archery Girl)
            (2964201, 1),   // SDP-KR003 드래곤 에그 (Ryu-Ran)
            (28546905, 1),  // SDP-KR004 환술사 노페이스 (Illusionist Faceless Mage)
            (91939608, 1),  // SDP-KR005 성스러운 인형 (Rogue Doll)
            (1784619, 1),   // SDP-KR006 와일드 랩터 (Uraby)
            (13039848, 1),  // SDP-KR007 암석 거인 (Giant Soldier of Stone)
            (85639257, 1),  // SDP-KR008 물의 조종자 (Aqua Madoor)
            (59383041, 1),  // SDP-KR009 툰 앨리게이터 (Toon Alligator)
            (7089711, 1),   // SDP-KR010 하늘하늘 (Hane-Hane)
            (57617178, 1),  // SDP-KR011 소닉 버드 (Sonic Bird)
            (90020065, 1),  // SDP-KR012 시한폭탄 (Jigen Bakudan)
            (28933734, 1),  // SDP-KR013 암흑의 가면 (Mask of Darkness)
            (78010363, 1),  // SDP-KR014 검은 숲의 마녀 (Witch of the Black Forest)
            (54652250, 1),  // SDP-KR015 식인 곤충 (Man-Eater Bug)
            (46657337, 1),  // SDP-KR016 무카무카 (Muka Muka)
            (13215230, 1),  // SDP-KR017 드림 삐에로 (Dream Clown)
            (9076207, 1),   // SDP-KR018 푸른 첩자 (Armed Ninja)
            (81863068, 1),  // SDP-KR019 악마의 정찰자 (Hiro's Shadow Scout)
            (53183600, 1),  // SDP-KR020 푸른 눈의 툰 드래곤 (Blue-Eyes Toon Dragon)
            (91842653, 1),  // SDP-KR021 툰 데몬 (Toon Summoned Skull)
            (38369349, 1),  // SDP-KR022 툰 드래곤 에그 (Manga Ryu-Ran)
            (65458948, 1),  // SDP-KR023 툰 인어 (Toon Mermaid)
            (15259703, 1),  // SDP-KR024 툰 월드 (Toon World)
            (65169794, 1),  // SDP-KR025 검은 펜던트 (Black Pendant)
            (53129443, 1),  // SDP-KR026 블랙홀 (Dark Hole)
            (84257639, 1),  // SDP-KR027 치료의 신 다이안 켓 (Dian Keto the Cure Master)
            (66788016, 1),  // SDP-KR028 갈라진 대지 (Fissure)
            (19159413, 1),  // SDP-KR029 마법 제거 (De-Spell)
            (4031928, 1),   // SDP-KR030 마음의 변화 (Change of Heart)
            (63102017, 1),  // SDP-KR031 수비봉인 (Stop Defense)
            (5318639, 1),   // SDP-KR032 싸이크론 (Mystical Space Typhoon)
            (70046172, 1),  // SDP-KR033 돌진 (Rush Recklessly)
            (51482758, 1),  // SDP-KR034 함정 해제 (Remove Trap)
            (83764718, 1),  // SDP-KR035 죽은 자의 소생 (Monster Reborn)
            (5758500, 1),   // SDP-KR036 영혼의 해방 (Soul Release)
            (59197169, 1),  // SDP-KR037 어둠 (Yami)
            (41426869, 1),  // SDP-KR038 환술의 의식 (Black Illusion Ritual)
            (20436034, 1),  // SDP-KR039 자력의 반지 (Ring of Magnetism)
            (79571449, 1),  // SDP-KR040 천사의 자비 (Graceful Charity)
            (4206964, 1),   // SDP-KR041 함정 속으로 (Trap Hole)
            (17814387, 1),  // SDP-KR042 지원군 (Reinforcements)
            (44209392, 1),  // SDP-KR043 성벽 (Castle Walls)
            (12607053, 1),  // SDP-KR044 화목의 사자 (Waboku)
            (3819470, 1),   // SDP-KR045 도적의 7가지 도구 (Seven Tools of the Bandit)
            (80604091, 1),  // SDP-KR046 희생의 제물 (Ultimate Offering)
            (88279736, 1),  // SDP-KR047 로빈 고블린 (Robbin' Goblin)
            (77414722, 1),  // SDP-KR048 매직 재머 (Magic Jammer)
            (96355986, 1),  // SDP-KR049 홀리 자베린 (Enchanted Javelin)
            (55608151, 1),  // SDP-KR050 그리폰의 날개 (Gryphon Wing)
        });

        /// <summary>StarterDeckId 순서.</summary>
        public static readonly IReadOnlyList<DeckRecipe> All = new[] { Yugi, Kaiba, Joey, Pegasus };

        public static DeckRecipe Get(StarterDeckId id) => All[(int)id];

        /// <summary>Id(Yugi 등, 대소문자 무시)로 찾는다. 없으면 null.</summary>
        public static DeckRecipe Find(string id) =>
            All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}