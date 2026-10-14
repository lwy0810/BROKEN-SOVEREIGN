using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 능력치 효과(디버프)가 걸려 있는 동안 대상의 몸을 반투명 보라색 머티리얼로 바꿔 표시하고, 끝나면 원래 머티리얼로 되돌립니다.
    /// 머티리얼: Resources/StatusEffects/DebuffTint (URP Lit 반투명). 없으면 실행 중 생성.
    /// StatusEffectHost가 붙을 때 자동으로 붙습니다. 이펙트 에셋 적용 시 셰이더 효과로 교체합니다.
    /// </summary>
    [RequireComponent(typeof(StatusEffectHost))]
    public class StatusEffectIndicator : MonoBehaviour
    {
        private const string TINT_RESOURCE = "StatusEffects/DebuffTint";

        private static Material _tintMaterial;

        private StatusEffectHost _host;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material[]> _originalMaterials = new List<Material[]>();
        private bool _isTinted;

        private void Awake()
        {
            _host = GetComponent<StatusEffectHost>();
        }

        private void LateUpdate()
        {
            bool active = _host.Count > 0;

            if (active && !_isTinted)
            {
                ApplyTint();
            }
            else if (!active && _isTinted)
            {
                RestoreMaterials();
            }
        }

        private void OnDisable()
        {
            if (_isTinted)
            {
                RestoreMaterials();
            }
        }

        private void ApplyTint()
        {
            Material tint = GetTintMaterial();

            if (tint == null)
            {
                return;
            }

            _renderers.Clear();
            _originalMaterials.Clear();

            // 몸체 메시만 (선·파티클 등 표시용 렌더러 제외)
            foreach (Renderer bodyRenderer in GetComponentsInChildren<Renderer>())
            {
                if (!(bodyRenderer is MeshRenderer) && !(bodyRenderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                Material[] originals = bodyRenderer.sharedMaterials;
                var tinted = new Material[originals.Length];

                for (int i = 0; i < tinted.Length; i++)
                {
                    tinted[i] = tint;
                }

                _renderers.Add(bodyRenderer);
                _originalMaterials.Add(originals);
                bodyRenderer.sharedMaterials = tinted;
            }

            _isTinted = true;
        }

        private void RestoreMaterials()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].sharedMaterials = _originalMaterials[i];
                }
            }

            _renderers.Clear();
            _originalMaterials.Clear();
            _isTinted = false;
        }

        // 모든 대상이 같은 머티리얼을 공유 (대상마다 머티리얼 생성 방지)
        private static Material GetTintMaterial()
        {
            if (_tintMaterial != null)
            {
                return _tintMaterial;
            }

            _tintMaterial = Resources.Load<Material>(TINT_RESOURCE);

            if (_tintMaterial == null)
            {
                Debug.LogWarning($"StatusEffectIndicator: Resources/{TINT_RESOURCE} 머티리얼이 없어 단색 반투명 머티리얼을 만듭니다.");
                Shader shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    _tintMaterial = new Material(shader) { color = new Color(0.65f, 0.35f, 1f, 0.75f) };
                }
            }

            return _tintMaterial;
        }
    }
}
