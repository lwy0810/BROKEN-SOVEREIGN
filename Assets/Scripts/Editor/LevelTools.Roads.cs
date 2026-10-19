using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 가도·마을 길 덮개와 성문 밖 적 배치 (2026-10-20).
    /// 실사 지형 패키지 머티리얼은 텍스처 칠하기를 쓰지 않아 걸을 수 있는 길이 풀밭과 구분되지 않음 →
    /// 지형 칠하기의 흙길·돌바닥 비율을 1m 격자 메시(정점 색)로 옮겨 지형 위 5cm에 얹고 RoadOverlay 셰이더로 그림.
    /// </summary>
    public static partial class LevelTools
    {
        private const string ROAD_SHADER = "BrokenSovereign/Field/RoadOverlay";
        private const string ROAD_MATERIAL = "Assets/Materials/Field/M_Road.mat";
        private const float ROAD_CELL = 1f;           // 길 메시 격자 (m)
        private const float ROAD_LIFT = 0.05f;        // 지형 위로 띄우는 높이 (m)
        private const float ROAD_MIN_WEIGHT = 0.02f;  // 이보다 옅으면 길 아님
        private const float GATE_ENEMY_DISTANCE = 28f;  // 성벽에서 바깥으로 이 거리에 적 무리

        private static void BuildRoadOverlay(LevelSpec spec, Terrain terrain, Transform parent)
        {
            TerrainData data = terrain.terrainData;

            if (data.alphamapLayers <= DIRT)
            {
                return;
            }

            float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            int alphaRes = data.alphamapWidth;
            float half = spec.Size * 0.5f;
            int n = Mathf.RoundToInt(spec.Size / ROAD_CELL) + 1;
            var dirt = new float[n * n];
            var stone = new float[n * n];

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int ax = Mathf.Min(alphaRes - 1, x * alphaRes / n);
                    int az = Mathf.Min(alphaRes - 1, z * alphaRes / n);
                    dirt[z * n + x] = alpha[az, ax, DIRT];
                    stone[z * n + x] = alpha[az, ax, STONE];
                }
            }

            // 길 칸(네 모서리 중 하나라도 길) → 그 칸의 네 정점 사용 (가장자리는 비율 0으로 흐려짐)
            var index = new int[n * n];

            for (int i = 0; i < index.Length; i++)
            {
                index[i] = -1;
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();

            int Vertex(int x, int z)
            {
                int i = z * n + x;

                if (index[i] >= 0)
                {
                    return index[i];
                }

                float u = (float)x / (n - 1);
                float v = (float)z / (n - 1);
                float y = data.GetInterpolatedHeight(u, v) + terrain.transform.position.y + ROAD_LIFT;
                index[i] = vertices.Count;
                vertices.Add(new Vector3(-half + x * ROAD_CELL, y, -half + z * ROAD_CELL));
                normals.Add(data.GetInterpolatedNormal(u, v));
                colors.Add(new Color32((byte)(Mathf.Clamp01(dirt[i]) * 255f), (byte)(Mathf.Clamp01(stone[i]) * 255f), 0, 255));
                return index[i];
            }

            bool IsRoad(int i) => dirt[i] + stone[i] > ROAD_MIN_WEIGHT;

            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i00 = z * n + x;
                    int i10 = i00 + 1;
                    int i01 = i00 + n;
                    int i11 = i01 + 1;

                    if (!(IsRoad(i00) || IsRoad(i10) || IsRoad(i01) || IsRoad(i11)))
                    {
                        continue;
                    }

                    int a = Vertex(x, z);
                    int b = Vertex(x + 1, z);
                    int c = Vertex(x, z + 1);
                    int d = Vertex(x + 1, z + 1);
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            var mesh = new Mesh { name = $"{spec.Name}Roads", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            string path = $"{SCENE_ROOT}/{spec.Name}/{mesh.name}.asset";
            ModelAssetUtility.EnsureFolder($"{SCENE_ROOT}/{spec.Name}");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("Roads", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = RoadMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Debug.Log($"LevelTools: 길 덮개 메시 {vertices.Count:N0} 정점");
        }

        private static Material RoadMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ROAD_MATERIAL);

            if (material == null)
            {
                material = new Material(Shader.Find(ROAD_SHADER));
                AssetDatabase.CreateAsset(material, ROAD_MATERIAL);
            }

            material.SetTexture("_DirtTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{LAYER_ROOT}/T_TerrainDirt.png"));
            material.SetTexture("_StoneTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{LAYER_ROOT}/T_TerrainStone.png"));
            EditorUtility.SetDirty(material);
            return material;
        }

        // 마을 성문 4곳 바깥 길 위에 적 무리 (천병 2 + 역사 1, 마을을 바라봄). NavMesh 굽기에서 빠지도록 Level 밖에 둠
        private static void PlaceGateEnemies(LevelSpec spec, Ground ground)
        {
            int townIndex = System.Array.FindIndex(spec.Rooms, r => r.Kind == RoomKind.Town);
            GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/EnemyHeavenSoldier.prefab");
            GameObject guardian = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/EnemyTempleGuardian.prefab");

            if (townIndex < 0 || soldier == null)
            {
                return;
            }

            Room town = spec.Rooms[townIndex];
            var root = new GameObject("GateEnemies").transform;
            Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

            foreach (Vector2 dir in directions)
            {
                var side = new Vector2(dir.y, -dir.x);
                Vector2 center = town.Center + dir * (TOWN_HALF + GATE_ENEMY_DISTANCE);
                Quaternion facing = Yaw(-dir);

                SpawnEnemy(soldier, root, ground.At(center + side * 2.5f), facing);
                SpawnEnemy(soldier, root, ground.At(center - side * 2.5f + dir * 1.5f), facing);
                SpawnEnemy(guardian != null ? guardian : soldier, root, ground.At(center + dir * 5f), facing);
            }
        }

        private static void SpawnEnemy(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.transform.SetParent(parent, false);
            enemy.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
