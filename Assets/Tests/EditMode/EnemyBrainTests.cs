using BrokenSovereign.Enemy;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class EnemyBrainTests
    {
        // 감지 8, 추적 포기 16, 끌려가기 한계 20, 공격 사거리 1.8, 도착 0.5
        private readonly EnemyBrain _brain = new EnemyBrain(new EnemyBrain.Settings(8f, 16f, 20f, 1.8f, 0.5f));

        private static EnemySense Sense(float toTarget = 5f, float fromHome = 0f, bool hasTarget = true,
            bool isDead = false, bool wasDamaged = false, bool canAttack = true, bool attackFinished = true)
        {
            return new EnemySense(isDead, hasTarget, toTarget, fromHome, wasDamaged, canAttack, attackFinished);
        }

        [Test]
        public void Idle_TargetInDetectRange_Chase()
        {
            Assert.AreEqual(EnemyState.Chase, _brain.Evaluate(EnemyState.Idle, Sense(toTarget: 7.9f)));
        }

        [Test]
        public void Idle_TargetOutOfDetectRange_StaysIdle()
        {
            Assert.AreEqual(EnemyState.Idle, _brain.Evaluate(EnemyState.Idle, Sense(toTarget: 8.1f)));
        }

        [Test]
        public void Idle_DamagedOutOfDetectRange_Chase()
        {
            Assert.AreEqual(EnemyState.Chase, _brain.Evaluate(EnemyState.Idle, Sense(toTarget: 12f, wasDamaged: true)));
        }

        [Test]
        public void Chase_InAttackRangeAndReady_Attack()
        {
            Assert.AreEqual(EnemyState.Attack, _brain.Evaluate(EnemyState.Chase, Sense(toTarget: 1.5f)));
        }

        [Test]
        public void Chase_InAttackRangeButCooldown_StaysChase()
        {
            Assert.AreEqual(EnemyState.Chase, _brain.Evaluate(EnemyState.Chase, Sense(toTarget: 1.5f, canAttack: false)));
        }

        [Test]
        public void Chase_TargetBeyondLoseRange_Return()
        {
            Assert.AreEqual(EnemyState.Return, _brain.Evaluate(EnemyState.Chase, Sense(toTarget: 16.1f)));
        }

        [Test]
        public void Chase_BeyondLeash_Return()
        {
            Assert.AreEqual(EnemyState.Return, _brain.Evaluate(EnemyState.Chase, Sense(toTarget: 3f, fromHome: 20.1f)));
        }

        [Test]
        public void Chase_TargetDead_Return()
        {
            Assert.AreEqual(EnemyState.Return, _brain.Evaluate(EnemyState.Chase, Sense(hasTarget: false)));
        }

        [Test]
        public void Attack_NotFinished_StaysAttackEvenIfTargetLeaves()
        {
            Assert.AreEqual(EnemyState.Attack,
                _brain.Evaluate(EnemyState.Attack, Sense(toTarget: 30f, attackFinished: false)));
        }

        [Test]
        public void Attack_Finished_BackToChase()
        {
            Assert.AreEqual(EnemyState.Chase, _brain.Evaluate(EnemyState.Attack, Sense(toTarget: 1.5f, canAttack: false)));
        }

        [Test]
        public void Return_IgnoresTargetUntilHome()
        {
            Assert.AreEqual(EnemyState.Return, _brain.Evaluate(EnemyState.Return, Sense(toTarget: 1f, fromHome: 5f)));
            Assert.AreEqual(EnemyState.Idle, _brain.Evaluate(EnemyState.Return, Sense(toTarget: 1f, fromHome: 0.4f)));
        }

        [Test]
        public void AnyState_Dead_Dead_ThenRevive_Idle()
        {
            Assert.AreEqual(EnemyState.Dead, _brain.Evaluate(EnemyState.Attack, Sense(isDead: true)));
            Assert.AreEqual(EnemyState.Idle, _brain.Evaluate(EnemyState.Dead, Sense()));
        }
    }
}
