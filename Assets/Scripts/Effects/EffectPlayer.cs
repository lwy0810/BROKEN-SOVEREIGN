using System.Collections;
using UnityEngine;

namespace BrokenSovereign.Effects
{
    /// <summary>
    /// 이펙트 재생 (10/26). 어디서든 EffectPlayer.Play(prefab, 위치) 한 줄로 호출합니다.
    /// - Cartoon FX 프리팹은 재생이 끝나면 스스로 삭제 (CFXR_Effect). 그 외에도 안전하게 최대 8초 뒤 삭제
    /// - 크기: 파티클 Scaling Mode를 Hierarchy로 바꿔 transform 크기를 따름
    /// - Follow: 대상을 따라다니며, 유지 시간이 지나면 방출을 멈추고 남은 입자가 사라진 뒤 삭제
    /// </summary>
    public static class EffectPlayer
    {
        private const float SAFETY_LIFETIME = 8f;
        private static EffectHost _host;

        /// <summary>한 번 재생 (위치·크기·방향)</summary>
        public static GameObject Play(GameObject prefab, Vector3 position, float scale = 1f, Quaternion? rotation = null)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject effect = Object.Instantiate(prefab, position, rotation ?? prefab.transform.rotation);
            Prepare(effect, scale);
            Object.Destroy(effect, SAFETY_LIFETIME);
            return effect;
        }

        /// <summary>대상을 따라다니며 재생. lifetime(초)이 지나거나 keepWhile이 false가 되면 방출 정지 (≤0 = 이펙트 자체 길이)</summary>
        public static GameObject Follow(GameObject prefab, Transform target, float scale, float lifetime, System.Func<bool> keepWhile = null)
        {
            if (prefab == null || target == null)
            {
                return null;
            }

            GameObject effect = Object.Instantiate(prefab, target.position, prefab.transform.rotation, target);
            Prepare(effect, scale);

            // 유지형(반복) 이펙트를 원하는 시간만 유지: 스스로 삭제하는 스크립트(CFXR_Effect)는 끄고 직접 정리
            if (lifetime > 0f || keepWhile != null)
            {
                foreach (MonoBehaviour behaviour in effect.GetComponents<MonoBehaviour>())
                {
                    if (behaviour.GetType().Name == "CFXR_Effect")
                    {
                        behaviour.enabled = false;
                    }
                }

                foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = system.main;
                    main.loop = true;
                }

                Host().StartCoroutine(StopLater(effect, lifetime, keepWhile));
            }
            else
            {
                Object.Destroy(effect, SAFETY_LIFETIME);
            }

            return effect;
        }

        private static void Prepare(GameObject effect, float scale)
        {
            effect.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);

            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        private static IEnumerator StopLater(GameObject effect, float lifetime, System.Func<bool> keepWhile)
        {
            float end = lifetime > 0f ? Time.time + lifetime : float.PositiveInfinity;

            while (effect != null && Time.time < end && (keepWhile == null || keepWhile()))
            {
                yield return null;
            }

            if (effect == null)
            {
                yield break;
            }

            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>())
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            effect.transform.SetParent(null, true);   // 대상이 사라져도 남은 입자는 자연스럽게
            Object.Destroy(effect, 2.5f);
        }

        private static EffectHost Host()
        {
            if (_host == null)
            {
                var go = new GameObject("EffectPlayer");
                Object.DontDestroyOnLoad(go);
                _host = go.AddComponent<EffectHost>();
            }

            return _host;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _host = null;
        }
    }

    /// <summary>EffectPlayer 코루틴용 (씬 전환에도 유지)</summary>
    internal class EffectHost : MonoBehaviour
    {
    }
}
