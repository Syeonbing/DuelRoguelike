// WindBot의 카드 정보(NamedCardsManager)를 이 프로젝트의 카드 DB로 채운다.
// WindBot은 카드 수치(레벨·공격력·종족 등)로 판단하고, 카드 선언(AnnounceCard)에서는 전체 카드 목록을 훑는다.
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;
using YGOSharp.OCGWrapper;

namespace DuelRoguelike.AI
{
    internal static class WindBotCards
    {
        static CardDatabase _loaded;

        /// <summary>이 DB로 아직 채우지 않았으면 채운다. WindBot의 카드 정보는 정적이라 한 번에 DB 하나만 쓴다.</summary>
        public static void EnsureLoaded(CardDatabase database)
        {
            if (ReferenceEquals(_loaded, database))
                return;
            NamedCardsManager.Clear();
            foreach (var record in database.Records)
            {
                var d = record.Data;
                ulong setcode = 0;
                if (d.SetCodes != null)
                    for (int i = 0; i < d.SetCodes.Length && i < 4; i++)
                        setcode |= (ulong)d.SetCodes[i] << (i * 16);
                // cards.cdb의 level 칸과 같은 형식으로 되돌린다: 하위 8비트 레벨/랭크, 16~23비트 오른쪽 스케일, 24~31비트 왼쪽 스케일
                uint levelInfo = (d.Level & 0xff) | ((d.RightScale & 0xff) << 16) | ((d.LeftScale & 0xff) << 24);
                // 링크 몬스터는 DB의 def 칸에 링크 마커가 들어 있다 (WindBot의 Card 생성자가 그렇게 읽는다).
                int defense = (d.Type & (uint)OcgCardType.Link) != 0 ? (int)d.LinkMarker : d.Defense;
                NamedCardsManager.AddCard((int)d.Code, (int)record.Ot, (int)d.Alias, setcode, d.Type, levelInfo,
                    d.Race, d.Attribute, d.Attack, defense, record.Name, record.Description);
            }
            _loaded = database;
        }
    }
}
