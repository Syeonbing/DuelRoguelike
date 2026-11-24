// 듀얼에 필요한 카드 데이터를 한 번에 읽는다:
// 기본 카드 DB(BabelCDB) → 한글 번역 DB(EDOPro 한글 패치, 카드 ID별로 덮어쓰기) → 카드명 덮어쓰기 표(공식 한글 카드명)
// → 시스템 문구(strings.conf) → 카드 스크립트 색인.
using System.IO;

namespace DuelRoguelike.Engine.Data
{
    public sealed class CardDataSet
    {
        /// <summary>카드명 덮어쓰기 표의 기본 위치 (StreamingAssets 기준).</summary>
        public const string NameOverridesRelativePath = "Data/card-names.txt";

        public CardDatabase Database { get; private set; }
        public CardScriptSource Scripts { get; private set; }
        public SystemStrings Strings { get; private set; }

        /// <summary>기본 DB에서 읽은 카드 수.</summary>
        public int CardCount { get; private set; }

        /// <summary>한글 텍스트를 입힌 카드 수. 한글 DB가 없으면 0.</summary>
        public int LocalizedCount { get; private set; }

        /// <summary>카드명 덮어쓰기 표로 이름을 바꾼 카드 수. 표가 없으면 0.</summary>
        public int NameOverrideCount { get; private set; }

        /// <param name="externalRoot">저장소의 External 폴더 (서브모듈 BabelCDB, edopro-korean, CardScripts가 있는 곳)</param>
        /// <param name="nameOverridesPath">카드명 덮어쓰기 표 (보통 StreamingAssets/Data/card-names.txt). null이거나 파일이 없으면 건너뛴다.</param>
        public static CardDataSet Load(string externalRoot, string nameOverridesPath = null)
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
            if (!string.IsNullOrEmpty(nameOverridesPath) && File.Exists(nameOverridesPath))
                set.NameOverrideCount = set.Database.ApplyNameOverrides(File.ReadAllText(nameOverridesPath), Path.GetFileName(nameOverridesPath));
            string strings = Path.Combine(korean, "strings.conf");
            if (File.Exists(strings))
                set.Strings.Load(strings);

            set.Scripts = new CardScriptSource(Path.Combine(externalRoot, "CardScripts"));
            return set;
        }
    }
}
