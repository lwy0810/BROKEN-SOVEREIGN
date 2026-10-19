using BrokenSovereign.Map;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 오른쪽 위 정사각형 미니맵: 플레이어 주변 Radius(m)를 보여 줍니다. 위쪽 = 쿼터뷰 카메라가 바라보는 방향.
    /// 지도 그림은 LevelMap(맵 생성 때 미리 그린 그림)을 플레이어 위치에 맞춰 움직이고 카메라 방향만큼 돌립니다.
    /// 아이콘: MapMarker 목록 (적은 반경 안만). 지도가 없는 씬에서는 숨김. 테두리는 얇은 금선 (10/19).
    /// HudView가 생성합니다.
    /// </summary>
    public class MinimapView : MonoBehaviour
    {
        private const float SIZE = 220f;
        private const float MARGIN = 20f;
        private const float RADIUS = 120f;      // 가운데에서 가장자리까지 보여 주는 거리 (m)
        private const float ICON_SIZE = 24f;
        private const float FRAME = 1.5f;       // 금테 두께 (px)

        private Transform _player;
        private CanvasGroup _group;
        private MapLayer _layer;

        private static float PixelsPerMeter => SIZE * 0.5f / RADIUS;

        public void Initialize(Transform player)
        {
            _player = player;
            RectTransform root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-MARGIN, -MARGIN);
            root.sizeDelta = new Vector2(SIZE, SIZE);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            // 바탕 + 얇은 금테
            Image background = root.gameObject.AddComponent<Image>();
            background.sprite = HudGraphics.WhiteSprite;
            background.color = new Color(0.05f, 0.06f, 0.07f, 0.85f);
            background.raycastTarget = false;
            Outline frame = root.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(0.9f, 0.75f, 0.42f);
            frame.effectDistance = new Vector2(FRAME, -FRAME);
            Outline frameInner = root.gameObject.AddComponent<Outline>();
            frameInner.effectColor = frame.effectColor;
            frameInner.effectDistance = new Vector2(-FRAME, FRAME);

            // 정사각형 마스크 (회전하는 지도 모서리 자르기)
            RectTransform mask = HudGraphics.CreateRect("Mask", root);
            HudGraphics.Stretch(mask);
            Image maskImage = mask.gameObject.AddComponent<Image>();
            maskImage.sprite = HudGraphics.WhiteSprite;
            maskImage.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _layer = new MapLayer(mask, ICON_SIZE);
        }

        private void LateUpdate()
        {
            LevelMap level = LevelMap.Current;
            _group.alpha = level != null && level.Texture != null ? 1f : 0f;

            if (_group.alpha == 0f || _player == null)
            {
                return;
            }

            Vector3 position = _player.position;
            float diagonal = RADIUS * 1.42f;
            _layer.Update(level, new Vector2(position.x, position.z), PixelsPerMeter, MapLayer.CameraYaw(), _player,
                diagonal, RADIUS * 2f, true);
        }
    }
}
