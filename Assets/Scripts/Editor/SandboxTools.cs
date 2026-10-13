using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 테스트 씬 편집 도구 (에디터 전용).
    /// 메뉴: BrokenSovereign > Sandbox > 경기장 벽 생성
    /// </summary>
    public static class SandboxTools
    {
        private const string ROOT_NAME = "ArenaWalls";
        private const float INNER_SIZE = 20f;
        private const float WALL_THICKNESS = 0.5f;
        private const float WALL_HEIGHT = 2f;

        /// <summary>
        /// 월드 원점 기준 안쪽 20 × 20 정사각형 벽 4개를 생성합니다. 이미 있으면 교체합니다.
        /// NavMeshObstacle(Carve)로 길찾기 영역을 깎으므로 NavMesh 재굽기가 필요 없습니다.
        /// </summary>
        [MenuItem("BrokenSovereign/Sandbox/경기장 벽 생성 (20x20)")]
        private static void CreateArenaWalls()
        {
            GameObject existing = GameObject.Find(ROOT_NAME);

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject(ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(root, "Create Arena Walls");

            float half = INNER_SIZE * 0.5f + WALL_THICKNESS * 0.5f;
            float outer = INNER_SIZE + WALL_THICKNESS * 2f;

            CreateWall(root.transform, "Wall_North", new Vector3(0f, 0f, half), new Vector3(outer, WALL_HEIGHT, WALL_THICKNESS));
            CreateWall(root.transform, "Wall_South", new Vector3(0f, 0f, -half), new Vector3(outer, WALL_HEIGHT, WALL_THICKNESS));
            CreateWall(root.transform, "Wall_East", new Vector3(half, 0f, 0f), new Vector3(WALL_THICKNESS, WALL_HEIGHT, INNER_SIZE));
            CreateWall(root.transform, "Wall_West", new Vector3(-half, 0f, 0f), new Vector3(WALL_THICKNESS, WALL_HEIGHT, INNER_SIZE));

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"SandboxTools: {ROOT_NAME} 생성 (안쪽 {INNER_SIZE} x {INNER_SIZE}, 높이 {WALL_HEIGHT}). 씬을 저장하세요.");
        }

        // 바닥(y = 0) 위에 서도록 높이의 절반만큼 올려 배치
        private static void CreateWall(Transform parent, string wallName, Vector3 position, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallName;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position + Vector3.up * (size.y * 0.5f);
            wall.transform.localScale = size;

            NavMeshObstacle obstacle = wall.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }
    }
}
