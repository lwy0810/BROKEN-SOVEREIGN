using System.Collections.Generic;
using BrokenSovereign.Ascension;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using BrokenSovereign.Item;
using BrokenSovereign.Player;
using BrokenSovereign.Skill;
using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 전투 HUD (GDD 10장, 10/21 B안): 가운데 금속판 기술 바(기술 칸·물약·회피), 양 옆 체력 구슬(왼쪽)·직업 자원 구슬(오른쪽),
    /// 기술 바 위 직업 게이지(신앙)와 직업 이름, 선택한 대상의 상단 체력 게이지와 발밑 원 테두리, 전직 알림.
    /// 시작 시 코드로 UI를 만들고, 매 프레임 플레이어 상태를 읽어 표시합니다 (UI는 읽기 전용).
    /// 색·서체는 UiTheme(_theme), 배치·연출 값은 Play 중 Inspector에서 바꾸면 즉시 반영됩니다.
    /// 아트 확정 후 프리팹으로 옮길 예정입니다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        private static readonly SkillSlot[] SKILL_SLOTS =
        {
            SkillSlot.LeftClick, SkillSlot.RightClick, SkillSlot.Key1, SkillSlot.Key2, SkillSlot.Key3, SkillSlot.Key4,
            SkillSlot.Key5, SkillSlot.Key6
        };

        private static readonly string[] SKILL_KEYS = { "LMB", "RMB", "1", "2", "3", "4", "5", "6" };

        [SerializeField] private PlayerController _player;

        [Tooltip("UI 테마 (10/21): SovereignIron = B안 어두운 금속판, CelestialJade = A′안 천계 백옥. Play 시작 시 적용")]
        [SerializeField] private UiThemeId _theme = UiThemeId.SovereignIron;
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private Shader _orbShader;
        [SerializeField] private OrbSettings _orb = new OrbSettings();
        [SerializeField] private SkillBarSettings _bar = new SkillBarSettings();
        [SerializeField] private TargetSettings _target = new TargetSettings();
        [SerializeField] private JobDisplaySettings _jobDisplay = new JobDisplaySettings();
        [SerializeField] private JobGaugeSettings _jobGauge = new JobGaugeSettings();

        [Header("아트 (BrokenSovereign > UI > HUD 아트 연결 메뉴가 연결)")]
        private const string ORB_SHADER_NAME = "BrokenSovereign/UI/ResourceOrb";
        private const string CURSOR_SETTINGS_RESOURCE = "UI/CursorSettings";
        private const string MAP_ICON_SET_RESOURCE = "UI/MapIconSet";

        [SerializeField] private CursorSettings _cursorSettings;
        [SerializeField] private MapIconSet _mapIconSet;

        private Health _health;
        private IResourceGauge _resource;
        private readonly List<IResourceGauge> _gaugeBuffer = new List<IResourceGauge>();
        private PotionUser _potion;
        private DodgeAction _dodge;

        private CanvasScaler _scaler;
        private ResourceOrbView _healthOrb;
        private ResourceOrbView _resourceOrb;
        private Text _autoMoveText;
        private Image _trayPlate;
        private UiBar _expBar;
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
        private const string NOT_ENOUGH_MANA_MESSAGE = "기력이 부족합니다.";
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
        private NpcDialogView _npcDialog;
        private MinimapView _minimap;
        private WorldMapView _worldMap;
        private BossHudView _bossHud;
        private StatusLabelView _statusLabels;
        private DamageNumberView _damageNumbers;
        private SkillWindowView _skillWindow;
        private CharacterWindowView _characterWindow;
        private InventoryWindowView _inventoryWindow;
        private OutcomeScreenView _outcome;
        private Health _frameTarget;
        private float _noticeStartTime = float.NegativeInfinity;
        private bool _noticeEmphasis;
        private HeroPathController _hero;
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

            UiTheme.Apply(_theme);
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

            UpdateAutoMove();

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
            ShowNotice($"격 돌파 — {AscensionModel.DisplayName(tier)}", true);
        }

        // 격 돌파 알림에 전직을 이어 붙임 (JobChanged 알림이 돌파 알림을 덮어쓰지 않도록)
        private void OnJobAdvanced(JobDefinition job)
        {
            ShowNotice($"격 돌파 — {AscensionModel.DisplayName(_progression.Tier)} · {job.DisplayName} 전직", true);
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

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private void OnHeroPathChosen(HeroPathDefinition path)
        {
            ShowNotice($"영웅의 길 — {path.DisplayName} ({path.Title})", true);
        }

        // emphasis: 격 돌파·영웅의 길처럼 큰 사건은 크게 튀어나왔다 줄어드는 연출
        private void ShowNotice(string message, bool emphasis = false)
        {
            _noticeText.text = message;
            _noticeStartTime = Time.time;
            _noticeEmphasis = emphasis;
            _noticeEndTime = Time.time + _jobDisplay.NoticeDuration * (emphasis ? 1.5f : 1f);
        }

        // 경험치 막대와 "직업 · 격 · 레벨" 표시 (값이 바뀔 때만 문자열 갱신)
        private void UpdateProgression()
        {
            if (_progression == null)
            {
                _expBar.Rect.gameObject.SetActive(false);
                return;
            }

            _expBar.SetRatio(_progression.Progress);

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

        // 기력(공용 자원, 코드명 Mana) 부족: 누르고 있는 동안 반복 시도해도 메시지가 보이는 동안에는 다시 띄우지 않음 (ShowWarning)
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

            Color color = UiTheme.P.Warning;
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

            Color color = UiTheme.P.Title;
            float fade = _jobDisplay.NoticeFadeTime;
            color.a *= fade > 0f ? Mathf.Clamp01(remaining / fade) : 1f;
            _noticeText.color = color;

            // 등장 순간 크게 → 0.3초에 걸쳐 원래 크기
            float punch = _noticeEmphasis ? NOTICE_PUNCH_EMPHASIS : NOTICE_PUNCH;
            float t = 1f - Mathf.Clamp01((Time.time - _noticeStartTime) / NOTICE_PUNCH_TIME);
            _noticeText.rectTransform.localScale = Vector3.one * (1f + punch * t * t);
        }

        private const float NOTICE_PUNCH = 0.15f;
        private const float NOTICE_PUNCH_EMPHASIS = 0.5f;
        private const float NOTICE_PUNCH_TIME = 0.3f;

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
                string skillName = skill != null ? GetSkillLabel(skill)
                    : SKILL_SLOTS[i] == SkillSlot.LeftClick ? "기본 공격" : string.Empty;
                _skillSlots[i].SetSkillName(skillName);
                _skillSlots[i].SetIcon(SkillIcons.Get(skill));
            }
        }

        // 기술 이름 (플레이어의 ISkillLabelSource가 덧붙일 이름이 있으면 사용: 석천 모드변환 → "모드변환 · 단봉")
        private string GetSkillLabel(SkillDefinition skill)
        {
            _caster.GetComponents(_labelSources);

            foreach (ISkillLabelSource source in _labelSources)
            {
                if (source.TryGetSkillLabel(skill.SkillId, out string label))
                {
                    _labelSources.Clear();
                    return label;
                }
            }

            _labelSources.Clear();
            return skill.DisplayName;
        }

        private readonly List<ISkillLabelSource> _labelSources = new List<ISkillLabelSource>();

        // 플레이어가 선택한 대상이 바뀌면 원 테두리와 상단 게이지를 새 대상으로 전환
        private void UpdateTarget()
        {
            Health target = _player.CurrentTarget;
            bool bossShowing = _bossHud != null && _bossHud.IsShowing;

            // 보스를 선택하면 상단 보스 체력바가 대신 보여 주므로 대상 게이지는 숨김.
            // 다른 대상을 고르면 보스 체력바 아래로 내려 겹치지 않게 함
            Health frameTarget = bossShowing && target == _bossHud.ShownHealth ? null : target;
            float top = _target.TopMargin + (bossShowing ? _bossHud.OccupiedHeight : 0f);
            Vector2 position = _targetFrame.Rect.anchoredPosition;
            _targetFrame.Rect.anchoredPosition = new Vector2(position.x, -top);

            if (frameTarget != _frameTarget)
            {
                _frameTarget = frameTarget;
                _targetFrame.SetTarget(frameTarget);
            }

            if (target == _shownTarget)
            {
                return;
            }

            _shownTarget = target;
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

            // 기술 바 금속판 (기술 칸보다 먼저 = 뒤에 그림)
            _trayPlate = UiKit.Plate("SkillTray", transform);

            _barRoot = HudGraphics.CreateRect("SkillBar", transform);
            _skillSlots = new SkillSlotView[SKILL_SLOTS.Length];

            for (int i = 0; i < SKILL_SLOTS.Length; i++)
            {
                _skillSlots[i] = CreateSlot($"Slot_{SKILL_SLOTS[i]}", SKILL_KEYS[i]);
            }

            _potionSlot = CreateSlot("Slot_Potion", "Q");
            _dodgeSlot = CreateSlot("Slot_Dodge", "SPACE");

            // 데미지 숫자는 다른 HUD 아래에 그리도록 먼저 생성
            _damageNumbers = HudGraphics.CreateRect("DamageNumbers", transform).gameObject.AddComponent<DamageNumberView>();
            _damageNumbers.Initialize(_player.gameObject);

            // 적 머리 위 효과 이름 (도발·기절 등)
            _statusLabels = HudGraphics.CreateRect("StatusLabels", transform).gameObject.AddComponent<StatusLabelView>();
            _statusLabels.Initialize(_player.gameObject);

            // 바닥 클릭 이동 표식 (월드에 그리므로 Canvas 밖, 1개만 재사용)
            PlayerInputHandler input = _player.GetComponent<PlayerInputHandler>();

            if (input != null)
            {
                new GameObject("MoveMarker", typeof(LineRenderer)).AddComponent<MoveMarkerView>().Initialize(input);
            }

            _targetFrame = HudGraphics.CreateRect("TargetFrame", transform).gameObject.AddComponent<TargetFrameView>();
            _targetFrame.Initialize();

            // 상단 보스 체력바 (BossStatus가 붙은 적과 교전 중일 때만)
            _bossHud = HudGraphics.CreateRect("BossHud", transform).gameObject.AddComponent<BossHudView>();
            _bossHud.Initialize();

            // 원 테두리는 월드에 그리므로 Canvas 밖에 생성
            _targetRing = new GameObject("TargetRing", typeof(LineRenderer)).AddComponent<TargetRingView>();
            _targetRing.Initialize(_target);

            _jobGaugeView = HudGraphics.CreateRect("JobGauge", transform).gameObject.AddComponent<JobGaugeView>();
            _jobGaugeView.Initialize();

            _jobLabel = CreateLabel("JobLabel", TextAnchor.LowerCenter);
            UiTheme.ApplyTitle(_jobLabel);
            _jobLabel.text = string.Empty;
            _buffText = CreateLabel("BuffList", TextAnchor.LowerCenter);
            _buffText.text = string.Empty;
            _noticeText = CreateLabel("JobNotice", TextAnchor.MiddleCenter);
            UiTheme.ApplyTitle(_noticeText);
            _noticeText.enabled = false;
            _warningText = CreateLabel("Warning", TextAnchor.MiddleCenter);
            _autoMoveText = CreateLabel("AutoMove", TextAnchor.MiddleCenter);
            UiTheme.ApplyTitle(_autoMoveText);
            _autoMoveText.text = "자동 이동 중";
            _autoMoveText.enabled = false;
            _warningText.enabled = false;

            // 경험치 막대: 기술 바 판 바로 아래 (얇은 알약형)
            _expBar = UiKit.Bar("ExpBar", transform, UiTheme.P.Xp, UiTheme.P.Xp * 0.5f);
            _expBar.SetRatio(0f);

            // 씬에 연결하지 않았으면 Resources/UI의 기본 설정 사용 (씬마다 메뉴 실행 불필요)
            if (_cursorSettings == null)
            {
                _cursorSettings = Resources.Load<CursorSettings>(CURSOR_SETTINGS_RESOURCE);
            }

            if (_mapIconSet == null)
            {
                _mapIconSet = Resources.Load<MapIconSet>(MAP_ICON_SET_RESOURCE);
            }

            MapIcons.IconSet = _mapIconSet;
            // 미니맵(오른쪽 위)·지도 화면(M). 지도가 없는 씬에서는 표시 안 함
            _minimap = HudGraphics.CreateRect("Minimap", transform).gameObject.AddComponent<MinimapView>();
            _minimap.Initialize(_player.transform);
            _worldMap = HudGraphics.CreateRect("WorldMap", transform).gameObject.AddComponent<WorldMapView>();
            _worldMap.Initialize(_player.transform);

            // 마우스 커서 (레이어별 모양)
            if (_cursorSettings != null)
            {
                gameObject.AddComponent<CursorController>().Initialize(_cursorSettings);
            }

            // 마을 NPC 안내·대화 창 (NpcInteractor가 있을 때만)
            NpcInteractor interactor = _player.GetComponent<NpcInteractor>();

            if (interactor != null)
            {
                _npcDialog = HudGraphics.CreateRect("NpcDialog", transform).gameObject.AddComponent<NpcDialogView>();
                _npcDialog.Initialize(interactor);
            }

            // 기술 창(K)·캐릭터 창(C)
            if (_caster != null)
            {
                _skillWindow = HudGraphics.CreateRect("SkillWindow", transform).gameObject.AddComponent<SkillWindowView>();
                _skillWindow.Initialize(_caster, _player.gameObject);
            }

            // 가방·장비·처치 보상 (10/21): 씬 수정 없이 플레이어에 없으면 붙임
            Inventory inventory = EnsureComponent<Inventory>(_player.gameObject);
            EnsureComponent<PlayerEquipment>(_player.gameObject);
            EnsureComponent<LootDropper>(_player.gameObject);
            EnsureComponent<SkillPointGrant>(_player.gameObject);

            _characterWindow = HudGraphics.CreateRect("CharacterWindow", transform).gameObject.AddComponent<CharacterWindowView>();
            _characterWindow.Initialize(_player.gameObject, (RectTransform)transform);

            // 인벤토리 창(I)·획득 알림
            _inventoryWindow = HudGraphics.CreateRect("InventoryWindow", transform).gameObject.AddComponent<InventoryWindowView>();
            _inventoryWindow.Initialize(_player.gameObject, (RectTransform)transform);
            HudGraphics.CreateRect("LootFeed", transform).gameObject.AddComponent<LootFeedView>().Initialize(inventory);

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

            // 사망·클리어 화면: 모든 UI 위
            if (_health != null)
            {
                _outcome = HudGraphics.CreateRect("Outcome", transform).gameObject.AddComponent<OutcomeScreenView>();
                _outcome.Initialize(_health);
            }

            _isBuilt = true;
        }

        private void ApplyLayout()
        {
            _scaler.referenceResolution = _referenceResolution;

            // 기술 6칸 + 묶음 간격 + 물약·회피 2칸을 가운데 정렬
            float slot = _bar.SlotSize;
            float skillWidth = SKILL_SLOTS.Length * slot + (SKILL_SLOTS.Length - 1) * _bar.Spacing;
            float utilityWidth = 2f * slot + _bar.Spacing;
            float totalWidth = skillWidth + _bar.GroupSpacing + utilityWidth;

            PlaceCorner(_barRoot, new Vector2(0.5f, 0f), new Vector2(0f, _bar.BottomMargin), new Vector2(totalWidth, slot));
            _barRoot.pivot = new Vector2(0.5f, 0f);

            _healthOrb.ApplyStyle(_orb, _orb.HealthColor, _orb.HealthSurfaceColor);
            Color resourceColor = _resource != null ? _resource.DisplayColor : Color.gray;
            _resourceOrb.ApplyStyle(_orb, resourceColor);

            // 기술 바 판: 칸 둘레 여백 (10/21 B안)
            float trayWidth = totalWidth + TRAY_SIDE * 2f;
            float trayBottom = _bar.BottomMargin - TRAY_BOTTOM;
            float trayTop = _bar.BottomMargin + slot + TRAY_TOP;
            PlaceAt(_trayPlate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, trayBottom),
                new Vector2(trayWidth, trayTop - trayBottom));

            // 구슬은 기술 바 판 양 끝에 붙여 배치 (화면 아래 가운데 기준)
            Vector2 orbSize = new Vector2(_orb.Size, _orb.Size);
            float orbOffset = trayWidth * 0.5f + _orb.GapToBar;
            PlaceAt(_healthOrb.Rect, new Vector2(1f, 0f), new Vector2(-orbOffset, _orb.BottomMargin), orbSize);
            PlaceAt(_resourceOrb.Rect, new Vector2(0f, 0f), new Vector2(orbOffset, _orb.BottomMargin), orbSize);

            // 자동 이동 중 표시 (화면 가운데 위)
            _autoMoveText.fontSize = 26;
            RectTransform autoRect = _autoMoveText.rectTransform;
            autoRect.anchorMin = autoRect.anchorMax = new Vector2(0.5f, 0.5f);
            autoRect.pivot = new Vector2(0.5f, 0.5f);
            autoRect.sizeDelta = new Vector2(600f, 40f);
            autoRect.anchoredPosition = new Vector2(0f, 90f);

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

            // 경험치 막대: 기술 바 판 아래
            PlaceAt(_expBar.Rect, new Vector2(0.5f, 0f), new Vector2(0f, Mathf.Max(1f, trayBottom - 8f)),
                new Vector2(trayWidth - 24f, 6f));

            // 직업 게이지: 체력 막대 위 (기술 바 판 너비)
            float gaugeBottom = trayTop + _jobGauge.GapToBar;
            PlaceAt(_jobGaugeView.Rect, new Vector2(0.5f, 0f), new Vector2(0f, gaugeBottom),
                new Vector2(trayWidth, _jobGauge.Height));
            _jobGaugeView.ApplyStyle(_jobGauge);

            // 직업 이름: 게이지 위 가운데 (게이지가 없어도 같은 자리)
            _jobLabel.fontSize = _jobDisplay.LabelFontSize;
            _jobLabel.color = UiTheme.P.Title;
            PlaceAt(_jobLabel.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0f, gaugeBottom + _jobGauge.Height + _jobDisplay.LabelGapToBar),
                new Vector2(totalWidth, _jobDisplay.LabelFontSize + 8f));

            // 지속 버프 목록: 직업 이름 위
            _buffText.fontSize = _jobDisplay.LabelFontSize - 4;
            _buffText.color = UiTheme.P.Buff;
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

        private const float TRAY_SIDE = 24f;
        private const float TRAY_BOTTOM = 14f;
        private const float TRAY_TOP = 12f;

        private ResourceOrbView CreateOrb(string orbName, Shader shader)
        {
            ResourceOrbView orb = HudGraphics.CreateRect(orbName, transform).gameObject.AddComponent<ResourceOrbView>();
            orb.Initialize(shader);
            return orb;
        }

        // 지도에서 고른 자동 이동: 이동 중이면 화면 가운데 "자동 이동 중" (천천히 깜빡임)
        private void UpdateAutoMove()
        {
            MapPin.CheckArrival(_player.transform.position);
            bool moving = _player.IsAutoMoving;
            _autoMoveText.enabled = moving;

            if (moving)
            {
                Color color = UiTheme.P.Title;
                color.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);
                _autoMoveText.color = color;
            }
        }

        private Text CreateLabel(string labelName, TextAnchor alignment)
        {
            Text text = UiKit.SceneLabel(labelName, transform, 20, UiTheme.P.OnScene, alignment);
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
