using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 오픈 필드 정사각형 보스방 (10/25): 돌벽 + 입구 패루·석등 + 모서리 석주, 계단 모형.
    /// 계단: Props/PropBossStairs.prefab(VARCO)이 있으면 길에 맞춰 늘려 배치, 없으면 임시 돌계단 (충돌 없음, 걷기는 지형 경사).
    /// 10/26: VARCO 계단은 '한 마디' 모듈 → 폭에 맞춰 키우고, 모듈 하나의 오름 높이로 개수를 정해 길을 따라 이어 붙임.
    ///   메뉴 BrokenSovereign/Level/보스 계단 VARCO 모형으로 교체: 열린 씬의 BossStairs(임시 Step_*)를 바로 교체
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
                TileStairModules(model, root, link.Width, run, rise);
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

        [MenuItem("BrokenSovereign/Level/보스 계단 VARCO 모형으로 교체")]
        private static void ReplaceBossStairsInScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BOSS_STAIRS_PREFAB) == null)
            {
                PropSetupTools.BuildOne("PropBossStairs");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BOSS_STAIRS_PREFAB);
            GameObject stairs = GameObject.Find("BossStairs");

            if (model == null || stairs == null)
            {
                Debug.LogWarning($"LevelTools: 계단 모형({BOSS_STAIRS_PREFAB}) 또는 씬의 BossStairs가 없습니다.");
                return;
            }

            // 임시 돌계단(Step_*)에서 폭·길이·높이 복원 (위 면 = 오름 + 0.08)
            float width = 0f, run = 0f, rise = 0f;
            var old = new System.Collections.Generic.List<GameObject>();

            foreach (Transform child in stairs.transform)
            {
                old.Add(child.gameObject);

                if (!child.name.StartsWith("Step_"))
                {
                    continue;
                }

                Vector3 p = child.localPosition;
                Vector3 s = child.localScale;
                width = Mathf.Max(width, s.x);
                run = Mathf.Max(run, p.z + s.z * 0.5f);
                rise = Mathf.Max(rise, p.y + s.y * 0.5f - 0.08f);
            }

            if (width <= 0f || run <= 0f)
            {
                Debug.LogWarning("LevelTools: BossStairs 아래에 Step_* 가 없어 크기를 알 수 없습니다.");
                return;
            }

            foreach (GameObject go in old)
            {
                Undo.DestroyObjectImmediate(go);
            }

            TileStairModules(model, stairs.transform, width, run, rise);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stairs.scene);
            Debug.Log($"LevelTools: 보스 계단 교체 (폭 {width:0.#} · 길이 {run:0.#} · 높이 {rise:0.#}m)");
        }

        // VARCO 계단 모듈 이어 붙이기: 폭에 맞춘 균일 크기 → 모듈 하나가 오르는 높이로 개수 결정 → 길이만 늘려 정확히 채움.
        // 모듈의 오르는 방향은 메시 가운데 줄의 높이로 판단 (낮은 끝 = 시작)
        private static void TileStairModules(GameObject model, Transform root, float width, float run, float rise)
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            probe.transform.SetParent(root, false);
            probe.transform.localPosition = Vector3.zero;
            probe.transform.localRotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;
            MeasureModule(probe, root, out Bounds bounds, out float lowEnd, out float highTop, out bool flip);
            Object.DestroyImmediate(probe);

            if (bounds.size.x < 0.01f || bounds.size.z < 0.01f)
            {
                return;
            }

            float scale = width / bounds.size.x;
            float moduleRise = Mathf.Max(0.05f, (highTop - lowEnd) * scale);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rise) / moduleRise), 1, 40);
            float depth = run / count;
            float zScale = depth / bounds.size.z;
            float stepRise = rise / count;
            Quaternion turn = flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            for (int i = 0; i < count; i++)
            {
                var module = (GameObject)PrefabUtility.InstantiatePrefab(model);
                module.name = $"StairModule_{i}";
                module.transform.SetParent(root, false);
                module.transform.localRotation = turn;
                module.transform.localScale = new Vector3(scale, scale * Mathf.Max(0.35f, stepRise / moduleRise), zScale);

                // 충돌 없음 (걷기·클릭은 지형 경사)
                foreach (Collider collider in module.GetComponentsInChildren<Collider>())
                {
                    Object.DestroyImmediate(collider);
                }

                // 모듈 아래 끝 앞쪽 가운데를 (0, 오름, 깊이·i)에 맞춤
                module.transform.localPosition = Vector3.zero;
                Bounds placed = LocalBounds(module, root);
                float baseY = stepRise * i - lowEnd * module.transform.localScale.y + 0.05f;
                module.transform.localPosition = new Vector3(-placed.center.x, baseY - placed.min.y, depth * i - placed.min.z);
                GameObjectUtility.SetStaticEditorFlags(module, StaticEditorFlags.BatchingStatic);
            }
        }

        // 모듈 크기(root 기준)와 가운데 줄 높이: 낮은 끝 위 면 높이(lowEnd) · 가장 높은 위 면(highTop) · 높은 쪽이 -Z면 flip
        private static void MeasureModule(GameObject module, Transform root, out Bounds bounds, out float lowEnd, out float highTop, out bool flip)
        {
            bounds = LocalBounds(module, root);
            var points = new System.Collections.Generic.List<Vector3>();

            foreach (MeshFilter filter in module.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    points.Add(root.InverseTransformPoint(filter.transform.TransformPoint(v)));
                }
            }

            float front = float.MinValue, back = float.MinValue, top = float.MinValue;
            float xMin = bounds.min.x + bounds.size.x * 0.35f, xMax = bounds.max.x - bounds.size.x * 0.35f;

            foreach (Vector3 p in points)
            {
                if (p.x < xMin || p.x > xMax)
                {
                    continue;
                }

                float t = (p.z - bounds.min.z) / Mathf.Max(0.0001f, bounds.size.z);
                float h = p.y - bounds.min.y;
                top = Mathf.Max(top, h);

                if (t < 0.15f)
                {
                    front = Mathf.Max(front, h);
                }
                else if (t > 0.85f)
                {
                    back = Mathf.Max(back, h);
                }
            }

            flip = back < front;
            lowEnd = Mathf.Max(0f, Mathf.Min(front, back));
            highTop = Mathf.Max(lowEnd, top);
        }

        private static Bounds LocalBounds(GameObject go, Transform root)
        {
            var bounds = new Bounds();
            bool first = true;

            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds mesh = filter.sharedMesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.InverseTransformPoint(filter.transform.TransformPoint(corner));

                    if (first)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
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
