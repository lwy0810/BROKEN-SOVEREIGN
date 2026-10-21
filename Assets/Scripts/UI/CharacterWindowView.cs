using System;
using System.Collections.Generic;
using BrokenSovereign.Ascension;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 캐릭터 창 (C, GDD 10장): 왼쪽 직업·격·레벨·경험치·다음 격 돌파·영웅의 길 / 가운데 캐릭터 모델(장착 모습) / 오른쪽 능력치(휠 스크롤).
    /// 능력치는 분류별로 묶어 표시 (주능력치·공격·방어·보조·자원, 10/21). 줄 색은 바꾸지 않음.
    /// 프로토타입은 스탯 분배 대신 레벨업 자동 분배라 읽기 전용. 열려 있는 동안 전투 입력 차단. 닫기: C·Esc·오른쪽 위 ×.
    /// HudView가 생성합니다.
    /// </summary>
    public class CharacterWindowView : MonoBehaviour
    {
        private const float WIDTH = 1100f;
        private const float HEIGHT = 720f;
        private const float ROW = 30f;
        private const float HEADER = 36f;
        private const float GROUP_GAP = 14f;
        private const float TOP = 86f;
        private const float SLOT = 44f;
        private const float SCROLL_STEP = 90f;
        private const float SCROLL_SMOOTH = 14f;

        private sealed class Entry
        {
            public string Name;
            public Func<string> Value;
            public StatType? Stat;
            public bool Percent;
            public Text NameText;
            public Text ValueText;
        }

        private RectTransform _panel;
        private Image _close;
        private WindowDrag _drag;
        private Text _jobText;
        private Text _tierText;
        private UiBar _expBar;
        private Text _expText;
        private Text _hintText;
        private Text _heroText;
        private CharacterPreview _preview;
        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _scrollTrack;
        private RectTransform _scrollHandle;
        private float _scrollTarget;
        private float _scroll;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<IJobGauge> _jobGauges = new List<IJobGauge>();
        private readonly List<IResourceGauge> _resources = new List<IResourceGauge>();
        private GameObject _player;
        private PlayerProgression _progression;
        private PlayerJob _job;
        private HeroPathController _hero;
        private CharacterStats _stats;
        private Health _health;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Initialize(GameObject player)
        {
            _player = player;
            _progression = player.GetComponent<PlayerProgression>();
            _job = player.GetComponent<PlayerJob>();
            _hero = player.GetComponent<HeroPathController>();
            _stats = player.GetComponent<CharacterStats>();
            _health = player.GetComponent<Health>();
            UiPalette p = UiTheme.P;

            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(WIDTH, HEIGHT));
            _drag = new WindowDrag(_panel, (RectTransform)UiKit.TitleTab(_panel, "캐릭터").transform.parent);
            _close = UiKit.CloseButton(_panel);

            // 왼쪽: 성장
            float left = 36f, colWidth = 320f;
            _jobText = UiKit.Title("Job", _panel, 34, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_jobText.rectTransform, new Vector2(left, TOP), new Vector2(colWidth, 46f));
            _tierText = UiKit.Title("Tier", _panel, 19, p.Text, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_tierText.rectTransform, new Vector2(left, TOP + 50f), new Vector2(colWidth, 28f));

            _expBar = UiKit.Bar("Exp", _panel, p.Xp, p.Xp * 0.5f);
            UiKit.PlaceTopLeft(_expBar.Rect, new Vector2(left, TOP + 90f), new Vector2(colWidth, 10f));
            _expText = UiKit.Label("ExpText", _panel, 13, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_expText.rectTransform, new Vector2(left, TOP + 106f), new Vector2(colWidth, 20f));

            Header(_panel, "다음 격 돌파", left, TOP + 150f, colWidth);
            _hintText = UiKit.Label("Hint", _panel, 15, p.Text, TextAnchor.UpperLeft);
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.PlaceTopLeft(_hintText.rectTransform, new Vector2(left, TOP + 150f + HEADER), new Vector2(colWidth, 46f));

            Header(_panel, "영웅의 길", left, TOP + 250f, colWidth);
            _heroText = UiKit.Label("Hero", _panel, 15, p.Text, TextAnchor.UpperLeft);
            _heroText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.PlaceTopLeft(_heroText.rectTransform, new Vector2(left, TOP + 250f + HEADER), new Vector2(colWidth, 70f));

            Text note = UiKit.Label("Note", _panel, 13, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(left, 22f), new Vector2(colWidth, 20f));
            note.text = "능력치는 레벨업 시 자동 분배 (프로토타입)";

            Divider(386f);
            Divider(738f);

            BuildPreview(410f, 304f);
            BuildStats(762f, 290f);

            _panel.gameObject.SetActive(false);
        }

        private static Entry Stat(string name, StatType stat, bool percent)
        {
            return new Entry { Name = name, Stat = stat, Percent = percent };
        }

        private static Entry Custom(string name, Func<string> value)
        {
            return new Entry { Name = name, Value = value };
        }

        // 가운데: 캐릭터 모델 (장비 착용 모습 확인용) + 장비 칸
        private void BuildPreview(float x, float width)
        {
            UiPalette p = UiTheme.P;
            Header(_panel, "장착 모습", x, TOP, width);

            float top = TOP + HEADER;
            float slotY = HEIGHT - 40f - SLOT - 18f;
            Vector2 size = new Vector2(width, slotY - 14f - top);
            Image frame = UiKit.Sliced("PreviewFrame", _panel, UiSprites.Tile);
            UiKit.PlaceTopLeft(frame.rectTransform, new Vector2(x, top), size);

            _preview = new CharacterPreview(frame.transform, size - new Vector2(8f, 8f));
            HudGraphics.Stretch(_preview.Rect);
            _preview.Rect.offsetMin = new Vector2(4f, 4f);
            _preview.Rect.offsetMax = new Vector2(-4f, -4f);

            Text hint = UiKit.Label("RotateHint", frame.transform, 12, p.TextDim, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(width, 18f));
            hint.text = "끌어서 회전";

            string[] slots = { "무기", "투구", "갑옷", "장갑", "신발", "장신구" };
            float gap = (width - SLOT * slots.Length) / (slots.Length - 1);

            for (int i = 0; i < slots.Length; i++)
            {
                float sx = x + i * (SLOT + gap);
                Image tile = UiKit.Sliced("Slot", _panel, UiSprites.Tile);
                UiKit.PlaceTopLeft(tile.rectTransform, new Vector2(sx, slotY), new Vector2(SLOT, SLOT));
                Text name = UiKit.Label("SlotName", _panel, 11, p.TextDim, TextAnchor.MiddleCenter);
                UiKit.PlaceTopLeft(name.rectTransform, new Vector2(sx - 6f, slotY + SLOT + 2f), new Vector2(SLOT + 12f, 16f));
                name.text = slots[i];
            }
        }

        // 오른쪽: 능력치 전체 (주능력치·공격·방어·보조·자원). 넘치는 부분은 마우스 휠로 이동
        private void BuildStats(float x, float width)
        {
            float height = HEIGHT - 40f - TOP;
            _viewport = HudGraphics.CreateRect("StatViewport", _panel);
            UiKit.PlaceTopLeft(_viewport, new Vector2(x, TOP), new Vector2(width, height));
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = HudGraphics.CreateRect("StatContent", _viewport);
            float y = 0f;
            y = Group(_content, "주능력치", 0f, y, width, new[]
            {
                Stat("힘", StatType.Strength, false), Stat("민첩", StatType.Agility, false), Stat("지능", StatType.Intelligence, false)
            });
            y = Group(_content, "공격", 0f, y + GROUP_GAP, width, new[]
            {
                Stat("공격력", StatType.AttackPower, false), Stat("공격 속도", StatType.AttackSpeed, true),
                Stat("치명타 확률", StatType.CritChance, true), Stat("치명타 피해", StatType.CritDamage, true),
                Stat("피해 증가", StatType.DamageBonus, true), Stat("물리 피해 증가", StatType.PhysicalDamageBonus, true),
                Stat("원소 피해 증가", StatType.ElementalDamageBonus, true)
            });
            y = Group(_content, "방어", 0f, y + GROUP_GAP, width, new[]
            {
                Stat("방어력", StatType.Armor, false), Stat("원소 저항", StatType.ElementalResistance, true),
                Stat("회피율", StatType.DodgeChance, true), Stat("받는 피해 감소", StatType.DamageReduction, true),
                Custom("물리 피해 감소", () => _stats != null ? Percent(_stats.Sheet.GetMitigation(DamageType.Physical)) : "-")
            });
            y = Group(_content, "보조", 0f, y + GROUP_GAP, width, new[]
            {
                Stat("이동 속도", StatType.MoveSpeed, false), Stat("재사용 대기 감소", StatType.CooldownReduction, true),
                Stat("초당 생명력 회복", StatType.LifeRegen, false), Stat("물약 회복량 증가", StatType.PotionHealing, true)
            });
            y = Group(_content, "자원", 0f, y + GROUP_GAP, width, new[]
            {
                Custom("최대 체력", () => _health != null ? $"{Mathf.CeilToInt(_health.Max)}" : "-"),
                Custom("최대 기력", () => FindResource() is IResourceGauge r ? $"{Mathf.CeilToInt(r.Max)}" : "-"),
                Custom("최대 신앙", () => FindJobGauge() is IJobGauge g ? $"{Mathf.CeilToInt(g.Max)}" : "-"),
                Stat("기력 소모 감소", StatType.ResourceCostReduction, true)
            });
            UiKit.PlaceTopLeft(_content, Vector2.zero, new Vector2(width, y + 8f));

            // 스크롤 막대 (넘칠 때만)
            _scrollTrack = UiKit.Box("ScrollTrack", _panel, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.25f)).rectTransform;
            UiKit.PlaceTopLeft(_scrollTrack, new Vector2(x + width + 10f, TOP), new Vector2(4f, height));
            _scrollHandle = UiKit.Box("ScrollHandle", _scrollTrack, UiTheme.WithAlpha(UiTheme.P.Accent, 0.8f)).rectTransform;
        }

        private float MaxScroll => _content != null ? Mathf.Max(0f, _content.sizeDelta.y - _viewport.rect.height) : 0f;

        private void UpdateScroll()
        {
            Mouse mouse = Mouse.current;
            float max = MaxScroll;

            if (mouse != null && UiKit.IsHover(_viewport))
            {
                float wheel = mouse.scroll.ReadValue().y;

                if (Mathf.Abs(wheel) > 0.01f)
                {
                    _scrollTarget -= Mathf.Sign(wheel) * SCROLL_STEP;
                }
            }

            _scrollTarget = Mathf.Clamp(_scrollTarget, 0f, max);
            _scroll = Mathf.Lerp(_scroll, _scrollTarget, 1f - Mathf.Exp(-SCROLL_SMOOTH * Time.unscaledDeltaTime));
            _content.anchoredPosition = new Vector2(0f, _scroll);

            bool overflow = max > 0.5f;
            _scrollTrack.gameObject.SetActive(overflow);

            if (overflow)
            {
                float track = _scrollTrack.rect.height;
                float handle = Mathf.Max(30f, track * _viewport.rect.height / _content.sizeDelta.y);
                UiKit.PlaceTopLeft(_scrollHandle, new Vector2(0f, (track - handle) * (_scroll / max)), new Vector2(4f, handle));
            }
        }

        // 분류 제목 + 행들. 다음 분류를 놓을 y 반환
        private float Group(RectTransform parent, string title, float x, float y, float width, Entry[] entries)
        {
            Header(parent, title, x, y, width);
            y += HEADER;

            foreach (Entry entry in entries)
            {
                entry.NameText = UiKit.Label("Name", parent, 15, UiTheme.P.TextDim, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(entry.NameText.rectTransform, new Vector2(x + 8f, y), new Vector2(width - 120f, ROW));
                entry.NameText.text = entry.Name;
                entry.ValueText = UiKit.Label("Value", parent, 16, UiTheme.P.Text, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.PlaceTopLeft(entry.ValueText.rectTransform, new Vector2(x + width - 128f, y), new Vector2(120f, ROW));
                _entries.Add(entry);
                y += ROW;
            }

            return y;
        }

        private void Header(RectTransform parent, string title, float x, float y, float width)
        {
            Text text = UiKit.Title(title, parent, 17, UiTheme.P.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(text.rectTransform, new Vector2(x, y), new Vector2(width, 28f));
            text.text = title;
            Image line = UiKit.Box("Line", parent, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.45f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, y + 29f), new Vector2(width, 1f));
        }

        private void Divider(float x)
        {
            Image divider = UiKit.Box("Divider", _panel, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.35f));
            UiKit.PlaceTopLeft(divider.rectTransform, new Vector2(x, TOP), new Vector2(1f, HEIGHT - TOP - 40f));
        }

        private IResourceGauge FindResource()
        {
            _player.GetComponents(_resources);

            foreach (IResourceGauge gauge in _resources)
            {
                if (!(gauge is Behaviour behaviour) || behaviour.isActiveAndEnabled)
                {
                    return gauge;
                }
            }

            return null;
        }

        private IJobGauge FindJobGauge()
        {
            _player.GetComponents(_jobGauges);

            foreach (IJobGauge gauge in _jobGauges)
            {
                if (!(gauge is Behaviour behaviour) || behaviour.isActiveAndEnabled)
                {
                    return gauge;
                }
            }

            return null;
        }

        private static string Percent(float value)
        {
            return $"{value * 100f:0.#}%";
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                GameplayInput.IsBlocked = false;
            }

            _preview?.Release();
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else if (!GameplayInput.IsBlocked)
            {
                _panel.gameObject.SetActive(true);
                GameplayInput.IsBlocked = true;
                _scrollTarget = _scroll = 0f;
                _preview.Open(_player);
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _panel.gameObject.SetActive(false);
            GameplayInput.IsBlocked = false;
            _preview.Close();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                Toggle();
            }
            else if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (IsOpen && UiKit.IsCloseClicked(_close))
            {
                Close();
            }

            if (IsOpen)
            {
                _drag.Tick();
                _preview.Tick();
                UpdateScroll();
                Refresh();
            }
        }

        private void Refresh()
        {
            UiPalette p = UiTheme.P;
            _jobText.text = _job != null && _job.Current != null ? _job.Current.DisplayName : "무직";

            if (_progression != null)
            {
                _tierText.text = $"격  {AscensionModel.DisplayName(_progression.Tier)}      레벨  {_progression.Level}";
                _expBar.SetRatio(_progression.IsMaxLevel ? 1f : _progression.Progress);
                _expText.text = _progression.IsMaxLevel
                    ? "경험치  최대 레벨"
                    : $"경험치  {_progression.Experience} / {_progression.ExperienceToNext}  ({_progression.Progress * 100f:0}%)";
                _hintText.text = _progression.NextAscensionHint;
            }

            if (_hero != null && _hero.HasPath)
            {
                HeroPathDefinition path = _hero.Current;
                _heroText.color = p.Text;
                _heroText.text = $"{path.DisplayName}  ·  {path.Title}\n고유 메커니즘: {path.MechanicName}";
            }
            else
            {
                _heroText.color = p.TextDisabled;
                _heroText.text = "반신 돌파 후 선택";
            }

            StatSheet sheet = _stats != null ? _stats.Sheet : null;

            foreach (Entry entry in _entries)
            {
                if (entry.Stat.HasValue)
                {
                    StatType stat = entry.Stat.Value;
                    bool main = sheet != null && sheet.MainStat.HasValue && sheet.MainStat.Value == stat;
                    entry.NameText.text = main ? $"{entry.Name} (주)" : entry.Name;
                    entry.NameText.color = main ? p.Title : p.TextDim;

                    if (sheet == null)
                    {
                        entry.ValueText.text = "-";
                        continue;
                    }

                    float value = sheet.Get(stat);
                    entry.ValueText.text = entry.Percent ? Percent(value) : $"{value:0.#}";
                }
                else
                {
                    entry.ValueText.text = entry.Value();
                }
            }
        }
    }
}
