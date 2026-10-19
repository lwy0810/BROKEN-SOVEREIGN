using System;
using System.Collections.Generic;
using System.IO;
using BrokenSovereign.Map;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 오픈 필드 계곡 지형 (2026-10-19 변경, 참고: 티베트 고원 계곡).
    /// 높은 산맥으로 둘러싼 분지 + 길을 따라 완만한 초원 계곡 + 여러 갈래 자갈 강.
    /// 걸을 수 있는 곳은 보이지 않는 걷기 메시(계곡 바닥·길·여울)로만 NavMesh를 구워 산·강을 막습니다.
    /// 풀(Terrain 디테일)·바위(코드 생성 메시)·물(RiverWater 셰이더)·미니맵 그림을 함께 만듭니다.
    /// </summary>
    public static partial class LevelTools
    {
        private const string MAP_ROOT = "Assets/Sprites/Map";
        private const string ROCK_MESH_ROOT = "Assets/Models/Field";
        private const string WATER_MATERIAL = "Assets/Materials/Field/M_RiverWater.mat";
        private const string WATER_SHADER = "BrokenSovereign/Field/RiverWater";
        private const string VALLEY_ROCK_MATERIAL = "Assets/Materials/Field/M_ValleyRock.mat";
        private const string DETAIL_GRASS_TEXTURE = LAYER_ROOT + "/T_DetailGrass.png";
        private const int ROCK_VARIANTS = 4;
        private const int MAP_TEXTURE_SIZE = 2048;
        private const float WALK_CELL = 2f;          // 걷기 메시 격자 (m)
        private const float WALK_MAX_SLOPE = 35f;    // 이보다 가파르면 걷기 제외
        private const float WATER_STEP = 3f;         // 물 메시 표본 간격 (m)

        // 6번째 레이어까지 (기존 4종 + 강 자갈·마른 풀)
        private const int GRAVEL = 4;
        private const int DRY = 5;

        /// <summary>강 한 줄기: 상류 → 하류 꺾은선, 기본 반폭(m)</summary>
        private readonly struct River
        {
            public readonly Vector2[] Points;
            public readonly float HalfWidth;

            public River(float halfWidth, params Vector2[] points)
            {
                HalfWidth = halfWidth;
                Points = points;
            }
        }

        /// <summary>지형 모양 (부유 섬 IslandShape / 계곡 ValleyShape 공통)</summary>
        private interface ITerrainShape
        {
            float Height(float x, float z);

            /// <summary>레이어 가중치 (합 1). worldY = 지형 높이, steep = 경사(도)</summary>
            void Weights(float x, float z, float worldY, float steep, float[] weights);
        }

        // ------------------------------------------------------------------
        // 모양
        // ------------------------------------------------------------------

        private sealed class ValleyShape : ITerrainShape
        {
            private readonly LevelSpec _spec;
            private readonly float _offset;

            public ValleyShape(LevelSpec spec)
            {
                _spec = spec;
                _offset = spec.Seed * 13.37f;
            }

            /// <summary>한 지점의 길·강 정보</summary>
            public struct Sample
            {
                public float Nearest;       // 걷는 곳(방·길)까지 거리 (안쪽 음수)
                public float Walk;          // 방·길 바닥 (0 ~ 1)
                public float Ford;          // 길 근처 (여울 = 강을 얕게)
                public float Gorge;         // 다리 아래 협곡
                public float RiverDist;     // 가장 가까운 강 중심선까지 거리
                public float RiverWidth;    // 그 지점 강 반폭
                public float PathValley;    // 길 주변 계곡 바닥 (걸을 수 있는 곳 기준)
                public float Valley;        // 계곡 (길 + 강 골짜기)
                public float Floor;         // 계곡 바닥 기준 높이
                public float Radius;        // 맵 가장자리 기준 거리 = max(|x|, |z|) (산맥 벽 판정)
            }

            public Sample Query(float x, float z)
            {
                var p = new Vector2(x, z);
                var s = new Sample { Nearest = float.MaxValue, RiverDist = float.MaxValue, Radius = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) };

                foreach (Room room in _spec.Rooms)
                {
                    float d = Vector2.Distance(p, room.Center) - room.Radius;
                    s.Walk = Mathf.Max(s.Walk, 1f - SmoothStep(-1f, 1f, d));
                    s.Nearest = Mathf.Min(s.Nearest, d);
                }

                foreach (Link link in _spec.Links)
                {
                    float d = SegmentDistance(p, _spec.Rooms[link.From].Center, _spec.Rooms[link.To].Center) - link.Width * 0.5f;

                    if (link.IsBridge)
                    {
                        s.Gorge = Mathf.Max(s.Gorge, 1f - SmoothStep(_spec.GorgeWidth * 0.6f, _spec.GorgeWidth, d + link.Width * 0.5f));
                        continue;
                    }

                    s.Walk = Mathf.Max(s.Walk, 1f - SmoothStep(-1f, 1f, d));
                    s.Ford = Mathf.Max(s.Ford, 1f - SmoothStep(2f, 7f, d));
                    s.Nearest = Mathf.Min(s.Nearest, d);
                }

                float baseWidth = 0f;

                foreach (River river in _spec.Rivers)
                {
                    for (int i = 0; i < river.Points.Length - 1; i++)
                    {
                        float d = SegmentDistance(p, river.Points[i], river.Points[i + 1]);

                        if (d < s.RiverDist)
                        {
                            s.RiverDist = d;
                            baseWidth = river.HalfWidth;
                        }
                    }
                }

                s.RiverWidth = baseWidth * (0.75f + 0.6f * Fbm(x * 0.01f, z * 0.01f, 2));

                float edge = _spec.ValleyWidth + _spec.ValleyWidthVariation * Fbm(x * 0.006f + 40f, z * 0.006f, 2);
                s.PathValley = 1f - SmoothStep(edge, edge + 30f, s.Nearest);
                float riverValley = (1f - SmoothStep(s.RiverWidth + 18f, s.RiverWidth + 60f, s.RiverDist))
                    * (1f - SmoothStep(_spec.RingStart - 40f, _spec.RingStart + 30f, s.Radius));
                s.Valley = Mathf.Max(s.PathValley, riverValley);
                s.Floor = 3f + Fbm(x * 0.003f, z * 0.003f, 3) * _spec.FloorHeight;
                return s;
            }

            public float Height(float x, float z)
            {
                Sample s = Query(x, z);
                float outside = 1f - s.Valley;
                float rw = s.RiverWidth;
                float riverCut = 1f - SmoothStep(rw + 6f, rw + 40f, s.RiverDist);

                // 계곡 밖: 능선 산 (가장자리 급경사), 맵 가장자리: 높은 산맥 벽. 강은 산맥을 협곡으로 뚫고 나감
                float mountain = Mathf.Pow(SmoothStep(0f, 1f, outside), 1.2f)
                    * (_spec.MountainBase + _spec.MountainHeight * Ridge(x * 0.0032f, z * 0.0032f))
                    + SmoothStep(0.3f, 1f, outside) * 25f * Fbm(x * 0.02f, z * 0.02f, 3);
                float ring = SmoothStep(_spec.RingStart, _spec.RingEnd, s.Radius + (Fbm(x * 0.01f, z * 0.01f, 2) - 0.5f) * 60f)
                    * (_spec.RingHeight + 50f * Fbm(x * 0.02f, z * 0.02f, 2));

                float h = s.Floor
                    + mountain * (1f - 0.85f * riverCut * (1f - SmoothStep(_spec.RingStart - 25f, _spec.RingEnd - 5f, s.Radius)))
                    + ring * (1f - 0.8f * riverCut)
                    + (Fbm(x * 0.05f, z * 0.05f, 2) - 0.5f) * 2f * s.Valley;

                // 방·길: 평탄
                float flat = s.Floor + (Fbm(x * 0.15f, z * 0.15f, 2) - 0.5f) * 0.25f;
                h = Mathf.Lerp(h, flat, s.Walk);

                // 강: 바닥 1.8m 파기, 길이 지나는 곳(여울)은 얕게, 다리 아래는 협곡
                float channel = 1f - SmoothStep(rw * 0.55f, rw * 1.05f, s.RiverDist);
                float bank = 1f - SmoothStep(rw, rw + 10f, s.RiverDist);
                float depth = 1.8f * (1f - s.Ford) + 10f * s.Gorge;
                h -= channel * depth * (1f - s.Walk * (1f - s.Ford)) + bank * 0.6f * (1f - 0.7f * s.Ford);

                if (s.Gorge * (1f - s.Walk) > 0.3f)
                {
                    h = Mathf.Min(h, s.Floor - 8f * s.Gorge);
                }

                return h;
            }

            public void Weights(float x, float z, float worldY, float steep, float[] w)
            {
                Sample s = Query(x, z);
                Surface(_spec, _offset, x, z, out float stone, out float dirt);

                float dry = SmoothStep(6f, 30f, worldY - s.Floor + (Fbm(x * 0.03f, z * 0.03f, 2) - 0.5f) * 10f);
                float gravel = 1f - SmoothStep(s.RiverWidth + 2f, s.RiverWidth + 8f + 4f * Fbm(x * 0.05f, z * 0.05f, 2), s.RiverDist);
                float rock = SmoothStep(34f, 46f, steep);
                float path = Mathf.Max(stone, dirt);

                Array.Clear(w, 0, w.Length);
                w[GRASS] = 1f - dry;
                w[DRY] = dry;
                Blend(w, GRAVEL, gravel * (1f - s.Gorge));
                Blend(w, ROCK, rock);
                Blend(w, DIRT, dirt * (1f - stone));
                Blend(w, STONE, stone);

                // 길 위는 바위 대신 길 (경사 판정 잡음 제거)
                if (path > 0.5f)
                {
                    w[ROCK] *= 1f - path;
                }

                Normalize(w);
            }

            /// <summary>걷기 메시에 넣을 곳: 길 주변 계곡 바닥, 강 물줄기 밖(여울 제외), 협곡·산맥 벽 제외</summary>
            public bool IsWalkable(Sample s)
            {
                bool outsideRiver = s.RiverDist > s.RiverWidth * 0.9f || s.Ford > 0.5f;
                return s.PathValley > 0.6f && outsideRiver && s.Gorge * (1f - s.Walk) < 0.2f && s.Radius < _spec.RingStart;
            }

            public float Fbm(float x, float z, int octaves) => LevelTools.Fbm(x, z, octaves, _offset);

            private float Ridge(float x, float z) => LevelTools.Ridge(x, z, _offset);
        }

        private static void Blend(float[] w, int layer, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            for (int i = 0; i < w.Length; i++)
            {
                w[i] *= 1f - amount;
            }

            w[layer] += amount;
        }

        private static void Normalize(float[] w)
        {
            float sum = 0f;

            foreach (float v in w)
            {
                sum += v;
            }

            if (sum <= 0f)
            {
                w[GRASS] = 1f;
                return;
            }

            for (int i = 0; i < w.Length; i++)
            {
                w[i] /= sum;
            }
        }

        private static float Fbm(float x, float z, int octaves, float offset)
        {
            float sum = 0f;
            float amp = 0.5f;
            float total = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x + offset + i * 31.7f, z - offset + i * 17.3f) * amp;
                total += amp;
                x *= 2f;
                z *= 2f;
                amp *= 0.5f;
            }

            return sum / total;
        }

        private static float Ridge(float x, float z, float offset)
        {
            float sum = 0f;
            float amp = 0.6f;
            float total = 0f;

            for (int i = 0; i < 3; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x + offset * 0.5f + i * 11.1f, z + i * 7.7f) * 2f - 1f);
                sum += n * n * amp;
                total += amp;
                x *= 2.1f;
                z *= 2.1f;
                amp *= 0.45f;
            }

            return sum / total;
        }

        /// <summary>
        /// 돌바닥(전투·시련·아레나·제단·곁길)과 흙길(길·이벤트) 가중치.
        /// 마을은 원 대신 성벽 안 정사각형: 길·광장 = 돌바닥, 나머지 구역 = 다진 흙 (LevelTools.Town)
        /// </summary>
        private static void Surface(LevelSpec spec, float offset, float x, float z, out float stone, out float dirt)
        {
            var p = new Vector2(x, z);
            float edge = (Mathf.PerlinNoise(x * 0.3f + offset, z * 0.3f) - 0.5f) * 1.5f;
            stone = 0f;
            dirt = 0f;
            int townIndex = -1;
            float inTown = 0f;

            for (int i = 0; i < spec.Rooms.Length; i++)
            {
                Room room = spec.Rooms[i];

                if (room.Kind == RoomKind.Town)
                {
                    townIndex = i;
                    Vector2 local = p - room.Center;

                    if (Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y)) < TOWN_HALF + 12f)
                    {
                        inTown = InsideTownSquare(local, 6f);
                        float street = 1f - SmoothStep(-0.6f, 0.4f, TownStreetDistance(local) + edge * 0.3f);
                        stone = Mathf.Max(stone, street * inTown);
                        dirt = Mathf.Max(dirt, InsideTownSquare(local) * (1f - street));
                    }

                    continue;
                }

                float d = Vector2.Distance(p, room.Center) - room.Radius + edge;

                if (room.StoneFloor)
                {
                    stone = Mathf.Max(stone, 1f - SmoothStep(-2.5f, -0.5f, d));
                }
                else
                {
                    dirt = Mathf.Max(dirt, 1f - SmoothStep(-2f, 0f, d));
                }
            }

            foreach (Link link in spec.Links)
            {
                if (link.IsBridge)
                {
                    continue;
                }

                // 마을 안은 마을 길이 대신함
                if ((link.From == townIndex || link.To == townIndex) && inTown > 0.5f)
                {
                    continue;
                }

                float d = SegmentDistance(p, spec.Rooms[link.From].Center, spec.Rooms[link.To].Center) - link.Width * 0.5f + edge;
                dirt = Mathf.Max(dirt, 1f - SmoothStep(-0.5f, 1f, d));
            }
        }

        // ------------------------------------------------------------------
        // 계곡 전용 배치 (Build에서 호출)
        // ------------------------------------------------------------------

        private static void BuildValleyExtras(LevelSpec spec, ValleyShape shape, Terrain terrain, Ground ground, GameObject level)
        {
            Transform parent = level.transform;
            EditorUtility.DisplayProgressBar(spec.Title, "강 물", 0.80f);
            BuildRiverWater(spec, ground, parent);
            EditorUtility.DisplayProgressBar(spec.Title, "풀", 0.82f);
            PaintDetails(spec, shape, terrain);
            EditorUtility.DisplayProgressBar(spec.Title, "바위", 0.85f);
            ScatterRocks(spec, shape, ground, parent);
            EditorUtility.DisplayProgressBar(spec.Title, "걷기 메시", 0.88f);
            BuildWalkMesh(spec, shape, terrain, parent);
            EditorUtility.DisplayProgressBar(spec.Title, "지도", 0.92f);
            BuildMap(spec, shape, terrain, level);
        }

        // ------------------------------------------------------------------
        // 강 물: 중심선을 따라 띠 메시, 물 높이 = 강바닥 + 0.4m. 갈래·모래톱은 셰이더 노이즈로 표현
        // ------------------------------------------------------------------

        private static void BuildRiverWater(LevelSpec spec, Ground ground, Transform parent)
        {
            Material material = WaterMaterial();
            var root = new GameObject("River").transform;
            root.SetParent(parent, false);
            float half = spec.Size * 0.5f - 4f;
            var shape = new ValleyShape(spec);

            for (int r = 0; r < spec.Rivers.Length; r++)
            {
                List<Vector2> samples = Resample(spec.Rivers[r].Points, WATER_STEP);
                var vertices = new List<Vector3>();
                var uvs = new List<Vector2>();
                var triangles = new List<int>();
                float distance = 0f;

                for (int i = 0; i < samples.Count; i++)
                {
                    Vector2 c = samples[i];

                    if (Mathf.Abs(c.x) > half || Mathf.Abs(c.y) > half)
                    {
                        continue;
                    }

                    Vector2 tangent = (samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(i - 1, 0)]).normalized;
                    var normal = new Vector2(-tangent.y, tangent.x);
                    float width = shape.Query(c.x, c.y).RiverWidth * 0.75f;
                    float y = ground.At(c).y + 0.4f;

                    if (i > 0)
                    {
                        distance += Vector2.Distance(samples[i - 1], c);
                    }

                    int start = vertices.Count;
                    Vector2 left = c + normal * width;
                    Vector2 right = c - normal * width;
                    vertices.Add(new Vector3(left.x, y, left.y));
                    vertices.Add(new Vector3(right.x, y, right.y));
                    uvs.Add(new Vector2(distance / 10f, 0f));
                    uvs.Add(new Vector2(distance / 10f, 1f));

                    if (start >= 2)
                    {
                        triangles.AddRange(new[] { start - 2, start, start - 1, start - 1, start, start + 1 });
                    }
                }

                var mesh = new Mesh { name = $"{spec.Name}River{r}" };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                string path = $"{SCENE_ROOT}/{spec.Name}/{mesh.name}.asset";
                ModelAssetUtility.EnsureFolder($"{SCENE_ROOT}/{spec.Name}");
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);

                var go = new GameObject($"Water_{r}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            // 여울: 길이 강을 지나는 곳 양옆에 징검 바위
            PlaceFordStones(spec, ground, root);
        }

        private static List<Vector2> Resample(Vector2[] points, float step)
        {
            var result = new List<Vector2> { points[0] };

            for (int i = 0; i < points.Length - 1; i++)
            {
                float length = Vector2.Distance(points[i], points[i + 1]);
                int count = Mathf.Max(1, Mathf.CeilToInt(length / step));

                for (int k = 1; k <= count; k++)
                {
                    result.Add(Vector2.Lerp(points[i], points[i + 1], k / (float)count));
                }
            }

            // 모서리 부드럽게 (이동 평균 2회)
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 1; i < result.Count - 1; i++)
                {
                    result[i] = (result[i - 1] + result[i] * 2f + result[i + 1]) * 0.25f;
                }
            }

            return result;
        }

        private static void PlaceFordStones(LevelSpec spec, Ground ground, Transform parent)
        {
            GameObject[] rocks = EnsureRockPrefabs();

            foreach (Link link in spec.Links)
            {
                if (link.IsBridge)
                {
                    continue;
                }

                Vector2 a = spec.Rooms[link.From].Center;
                Vector2 b = spec.Rooms[link.To].Center;

                foreach (River river in spec.Rivers)
                {
                    for (int i = 0; i < river.Points.Length - 1; i++)
                    {
                        if (!Intersect(a, b, river.Points[i], river.Points[i + 1], out Vector2 hit))
                        {
                            continue;
                        }

                        Vector2 dir = (b - a).normalized;
                        var side = new Vector2(dir.y, -dir.x);

                        for (int k = -2; k <= 2; k++)
                        {
                            foreach (float s in new[] { -1f, 1f })
                            {
                                Vector2 p = hit + dir * (k * 2.4f) + side * (link.Width * 0.5f + 1.2f) * s;
                                var stone = (GameObject)PrefabUtility.InstantiatePrefab(rocks[(k + 2 + (s > 0 ? 1 : 0)) % rocks.Length]);
                                stone.name = $"FordStone_{link.From}_{link.To}";
                                stone.transform.SetParent(parent, false);
                                stone.transform.position = ground.At(p) + Vector3.down * 0.15f;
                                stone.transform.localScale = new Vector3(1.1f, 0.45f, 0.9f);
                                stone.transform.rotation = Quaternion.Euler(0f, k * 47f, 0f);
                            }
                        }
                    }
                }
            }
        }

        private static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 hit)
        {
            hit = Vector2.zero;
            Vector2 r = b - a;
            Vector2 s = d - c;
            float denom = r.x * s.y - r.y * s.x;

            if (Mathf.Abs(denom) < 1e-5f)
            {
                return false;
            }

            float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / denom;
            float u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / denom;

            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                return false;
            }

            hit = a + r * t;
            return true;
        }

        private static Material WaterMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WATER_MATERIAL);

            if (material == null)
            {
                Shader shader = Shader.Find(WATER_SHADER);

                if (shader == null)
                {
                    Debug.LogWarning($"LevelTools: 물 셰이더 없음 ({WATER_SHADER})");
                    shader = Shader.Find(ModelAssetUtility.URP_LIT);
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, WATER_MATERIAL);
            }

            return material;
        }

        // ------------------------------------------------------------------
        // 풀 (Terrain 디테일): 초원 = 촘촘, 마른 풀 경사 = 드문드문, 길·자갈·바위 = 없음
        // ------------------------------------------------------------------

        private static void PaintDetails(LevelSpec spec, ValleyShape shape, Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            Texture2D grass = EnsureGrassTexture();

            data.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototypeTexture = grass,
                    renderMode = DetailRenderMode.GrassBillboard,
                    healthyColor = new Color(0.55f, 0.78f, 0.32f),
                    dryColor = new Color(0.72f, 0.74f, 0.4f),
                    minWidth = 0.5f, maxWidth = 1.0f, minHeight = 0.35f, maxHeight = 0.75f,
                    noiseSpread = 0.3f, useInstancing = false,
                },
                new DetailPrototype
                {
                    prototypeTexture = grass,
                    renderMode = DetailRenderMode.GrassBillboard,
                    healthyColor = new Color(0.72f, 0.68f, 0.42f),
                    dryColor = new Color(0.62f, 0.55f, 0.36f),
                    minWidth = 0.4f, maxWidth = 0.8f, minHeight = 0.3f, maxHeight = 0.6f,
                    noiseSpread = 0.5f, useInstancing = false,
                },
            };

            const int res = 1024;
            data.SetDetailResolution(res, 32);
            float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            int alphaRes = data.alphamapWidth;
            var lush = new int[res, res];
            var dry = new int[res, res];
            var rng = new Random(spec.Seed + 5);

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int ax = Mathf.Min(alphaRes - 1, x * alphaRes / res);
                    int az = Mathf.Min(alphaRes - 1, z * alphaRes / res);
                    float g = alpha[az, ax, GRASS];
                    float d = alpha[az, ax, DRY];
                    double roll = rng.NextDouble();
                    lush[z, x] = g > 0.55f ? Mathf.RoundToInt(g * (6f + (float)roll * 8f)) : 0;
                    dry[z, x] = d > 0.5f && roll < 0.35 ? Mathf.RoundToInt(d * 3f) : 0;
                }
            }

            data.SetDetailLayer(0, 0, 0, lush);
            data.SetDetailLayer(0, 0, 1, dry);
            terrain.detailObjectDistance = 70f;
            terrain.detailObjectDensity = 1f;
            terrain.drawTreesAndFoliage = true;
        }

        // 풀잎 다발 (가는 잎 여러 장, 아래 진하고 위로 밝게, 투명 배경)
        private static Texture2D EnsureGrassTexture()
        {
            if (!File.Exists(DETAIL_GRASS_TEXTURE))
            {
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                var rng = new Random(77);

                for (int blade = 0; blade < 22; blade++)
                {
                    float baseX = 0.15f + 0.7f * (float)rng.NextDouble();
                    float lean = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                    float height = 0.55f + 0.42f * (float)rng.NextDouble();
                    float width = 0.025f + 0.02f * (float)rng.NextDouble();
                    float shade = 0.75f + 0.25f * (float)rng.NextDouble();

                    for (int y = 0; y < size; y++)
                    {
                        float t = y / (float)size / height;

                        if (t > 1f)
                        {
                            break;
                        }

                        float cx = baseX + lean * t * t;
                        float w = width * (1f - t);

                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Abs(x / (float)size - cx);

                            if (dx < w)
                            {
                                var c = Color.Lerp(new Color(0.35f, 0.5f, 0.2f), new Color(0.8f, 0.9f, 0.55f), t) * shade;
                                c.a = 1f;
                                pixels[y * size + x] = c;
                            }
                        }
                    }
                }

                texture.SetPixels(pixels);
                File.WriteAllBytes(DETAIL_GRASS_TEXTURE, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(DETAIL_GRASS_TEXTURE);

                if (AssetImporter.GetAtPath(DETAIL_GRASS_TEXTURE) is TextureImporter importer)
                {
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(DETAIL_GRASS_TEXTURE);
        }

        // ------------------------------------------------------------------
        // 바위: 코드 생성 메시 4종, 강가·산기슭에 흩뿌림 (길 위 제외)
        // ------------------------------------------------------------------

        private static void ScatterRocks(LevelSpec spec, ValleyShape shape, Ground ground, Transform parent)
        {
            GameObject[] rocks = EnsureRockPrefabs();
            var root = new GameObject("Rocks").transform;
            root.SetParent(parent, false);
            var rng = new Random(spec.Seed + 9);
            const float cell = 9f;
            float half = spec.Size * 0.5f;
            int count = 0;

            for (float z = -half; z < half; z += cell)
            {
                for (float x = -half; x < half; x += cell)
                {
                    float px = x + (float)rng.NextDouble() * cell;
                    float pz = z + (float)rng.NextDouble() * cell;
                    double roll = rng.NextDouble();
                    ValleyShape.Sample s = shape.Query(px, pz);

                    if (s.Nearest < 4f || s.Radius > spec.RingEnd)
                    {
                        continue;
                    }

                    float scale;
                    bool bank = s.RiverDist > s.RiverWidth && s.RiverDist < s.RiverWidth + 12f;
                    bool foot = s.Valley > 0.12f && s.Valley < 0.65f;

                    if (bank && roll < 0.3)
                    {
                        scale = 0.5f + (float)rng.NextDouble() * 1.2f;
                    }
                    else if (foot && roll < 0.18)
                    {
                        scale = 0.8f + (float)rng.NextDouble() * 2.2f;
                    }
                    else if (s.Valley < 0.12f && roll < 0.025)
                    {
                        scale = 2.5f + (float)rng.NextDouble() * 4f;
                    }
                    else
                    {
                        continue;
                    }

                    var rock = (GameObject)PrefabUtility.InstantiatePrefab(rocks[rng.Next(rocks.Length)]);
                    rock.name = $"Rock_{count}";
                    rock.transform.SetParent(root, false);
                    rock.transform.position = ground.At(new Vector2(px, pz)) + Vector3.down * (0.25f * scale);
                    rock.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    rock.transform.localScale = new Vector3(scale, scale * (0.6f + 0.4f * (float)rng.NextDouble()), scale);
                    count++;
                }
            }
        }

        private static GameObject[] EnsureRockPrefabs()
        {
            var prefabs = new GameObject[ROCK_VARIANTS];
            Material material = ValleyRockMaterial();

            for (int i = 0; i < ROCK_VARIANTS; i++)
            {
                string prefabPath = $"{FIELD_PREFABS}/FieldRock{i}.prefab";
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefabs[i] != null)
                {
                    continue;
                }

                string meshPath = $"{ROCK_MESH_ROOT}/FieldRock{i}.asset";
                Mesh mesh = BuildRockMesh(100 + i * 17);
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);

                var go = new GameObject($"FieldRock{i}", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.GetComponent<MeshCollider>().sharedMesh = mesh;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                ModelAssetUtility.EnsureFolder(FIELD_PREFABS);
                prefabs[i] = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
            }

            return prefabs;
        }

        // 구를 노이즈로 울퉁불퉁하게, 아래는 평평, 위아래로 납작. UV = 방향별 상자 투영
        private static Mesh BuildRockMesh(int seed)
        {
            const int rings = 10;
            const int segments = 14;
            var rng = new Random(seed);
            float ox = (float)rng.NextDouble() * 100f;
            float oz = (float)rng.NextDouble() * 100f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = v * Mathf.PI;

                for (int s = 0; s <= segments; s++)
                {
                    float theta = s / (float)segments * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    float noise = Mathf.PerlinNoise(dir.x * 1.6f + ox, dir.z * 1.6f + dir.y * 0.8f + oz);
                    float facet = Mathf.PerlinNoise(dir.x * 4f + oz, dir.y * 4f + dir.z * 2f + ox);
                    Vector3 p = dir * (0.75f + 0.35f * noise + 0.12f * facet);
                    p.y = Mathf.Max(p.y * 0.75f, -0.15f);
                    vertices.Add(p);
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s;
                    int b = a + segments + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            }

            var mesh = new Mesh { name = $"FieldRock_{seed}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            var normals = mesh.normals;
            var uvs = new Vector2[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 n = normals[i];
                Vector3 p = vertices[i];
                uvs[i] = Mathf.Abs(n.y) > 0.6f ? new Vector2(p.x, p.z)
                    : Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
            }

            mesh.uv = uvs;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material ValleyRockMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(VALLEY_ROCK_MATERIAL);

            if (material == null)
            {
                material = new Material(Shader.Find(ModelAssetUtility.URP_LIT));
                AssetDatabase.CreateAsset(material, VALLEY_ROCK_MATERIAL);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{LAYER_ROOT}/T_TerrainRock.png"));
            material.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
            material.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.86f));
            material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------
        // 걷기 메시: 2m 격자, 걸을 수 있는 정점끼리만 삼각형. 렌더러 없이 MeshCollider만 (NavMesh 굽기용)
        // ------------------------------------------------------------------

        private static void BuildWalkMesh(LevelSpec spec, ValleyShape shape, Terrain terrain, Transform parent)
        {
            TerrainData data = terrain.terrainData;
            float half = spec.Size * 0.5f;
            int n = Mathf.RoundToInt(spec.Size / WALK_CELL) + 1;
            var index = new int[n * n];
            var vertices = new List<Vector3>();

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    float wx = -half + x * WALK_CELL;
                    float wz = -half + z * WALK_CELL;
                    float u = (wx + half) / spec.Size;
                    float v = (wz + half) / spec.Size;
                    ValleyShape.Sample s = shape.Query(wx, wz);
                    bool walkable = shape.IsWalkable(s) && data.GetSteepness(u, v) < WALK_MAX_SLOPE;
                    index[z * n + x] = -1;

                    if (walkable)
                    {
                        float y = data.GetInterpolatedHeight(u, v) + terrain.transform.position.y + 0.03f;
                        index[z * n + x] = vertices.Count;
                        vertices.Add(new Vector3(wx, y, wz));
                    }
                }
            }

            var triangles = new List<int>();

            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int a = index[z * n + x];
                    int b = index[z * n + x + 1];
                    int c = index[(z + 1) * n + x];
                    int d = index[(z + 1) * n + x + 1];

                    if (a >= 0 && c >= 0 && b >= 0)
                    {
                        triangles.AddRange(new[] { a, c, b });
                    }

                    if (b >= 0 && c >= 0 && d >= 0)
                    {
                        triangles.AddRange(new[] { b, c, d });
                    }
                }
            }

            var mesh = new Mesh { name = $"{spec.Name}WalkArea", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = $"{SCENE_ROOT}/{spec.Name}/{mesh.name}.asset";
            ModelAssetUtility.EnsureFolder($"{SCENE_ROOT}/{spec.Name}");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("WalkArea", typeof(MeshCollider));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);

            // 지형은 NavMesh에서 빼고 걷기 메시만 사용 (산·강 진입 차단)
            NavMeshModifier modifier = terrain.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }

        // ------------------------------------------------------------------
        // 지도 그림 (2026-10-19, 디아블로4 지도 느낌): 양피지 색조 + 언덕 음영, 길은 밝게, 갈 수 없는 곳은 어둡게,
        // 물은 차분한 청회색. 마을은 길·건물 자리·성벽을 또렷하게 그림.
        // 전체 그림 2048² + 마을 세부 그림 1024² (150m, 확대해도 선명). LevelMap 연결
        // ------------------------------------------------------------------

        private const int MAP_DETAIL_SIZE = 1024;
        private const float MAP_DETAIL_WORLD = 150f;

        private static readonly Color MAP_PAPER = new Color(0.84f, 0.74f, 0.56f);
        private static readonly Color MAP_INK = new Color(0.2f, 0.16f, 0.11f);
        private static readonly Color MAP_WATER = new Color(0.33f, 0.42f, 0.44f);
        private static readonly Color MAP_STREET = new Color(0.93f, 0.86f, 0.7f);
        private static readonly Color MAP_YARD = new Color(0.62f, 0.52f, 0.38f);
        private static readonly Color MAP_BUILDING = new Color(0.34f, 0.27f, 0.2f);
        private static readonly Color MAP_WALL = new Color(0.17f, 0.13f, 0.1f);

        // 레이어별 밝기 (풀·돌바닥·흙길·바위·자갈·마른 풀)
        private static readonly float[] MAP_TONE = { 0.5f, 0.95f, 0.85f, 0.32f, 0.72f, 0.58f };

        private sealed class MapSampler
        {
            public LevelSpec Spec;
            public ValleyShape Shape;
            public TerrainData Data;
            public float[,,] Alpha;
            public Vector2 TownCenter;
            public bool HasTown;
            public Vector3 Light;

            public Color At(float wx, float wz)
            {
                float half = Spec.Size * 0.5f;
                float u = Mathf.Clamp01((wx + half) / Spec.Size);
                float v = Mathf.Clamp01((wz + half) / Spec.Size);
                int res = Alpha.GetLength(0);
                int ax = Mathf.Min(res - 1, (int)(u * res));
                int az = Mathf.Min(res - 1, (int)(v * res));
                float tone = 0f;

                for (int l = 0; l < MAP_TONE.Length && l < Alpha.GetLength(2); l++)
                {
                    tone += MAP_TONE[l] * Alpha[az, ax, l];
                }

                Color c = Color.Lerp(MAP_INK, MAP_PAPER, tone);
                ValleyShape.Sample s = Shape.Query(wx, wz);

                if (s.RiverDist < s.RiverWidth * 0.6f)
                {
                    c = Color.Lerp(c, MAP_WATER, 0.85f);
                }

                Vector3 normal = Data.GetInterpolatedNormal(u, v);
                c *= 0.68f + 0.42f * Mathf.Clamp01(Vector3.Dot(normal, Light));

                if (!Shape.IsWalkable(s))
                {
                    c = Color.Lerp(c * 0.5f, MAP_INK, 0.25f);
                }

                if (HasTown)
                {
                    TownColor(new Vector2(wx, wz), ref c);
                }

                c.a = 1f;
                return c;
            }

            // 마을: 길·광장(밝게), 구역(마당 색), 건물 자리(어둡게 + 테두리), 성벽
            private void TownColor(Vector2 world, ref Color c)
            {
                Vector2 local = world - TownCenter;
                float edge = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y));

                if (edge > TOWN_HALF + 8f)
                {
                    return;
                }

                float street = TownStreetDistance(local);

                if (street < 0f)
                {
                    c = street > -0.5f ? Color.Lerp(MAP_STREET, MAP_INK, 0.35f) : MAP_STREET;
                }
                else if (edge <= TOWN_HALF)
                {
                    c = Color.Lerp(c, MAP_YARD, 0.75f);
                }

                foreach (Footprint f in s_footprints)
                {
                    float d = f.Distance(world);

                    if (d <= 0f)
                    {
                        c = d > -0.45f ? MAP_WALL : MAP_BUILDING;
                        break;
                    }
                }

                if (Mathf.Abs(edge - TOWN_HALF) < 0.8f && Mathf.Min(Mathf.Abs(local.x), Mathf.Abs(local.y)) > GATE_HALF)
                {
                    c = MAP_WALL;
                }
            }
        }

        private static void BuildMap(LevelSpec spec, ValleyShape shape, Terrain terrain, GameObject level)
        {
            TerrainData data = terrain.terrainData;
            Room town = Array.Find(spec.Rooms, r => r.Kind == RoomKind.Town);
            bool hasTown = Array.Exists(spec.Rooms, r => r.Kind == RoomKind.Town);
            var sampler = new MapSampler
            {
                Spec = spec,
                Shape = shape,
                Data = data,
                Alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight),
                TownCenter = hasTown ? town.Center : Vector2.zero,
                HasTown = hasTown,
                Light = new Vector3(-0.45f, 0.75f, -0.5f).normalized,
            };

            float half = spec.Size * 0.5f;
            var worldMin = new Vector2(-half, -half);
            string path = $"{MAP_ROOT}/T_Map_{spec.Name}.png";
            RenderMap(sampler, worldMin, spec.Size, MAP_TEXTURE_SIZE, path, $"{spec.Title}: 지도 그림");

            LevelMap map = level.AddComponent<LevelMap>();
            map.Setup(AssetDatabase.LoadAssetAtPath<Texture2D>(path), worldMin, spec.Size);

            if (hasTown)
            {
                Vector2 detailMin = town.Center - Vector2.one * (MAP_DETAIL_WORLD * 0.5f);
                string detailPath = $"{MAP_ROOT}/T_Map_{spec.Name}_Town.png";
                RenderMap(sampler, detailMin, MAP_DETAIL_WORLD, MAP_DETAIL_SIZE, detailPath, $"{spec.Title}: 마을 지도");
                map.SetupDetail(AssetDatabase.LoadAssetAtPath<Texture2D>(detailPath), detailMin, MAP_DETAIL_WORLD);
                map.SetupTown(town.Center, TOWN_HALF + 6f);
            }
        }

        private static void RenderMap(MapSampler sampler, Vector2 min, float worldSize, int size, string path, string progress)
        {
            var pixels = new Color[size * size];

            for (int z = 0; z < size; z++)
            {
                if ((z & 63) == 0)
                {
                    EditorUtility.DisplayProgressBar(progress, $"{z}/{size}", 0.9f + 0.05f * z / size);
                }

                for (int x = 0; x < size; x++)
                {
                    float wx = min.x + (x + 0.5f) / size * worldSize;
                    float wz = min.y + (z + 0.5f) / size * worldSize;
                    pixels[z * size + x] = sampler.At(wx, wz);
                }
            }

            ModelAssetUtility.EnsureFolder(MAP_ROOT);
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = size;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        /// <summary>적 프리팹(Prefabs/Enemies)에 미니맵 빨간 점 표시 붙이기 (이미 있으면 건너뜀)</summary>
        private static void EnsureEnemyMapMarkers()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);

                if (root.GetComponent<MapMarker>() == null)
                {
                    AddMapMarker(root, MapMarkerKind.Enemy);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>표시 오브젝트에 지도 아이콘 붙이기</summary>
        private static void AddMapMarker(GameObject go, MapMarkerKind kind)
        {
            go.AddComponent<MapMarker>().SetKind(kind);
        }
    }
}
