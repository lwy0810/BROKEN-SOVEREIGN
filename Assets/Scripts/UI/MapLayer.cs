using System.Collections.Generic;
using BrokenSovereign.Map;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 미니맵·전체 지도 공용 그리기: 전체 지도 그림 + 마을 세부 그림 + 아이콘 + 플레이어 화살표.
    /// 내용(Content)을 카메라 방향만큼 돌려 화면 위쪽 = 카메라가 바라보는 방향이 되게 합니다.
    /// 좌표: 내용 원점 = 보고 있는 월드 위치(ViewCenter), 1m = PixelsPerMeter px.
    /// </summary>
    public class MapLayer
    {
        private readonly RectTransform _content;
        private readonly RawImage _base;
        private readonly RawImage _detail;
        private readonly RectTransform _icons;
        private readonly RectTransform _player;
        private readonly RectTransform _pin;
        private readonly RectTransform _focus;      // 역이등변삼각형 표식 (10/25, 웨이포인트 선택)
        private Vector2? _focusWorld;
        private static Shader _styleShader;
        private readonly float _iconSize;
        private readonly Dictionary<MapMarker, RectTransform> _iconByMarker = new Dictionary<MapMarker, RectTransform>();
        private readonly List<MapMarker> _stale = new List<MapMarker>();

        /// <summary>parent 안을 채우는 회전 내용 생성. playerParent: 플레이어 화살표를 둘 곳 (null이면 내용 안)</summary>
        public MapLayer(RectTransform parent, float iconSize)
        {
            _iconSize = iconSize;
            _content = HudGraphics.CreateRect("Content", parent);
            _content.sizeDelta = Vector2.zero;

            _base = HudGraphics.CreateRect("Map", _content).gameObject.AddComponent<RawImage>();
            _base.raycastTarget = false;
            _base.material = CreateStyle(1.5f);
            _detail = HudGraphics.CreateRect("Detail", _content).gameObject.AddComponent<RawImage>();
            _detail.raycastTarget = false;
            _detail.material = CreateStyle(5f);
            _icons = HudGraphics.CreateRect("Icons", _content);
            _icons.sizeDelta = Vector2.zero;

            // 핀 포인트 (MapPin)
            Image pin = HudGraphics.CreateRect("Pin", _content).gameObject.AddComponent<Image>();
            pin.sprite = UiSprites.Pin;
            pin.raycastTarget = false;
            _pin = pin.rectTransform;
            _pin.sizeDelta = new Vector2(iconSize * 0.9f, iconSize * 1.2f);
            _pin.pivot = new Vector2(0.5f, 0.05f);

            _player = MapIcons.CreatePlayer(_content, iconSize * 0.95f);

            // 선택 표식: 아이콘 바로 위에 역이등변삼각형 (아래 꼭짓점이 아이콘 위를 가리킴)
            Image focus = HudGraphics.CreateRect("Focus", _content).gameObject.AddComponent<Image>();
            focus.sprite = UiSprites.MarkerDown;
            focus.raycastTarget = false;
            _focus = focus.rectTransform;
            _focus.sizeDelta = new Vector2(iconSize * 0.9f, iconSize * 0.8f);
            _focus.pivot = new Vector2(0.5f, -0.55f);   // 위치 = 아이콘 중심, 그림은 아이콘 위로 띄움
            _focus.gameObject.SetActive(false);
        }

        /// <summary>선택 표식 위치 (월드 x, z). null이면 숨김</summary>
        public void SetFocus(Vector2? world)
        {
            _focusWorld = world;
        }

        // 지도 스타일 셰이더 (Resources/UI/MapStyle). 없으면 원래 그림 그대로
        private static Material CreateStyle(float edgeWidth)
        {
            if (_styleShader == null)
            {
                _styleShader = Resources.Load<Shader>("UI/MapStyle");
            }

            if (_styleShader == null)
            {
                return null;
            }

            var material = new Material(_styleShader) { name = "MapStyle" };
            material.SetFloat("_EdgeWidth", edgeWidth);
            return material;
        }

        /// <summary>그림 투명도 (오버레이 표시용)</summary>
        public void SetAlpha(float alpha)
        {
            _base.color = new Color(1f, 1f, 1f, alpha);
            _detail.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// 다시 그리기. visibleRadius: 이 거리(m) 밖 아이콘 숨김 (0 = 제한 없음), viewWidth: 지도에 보이는 가로 폭(m, 아이콘 표시 조건)
        /// </summary>
        public void Update(LevelMap level, Vector2 viewCenter, float pixelsPerMeter, float yaw, Transform player,
            float visibleRadius, float viewWidth, bool includeEnemies)
        {
            _content.localRotation = Quaternion.Euler(0f, 0f, yaw);

            _base.texture = level.Texture;
            _base.rectTransform.sizeDelta = Vector2.one * (level.WorldSize * pixelsPerMeter);
            _base.rectTransform.anchoredPosition = (level.WorldCenter - viewCenter) * pixelsPerMeter;

            bool hasDetail = level.DetailTexture != null && level.DetailSize > 0f;
            _detail.gameObject.SetActive(hasDetail);

            if (hasDetail)
            {
                _detail.texture = level.DetailTexture;
                _detail.rectTransform.sizeDelta = Vector2.one * (level.DetailSize * pixelsPerMeter);
                _detail.rectTransform.anchoredPosition = (level.DetailCenter - viewCenter) * pixelsPerMeter;
            }

            var counter = Quaternion.Euler(0f, 0f, -yaw);

            _pin.gameObject.SetActive(MapPin.Has);

            if (MapPin.Has)
            {
                _pin.anchoredPosition = (MapPin.Position - viewCenter) * pixelsPerMeter;
                _pin.localRotation = counter;
                _pin.SetAsLastSibling();
            }

            _focus.gameObject.SetActive(_focusWorld.HasValue);

            if (_focusWorld.HasValue)
            {
                _focus.anchoredPosition = (_focusWorld.Value - viewCenter) * pixelsPerMeter;
                _focus.localRotation = counter;
                _focus.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f);
            }

            if (player != null)
            {
                Vector3 p = player.position;
                _player.anchoredPosition = (new Vector2(p.x, p.z) - viewCenter) * pixelsPerMeter;
                _player.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
                _player.SetAsLastSibling();
            }

            foreach (MapMarker marker in MapMarker.All)
            {
                Vector3 position = marker.transform.position;
                Vector2 offset = new Vector2(position.x, position.z) - viewCenter;
                bool visible = (includeEnemies || marker.Kind != MapMarkerKind.Enemy)
                    && (visibleRadius <= 0f || offset.sqrMagnitude <= visibleRadius * visibleRadius)
                    && MapIcons.IsVisibleAt(marker.Kind, viewWidth);

                if (!_iconByMarker.TryGetValue(marker, out RectTransform icon))
                {
                    if (!visible)
                    {
                        continue;
                    }

                    icon = MapIcons.Create(_icons, marker.Kind, _iconSize);
                    _iconByMarker.Add(marker, icon);
                }

                icon.gameObject.SetActive(visible);

                if (visible)
                {
                    icon.anchoredPosition = offset * pixelsPerMeter;
                    icon.localRotation = counter;
                }
            }

            _stale.Clear();

            foreach (KeyValuePair<MapMarker, RectTransform> pair in _iconByMarker)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                {
                    _stale.Add(pair.Key);
                }
            }

            foreach (MapMarker marker in _stale)
            {
                Object.Destroy(_iconByMarker[marker].gameObject);
                _iconByMarker.Remove(marker);
            }
        }

        /// <summary>쿼터뷰 카메라 방향 (도). 카메라가 없으면 0</summary>
        public static float CameraYaw()
        {
            Camera camera = Camera.main;
            return camera != null ? camera.transform.eulerAngles.y : 0f;
        }

        /// <summary>화면 방향 벡터 → 월드 (x, z) 방향 (지도 회전 yaw 되돌리기)</summary>
        public static Vector2 ScreenToWorldDirection(Vector2 screen, float yaw)
        {
            float r = -yaw * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(screen.x * c - screen.y * s, screen.x * s + screen.y * c);
        }
    }
}
