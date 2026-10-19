using UnityEngine;

namespace BrokenSovereign.Map
{
    /// <summary>
    /// 맵 한 장의 지도 그림과 월드 범위. 맵 생성 도구(LevelTools)가 레벨 루트에 붙이고 그림을 연결합니다.
    /// 지도 그림 좌표: u = 월드 x, v = 월드 z (왼쪽 아래 = WorldMin).
    /// 세부 그림(Detail): 마을처럼 확대해서 보는 곳만 더 높은 해상도로 따로 그려 전체 그림 위에 겹칩니다.
    /// 마을 범위(TownCenter·TownRadius): 지도를 열 때 첫 확대 단계를 고르는 데 사용합니다.
    /// </summary>
    public class LevelMap : MonoBehaviour
    {
        [SerializeField] private Texture2D _texture;
        [SerializeField] private Vector2 _worldMin = new Vector2(-500f, -500f);
        [SerializeField] private float _worldSize = 1000f;

        [Header("세부 그림 (마을)")]
        [SerializeField] private Texture2D _detailTexture;
        [SerializeField] private Vector2 _detailMin;
        [SerializeField] private float _detailSize;

        [Header("마을")]
        [SerializeField] private Vector2 _townCenter;
        [SerializeField] private float _townRadius;

        /// <summary>현재 씬의 지도 (없으면 null → 미니맵 숨김)</summary>
        public static LevelMap Current { get; private set; }

        public Texture2D Texture => _texture;
        public Vector2 WorldMin => _worldMin;
        public float WorldSize => _worldSize;
        public Vector2 WorldCenter => _worldMin + Vector2.one * (_worldSize * 0.5f);

        public Texture2D DetailTexture => _detailTexture;
        public Vector2 DetailCenter => _detailMin + Vector2.one * (_detailSize * 0.5f);
        public float DetailSize => _detailSize;

        public void Setup(Texture2D texture, Vector2 worldMin, float worldSize)
        {
            _texture = texture;
            _worldMin = worldMin;
            _worldSize = worldSize;
        }

        public void SetupDetail(Texture2D texture, Vector2 min, float size)
        {
            _detailTexture = texture;
            _detailMin = min;
            _detailSize = size;
        }

        public void SetupTown(Vector2 center, float radius)
        {
            _townCenter = center;
            _townRadius = radius;
        }

        /// <summary>월드 위치(x, z)가 마을 안인지</summary>
        public bool IsInTown(Vector2 position)
        {
            return _townRadius > 0f && (position - _townCenter).sqrMagnitude <= _townRadius * _townRadius;
        }

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
