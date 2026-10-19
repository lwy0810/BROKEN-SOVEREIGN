using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 마우스 커서 변경: 매 프레임 마우스 아래를 레이캐스트해 첫 물체의 레이어로 CursorSettings 규칙을 고릅니다.
    /// 전체 화면 UI(지도·선택 화면·대화 창)가 열려 있으면 기본 커서. 바뀔 때만 Cursor.SetCursor 호출.
    /// HudView가 생성합니다.
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        private CursorSettings _settings;
        private Texture2D _current;
        private int _mask;

        public void Initialize(CursorSettings settings)
        {
            _settings = settings;
            _mask = 0;

            foreach (CursorSettings.Rule rule in settings.Rules)
            {
                _mask |= rule.Layers.value;
            }

            Apply(settings.DefaultTexture, settings.DefaultHotspot);
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void Update()
        {
            if (_settings == null)
            {
                return;
            }

            CursorSettings.Rule rule = GameplayInput.IsBlocked ? null : FindRule();

            if (rule != null)
            {
                Apply(rule.Texture, rule.Hotspot);
            }
            else
            {
                Apply(_settings.DefaultTexture, _settings.DefaultHotspot);
            }
        }

        private CursorSettings.Rule FindRule()
        {
            Camera camera = Camera.main;
            Mouse mouse = Mouse.current;

            if (camera == null || mouse == null || _mask == 0)
            {
                return null;
            }

            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());

            // 규칙 레이어만 검사 (땅 등 다른 물체 뒤에 가려진 적·NPC도 잡히도록)
            if (!Physics.Raycast(ray, out RaycastHit hit, _settings.RayDistance, _mask, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            int layerBit = 1 << hit.collider.gameObject.layer;

            foreach (CursorSettings.Rule rule in _settings.Rules)
            {
                if ((rule.Layers.value & layerBit) != 0)
                {
                    return rule;
                }
            }

            return null;
        }

        private void Apply(Texture2D texture, Vector2 hotspot)
        {
            if (texture == _current)
            {
                return;
            }

            _current = texture;
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        }
    }
}
