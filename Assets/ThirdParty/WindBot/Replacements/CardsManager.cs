// WindBot Ignite의 YGOSharp.OCGWrapper/CardsManager.cs를 대신한다 (AGPL-3.0-or-later, 원본: https://github.com/ProjectIgnis/windbot).
// 원본은 Mono.Data.Sqlite로 cards.cdb를 따로 한 번 더 읽는다. 여기서는 NamedCardsManager에 넣은 카드를 그대로 돌려준다.
namespace YGOSharp.OCGWrapper
{
    internal static class CardsManager
    {
        internal static Card GetCard(int id)
        {
            return NamedCardsManager.GetCard(id);
        }
    }
}
