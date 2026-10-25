using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;
using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>플레이 기록 (10/21 캐릭터 창 성장 정보): 누적 처치 수·플레이 시간. HudView가 없으면 붙입니다.</summary>
    public class PlayerRecord : MonoBehaviour
    {
        private Health _health;

        public int Kills { get; private set; }
        public float PlaySeconds { get; private set; }

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            Health.AnyDied += OnAnyDied;
        }

        private void OnDisable()
        {
            Health.AnyDied -= OnAnyDied;
        }

        private void Update()
        {
            PlaySeconds += Time.unscaledDeltaTime;
        }

        private void OnAnyDied(Health victim)
        {
            if (victim != null && victim != _health && victim.GetComponent<EnemyController>() != null)
            {
                Kills++;
            }
        }
    }
}
