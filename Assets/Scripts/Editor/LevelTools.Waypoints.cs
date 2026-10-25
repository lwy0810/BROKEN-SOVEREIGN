using System;
using BrokenSovereign.Map;
using BrokenSovereign.Town;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 오픈 필드 웨이포인트 4곳 배치 (2026-10-22): 마을 광장 중앙(처음부터 발견) · 구역 1 길목 · 구역 2 길목 · 보스 제단 앞.
    /// 모형은 Prefabs/Props/PropWaypoint.prefab(VARCO), 없으면 임시 석단 + 빛 기둥.
    /// 씬 최상위 "Waypoints"에 두며 다시 실행하면 새로 만듦. 오픈 필드 생성(Build) 때도 자동 배치.
    /// 메뉴: BrokenSovereign > Levels > 웨이포인트 배치 (현재 씬)
    /// </summary>
    public static partial class LevelTools
    {
        private const string WAYPOINT_ROOT = "Waypoints";
        private const string WAYPOINT_PREFAB = PROP_PREFABS + "/PropWaypoint.prefab";
        private const string WAYPOINT_PLACEHOLDER_MATERIAL = "Assets/Materials/Field/M_WaypointPlaceholder.mat";

        /// <summary>(id, 이름, 방 이름, 방 중심에서 들어오는 쪽으로 당길 비율 0~1)</summary>
        private static readonly (string id, string name, string room, float pullBack)[] WAYPOINTS =
        {
            ("town", "마을 광장", "Town", 0f),
            ("z1", "구역 1 · 남서 들길", "Z1_N3", 0f),
            ("z2", "구역 2 · 협곡 길목", "Z2_N3", 0f),
            ("boss", "보스 제단 앞", "BossGate", 0.6f),
        };

        [MenuItem("BrokenSovereign/Levels/웨이포인트 배치 (현재 씬)")]
        private static void PlaceWaypointsMenu()
        {
            Terrain terrain = FindLevelTerrain();

            if (terrain == null)
            {
                EditorUtility.DisplayDialog("웨이포인트 배치", "현재 씬에 Level/Terrain이 없습니다. World 씬을 열고 실행하세요.", "확인");
                return;
            }

            PlaceWaypoints(WORLD, new Ground(terrain));
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            EditorSceneManager.SaveScene(terrain.gameObject.scene);
        }

        private static void PlaceWaypoints(LevelSpec spec, Ground ground)
        {
            GameObject old = GameObject.Find(WAYPOINT_ROOT);

            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            // 예전 마을 표식(지도 W 아이콘만 있던 빈 오브젝트) 제거 → 실제 웨이포인트가 지도 표식을 가짐
            GameObject oldMarker = GameObject.Find("Level/Markers/Room_Town/Waypoint");

            if (oldMarker != null)
            {
                Object.DestroyImmediate(oldMarker);
            }

            var root = new GameObject(WAYPOINT_ROOT).transform;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WAYPOINT_PREFAB);

            for (int i = 0; i < WAYPOINTS.Length; i++)
            {
                var info = WAYPOINTS[i];
                int index = Array.FindIndex(spec.Rooms, r => r.Name == info.room);

                if (index < 0)
                {
                    Debug.LogWarning($"LevelTools: 웨이포인트 방 없음 ({info.room})");
                    continue;
                }

                Room room = spec.Rooms[index];
                Vector2 incoming = IncomingDirection(spec, room);
                Vector2 p = room.Center - incoming * room.Radius * info.pullBack;
                // 들어오는 길 쪽을 바라봄 (도착 위치 = 앞쪽)
                Quaternion facing = info.id == "town" ? Quaternion.Euler(0f, 180f, 0f) : Yaw(-incoming);

                GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : WaypointPlaceholder();
                go.name = $"Waypoint_{info.id}";
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(ground.At(p), facing);

                if (!go.TryGetComponent(out Waypoint waypoint))
                {
                    waypoint = go.AddComponent<Waypoint>();
                }

                waypoint.Configure(info.id, info.name, i, info.id == "town");

                if (!go.TryGetComponent(out MapMarker marker))
                {
                    marker = go.AddComponent<MapMarker>();
                }

                marker.SetKind(MapMarkerKind.Waypoint);
                SetIcon(go, 2);
            }

            Debug.Log($"LevelTools: 웨이포인트 {root.childCount}곳 배치 (모형: {(prefab != null ? "PropWaypoint" : "임시")})");
        }

        /// <summary>VARCO 모형이 오기 전 임시 모양: 지름 4m 석단 + 푸른 빛 기둥 + 점광원 (충돌 없음, NavMesh 영향 없음)</summary>
        private static GameObject WaypointPlaceholder()
        {
            var go = new GameObject("Waypoint");
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE_MATERIAL);

            GameObject dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "Dais";
            Object.DestroyImmediate(dais.GetComponent<Collider>());
            dais.transform.SetParent(go.transform, false);
            dais.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            dais.transform.localScale = new Vector3(4f, 0.1f, 4f);

            if (stone != null)
            {
                dais.GetComponent<MeshRenderer>().sharedMaterial = stone;
            }

            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            crystal.name = "Crystal";
            Object.DestroyImmediate(crystal.GetComponent<Collider>());
            crystal.transform.SetParent(go.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            crystal.transform.localScale = new Vector3(0.45f, 0.8f, 0.45f);
            MeshRenderer renderer = crystal.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = WaypointGlowMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 2f, 0f);
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.8f, 1f);
            light.range = 8f;
            light.intensity = 3f;
            return go;
        }

        private static Material WaypointGlowMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WAYPOINT_PLACEHOLDER_MATERIAL);

            if (material == null)
            {
                material = new Material(Shader.Find(ModelAssetUtility.URP_LIT));
                AssetDatabase.CreateAsset(material, WAYPOINT_PLACEHOLDER_MATERIAL);
            }

            material.SetColor("_BaseColor", new Color(0.6f, 0.85f, 1f));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.45f, 0.75f, 1f) * 2.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
