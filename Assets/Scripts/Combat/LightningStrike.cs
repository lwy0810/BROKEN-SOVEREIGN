using System.Collections;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 하늘에서 떨어지는 지그재그 번개 + 섬광 (10/25, 보스 등장·페이즈 전환 공용). Spawn으로 만들고 스스로 지워짐.
    /// 전용 VFX가 생기면 이 클래스만 교체.
    /// </summary>
    public class LightningStrike : MonoBehaviour
    {
        private const float HEIGHT = 35f;
        private const int SEGMENTS = 14;

        public static void Spawn(Vector3 ground, Color color, float duration = 0.5f, float jitter = 1.6f)
        {
            var go = new GameObject("LightningStrike");
            go.transform.position = ground;
            go.AddComponent<LightningStrike>().StartCoroutine(Run(go, ground, color, duration, jitter));
        }

        private static IEnumerator Run(GameObject owner, Vector3 ground, Color color, float duration, float jitter)
        {
            var bolt = owner.AddComponent<LineRenderer>();
            bolt.positionCount = SEGMENTS + 1;
            bolt.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1.1f));
            bolt.numCornerVertices = 2;
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bolt.material = new Material(Shader.Find("Sprites/Default"));
            bolt.startColor = bolt.endColor = color;

            Light flash = new GameObject("Flash").AddComponent<Light>();
            flash.transform.SetParent(owner.transform, false);
            flash.transform.position = ground + Vector3.up * 4f;
            flash.type = LightType.Point;
            flash.color = color;
            flash.range = 30f;

            float end = Time.time + duration;
            float next = 0f;

            while (Time.time < end)
            {
                if (Time.time >= next)
                {
                    next = Time.time + 0.06f;

                    for (int i = 0; i <= SEGMENTS; i++)
                    {
                        float t = (float)i / SEGMENTS;
                        Vector2 j = i == 0 || i == SEGMENTS ? Vector2.zero : Random.insideUnitCircle * jitter;
                        bolt.SetPosition(i, ground + new Vector3(j.x, HEIGHT * (1f - t), j.y));
                    }

                    bolt.enabled = Random.value > 0.2f;
                    flash.intensity = bolt.enabled ? 14f : 4f;
                }

                yield return null;
            }

            bolt.enabled = false;

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                flash.intensity = Mathf.Lerp(6f, 0f, t);
                yield return null;
            }

            Destroy(bolt.material);
            Destroy(owner);
        }
    }
}
