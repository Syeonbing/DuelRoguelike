// 엔진 메시지 → 로그 한 줄 (설계: 화려한 연출 대신 처리 과정을 로그로 남긴다).
// 상대가 볼 수 없는 카드(상대 패, 뒷면, 덱에서 패로 등)는 이름을 숨긴다.
using System.Linq;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.UI
{
    public sealed class DuelLogFormatter
    {
        readonly DuelText _text;
        bool _inBattle; // 데미지 계산(MSG_BATTLE) 뒤 데미지 스텝이 끝날 때까지: 이 사이의 데미지는 전투 데미지다

        public DuelLogFormatter(DuelText text) => _text = text;

        /// <summary>
        /// 바로 앞에 Format한 메시지의 행동을 한 플레이어 (로그 색·가운데 줄에 쓴다). 소환·발동·공격처럼 행동한 쪽이 분명하면 그쪽,
        /// 이동·LP 변화·체인 처리처럼 분명하지 않으면 그 일을 일으킨 앞 행동의 쪽, 그것도 없으면 턴 플레이어.
        /// </summary>
        public byte LastActor { get; private set; } = OcgConstants.PlayerNone;

        /// <summary>로그에 남길 문장. 남기지 않을 메시지는 null.</summary>
        public string Format(DuelMessage message, DuelState state)
        {
            UpdateActor(message, state);
            switch (message)
            {
                case SimpleMessage m when m.Type == OcgMessageType.DamageStepStart:
                    return "[데미지 스텝]";
                case SimpleMessage m when m.Type == OcgMessageType.DamageStepEnd:
                    _inBattle = false;
                    return null;
                case BattleMessage m:
                    _inBattle = true;
                    return FormatBattle(m, state);
                case CardListMessage m when m.Type == OcgMessageType.BecomeTarget && m.Cards.Count > 0:
                    return $"대상: {string.Join(", ", m.Cards.Select(c => CardAt(state.Find(c), c)))}";
                case NewTurnMessage m:
                    return $"──── {state.Turn}턴: {_text.Player(m.Player)}의 턴 ────";
                case NewPhaseMessage m:
                    return $"[{DuelText.Phase(m.Phase)}]";
                case DrawMessage m:
                    return m.Player == _text.LocalPlayer
                        ? $"{_text.Player(m.Player)}: {m.Codes.Count}장 드로우 ({string.Join(", ", m.Codes.Select(_text.CardName))})"
                        : $"{_text.Player(m.Player)}: {m.Codes.Count}장 드로우";
                case SummoningMessage m:
                    return $"{_text.Player(m.Location.Controller)}: {VisibleName(m.Code, m.Location)} {SummonWord(m.Kind)}";
                case SetMessage m:
                    return $"{_text.Player(m.Location.Controller)}: {VisibleName(m.Code, m.Location, faceUp: false)} 세트";
                case PositionChangeMessage m:
                    return $"{CardAt(m.Code, m.Location, m.Current)}: {DuelText.Position(m.Previous)} → {DuelText.Position(m.Current)}";
                case MoveMessage m:
                    return FormatMove(m);
                case ChainingMessage m:
                    return $"체인 {m.ChainCount}: {_text.Player(m.Location.Controller)}의 {EffectName(m.Code, m.Description)} 발동";
                case ChainEventMessage m when m.Type == OcgMessageType.ChainSolving:
                    return $"체인 {m.ChainCount} 처리";
                case ChainEventMessage m when m.Type == OcgMessageType.ChainNegated:
                    return $"체인 {m.ChainCount}: 발동 무효";
                case ChainEventMessage m when m.Type == OcgMessageType.ChainDisabled:
                    return $"체인 {m.ChainCount}: 효과 무효";
                case SimpleMessage m when m.Type == OcgMessageType.ChainEnd:
                    return "체인 종료";
                case SimpleMessage m when m.Type == OcgMessageType.AttackDisabled:
                    return "공격이 무효가 됐다";
                case LpChangeMessage m:
                    return FormatLp(m, state);
                case AttackMessage m:
                {
                    string attacker = CardAt(state.Find(m.Attacker), m.Attacker);
                    return m.IsDirect
                        ? $"{_text.Player(m.Attacker.Controller)}: {attacker} → 직접 공격"
                        : $"{_text.Player(m.Attacker.Controller)}: {attacker} → {CardAt(state.Find(m.Target), m.Target)} 공격";
                }
                case WinMessage m:
                    return FormatWin(m);
                case TossMessage m when m.Type == OcgMessageType.TossCoin:
                    return $"{_text.Player(m.Player)}: 코인 토스 → {string.Join(", ", m.Results.Select(x => x != 0 ? "앞" : "뒤"))}";
                case TossMessage m:
                    return $"{_text.Player(m.Player)}: 주사위 → {string.Join(", ", m.Results)}";
                case ConfirmCardsMessage m when m.Cards.Count > 0:
                {
                    // 덱·엑스트라 덱 위를 넘겨 보이는 메시지의 Player는 그 덱의 주인이고, 그 밖에는 카드를 보는 쪽이다.
                    string names = string.Join(", ", m.Cards.Select(c => _text.CardName(c.Code)));
                    return m.Type == OcgMessageType.ConfirmDecktop ? $"{_text.Player(m.Player)}의 덱 위 카드 공개: {names}"
                        : m.Type == OcgMessageType.ConfirmExtratop ? $"{_text.Player(m.Player)}의 엑스트라 덱 위 카드 공개: {names}"
                        : $"{_text.Player(m.Player)}에게 공개: {names}";
                }
                case CardPairMessage m when m.Type == OcgMessageType.Equip:
                    return $"{CardAt(state.Find(m.Card), m.Card)} → {CardAt(state.Find(m.Target), m.Target)}에 장착";
                case HintMessage m when m.HintType == OcgHintType.OptionSelected:
                    // 상대가 고른 효과를 알려 주는 안내 (Player는 안내를 받는 쪽).
                    return $"{_text.Player((byte)(1 - m.Player))}의 선택: {_text.Description(m.Data)}";
                case HintMessage m when m.HintType == OcgHintType.Code:
                    return $"{_text.Player(m.Player)}: 「{_text.CardName((uint)m.Data)}」 선언";
                case HintMessage m when m.HintType == OcgHintType.Number:
                    return $"{_text.Player(m.Player)}: {m.Data} 선언";
                case HintMessage m when m.HintType == OcgHintType.Race:
                    return $"{_text.Player(m.Player)}: {DuelText.Race(m.Data)} 선언";
                case HintMessage m when m.HintType == OcgHintType.Attribute:
                    return $"{_text.Player(m.Player)}: {DuelText.Attribute((OcgAttribute)m.Data)} 선언";
                case MissedEffectMessage m:
                    return $"{_text.CardName(m.Code)}: 효과 타이밍을 놓쳤다";
                case SimpleMessage m when m.Type == OcgMessageType.Retry:
                    return "엔진이 선택을 받아들이지 않았다. 다시 고른다.";
                default:
                    return null;
            }
        }

        void UpdateActor(DuelMessage message, DuelState state)
        {
            switch (message)
            {
                case NewTurnMessage m: LastActor = m.Player; break;
                case SummoningMessage m: LastActor = m.Location.Controller; break;
                case SetMessage m: LastActor = m.Location.Controller; break;
                case ChainingMessage m: LastActor = m.Location.Controller; break;
                case AttackMessage m: LastActor = m.Attacker.Controller; break;
                case BattleMessage m: LastActor = m.Attacker.Controller; break;
                case DrawMessage m: LastActor = m.Player; break;
                case TossMessage m: LastActor = m.Player; break;
                case ChainEventMessage m when m.Type == OcgMessageType.ChainSolving && m.ChainCount >= 1 && m.ChainCount <= state.Chain.Count:
                    LastActor = state.Chain[(int)m.ChainCount - 1].Location.Controller;
                    break;
                case SimpleMessage m when m.Type == OcgMessageType.ChainEnd:
                    LastActor = state.TurnPlayer;
                    break;
                case NewPhaseMessage _:
                    _inBattle = false;
                    LastActor = state.TurnPlayer;
                    break;
            }
            if (LastActor > 1)
                LastActor = state.TurnPlayer;
        }

        string FormatBattle(BattleMessage m, DuelState state)
        {
            string attacker = $"{CardAt(state.Find(m.Attacker), m.Attacker)} 공격력 {m.AttackerAttack}";
            if (m.Target.IsEmpty)
                return $"데미지 계산: {attacker} → 직접 공격";
            bool defense = (m.Target.Position & OcgPosition.Defense) != 0;
            string target = CardAt(state.Find(m.Target), m.Target);
            return defense
                ? $"데미지 계산: {attacker} vs {target} 수비력 {m.TargetDefense}"
                : $"데미지 계산: {attacker} vs {target} 공격력 {m.TargetAttack}";
        }

        /// <summary>발동한 카드 이름 (효과 설명이 따로 있으면 괄호로 붙인다).</summary>
        string EffectName(uint code, ulong description)
        {
            string name = _text.CardName(code);
            string desc = _text.Description(description);
            return string.IsNullOrEmpty(desc) || desc.StartsWith(name) ? $"「{name}」" : $"「{name}」({desc})";
        }

        static string SummonWord(SummonKind kind)
        {
            switch (kind)
            {
                case SummonKind.Special: return "특수 소환";
                case SummonKind.Flip: return "반전 소환";
                default: return "일반 소환";
            }
        }

        string FormatMove(MoveMessage m)
        {
            // 소환·세트·드로우는 따로 남기므로, 그 이동은 넘긴다.
            if (m.From.Location == OcgLocation.Deck && m.To.Location == OcgLocation.Hand && (m.Reason & OcgReason.Draw) != 0)
                return null;
            if ((m.Reason & (OcgReason.Summon | OcgReason.SpSummon)) != 0 && m.To.IsOnField)
                return null;
            // 패에서 필드로 나오는 것은 소환·세트·발동 문장으로 남는다.
            if (m.From.Location == OcgLocation.Hand && m.To.IsOnField)
                return null;
            // 덱 맨 위로 옮기는 등 엔진 내부 정리용 이동은 이유가 없다.
            if (m.From.Location == OcgLocation.None)
                return null;

            byte owner = m.From.Location != OcgLocation.None ? m.From.Controller : m.To.Controller;
            bool visible = IsVisible(m.Code, m.To) || IsVisible(m.Code, m.From);
            string name = visible ? $"「{_text.CardName(m.Code)}」" : "카드 1장";
            string reason = DuelText.Reason(m.Reason);
            string suffix = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
            return $"{_text.Player(owner)}: {name} {DuelText.Location(m.From)} → {DuelText.Location(m.To)}{suffix}";
        }

        string FormatLp(LpChangeMessage m, DuelState state)
        {
            string who = _text.Player(m.Player);
            int lp = m.Player < 2 ? state.Players[m.Player].Lp : 0;
            switch (m.Kind)
            {
                case LpChangeKind.Damage when _inBattle: return $"{who}: 전투 데미지 {m.Amount} (LP {lp})";
                case LpChangeKind.Damage: return $"{who}: {m.Amount} 데미지 (LP {lp})";
                case LpChangeKind.Recover: return $"{who}: LP {m.Amount} 회복 (LP {lp})";
                case LpChangeKind.PayCost: return $"{who}: LP {m.Amount} 지불 (LP {lp})";
                default: return $"{who}: LP가 {m.Amount}이 됐다";
            }
        }

        string FormatWin(WinMessage m)
        {
            string reason = m.Reason == OcgConstants.WinReasonLp ? "LP 0"
                : m.Reason == OcgConstants.WinReasonDeckOut ? "덱에서 카드를 뽑을 수 없음"
                : _text.Victory(m.Reason) ?? $"특수 승리 (이유 0x{m.Reason:X})";
            if (m.Winner == OcgConstants.PlayerNone)
                return $"════ 무승부 ({reason}) ════";
            return $"════ {_text.Player(m.Winner)}의 승리 ({reason}) ════";
        }

        /// <summary>로컬 플레이어가 이 위치의 카드 이름을 알 수 있는지.</summary>
        bool IsVisible(uint code, CardLocation loc)
        {
            if (code == 0)
                return false;
            if (loc.Controller == _text.LocalPlayer)
                return true;
            switch (loc.Location)
            {
                case OcgLocation.Grave:
                case OcgLocation.Removed when (loc.Position & OcgPosition.FaceDown) == 0:
                    return true;
                case OcgLocation.MonsterZone:
                case OcgLocation.SpellTrapZone:
                    return (loc.Position & OcgPosition.FaceUp) != 0;
                default:
                    return false;
            }
        }

        string VisibleName(uint code, CardLocation loc, bool faceUp = true)
        {
            if (code != 0 && (loc.Controller == _text.LocalPlayer || faceUp))
                return $"「{_text.CardName(code)}」";
            return "카드 1장";
        }

        string CardAt(uint code, CardLocation loc, OcgPosition position)
        {
            var withPos = new CardLocation(loc.Controller, loc.Location, loc.Sequence, position);
            return IsVisible(code, withPos) || IsVisible(code, loc) ? $"「{_text.CardName(code)}」" : "뒷면 카드";
        }

        string CardAt(CardInfo card, CardLocation loc)
        {
            if (card == null)
                return DuelText.Location(loc);
            if (card.Location.Controller == _text.LocalPlayer || card.IsPublic || card.IsFaceUp)
                return $"「{_text.CardName(card.Code)}」";
            return "뒷면 카드";
        }
    }
}
