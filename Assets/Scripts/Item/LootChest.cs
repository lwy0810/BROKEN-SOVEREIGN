using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 테스트용 보물상자 (10/26). 게임 시작 시 플레이어 근처에 생김 (LootDropper).
    /// 이름표를 클릭하면 걸어가서 열고, 여러 종류의 아이템을 주변에 흩뿌립니다:
    /// 장비 희귀도별 1개씩(일반~전설) · 물약 · 영약 · 재료(자동/클릭 습득 확인용) · 금화 2무더기.
    /// 열고 나서 몇 초 뒤 다시 열 수 있습니다.
    /// </summary>
    public class LootChest : WorldInteractable
    {
        private const float REOPEN_DELAY = 3f;

        private float _readyTime;
        private Transform _lid;

        private float Remaining => Mathf.Max(0f, _readyTime - Time.time);

        public override string LabelText => Remaining > 0f ? $"테스트 상자 ({Mathf.CeilToInt(Remaining)}초)" : "테스트 상자 [클릭해서 열기]";

        public override Color LabelColor => new Color(1f, 0.84f, 0.54f);

        public override float InteractRange => 1.8f;

        public override float LabelHeight => 1.3f;

        /// <summary>웨이포인트와 이 거리(m) 이상 떨어지게 (흩뿌린 아이템을 주우려다 웨이포인트를 누르지 않도록)</summary>
        private const float WAYPOINT_CLEARANCE = 7f;

        /// <summary>
        /// 화면 위쪽(카메라가 바라보는 쪽)으로 플레이어에서 떨어진 NavMesh 위에 상자를 놓습니다 (10/26: 위로 올림).
        /// 웨이포인트 근처면 각도를 바꿔 가며 떨어진 자리를 찾습니다.
        /// </summary>
        public static LootChest SpawnNear(Transform player, float distance = 6f)
        {
            Camera camera = Camera.main;
            Vector3 up = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : player.forward;
            up = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.forward;
            Vector3 point = player.position + up * distance;
            float best = -1f;

            // 화면 위쪽 → 좌우로 조금씩 틀어 가며 웨이포인트에서 가장 먼 자리
            foreach (float angle in new[] { 0f, 25f, -25f, 50f, -50f, 75f, -75f })
            {
                Vector3 candidate = player.position + Quaternion.Euler(0f, angle, 0f) * up * distance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    continue;
                }

                float clearance = NearestWaypointDistance(hit.position);

                if (clearance > best)
                {
                    best = clearance;
                    point = hit.position;
                }

                if (clearance >= WAYPOINT_CLEARANCE)
                {
                    break;
                }
            }

            var go = new GameObject("TestLootChest");
            go.transform.position = point;
            go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(player.position - point, Vector3.up).normalized + Vector3.forward * 0.0001f);
            LootChest chest = go.AddComponent<LootChest>();
            chest.BuildModel();
            return chest;
        }

        private static float NearestWaypointDistance(Vector3 point)
        {
            float nearest = float.PositiveInfinity;

            foreach (BrokenSovereign.Town.Waypoint waypoint in BrokenSovereign.Town.Waypoint.All)
            {
                if (waypoint != null)
                {
                    nearest = Mathf.Min(nearest, Vector3.Distance(point, waypoint.transform.position));
                }
            }

            return nearest;
        }

        private void BuildModel()
        {
            Color wood = new Color(0.42f, 0.26f, 0.14f);
            Color gold = new Color(0.93f, 0.72f, 0.3f);
            Shape(PrimitiveType.Cube, transform, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 0.65f), wood);
            Shape(PrimitiveType.Cube, transform, new Vector3(0f, 0.3f, 0.33f), new Vector3(0.16f, 0.22f, 0.02f), gold);
            _lid = new GameObject("Lid").transform;
            _lid.SetParent(transform, false);
            _lid.localPosition = new Vector3(0f, 0.6f, -0.33f);
            Shape(PrimitiveType.Cube, _lid, new Vector3(0f, 0.09f, 0.33f), new Vector3(1.04f, 0.18f, 0.69f), gold);
        }

        private void Update()
        {
            // 열린 동안 뚜껑을 젖힘
            float open = Remaining > 0f ? 1f : 0f;
            Quaternion target = Quaternion.Euler(-open * 70f, 0f, 0f);
            _lid.localRotation = Quaternion.Slerp(_lid.localRotation, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        public override bool Interact(GameObject player)
        {
            if (Remaining > 0f)
            {
                return false;
            }

            _readyTime = Time.time + REOPEN_DELAY;
            Vector3 from = transform.position + Vector3.up * 0.7f;

            // 장비: 희귀도마다 1개 (클릭 습득)
            for (int r = 0; r <= (int)ItemRarity.Legendary; r++)
            {
                Drop(ItemCatalog.RandomEquipment((ItemRarity)r), 1, from);
            }

            // 소모품: 물약 · 영약 (희귀 이하면 자동 습득)
            Drop(ItemCatalog.Get(ItemCatalog.POTION_ID), 2, from);
            Drop(ItemCatalog.Get(ItemCatalog.ELIXIR_ID), 1, from);

            // 재료: 낮은 것 2개 + 높은 것 1개 (영웅 이상이면 클릭 습득)
            Drop(ItemCatalog.RandomMaterial(ItemRarity.Uncommon), 2, from);
            Drop(ItemCatalog.RandomMaterial(ItemRarity.Rare), 1, from);
            Drop(ItemCatalog.RandomMaterial(ItemRarity.Legendary), 1, from);

            // 금화
            GroundItem.SpawnGold(Random.Range(20, 61), from, GroundItem.ScatterPoint(transform.position, 1.2f, 2.6f));
            GroundItem.SpawnGold(Random.Range(20, 61), from, GroundItem.ScatterPoint(transform.position, 1.2f, 2.6f));
            return true;
        }

        private void Drop(ItemDefinition item, int count, Vector3 from)
        {
            GroundItem.SpawnItem(item, count, from, GroundItem.ScatterPoint(transform.position, 1.2f, 2.8f));
        }
    }
}
