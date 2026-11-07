// WindBotGameBehavior에 이 프로젝트가 더한 부분 (원본 WindBot에는 없다).
// EDOPro에서는 서버가 MSG_START를 만들고, 필요할 때마다 MSG_UPDATE_DATA로 카드 정보를 보낸다. 여기서는 WindBotPlayer가 그 내용을 만들어 넣는다.
// WindBot의 MSG_UPDATE_DATA 처리(OnUpdateData)는 자기 필드 모델의 칸 수만큼 카드를 읽으므로, 모델이 엔진과 어긋나면 잘못 읽는다.
// 그래서 넣기 전에 칸 수와 빈칸을 엔진에 맞춘다 (SyncLocation). 정상이라면 맞출 것이 없다.
using System.Collections.Generic;
using WindBot.Game;
using YGOSharp.OCGWrapper.Enums;

namespace DuelRoguelike.AI
{
    internal sealed partial class WindBotGameBehavior
    {
        /// <summary>MSG_START를 넣는다 (EDOPro 서버가 만드는 메시지). team: 이 AI의 엔진 팀 번호.</summary>
        public void Start(int team, int[] lifePoints, int[] deckCount, int[] extraCount)
        {
            var body = new byte[1 + 4 * 2 + 2 * 4];
            body[0] = (byte)team;
            for (int p = 0; p < 2; p++)
            {
                System.BitConverter.GetBytes(lifePoints[p]).CopyTo(body, 1 + p * 4);
                System.BitConverter.GetBytes((short)deckCount[p]).CopyTo(body, 9 + p * 4);
                System.BitConverter.GetBytes((short)extraCount[p]).CopyTo(body, 11 + p * 4);
            }
            HandleMessage(GameMessage.Start, body);
        }

        /// <summary>
        /// 한 영역의 칸 수와 빈칸을 엔진과 맞춘다. engineSlots: 엔진 조회 결과의 칸마다 카드가 있으면 표시 형식, 빈 칸이면 null.
        /// 새로 만든 카드는 코드 0으로 두고, 바로 뒤의 MSG_UPDATE_DATA가 채운다. 맞춘 칸 수를 돌려준다.
        /// </summary>
        public int SyncLocation(int team, CardLocation location, IList<int?> engineSlots)
        {
            int player = GetLocalPlayer(team);
            ClientField field = _duel.Fields[player];
            switch (location)
            {
                case CardLocation.MonsterZone: return SyncZone(field.MonsterZone, location, player, engineSlots);
                case CardLocation.SpellZone: return SyncZone(field.SpellZone, location, player, engineSlots);
                case CardLocation.Hand: return SyncList(field.Hand, location, player, engineSlots);
                case CardLocation.Grave: return SyncList(field.Graveyard, location, player, engineSlots);
                case CardLocation.Removed: return SyncList(field.Banished, location, player, engineSlots);
                case CardLocation.Extra: return SyncList(field.ExtraDeck, location, player, engineSlots);
                default: return 0;
            }
        }

        private int SyncZone(ClientCard[] zone, CardLocation location, int player, IList<int?> engineSlots)
        {
            int fixes = 0;
            for (int seq = 0; seq < zone.Length; seq++)
            {
                int? position = seq < engineSlots.Count ? engineSlots[seq] : null;
                if (position.HasValue && zone[seq] == null)
                {
                    _duel.AddCard(location, 0, player, seq, position.Value);
                    fixes++;
                }
                else if (!position.HasValue && zone[seq] != null)
                {
                    zone[seq] = null;
                    fixes++;
                }
            }
            return fixes;
        }

        private int SyncList(IList<ClientCard> list, CardLocation location, int player, IList<int?> engineSlots)
        {
            var positions = new List<int>();
            foreach (int? slot in engineSlots)
                if (slot.HasValue)
                    positions.Add(slot.Value);
            if (list.Count == positions.Count)
                return 0;
            int fixes = System.Math.Abs(list.Count - positions.Count);
            list.Clear();
            for (int seq = 0; seq < positions.Count; seq++)
                _duel.AddCard(location, 0, player, seq, positions[seq]);
            return fixes;
        }

        /// <summary>점검용: 이 영역에서 secret[i]가 true(비공개)인데 WindBot이 카드 코드를 알고 있는 장수.</summary>
        public int CountKnownSecrets(int team, CardLocation location, IList<bool> secret)
        {
            if (location != CardLocation.Hand)
                return 0;
            IList<ClientCard> hand = _duel.Fields[GetLocalPlayer(team)].Hand;
            int known = 0;
            for (int i = 0; i < hand.Count && i < secret.Count; i++)
                if (secret[i] && hand[i] != null && hand[i].Id != 0)
                    known++;
            return known;
        }

        /// <summary>MSG_UPDATE_DATA를 넣는다: [팀 u8][위치 u8] + 조회 결과.</summary>
        public void UpdateData(int team, CardLocation location, byte[] query)
        {
            var body = new byte[2 + query.Length];
            body[0] = (byte)team;
            body[1] = (byte)location;
            query.CopyTo(body, 2);
            HandleMessage(GameMessage.UpdateData, body);
        }
    }
}
