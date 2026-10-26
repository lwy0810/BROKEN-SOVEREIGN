using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 오픈 필드 정사각형 보스방 (10/25): 돌벽 + 입구 패루·석등 + 모서리 석주, 계단 모형.
    /// 계단: Props/PropBossStairs.prefab(VARCO)이 있으면 길에 맞춰 늘려 배치, 없으면 임시 돌계단 (충돌 없음, 걷기는 지형 경사).
    /// </summary>
    public static partial class LevelTools
    {
        private const string BOSS_STAIRS_PREFAB = PROP_PREFABS + "/PropBossStairs.prefab";
        private const float BOSS_STEP_RISE = 0.3f;

        private static void PlaceBossRoom(LevelSpec spec, Room room, Ground ground, Transform parent)
        {
            GameObject wall = EnsureTownWall();
            GameObject paifang = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropPaifang.prefab");
            GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStoneLantern.prefab");
            GameObject pillar = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStonePillar.prefab");
            var root = new GameObject($"BossRoom_{room.Name}").transform;
            root.SetParent(parent, false);

            Vector2 entry = -IncomingDirection(spec, room);   // 방 중심에서 입구 쪽
            float half = room.Radius;
            float gateHalf = BOSS_STAIRS_WIDTH * 0.5f + 1f;
            Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

            foreach (Vector2 dir in directions)
            {
                var along = new Vector2(dir.y, -dir.x);
                int count = Mathf.RoundToInt(half * 2f / WALL_SEGMENT);
                bool isEntry = Vector2.Dot(dir, entry) > 0.7f;

                for (int i = 0; i < count; i++)
                {
                    float t = -half + (i + 0.5f) * (half * 2f / count);

                    if (isEntry && Mathf.Abs(t) < gateHalf + WALL_SEGMENT * 0.5f)
                    {
                        continue;
                    }

                    Place(wall, root, ground.At(room.Center + dir * half + along * t), Yaw(dir), $"Wall_{i}");
                }

                if (isEntry)
                {
                    Vector2 gate = room.Center + dir * half;
                    Place(paifang, root, ground.At(gate), Yaw(-dir), "Paifang_Entry");

                    foreach (float s in new[] { -1f, 1f })
                    {
                        Place(lantern, root, ground.At(gate - dir * 3f + along * (gateHalf + 1f) * s), Quaternion.identity, "Lantern_Entry");
                    }
                }
            }

            foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 1f) })
            {
                Place(pillar, root, ground.At(room.Center + corner * half), Quaternion.identity, "Pillar_Corner");
            }
        }

        private static void PlaceBossStairs(LevelSpec spec, Link link, Ground ground, Transform parent)
        {
            Room from = spec.Rooms[link.From];
            Room to = spec.Rooms[link.To];
            Vector2 dir = (to.Center - from.Center).normalized;
            Vector2 start = from.Center + dir * from.Radius;
            Vector2 end = to.Center - dir * (to.Square ? to.Radius : to.Radius);
            float run = Vector2.Distance(start, end);
            Vector3 a = ground.At(start);
            Vector3 b = ground.At(end);
            float rise = b.y - a.y;
            var root = new GameObject("BossStairs").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(a, Yaw(dir));

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BOSS_STAIRS_PREFAB);

            if (model != null)
            {
                // VARCO 계단: 모형 크기를 재서 길 폭·길이·높이에 맞춤 (모형 정면 +Z = 오르는 방향, 원점 = 아래쪽 가운데)
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Stairs";
                instance.transform.SetParent(root, false);
                Bounds bounds = RendererBounds(instance);
                Vector3 size = bounds.size;
                instance.transform.localScale = new Vector3(
                    size.x > 0.01f ? link.Width / size.x : 1f,
                    size.y > 0.01f ? Mathf.Max(0.5f, rise) / size.y : 1f,
                    size.z > 0.01f ? run / size.z : 1f);
                return;
            }

            // 임시 돌계단: 0.3m 단, 위 면이 경사 지면보다 조금 높게 (충돌 없음)
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE_MATERIAL);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(rise) / BOSS_STEP_RISE));
            float depth = run / steps;

            for (int i = 0; i < steps; i++)
            {
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(step.GetComponent<Collider>());
                step.name = $"Step_{i}";
                step.transform.SetParent(root, false);
                float top = rise * (i + 1) / steps + 0.08f;
                float bottom = rise * i / steps - 0.6f;
                step.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, depth * (i + 0.5f));
                step.transform.localScale = new Vector3(link.Width, top - bottom, depth);

                if (stone != null)
                {
                    step.GetComponent<Renderer>().sharedMaterial = stone;
                }

                GameObjectUtility.SetStaticEditorFlags(step, StaticEditorFlags.BatchingStatic);
            }
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);

            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }
}
