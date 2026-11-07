// WindBot Ignite의 Game/Dialogs.cs를 대신한다 (AGPL-3.0-or-later, 원본: https://github.com/ProjectIgnis/windbot).
// 원본은 생성자에서 Dialogs/<이름>.json(영문·중문 대사)을 읽어 채팅으로 보낸다.
// 이 게임에는 채팅이 없으므로 같은 메서드를 두고 아무것도 하지 않는다. 한국어 대사를 넣게 되면 여기서 chat으로 보낸다.
using System;

namespace WindBot.Game.AI
{
    public class Dialogs
    {
        private readonly Action<string, bool> _chat;

        public Dialogs(string dialogfilename, Action<string, bool> chat, string path)
        {
            _chat = chat;
        }

        public void SendSorry() { }
        public void SendDeckSorry(string card) { }
        public void SendWelcome() { }
        public void SendDuelStart() { }
        public void SendNewTurn() { }
        public void SendEndTurn() { }
        public void SendDirectAttack(string attacker) { }
        public void SendAttack(string attacker, string defender) { }
        public void SendOnDirectAttack(string attacker) { }
        public void SendOnDirectAttack() { }
        public void SendActivate(string spell) { }
        public void SendSummon(string monster) { }
        public void SendSetMonster() { }
        public void SendChaining(string card) { }
        public void SendSurrender() { }
        public void SendCustomChat(int index, params object[] opts) { }
    }
}
