using UnityEngine;

namespace BrokenSovereign.Equipment
{
    /// <summary>
    /// 무기 정의. 기본 공격 간격(공격 속도 0% 기준)과 손에 드는 외형을 정합니다.
    /// 실제 간격 = 기본 공격 간격 ÷ (1 + 공격 속도) (IAttackIntervalSource 참고)
    /// 손잡이 위치·회전은 오른손 본 기준이며, 에디터 메뉴가 T포즈에서 계산해 넣습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "BrokenSovereign/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string _weaponId = "weapon";
        [SerializeField] private string _displayName = "무기";
        [SerializeField] private WeaponType _weaponType = WeaponType.OneHandBlunt;

        [Tooltip("기본 공격 간격(초). 한손 둔기 1.0, 양손 둔기·봉 1.3")]
        [Min(0.1f)]
        [SerializeField] private float _baseAttackInterval = 1f;

        [Header("외형")]
        [Tooltip("손에 드는 모델 프리팹 (원점 = 손잡이). 비우면 외형 없음")]
        [SerializeField] private GameObject _visualPrefab;

        [Tooltip("오른손 본 기준 위치")]
        [SerializeField] private Vector3 _gripPosition;

        [Tooltip("오른손 본 기준 회전 (오일러)")]
        [SerializeField] private Vector3 _gripEuler;

        public string WeaponId => _weaponId;
        public string DisplayName => _displayName;
        public WeaponType WeaponType => _weaponType;
        public float BaseAttackInterval => _baseAttackInterval;
        public GameObject VisualPrefab => _visualPrefab;
        public Vector3 GripPosition => _gripPosition;
        public Quaternion GripRotation => Quaternion.Euler(_gripEuler);

#if UNITY_EDITOR
        public void EditorSetup(string id, string displayName, WeaponType type, float interval)
        {
            _weaponId = id;
            _displayName = displayName;
            _weaponType = type;
            _baseAttackInterval = interval;
        }

        public void EditorSetVisual(GameObject prefab, Vector3 gripPosition, Vector3 gripEuler)
        {
            _visualPrefab = prefab;
            _gripPosition = gripPosition;
            _gripEuler = gripEuler;
        }
#endif
    }
}
