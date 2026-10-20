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
    /// 피격되거나 효과가 걸린 적만 추적하고, 효과가 없이 일정 시간이 지나면 추적 해제. HudView가 생성합니다.
    /// </summary>
    public class StatusLabelView : MonoBehaviour
    {
        private const float HEAD_HEIGHT = 2.75f;
        private const float TRACK_TIME = 10f;

        private static readonly Color COLOR = new Color(1f, 0.62f, 0.25f);

        private sealed class Entry
        {
            public Health Health;
            public EnemyController Enemy;
            public PoiseReceiver Poise;
            public StatusEffectHost Host;
            public float LastActiveTime;
            public Text Text;
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
                    Text = UiKit.Label("Status", _root, 17, COLOR, TextAnchor.MiddleCenter, FontStyle.Bold)
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

                Vector3 screen = hidden ? Vector3.back
                    : _camera.WorldToScreenPoint(entry.Health.transform.position + Vector3.up * HEAD_HEIGHT);
                bool visible = label.Length > 0 && screen.z > 0f;
                entry.Text.enabled = visible;

                if (!visible)
                {
                    continue;
                }

                if (entry.Text.text != label)
                {
                    entry.Text.text = label;
                }

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
