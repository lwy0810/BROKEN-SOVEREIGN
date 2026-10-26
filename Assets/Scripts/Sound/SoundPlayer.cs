using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Sound
{
    /// <summary>
    /// 효과음·음악 재생 (10/26). 어디서든 SoundPlayer.Play(SoundId.X) 한 줄로 호출합니다.
    /// - 클립: Resources/Audio/{이름}, 변형 {이름}_2 … _4 중 무작위. 없으면 무음 (한 번만 경고 없이 넘어감)
    /// - 효과음: AudioSource 풀(2D). PlayAt은 플레이어(듣는 위치)와의 거리로 음량을 줄임 (쿼터뷰라 3D 감쇠 대신)
    /// - 같은 소리를 아주 짧은 간격(0.05초)으로 겹쳐 틀지 않음 (적 여럿 동시 타격)
    /// - 음악: 두 소스 교차 페이드
    /// </summary>
    public static class SoundPlayer
    {
        private const string ROOT = "Audio/";
        private const int POOL_SIZE = 20;
        private const int MAX_VARIANTS = 4;
        private const float REPEAT_GUARD = 0.05f;
        private const float NEAR = 12f;
        private const float FAR = 45f;

        // 10/26: 음량은 설정 창(GameSettings, PlayerPrefs)에서 읽음
        public static float MasterVolume => Core.GameSettings.MasterVolume;
        public static float SfxVolume => Core.GameSettings.SfxVolume;
        public static float MusicVolume => Core.GameSettings.MusicVolume;

        private static readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private static AudioSource[] _pool;
        private static int _next;
        private static AudioSource _musicA;
        private static AudioSource _musicB;
        private static string _musicId;
        private static Transform _listener;
        private static SoundHost _host;

        /// <summary>파일이 하나라도 있는지 (기술별 소리 → 공용 소리 대체 판단)</summary>
        public static bool Has(string id) => Load(id).Length > 0;

        /// <summary>화면 소리 (거리 무관)</summary>
        public static void Play(string id, float volume = 1f, float pitchJitter = 0.04f)
        {
            PlayInternal(id, volume, pitchJitter);
        }

        /// <summary>월드 위치 소리: 듣는 위치에서 멀수록 작게, 아주 멀면 생략</summary>
        public static void PlayAt(string id, Vector3 position, float volume = 1f, float pitchJitter = 0.06f)
        {
            Transform listener = Listener();

            if (listener != null)
            {
                float distance = Vector3.Distance(listener.position, position);

                if (distance > FAR)
                {
                    return;
                }

                volume *= 1f - Mathf.Clamp01((distance - NEAR) / (FAR - NEAR)) * 0.85f;
            }

            PlayInternal(id, volume, pitchJitter);
        }

        /// <summary>음악 바꾸기 (같은 곡이면 그대로). null이면 멈춤</summary>
        public static void PlayMusic(string id, float fade = 1.5f)
        {
            if (_musicId == id)
            {
                return;
            }

            AudioClip[] clips = string.IsNullOrEmpty(id) ? System.Array.Empty<AudioClip>() : Load(id);

            if (!string.IsNullOrEmpty(id) && clips.Length == 0)
            {
                return;   // 파일이 아직 없으면 지금 곡 유지
            }

            _musicId = id;
            Ensure();
            AudioSource from = _musicA.isPlaying && _musicA.volume > 0f ? _musicA : _musicB;
            AudioSource to = from == _musicA ? _musicB : _musicA;

            if (clips.Length > 0)
            {
                to.clip = clips[0];
                to.volume = 0f;
                to.Play();
            }

            _host.Crossfade(from, clips.Length > 0 ? to : null, fade);
        }

        /// <summary>듣는 위치 (플레이어, 없으면 카메라)</summary>
        public static void SetListener(Transform listener)
        {
            _listener = listener;
        }

        internal static float MusicTarget => MasterVolume * MusicVolume;

        private static void PlayInternal(string id, float volume, float pitchJitter)
        {
            AudioClip[] clips = Load(id);

            if (clips.Length == 0)
            {
                return;
            }

            float now = Time.unscaledTime;

            if (_lastPlayed.TryGetValue(id, out float last) && now - last < REPEAT_GUARD)
            {
                return;
            }

            _lastPlayed[id] = now;
            Ensure();
            AudioSource source = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], Mathf.Clamp01(volume) * SfxVolume * MasterVolume);
        }

        private static AudioClip[] Load(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return System.Array.Empty<AudioClip>();
            }

            if (_clips.TryGetValue(id, out AudioClip[] cached))
            {
                return cached;
            }

            var list = new List<AudioClip>();
            AudioClip first = Resources.Load<AudioClip>(ROOT + id);

            if (first != null)
            {
                list.Add(first);
            }

            for (int i = 2; i <= MAX_VARIANTS; i++)
            {
                AudioClip variant = Resources.Load<AudioClip>($"{ROOT}{id}_{i}");

                if (variant != null)
                {
                    list.Add(variant);
                }
            }

            AudioClip[] clips = list.ToArray();
            _clips[id] = clips;
            return clips;
        }

        private static Transform Listener()
        {
            if (_listener != null)
            {
                return _listener;
            }

            Camera camera = Camera.main;
            return camera != null ? camera.transform : null;
        }

        private static void Ensure()
        {
            if (_host != null)
            {
                return;
            }

            var go = new GameObject("SoundPlayer");
            Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<SoundHost>();
            _pool = new AudioSource[POOL_SIZE];

            for (int i = 0; i < POOL_SIZE; i++)
            {
                _pool[i] = NewSource(go, false);
            }

            _musicA = NewSource(go, true);
            _musicB = NewSource(go, true);
        }

        private static AudioSource NewSource(GameObject go, bool loop)
        {
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        // 플레이 모드 재시작(도메인 리로드 끔) 대비 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _clips.Clear();
            _lastPlayed.Clear();
            _host = null;
            _pool = null;
            _musicId = null;
            _listener = null;
        }
    }

    /// <summary>SoundPlayer의 코루틴·업데이트용 (씬 전환에도 유지)</summary>
    internal class SoundHost : MonoBehaviour
    {
        private AudioSource _fadeOut;
        private AudioSource _fadeIn;
        private float _fadeTime = 1f;
        private float _t = 1f;

        public void Crossfade(AudioSource from, AudioSource to, float time)
        {
            _fadeOut = from;
            _fadeIn = to;
            _fadeTime = Mathf.Max(0.01f, time);
            _t = 0f;
        }

        private void Update()
        {
            float target = SoundPlayer.MusicTarget;

            if (_t >= 1f)
            {
                if (_fadeIn != null)
                {
                    _fadeIn.volume = target;
                }

                return;
            }

            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / _fadeTime);

            if (_fadeIn != null)
            {
                _fadeIn.volume = target * _t;
            }

            if (_fadeOut != null && _fadeOut != _fadeIn)
            {
                _fadeOut.volume = target * (1f - _t);

                if (_t >= 1f)
                {
                    _fadeOut.Stop();
                }
            }
        }
    }
}
