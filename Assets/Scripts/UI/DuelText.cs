// 화면에 쓰는 한국어 문구: 플레이어, 페이즈, 위치, 카드 종류, 효과 설명.
// 카드명은 아직 카드 DB의 영문명이다 (한글 카드명은 개발 단계 5).
using System.Collections.Generic;
using System.Text;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.UI
{
    public sealed class DuelText
    {
        readonly CardDatabase _db;
        readonly SystemStrings _strings;

        /// <summary>화면 앞쪽(아래)에 있는 플레이어. 이 플레이어 기준으로 "나"/"상대"라고 쓴다.</summary>
        public byte LocalPlayer { get; set; }

        /// <param name="strings">EDOPro strings.conf (시스템 문구). 없으면 번호로 보여 준다.</param>
        public DuelText(CardDatabase db, SystemStrings strings, byte localPlayer = 0)
        {
            _db = db;
            _strings = strings ?? new SystemStrings();
            LocalPlayer = localPlayer;
        }

        /// <summary>시스템 문구. 없으면 null.</summary>
        public string System(uint id) => _strings.System(id);

        /// <summary>특수 승리 이유 문구. 없으면 null.</summary>
        public string Victory(uint reason) => _strings.Victory(reason);

        public string Player(byte player) =>
            player == LocalPlayer ? "나" : player == 1 - LocalPlayer ? "상대" : "양쪽";

        public string CardName(uint code) => code == 0 ? "카드" : _db.GetName(code);

        public CardRecord Record(uint code) => _db.TryGet(code, out var record) ? record : null;

        public static string Phase(OcgPhase phase)
        {
            switch (phase)
            {
                case OcgPhase.Draw: return "드로우 페이즈";
                case OcgPhase.Standby: return "스탠바이 페이즈";
                case OcgPhase.Main1: return "메인 페이즈 1";
                case OcgPhase.BattleStart: return "배틀 페이즈";
                case OcgPhase.BattleStep: return "배틀 스텝";
                case OcgPhase.Damage: return "데미지 스텝";
                case OcgPhase.DamageCalculation: return "데미지 계산";
                case OcgPhase.Battle: return "배틀 페이즈";
                case OcgPhase.Main2: return "메인 페이즈 2";
                case OcgPhase.End: return "엔드 페이즈";
                default: return phase.ToString();
            }
        }

        public static string ShortPhase(OcgPhase phase)
        {
            switch (phase)
            {
                case OcgPhase.Draw: return "드로우";
                case OcgPhase.Standby: return "스탠바이";
                case OcgPhase.Main1: return "메인 1";
                case OcgPhase.BattleStart:
                case OcgPhase.BattleStep:
                case OcgPhase.Damage:
                case OcgPhase.DamageCalculation:
                case OcgPhase.Battle: return "배틀";
                case OcgPhase.Main2: return "메인 2";
                case OcgPhase.End: return "엔드";
                default: return "";
            }
        }

        public static string Location(CardLocation loc)
        {
            if (loc.IsOverlay) return "엑시즈 소재";
            switch (loc.Location)
            {
                case OcgLocation.Deck: return "덱";
                case OcgLocation.Hand: return "패";
                case OcgLocation.MonsterZone: return "몬스터 존";
                case OcgLocation.SpellTrapZone: return loc.Sequence == 5 ? "필드 존" : loc.Sequence >= 6 ? "펜듈럼 존" : "마법·함정 존";
                case OcgLocation.Grave: return "묘지";
                case OcgLocation.Removed: return "제외";
                case OcgLocation.Extra: return "엑스트라 덱";
                case OcgLocation.None: return "없음";
                default: return loc.Location.ToString();
            }
        }

        public static string Position(OcgPosition position)
        {
            switch (position)
            {
                case OcgPosition.FaceUpAttack: return "앞면 공격 표시";
                case OcgPosition.FaceDownAttack: return "뒷면 공격 표시";
                case OcgPosition.FaceUpDefense: return "앞면 수비 표시";
                case OcgPosition.FaceDownDefense: return "뒷면 수비 표시";
                case OcgPosition.FaceUp: return "앞면";
                case OcgPosition.FaceDown: return "뒷면";
                default: return position.ToString();
            }
        }

        public static string Reason(OcgReason reason)
        {
            var parts = new List<string>();
            if ((reason & OcgReason.Destroy) != 0) parts.Add((reason & OcgReason.Battle) != 0 ? "전투 파괴" : "파괴");
            else if ((reason & OcgReason.Battle) != 0) parts.Add("전투");
            if ((reason & OcgReason.Release) != 0) parts.Add("릴리스");
            if ((reason & OcgReason.Discard) != 0) parts.Add("버림");
            if ((reason & OcgReason.Cost) != 0) parts.Add("코스트");
            if ((reason & OcgReason.Fusion) != 0) parts.Add("융합 소재");
            else if ((reason & OcgReason.Synchro) != 0) parts.Add("싱크로 소재");
            else if ((reason & OcgReason.Xyz) != 0) parts.Add("엑시즈 소재");
            else if ((reason & OcgReason.Ritual) != 0) parts.Add("의식 소재");
            else if ((reason & OcgReason.Material) != 0 && (reason & OcgReason.Release) == 0) parts.Add("소재");
            if ((reason & OcgReason.Return) != 0) parts.Add("되돌림");
            if ((reason & OcgReason.Effect) != 0 && parts.Count == 0) parts.Add("효과");
            if ((reason & OcgReason.Rule) != 0 && parts.Count == 0) parts.Add("룰");
            return string.Join(", ", parts);
        }

        /// <summary>카드 종류 한 줄. 예: "어둠 / 마법사족 / 레벨 7 / 일반 몬스터", "일반 마법"</summary>
        public static string TypeLine(OcgCardType type, uint level, OcgAttribute attribute, ulong race)
        {
            if ((type & OcgCardType.Spell) != 0)
                return SubType(type, "마법");
            if ((type & OcgCardType.Trap) != 0)
                return SubType(type, "함정");
            var sb = new StringBuilder();
            sb.Append(Attribute(attribute)).Append(" / ").Append(Race(race)).Append(" / ");
            sb.Append((type & OcgCardType.Xyz) != 0 ? "랭크 " : "레벨 ").Append(level).Append(" / ");
            sb.Append(MonsterKind(type));
            return sb.ToString();
        }

        static string SubType(OcgCardType type, string kind)
        {
            if ((type & OcgCardType.QuickPlay) != 0) return "속공 " + kind;
            if ((type & OcgCardType.Continuous) != 0) return "지속 " + kind;
            if ((type & OcgCardType.Equip) != 0) return "장착 " + kind;
            if ((type & OcgCardType.Field) != 0) return "필드 " + kind;
            if ((type & OcgCardType.Counter) != 0) return "카운터 " + kind;
            if ((type & OcgCardType.Ritual) != 0) return "의식 " + kind;
            return "일반 " + kind;
        }

        static string MonsterKind(OcgCardType type)
        {
            var parts = new List<string>();
            if ((type & OcgCardType.Fusion) != 0) parts.Add("융합");
            if ((type & OcgCardType.Ritual) != 0) parts.Add("의식");
            if ((type & OcgCardType.Synchro) != 0) parts.Add("싱크로");
            if ((type & OcgCardType.Xyz) != 0) parts.Add("엑시즈");
            if ((type & OcgCardType.Tuner) != 0) parts.Add("튜너");
            if ((type & OcgCardType.Flip) != 0) parts.Add("리버스");
            if ((type & OcgCardType.Toon) != 0) parts.Add("툰");
            if ((type & OcgCardType.Spirit) != 0) parts.Add("스피릿");
            if ((type & OcgCardType.Union) != 0) parts.Add("유니온");
            if ((type & OcgCardType.Gemini) != 0) parts.Add("듀얼");
            if ((type & OcgCardType.Token) != 0) parts.Add("토큰");
            if ((type & OcgCardType.Normal) != 0) parts.Add("일반");
            else if ((type & OcgCardType.Effect) != 0) parts.Add("효과");
            return string.Join(" ", parts) + " 몬스터";
        }

        public static string Attribute(OcgAttribute attribute)
        {
            switch (attribute)
            {
                case OcgAttribute.Earth: return "땅";
                case OcgAttribute.Water: return "물";
                case OcgAttribute.Fire: return "화염";
                case OcgAttribute.Wind: return "바람";
                case OcgAttribute.Light: return "빛";
                case OcgAttribute.Dark: return "어둠";
                case OcgAttribute.Divine: return "신";
                default: return "?";
            }
        }

        static readonly string[] RaceNames =
        {
            "전사족", "마법사족", "천사족", "악마족", "언데드족", "기계족", "물족", "화염족", "암석족", "비행야수족",
            "식물족", "곤충족", "번개족", "드래곤족", "야수족", "야수전사족", "공룡족", "어류족", "해룡족", "파충류족",
            "사이킥족", "환신야수족", "창조신족", "환룡족", "사이버스족", "환상마족",
        };

        public static string Race(ulong race)
        {
            for (int i = 0; i < RaceNames.Length; i++)
                if ((race & (1UL << i)) != 0)
                    return RaceNames[i];
            return race == 0 ? "?" : $"종족 0x{race:X}";
        }

        /// <summary>
        /// 효과 설명 번호를 문구로 바꾼다. 카드 문구는 (카드 코드 &lt;&lt; 20 | 순번)이고 카드 DB의 str1~str16에 있다.
        /// 그보다 작은 번호는 strings.conf의 시스템 문구다.
        /// </summary>
        public string Description(ulong description)
        {
            if (description == 0)
                return null;
            uint code = (uint)(description >> 20);
            int index = (int)(description & 0xfffff);
            if (code == 0)
                return _strings.System((uint)description) ?? $"시스템 문구 #{description}";
            if (_db.TryGet(code, out var record))
            {
                if (index < record.Strings.Length && !string.IsNullOrEmpty(record.Strings[index]))
                    return record.Strings[index];
                return $"{record.Name}의 효과 {index + 1}";
            }
            return $"카드 {code}의 효과 {index + 1}";
        }

        /// <summary>효과 선택지 한 줄: "카드명" 또는 "카드명: 설명"</summary>
        public string Effect(uint code, ulong description)
        {
            string name = CardName(code);
            string desc = Description(description);
            return string.IsNullOrEmpty(desc) || desc.StartsWith(name) ? name : $"{name}: {desc}";
        }
    }
}
