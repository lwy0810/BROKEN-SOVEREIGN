using System.Collections.Generic;
using BrokenSovereign.Ascension;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using BrokenSovereign.Player;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 전투 HUD (GDD 10장): 가운데 기술 슬롯과 물약·회피, 그 양 옆에 체력 구슬(왼쪽)과 직업 자원 구슬(오른쪽),
    /// 선택한 대상의 상단 체력 게이지와 발밑 원 테두리, 직업 이름·전직 알림·슬롯 기술 이름.
    /// 시작 시 코드로 UI를 만들고, 매 프레임 플레이어 상태를 읽어 표시합니다 (UI는 읽기 전용).
    /// 배치·색·연출 값은 Play 중 Inspector에서 바꾸면 즉시 반영됩니다.
    /// 아트 확정 후 프리팹으로 옮길 예정입니다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        private const string ORB_SHADER_NAME = "BrokenSovereign/UI/ResourceOrb";

        private static readonly SkillSlot[] SKILL_SLOTS =
        {
            SkillSlot.LeftClick, SkillSlot.RightClick, SkillSlot.Key1, SkillSlot.Key2, SkillSlot.Key3, SkillSlot.Key4,
            SkillSlot.Key5, SkillSlot.Key6
        };

        private static readonly string[] SKILL_KEYS = { "LMB", "RMB", "1", "2", "3", "4", "5", "6" };

        [SerializeField] private PlayerController _player;
        [SerializeField] private Shader _orbShader;
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private OrbSettings _orb = new OrbSettings();
        [SerializeField] private SkillBarSettings _bar = new SkillBarSettings();
        [SerializeField] private TargetSettings _target = new TargetSettings();
        [SerializeField] private JobDisplaySettings _jobDisplay = new JobDisplaySettings();
        [SerializeField] private JobGaugeSettings _jobGauge = new JobGaugeSettings();

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
        private PlayerJob _job;
        private SkillCaster _caster;
        private Text _jobLabel;
        private JobGaugeView _jobGaugeView;
        private readonly List<IJobGauge> _jobGaugeBuffer = new List<IJobGauge>();
        private Text _noticeText;
        private Text _warningText;
        private const string SKILL_NOT_READY_MESSAGE = "아직 사용할 수 없습니다";
        private const string NOT_ENOUGH_MANA_MESSAGE = "마나가 부족합니다.";
        private float _warningEndTime = float.NegativeInfinity;
        private float _warningStartTime = float.NegativeInfinity;
        private Text _buffText;
        private readonly System.Text.StringBuilder _buffBuilder = new System.Text.StringBuilder();
        private float _noticeEndTime = float.NegativeInfinity;
        private bool _hasShownJob;
        private bool _isBuilt;
        private PlayerProgression _progression;
        private JobSelectView _jobSelect;
        private HeroPathSelectView _heroSelect;
        private HeroPathController _hero;
        private RectTransform _expRoot;
        private Image _expFill;
        private string _jobName = string.Empty;
        private int _shownLevel = -1;
        private AscensionTier _shownTier;

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
            _job = _player.GetComponent<PlayerJob>();
            _caster = _player.GetComponent<SkillCaster>();
            _progression = _player.GetComponent<PlayerProgression>();
            _hero = _player.GetComponent<HeroPathController>();

            Build();
            ApplyLayout();

            _player.SkillNotReady += OnSkillNotReady;

            if (_caster != null)
            {
                _caster.CastFailed += OnSkillCastFailed;
            }

            // PlayerJob.Start의 첫 직업 적용보다 먼저 구독
            if (_job != null)
            {
                _job.JobChanged += OnJobChanged;

                if (_progression != null)
                {
                    _job.Advanced += OnJobAdvanced;
                }
            }

            if (_progression != null)
            {
                _progression.LeveledUp += OnLeveledUp;
                _progression.Ascended += OnAscended;
                _progression.JobSelectionRequested += OnJobSelectionRequested;
                _progression.HeroPathSelectionRequested += OnHeroPathSelectionRequested;
            }
            else
            {
                Debug.LogWarning("HudView: Player에 PlayerProgression이 없어 경험치·레벨·직업 선택을 표시하지 않습니다.", this);
            }
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
            UpdateSkillNames();
            UpdateNotice();
            UpdateWarning();
            _jobGaugeView.SetGauge(FindActiveJobGauge());
            UpdateBuffs();
            UpdateProgression();
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.SkillNotReady -= OnSkillNotReady;
            }

            if (_caster != null)
            {
                _caster.CastFailed -= OnSkillCastFailed;
            }

            if (_job != null)
            {
                _job.JobChanged -= OnJobChanged;
                _job.Advanced -= OnJobAdvanced;
            }

            if (_progression != null)
            {
                _progression.LeveledUp -= OnLeveledUp;
                _progression.Ascended -= OnAscended;
                _progression.JobSelectionRequested -= OnJobSelectionRequested;
                _progression.HeroPathSelectionRequested -= OnHeroPathSelectionRequested;
            }

            if (_targetRing != null)
            {
                Destroy(_targetRing.gameObject);
            }
        }

        // 직업 이름 갱신. 시작 시 첫 적용은 알림 없이, 이후 전환부터 화면 중앙 알림
        private void OnJobChanged(JobDefinition job)
        {
            _jobName = job.DisplayName;
            RefreshJobLabel();

            if (_hasShownJob)
            {
                ShowNotice(job.DisplayName);
            }

            _hasShownJob = true;
        }

        private void OnLeveledUp(int level)
        {
            ShowNotice($"레벨 {level}");
        }

        private void OnAscended(AscensionTier tier)
        {
            ShowNotice($"격 돌파 — {AscensionModel.DisplayName(tier)}");
        }

        // 격 돌파 알림에 전직을 이어 붙임 (JobChanged 알림이 돌파 알림을 덮어쓰지 않도록)
        private void OnJobAdvanced(JobDefinition job)
        {
            ShowNotice($"격 돌파 — {AscensionModel.DisplayName(_progression.Tier)} · {job.DisplayName} 전직");
        }

        private void OnJobSelectionRequested()
        {
            _jobSelect.Open();
        }

        private void OnHeroPathSelectionRequested()
        {
            if (_heroSelect != null && _job != null && _job.Current != null)
            {
                _heroSelect.Open(_job.Current.ResolveHeroPaths());
            }
        }

        private void OnHeroPathChosen(HeroPathDefinition path)
        {
            ShowNotice($"영웅의 길 — {path.DisplayName} ({path.Title})");
        }

        private void ShowNotice(string message)
        {
            _noticeText.text = message;
            _noticeEndTime = Time.time + _jobDisplay.NoticeDuration;
        }

        // 경험치 막대와 "직업 · 격 · 레벨" 표시 (값이 바뀔 때만 문자열 갱신)
        private void UpdateProgression()
        {
            if (_progression == null)
            {
                _expRoot.gameObject.SetActive(false);
                return;
            }

            _expFill.fillAmount = _progression.Progress;

            if (_progression.Level != _shownLevel || _progression.Tier != _shownTier)
            {
                RefreshJobLabel();
            }
        }

        private void RefreshJobLabel()
        {
            if (_progression == null)
            {
                _jobLabel.text = _jobName;
                return;
            }

            _shownLevel = _progression.Level;
            _shownTier = _progression.Tier;
            _jobLabel.text = $"{_jobName} · {AscensionModel.DisplayName(_shownTier)} · Lv {_shownLevel}";
        }

        /// <summary>화면 상단 중앙 경고 메시지 (기술 재사용 대기 등)</summary>
        public void ShowWarning(string message)
        {
            // 같은 메시지가 이미 보이는 중이면 다시 띄우지 않음 (연타해도 1회만 표시)
            if (Time.time < _warningEndTime && _warningText.text == message)
            {
                return;
            }

            _warningStartTime = Time.time;
            _warningText.text = message;
            _warningEndTime = Time.time + _jobDisplay.WarningDuration;
        }

        private void OnSkillNotReady(SkillSlot slot, SkillDefinition skill)
        {
            ShowWarning(SKILL_NOT_READY_MESSAGE);
        }

        // 마나 부족: 누르고 있는 동안 반복 시도해도 메시지가 보이는 동안에는 다시 띄우지 않음 (ShowWarning)
        private void OnSkillCastFailed(SkillSlot slot, SkillDefinition skill, SkillCastFailReason reason)
        {
            if (reason == SkillCastFailReason.NotEnoughResource)
            {
                ShowWarning(NOT_ENOUGH_MANA_MESSAGE);
            }
        }

        private void UpdateWarning()
        {
            float remaining = _warningEndTime - Time.time;
            bool visible = remaining > 0f;
            _warningText.enabled = visible;

            if (!visible)
            {
                return;
            }

            // 페이드 인 → 유지 → 페이드 아웃
            float fadeIn = _jobDisplay.WarningFadeInTime;
            float fadeOut = _jobDisplay.WarningFadeTime;
            float alphaIn = fadeIn > 0f ? Mathf.Clamp01((Time.time - _warningStartTime) / fadeIn) : 1f;
            float alphaOut = fadeOut > 0f ? Mathf.Clamp01(remaining / fadeOut) : 1f;

            Color color = _jobDisplay.WarningColor;
            color.a *= Mathf.Min(alphaIn, alphaOut);
            _warningText.color = color;
        }

        private void UpdateNotice()
        {
            float remaining = _noticeEndTime - Time.time;
            bool visible = remaining > 0f;
            _noticeText.enabled = visible;

            if (!visible)
            {
                return;
            }

            Color color = _jobDisplay.NoticeColor;
            float fade = _jobDisplay.NoticeFadeTime;
            color.a *= fade > 0f ? Mathf.Clamp01(remaining / fade) : 1f;
            _noticeText.color = color;
        }

        // 지속 중인 기술 버프 (불굴의 갑주 등)와 남은 시간
        private void UpdateBuffs()
        {
            _buffBuilder.Clear();

            if (_caster != null)
            {
                foreach (SkillContext context in _caster.ActiveCasts)
                {
                    if (context.Duration <= 0f)
                    {
                        continue;
                    }

                    if (_buffBuilder.Length > 0)
                    {
                        _buffBuilder.Append("   ");
                    }

                    _buffBuilder.Append(context.Definition.DisplayName).Append(' ')
                        .Append(context.Remaining.ToString("0.0")).Append('초');
                }
            }

            _buffText.text = _buffBuilder.ToString();
        }

        // 슬롯별 장착 기술 이름 (좌클릭은 기술이 없으면 기본 공격)
        private void UpdateSkillNames()
        {
            for (int i = 0; i < SKILL_SLOTS.Length; i++)
            {
                SkillDefinition skill = _caster != null ? _caster.GetSkill(SKILL_SLOTS[i]) : null;
                string skillName = skill != null ? skill.DisplayName
                    : SKILL_SLOTS[i] == SkillSlot.LeftClick ? "기본 공격" : string.Empty;
                _skillSlots[i].SetSkillName(skillName);
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

            _jobGaugeView = HudGraphics.CreateRect("JobGauge", transform).gameObject.AddComponent<JobGaugeView>();
            _jobGaugeView.Initialize();

            _jobLabel = CreateLabel("JobLabel", TextAnchor.LowerCenter);
            _jobLabel.text = string.Empty;
            _buffText = CreateLabel("BuffList", TextAnchor.LowerCenter);
            _buffText.text = string.Empty;
            _noticeText = CreateLabel("JobNotice", TextAnchor.MiddleCenter);
            _noticeText.fontStyle = FontStyle.Bold;
            _noticeText.enabled = false;
            _warningText = CreateLabel("Warning", TextAnchor.MiddleCenter);
            _warningText.fontStyle = FontStyle.Bold;
            _warningText.enabled = false;

            // 경험치 막대: 기술 바 바로 아래
            _expRoot = HudGraphics.CreateRect("ExpBar", transform);
            Image expBackground = _expRoot.gameObject.AddComponent<Image>();
            expBackground.sprite = HudGraphics.WhiteSprite;
            expBackground.color = new Color(0f, 0f, 0f, 0.7f);
            expBackground.raycastTarget = false;
            RectTransform fillRect = HudGraphics.CreateRect("Fill", _expRoot);
            HudGraphics.Stretch(fillRect);
            _expFill = fillRect.gameObject.AddComponent<Image>();
            _expFill.sprite = HudGraphics.WhiteSprite;
            _expFill.color = new Color(0.95f, 0.78f, 0.3f);
            _expFill.type = Image.Type.Filled;
            _expFill.fillMethod = Image.FillMethod.Horizontal;
            _expFill.fillAmount = 0f;
            _expFill.raycastTarget = false;

            // 직업 선택 화면: 가장 위에 그리도록 마지막에 생성
            _jobSelect = HudGraphics.CreateRect("JobSelect", transform).gameObject.AddComponent<JobSelectView>();
            _jobSelect.Initialize(_job);

            // 영웅의 길 선택 화면 (반신 돌파). HeroPathController가 없으면 만들지 않음
            if (_hero != null)
            {
                _heroSelect = HudGraphics.CreateRect("HeroPathSelect", transform).gameObject.AddComponent<HeroPathSelectView>();
                _heroSelect.Initialize(_hero);
                _heroSelect.PathChosen += OnHeroPathChosen;
            }
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

            // 경험치 막대: 기술 바 아래 (기술 바 너비)
            PlaceAt(_expRoot, new Vector2(0.5f, 0f), new Vector2(0f, Mathf.Max(2f, _bar.BottomMargin - 14f)),
                new Vector2(totalWidth, 8f));

            // 직업 게이지: 기술 바 바로 위 (기술 바 너비)
            float gaugeBottom = _bar.BottomMargin + slot + _jobGauge.GapToBar;
            PlaceAt(_jobGaugeView.Rect, new Vector2(0.5f, 0f), new Vector2(0f, gaugeBottom),
                new Vector2(totalWidth, _jobGauge.Height));
            _jobGaugeView.ApplyStyle(_jobGauge);

            // 직업 이름: 게이지 위 가운데 (게이지가 없어도 같은 자리)
            _jobLabel.fontSize = _jobDisplay.LabelFontSize;
            _jobLabel.color = _jobDisplay.LabelColor;
            PlaceAt(_jobLabel.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0f, gaugeBottom + _jobGauge.Height + _jobDisplay.LabelGapToBar),
                new Vector2(totalWidth, _jobDisplay.LabelFontSize + 8f));

            // 지속 버프 목록: 직업 이름 위
            _buffText.fontSize = _jobDisplay.LabelFontSize - 4;
            _buffText.color = new Color(0.75f, 0.9f, 1f);
            PlaceAt(_buffText.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0f, gaugeBottom + _jobGauge.Height + _jobDisplay.LabelGapToBar + _jobDisplay.LabelFontSize + 8f),
                new Vector2(totalWidth, _jobDisplay.LabelFontSize + 4f));

            // 전직 알림: 화면 중앙에서 위쪽
            _noticeText.fontSize = _jobDisplay.NoticeFontSize;
            RectTransform noticeRect = _noticeText.rectTransform;
            noticeRect.anchorMin = noticeRect.anchorMax = new Vector2(0.5f, 0.5f);
            noticeRect.pivot = new Vector2(0.5f, 0.5f);
            noticeRect.sizeDelta = new Vector2(800f, _jobDisplay.NoticeFontSize + 20f);
            noticeRect.anchoredPosition = new Vector2(0f, _jobDisplay.NoticeOffsetY);

            // 경고 메시지: 화면 상단 중앙
            _warningText.fontSize = _jobDisplay.WarningFontSize;
            RectTransform warningRect = _warningText.rectTransform;
            warningRect.anchorMin = warningRect.anchorMax = new Vector2(0.5f, 1f);
            warningRect.pivot = new Vector2(0.5f, 0.5f);
            warningRect.sizeDelta = new Vector2(800f, _jobDisplay.WarningFontSize + 16f);
            warningRect.anchoredPosition = new Vector2(0f, -_jobDisplay.WarningOffsetY);

            foreach (SkillSlotView skillSlot in _skillSlots)
            {
                skillSlot.ApplyNameStyle(_jobDisplay);
            }

            // 대상 체력 게이지는 화면 위 가운데
            PlaceCorner(_targetFrame.Rect, new Vector2(0.5f, 1f), new Vector2(0f, -_target.TopMargin), _target.BarSize);
            _targetFrame.ApplyStyle(_target);
            _targetRing.ApplyStyle(_target);
        }

        // 켜져 있는 직업 게이지 (무직이면 없음)
        private IJobGauge FindActiveJobGauge()
        {
            _player.GetComponents(_jobGaugeBuffer);

            foreach (IJobGauge gauge in _jobGaugeBuffer)
            {
                if (gauge is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                return gauge;
            }

            return null;
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

        private Text CreateLabel(string labelName, TextAnchor alignment)
        {
            Text text = HudGraphics.CreateRect(labelName, transform).gameObject.AddComponent<Text>();
            text.font = HudGraphics.Font;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private SkillSlotView CreateSlot(string slotName, string key)
        {
            SkillSlotView view = HudGraphics.CreateRect(slotName, _barRoot).gameObject.AddComponent<SkillSlotView>();
            view.Initialize(key);
            return view;
        }
    }
}
