// 스트럭처 덱 3개(열풍의 패왕, 기계의 반란, 암흑의 주술)의 레시피. 원래 구성은 상대(AI) 덱이고,
// 몇 장을 바꾼 시작 덱 판은 플레이어가 처음에 고르는 공통 시작 덱이다 (2026-11-24 사용자 결정).
//
// 출처 (2026-11-24 확인)
// - 열풍의 패왕(SD8-KR), 기계의 반란(SD10-KR): 공식 한국어 카드 DB(Yu-Gi-Oh! Neuron 카드 데이터베이스, request_locale=ko)의 상품별 목록과
//   Yugipedia 한국판 세트 목록 (Set_Card_Lists:Structure_Deck:_Lord_of_the_Storm_(OCG-KR), ..._Machine_Re-Volt_(OCG-KR)).
// - 암흑의 주술(SD12-JP): 한국에 정식 발매되지 않은 일본판 「Structure Deck: Curse of Darkness」의 구성 (Yugipedia OCG-JP 세트 목록).
//   리메이크판 「암흑의 주박」(SR06)이 아니다. 한글 카드명은 카드마다 공식 한국어 카드 DB의 이름이다.
// - 카드 ID는 BabelCDB cards.cdb의 기본 ID(alias 없음)다. 모든 카드에 CardScripts 스크립트가 있다.
// - 에라타가 있는 카드(죽음의 덱 파괴 바이러스 등)는 cards.cdb의 현재 버전이다.
// 주석: 세트 번호, 공식 한글 카드명, 괄호 안은 BabelCDB 영문명. 장수가 2장 이상이면 레시피에 적은 대로다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuelRoguelike.Engine.Data
{
    public static class StructureDecks
    {
        /// <summary>스트럭처 덱 —열풍의 패왕— (SD8-KR001~KR036, 40장)</summary>
        public static readonly DeckRecipe LordOfTheStorm = new DeckRecipe("LordOfTheStorm", "스트럭처 덱 —열풍의 패왕—", new (uint, int)[]
        {
            (14989021, 1),  // SD8-KR001 신조 시무르그 (Simorgh, Bird of Divinity)
            (40384720, 1),  // SD8-KR002 소닉 슈터 (Sonic Shooter)
            (84696266, 1),  // SD8-KR003 음속 덕 (Sonic Duck)
            (34100324, 1),  // SD8-KR004 해피 걸 (Harpie Girl)
            (78636495, 1),  // SD8-KR005 뉴트 (Slate Warrior)
            (84834865, 2),  // SD8-KR006 드래곤 플라이 (Flying Kamakiri #1)
            (12206212, 2),  // SD8-KR007 해피 레이디 세자매 (Harpie Lady Sisters)
            (28470714, 1),  // SD8-KR008 브레이드 플라이 (Bladefly)
            (45547649, 1),  // SD8-KR009 버드 페이스 (Birdface)
            (73001017, 1),  // SD8-KR010 실피드 (Silpheed)
            (82005435, 1),  // SD8-KR011 여첩자 야에 (Lady Ninja Yae)
            (28143906, 1),  // SD8-KR012 안개 계곡의 대괴조 (Roc from the Valley of Haze)
            (91932350, 1),  // SD8-KR013 해피 레이디1 (Harpie Lady 1). 해피 레이디 1·2·3은 카드명 취급(alias 76812113)이 같아 합쳐 3장까지다
            (27927359, 1),  // SD8-KR014 해피 레이디2 (Harpie Lady 2)
            (54415063, 1),  // SD8-KR015 해피 레이디3 (Harpie Lady 3)
            (81896370, 1),  // SD8-KR016 질풍조인 죠 (Swift Birdman Joe)
            (6924874, 1),   // SD8-KR017 해피의 애완 베이비 드래곤 (Harpie's Pet Baby Dragon)
            (72892473, 1),  // SD8-KR018 카드 파괴 (Card Destruction)
            (5318639, 1),   // SD8-KR019 싸이크론 (Mystical Space Typhoon)
            (71044499, 1),  // SD8-KR020 말살의 사도 (Nobleman of Crossout)
            (90219263, 2),  // SD8-KR021 만화경 -화려한 분신- (Elegant Egotist)
            (19613556, 1),  // SD8-KR022 태풍 (Heavy Storm)
            (22589918, 1),  // SD8-KR023 리로드 (Reload)
            (75782277, 2),  // SD8-KR024 해피의 사냥터 (Harpies' Hunting Ground)
            (12181376, 1),  // SD8-KR025 트라이앵글 X 스파크 (Triangle Ecstasy Spark)
            (69162969, 1),  // SD8-KR026 라이트닝 보텍스 (Lightning Vortex)
            (77778835, 1),  // SD8-KR027 히스테릭 파티 (Hysteric Party)
            (95132338, 1),  // SD8-KR028 아쿠아의 합창 (Aqua Chorus)
            (60082869, 1),  // SD8-KR029 더스트 토네이도 (Dust Tornado)
            (97077563, 1),  // SD8-KR030 리빙 데드가 부르는 소리 (Call of the Haunted)
            (77414722, 1),  // SD8-KR031 매직 재머 (Magic Jammer)
            (1804528, 1),   // SD8-KR032 저주받은 관 (Dark Coffin)
            (37576645, 1),  // SD8-KR033 무모한 욕심쟁이 (Reckless Greed)
            (56120475, 1),  // SD8-KR034 작렬 장갑 (Sakuretsu Armor)
            (70861343, 1),  // SD8-KR035 인법 변신술 (Ninjitsu Art of Transformation)
            (53567095, 1),  // SD8-KR036 갓버드 어택 (Icarus Attack)
        });

        /// <summary>스트럭처 덱 —기계의 반란— (SD10-KR001~KR037, 40장)</summary>
        public static readonly DeckRecipe MachineReVolt = new DeckRecipe("MachineReVolt", "스트럭처 덱 —기계의 반란—", new (uint, int)[]
        {
            (50933533, 1),  // SD10-KR001 앤틱 기어 가젤 드래곤 (Ancient Gear Gadjiltron Dragon)
            (86321248, 1),  // SD10-KR002 앤틱 기어 가젤 키메라 (Ancient Gear Gadjiltron Chimera)
            (1953925, 1),   // SD10-KR003 앤틱 기어 엔지니어 (Ancient Gear Engineer)
            (13316346, 1),  // SD10-KR004 기동병사 데드 리볼버 (Boot-Up Soldier - Dread Dynamo)
            (7359741, 1),   // SD10-KR005 메카 헌터 (Mechanicalchaser)
            (41172955, 1),  // SD10-KR006 그린 가제트 (Green Gadget)
            (86445415, 1),  // SD10-KR007 레드 가제트 (Red Gadget)
            (13839120, 1),  // SD10-KR008 옐로 가제트 (Yellow Gadget)
            (11384280, 1),  // SD10-KR009 캐논 솔저 (Cannon Soldier)
            (30190809, 1),  // SD10-KR010 기동요새의 기어 골렘 (Gear Golem the Moving Fortress)
            (23265594, 2),  // SD10-KR011 강화지원메카 헤비웨폰 (Heavy Mech Support Platform)
            (83104731, 1),  // SD10-KR012 앤틱 기어 골렘 (Ancient Gear Golem)
            (10509340, 1),  // SD10-KR013 앤틱 기어 비스트 (Ancient Gear Beast)
            (56094445, 1),  // SD10-KR014 앤틱 기어 솔저 (Ancient Gear Soldier)
            (31557782, 2),  // SD10-KR015 앤틱 기어 (Ancient Gear)
            (80045583, 1),  // SD10-KR016 앤틱 기어 캐논 (Ancient Gear Cannon)
            (59811955, 1),  // SD10-KR017 앤틱 기어 개라지 (Ancient Gear Workshop)
            (37457534, 1),  // SD10-KR018 앤틱 기어 탱크 (Ancient Gear Tank)
            (4446672, 1),   // SD10-KR019 앤틱 기어 폭탄 (Ancient Gear Explosive)
            (40830387, 1),  // SD10-KR020 앤틱 기어 핸드 (Ancient Gear Fist)
            (30435145, 1),  // SD10-KR021 앤틱 기어 팩토리 (Ancient Gear Factory)
            (67829249, 1),  // SD10-KR022 앤틱 기어 드릴 (Ancient Gear Drill)
            (92001300, 1),  // SD10-KR023 앤틱 기어 캐슬 (Ancient Gear Castle)
            (5318639, 1),   // SD10-KR024 싸이크론 (Mystical Space Typhoon)
            (23171610, 1),  // SD10-KR025 리미터 해제 (Limiter Removal)
            (19613556, 1),  // SD10-KR026 태풍 (Heavy Storm)
            (98045062, 1),  // SD10-KR027 에너미 컨트롤러 (Enemy Controller)
            (10035717, 1),  // SD10-KR028 웨폰 체인지 (Weapon Change)
            (63995093, 1),  // SD10-KR029 기계 복제술 (Machine Duplication)
            (67169062, 1),  // SD10-KR030 탐욕의 항아리 (Pot of Avarice)
            (13955608, 1),  // SD10-KR031 기동요새 스트롱 홀드 (Stronghold the Moving Fortress)
            (80604091, 1),  // SD10-KR032 희생의 제물 (Ultimate Offering)
            (56120475, 2),  // SD10-KR033 작렬 장갑 (Sakuretsu Armor)
            (18190572, 1),  // SD10-KR034 마이크로 광선 (Micro Ray)
            (12503902, 1),  // SD10-KR035 레어메탈화 마법반사장갑 (Rare Metalmorph)
            (74458486, 1),  // SD10-KR036 원호사격 (Covering Fire)
            (91597389, 1),  // SD10-KR037 겟 라이드! (Roll Out!)
        });

        /// <summary>스트럭처 덱 —암흑의 주술— (SD12-JP001~JP032, 40장. 일본판만 있다)</summary>
        public static readonly DeckRecipe CurseOfDarkness = new DeckRecipe("CurseOfDarkness", "스트럭처 덱 —암흑의 주술—", new (uint, int)[]
        {
            (29424328, 1),  // SD12-JP001 마왕 디아블로스 (Diabolos, King of the Abyss)
            (55818463, 1),  // SD12-JP002 사왕 리치로드 (Lich Lord, King of the Underworld)
            (82213171, 1),  // SD12-JP003 암왕 프로메티스 (Prometheus, King of the Shadows)
            (28601770, 1),  // SD12-JP004 미스트 데몬 (Mist Archfiend)
            (55696885, 1),  // SD12-JP005 역병 늑대 (Plague Wolf)
            (63665875, 1),  // SD12-JP006 고블린 좀비 (Goblin Zombie)
            (84914462, 2),  // SD12-JP007 액스 드라고뉴트 (Axe Dragonute)
            (83011277, 1),  // SD12-JP008 킬러 토마토 (Mystic Tomato)
            (28933734, 1),  // SD12-JP009 암흑의 가면 (Mask of Darkness)
            (23205979, 1),  // SD12-JP010 영혼을 깎는 사령 (Spirit Reaper)
            (73698349, 3),  // SD12-JP011 자이언트 오크 (Giant Orc)
            (3510565, 2),   // SD12-JP012 스텔스 버드 (Stealth Bird)
            (72657739, 2),  // SD12-JP013 원한의 킬러돌 (Malice Doll of Demise)
            (81191584, 1),  // SD12-JP014 악몽 재발 (Recurring Nightmare)
            (17589298, 1),  // SD12-JP015 새크리파이스 스워드 (Sword of Dark Rites)
            (30683373, 1),  // SD12-JP016 실드 크러시 (Shield Crush)
            (58775978, 1),  // SD12-JP017 악몽의 쇠창살 (Nightmare's Steelcage)
            (5318639, 1),   // SD12-JP018 싸이크론 (Mystical Space Typhoon)
            (42703248, 1),  // SD12-JP019 허리케인 (Giant Trunade)
            (22046459, 1),  // SD12-JP020 거대화 (Megamorph)
            (71044499, 1),  // SD12-JP021 말살의 사도 (Nobleman of Crossout)
            (19613556, 1),  // SD12-JP022 태풍 (Heavy Storm)
            (97342942, 2),  // SD12-JP023 엑토플라즈머 (Ectoplasmer)
            (67169062, 1),  // SD12-JP024 탐욕의 항아리 (Pot of Avarice)
            (85852291, 1),  // SD12-JP025 요술망치 (Magical Mallet)
            (54974237, 1),  // SD12-JP026 어둠의 덱 파괴 바이러스 (Eradicator Epidemic Virus)
            (57728570, 1),  // SD12-JP027 죽음의 덱 파괴 바이러스 (Crush Card Virus)
            (35027493, 2),  // SD12-JP028 마의 덱 파괴 바이러스 (Deck Devastation Virus)
            (62279055, 1),  // SD12-JP029 매직 실린더 (Magic Cylinder)
            (24068492, 1),  // SD12-JP030 자업자득 (Just Desserts)
            (20522190, 1),  // SD12-JP031 사악한 방어막 거울의 힘 (Dark Mirror Force)
            (67095270, 2),  // SD12-JP032 디멘션 월 (Dimension Wall)
        });

        // 시작 덱 판에서 빼는 카드 (등급표에서 UR·FR)와 대신 넣는 카드 (SR·R). 등급은 rarity.txt.
        const uint HeavyStorm = 19613556;            // 태풍 (Heavy Storm) FR
        const uint MysticalSpaceTyphoon = 5318639;   // 싸이크론 (Mystical Space Typhoon) UR
        const uint PotOfAvarice = 67169062;          // 탐욕의 항아리 (Pot of Avarice) UR
        const uint UltimateOffering = 80604091;      // 희생의 제물 (Ultimate Offering) UR
        const uint MagicCylinder = 62279055;         // 매직 실린더 (Magic Cylinder) UR
        const uint MalevolentCatastrophe = 1224927;  // 사신의 대재해 (Malevolent Catastrophe) SR
        const uint GiantTrunade = 42703248;          // 허리케인 (Giant Trunade) SR
        const uint DustTornado = 60082869;           // 더스트 토네이도 (Dust Tornado) R
        const uint JarOfGreed = 83968380;            // 욕망의 단지 (Jar of Greed) R
        const uint JarOfAvarice = 98954106;          // 탐욕의 단지 (Jar of Avarice) R
        const uint DoubleSummon = 43422537;          // 이중소환 (Double Summon) R
        const uint DimensionWall = 67095270;         // 디멘션 월 (Dimension Wall) SR

        /// <summary>
        /// 시작 덱 —열풍의 패왕—: 열풍의 패왕에서 태풍 → 사신의 대재해, 싸이크론 → 더스트 토네이도 (2장이 된다). 40장.
        /// </summary>
        public static readonly DeckRecipe LordOfTheStormStart = Variant(LordOfTheStorm, "LordOfTheStormStart", "시작 덱 —열풍의 패왕—",
            (HeavyStorm, MalevolentCatastrophe), (MysticalSpaceTyphoon, DustTornado));

        /// <summary>
        /// 시작 덱 —기계의 반란—: 기계의 반란에서 태풍 → 허리케인, 싸이크론 → 더스트 토네이도, 탐욕의 항아리 → 욕망의 단지,
        /// 희생의 제물 → 이중소환. 40장.
        /// </summary>
        public static readonly DeckRecipe MachineReVoltStart = Variant(MachineReVolt, "MachineReVoltStart", "시작 덱 —기계의 반란—",
            (HeavyStorm, GiantTrunade), (MysticalSpaceTyphoon, DustTornado), (PotOfAvarice, JarOfGreed), (UltimateOffering, DoubleSummon));

        /// <summary>
        /// 시작 덱 —암흑의 주술—: 암흑의 주술에서 태풍 → 사신의 대재해, 싸이크론 → 더스트 토네이도, 탐욕의 항아리 → 탐욕의 단지,
        /// 매직 실린더 → 디멘션 월 (3장이 된다). 40장.
        /// </summary>
        public static readonly DeckRecipe CurseOfDarknessStart = Variant(CurseOfDarkness, "CurseOfDarknessStart", "시작 덱 —암흑의 주술—",
            (HeavyStorm, MalevolentCatastrophe), (MysticalSpaceTyphoon, DustTornado), (PotOfAvarice, JarOfAvarice), (MagicCylinder, DimensionWall));

        /// <summary>상대(AI) 덱으로 쓰는 원래 구성.</summary>
        public static readonly IReadOnlyList<DeckRecipe> Originals = new[] { LordOfTheStorm, MachineReVolt, CurseOfDarkness };

        /// <summary>플레이어가 고르는 시작 덱 판 (Originals와 같은 순서).</summary>
        public static readonly IReadOnlyList<DeckRecipe> StartVersions = new[] { LordOfTheStormStart, MachineReVoltStart, CurseOfDarknessStart };

        /// <summary>source에서 카드를 1장씩 바꾼 레시피. 새로 들어온 카드는 빠진 카드 자리에 둔다 (이미 있으면 그 줄의 장수를 늘린다).</summary>
        static DeckRecipe Variant(DeckRecipe source, string id, string name, params (uint Out, uint In)[] swaps)
        {
            var entries = source.Entries.ToList();
            foreach (var (outCode, inCode) in swaps)
            {
                int at = entries.FindIndex(e => e.Code == outCode);
                if (at < 0)
                    throw new InvalidOperationException($"{source.Id}에 빼낼 카드 {outCode}가 없음");
                int left = entries[at].Count - 1;
                int existing = entries.FindIndex(e => e.Code == inCode);
                if (existing >= 0)
                    entries[existing] = (inCode, entries[existing].Count + 1);
                if (left > 0)
                {
                    entries[at] = (outCode, left);
                    if (existing < 0)
                        entries.Insert(at + 1, (inCode, 1));
                }
                else if (existing < 0)
                    entries[at] = (inCode, 1);
                else
                    entries.RemoveAt(at);
            }
            return new DeckRecipe(id, name, entries);
        }
    }

    /// <summary>게임에 있는 모든 덱 레시피 (스타터 덱, 스트럭처 덱 원래 구성과 시작 덱 판). 저장 파일·이벤트의 덱 Id를 찾을 때 쓴다.</summary>
    public static class DeckCatalog
    {
        /// <summary>런의 상대 덱 목록: 스타터 덱 4개 + 스트럭처 덱 원래 구성 3개.</summary>
        public static readonly IReadOnlyList<DeckRecipe> Opponents = StarterDecks.All.Concat(StructureDecks.Originals).ToArray();

        /// <summary>모든 레시피 (Id가 겹치지 않는다).</summary>
        public static readonly IReadOnlyList<DeckRecipe> All = Opponents.Concat(StructureDecks.StartVersions).ToArray();

        /// <summary>Id(대소문자 무시)로 찾는다. 없으면 null.</summary>
        public static DeckRecipe Find(string id) =>
            All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
