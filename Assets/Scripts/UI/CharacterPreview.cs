using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 캐릭터 창 가운데 모델 미리보기 (장비 착용 모습 확인용, 10/21).
    /// 창을 열 때 플레이어 모델(PlayerAnimator의 Animator 오브젝트)을 화면 밖 무대에 복제해
    /// 전용 레이어만 찍는 카메라로 RenderTexture에 그리고 RawImage로 표시합니다.
    /// 복제본은 스크립트·충돌체를 떼어 내 게임에 영향이 없고, 닫으면 지웁니다. 끌어서 좌우 회전.
    /// 장비 시스템이 생기면 Open이 그때의 모델을 복제하므로 착용 모습이 그대로 보입니다.
    /// 10/25: Resources/Animations/PaladinPreviewIdle(성기사로 Mixamo에서 받은 무기 내린 대기 동작)이 있으면 그 동작을 반복 재생,
    /// 없으면 예전처럼 이동 블렌드의 대기 자세를 골라 멈춘 모습.
    /// </summary>
    public sealed class CharacterPreview
    {
        private const int LAYER = 31;
        private const float ROTATE_SPEED = 0.4f;
        // 대기 모션이 자리 잡을 때까지 재생한 뒤 멈추는 시간(초). 첫 프레임은 자세가 덜 잡혀 무기를 든 모양이 됨
        private const float FREEZE_DELAY = 0.25f;
        private const int POSE_SAMPLES = 40;
        private static readonly int LOCOMOTION_ID = Animator.StringToHash("Locomotion");
        private static readonly Vector3 STAGE = new Vector3(0f, -2000f, 0f);
        private static int _stageCount;
        private const string PREVIEW_IDLE = "Animations/PaladinPreviewIdle";
        private static AnimationClip _previewIdle;
        private static bool _previewIdleLoaded;
        private PlayableGraph _graph;
        private bool _live;   // 대기 동작 반복 재생 중 (멈추지 않음)

        private readonly RawImage _image;
        private readonly Vector3 _stagePosition;
        private readonly RenderTexture _texture;
        private Camera _camera;
        private Light _light;
        private Transform _pivot;
        private GameObject _clone;
        private bool _dragging;
        private Animator _poseAnimator;
        private float _freezeTime;
        private int _reframeFrames;
        private float _lastX;

        public RectTransform Rect => _image.rectTransform;

        /// <summary>미리보기마다 무대를 따로 두어(캐릭터 창·인벤토리) 서로의 복제본이 비치지 않게 함</summary>
        public CharacterPreview(Transform parent, Vector2 size)
        {
            _stagePosition = STAGE + new Vector3(60f * _stageCount++, 0f, 0f);
            _image = HudGraphics.CreateRect("ModelPreview", parent).gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _texture = new RenderTexture(Mathf.RoundToInt(size.x * 2f), Mathf.RoundToInt(size.y * 2f), 24, RenderTextureFormat.ARGB32)
            {
                name = "CharacterPreviewRT",
                antiAliasing = 4
            };
            _image.texture = _texture;
        }

        public void Open(GameObject player)
        {
            Close();
            EnsureStage();
            PlayerAnimator animator = player != null ? player.GetComponent<PlayerAnimator>() : null;
            GameObject model = animator != null && animator.Animator != null ? animator.Animator.gameObject : null;

            if (model == null)
            {
                _image.enabled = false;
                return;
            }

            // 비활성 부모 아래에 복제해 스크립트 Awake가 돌지 않게 한 뒤 정리
            GameObject holder = new GameObject("PreviewHolder");
            holder.SetActive(false);
            _clone = Object.Instantiate(model, holder.transform);
            Strip(_clone);
            WarnSharedBones(_clone);
            _clone.transform.SetParent(_pivot, false);
            _clone.transform.localPosition = Vector3.zero;
            _clone.transform.localRotation = Quaternion.identity;
            _clone.transform.localScale = model.transform.lossyScale;
            Object.Destroy(holder);
            SetLayer(_clone.transform);
            _clone.SetActive(true);

            AnimationClip idle = PreviewIdle();
            _live = idle != null;
            _poseAnimator = _live ? PlayLoop(_clone, animator.Animator, idle, out _graph) : StandIdle(_clone, animator.Animator);
            _freezeTime = Time.unscaledTime + FREEZE_DELAY;
            _image.color = new Color(1f, 1f, 1f, 0f);

            _pivot.rotation = Quaternion.Euler(0f, 180f, 0f);
            Frame();
            _camera.enabled = true;
            _light.enabled = true;
            _image.enabled = true;
        }

        // 고정 자세: 이동 블렌드(Locomotion)를 속도 0(= 대기 모션, 무기를 내린 자세)으로 잠깐 재생한 뒤
        // Tick에서 Animator를 꺼 멈춤. 복제본 전용 Animator라 필드 캐릭터 애니메이션과 독립
        private static Animator StandIdle(GameObject clone, Animator source)
        {
            Animator animator = clone.GetComponent<Animator>();

            if (animator == null)
            {
                animator = clone.AddComponent<Animator>();
                animator.avatar = source.avatar;
            }

            animator.runtimeAnimatorController = source.runtimeAnimatorController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.Rebind();
            SetIfExists(animator, "Speed", AnimatorControllerParameterType.Float);
            SetIfExists(animator, "Moving", AnimatorControllerParameterType.Bool);

            if (animator.HasState(0, LOCOMOTION_ID))
            {
                animator.Play(LOCOMOTION_ID, 0, 0f);
            }

            animator.Update(0f);
            return animator;
        }

        private static AnimationClip PreviewIdle()
        {
            if (!_previewIdleLoaded)
            {
                _previewIdleLoaded = true;
                _previewIdle = Resources.Load<AnimationClip>(PREVIEW_IDLE);
            }

            return _previewIdle;
        }

        // 대기 동작 반복 재생: 복제본 Animator에 전용 PlayableGraph (필드 캐릭터 Animator와 독립, 시간 정지 무관)
        private static Animator PlayLoop(GameObject clone, Animator source, AnimationClip clip, out PlayableGraph graph)
        {
            Animator animator = clone.GetComponent<Animator>();

            if (animator == null)
            {
                animator = clone.AddComponent<Animator>();
                animator.avatar = source.avatar;
            }

            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.Rebind();

            graph = PlayableGraph.Create("CharacterPreviewIdle");
            graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            var playable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput.Create(graph, "Preview", animator).SetSourcePlayable(playable);
            graph.Play();
            graph.Evaluate(0f);
            return animator;
        }

        // 대기 자세가 잡히면 (멈춤 모드면 Animator를 꺼 고정하고) 카메라 쪽으로 돌려 보여 줌
        private void FreezeWhenReady()
        {
            if (_poseAnimator == null || !_poseAnimator.enabled || _freezeTime <= 0f || Time.unscaledTime < _freezeTime)
            {
                return;
            }

            _freezeTime = 0f;

            if (!_live)
            {
                PickLoweredPose(_poseAnimator);
                _poseAnimator.enabled = false;
            }

            FaceCamera(_poseAnimator);
            _reframeFrames = 2;
            _image.color = Color.white;
        }

        // 대기 자세는 몸이 비스듬히 서 있으므로 어깨·골반이 향한 방향을 카메라 쪽으로 돌리고,
        // 골반을 회전축 위에 두어 화면 가운데 서고 끌어 돌려도 제자리에서 돌게 함
        private void FaceCamera(Animator animator)
        {
            if (!animator.isHuman)
            {
                return;
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 right = Across(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm)
                + Across(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg);
            right.y = 0f;

            if (right.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = Vector3.Cross(right.normalized, Vector3.up);
                Vector3 toCamera = _camera.transform.position - _pivot.position;
                toCamera.y = 0f;
                float angle = Vector3.SignedAngle(forward, toCamera, Vector3.up);
                _pivot.Rotate(0f, angle, 0f, Space.World);
            }

            if (hips != null)
            {
                Vector3 offset = hips.position - _pivot.position;
                offset.y = 0f;
                _clone.transform.position -= offset;
            }
        }

        private static Vector3 Across(Animator animator, HumanBodyBones left, HumanBodyBones right)
        {
            Transform l = animator.GetBoneTransform(left);
            Transform r = animator.GetBoneTransform(right);
            return l != null && r != null ? r.position - l.position : Vector3.zero;
        }

        // 대기 모션 한 바퀴를 훑어 두 손이 가장 낮은 순간(= 무기를 내린 자세)을 골라 그 자세로 고정
        private static void PickLoweredPose(Animator animator)
        {
            if (!animator.isHuman || !animator.HasState(0, LOCOMOTION_ID))
            {
                return;
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (hips == null || right == null || left == null)
            {
                return;
            }

            float bestTime = 0f;
            float bestHeight = float.MaxValue;

            for (int i = 0; i < POSE_SAMPLES; i++)
            {
                float time = (float)i / POSE_SAMPLES;
                animator.Play(LOCOMOTION_ID, 0, time);
                animator.Update(0f);
                float height = Mathf.Max(right.position.y, left.position.y) - hips.position.y;

                if (height < bestHeight)
                {
                    bestHeight = height;
                    bestTime = time;
                }
            }

            animator.Play(LOCOMOTION_ID, 0, bestTime);
            animator.Update(0f);
        }

        private static void SetIfExists(Animator animator, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name != name || parameter.type != type)
                {
                    continue;
                }

                if (type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat(name, 0f);
                }
                else
                {
                    animator.SetBool(name, false);
                }
            }
        }

        public void Close()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_clone != null)
            {
                Object.Destroy(_clone);
                _clone = null;
            }

            if (_camera != null)
            {
                _camera.enabled = false;
                _light.enabled = false;
            }

            _dragging = false;
        }

        /// <summary>창이 열린 동안 매 프레임: 미리보기를 끌면 회전</summary>
        public void Tick()
        {
            FreezeWhenReady();

            // 고정 직후 몇 프레임: 바뀐 자세의 경계로 다시 맞춤 (스킨 경계는 그려진 다음 갱신됨)
            if (_reframeFrames > 0 && _clone != null)
            {
                _reframeFrames--;
                Frame();
            }
            Mouse mouse = Mouse.current;

            if (mouse == null || _pivot == null)
            {
                return;
            }

            float x = mouse.position.ReadValue().x;

            if (mouse.leftButton.wasPressedThisFrame && UiKit.IsHover(Rect))
            {
                _dragging = true;
                _lastX = x;
            }
            else if (!mouse.leftButton.isPressed)
            {
                _dragging = false;
            }

            if (_dragging)
            {
                _pivot.Rotate(0f, -(x - _lastX) * ROTATE_SPEED, 0f, Space.World);
                _lastX = x;
            }
        }

        public void Release()
        {
            Close();

            if (_texture != null)
            {
                _texture.Release();
            }

            if (_pivot != null)
            {
                Object.Destroy(_pivot.parent.gameObject);
            }
        }

        private void EnsureStage()
        {
            if (_pivot != null)
            {
                return;
            }

            GameObject stage = new GameObject("CharacterPreviewStage");
            stage.transform.position = _stagePosition;
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(stage.transform, false);

            _camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            _camera.transform.SetParent(stage.transform, false);
            _camera.cullingMask = 1 << LAYER;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = UiTheme.P.Inset;
            _camera.fieldOfView = 24f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
            _camera.targetTexture = _texture;
            _camera.enabled = false;

            _light = new GameObject("PreviewLight").AddComponent<Light>();
            _light.transform.SetParent(stage.transform, false);
            _light.type = LightType.Directional;
            _light.intensity = 1.3f;
            _light.color = new Color(1f, 0.95f, 0.86f);
            _light.cullingMask = 1 << LAYER;
            _light.shadows = LightShadows.None;
            _light.transform.rotation = Quaternion.Euler(30f, 150f, 0f);
            _light.enabled = false;

            // 다른 카메라가 이 레이어를 그리지 않도록
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera != _camera)
                {
                    camera.cullingMask &= ~(1 << LAYER);
                }
            }
        }

        // 모델 경계에 맞춰 카메라 거리·높이
        private void Frame()
        {
            Bounds bounds = new Bounds(_pivot.position, Vector3.one);
            bool first = true;

            foreach (Renderer renderer in _clone.GetComponentsInChildren<Renderer>())
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            float height = Mathf.Max(bounds.size.y, 1f);
            float aspect = (float)_texture.width / _texture.height;
            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            // 회전축(골반) 기준으로 좌우 대칭이 되게 가장 먼 쪽까지의 거리 ×2를 폭으로
            float halfWidth = Mathf.Max(Mathf.Abs(bounds.max.x - _pivot.position.x), Mathf.Abs(bounds.min.x - _pivot.position.x));
            float fitHeight = Mathf.Max(height, halfWidth * 2f / aspect) * 1.15f;
            float distance = fitHeight * 0.5f / Mathf.Tan(halfFov);
            Vector3 center = new Vector3(_pivot.position.x, bounds.center.y, _pivot.position.z);
            _camera.transform.position = center + new Vector3(0f, height * 0.04f, -distance);
            _camera.transform.LookAt(center);
        }

        // 표시에 필요 없는 것 제거 (스크립트·충돌체·물리·길찾기·소리)
        private static void Strip(GameObject root)
        {
            // 뒤에 붙은 것이 앞의 것에 의존하는 경우가 많아 역순으로 제거
            Component[] components = root.GetComponentsInChildren<Component>(true);

            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];

                if (component is MonoBehaviour || component is Collider || component is Rigidbody ||
                    component is UnityEngine.AI.NavMeshAgent || component is AudioSource)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        // 뼈대가 복제본 밖(원본)을 가리키면 필드 캐릭터 움직임을 그대로 따라가므로 확인
        private static void WarnSharedBones(GameObject clone)
        {
            foreach (SkinnedMeshRenderer skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.rootBone != null && !skin.rootBone.IsChildOf(clone.transform))
                {
                    Debug.LogWarning($"[CharacterPreview] {skin.name}의 뼈대가 복제본 밖을 가리킵니다. 미리보기가 필드 캐릭터를 따라 움직일 수 있습니다.");
                }
            }
        }

        private static void SetLayer(Transform root)
        {
            root.gameObject.layer = LAYER;

            foreach (Transform child in root)
            {
                SetLayer(child);
            }
        }
    }
}
