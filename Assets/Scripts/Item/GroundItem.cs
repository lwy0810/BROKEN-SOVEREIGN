using BrokenSovereign.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 바닥에 떨어진 아이템·금화 (10/26). 처치 보상·테스트 상자가 흩뿌림.
    /// - 자동 습득: 금화, 희귀(파랑) 이하 소모품·재료 → 플레이어가 가까이 오면 바로 가방에
    /// - 클릭 습득: 장비 전부, 영웅(보라) 이상 소모품·재료 → 이름표 클릭 → 걸어가서 줍기 (ItemPicker)
    /// 가방이 가득 차면 바닥에 남습니다.
    /// </summary>
    public class GroundItem : WorldInteractable
    {
        private const float DROP_TIME = 0.45f;
        private const float DROP_HEIGHT = 1.4f;
        private const float AUTO_RANGE = 1.6f;
        private const float RETRY_DELAY = 1f;
        private static readonly Color GOLD_COLOR = new Color(1f, 0.82f, 0.3f);

        private ItemDefinition _item;
        private int _count;
        private int _gold;
        private Vector3 _from;
        private Vector3 _to;
        private float _spawnTime;
        private float _nextAutoTry;
        private Transform _model;

        public ItemDefinition Item => _item;
        public bool IsGold => _gold > 0;

        /// <summary>가까이 가면 자동으로 줍는지: 금화 · 희귀(파랑) 이하 소모품/재료</summary>
        public bool IsAutoPickup => IsGold || (_item != null && !_item.IsEquipment && _item.Rarity <= ItemRarity.Rare);

        private bool Landed => Time.time - _spawnTime >= DROP_TIME;

        public override bool ShowLabel => Landed;

        public override string LabelText => IsGold ? $"{_gold} 골드"
            : _count > 1 ? $"{_item.DisplayName} x{_count}" : _item.DisplayName;

        public override Color LabelColor
        {
            get
            {
                if (IsGold)
                {
                    return GOLD_COLOR;
                }

                return _item.Rarity == ItemRarity.Common ? new Color(0.88f, 0.86f, 0.82f) : ItemVisuals.RarityColor(_item.Rarity);
            }
        }

        public static GroundItem SpawnItem(ItemDefinition item, int count, Vector3 from, Vector3 to)
        {
            if (item == null || count <= 0)
            {
                return null;
            }

            GroundItem drop = Create($"Drop_{item.Id}", from, to);
            drop._item = item;
            drop._count = count;
            drop.BuildModel();
            return drop;
        }

        public static GroundItem SpawnGold(int amount, Vector3 from, Vector3 to)
        {
            if (amount <= 0)
            {
                return null;
            }

            GroundItem drop = Create("Drop_Gold", from, to);
            drop._gold = amount;
            drop.BuildModel();
            return drop;
        }

        /// <summary>from 주변 반경 안의 NavMesh 위 무작위 지점 (흩뿌리기용)</summary>
        public static Vector3 ScatterPoint(Vector3 center, float minRadius, float maxRadius)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(minRadius, maxRadius);
            Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
            return NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : center;
        }

        private static GroundItem Create(string name, Vector3 from, Vector3 to)
        {
            var go = new GameObject(name);
            go.transform.position = from;
            GroundItem drop = go.AddComponent<GroundItem>();
            drop._from = from;
            drop._to = to;
            drop._spawnTime = Time.time;
            return drop;
        }

        // 표시: 금화 = 납작한 원판, 장비 = 상자, 소모품·재료 = 구슬. 희귀 이상 장비는 빛기둥
        private void BuildModel()
        {
            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
            Color color = LabelColor;

            if (IsGold)
            {
                Shape(PrimitiveType.Cylinder, _model, Vector3.zero, new Vector3(0.32f, 0.03f, 0.32f), color);
                return;
            }

            if (_item.IsEquipment)
            {
                Shape(PrimitiveType.Cube, _model, Vector3.zero, new Vector3(0.34f, 0.12f, 0.24f), color);

                if (_item.Rarity >= ItemRarity.Rare)
                {
                    Shape(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.05f, 0.9f, 0.05f), color);
                }
            }
            else
            {
                Shape(PrimitiveType.Sphere, _model, Vector3.zero, Vector3.one * 0.22f, color);
            }
        }

        private void Update()
        {
            float t = (Time.time - _spawnTime) / DROP_TIME;

            if (t < 1f)
            {
                // 튀어 올랐다가 떨어지는 포물선
                Vector3 position = Vector3.Lerp(_from, _to, t);
                position.y += Mathf.Sin(t * Mathf.PI) * DROP_HEIGHT;
                transform.position = position;
                _model.Rotate(0f, 720f * Time.deltaTime, 0f);
                return;
            }

            transform.position = _to;
            _model.localPosition = new Vector3(0f, 0.15f + Mathf.Sin(Time.time * 3f + _spawnTime) * 0.04f, 0f);
            _model.Rotate(0f, 90f * Time.deltaTime, 0f);

            // 자동 습득
            Transform player = Player;

            if (IsAutoPickup && player != null && Time.time >= _nextAutoTry)
            {
                Vector3 offset = player.position - transform.position;
                offset.y = 0f;

                if (offset.magnitude <= AUTO_RANGE)
                {
                    Interact(player.gameObject);
                }
            }
        }

        public override bool Interact(GameObject player)
        {
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;

            if (inventory == null || !Landed)
            {
                return false;
            }

            if (IsGold)
            {
                inventory.AddGold(_gold);
                Destroy(gameObject);
                return true;
            }

            int remaining = inventory.Add(_item, _count);

            if (remaining <= 0)
            {
                Destroy(gameObject);
                return true;
            }

            // 가방이 가득 참: 남은 만큼 바닥에 두고 잠시 뒤 다시 시도
            _count = remaining;
            _nextAutoTry = Time.time + RETRY_DELAY;
            Debug.Log($"GroundItem: 가방이 가득 차 {_item.DisplayName} {remaining}개가 바닥에 남았습니다.");
            return false;
        }
    }
}
