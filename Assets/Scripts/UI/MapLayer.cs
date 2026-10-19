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
            _detail = HudGraphics.CreateRect("Detail", _content).gameObject.AddComponent<RawImage>();
            _detail.raycastTarget = false;
            _icons = HudGraphics.CreateRect("Icons", _content);
            _icons.sizeDelta = Vector2.zero;
            _player = MapIcons.CreatePlayer(_content, iconSize * 0.95f);
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
