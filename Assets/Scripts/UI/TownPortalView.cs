using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 귀환 포탈 시전 막대 (화면 가운데 아래) + 안내 문구 (10/25). HudView가 생성합니다.
    /// </summary>
    public class TownPortalView : MonoBehaviour
    {
        private const float TOAST_SECONDS = 2f;

        private TownPortalCaster _caster;
        private RectTransform _root;
        private UiBar _bar;
        private Text _toast;
        private float _toastUntil;

        public void Initialize(TownPortalCaster caster)
        {
            _caster = caster;
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);

            UiPalette p = UiTheme.P;
            _bar = UiKit.Bar("CastBar", _root, p.Accent, p.Accent * 0.5f);
            UiKit.Place(_bar.Rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(260f, 18f));
            _bar.EnableText(13).text = "마을 귀환";
            _bar.Rect.gameObject.SetActive(false);

            _toast = UiKit.SceneLabel("PortalToast", _root, 22, p.Title, TextAnchor.MiddleCenter);
            UiKit.Place(_toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(700f, 36f));
            _toast.gameObject.SetActive(false);

            _caster.Message += OnMessage;
        }

        private void OnDestroy()
        {
            if (_caster != null)
            {
                _caster.Message -= OnMessage;
            }
        }

        private void OnMessage(string text)
        {
            _toast.text = text;
            _toastUntil = Time.unscaledTime + TOAST_SECONDS;
        }

        private void LateUpdate()
        {
            if (_caster == null)
            {
                return;
            }

            bool casting = _caster.IsCasting;
            _bar.Rect.gameObject.SetActive(casting);

            if (casting)
            {
                _bar.SetRatio(_caster.CastProgress);
            }

            _toast.gameObject.SetActive(Time.unscaledTime < _toastUntil);
        }
    }
}
