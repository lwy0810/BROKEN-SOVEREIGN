using System.Collections;
using BrokenSovereign.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 보스방 입구 구름 장막 (10/26): 보스 교전이 시작되면 입구를 구름 막으로 막아 밖으로 나갈 수 없게 합니다.
    /// - 올라감: 보스가 살아 있고 교전 중(등장 연출 포함)이며 플레이어가 방 안에 있을 때
    /// - 걷힘: 플레이어 사망 또는 보스 격파 시 (부활 후 다시 들어와 교전하면 다시 올라감)
    /// - 막기: NavMeshObstacle(Carve)로 길찾기·이동을 막음 (클릭 이동·회피 모두 NavMesh 기준)
    /// - 모양: 부드러운 원형 입자(구름)가 입구 폭을 따라 피어오름 + 옅은 막
    /// BossAppearTrigger(방 중심)가 자동으로 붙입니다. 입구 방향·폭은 인스펙터에서 조절.
    /// </summary>
    public class BossArenaBarrier : MonoBehaviour
    {
        [Tooltip("방 중심 → 입구 방향 (x, z). 남쪽 계단 = (0, -1)")]
        [SerializeField] private Vector2 _entryDirection = Vector2.down;
        [Tooltip("보스방 한 변의 절반 (m) — 입구는 중심에서 이 거리의 벽 위")]
        [SerializeField, Min(1f)] private float _half = 25f;
        [Tooltip("막 폭 (m, 입구 계단 폭보다 넉넉히)")]
        [SerializeField, Min(1f)] private float _width = 12f;
        [SerializeField, Min(1f)] private float _height = 6f;
        [Tooltip("플레이어가 벽에서 이만큼 안쪽에 있어야 '방 안'")]
        [SerializeField, Min(0f)] private float _inset = 3f;
        [SerializeField] private Color _cloudColor = new Color(0.92f, 0.95f, 1f, 0.55f);

        private GameObject _boss;
        private BossStatus _bossStatus;
        private Transform _player;
        private Health _playerHealth;
        private GameObject _barrier;
        private ParticleSystem _particles;
        private Material _cloudMaterial;
        private Material _veilMaterial;
        private Renderer _veil;
        private bool _raised;
        private static Texture2D _softTexture;

        public bool IsRaised => _raised;

        public void Configure(GameObject boss, float half)
        {
            _boss = boss;
            _half = half;
        }

        private void Start()
        {
            GameObject player = GameObject.FindWithTag("Player");

            if (player != null)
            {
                _player = player.transform;
                _playerHealth = player.GetComponent<Health>();
            }
        }

        private void Update()
        {
            if (_boss != null && _bossStatus == null)
            {
                _bossStatus = _boss.GetComponent<BossStatus>();
            }

            bool playerAlive = _playerHealth == null || !_playerHealth.IsDead;
            bool bossAlive = _bossStatus != null && _boss.activeInHierarchy && !_bossStatus.IsDead;

            if (!_raised)
            {
                if (playerAlive && bossAlive && _bossStatus.IsEngaged && PlayerInside())
                {
                    Raise();
                }
            }
            else if (!playerAlive || !bossAlive)
            {
                Lower();
            }
        }

        private bool PlayerInside()
        {
            if (_player == null)
            {
                return false;
            }

            Vector3 offset = _player.position - transform.position;
            float limit = _half - _inset;
            return Mathf.Abs(offset.x) <= limit && Mathf.Abs(offset.z) <= limit && Mathf.Abs(offset.y) < 8f;
        }

        // ------------------------------------------------------------------
        // 올림·걷힘
        // ------------------------------------------------------------------

        private void Raise()
        {
            _raised = true;

            if (_barrier == null)
            {
                Build();
            }

            StopAllCoroutines();
            _barrier.SetActive(true);
            _particles.Play();
            StartCoroutine(FadeVeil(0f, _cloudColor.a * 0.45f, 0.8f));
        }

        private void Lower()
        {
            _raised = false;
            StopAllCoroutines();
            StartCoroutine(Dissolve());
        }

        private IEnumerator Dissolve()
        {
            if (_barrier == null)
            {
                yield break;
            }

            // 길은 바로 열고, 구름은 서서히 흩어짐
            _barrier.GetComponent<NavMeshObstacle>().enabled = false;
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return FadeVeil(_veilMaterial.color.a, 0f, 1.2f);
            yield return new WaitForSeconds(2f);

            if (!_raised)
            {
                _barrier.SetActive(false);
            }
        }

        private IEnumerator FadeVeil(float from, float to, float time)
        {
            if (to > 0f)
            {
                _barrier.GetComponent<NavMeshObstacle>().enabled = true;
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                SetVeilAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetVeilAlpha(to);
        }

        private void SetVeilAlpha(float alpha)
        {
            Color c = _veilMaterial.color;
            c.a = alpha;
            _veilMaterial.color = c;
            _veil.enabled = alpha > 0.001f;
        }

        // ------------------------------------------------------------------
        // 생성
        // ------------------------------------------------------------------

        private void Build()
        {
            Vector3 dir = new Vector3(_entryDirection.x, 0f, _entryDirection.y).normalized;
            Vector3 ground = transform.position + dir * _half;

            // 입구 바닥 높이 맞추기 (방 바닥 높이 기준, NavMesh가 있으면 그 위)
            if (NavMesh.SamplePosition(ground, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                ground.y = hit.position.y;
            }

            _barrier = new GameObject("BossArenaBarrier");
            _barrier.transform.SetParent(transform, true);
            _barrier.transform.position = ground;
            _barrier.transform.rotation = Quaternion.LookRotation(dir);   // 로컬 X = 입구 폭 방향

            NavMeshObstacle obstacle = _barrier.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, _height * 0.5f, 0f);
            obstacle.size = new Vector3(_width, _height, 2.5f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;

            BuildVeil();
            BuildClouds();
            _barrier.SetActive(false);
        }

        // 옅은 막 (사각형 하나)
        private void BuildVeil()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Veil";
            DestroyImmediate(quad.GetComponent<Collider>());   // 클릭 이동 레이캐스트에 걸리지 않게
            quad.transform.SetParent(_barrier.transform, false);
            quad.transform.localPosition = new Vector3(0f, _height * 0.5f, 0f);
            quad.transform.localScale = new Vector3(_width, _height, 1f);
            _veil = quad.GetComponent<Renderer>();
            _veilMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftTexture() };
            _veilMaterial.color = new Color(_cloudColor.r, _cloudColor.g, _cloudColor.b, 0f);
            _veil.sharedMaterial = _veilMaterial;
            _veil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // Sprites/Default는 양면 (Cull Off)
        }

        // 구름 입자: 입구 폭을 따라 피어올라 천천히 흩어짐
        private void BuildClouds()
        {
            var go = new GameObject("Clouds");
            go.transform.SetParent(_barrier.transform, false);
            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(_cloudColor.r, _cloudColor.g, _cloudColor.b, _cloudColor.a * 0.7f), _cloudColor);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.gravityModifier = -0.02f;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTime = 45f;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, _height * 0.45f, 0f);
            shape.scale = new Vector3(_width, _height * 0.9f, 0.6f);

            ParticleSystem.ColorOverLifetimeModule color = _particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f)));

            ParticleSystem.RotationOverLifetimeModule rotation = _particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            _cloudMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftTexture() };
            renderer.sharedMaterial = _cloudMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 가장자리가 부드러운 원 (구름 한 덩이)
        private static Texture2D SoftTexture()
        {
            if (_softTexture != null)
            {
                return _softTexture;
            }

            const int size = 64;
            _softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "CloudSoft", wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    _softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }

            _softTexture.Apply();
            return _softTexture;
        }

        private void OnDestroy()
        {
            if (_cloudMaterial != null)
            {
                Destroy(_cloudMaterial);
            }

            if (_veilMaterial != null)
            {
                Destroy(_veilMaterial);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 dir = new Vector3(_entryDirection.x, 0f, _entryDirection.y).normalized;
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.6f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position + dir * _half + Vector3.up * _height * 0.5f,
                Quaternion.LookRotation(dir), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_width, _height, 2.5f));
        }
    }
}
