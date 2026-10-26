using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 바닥에 놓여 이름표로 표시되고 클릭(또는 가까이 가면 자동)으로 상호작용하는 대상 (10/26).
    /// 바닥 아이템(GroundItem) · 테스트 상자(LootChest)가 상속. 이름표는 GroundItemLabelView, 걸어가서 줍기는 ItemPicker.
    /// </summary>
    public abstract class WorldInteractable : MonoBehaviour
    {
        private static readonly List<WorldInteractable> _all = new List<WorldInteractable>();

        public static IReadOnlyList<WorldInteractable> All => _all;

        /// <summary>이름표 글자</summary>
        public abstract string LabelText { get; }

        /// <summary>이름표 색 (희귀도 등)</summary>
        public abstract Color LabelColor { get; }

        /// <summary>이 거리 안에 오면 상호작용 (m)</summary>
        public virtual float InteractRange => 1.4f;

        /// <summary>이름표 높이 (바닥 기준, m)</summary>
        public virtual float LabelHeight => 0.6f;

        /// <summary>이름표를 표시할지 (떨어지는 중 등은 숨김)</summary>
        public virtual bool ShowLabel => true;

        /// <summary>상호작용 (줍기·열기). 성공하면 true</summary>
        public abstract bool Interact(GameObject player);

        protected virtual void OnEnable()
        {
            _all.Add(this);
        }

        protected virtual void OnDisable()
        {
            _all.Remove(this);
        }

        // ------------------------------------------------------------------
        // 공용: 단색 재질 (URP Unlit, 없으면 기본)
        // ------------------------------------------------------------------

        private static readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        protected static Material ColorMaterial(Color color)
        {
            if (_materials.TryGetValue(color, out Material material) && material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "WorldItem", color = color };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            _materials[color] = material;
            return material;
        }

        /// <summary>충돌체 없는 기본 도형 (표시 전용)</summary>
        protected static GameObject Shape(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Color color)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            Object.Destroy(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = localPosition;
            shape.transform.localScale = scale;
            Renderer renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = ColorMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return shape;
        }

        private static Transform _player;

        protected static Transform Player
        {
            get
            {
                if (_player == null)
                {
                    GameObject go = GameObject.FindWithTag("Player");
                    _player = go != null ? go.transform : null;
                }

                return _player;
            }
        }
    }
}
