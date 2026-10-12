using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 임시 디버그 표시: 머리 위 체력 막대, 피해 숫자, 피격 시 붉은 깜빡임, 무적 표시.
    /// 플레이어·적 공용. HUD·이펙트 구현 시 제거합니다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HealthDebugView : MonoBehaviour
    {
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float _barHeight = 2.3f;
        [SerializeField] private Vector2 _barSize = new Vector2(80f, 8f);
        [SerializeField] private Color _barColor = new Color(0.9f, 0.15f, 0.15f);
        [SerializeField] private bool _flashOnHit = true;
        [SerializeField] private Color _flashColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float _flashDuration = 0.12f;
        [SerializeField] private float _popupDuration = 0.8f;

        private struct Popup
        {
            public string Text;
            public Color Color;
            public float StartTime;
        }

        private readonly List<Popup> _popups = new List<Popup>();
        private Health _health;
        private Renderer[] _renderers;
        private MaterialPropertyBlock[] _savedBlocks;
        private MaterialPropertyBlock _flashBlock;
        private Coroutine _flashRoutine;
        private GUIStyle _popupStyle;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderers = GetComponentsInChildren<Renderer>();
            _savedBlocks = new MaterialPropertyBlock[_renderers.Length];

            for (int i = 0; i < _savedBlocks.Length; i++)
            {
                _savedBlocks[i] = new MaterialPropertyBlock();
            }

            _flashBlock = new MaterialPropertyBlock();
            _flashBlock.SetColor(BASE_COLOR_ID, _flashColor);
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.DamageIgnored += OnDamageIgnored;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.DamageIgnored -= OnDamageIgnored;
        }

        private void OnDamaged(DamageInfo info)
        {
            AddPopup($"-{info.Amount:0}", Color.white);

            if (_flashOnHit && isActiveAndEnabled)
            {
                if (_flashRoutine != null)
                {
                    StopCoroutine(_flashRoutine);
                    RestoreColors();
                }

                _flashRoutine = StartCoroutine(FlashRoutine());
            }
        }

        private void OnDamageIgnored(DamageInfo info)
        {
            AddPopup("무적", new Color(0.55f, 0.85f, 1f));
        }

        private void AddPopup(string text, Color color)
        {
            _popups.Add(new Popup { Text = text, Color = color, StartTime = Time.time });
        }

        private IEnumerator FlashRoutine()
        {
            // 다른 디버그 표시(PlayerDebugView 등)의 색 변경을 보존하기 위해 현재 값을 저장 후 복원
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].GetPropertyBlock(_savedBlocks[i]);
                _renderers[i].SetPropertyBlock(_flashBlock);
            }

            yield return new WaitForSeconds(_flashDuration);

            RestoreColors();
            _flashRoutine = null;
        }

        private void RestoreColors()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].SetPropertyBlock(_savedBlocks[i]);
            }
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;

            if (cam == null)
            {
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * _barHeight);

            if (screen.z < 0f)
            {
                return;
            }

            float x = screen.x - _barSize.x * 0.5f;
            float y = Screen.height - screen.y;

            EnsureStyles();

            if (_health.IsDead)
            {
                GUI.Label(new Rect(x, y - 18f, _barSize.x, 20f), "DEAD", _labelStyle);
            }
            else
            {
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x, y, _barSize.x, _barSize.y), Texture2D.whiteTexture);
                GUI.color = _barColor;
                GUI.DrawTexture(new Rect(x, y, _barSize.x * (_health.Current / _health.Max), _barSize.y), Texture2D.whiteTexture);
                GUI.color = previous;
                GUI.Label(new Rect(x, y - 18f, _barSize.x, 20f), $"{_health.Current:0}/{_health.Max:0}", _labelStyle);
            }

            DrawPopups(screen.x, y);
        }

        private void DrawPopups(float centerX, float baseY)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                float t = (Time.time - _popups[i].StartTime) / _popupDuration;

                if (t >= 1f)
                {
                    _popups.RemoveAt(i);
                    continue;
                }

                Color color = _popups[i].Color;
                color.a = 1f - t;
                _popupStyle.normal.textColor = color;
                GUI.Label(new Rect(centerX - 40f, baseY - 40f - t * 30f, 80f, 24f), _popups[i].Text, _popupStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            _labelStyle.normal.textColor = Color.white;
            _popupStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
