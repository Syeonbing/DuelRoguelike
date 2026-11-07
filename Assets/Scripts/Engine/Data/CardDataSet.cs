// 듀얼에 필요한 카드 데이터를 한 번에 읽는다:
// 기본 카드 DB(BabelCDB) → 한글 번역 DB(EDOPro 한글 패치, 카드 ID별로 덮어쓰기) → 시스템 문구(strings.conf) → 카드 스크립트 색인.
using System.IO;

namespace DuelRoguelike.Engine.Data
{
    public sealed class CardDataSet
    {
        public CardDatabase Database { get; private set; }
        public CardScriptSource Scripts { get; private set; }
        public SystemStrings Strings { get; private set; }

        /// <summary>기본 DB에서 읽은 카드 수.</summary>
        public int CardCount { get; private set; }

        /// <summary>한글 텍스트를 입힌 카드 수. 한글 DB가 없으면 0.</summary>
        public int LocalizedCount { get; private set; }

        /// <param name="externalRoot">저장소의 External 폴더 (서브모듈 BabelCDB, edopro-korean, CardScripts가 있는 곳)</param>
        public static CardDataSet Load(string externalRoot)
        {
            var set = new CardDataSet
            {
                Database = new CardDatabase(),
                Strings = new SystemStrings(),
            };
            set.CardCount = set.Database.Load(Path.Combine(externalRoot, "BabelCDB", "cards.cdb"));

            string korean = Path.Combine(externalRoot, "edopro-korean");
            string koreanCdb = Path.Combine(korean, "cards.cdb");
            if (File.Exists(koreanCdb))
                set.LocalizedCount = set.Database.LoadLocale(koreanCdb);
            string strings = Path.Combine(korean, "strings.conf");
            if (File.Exists(strings))
                set.Strings.Load(strings);

            set.Scripts = new CardScriptSource(Path.Combine(externalRoot, "CardScripts"));
            return set;
        }
    }
}
