using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;
using UnityEngine;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 적 처치 보상 (10/21): 적이 죽으면 바로 가방에 넣고 골드를 더합니다 (바닥 줍기 없음, 획득 알림으로 표시).
    /// 등급별: 일반 = 골드 + 확률 장비·물약·재료 / 정예 = 장비 1(고급 이상) + 재료 / 보스 = 장비 2(희귀 이상) + 골드 많이.
    /// 시작 시 기본 장비 몇 개·물약·골드를 지급합니다.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        private Inventory _inventory;
        private Health _health;
        private bool _granted;

        private void Awake()
        {
            _inventory = GetComponent<Inventory>();
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

        private void Start()
        {
            GrantStarter();
        }

        private void GrantStarter()
        {
            if (_granted || _inventory == null)
            {
                return;
            }

            _granted = true;
            string[] starter = { "mace_old", "helm_leather", "armor_cloth", "boots_leather", "ring_copper" };

            foreach (string id in starter)
            {
                _inventory.Add(ItemCatalog.Get(id), 1, false);
            }

            _inventory.Add(ItemCatalog.Get(ItemCatalog.POTION_ID), 3, false);
            _inventory.AddGold(50, false);
        }

        private void OnAnyDied(Health victim)
        {
            if (_inventory == null || victim == null || victim == _health || !victim.TryGetComponent(out EnemyController enemy))
            {
                return;
            }

            EnemyRank rank = enemy.Definition != null ? enemy.Definition.Rank : EnemyRank.Normal;

            switch (rank)
            {
                case EnemyRank.Boss:
                    _inventory.AddGold(Random.Range(200, 301));
                    GiveEquipment(RollRarity(0f, 0f, 30f, 50f, 20f));
                    GiveEquipment(RollRarity(0f, 0f, 30f, 50f, 20f));
                    _inventory.Add(ItemCatalog.Get("mat_essence"), 1);
                    break;

                case EnemyRank.Elite:
                    _inventory.AddGold(Random.Range(30, 61));
                    GiveEquipment(RollRarity(0f, 40f, 45f, 15f, 0f));
                    _inventory.Add(ItemCatalog.Get("mat_crystal"), 1);
                    break;

                default:
                    _inventory.AddGold(Random.Range(3, 11));

                    if (Random.value < 0.3f)
                    {
                        GiveEquipment(RollRarity(60f, 30f, 9f, 1f, 0f));
                    }

                    if (Random.value < 0.25f)
                    {
                        _inventory.Add(ItemCatalog.Get(ItemCatalog.POTION_ID), 1);
                    }

                    if (Random.value < 0.2f)
                    {
                        _inventory.Add(ItemCatalog.RandomMaterial(ItemRarity.Uncommon), 1);
                    }

                    break;
            }
        }

        private void GiveEquipment(ItemRarity rarity)
        {
            _inventory.Add(ItemCatalog.RandomEquipment(rarity), 1);
        }

        // 가중치 (일반·고급·희귀·영웅·전설)
        private static ItemRarity RollRarity(params float[] weights)
        {
            float total = 0f;

            foreach (float w in weights)
            {
                total += w;
            }

            float roll = Random.value * total;

            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];

                if (roll < 0f)
                {
                    return (ItemRarity)i;
                }
            }

            return ItemRarity.Common;
        }
    }
}
