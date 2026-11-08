// WindBot에게 넣기 전에, EDOPro 서버(gframe/generic_duel.cpp)가 각 플레이어에게 보내기 전에 하던 일을 똑같이 한다.
// 1. 다른 플레이어 몫의 메시지를 뺀다: 상대의 선택 요청, 상대에게만 가는 힌트. (가리기를 꺼도 해야 한다. 안 하면 WindBot의 선택 힌트가 꼬인다.)
// 2. 상대의 비공개 카드 코드를 0으로 가린다: 상대가 뽑은 카드, 상대 손패·덱·뒷면으로 가는 카드, 상대 손패 섞기,
//    내 선택 목록에 든 상대의 비공개 카드, 조회 결과의 상대 비공개 카드. (HideCards가 꺼져 있으면 하지 않는다.)
using System;
using System.Collections.Generic;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.AI
{
    internal static class WindBotMessageFilter
    {
        // HINT_* (ocgapi_constants.h)
        const byte HintEvent = 1, HintMessage = 2, HintSelectMsg = 3, HintOpSelected = 4, HintEffect = 5,
            HintRace = 6, HintAttrib = 7, HintCode = 8, HintNumber = 9, HintZone = 11;

        // loc_info: 조종자 u8, 위치 u8, 순서 u32, 표시 형식 u32
        const int LocInfoSize = 10;

        public static bool IsRequest(OcgMessageType type)
        {
            switch (type)
            {
                case OcgMessageType.SelectBattleCmd:
                case OcgMessageType.SelectIdleCmd:
                case OcgMessageType.SelectEffectYn:
                case OcgMessageType.SelectYesNo:
                case OcgMessageType.SelectOption:
                case OcgMessageType.SelectCard:
                case OcgMessageType.SelectChain:
                case OcgMessageType.SelectPlace:
                case OcgMessageType.SelectPosition:
                case OcgMessageType.SelectTribute:
                case OcgMessageType.SortChain:
                case OcgMessageType.SelectCounter:
                case OcgMessageType.SelectSum:
                case OcgMessageType.SelectDisfield:
                case OcgMessageType.SortCard:
                case OcgMessageType.SelectUnselectCard:
                case OcgMessageType.RockPaperScissors:
                case OcgMessageType.AnnounceRace:
                case OcgMessageType.AnnounceAttrib:
                case OcgMessageType.AnnounceCard:
                case OcgMessageType.AnnounceNumber:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// me(이 AI의 팀)가 받을 메시지 본문으로 바꾼다. 받지 않을 메시지면 null. 요청 메시지는 여기로 넣지 않는다.
        /// 원본 본문은 고치지 않는다 (가릴 때는 복사본을 고친다).
        /// </summary>
        public static byte[] ForObserver(OcgMessage message, byte me, bool hideCards)
        {
            byte[] body = message.Body;
            switch (message.Type)
            {
                case OcgMessageType.Hint:
                {
                    if (body.Length < 2)
                        return body;
                    byte type = body[0], player = body[1];
                    switch (type)
                    {
                        case HintEvent:
                        case HintMessage:
                        case HintSelectMsg:
                        case HintEffect:
                            return player == me ? body : null;
                        case HintOpSelected:
                        case HintRace:
                        case HintAttrib:
                        case HintCode:
                        case HintNumber:
                        case HintZone:
                            return player != me ? body : null;
                        default:
                            return body;
                    }
                }
                case OcgMessageType.MissedEffect:
                    // 효과를 놓친 플레이어에게만 간다. 본문에 플레이어 번호가 없어 카드 조종자로 판단한다.
                    return body.Length >= 1 && body[0] == me ? body : null;
            }

            if (!hideCards)
                return body;

            switch (message.Type)
            {
                case OcgMessageType.Draw:
                {
                    // 플레이어 u8, 장수 u32, (코드 u32, 표시 형식 u32) 반복
                    if (body.Length < 5 || body[0] == me)
                        return body;
                    var copy = (byte[])body.Clone();
                    uint count = BitConverter.ToUInt32(copy, 1);
                    for (int i = 0; i < count && 5 + i * 8 + 8 <= copy.Length; i++)
                    {
                        int offset = 5 + i * 8;
                        var position = (OcgPosition)BitConverter.ToUInt32(copy, offset + 4);
                        if ((position & OcgPosition.FaceUp) == 0)
                            ZeroCode(copy, offset);
                    }
                    return copy;
                }
                case OcgMessageType.Move:
                {
                    // 코드 u32, 이전 loc_info, 현재 loc_info, 이유 u32
                    const int to = 4 + LocInfoSize;
                    if (body.Length < to + LocInfoSize)
                        return body;
                    byte controller = body[to];
                    var location = (OcgLocation)body[to + 1];
                    var position = (OcgPosition)BitConverter.ToUInt32(body, to + 6);
                    bool hidden = controller != me
                        && (location & (OcgLocation.Grave | OcgLocation.Overlay)) == 0
                        && ((location & (OcgLocation.Deck | OcgLocation.Hand)) != 0 || (position & OcgPosition.FaceDown) != 0);
                    if (!hidden)
                        return body;
                    var copy = (byte[])body.Clone();
                    ZeroCode(copy, 0);
                    return copy;
                }
                case OcgMessageType.ShuffleHand:
                case OcgMessageType.ShuffleExtra:
                {
                    // 플레이어 u8, 장수 u32, 코드 u32 반복
                    if (body.Length < 5 || body[0] == me)
                        return body;
                    var copy = (byte[])body.Clone();
                    for (int offset = 5; offset + 4 <= copy.Length; offset += 4)
                        ZeroCode(copy, offset);
                    return copy;
                }
                default:
                    return body;
            }
        }

        /// <summary>me의 선택 요청 안에 든 상대의 비공개 카드 코드를 가린다.</summary>
        public static byte[] HideInRequest(OcgMessage message, byte me, bool hideCards)
        {
            byte[] body = message.Body;
            if (!hideCards)
                return body;
            switch (message.Type)
            {
                case OcgMessageType.SelectCard:
                {
                    // 플레이어, 취소 가능, 최소 u32, 최대 u32, [장수 u32, (코드 u32 + loc_info) 반복]
                    var copy = (byte[])body.Clone();
                    HideFullList(copy, 10, me);
                    return copy;
                }
                case OcgMessageType.SelectUnselectCard:
                {
                    // 플레이어, 끝낼 수 있음, 취소 가능, 최소 u32, 최대 u32, [고를 수 있는 목록], [고른 목록]
                    var copy = (byte[])body.Clone();
                    int next = HideFullList(copy, 11, me);
                    HideFullList(copy, next, me);
                    return copy;
                }
                case OcgMessageType.SelectSum:
                {
                    // 플레이어, 방식, 합계 u32, 최소 u32, 최대 u32, [꼭 고를 목록], [고를 수 있는 목록] — 항목: 코드 u32 + loc_info + 값 u32
                    var copy = (byte[])body.Clone();
                    int next = HideFullList(copy, 14, me, extraBytes: 4);
                    HideFullList(copy, next, me, extraBytes: 4);
                    return copy;
                }
                case OcgMessageType.SelectTribute:
                {
                    // 플레이어, 취소 가능, 최소 u32, 최대 u32, [장수 u32, (코드 u32, 조종자 u8, 위치 u8, 순서 u32, 릴리스 값 u8) 반복]
                    var copy = (byte[])body.Clone();
                    if (copy.Length < 14)
                        return copy;
                    uint count = BitConverter.ToUInt32(copy, 10);
                    for (int i = 0, offset = 14; i < count && offset + 11 <= copy.Length; i++, offset += 11)
                        if (copy[offset + 4] != me)
                            ZeroCode(copy, offset);
                    return copy;
                }
                default:
                    return body;
            }
        }

        /// <summary>상대 영역 조회 결과에서 비공개 카드는 표시 형식만 남기고, 공개 카드는 WindBot이 모르는 항목(IS_PUBLIC)만 뺀다.</summary>
        public static void HideCards(List<QueryCard> cards, OcgLocation location, OcgQuery windBotFlags)
        {
            foreach (var card in cards)
            {
                if (card == null)
                    continue;
                bool isPublic = card.IsPublic || IsPublicPlace(location, card.Position);
                card.Keep(isPublic ? windBotFlags : OcgQuery.Position);
            }
        }

        static bool IsPublicPlace(OcgLocation location, OcgPosition position)
        {
            if ((location & (OcgLocation.Grave | OcgLocation.Overlay)) != 0)
                return true;
            const OcgLocation faceUpPlaces = OcgLocation.MonsterZone | OcgLocation.SpellTrapZone | OcgLocation.Removed | OcgLocation.Extra;
            return (location & faceUpPlaces) != 0 && (position & OcgPosition.FaceUp) != 0;
        }

        /// <summary>[장수 u32, (코드 u32 + loc_info + extraBytes) 반복] 목록에서 상대의 비공개 카드 코드를 0으로. 목록 다음 위치를 돌려준다.</summary>
        static int HideFullList(byte[] body, int offset, byte me, int extraBytes = 0)
        {
            if (offset + 4 > body.Length)
                return body.Length;
            uint count = BitConverter.ToUInt32(body, offset);
            offset += 4;
            int entry = 4 + LocInfoSize + extraBytes;
            for (int i = 0; i < count && offset + entry <= body.Length; i++, offset += entry)
            {
                byte controller = body[offset + 4];
                var location = (OcgLocation)body[offset + 5];
                var position = (OcgPosition)BitConverter.ToUInt32(body, offset + 10);
                if (controller != me && !IsPublicPlace(location, position))
                    ZeroCode(body, offset);
            }
            return offset;
        }

        static void ZeroCode(byte[] body, int offset)
        {
            body[offset] = body[offset + 1] = body[offset + 2] = body[offset + 3] = 0;
        }
    }
}
