using BrokenSovereign.Combat;
using BrokenSovereign.Player;
using UnityEngine;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 치유사 기능: 체력 전체 회복 + 물약 충전 (비용 없음, 프로토타입).
    /// </summary>
    [RequireComponent(typeof(TownNpc))]
    public class HealerService : MonoBehaviour, INpcService
    {
        [SerializeField] private string _actionLabel = "치료받기";
        [SerializeField] private string _healedLine = "상처가 아물었네. 물약도 가득 채워 두었으니 조심해서 다녀오게.";
        [SerializeField] private string _alreadyFullLine = "멀쩡하구먼. 물약만 채워 주지.";

        public string ActionLabel => _actionLabel;

        public string Use(GameObject user)
        {
            bool healed = false;

            if (user.TryGetComponent(out Health health) && !health.IsDead && health.Current < health.Max)
            {
                health.Heal(health.Max - health.Current);
                healed = true;
            }

            if (user.TryGetComponent(out PotionUser potion))
            {
                potion.Refill();
            }

            return healed ? _healedLine : _alreadyFullLine;
        }
    }
}
