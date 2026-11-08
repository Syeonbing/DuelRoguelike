// 스타터 덱(유희·카이바·조이·페가수스 편)처럼 일반 몬스터와 고전 마법·함정으로 된 덱을 위한 범용 WindBot Executor.
// WindBot에는 이 덱들을 위한 Executor가 없다. DefaultExecutor(WindBot Ignite, AGPL-3.0-or-later)의 기본 판단을 바탕으로,
// 아는 카드는 카드별 조건으로 쓰고, 모르는 카드는 "자기 체인에 체인하지 않기" 기본 규칙으로 발동한다.
// 덱별 Executor는 스타터 덱 목록(개발 단계 5)이 정해진 뒤에 따로 만든다.
using System.Collections.Generic;
using System.Linq;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

namespace DuelRoguelike.AI
{
    internal sealed class StarterDeckExecutor : DefaultExecutor
    {
        public static class CardId
        {
            public const int DarkHole = 53129443;            // 블랙홀
            public const int MonsterReborn = 83764718;       // 죽은 자의 소생
            public const int SwordsOfRevealingLight = 72302403; // 빛의 봉인검
            public const int DianKeto = 84257639;            // 치료의 신 다이안 켓
            public const int Fissure = 66788016;             // 갈라진 대지
            public const int TrapHole = 4206964;             // 함정 속으로
            public const int DragonCaptureJar = 50045299;    // 드래곤족 봉인항아리
            public const int Raigeki = 12580477;             // 번개
            public const int HeavyStorm = 19613556;          // 대폭풍
            public const int MysticalSpaceTyphoon = 5318639; // 사이크론
        }

        /// <summary>죽은 자의 소생으로 되살릴 만한 최소 공격력.</summary>
        const int RebornMinAttack = 1500;

        public StarterDeckExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 마법: 아는 카드는 조건을 따진다 (앞에 둘수록 먼저 쓴다)
            AddExecutor(ExecutorType.Activate, CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, CardId.Raigeki, DefaultRaigeki);
            AddExecutor(ExecutorType.Activate, CardId.DarkHole, DarkHole);
            AddExecutor(ExecutorType.Activate, CardId.Fissure, Fissure);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLight);
            AddExecutor(ExecutorType.Activate, CardId.MonsterReborn, MonsterReborn);
            AddExecutor(ExecutorType.Activate, CardId.DianKeto, DianKeto);

            // 함정
            AddExecutor(ExecutorType.Activate, CardId.TrapHole, DefaultTrap);
            AddExecutor(ExecutorType.Activate, CardId.DragonCaptureJar, DragonCaptureJar);

            // 특수 소환 절차가 있는 몬스터, 모르는 카드의 효과
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);

            // 소환: 가장 센 몬스터부터. 못 이길 것 같으면 DefaultExecutor가 세트를 고른다.
            AddExecutor(ExecutorType.SummonOrSet, StrongestSummon);
            AddExecutor(ExecutorType.MonsterSet, BestDefenderSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
        }

        bool DarkHole()
        {
            // 상대가 더 세고, 내 필드 몬스터가 상대보다 적을 때만 (내 손해가 더 크지 않게)
            return Util.IsOneEnemyBetter() && Bot.GetMonsterCount() <= Enemy.GetMonsterCount();
        }

        bool Fissure()
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup());
        }

        bool SwordsOfRevealingLight()
        {
            if (Enemy.GetMonsters().Any(card => card.IsFacedown()))
                return true;
            return Util.IsOneEnemyBetter(true);
        }

        bool MonsterReborn()
        {
            ClientCard best = Bot.Graveyard.Concat(Enemy.Graveyard)
                .Where(card => card != null && card.IsMonster() && card.Attack >= RebornMinAttack)
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            if (best == null)
                return false;
            AI.SelectCard(best);
            return true;
        }

        bool DianKeto()
        {
            return Bot.LifePoints <= 6000;
        }

        bool DragonCaptureJar()
        {
            if (!UniqueFaceupSpell())
                return false;
            int enemyDragons = Enemy.GetMonsters().Count(card => card.IsFaceup() && card.IsAttack() && card.HasRace(CardRace.Dragon));
            int myDragons = Bot.GetMonsters().Count(card => card.IsFaceup() && card.IsAttack() && card.HasRace(CardRace.Dragon));
            return enemyDragons > myDragons;
        }

        /// <summary>일반 소환할 수 있는 몬스터 중 쓸 만한 것 가운데 공격력이 가장 높은 몬스터만 소환한다.</summary>
        bool StrongestSummon()
        {
            ClientCard best = Duel.MainPhase.SummonableCards
                .Where(WorthSummoning)
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            return best != null && best == Card;
        }

        /// <summary>DefaultMonsterSummon과 같은 판단을 카드 하나에 대해 한다: 하급은 언제나, 상급은 릴리스할 몬스터가 자신보다 약할 때만.</summary>
        bool WorthSummoning(ClientCard card)
        {
            if (card.Level <= 4)
                return true;
            if (Bot.GetMonsters().Any(m => m.IsCode(card.Id) && m.IsFaceup()))
                return false;
            int tributes = card.Level >= 7 ? 2 : 1;
            int weaker = Bot.MonsterZone.Count(m => m != null && m.GetDefensePower() < card.Attack);
            return weaker >= tributes;
        }

        /// <summary>소환할 몬스터가 없을 때 수비력이 가장 높은 하급 몬스터를 세트한다.</summary>
        bool BestDefenderSet()
        {
            if (Duel.MainPhase.SummonableCards.Any(WorthSummoning))
                return false;
            ClientCard best = Duel.MainPhase.MonsterSetableCards
                .Where(card => card.Level <= 4)
                .OrderByDescending(card => card.Defense)
                .FirstOrDefault();
            return best != null && best == Card;
        }
    }
}
