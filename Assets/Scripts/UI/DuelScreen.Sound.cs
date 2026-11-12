// 듀얼 화면의 소리: 엔진 메시지마다 낼 효과음과 듀얼 배경음. 소리 파일·음량은 Sound.cs의 표에 있다.
// 연출과 같이 한 단계씩 보여 주는 동안에만 낸다. 건너뛰는 중과 개발용 "자동으로 끝까지"에서는 내지 않고,
// 건너뛰기를 시작하면 내던 소리를 빨리 줄인다 (BeginSkip → Sound.Hush).
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class DuelScreen
    {
        /// <summary>한 번에 여러 장·여러 번 일어날 때(드로우, 코인 토스) 소리를 낼 최대 횟수.</summary>
        const int MaxRepeatSounds = 3;

        /// <summary>직접 공격을 받는 플레이어 (그 데미지에 묵직한 타격음을 낸다). 없으면 -1.</summary>
        int _directHitTo = -1;

        /// <summary>
        /// 직접 공격을 기억한다: 직접 공격 선언부터 데미지 스텝이 끝날 때까지. 공격이 무효가 되거나 페이즈·턴이 바뀌면 잊는다.
        /// 소리를 내지 않는 동안(건너뛰기·자동 진행)에도 따라가야 하므로 모든 메시지에서 부른다.
        /// </summary>
        void TrackDirectHit(DuelMessage message)
        {
            if (message is AttackMessage attack)
                _directHitTo = attack.IsDirect ? 1 - attack.Attacker.Controller : -1;
            else if (message is NewPhaseMessage || message is NewTurnMessage
                || message.Type == OcgMessageType.DamageStepEnd || message.Type == OcgMessageType.AttackDisabled)
                _directHitTo = -1;
        }

        void PlayMessageSound(DuelMessage message)
        {
            switch (message)
            {
                case DrawMessage m:
                    // 덱에서 한 장씩 날아오는 연출(장당 0.08초 늦게)에 맞춘다
                    for (int i = 0; i < Mathf.Min(MaxRepeatSounds, m.Codes.Count); i++)
                        Sound.Play(Sfx.Draw, i * 0.08f);
                    break;
                case MoveMessage m:
                    PlayMoveSound(m);
                    break;
                case SummoningMessage m:
                    Sound.Play(m.Kind == SummonKind.Special ? Sfx.SpecialSummon : m.Kind == SummonKind.Flip ? Sfx.Flip : Sfx.Summon);
                    break;
                case SetMessage _:
                    Sound.Play(Sfx.Set);
                    break;
                case PositionChangeMessage _:
                    Sound.Play(Sfx.Flip);
                    break;
                case ChainingMessage _:
                    // 카드가 제자리에서 펴지며 가운데로 떠오른다
                    Sound.Play(Sfx.Activate);
                    Sound.Play(Sfx.ActivateSwoosh, 0.05f);
                    break;
                case CardListMessage m when m.Type == OcgMessageType.BecomeTarget:
                    Sound.Play(Sfx.Target);
                    break;
                case CardPairMessage m when m.Type == OcgMessageType.Equip:
                    Sound.Play(Sfx.Equip);
                    break;
                case AttackMessage _:
                    Sound.Play(Sfx.Attack);
                    break;
                case LpChangeMessage m:
                    if (m.Kind == LpChangeKind.Recover)
                        Sound.Play(Sfx.Recover);
                    else if (m.Kind == LpChangeKind.Damage && m.Player == _directHitTo)
                    {
                        // 직접 공격이 들어갔다: 펀치와 낮은 쿵
                        _directHitTo = -1;
                        Sound.Play(Sfx.DirectHit);
                        Sound.Play(Sfx.DirectHitBoom, 0.02f);
                    }
                    else if (m.Kind == LpChangeKind.Damage)
                        Sound.Play(Sfx.Damage);
                    else if (m.Kind == LpChangeKind.PayCost)
                        Sound.Play(Sfx.Damage, volume: 0.6f);
                    break;
                case TossMessage m:
                {
                    // 여러 번 던지면 옆으로 놓고 조금씩 늦게 던진다 (FxToss)
                    var sfx = m.Type == OcgMessageType.TossDice ? Sfx.Dice : Sfx.Coin;
                    for (int i = 0; i < Mathf.Min(MaxRepeatSounds, m.Results.Count); i++)
                        Sound.Play(sfx, i * 0.12f);
                    break;
                }
                case ShuffleMessage _:
                    Sound.Play(Sfx.Shuffle);
                    break;
                case NewTurnMessage _:
                    Sound.Play(Sfx.NewTurn);
                    break;
                case NewPhaseMessage _:
                    Sound.Play(Sfx.Phase);
                    break;
                case WinMessage m:
                    Sound.Play(m.Winner == OcgConstants.PlayerNone ? Sfx.GameDraw : m.Winner == Me ? Sfx.Win : Sfx.Lose);
                    break;
            }
        }

        /// <summary>
        /// 카드 이동: 파괴(산산조각), 제외, 묘지로(버리기·덱에서 보내기 등), 패·덱으로 되돌리기·서치.
        /// 필드로 나오는 이동은 소환·세트·발동 메시지가 따로 소리를 낸다.
        /// </summary>
        void PlayMoveSound(MoveMessage m)
        {
            var to = m.To.Location;
            if (to == OcgLocation.Grave && m.From.IsOnField && (m.Reason & OcgReason.Destroy) != 0)
                Sound.Play(Sfx.Destroy);
            else if (to == OcgLocation.Removed)
                Sound.Play(Sfx.Banish);
            else if (to == OcgLocation.Grave)
                Sound.Play(Sfx.ToGrave);
            else if ((to == OcgLocation.Hand || to == OcgLocation.Deck || to == OcgLocation.Extra) && m.From.Location != OcgLocation.None)
                Sound.Play(Sfx.ToHand);
        }

        /// <summary>듀얼 화면이 보이는 동안의 배경음: 듀얼 중에는 듀얼 곡, 끝나면 멈춘다 (승패 소리가 들리게).</summary>
        void UpdateMusic()
        {
            if (_docRoot == null || _docRoot.style.display == DisplayStyle.None || _session == null)
                return;
            Sound.Music(_session.State.IsFinished ? MusicTrack.None : MusicTrack.Duel);
        }
    }
}
