// 스타터 덱(유희·카이바·조이·페가수스 편)과 스트럭처 덱(열풍의 패왕, 기계의 반란, 암흑의 주술)을 위한 범용 WindBot Executor.
// WindBot에는 이 덱들을 위한 Executor가 없다. DefaultExecutor(WindBot Ignite, AGPL-3.0-or-later)의 기본 판단을 바탕으로,
// 아는 카드는 카드별 조건으로 쓰고, 모르는 카드는 "자기 체인에 체인하지 않기" 기본 규칙으로 발동한다.
// 덱 하나에만 있는 카드의 조건은 덱별 partial 파일에 둔다 (StarterDeckExecutor.MachineReVolt.cs).
using System;
using System.Collections.Generic;
using System.Linq;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

namespace DuelRoguelike.AI
{
    internal sealed partial class StarterDeckExecutor : DefaultExecutor
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
            public const int HeavyStorm = 19613556;          // 태풍
            public const int MysticalSpaceTyphoon = 5318639; // 싸이크론
            public const int GiantTrunade = 42703248;        // 허리케인
        }

        /// <summary>죽은 자의 소생으로 되살릴 만한 최소 공격력.</summary>
        const int RebornMinAttack = 1500;

        /// <summary>LP를 내는 효과는 내고 난 LP가 이 값보다 적어지면 쓰지 않는다 (이번 턴에 이기는 경우는 카드별로 따로 본다).</summary>
        const int LpFloor = 2000;

        /// <summary>데미지 어림에서 정체를 모르는 뒷면 수비 몬스터의 수비력으로 보는 값 (넉넉하게 잡는다).</summary>
        const int UnknownDefense = 2000;

        public StarterDeckExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 마법: 아는 카드는 조건을 따진다 (앞에 둘수록 먼저 쓴다)
            AddExecutor(ExecutorType.Activate, CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, CardId.GiantTrunade, GiantTrunade);
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

            // 덱별 카드 (기계의 반란)
            AddMachineReVoltExecutors();

            // 특수 소환 절차가 있는 몬스터, 모르는 카드의 효과
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);

            // 소환: 가장 센 몬스터부터. 못 이길 것 같으면 DefaultExecutor가 세트를 고른다.
            AddExecutor(ExecutorType.SummonOrSet, StrongestSummon);
            AddExecutor(ExecutorType.MonsterSet, BestDefenderSet);
            AddExecutor(ExecutorType.Repos, MonsterRepos);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
        }

        public override void OnNewTurn()
        {
            if (Duel.Turn <= 1)
                ResetMachineReVoltState();
            base.OnNewTurn();
        }

        public override void OnChaining(int player, ClientCard card)
        {
            if (player == 0 && card != null)
                OnBotChaining(card);
            base.OnChaining(player, card);
        }

        public override void OnNewPhase()
        {
            _battlePhaseAtPhaseStart = Duel.BattlePhase;
            base.OnNewPhase();
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (ShouldAttackDirectly(attacker))
                return AI.Attack(attacker, null);
            if (TryFinishingAttack(attacker, out BattlePhaseAction finishing))
                return finishing;
            return base.OnSelectAttackTarget(attacker, defenders);
        }

        /// <summary>페이즈가 시작될 때의 Duel.BattlePhase. 어댑터는 배틀 커맨드마다 새 BattlePhase를 만든다.</summary>
        BattlePhase _battlePhaseAtPhaseStart;

        /// <summary>
        /// 이번 배틀 페이즈에 엔진의 배틀 커맨드를 한 번 이상 받았는지. 받기 전(배틀 페이즈 시작 때의 체인 등)에는
        /// 어떤 몬스터가 공격할 수 있는지(Attacked, CanDirectAttack)를 아직 모른다.
        /// </summary>
        bool BattleCommandSeenThisPhase()
        {
            return Duel.Phase == DuelPhase.BattleStart && !ReferenceEquals(Duel.BattlePhase, _battlePhaseAtPhaseStart);
        }

        /// <summary>
        /// 남은 공격으로 이번 턴에 이길 수 있으면 PlanAttacks의 순서를 따른다 (기본 판단은 가장 센 몬스터로 가장 센 상대를 쳐서
        /// 이길 수 있는 턴을 놓친다). 계획에서 직접 공격할 몬스터는 상대 몬스터가 치워질 때까지 기다린다 (action = null).
        /// 계획대로 하지 않아도 되면 false.
        /// </summary>
        bool TryFinishingAttack(ClientCard attacker, out BattlePhaseAction action)
        {
            action = null;
            var ready = ReadyAttackers().ToList();
            if (!ready.Contains(attacker))
                return false;
            int direct = ready.Where(ShouldAttackDirectly).Sum(m => m.Attack);
            var plan = PlanAttacks(ready.Where(m => !ShouldAttackDirectly(m)), m => m.Attack, out int damage);
            if (direct + damage < Enemy.LifePoints)
                return false;
            int at = plan.FindIndex(p => p.Attacker == attacker);
            if (at < 0)
                return false;
            ClientCard target = plan[at].Target;
            if (target != null)
                action = AI.Attack(attacker, target);
            else if (Enemy.GetMonsterCount() == 0)
                action = AI.Attack(attacker, null);
            return true;
        }

        bool DarkHole()
        {
            // 상대가 더 세고, 내 필드 몬스터가 상대보다 적을 때만 (내 손해가 더 크지 않게)
            return Util.IsOneEnemyBetter() && Bot.GetMonsterCount() <= Enemy.GetMonsterCount();
        }

        bool GiantTrunade()
        {
            // 양쪽 마법·함정을 모두 패로 되돌린다: 상대 쪽이 더 많을 때만 (필드에서 발동하는 이 카드는 빼고 센다)
            int mine = Bot.GetSpellCount() - (Card.Location == CardLocation.SpellZone ? 1 : 0);
            return Enemy.GetSpellCount() > mine;
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

        /// <summary>DefaultMonsterSummon과 같은 판단을 카드 하나에 대해 한다: 릴리스가 없으면 언제나, 있으면 릴리스할 몬스터가 자신보다 약할 때만.</summary>
        bool WorthSummoning(ClientCard card)
        {
            int tributes = TributesNeeded(card);
            if (tributes == 0)
                return true;
            if (Bot.GetMonsters().Any(m => m.IsCode(card.Id) && m.IsFaceup()))
                return false;
            int weaker = Bot.MonsterZone.Count(m => m != null && m.GetDefensePower() < card.Attack);
            return weaker >= tributes;
        }

        /// <summary>
        /// 일반 소환에 필요한 릴리스 수: 레벨 5·6은 1장, 7 이상은 2장.
        /// 필드 존에 앞면 「기어 타운」이 있으면 「앤틱 기어」 몬스터는 1장 줄어든다 (기어 타운은 양쪽 패에 적용된다).
        /// </summary>
        int TributesNeeded(ClientCard card)
        {
            int tributes = card.Level >= 7 ? 2 : card.Level >= 5 ? 1 : 0;
            if (tributes > 0 && card.HasSetcode(AncientGearSetcode) && GeartownActive())
                tributes--;
            return tributes;
        }

        /// <summary>소환할 몬스터가 없을 때 수비력이 가장 높은 몬스터(릴리스 없이 낼 수 있는)를 세트한다.</summary>
        bool BestDefenderSet()
        {
            if (Duel.MainPhase.SummonableCards.Any(WorthSummoning))
                return false;
            ClientCard best = Duel.MainPhase.MonsterSetableCards
                .Where(card => TributesNeeded(card) == 0)
                .OrderByDescending(card => card.Defense)
                .FirstOrDefault();
            return best != null && best == Card;
        }

        /// <summary>표시 형식 변경: 카드별 판단(기어 골렘의 직접 공격)이 없으면 DefaultMonsterRepos.</summary>
        bool MonsterRepos()
        {
            bool? decided = MachineReVoltRepos();
            return decided ?? DefaultMonsterRepos();
        }

        /// <summary>이번 턴에 이 LP를 내도 LpFloor 이상 남는지.</summary>
        bool CanPayLp(int cost)
        {
            return Bot.LifePoints - cost >= LpFloor;
        }

        /// <summary>이번 턴에 아직 공격할 수 있는 내 앞면 공격 표시 몬스터 (배틀 페이즈 전이면 모두).</summary>
        IEnumerable<ClientCard> ReadyAttackers()
        {
            return Bot.GetMonsters().Where(m => m.IsFaceup() && m.IsAttack() && !m.Attacked);
        }

        /// <summary>
        /// 이 몬스터들의 공격 계획과 그 전투 데미지 어림 (관통·효과 데미지는 세지 않아 적게 나온다).
        /// 상대 몬스터를 센 것부터 하나씩, 이길 수 있는 가장 약한 공격 몬스터로 치운다. 다 치우면 남은 몬스터는 직접 공격한다 (Target = null).
        /// 치우지 못하는 몬스터가 있으면 그때까지 치운 공격 표시 몬스터를 넘어 준 데미지만 세고, 남은 공격 몬스터는 계획에 넣지 않는다.
        /// </summary>
        List<(ClientCard Attacker, ClientCard Target)> PlanAttacks(IEnumerable<ClientCard> attackers, Func<ClientCard, int> attackOf, out int damage)
        {
            var pool = attackers.Select(m => (Card: m, Power: attackOf(m))).OrderBy(a => a.Power).ToList();
            var blockers = Enemy.MonsterZone.Where(m => m != null).OrderByDescending(BlockPower).ToList();
            var plan = new List<(ClientCard Attacker, ClientCard Target)>();
            damage = 0;
            foreach (ClientCard blocker in blockers)
            {
                int power = BlockPower(blocker);
                int index = pool.FindIndex(a => a.Power > power);
                if (index < 0)
                    return plan;
                if (blocker.IsFaceup() && blocker.IsAttack())
                    damage += pool[index].Power - power;
                plan.Add((pool[index].Card, blocker));
                pool.RemoveAt(index);
            }
            foreach (var a in pool)
            {
                damage += a.Power;
                plan.Add((a.Card, null));
            }
            return plan;
        }

        /// <summary>PlanAttacks의 데미지 어림만.</summary>
        int EstimateBattleDamage(IEnumerable<ClientCard> attackers, Func<ClientCard, int> attackOf)
        {
            PlanAttacks(attackers, attackOf, out int damage);
            return damage;
        }

        /// <summary>상대 몬스터를 전투로 치우는 데 넘어야 하는 값. WindBot이 모르는 뒷면 몬스터는 UnknownDefense로 본다.</summary>
        static int BlockPower(ClientCard monster)
        {
            if (monster.IsFacedown())
                return monster.Data == null ? UnknownDefense : monster.Defense;
            return monster.GetDefensePower();
        }
    }
}
