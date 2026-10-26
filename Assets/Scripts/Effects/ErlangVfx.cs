using System.Collections;
using UnityEngine;

namespace BrokenSovereign.Effects
{
    /// <summary>
    /// 이랑진군 패턴 코드 이펙트 (10/26). 프리팹 없이 LineRenderer·Light로 만들고 스스로 지워짐.
    /// - Beam: 제3의 눈 광선. 가장자리가 부드러운 겹 빛줄기(바깥 光 + 출렁이는 띠 + 하얀 심) · 흔들림 · 끝점 섬광
    /// - EyeCharge: 광선 예고. 이마 위치에 빛이 모임
    /// - Shockwave: 내려찍기 번개 뒤 지면을 따라 퍼지는 고리 + 갈래 번개
    /// </summary>
    public class ErlangVfx : MonoBehaviour
    {
        private const int BEAM_POINTS = 28;
        private static Texture2D _soft;

        // 가로(폭) 방향으로 가운데가 밝고 가장자리가 투명한 띠 텍스처 → 직사각형 느낌 제거
        private static Texture2D SoftTexture
        {
            get
            {
                if (_soft != null)
                {
                    return _soft;
                }

                const int size = 64;
                _soft = new Texture2D(4, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ErlangSoft" };

                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Exp(-v * v * 3.2f);

                    for (int x = 0; x < 4; x++)
                    {
                        _soft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                _soft.Apply();
                return _soft;
            }
        }

        // ------------------------------------------------------------------
        // 광선
        // ------------------------------------------------------------------

        public static void Beam(Vector3 from, Vector3 to, float width, Color color, float duration = 0.55f)
        {
            var go = new GameObject("ErlangBeam");
            go.transform.position = from;
            go.AddComponent<ErlangVfx>().StartCoroutine(RunBeam(go, from, to, width, color, duration));
        }

        private static IEnumerator RunBeam(GameObject owner, Vector3 from, Vector3 to, float width, Color color, float duration)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            Vector3 dir = length > 0.001f ? axis / length : Vector3.forward;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            side = side.sqrMagnitude > 0.001f ? side.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(side, dir).normalized;

            // 겹: 바깥 빛(넓고 옅음) · 출렁이는 띠 2줄 · 하얀 심
            LineRenderer glow = Line(owner, "Glow", BEAM_POINTS, WithAlpha(color, 0.35f));
            LineRenderer waveA = Line(owner, "WaveA", BEAM_POINTS, WithAlpha(Color.Lerp(color, Color.white, 0.2f), 0.75f));
            LineRenderer waveB = Line(owner, "WaveB", BEAM_POINTS, WithAlpha(Color.Lerp(color, Color.white, 0.2f), 0.75f));
            LineRenderer core = Line(owner, "Core", BEAM_POINTS, new Color(1f, 0.98f, 0.9f, 1f));

            // 눈에서 가늘게 시작해 굵어지고 끝은 흩어짐
            var taper = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.12f, 0.9f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.55f));

            Light eyeLight = PointLight(owner, from, color, 6f);
            Light endLight = PointLight(owner, to + Vector3.up * 0.5f, color, 9f);
            Light midLight = PointLight(owner, Vector3.Lerp(from, to, 0.5f), color, 12f);

            float grow = Mathf.Min(0.08f, duration * 0.2f);
            float fade = duration * 0.45f;
            float start = Time.time;

            while (Time.time - start < duration)
            {
                float t = Time.time - start;
                float open = t < grow ? t / grow : t > duration - fade ? Mathf.Clamp01((duration - t) / fade) : 1f;
                float flicker = 0.88f + 0.12f * Mathf.Sin(t * 70f) + Random.Range(-0.05f, 0.05f);
                float w = width * open * flicker;

                for (int i = 0; i < BEAM_POINTS; i++)
                {
                    float u = (float)i / (BEAM_POINTS - 1);
                    Vector3 p = from + dir * (length * u);
                    // 출렁임: 앞으로 흐르는 나선 (끝으로 갈수록 크게)
                    float phase = u * length * 1.6f - t * 26f;
                    float amp = width * 0.22f * (0.3f + u) * open;
                    Vector3 offset = side * Mathf.Sin(phase) + up * Mathf.Cos(phase);

                    glow.SetPosition(i, p);
                    core.SetPosition(i, p + Random.insideUnitSphere * (0.03f * open));
                    waveA.SetPosition(i, p + offset * amp);
                    waveB.SetPosition(i, p - offset * amp);
                }

                glow.widthCurve = taper;
                glow.widthMultiplier = w * 2.4f;
                waveA.widthCurve = taper;
                waveA.widthMultiplier = w * 0.75f;
                waveB.widthCurve = taper;
                waveB.widthMultiplier = w * 0.75f;
                core.widthCurve = taper;
                core.widthMultiplier = w * 0.32f;

                eyeLight.intensity = 6f * open;
                midLight.intensity = 5f * open * flicker;
                endLight.intensity = 9f * open * flicker;
                yield return null;
            }

            Destroy(owner);
        }

        // ------------------------------------------------------------------
        // 광선 예고 (이마에 빛이 모임)
        // ------------------------------------------------------------------

        public static void EyeCharge(Transform caster, float height, Color color, float duration)
        {
            var go = new GameObject("ErlangEyeCharge");
            go.AddComponent<ErlangVfx>().StartCoroutine(RunCharge(go, caster, height, color, duration));
        }

