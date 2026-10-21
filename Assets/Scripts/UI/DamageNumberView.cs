using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 피해 숫자 (10/21 연출): 플레이어가 준 피해(흰색, 치명타는 크고 노란색)와 플레이어가 받은 피해(빨간색)를
    /// 맞은 위치 위에 띄워 위로 떠오르며 사라지게 합니다. 글자 오브젝트는 재사용(풀).
    /// HudView가 생성합니다.
    /// </summary>
    public class DamageNumberView : MonoBehaviour
    {
        private const int POOL_SIZE = 48;
        private const float LIFETIME = 0.9f;
        private const float RISE = 70f;
        private const float HEAD_HEIGHT = 2.2f;

        private static Color DEALT => UiTheme.P.Damage;
        private static Color CRIT => UiTheme.P.Crit;
        private static Color TAKEN => UiTheme.P.Taken;

        private sealed class Number
        {
            public Text Text;
            public Vector3 World;
            public Vector2 Jitter;
            public float StartTime;
            public float BaseScale;
            public bool Active;
        }

        private readonly List<Number> _pool = new List<Number>();
        private GameObject _player;
        private Camera _camera;
        private RectTransform _root;
        private int _next;

        public void Initialize(GameObject player)
        {
            _player = player;
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);

            for (int i = 0; i < POOL_SIZE; i++)
            {
                Text text = UiKit.SceneLabel($"Number_{i}", _root, 26, Color.white, TextAnchor.MiddleCenter);
                text.rectTransform.sizeDelta = new Vector2(200f, 40f);
                text.enabled = false;
                _pool.Add(new Number { Text = text });
            }

            Health.AnyDamaged += OnAnyDamaged;
            HealthDebugView.HudShowsPopups = true;
        }

        private void OnDestroy()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            HealthDebugView.HudShowsPopups = false;
        }

        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            if (victim == null || info.Amount <= 0f || _player == null)
            {
                return;
            }

            bool taken = victim.gameObject == _player;
            bool dealt = info.Attacker == _player;

            if (!taken && !dealt)
            {
                return;
            }

            Number number = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            number.Active = true;
            number.StartTime = Time.time;
            number.World = victim.transform.position + Vector3.up * HEAD_HEIGHT;
            number.Jitter = new Vector2(Random.Range(-28f, 28f), Random.Range(-8f, 8f));
            number.Text.text = Mathf.CeilToInt(info.Amount).ToString();
            number.Text.color = taken ? TAKEN : info.IsCritical ? CRIT : DEALT;
            number.BaseScale = info.IsCritical ? 1.4f : taken ? 1.05f : 1f;
            number.Text.enabled = true;
            number.Text.transform.SetAsLastSibling();
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;

                if (_camera == null)
                {
                    return;
                }
            }

            Canvas canvas = _root.GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;

            foreach (Number number in _pool)
            {
                if (!number.Active)
                {
                    continue;
                }

                float t = (Time.time - number.StartTime) / LIFETIME;
                Vector3 screen = _camera.WorldToScreenPoint(number.World);

                if (t >= 1f || screen.z < 0f)
                {
                    number.Active = false;
                    number.Text.enabled = false;
                    continue;
                }

                // 화면 좌표 → 캔버스 좌표 (Screen Space Overlay, 왼쪽 아래 기준)
                Vector2 position = new Vector2(screen.x, screen.y) / scaleFactor + number.Jitter
                    + Vector2.up * (RISE * Mathf.Sqrt(t));
                RectTransform rect = number.Text.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.anchoredPosition = position;

                // 처음 0.12초 커졌다가 원래 크기, 마지막 40% 페이드
                float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
                rect.localScale = Vector3.one * number.BaseScale * pop;
                Color color = number.Text.color;
                color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                number.Text.color = color;
            }
        }
    }
}
