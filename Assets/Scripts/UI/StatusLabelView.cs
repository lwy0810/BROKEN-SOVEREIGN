using System.Collections.Generic;
using System.Text;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 적 머리 위 효과 이름: 능력치 효과(도발·전장의 함성 등 StatusEffectHost 이름)와 기절·공중·무력화를
    /// "이름 · 이름"으로 표시합니다. 몸 색은 바꾸지 않습니다 (10/21 요청).
    /// 10/26: 효과 이름은 걸린 순간 약 1초만 표시 (나타남 0.15초 → 유지 → 사라짐 0.35초). 새 효과가 더해지면 다시 표시.
    /// 피격되거나 효과가 걸린 적만 추적하고, 효과가 없이 일정 시간이 지나면 추적 해제. HudView가 생성합니다.
    /// </summary>
    public class StatusLabelView : MonoBehaviour
    {
        private const float HEAD_HEIGHT = 2.75f;
        private const float TRACK_TIME = 10f;
        private const float SHOW_TIME = 1f;
        private const float FADE_IN = 0.15f;
        private const float FADE_OUT = 0.35f;

        private static Color COLOR => UiTheme.P.Status;

        private sealed class Entry
        {
            public Health Health;
            public EnemyController Enemy;
            public PoiseReceiver Poise;
            public StatusEffectHost Host;
            public float LastActiveTime;
            public Text Text;
            public string Shown = string.Empty;
            public float ShownAt;
        }

        private readonly Dictionary<Health, Entry> _entries = new Dictionary<Health, Entry>();
        private readonly List<Health> _removed = new List<Health>();
        private readonly List<string> _names = new List<string>();
        private readonly StringBuilder _builder = new StringBuilder();
        private GameObject _player;
        private RectTransform _root;
        private Camera _camera;

        public void Initialize(GameObject player)
        {
            _player = player;
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);
            Health.AnyDamaged += OnAnyDamaged;
            StatusEffectHost.AnyApplied += OnEffectApplied;
        }

        private void OnDestroy()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            StatusEffectHost.AnyApplied -= OnEffectApplied;
        }

        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            Track(victim);
        }

        private void OnEffectApplied(StatusEffectHost host, string displayName)
        {
            if (host != null)
            {
                Track(host.GetComponent<Health>());
            }
        }

        private void Track(Health health)
        {
            if (health == null || health.gameObject == _player)
            {
                return;
            }

            if (!_entries.TryGetValue(health, out Entry entry))
            {
                entry = new Entry
                {
                    Health = health,
                    Enemy = health.GetComponent<EnemyController>(),
                    Poise = health.GetComponent<PoiseReceiver>(),
                    Text = UiKit.SceneLabel("Status", _root, 17, COLOR, TextAnchor.MiddleCenter)
                };
                entry.Text.rectTransform.sizeDelta = new Vector2(320f, 26f);
                entry.Text.rectTransform.anchorMin = entry.Text.rectTransform.anchorMax = Vector2.zero;
                entry.Text.enabled = false;
                _entries[health] = entry;
            }

            entry.LastActiveTime = Time.time;
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            Canvas canvas = _root.GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            bool hidden = _camera == null || GameplayInput.IsBlocked;

            foreach (KeyValuePair<Health, Entry> pair in _entries)
            {
                Entry entry = pair.Value;

                if (entry.Health == null)
                {
                    _removed.Add(pair.Key);
                    continue;
                }

                string label = entry.Health.IsDead ? string.Empty : BuildLabel(entry);

                if (label.Length > 0)
                {
                    entry.LastActiveTime = Time.time;
                }
                else if (Time.time - entry.LastActiveTime > TRACK_TIME)
                {
                    _removed.Add(pair.Key);
                    continue;
                }

                // 효과 구성이 바뀌면(새로 걸림) 표시 시간을 다시 시작
                if (label != entry.Shown)
                {
                    entry.Shown = label;
                    entry.ShownAt = Time.time;
                }

                float elapsed = Time.time - entry.ShownAt;
                Vector3 screen = hidden ? Vector3.back
                    : _camera.WorldToScreenPoint(entry.Health.transform.position + Vector3.up * HEAD_HEIGHT);
                bool visible = label.Length > 0 && elapsed < SHOW_TIME && screen.z > 0f;
                entry.Text.enabled = visible;

                if (!visible)
                {
                    continue;
                }

                if (entry.Text.text != label)
                {
                    entry.Text.text = label;
                }

                float alpha = Mathf.Min(Mathf.Clamp01(elapsed / FADE_IN), Mathf.Clamp01((SHOW_TIME - elapsed) / FADE_OUT));
                Color color = COLOR;
                color.a *= alpha;
                entry.Text.color = color;

                entry.Text.rectTransform.anchoredPosition = new Vector2(screen.x, screen.y) / scaleFactor;
            }

            foreach (Health key in _removed)
            {
                Entry entry = _entries[key];

                if (entry.Text != null)
                {
                    Destroy(entry.Text.gameObject);
                }

                _entries.Remove(key);
            }

            _removed.Clear();
        }

        private string BuildLabel(Entry entry)
        {
            _builder.Clear();

            if (entry.Host == null)
            {
                entry.Host = entry.Health.GetComponent<StatusEffectHost>();
            }

            if (entry.Host != null && entry.Host.Count > 0)
            {
                entry.Host.GetActiveNames(_names);

                foreach (string effectName in _names)
                {
                    Append(effectName);
                }
            }

            if (entry.Enemy != null)
            {
                if (entry.Enemy.IsStunned)
                {
                    Append("기절");
                }

                if (entry.Enemy.IsAirborne)
                {
                    Append("공중");
                }
            }

            if (entry.Poise != null && entry.Poise.IsStaggered)
            {
                Append("무력화");
            }

            return _builder.ToString();
        }

        private void Append(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (_builder.Length > 0)
            {
                _builder.Append(" · ");
            }

            _builder.Append(text);
        }
    }
}
