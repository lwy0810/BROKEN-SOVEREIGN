using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;
using UnityEngine;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 적 처치 보상 (10/21, 10/26 바닥 드랍): 적이 죽은 자리 주변에 아이템·금화를 흩뿌립니다 (GroundItem).
    /// 금화·희귀 이하 소모품/재료는 가까이 가면 자동 습득, 장비는 이름표를 클릭해서 습득.
    /// 테스트 상자(LootChest)를 시작 위치 앞에 놓습니다 (_spawnTestChest).
    /// 등급별: 일반 = 골드 + 확률 장비·물약·재료 / 정예 = 장비 1(고급 이상) + 재료 / 보스 = 장비 2(희귀 이상) + 골드 많이.
    /// 시작 시 기본 장비 몇 개·물약·골드를 지급합니다.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [Tooltip("시작 시 플레이어 앞에 테스트 보물상자 생성 (10/26, 테스트용)")]
        [SerializeField] private bool _spawnTestChest = true;

        private Inventory _inventory;
        private Vector3 _dropCenter;
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

            if (_spawnTestChest)
            {
                LootChest.SpawnNear(transform);
            }
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
            _inventory.Add(ItemCatalog.Get(ItemCatalog.ELIXIR_ID), 2, false);
            _inventory.AddGold(50, false);
        }

        private void OnAnyDied(Health victim)
        {
            if (_inventory == null || victim == null || victim == _health || !victim.TryGetComponent(out EnemyController enemy))
            {
                return;
            }

            EnemyRank rank = enemy.Definition != null ? enemy.Definition.Rank : EnemyRank.Normal;
            _dropCenter = victim.transform.position;

            switch (rank)
            {
                case EnemyRank.Boss:
                    DropGold(Random.Range(200, 301));
                    GiveEquipment(RollRarity(0f, 0f, 30f, 50f, 20f));
                    GiveEquipment(RollRarity(0f, 0f, 30f, 50f, 20f));
                    DropItem(ItemCatalog.Get("mat_essence"), 1);
                    break;

                case EnemyRank.Elite:
                    DropGold(Random.Range(30, 61));
                    GiveEquipment(RollRarity(0f, 40f, 45f, 15f, 0f));
                    DropItem(ItemCatalog.Get("mat_crystal"), 1);
                    break;

                default:
                    DropGold(Random.Range(3, 11));

                    if (Random.value < 0.3f)
                    {
                        GiveEquipment(RollRarity(60f, 30f, 9f, 1f, 0f));
                    }

                    if (Random.value < 0.25f)
                    {
                        DropItem(ItemCatalog.Get(ItemCatalog.POTION_ID), 1);
                    }

                    if (Random.value < 0.2f)
                    {
                        DropItem(ItemCatalog.RandomMaterial(ItemRarity.Uncommon), 1);
                    }

                    break;
            }
        }

        private void GiveEquipment(ItemRarity rarity)
        {
            DropItem(ItemCatalog.RandomEquipment(rarity), 1);
        }

        // 바닥에 흩뿌림 (죽은 자리 주변)
        private void DropItem(ItemDefinition item, int count)
        {
            GroundItem.SpawnItem(item, count, _dropCenter + Vector3.up * 0.8f, GroundItem.ScatterPoint(_dropCenter, 0.6f, 1.8f));
        }

        private void DropGold(int amount)
        {
            GroundItem.SpawnGold(amount, _dropCenter + Vector3.up * 0.8f, GroundItem.ScatterPoint(_dropCenter, 0.6f, 1.8f));
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
