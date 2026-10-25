using System.Collections;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 보스방 등장 연출 (10/25 개편): 정사각형 보스방(이 오브젝트 = 방 중심) 안으로 플레이어가 들어오면
    /// 방 중앙 표식 → 하늘에서 번개가 떨어짐 → 번개 자리에서 보스가 커지며 나타남.
    /// 연출 중: BossStatus.IsIntro = true → 무적(IInvulnerabilitySource), 보스 체력바 회색, AI 정지(ExternalControl, ErlangBoss 꺼짐).
    /// 연출 끝: 원래대로 (공격 가능, 체력바 원래 색, AI 시작). 한 번만 동작. BossSetupTools가 배치합니다.
    /// 번개 모양은 LineRenderer 임시 연출 → 전용 VFX가 생기면 Strike만 교체.
    /// </summary>
    public class BossAppearTrigger : MonoBehaviour
    {
        [SerializeField] private GameObject _boss;
        [Tooltip("보스방 한 변의 절반 (m)")]
        [SerializeField, Min(1f)] private float _half = 25f;
        [Tooltip("벽에서 이만큼 안쪽으로 들어오면 시작 (m)")]
        [SerializeField, Min(0f)] private float _inset = 3f;
        [SerializeField, Min(0f)] private float _warnTime = 0.7f;     // 중앙 표식 → 번개까지
        [SerializeField, Min(0.1f)] private float _strikeTime = 0.5f;  // 번개 깜빡임
        [SerializeField, Min(0.05f)] private float _growTime = 0.6f;
        [SerializeField, Min(0f)] private float _holdTime = 0.8f;     // 나타난 뒤 무적 유지
        [SerializeField] private Color _boltColor = new Color(0.75f, 0.88f, 1f);

        private const float BOLT_HEIGHT = 35f;
        private const int BOLT_SEGMENTS = 14;

        private Transform _player;
        private bool _started;

        public void Configure(GameObject boss, float half)
        {
            _boss = boss;
            _half = half;
        }

        private void Start()
        {
            if (_boss != null && !_started)
            {
                _boss.SetActive(false);
            }

            GameObject player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;

            // 10/26: 입구 구름 장막 (보스 교전 중 밖으로 못 나감)
            if (!TryGetComponent(out BossArenaBarrier barrier))
            {
                barrier = gameObject.AddComponent<BossArenaBarrier>();
            }

            barrier.Configure(_boss, _half);
        }

        private void Update()
        {
            if (_started || _boss == null || _player == null)
            {
                return;
            }

            Vector3 offset = _player.position - transform.position;
            float limit = _half - _inset;

            if (Mathf.Abs(offset.x) <= limit && Mathf.Abs(offset.z) <= limit && Mathf.Abs(offset.y) < 8f)
            {
                _started = true;
                StartCoroutine(Intro());
            }
        }

        private IEnumerator Intro()
        {
            BossStatus status = _boss.GetComponent<BossStatus>();
            EnemyController controller = _boss.GetComponent<EnemyController>();
            ErlangBoss pattern = _boss.GetComponent<ErlangBoss>();

            // 꺼진 상태에서 미리 설정 → 켜지는 순간부터 무적·회색 체력바·AI 정지
            if (status != null)
            {
                status.IsIntro = true;
            }

            if (controller != null)
            {
                controller.ExternalControl = true;
            }

            if (pattern != null)
            {
                pattern.enabled = false;
            }

            Vector3 center = _boss.transform.position;
            AttackAreaDebugDrawer.ShowCircle(center, 3.5f, center.y, new Color(_boltColor.r, _boltColor.g, _boltColor.b, 0.5f), _warnTime + _strikeTime);
            yield return new WaitForSeconds(_warnTime);

            // 번개 + 보스 등장
            StartCoroutine(Strike(center));
            _boss.SetActive(true);
            Transform visual = _boss.transform.Find("Visual");
            Vector3 scale = visual != null ? visual.localScale : Vector3.one;
            AttackAreaDebugDrawer.ShowCircle(center, 4.5f, center.y, new Color(1f, 0.85f, 0.4f, 0.55f), _growTime + 0.6f);

            for (float t = 0f; t < 1f; t += Time.deltaTime / _growTime)
            {
                if (visual != null)
                {
                    float eased = 1f - (1f - t) * (1f - t);
                    visual.localScale = scale * Mathf.Max(0.01f, eased);
                }

                yield return null;
            }

            if (visual != null)
            {
                visual.localScale = scale;
            }

            yield return new WaitForSeconds(_holdTime);

            // 연출 끝: 공격 가능, 체력바 원래 색, AI 시작
            if (status != null)
            {
                status.IsIntro = false;
            }

            if (controller != null)
            {
                controller.ExternalControl = false;
            }

            if (pattern != null)
            {
                pattern.enabled = true;
            }
        }

        // 하늘에서 지그재그 번개 (몇 번 깜빡이며 모양이 바뀜) + 섬광
        private IEnumerator Strike(Vector3 ground)
        {
            var bolt = new GameObject("BossIntroBolt").AddComponent<LineRenderer>();
            bolt.positionCount = BOLT_SEGMENTS + 1;
            bolt.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1.1f));
            bolt.numCornerVertices = 2;
            Shader shader = Shader.Find("Sprites/Default");
            bolt.material = new Material(shader);
            bolt.startColor = bolt.endColor = _boltColor;

            Light flash = new GameObject("BossIntroFlash").AddComponent<Light>();
            flash.transform.position = ground + Vector3.up * 4f;
            flash.type = LightType.Point;
            flash.color = _boltColor;
            flash.range = 30f;

            float end = Time.time + _strikeTime;
            float next = 0f;

            while (Time.time < end)
            {
                if (Time.time >= next)
                {
                    next = Time.time + 0.06f;

                    for (int i = 0; i <= BOLT_SEGMENTS; i++)
                    {
                        float t = (float)i / BOLT_SEGMENTS;
                        Vector2 jitter = i == 0 || i == BOLT_SEGMENTS ? Vector2.zero : Random.insideUnitCircle * 1.6f;
                        bolt.SetPosition(i, ground + new Vector3(jitter.x, BOLT_HEIGHT * (1f - t), jitter.y));
                    }

                    bolt.enabled = Random.value > 0.2f;
                    flash.intensity = bolt.enabled ? 14f : 4f;
                }

                yield return null;
            }

            Destroy(bolt.material);
            Destroy(bolt.gameObject);

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                flash.intensity = Mathf.Lerp(6f, 0f, t);
                yield return null;
            }

            Destroy(flash.gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3((_half - _inset) * 2f, 2f, (_half - _inset) * 2f));
        }
    }
}