        private static IEnumerator RunCharge(GameObject owner, Transform caster, float height, Color color, float duration)
        {
            Light light = PointLight(owner, Vector3.zero, color, 5f);
            LineRenderer slit = Line(owner, "Slit", 2, WithAlpha(Color.Lerp(color, Color.white, 0.5f), 0.9f));
            float start = Time.time;

            while (Time.time - start < duration && caster != null)
            {
                float t = (Time.time - start) / duration;
                Vector3 eye = caster.position + Vector3.up * height + caster.forward * 0.35f;
                light.transform.position = eye;
                light.intensity = Mathf.Lerp(0.5f, 7f, t * t) * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f));
                // 세로로 갈라진 눈빛이 점점 커짐
                float half = Mathf.Lerp(0.05f, 0.35f, t);
                slit.SetPosition(0, eye - Vector3.up * half);
                slit.SetPosition(1, eye + Vector3.up * half);
                slit.widthMultiplier = Mathf.Lerp(0.08f, 0.5f, t);
                yield return null;
            }

            Destroy(owner);
        }

        // ------------------------------------------------------------------
        // 내려찍기 충격파 (번개 뒤 퍼짐)
        // ------------------------------------------------------------------

        public static void Shockwave(Vector3 center, float radius, Color color, float duration = 0.4f, int forks = 8)
        {
            var go = new GameObject("ErlangShockwave");
            go.transform.position = center;
            go.AddComponent<ErlangVfx>().StartCoroutine(RunShockwave(go, center, radius, color, duration, forks));
        }

        private static IEnumerator RunShockwave(GameObject owner, Vector3 center, float radius, Color color, float duration, int forks)
        {
            const int ringPoints = 48;
            const int forkPoints = 7;
            Vector3 ground = center + Vector3.up * 0.12f;

            LineRenderer ring = Line(owner, "Ring", ringPoints + 1, WithAlpha(Color.Lerp(color, Color.white, 0.3f), 0.95f));
            LineRenderer ringGlow = Line(owner, "RingGlow", ringPoints + 1, WithAlpha(color, 0.4f));
            var forkLines = new LineRenderer[forks];
            var forkAngles = new float[forks];

            for (int f = 0; f < forks; f++)
            {
                forkLines[f] = Line(owner, "Fork", forkPoints, WithAlpha(Color.Lerp(color, Color.white, 0.5f), 0.9f));
                forkAngles[f] = (360f / forks) * f + Random.Range(-12f, 12f);
            }

            Light light = PointLight(owner, center + Vector3.up * 2f, color, radius * 3f);
            float start = Time.time;
            float total = duration + 0.25f;
            float nextJitter = 0f;

            while (Time.time - start < total)
            {
                float t = (Time.time - start) / duration;
                float spread = Mathf.Clamp01(t);
                float ease = 1f - (1f - spread) * (1f - spread);
                float r = Mathf.Max(0.1f, radius * ease);
                float alpha = t <= 1f ? 1f : Mathf.Clamp01(1f - (t - 1f) * duration / 0.25f);

                for (int i = 0; i <= ringPoints; i++)
                {
                    float a = i * Mathf.PI * 2f / ringPoints;
                    Vector3 p = ground + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    ring.SetPosition(i, p);
                    ringGlow.SetPosition(i, p);
                }

                ring.widthMultiplier = Mathf.Lerp(0.55f, 0.15f, spread) * alpha;
                ringGlow.widthMultiplier = Mathf.Lerp(1.6f, 0.6f, spread) * alpha;

                // 갈래 번개: 가운데에서 고리까지 지그재그 (자주 다시 그림)
                if (Time.time >= nextJitter)
                {
                    nextJitter = Time.time + 0.05f;

                    for (int f = 0; f < forks; f++)
                    {
                        float a = forkAngles[f] * Mathf.Deg2Rad;
                        Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Vector3 across = new Vector3(-outward.z, 0f, outward.x);

                        for (int i = 0; i < forkPoints; i++)
                        {
                            float u = (float)i / (forkPoints - 1);
                            float jitter = i == 0 || i == forkPoints - 1 ? 0f : Random.Range(-0.45f, 0.45f);
                            forkLines[f].SetPosition(i, ground + outward * (r * u) + across * jitter + Vector3.up * Random.Range(0f, 0.15f));
                        }

                        forkLines[f].enabled = Random.value > 0.15f;
                    }
                }

                foreach (LineRenderer fork in forkLines)
                {
                    fork.widthMultiplier = 0.16f * alpha;
                }

                light.intensity = Mathf.Lerp(16f, 3f, spread) * alpha;
                yield return null;
            }

            Destroy(owner);
        }

        // ------------------------------------------------------------------
        // 공용
        // ------------------------------------------------------------------

        private static LineRenderer Line(GameObject owner, string name, int points, Color color)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(owner.transform, false);
            line.useWorldSpace = true;
            line.positionCount = points;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                line.material = new Material(shader) { mainTexture = SoftTexture };
            }

            line.startColor = line.endColor = color;
            return line;
        }

        private static Light PointLight(GameObject owner, Vector3 position, Color color, float range)
        {
            Light light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(owner.transform, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = 0f;
            return light;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void OnDestroy()
        {
            foreach (LineRenderer line in GetComponentsInChildren<LineRenderer>(true))
            {
                if (line.sharedMaterial != null)
                {
                    Destroy(line.sharedMaterial);
                }
            }
        }
    }
}
