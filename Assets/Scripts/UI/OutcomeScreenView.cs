using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 결과 화면 (10/21 연출): 플레이어 사망 시 "쓰러졌다" + R 부활, 보스 처치 시 "프로토타입 클리어".
    /// 부활 (10/26): 쓰러진 위치에서 가장 가까운 '발견한' 웨이포인트에서 체력 전부 회복
    /// (발견한 웨이포인트가 없으면 가장 가까운 웨이포인트 → PlayerStart → 그 자리 순).
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
        private Image _reviveButton;     // 10/26: 사망 시 [가까운 웨이포인트에서 부활] 버튼 (R 키도 그대로)
        private Image _reviveHighlight;
        private Image _lineTop;
        private Image _lineBottom;
        private Outline _titleGlow;
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

            _dim = UiKit.Box("Dim", root, UiTheme.P.CrackGlow ? new Color(0f, 0f, 0f, 0.75f) : UiTheme.P.Dim);
            // 가운데 띠 + 위아래 빛줄 (B안)
            Image band = UiKit.Box("Band", root, new Color(0f, 0f, 0f, 0.45f));
            UiKit.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1920f, 230f));
            _lineTop = UiKit.Box("LineTop", root, UiTheme.P.CrackOuter);
            UiKit.Place(_lineTop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(900f, 2f));
            _lineBottom = UiKit.Box("LineBottom", root, UiTheme.P.CrackOuter);
            UiKit.Place(_lineBottom.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(900f, 2f));
            HudGraphics.Stretch(_dim.rectTransform);
            // 10/26: 글자 크기 88 → 84, 위아래 빛줄(y 110 · -12)의 정가운데(49)에 맞춤.
            // 세리프 서체는 글자가 줄 상자 안에서 아래로 치우쳐 보여 +8 보정
            _title = UiKit.Title("Title", root, 84, UiKit.GOLD, TextAnchor.MiddleCenter);
            _titleGlow = _title.gameObject.AddComponent<Outline>();
            _titleGlow.effectDistance = new Vector2(2f, -2f);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 57f),
                new Vector2(1400f, 100f));
            _sub = UiKit.Title("Sub", root, 24, UiKit.SUBTLE, TextAnchor.MiddleCenter);
            UiKit.Place(_sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                new Vector2(1400f, 40f));
            BuildReviveButton(root);
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
            _title.color = UiTheme.P.Danger;
            SetAccent(UiTheme.P.Danger);
            _sub.text = string.Empty;
            _reviveButton.gameObject.SetActive(true);
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
            SetAccent(UiTheme.P.CrackOuter);
            _sub.text = $"{boss.BossName}을(를) 쓰러뜨렸다 — 프로토타입 클리어 (Enter: 계속)";
            _reviveButton.gameObject.SetActive(false);
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

            bool hover = _isDeath && UiKit.IsHover(_reviveButton.rectTransform);
            _reviveHighlight.enabled = hover;
            _reviveButton.color = hover ? Color.white : new Color(0.85f, 0.85f, 0.88f);

            if (_isDeath && (keyboard.rKey.wasPressedThisFrame || (hover && UiKit.Clicked)))
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

        // [가까운 웨이포인트에서 부활] 버튼: 빛줄 아래, 오른쪽에 R 키 칸. 오버 시 거래 선택지와 같은 금색 강조
        private void BuildReviveButton(RectTransform root)
        {
            UiPalette p = UiTheme.P;
            _reviveButton = UiKit.Sliced("Revive", root, UiSprites.Button);
            UiKit.Place(_reviveButton.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(380f, 54f));

            _reviveHighlight = UiKit.Box("Highlight", _reviveButton.rectTransform, UiTheme.WithAlpha(p.Title, 0.28f));
            HudGraphics.Stretch(_reviveHighlight.rectTransform);
            _reviveHighlight.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Title, 0.9f);
            _reviveHighlight.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            _reviveHighlight.enabled = false;

            Text label = UiKit.Label("Label", _reviveButton.rectTransform, 19, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-16f, 0f), new Vector2(300f, 54f));
            label.text = "가까운 웨이포인트에서 부활";

            Image keyBox = UiKit.Box("Key", _reviveButton.rectTransform, p.Inset);
            UiKit.Place(keyBox.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(28f, 26f));
            Text keyText = UiKit.Label("KeyText", keyBox.rectTransform, 14, p.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(keyText.rectTransform);
            keyText.text = "R";
            _reviveButton.gameObject.SetActive(false);
        }

        // 띠의 빛줄·제목 테두리 색 (사망 = 붉은빛, 클리어 = 금빛)
        private void SetAccent(Color color)
        {
            _lineTop.color = color;
            _lineBottom.color = color;
            _titleGlow.effectColor = UiTheme.WithAlpha(color, 0.45f);
        }

        private void Revive()
        {
            _isDeath = false;
            Unblock();
            _group.alpha = 0f;

            // 10/26: 쓰러진 자리에서 가장 가까운 웨이포인트 (발견한 것 우선)
            Waypoint waypoint = _player != null
                ? Waypoint.Nearest(_player.position, true) ?? Waypoint.Nearest(_player.position, false)
                : null;

            if (waypoint != null)
            {
                if (_player.TryGetComponent(out WaypointTraveler traveler))
                {
                    traveler.ArriveAt(waypoint);
                }
                else
                {
                    PlayerWarp.Warp(_player, waypoint.transform.position, waypoint.transform.forward);
                }

                _health.RestoreFull();
                return;
            }

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
