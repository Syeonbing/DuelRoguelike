// 스트럭처 덱 —기계의 반란—(상대 덱인 원래 구성과 시작 덱 판)의 카드 판단. StarterDeckExecutor의 일부다.
// 모르는 카드로 두면 "낼 수 있으면 낸다"(DefaultDontChainMyself)로 발동해 크게 손해 보는 카드에 조건을 둔다
// (2026-11-24 화면 밖 측정의 로그: LP를 내고 공격하지 않기, 뽑자마자 리미터 해제, 자기 상급 몬스터 폭파 등).
using System;
using System.Collections.Generic;
using System.Linq;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

namespace DuelRoguelike.AI
{
    internal sealed partial class StarterDeckExecutor
    {
        public static class MachineCardId
        {
            public const int GearGolem = 30190809;                // 기동요새의 기어 골렘
            public const int WeaponChange = 10035717;             // 웨폰 체인지
            public const int DoubleSummon = 43422537;             // 이중소환
            public const int LimiterRemoval = 23171610;           // 리미터 해제
            public const int AncientGearExplosive = 4446672;      // 앤틱 기어 폭탄
            public const int CannonSoldier = 11384280;            // 캐논 솔저
            public const int AncientGearCannon = 80045583;        // 앤틱 기어 캐논
            public const int HeavyMechSupportPlatform = 23265594; // 강화지원메카 헤비웨폰
            public const int Geartown = 37694547;                 // 기어 타운
        }

        /// <summary>「앤틱 기어」 카드군 (CardScripts SET_ANCIENT_GEAR).</summary>
        const int AncientGearSetcode = 0x7;

        const int GearGolemCost = 800;      // 기어 골렘: LP 800을 내고 이번 턴 직접 공격
        const int WeaponChangeCost = 700;   // 웨폰 체인지: 스탠바이 페이즈에 LP 700을 내고 공·수 바꾸기
        const int BurnPerTribute = 500;     // 캐논 솔저·앤틱 기어 캐논: 릴리스 1장마다 500 데미지

        /// <summary>앤틱 기어 폭탄을 "어차피 잃을 몬스터"에 쓸 때 바라는 최소 데미지 (작으면 상대를 끝낼 때를 위해 아껴 둔다).</summary>
        const int ExplosiveDoomedMinDamage = 1000;

        /// <summary>리미터 해제를 발동한 턴 (그 턴 엔드 페이즈에 앞면 기계족이 파괴된다). 없으면 0.</summary>
        int _limiterRemovalTurn;

        /// <summary>기어 골렘의 직접 공격 효과를 쓴 턴. 그 턴에는 공격 표시를 지키고 직접 공격한다.</summary>
        int _gearGolemTurn;

        void AddMachineReVoltExecutors()
        {
            AddExecutor(ExecutorType.Activate, MachineCardId.DoubleSummon, DoubleSummon);
            AddExecutor(ExecutorType.Activate, MachineCardId.LimiterRemoval, LimiterRemoval);
            AddExecutor(ExecutorType.Activate, MachineCardId.AncientGearExplosive, AncientGearExplosive);
            AddExecutor(ExecutorType.Activate, MachineCardId.CannonSoldier, TributeBurn);
            AddExecutor(ExecutorType.Activate, MachineCardId.AncientGearCannon, TributeBurn);
            AddExecutor(ExecutorType.Activate, MachineCardId.WeaponChange, WeaponChange);
            AddExecutor(ExecutorType.Activate, MachineCardId.GearGolem, GearGolem);
            AddExecutor(ExecutorType.Activate, MachineCardId.HeavyMechSupportPlatform, HeavyMechSupportPlatform);
        }

        void ResetMachineReVoltState()
        {
            _limiterRemovalTurn = 0;
            _gearGolemTurn = 0;
        }

        void OnBotChaining(ClientCard card)
        {
            if (card.IsCode(MachineCardId.LimiterRemoval))
                _limiterRemovalTurn = Duel.Turn;
        }

