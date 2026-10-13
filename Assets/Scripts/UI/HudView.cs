using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 전투 HUD (GDD 10장): 가운데 기술 슬롯과 물약·회피, 그 양 옆에 체력 구슬(왼쪽)과 직업 자원 구슬(오른쪽),
    /// 선택한 대상의 상단 체력 게이지와 발밑 원 테두리.
    /// 시작 시 코드로 UI를 만들고, 매 프레임 플레이어 상태를 읽어 표시합니다 (UI는 읽기 전용).
    /// 배치·색·연출 값은 Play 중 Inspector에서 바꾸면 즉시 반영됩니다.
    /// 아트 확정 후 프리팹으로 옮길 예정입니다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        private const string ORB_SHADER_NAME = "BrokenSovereign/UI/ResourceOrb";

        private static readonly SkillSlot[] SKILL_SLOTS =
        {
            SkillSlot.LeftClick, SkillSlot.RightClick, SkillSlot.Key1, SkillSlot.Key2, SkillSlot.Key3, SkillSlot.Key4
        };

        private static readonly string[] SKILL_KEYS = { "LMB", "RMB", "1", "2", "3", "4" };

        [SerializeField] private PlayerController _player;
        [SerializeField] private Shader _orbShader;
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private OrbSettings _orb = new OrbSettings();
        [SerializeField] private SkillBarSettings _bar = new SkillBarSettings();
        [SerializeField] private TargetSettings _target = new TargetSettings();

        private Health _health;
        private IResourceGauge _resource;
        private readonly List<IResourceGauge> _gaugeBuffer = new List<IResourceGauge>();
        private PotionUser _potion;
        private DodgeAction _dodge;

        private CanvasScaler _scaler;
        private ResourceOrbView _healthOrb;
        private ResourceOrbView _resourceOrb;
        private RectTransform _barRoot;
        private SkillSlotView[] _skillSlots;
        private SkillSlotView _potionSlot;
        private SkillSlotView _dodgeSlot;
        private TargetFrameView _targetFrame;
        private TargetRingView _targetRing;
        private Health _shownTarget;
        private bool _isBuilt;

        private void Awake()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
            }

            if (_player == null)
            {
                Debug.LogError("HudView: PlayerController를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _health = _player.GetComponent<Health>();
            _resource = FindActiveResource();
            _potion = _player.GetComponent<PotionUser>();
            _dodge = _player.GetComponent<DodgeAction>();

            Build();
            ApplyLayout();
        }

        private void Update()
        {
            if (_health != null)
            {
                _healthOrb.SetValue(_health.Current, _health.Max);
            }

            // 전직 등으로 자원 컴포넌트가 바뀌면 새 자원을 읽고 색을 부드럽게 전환
            _resource = FindActiveResource();

            if (_resource != null)
            {
                _resourceOrb.SetLiquidColor(_resource.DisplayColor, _orb.ResourceColorBlendTime);
                _resourceOrb.SetValue(_resource.Current, _resource.Max);
            }
            else
            {
                _resourceOrb.SetLocked(true);
            }

            for (int i = 0; i < SKILL_SLOTS.Length; i++)
            {
                _skillSlots[i].SetState(_player.GetSlotState(SKILL_SLOTS[i]));
            }

            if (_potion != null)
            {
                _potionSlot.SetState(new SkillSlotState(true, _potion.CooldownRemaining, _potion.Cooldown));
                _potionSlot.SetCount(_potion.Charges);
            }

            if (_dodge != null)
            {
                _dodgeSlot.SetState(new SkillSlotState(true, _dodge.CooldownRemaining, _dodge.Cooldown));
            }

            UpdateTarget();
        }

        private void OnDestroy()
        {
            if (_targetRing != null)
            {
                Destroy(_targetRing.gameObject);
            }
        }

        // 플레이어가 선택한 대상이 바뀌면 원 테두리와 상단 게이지를 새 대상으로 전환
        private void UpdateTarget()
        {
            Health target = _player.CurrentTarget;

            if (target == _shownTarget)
            {
                return;
            }

            _shownTarget = target;
            _targetFrame.SetTarget(target);
            _targetRing.SetTarget(target);
        }

        // Play 중 Inspector 값 변경을 즉시 반영
        private void OnValidate()
        {
            if (_isBuilt)
            {
                ApplyLayout();
            }
        }

        private void Build()
        {
            Canvas canvas = GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            _scaler = GetComponent<CanvasScaler>();

            if (_scaler == null)
            {
                _scaler = gameObject.AddComponent<CanvasScaler>();
            }

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.matchWidthOrHeight = 0.5f;

            Shader shader = _orbShader != null ? _orbShader : Shader.Find(ORB_SHADER_NAME);

            if (shader == null)
            {
                Debug.LogError($"HudView: {ORB_SHADER_NAME} 셰이더를 찾지 못해 기본 UI 셰이더를 사용합니다.", this);
                shader = Shader.Find("UI/Default");
            }

            _healthOrb = CreateOrb("HealthOrb", shader);
            _resourceOrb = CreateOrb("ResourceOrb", shader);

            _barRoot = HudGraphics.CreateRect("SkillBar", transform);
            _skillSlots = new SkillSlotView[SKILL_SLOTS.Length];

            for (int i = 0; i < SKILL_SLOTS.Length; i++)
            {
                _skillSlots[i] = CreateSlot($"Slot_{SKILL_SLOTS[i]}", SKILL_KEYS[i]);
            }

            _potionSlot = CreateSlot("Slot_Potion", "Q");
            _dodgeSlot = CreateSlot("Slot_Dodge", "SPACE");

            _targetFrame = HudGraphics.CreateRect("TargetFrame", transform).gameObject.AddComponent<TargetFrameView>();
            _targetFrame.Initialize();

            // 원 테두리는 월드에 그리므로 Canvas 밖에 생성
            _targetRing = new GameObject("TargetRing", typeof(LineRenderer)).AddComponent<TargetRingView>();
            _targetRing.Initialize(_target);
            _isBuilt = true;
        }

        private void ApplyLayout()
        {
            _scaler.referenceResolution = _referenceResolution;

            _healthOrb.ApplyStyle(_orb, _orb.HealthColor, _orb.HealthSurfaceColor);
            Color resourceColor = _resource != null ? _resource.DisplayColor : Color.gray;
            _resourceOrb.ApplyStyle(_orb, resourceColor);

            // 기술 6칸 + 묶음 간격 + 물약·회피 2칸을 가운데 정렬
            float slot = _bar.SlotSize;
            float skillWidth = SKILL_SLOTS.Length * slot + (SKILL_SLOTS.Length - 1) * _bar.Spacing;
            float utilityWidth = 2f * slot + _bar.Spacing;
            float totalWidth = skillWidth + _bar.GroupSpacing + utilityWidth;

            PlaceCorner(_barRoot, new Vector2(0.5f, 0f), new Vector2(0f, _bar.BottomMargin), new Vector2(totalWidth, slot));
            _barRoot.pivot = new Vector2(0.5f, 0f);

            // 구슬은 기술 바 양 끝에 붙여 배치 (화면 아래 가운데 기준)
            Vector2 orbSize = new Vector2(_orb.Size, _orb.Size);
            float orbOffset = totalWidth * 0.5f + _orb.GapToBar;
            PlaceAt(_healthOrb.Rect, new Vector2(1f, 0f), new Vector2(-orbOffset, _orb.BottomMargin), orbSize);
            PlaceAt(_resourceOrb.Rect, new Vector2(0f, 0f), new Vector2(orbOffset, _orb.BottomMargin), orbSize);

            float x = 0f;

            for (int i = 0; i < _skillSlots.Length; i++)
            {
                PlaceSlot(_skillSlots[i], x);
                x += slot + _bar.Spacing;
            }

            x += _bar.GroupSpacing - _bar.Spacing;
            PlaceSlot(_potionSlot, x);
            x += slot + _bar.Spacing;
            PlaceSlot(_dodgeSlot, x);

            // 대상 체력 게이지는 화면 위 가운데
            PlaceCorner(_targetFrame.Rect, new Vector2(0.5f, 1f), new Vector2(0f, -_target.TopMargin), _target.BarSize);
            _targetFrame.ApplyStyle(_target);
            _targetRing.ApplyStyle(_target);
        }

        // 활성화된 자원 컴포넌트 중 첫 번째. 전직 시 이전 자원을 끄고 새 자원을 켜면 HUD가 따라감
        private IResourceGauge FindActiveResource()
        {
            _player.GetComponents(_gaugeBuffer);

            foreach (IResourceGauge gauge in _gaugeBuffer)
            {
                if (gauge is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                return gauge;
            }

            return null;
        }

        private void PlaceSlot(SkillSlotView view, float x)
        {
            RectTransform rect = view.Rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(_bar.SlotSize, _bar.SlotSize);
            rect.anchoredPosition = new Vector2(x, 0f);
            view.ApplyStyle(_bar);
        }

        // 화면 아래 가운데에 앵커를 두고, pivot 기준으로 위치 지정
        private static void PlaceAt(RectTransform rect, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void PlaceCorner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private ResourceOrbView CreateOrb(string orbName, Shader shader)
        {
            ResourceOrbView orb = HudGraphics.CreateRect(orbName, transform).gameObject.AddComponent<ResourceOrbView>();
            orb.Initialize(shader);
            return orb;
        }

        private SkillSlotView CreateSlot(string slotName, string key)
        {
            SkillSlotView view = HudGraphics.CreateRect(slotName, _barRoot).gameObject.AddComponent<SkillSlotView>();
            view.Initialize(key);
            return view;
        }
    }
}
