using System.Collections;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 테스트용: 사망 시 모형과 충돌체를 숨기고 일정 시간 뒤 체력을 회복해 다시 나타납니다.
    /// 일반 적 AI·스포너 구현 후 제거합니다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TestDummyRespawner : MonoBehaviour
    {
        [SerializeField] private float _respawnDelay = 3f;

        private Health _health;
        private Renderer[] _renderers;
        private Collider[] _colliders;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
        }

        private void OnDied()
        {
            SetVisible(false);
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_respawnDelay);

            _health.RestoreFull();
            SetVisible(true);
        }

        // 오브젝트 자체를 끄면 코루틴이 멈추므로 표시·충돌만 끔
        private void SetVisible(bool visible)
        {
            foreach (Renderer meshRenderer in _renderers)
            {
                meshRenderer.enabled = visible;
            }

            foreach (Collider bodyCollider in _colliders)
            {
                bodyCollider.enabled = visible;
            }
        }
    }
}