        /// <summary>앞면 「기어 타운」이 어느 쪽 필드 존에든 있는지.</summary>
        bool GeartownActive()
        {
            return IsFaceupGeartown(Bot.SpellZone[5]) || IsFaceupGeartown(Enemy.SpellZone[5]);
        }

        static bool IsFaceupGeartown(ClientCard card)
        {
            return card != null && card.IsCode(MachineCardId.Geartown) && card.IsFaceup() && !card.IsDisabled();
        }

        /// <summary>이 몬스터가 리미터 해제로 이번 턴 엔드 페이즈에 파괴되는지 (메인 페이즈 2에서만 본다: 배틀 페이즈에는 아직 공격에 쓴다).</summary>
        bool DoomedThisTurn(ClientCard monster)
        {
            return _limiterRemovalTurn == Duel.Turn && Duel.Player == 0 && Duel.Phase == DuelPhase.Main2
                && monster.IsFaceup() && monster.HasRace(CardRace.Machine);
        }

        // ── 기동요새의 기어 골렘 ──

        bool GearGolem()
        {
            // 메인 페이즈 1에 LP 800: 이번 턴 직접 공격할 수 있다. 공격 표시이고, 이 직접 공격으로 이번 턴에 이길 때만 쓴다.
            if (!Card.IsAttack() || !GearGolemFinishes(Card))
                return false;
            _gearGolemTurn = Duel.Turn;
            return true;
        }

