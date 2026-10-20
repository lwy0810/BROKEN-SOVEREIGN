using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 결과 화면 (10/21 연출): 플레이어 사망 시 "쓰러졌다" + R 부활, 보스 처치 시 "프로토타입 클리어".
    /// 부활: 체력 전부 회복 후 PlayerStart(없으면 그 자리)로 이동.
    /// 사망 화면은 전투 입력 차단, 클리어 화면은 게임 일시정지. HudView가 생성합니다.
    /// </summary>
    public class OutcomeScreenView : MonoBehaviour
    {
        private const float DEATH_DELAY = 1.2f;
        private const float CLEAR_DELAY = 3.2f;

        private Health _health;
        private Transform _player;
        private Image _dim;
        private Text _title;
        private Text _sub;
        private CanvasGroup _group;
        private float _showTime = float.PositiveInfinity;
        private bool _isDeath;
        private bool _isClear;
        private bool _blocking;
        private float _previousTimeScale = 1f;

        public void Initialize(Health health)
        {
            _health = health;
            _player = health != null ? health.transform : null;
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;

            _dim = UiKit.Box("Dim", root, new Color(0f, 0f, 0f, 0.7f));
            HudGraphics.Stretch(_dim.rectTransform);
            _title = UiKit.Label("Title", root, 72, UiKit.GOLD, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                new Vector2(1400f, 90f));
            _sub = UiKit.Label("Sub", root, 26, UiKit.SUBTLE, TextAnchor.MiddleCenter);
            UiKit.Place(_sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                new Vector2(1400f, 40f));
            _group.alpha = 0f;

            if (_health != null)
            {
                _health.Died += OnPlayerDied;
                _health.Restored += OnPlayerRestored;
            }

            BossStatus.AnyDefeated += OnBossDefeated;
        }

        // 다른 경로(Inspector·디버그)로 되살아나도 사망 화면을 닫음
        private void OnPlayerRestored()
        {
            if (!_isDeath)
            {
                return;
            }

            _isDeath = false;
            Unblock();
            _group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnPlayerDied;
                _health.Restored -= OnPlayerRestored;
            }

            BossStatus.AnyDefeated -= OnBossDefeated;
            Unblock();
        }

        private void OnPlayerDied()
        {
            _isDeath = true;
            _isClear = false;
            _title.text = "쓰러졌다";
            _title.color = new Color(0.85f, 0.2f, 0.18f);
            _sub.text = "R — 마을에서 다시 일어나기";
            _showTime = Time.time + DEATH_DELAY;
        }

        private void OnBossDefeated(BossStatus boss)
        {
            if (_isDeath)
            {
                return;
            }

            _isClear = true;
            _title.text = "천계 평정";
            _title.color = UiKit.GOLD;
            _sub.text = $"{boss.BossName}을(를) 쓰러뜨렸다 — 프로토타입 클리어 (Enter: 계속)";
            _showTime = Time.time + CLEAR_DELAY;
        }

        private void Update()
        {
            bool visible = (_isDeath || _isClear) && Time.time >= _showTime;

            if (_isClear && !_blocking && Time.time >= _showTime)
            {
                // 클리어: 일시정지
                _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                _blocking = true;
                GameplayInput.IsBlocked = true;
            }
            else if (_isDeath && !_blocking && Time.time >= _showTime)
            {
                _blocking = true;
                GameplayInput.IsBlocked = true;
            }

            _group.alpha = Mathf.MoveTowards(_group.alpha, visible || (_isClear && _blocking) ? 1f : 0f,
                Time.unscaledDeltaTime * 2.5f);

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || !_blocking)
            {
                return;
            }

            if (_isDeath && keyboard.rKey.wasPressedThisFrame)
            {
                Revive();
            }
            else if (_isClear && (keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                _isClear = false;
                Time.timeScale = _previousTimeScale;
                Unblock();
                _group.alpha = 0f;
            }
        }

        private void Revive()
        {
            _isDeath = false;
            Unblock();
            _group.alpha = 0f;

            GameObject start = GameObject.Find("PlayerStart");

            if (start != null && _player != null)
            {
                if (_player.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
                {
                    agent.Warp(start.transform.position);
                }
                else
                {
                    _player.position = start.transform.position;
                }
            }

            _health.RestoreFull();
        }

        private void Unblock()
        {
            if (_blocking)
            {
                _blocking = false;
                GameplayInput.IsBlocked = false;
            }
        }
    }
}
