using System.Collections;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 임시 디버그 표시: 머리 위 체력 막대(전투 중이고 가까울 때만), 피해 숫자, 피격 시 붉은 깜빡임, 무적 표시.
    /// 플레이어·적 공용. HUD·이펙트 구현 시 제거합니다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HealthDebugView : MonoBehaviour
    {
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

        [Tooltip("머리 위 체력 막대 표시 (HUD가 있는 플레이어는 끔)")]
        [SerializeField] private bool _showBar = true;
        [SerializeField] private float _barHeight = 2.3f;
        [SerializeField] private Vector2 _barPixelSize = new Vector2(40f, 4f);

        [Tooltip("체력 막대 위 수치(현재/최대) 표시")]
        [SerializeField] private bool _showValueText;

        [Header("표시 조건")]
        [Tooltip("켜면 전투 중이고 가까이 있을 때만 체력 막대 표시")]
        [SerializeField] private bool _hideWhenOutOfCombat = true;

        [Tooltip("마지막 피격 후 이 시간(초)까지 전투 중으로 판단")]
        [SerializeField] private float _combatDuration = 5f;

        [Tooltip("기준 대상(플레이어)과 이 거리(m) 안일 때만 표시")]
        [SerializeField] private float _showDistance = 12f;

        [Tooltip("거리 기준 대상. 비우면 Player 태그 오브젝트를 찾음")]
        [SerializeField] private Transform _distanceReference;
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
            public float Scale;
        }

        private readonly List<Popup> _popups = new List<Popup>();
        private Health _health;
        private IStunnable _stunnable;
        private Renderer[] _renderers;
        private MaterialPropertyBlock[] _savedBlocks;
        private MaterialPropertyBlock _flashBlock;
        private Coroutine _flashRoutine;
        private GUIStyle _popupStyle;
        private int _popupBaseFontSize;
        private GUIStyle _labelStyle;
        private float _lastDamagedTime = float.NegativeInfinity;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _stunnable = GetComponent<IStunnable>();
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
            _health.Dodged += OnDodged;

            if (_stunnable != null)
            {
                _stunnable.Stunned += OnStunned;
            }
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.DamageIgnored -= OnDamageIgnored;
            _health.Dodged -= OnDodged;

            if (_stunnable != null)
            {
                _stunnable.Stunned -= OnStunned;
            }
        }

        private void Start()
        {
            if (_distanceReference == null)
            {
                GameObject reference = GameObject.FindWithTag("Player");

                if (reference != null && reference.transform != transform)
                {
                    _distanceReference = reference.transform;
                }
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            _lastDamagedTime = Time.time;
            if (info.IsCritical)
            {
                AddPopup($"{info.Amount:0}!", new Color(1f, 0.85f, 0.2f), 1.5f);
            }
            else
            {
                AddPopup($"{info.Amount:0}", Color.white);
            }

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

        private void OnStunned(float duration)
        {
            AddPopup("기절", new Color(1f, 0.6f, 0.2f));
        }

        private void OnDodged(DamageInfo info)
        {
            AddPopup("회피", new Color(0.7f, 0.7f, 0.7f));
        }

        private void AddPopup(string text, Color color, float scale = 1f)
        {
            _popups.Add(new Popup { Text = text, Color = color, StartTime = Time.time, Scale = scale });
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
            // OnGUI는 모든 Canvas 위에 그려지므로 직업 선택 화면 등 전체 화면 UI가 열려 있으면 숨김
            if (GameplayInput.IsBlocked)
            {
                return;
            }

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

            float x = screen.x - _barPixelSize.x * 0.5f;
            float y = Screen.height - screen.y;

            EnsureStyles();

            if (ShouldShowBar())
            {
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x, y, _barPixelSize.x, _barPixelSize.y), Texture2D.whiteTexture);
                GUI.color = _barColor;
                GUI.DrawTexture(new Rect(x, y, _barPixelSize.x * (_health.Current / _health.Max), _barPixelSize.y), Texture2D.whiteTexture);
                GUI.color = previous;

                if (_showValueText)
                {
                    GUI.Label(new Rect(x - 20f, y - 18f, _barPixelSize.x + 40f, 20f), $"{_health.Current:0}/{_health.Max:0}", _labelStyle);
                }
            }

            DrawPopups(screen.x, y);
        }

        // 사망 시 숨김. 조건부 표시가 켜져 있으면 전투 중(최근 피격) + 기준 대상과 가까울 때만 표시
        private bool ShouldShowBar()
        {
            if (!_showBar || _health.IsDead)
            {
                return false;
            }

            if (!_hideWhenOutOfCombat)
            {
                return true;
            }

            bool inCombat = Time.time - _lastDamagedTime <= _combatDuration;
            bool isNear = _distanceReference == null
                || (_distanceReference.position - transform.position).sqrMagnitude <= _showDistance * _showDistance;
            return inCombat && isNear;
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
                float scale = Mathf.Max(1f, _popups[i].Scale);
                _popupStyle.normal.textColor = color;
                _popupStyle.fontSize = Mathf.RoundToInt(_popupBaseFontSize * scale);
                float width = 80f * scale;
                float height = 24f * scale;
                GUI.Label(new Rect(centerX - width * 0.5f, baseY - 40f - t * 30f - height * 0.5f, width, height),
                    _popups[i].Text, _popupStyle);
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
            _popupBaseFontSize = _popupStyle.fontSize;
        }
    }
}