        /// <summary>기어 골렘이 직접 공격하면 이번 턴에 상대 LP를 0으로 만들 수 있는지 (메인 페이즈 1, 공격 표시라고 보고).</summary>
        bool GearGolemFinishes(ClientCard golem)
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 || !Duel.MainPhase.CanBattlePhase)
                return false;
            if (golem.IsDisabled() || golem.Attacked || Bot.LifePoints <= GearGolemCost)
                return false;
            if (Enemy.GetMonsterCount() == 0)
                return false; // 상대 몬스터가 없으면 효과 없이도 직접 공격한다
            var others = ReadyAttackers().Where(m => m != golem);
            return golem.Attack + EstimateBattleDamage(others, m => m.Attack) >= Enemy.LifePoints;
        }

        /// <summary>표시 형식의 카드별 판단. 정하지 않으면 null (DefaultMonsterRepos로 넘긴다).</summary>
        bool? MachineReVoltRepos()
        {
            if (!Card.IsCode(MachineCardId.GearGolem))
                return null;
            // 직접 공격을 켠 턴에는 공격 표시를 지킨다
            if (_gearGolemTurn == Duel.Turn)
                return Card.IsDefense();
            // 직접 공격으로 이길 수 있으면 공격 표시로 바꾼 뒤 효과를 쓴다
            if (Card.IsDefense() && GearGolemFinishes(Card))
                return true;
            return null;
        }

        /// <summary>LP를 내고 직접 공격을 켠 기어 골렘은 상대 몬스터가 있어도 직접 공격한다.</summary>
        bool ShouldAttackDirectly(ClientCard attacker)
        {
            return attacker.IsCode(MachineCardId.GearGolem) && attacker.CanDirectAttack && _gearGolemTurn == Duel.Turn;
        }

        /// <summary>남은 공격의 데미지 어림. 직접 공격을 켠 기어 골렘은 상대 몬스터와 상관없이 직접 공격으로 센다.</summary>
        int EstimateRemainingDamage(List<ClientCard> attackers, Func<ClientCard, int> attackOf)
        {
            int direct = attackers.Where(ShouldAttackDirectly).Sum(attackOf);
            return direct + EstimateBattleDamage(attackers.Where(m => !ShouldAttackDirectly(m)), attackOf);
        }

        // ── 웨폰 체인지 ──

        bool WeaponChange()
        {
            if (Card.Location == CardLocation.SpellZone && Card.IsFaceup())
            {
                // 내 스탠바이 페이즈: LP 700으로 기계족·전사족 1장의 공·수를 바꾼다 (상대 턴 끝까지).
                // 수비력이 700 이상 높은 공격 표시 몬스터만 (수비 표시 몬스터는 수비력이 낮아진다).
                if (!CanPayLp(WeaponChangeCost))
                    return false;
                ClientCard target = WeaponChangeTarget(attackOnly: true);
                if (target == null)
                    return false;
                AI.SelectCard(target);
                return true;
            }
            // 카드 발동 (비용 없음): 바꿀 만한 몬스터가 필드에 있을 때만 앞면으로 둔다
            return WeaponChangeTarget(attackOnly: false) != null;
        }

        ClientCard WeaponChangeTarget(bool attackOnly)
        {
            return Bot.GetMonsters()
                .Where(m => m.IsFaceup() && (!attackOnly || m.IsAttack())
                    && (m.HasRace(CardRace.Machine) || m.HasRace(CardRace.Warrior))
                    && m.Defense - m.Attack >= WeaponChangeCost)
                .OrderByDescending(m => m.Defense - m.Attack)
                .FirstOrDefault();
        }

        // ── 이중소환 ──

        bool DoubleSummon()
        {
            // 이번 턴 일반 소환을 이미 했고 (엔진이 일반 소환·세트할 카드를 주지 않는다), 패에 하나 더 낼 몬스터가 있을 때만
            if (Duel.MainPhase.SummonableCards.Count > 0 || Duel.MainPhase.MonsterSetableCards.Count > 0)
                return false;
            return Bot.Hand.Any(c => c != Card && c.IsMonster() && CanSummonWithExtraNormalSummon(c));
        }

        /// <summary>일반 소환 권리가 하나 더 있으면 이 패의 몬스터를 낼 만한지 (빈 몬스터 존이나 약한 릴리스가 있는지).</summary>
        bool CanSummonWithExtraNormalSummon(ClientCard card)
        {
            if (TributesNeeded(card) == 0)
                return Bot.GetMonstersInMainZone().Count < 5;
            return WorthSummoning(card);
        }

        // ── 리미터 해제 ──

        bool LimiterRemoval()
        {
            var machines = Bot.GetMonsters().Where(m => m.IsFaceup() && m.HasRace(CardRace.Machine)).ToList();
            if (machines.Count == 0)
                return false;
            // 엔진은 배틀 페이즈 시작만 페이즈 메시지로 보낸다 (배틀 스텝·데미지 스텝은 따로 오지 않는다).
            // 그래서 배틀 페이즈 동안 Duel.Phase는 BattleStart이고, 공격 선언부터 데미지 계산까지는 공격받는 쪽의 UnderAttack이 켜져 있다.
            if (Duel.Phase != DuelPhase.BattleStart)
                return false;
            if (Duel.Player == 0)
            {
                // 내 배틀 페이즈 (공격과 공격 사이): 앞면 기계족 공격력 2배로 이번 턴에 이기게 될 때만 (엔드 페이즈에 내 기계족이 모두 파괴된다).
                // 배틀 커맨드를 받기 전에는 공격할 수 있는 몬스터를 모르고 (공격 금지 효과 등), 방금 공격한 몬스터는 다음 커맨드 전까지 Attacked가 꺼져 있다.
                if (!BattleCommandSeenThisPhase() || Enemy.UnderAttack)
                    return false;
                var attackers = ReadyAttackers().Where(m => m != Bot.BattlingMonster).ToList();
                if (attackers.Count == 0)
                    return false;
                int now = EstimateRemainingDamage(attackers, m => m.Attack);
                int doubled = EstimateRemainingDamage(attackers, m => machines.Contains(m) ? m.Attack * 2 : m.Attack);
                return now < Enemy.LifePoints && doubled >= Enemy.LifePoints;
            }
            // 상대의 공격: 공격받은 내 공격 표시 기계족이 2배로 공격 몬스터를 이기고, 그 밖에 엔드 페이즈에 잃을 앞면 기계족이 없을 때.
            // 공격받은 몬스터는 어차피 전투로 잃으므로, 리미터 해제 1장으로 공격 몬스터를 처리하고 데미지를 막는다.
            if (!Bot.UnderAttack)
                return false;
            ClientCard mine = Bot.BattlingMonster;
            ClientCard attacker = Enemy.BattlingMonster;
            if (mine == null || attacker == null || !mine.IsAttack() || !machines.Contains(mine) || machines.Count > 1)
                return false;
            return attacker.Attack >= mine.Attack && attacker.Attack < mine.Attack * 2;
        }

        // ── 앤틱 기어 폭탄 ──

        bool AncientGearExplosive()
        {
            // 내 「앤틱 기어」 몬스터 1장을 파괴해 원래 공격력 절반의 데미지.
            // 상대를 끝낼 때나, 리미터 해제로 엔드 페이즈에 어차피 파괴될 몬스터일 때만.
            var gears = Bot.GetMonsters()
                .Where(m => m.IsFaceup() && m.HasSetcode(AncientGearSetcode))
                .OrderByDescending(m => m.BaseAttack)
                .ToList();
            ClientCard finisher = gears.FirstOrDefault(m => m.BaseAttack / 2 >= Enemy.LifePoints);
            ClientCard doomed = gears.FirstOrDefault(m => DoomedThisTurn(m) && m.BaseAttack / 2 >= ExplosiveDoomedMinDamage);
            ClientCard target = finisher ?? doomed;
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        // ── 캐논 솔저, 앤틱 기어 캐논 ──

        bool TributeBurn()
        {
            // 캐논 솔저(몬스터 1장 릴리스)·앤틱 기어 캐논(자신을 릴리스): 500 데미지.
            // 되풀이해 상대를 끝낼 수 있을 때나, 리미터 해제로 어차피 파괴될 몬스터만 릴리스할 때만.
            bool isCannon = Card.IsCode(MachineCardId.AncientGearCannon);
            var monsters = Bot.MonsterZone.Where(m => m != null).ToList();
            bool use = Enemy.LifePoints <= BurnPotential()
                || (isCannon ? DoomedThisTurn(Card) : monsters.Count > 0 && monsters.All(DoomedThisTurn));
            if (!use)
                return false;
            if (!isCannon)
            {
                // 캐논 솔저의 릴리스: 다른 몬스터 중 공격력이 가장 낮은 것부터, 캐논 솔저 자신은 마지막에 (되풀이해 쓸 수 있게)
                ClientCard tribute = monsters.Where(m => m != Card).OrderBy(m => m.Attack).FirstOrDefault() ?? Card;
                AI.SelectCard(tribute);
            }
            return true;
        }

        /// <summary>캐논 솔저·앤틱 기어 캐논을 되풀이해 이번 메인 페이즈에 줄 수 있는 데미지.</summary>
        int BurnPotential()
        {
            var monsters = Bot.MonsterZone.Where(m => m != null).ToList();
            bool soldier = monsters.Any(m => m.IsCode(MachineCardId.CannonSoldier) && m.IsFaceup() && !m.IsDisabled());
            if (soldier)
                return BurnPerTribute * monsters.Count;
            return BurnPerTribute * monsters.Count(m => m.IsCode(MachineCardId.AncientGearCannon) && m.IsFaceup() && !m.IsDisabled());
        }

        // ── 강화지원메카 헤비웨폰 ──

        bool HeavyMechSupportPlatform()
        {
            // 유니온: 몬스터 존에서는 가장 센 앞면 기계족에 장착한다 (공·수 500, 파괴 대신).
            // 장착 중에 떼어 내 특수 소환하지는 않는다 (공 500으로 공격 표시가 되어 공격받는다).
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            // 릴리스가 필요한 일반 소환을 할 수 있으면 먼저 소환한다 (장착하면 몬스터 존에서 빠져 릴리스가 모자랄 수 있다)
            if (Duel.MainPhase.SummonableCards.Any(c => TributesNeeded(c) > 0 && WorthSummoning(c)))
                return false;
            ClientCard host = Bot.GetMonsters()
                .Where(m => m != Card && m.IsFaceup() && m.HasRace(CardRace.Machine))
                .OrderByDescending(m => m.Attack)
                .FirstOrDefault();
            if (host == null)
                return false;
            AI.SelectCard(host);
            return true;
        }
    }
}
