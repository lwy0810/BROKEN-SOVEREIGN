using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 임시 디버그 표시: 공격 범위를 바닥에 반투명 부채꼴로 잠깐 그립니다.
    /// 실제 이펙트로 교체할 때 호출부와 함께 제거합니다.
    /// </summary>
    public static class AttackAreaDebugDrawer
    {
        private const int SEGMENTS = 20;
        private const float GROUND_OFFSET = 0.03f;
        private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

        private static readonly Dictionary<Vector2, Mesh> _meshCache = new Dictionary<Vector2, Mesh>();
        private static Material _material;

        public static void ShowCone(Vector3 origin, Vector3 forward, float radius, float angle, float groundY,
            Color color, float duration)
        {
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            GameObject area = new GameObject("AttackAreaDebug");
            area.transform.SetPositionAndRotation(
                new Vector3(origin.x, groundY + GROUND_OFFSET, origin.z),
                Quaternion.LookRotation(forward.normalized, Vector3.up));

            area.AddComponent<MeshFilter>().sharedMesh = GetConeMesh(radius, angle);

            MeshRenderer meshRenderer = area.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetMaterial();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(COLOR_ID, color);
            meshRenderer.SetPropertyBlock(block);

            Object.Destroy(area, duration);
        }

        /// <summary>원형 범위 표시 (360도 부채꼴)</summary>
        public static void ShowCircle(Vector3 origin, float radius, float groundY, Color color, float duration)
        {
            ShowCone(origin, Vector3.forward, radius, 360f, groundY, color, duration);
        }

        /// <summary>직선(사각형) 범위 표시: origin에서 forward로 length, 폭 width (보스 광선 등)</summary>
        public static void ShowLine(Vector3 origin, Vector3 forward, float length, float width, float groundY,
            Color color, float duration)
        {
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            GameObject area = new GameObject("AttackAreaDebugLine");
            area.transform.SetPositionAndRotation(
                new Vector3(origin.x, groundY + GROUND_OFFSET, origin.z),
                Quaternion.LookRotation(forward.normalized, Vector3.up));

            float half = width * 0.5f;
            var mesh = new Mesh { name = "AttackAreaLine" };
            mesh.vertices = new[]
            {
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f),
                new Vector3(-half, 0f, length), new Vector3(half, 0f, length),
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            area.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer meshRenderer = area.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetMaterial();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(COLOR_ID, color);
            meshRenderer.SetPropertyBlock(block);

            Object.Destroy(mesh, duration);
            Object.Destroy(area, duration);
        }

        // 같은 크기의 부채꼴은 메시를 재사용 (매 공격마다 메시 생성 방지)
        private static Mesh GetConeMesh(float radius, float angle)
        {
            Vector2 key = new Vector2(radius, angle);

            if (!_meshCache.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = BuildConeMesh(radius, angle);
                _meshCache[key] = mesh;
            }

            return mesh;
        }

        private static Mesh BuildConeMesh(float radius, float angle)
        {
            Vector3[] vertices = new Vector3[SEGMENTS + 2];
            int[] triangles = new int[SEGMENTS * 3];
            float halfAngle = angle * 0.5f;

            vertices[0] = Vector3.zero;

            for (int i = 0; i <= SEGMENTS; i++)
            {
                float degree = Mathf.Lerp(-halfAngle, halfAngle, (float)i / SEGMENTS);
                vertices[i + 1] = Quaternion.Euler(0f, degree, 0f) * Vector3.forward * radius;
            }

            for (int i = 0; i < SEGMENTS; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            Mesh mesh = new Mesh { name = "AttackAreaCone" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material GetMaterial()
        {
            if (_material == null)
            {
                // 반투명 단색 표시용. 빌드 포함이 필요해지면 머티리얼 에셋으로 교체
                _material = new Material(Shader.Find("Sprites/Default"));
            }

            return _material;
        }
    }
}
