using System.Collections.Generic;
using System.IO;
using BrokenSovereign.Player;
using BrokenSovereign.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 레벨 씬의 플레이어 세트 자동 연결 (2026-10-20).
    /// 레벨을 생성(다시 생성)할 때 씬에 플레이어가 없으면 기준 씬(ClickMoveTest)의 플레이어·HUD·카메라·EventSystem을 옮겨 오고
    /// (서로의 참조 유지, 기준 씬 파일은 저장하지 않아 그대로), 플레이어를 시작 위치(발바닥 높이·NavMeshAgent Base Offset 보정)에 둡니다.
    /// 이미 있으면 위치만 다시 맞춤.
    /// </summary>
    public static partial class LevelTools
    {
        private const string PLAYER_SOURCE_SCENE = "Assets/Scenes/Sandbox/ClickMoveTest.unity";

        private static void EnsurePlayerRig(Vector3 start)
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();

            if (player == null && File.Exists(PLAYER_SOURCE_SCENE))
            {
                ImportPlayerRig(scene);
                player = Object.FindFirstObjectByType<PlayerController>();
            }

            if (player == null)
            {
                Debug.LogWarning($"LevelTools: 플레이어 없음. {PLAYER_SOURCE_SCENE}에 플레이어가 있는지 확인하세요.");
                return;
            }

            float footOffset = PivotToFeet(player.gameObject);
            player.transform.position = start + Vector3.up * (footOffset + 0.02f);

            if (player.GetComponent<NavMeshAgent>() is NavMeshAgent agent)
            {
                agent.baseOffset = footOffset;
            }
        }

        // 피벗이 몸 가운데인 모델: 피벗 높이 - 렌더러(없으면 콜라이더) 바닥 = 발바닥까지 거리
        private static float PivotToFeet(GameObject root)
        {
            float pivotY = root.transform.position.y;
            float bottom = float.MaxValue;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }

                bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            }

            if (bottom == float.MaxValue)
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                {
                    bottom = Mathf.Min(bottom, collider.bounds.min.y);
                }
            }

            return bottom == float.MaxValue ? 0f : Mathf.Max(0f, pivotY - bottom);
        }

        private static void ImportPlayerRig(Scene target)
        {
            Scene source = EditorSceneManager.OpenScene(PLAYER_SOURCE_SCENE, OpenSceneMode.Additive);
            var moving = new List<GameObject>();
            bool hasCamera = false;
            bool targetHasEventSystem = Object.FindFirstObjectByType<EventSystem>() != null;

            foreach (GameObject root in source.GetRootGameObjects())
            {
                bool isPlayer = root.GetComponentInChildren<PlayerController>(true) != null;
                bool isHud = root.GetComponentInChildren<HudView>(true) != null;
                bool isCamera = root.CompareTag("MainCamera");
                bool isEventSystem = root.GetComponentInChildren<EventSystem>(true) != null && !targetHasEventSystem;

                if (isPlayer || isHud || isCamera || isEventSystem)
                {
                    moving.Add(root);
                    hasCamera |= isCamera;
                }
            }

            // 기준 씬 카메라(플레이어 추적 설정 포함)를 쓰므로 생성된 카메라는 지움
            if (hasCamera)
            {
                foreach (GameObject root in target.GetRootGameObjects())
                {
                    if (root.CompareTag("MainCamera"))
                    {
                        Object.DestroyImmediate(root);
                    }
                }
            }

            foreach (GameObject root in moving)
            {
                SceneManager.MoveGameObjectToScene(root, target);
            }

            // 저장하지 않고 닫음 → ClickMoveTest 파일은 그대로
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(target);
            Debug.Log($"LevelTools: {PLAYER_SOURCE_SCENE}에서 플레이어 세트 {moving.Count}개 연결");
        }
    }
}
