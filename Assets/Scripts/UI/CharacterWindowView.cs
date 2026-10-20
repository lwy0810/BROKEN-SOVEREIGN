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
    /// 캐릭터 창 (C, GDD 10장): 직업·격·레벨·경험치, 다음 격 돌파 조건, 영웅의 길, 능력치.
    /// 프로토타입은 스탯 분배 대신 레벨업 자동 분배라 읽기 전용. 열려 있는 동안 전투 입력 차단.
    /// HudView가 생성합니다.
    /// </summary>
    public class CharacterWindowView : MonoBehaviour
    {
        private const float WIDTH = 860f;
        private const float HEIGHT = 600f;

        private static readonly (StatType Stat, string Name, bool Percent)[] STATS =
        {
            (StatType.Strength, "힘", false),
            (StatType.Agility, "민첩", false),
            (StatType.Intelligence, "지능", false),
            (StatType.AttackPower, "공격력", false),
            (StatType.AttackSpeed, "공격 속도", true),
            (StatType.CritChance, "치명타 확률", true),
            (StatType.CritDamage, "치명타 피해", true),
            (StatType.DamageBonus, "피해 증가", true),
            (StatType.MaxHealth, "최대 체력", false),
            (StatType.LifeRegen, "초당 생명력 회복", false),
            (StatType.Armor, "방어력", false),
            (StatType.ElementalResistance, "원소 저항", true),
            (StatType.DodgeChance, "회피율", true),
            (StatType.DamageReduction, "받는 피해 감소", true),
            (StatType.MoveSpeed, "이동 속도", false),
            (StatType.CooldownReduction, "재사용 대기 감소", true),
        };

        private RectTransform _panel;
        private Text _summary;
        private Text _statNames;
        private Text _statValues;
        private PlayerProgression _progression;
        private PlayerJob _job;
        private HeroPathController _hero;
        private CharacterStats _stats;
        private Health _health;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Initialize(GameObject player)
        {
            _progression = player.GetComponent<PlayerProgression>();
            _job = player.GetComponent<PlayerJob>();
            _hero = player.GetComponent<HeroPathController>();
            _stats = player.GetComponent<CharacterStats>();
            _health = player.GetComponent<Health>();

            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            _panel = UiKit.Panel("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f),
                new Vector2(WIDTH, HEIGHT));

            Text title = UiKit.Label("Title", _panel, 30, UiKit.GOLD, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(title.rectTransform, new Vector2(28f, 18f), new Vector2(400f, 40f));
            title.text = "캐릭터";

            Text help = UiKit.Label("Help", _panel, 16, UiKit.DIM_TEXT, TextAnchor.MiddleRight);
            UiKit.Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -26f),
                new Vector2(400f, 24f));
            help.text = "C / Esc 닫기";

            // 왼쪽: 성장, 오른쪽: 능력치
            Image divider = UiKit.Box("Divider", _panel, new Color(UiKit.GOLD.r, UiKit.GOLD.g, UiKit.GOLD.b, 0.3f));
            UiKit.PlaceTopLeft(divider.rectTransform, new Vector2(WIDTH * 0.5f, 80f), new Vector2(1f, HEIGHT - 110f));

            _summary = UiKit.Label("Summary", _panel, 19, Color.white, TextAnchor.UpperLeft);
            _summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            _summary.lineSpacing = 1.25f;
            _summary.supportRichText = true;
            UiKit.PlaceTopLeft(_summary.rectTransform, new Vector2(32f, 84f), new Vector2(WIDTH * 0.5f - 60f, HEIGHT - 110f));

            _statNames = UiKit.Label("StatNames", _panel, 18, UiKit.SUBTLE, TextAnchor.UpperLeft);
            _statNames.lineSpacing = 1.3f;
            UiKit.PlaceTopLeft(_statNames.rectTransform, new Vector2(WIDTH * 0.5f + 30f, 84f), new Vector2(220f, HEIGHT - 110f));

            _statValues = UiKit.Label("StatValues", _panel, 18, Color.white, TextAnchor.UpperRight, FontStyle.Bold);
            _statValues.lineSpacing = 1.3f;
            UiKit.PlaceTopLeft(_statValues.rectTransform, new Vector2(WIDTH - 230f, 84f), new Vector2(200f, HEIGHT - 110f));

            _panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                GameplayInput.IsBlocked = false;
            }
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

            if (IsOpen)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var builder = new System.Text.StringBuilder();
            string jobName = _job != null && _job.Current != null ? _job.Current.DisplayName : "무직";
            builder.Append("<size=26><b>").Append(jobName).Append("</b></size>\n");

            if (_progression != null)
            {
                builder.Append("격  <color=#F2CC73>").Append(AscensionModel.DisplayName(_progression.Tier))
                    .Append("</color>      레벨  <color=#F2CC73>").Append(_progression.Level).Append("</color>\n");
                builder.Append(_progression.IsMaxLevel
                    ? "경험치  최대 레벨\n"
                    : $"경험치  {_progression.Experience} / {_progression.ExperienceToNext}  ({_progression.Progress * 100f:0}%)\n");
                builder.Append("\n<color=#F2CC73>다음 격 돌파</color>\n").Append(_progression.NextAscensionHint).Append('\n');
            }

            builder.Append("\n<color=#F2CC73>영웅의 길</color>\n");

            if (_hero != null && _hero.HasPath)
            {
                HeroPathDefinition path = _hero.Current;
                builder.Append(path.DisplayName).Append("  <color=#BBB4A8>").Append(path.Title).Append("</color>\n");
                builder.Append("고유 메커니즘: ").Append(path.MechanicName).Append('\n');
            }
            else
            {
                builder.Append("<color=#99938A>반신 돌파 후 선택</color>\n");
            }

            if (_health != null)
            {
                builder.Append("\n체력  ").Append(Mathf.CeilToInt(_health.Current)).Append(" / ")
                    .Append(Mathf.CeilToInt(_health.Max));
            }

            _summary.text = builder.ToString();

            if (_stats == null)
            {
                _statNames.text = "능력치 없음";
                _statValues.text = string.Empty;
                return;
            }

            StatSheet sheet = _stats.Sheet;
            var names = new System.Text.StringBuilder();
            var values = new System.Text.StringBuilder();

            foreach ((StatType stat, string name, bool percent) in STATS)
            {
                bool main = sheet.MainStat.HasValue && sheet.MainStat.Value == stat;
                names.Append(main ? $"{name} (주)" : name).Append('\n');
                float value = sheet.Get(stat);
                values.Append(percent ? $"{value * 100f:0.#}%" : $"{value:0.#}").Append('\n');
            }

            names.Append("물리 피해 감소").Append('\n');
            values.Append($"{sheet.GetMitigation(DamageType.Physical) * 100f:0.#}%").Append('\n');

            _statNames.text = names.ToString();
            _statValues.text = values.ToString();
        }
    }
}
