using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 스킬 창 (K, GDD 10장): 슬롯별 장착 기술과 후보 기술. 해금한 후보를 눌러 장착 (비전투일 때만).
    /// 기술 수치(재사용 대기·비용·피해·범위) 요약 표시. 열려 있는 동안 전투 입력 차단 (게임은 계속 진행).
    /// HudView가 생성합니다. Alt+숫자 디버그 교체를 대신합니다.
    /// </summary>
    public class SkillWindowView : MonoBehaviour
    {
        private static readonly SkillSlot[] SLOTS =
        {
            SkillSlot.LeftClick, SkillSlot.RightClick, SkillSlot.Key1, SkillSlot.Key2, SkillSlot.Key3,
            SkillSlot.Key4, SkillSlot.Key5, SkillSlot.Key6
        };

        private static readonly string[] KEYS = { "LMB", "RMB", "1", "2", "3", "4", "5", "6" };

        private const float WIDTH = 1040f;
        private const float ROW_HEIGHT = 62f;
        private const float CANDIDATE_WIDTH = 150f;

        private sealed class Candidate
        {
            public SkillSlot Slot;
            public SkillDefinition Skill;
            public RectTransform Rect;
            public Image Background;
            public Outline Outline;
            public Text Label;
        }

        private SkillCaster _caster;
        private RectTransform _panel;
        private RectTransform _rows;
        private Text _message;
        private float _messageEndTime;
        private bool _dirty = true;
        private readonly List<Candidate> _candidates = new List<Candidate>();

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Initialize(SkillCaster caster)
        {
            _caster = caster;
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            float height = 120f + SLOTS.Length * ROW_HEIGHT + 50f;
            _panel = UiKit.Panel("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f),
                new Vector2(WIDTH, height));

            Text title = UiKit.Label("Title", _panel, 30, UiKit.GOLD, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(title.rectTransform, new Vector2(28f, 18f), new Vector2(400f, 40f));
            title.text = "기술";

            Text help = UiKit.Label("Help", _panel, 16, UiKit.DIM_TEXT, TextAnchor.MiddleRight);
            UiKit.Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -26f),
                new Vector2(600f, 24f));
            help.text = "해금한 후보를 눌러 장착 · 전투 중에는 교체 불가 · K / Esc 닫기";

            Text header = UiKit.Label("Header", _panel, 15, UiKit.DIM_TEXT, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(header.rectTransform, new Vector2(28f, 70f), new Vector2(WIDTH - 56f, 22f));
            header.text = "키        장착 기술                                                           후보 기술";

            _rows = HudGraphics.CreateRect("Rows", _panel);
            UiKit.PlaceTopLeft(_rows, new Vector2(20f, 98f), new Vector2(WIDTH - 40f, SLOTS.Length * ROW_HEIGHT));

            _message = UiKit.Label("Message", _panel, 18, UiKit.GOLD, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                new Vector2(WIDTH, 28f));

            if (_caster != null)
            {
                _caster.Loadout.Changed += OnLoadoutChanged;
            }

            _panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_caster != null)
            {
                _caster.Loadout.Changed -= OnLoadoutChanged;
            }

            if (IsOpen)
            {
                GameplayInput.IsBlocked = false;
            }
        }

        private void OnLoadoutChanged(SkillSlot slot, SkillDefinition skill)
        {
            _dirty = true;
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (IsOpen || GameplayInput.IsBlocked || _caster == null)
            {
                return;
            }

            _dirty = true;
            _panel.gameObject.SetActive(true);
            GameplayInput.IsBlocked = true;
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

            if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
            {
                Toggle();
            }
            else if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }

            if (!IsOpen)
            {
                return;
            }

            if (_dirty)
            {
                Rebuild();
            }

            _message.enabled = Time.unscaledTime < _messageEndTime;

            foreach (Candidate candidate in _candidates)
            {
                bool hover = UiKit.IsHover(candidate.Rect);
                bool equipped = _caster.GetSkill(candidate.Slot) == candidate.Skill;
                bool unlocked = _caster.Loadout.IsUnlocked(candidate.Skill);
                candidate.Background.color = equipped ? new Color(0.35f, 0.27f, 0.1f, 0.95f)
                    : hover && unlocked ? UiKit.ROW_HOVER : new Color(1f, 1f, 1f, 0.06f);
                candidate.Outline.effectColor = equipped || hover ? UiKit.GOLD : new Color(1f, 1f, 1f, 0.15f);
                candidate.Label.color = unlocked ? (equipped ? UiKit.GOLD : Color.white) : UiKit.DIM_TEXT;

                if (hover && UiKit.Clicked)
                {
                    TryEquip(candidate);
                }
            }
        }

        private void TryEquip(Candidate candidate)
        {
            EquipResult result = _caster.TryEquip(candidate.Slot, candidate.Skill);

            switch (result)
            {
                case EquipResult.Success:
                    ShowMessage($"{candidate.Skill.DisplayName} 장착");
                    break;
                case EquipResult.InCombat:
                    ShowMessage("전투 중에는 교체할 수 없습니다");
                    break;
                case EquipResult.Locked:
                    ShowMessage("아직 해금하지 않은 기술입니다");
                    break;
                case EquipResult.AlreadyEquipped:
                    break;
                default:
                    ShowMessage("장착할 수 없는 기술입니다");
                    break;
            }

            _dirty = true;
        }

        private void ShowMessage(string message)
        {
            _message.text = message;
            _messageEndTime = Time.unscaledTime + 2f;
        }

        // 행 다시 만들기 (열 때·구성 변경 시)
        private void Rebuild()
        {
            _dirty = false;
            _candidates.Clear();

            for (int i = _rows.childCount - 1; i >= 0; i--)
            {
                Destroy(_rows.GetChild(i).gameObject);
            }

            float rowWidth = WIDTH - 40f;

            for (int i = 0; i < SLOTS.Length; i++)
            {
                SkillSlot slot = SLOTS[i];
                Image row = UiKit.Box($"Row_{slot}", _rows, i % 2 == 0 ? UiKit.ROW : Color.clear);
                UiKit.PlaceTopLeft(row.rectTransform, new Vector2(0f, i * ROW_HEIGHT), new Vector2(rowWidth, ROW_HEIGHT - 4f));

                Text key = UiKit.Label("Key", row.transform, 20, UiKit.GOLD, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.PlaceTopLeft(key.rectTransform, new Vector2(8f, 0f), new Vector2(56f, ROW_HEIGHT - 4f));
                key.text = KEYS[i];

                SkillDefinition equipped = _caster.GetSkill(slot);
                Text name = UiKit.Label("Name", row.transform, 20, equipped != null ? Color.white : UiKit.DIM_TEXT,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.PlaceTopLeft(name.rectTransform, new Vector2(76f, 6f), new Vector2(360f, 26f));
                name.text = equipped != null ? equipped.DisplayName
                    : slot == SkillSlot.LeftClick ? "기본 공격" : "미개방";

                Text info = UiKit.Label("Info", row.transform, 14, UiKit.SUBTLE, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(info.rectTransform, new Vector2(76f, 32f), new Vector2(380f, 20f));
                info.text = equipped != null ? Describe(equipped) : string.Empty;

                IReadOnlyList<SkillDefinition> candidates = _caster.Loadout.GetCandidates(slot);
                float x = rowWidth - 12f - CANDIDATE_WIDTH;

                for (int c = candidates.Count - 1; c >= 0; c--)
                {
                    SkillDefinition skill = candidates[c];

                    if (skill == null)
                    {
                        continue;
                    }

                    _candidates.Add(CreateCandidate(row.transform, slot, skill, x));
                    x -= CANDIDATE_WIDTH + 10f;
                }
            }
        }

        private Candidate CreateCandidate(Transform row, SkillSlot slot, SkillDefinition skill, float x)
        {
            Image background = UiKit.Box($"Candidate_{skill.SkillId}", row, Color.clear);
            UiKit.PlaceTopLeft(background.rectTransform, new Vector2(x, 10f), new Vector2(CANDIDATE_WIDTH, ROW_HEIGHT - 24f));
            Outline outline = background.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            bool unlocked = _caster.Loadout.IsUnlocked(skill);
            Text label = UiKit.Label("Label", background.transform, 16, Color.white, TextAnchor.MiddleCenter);
            HudGraphics.Stretch(label.rectTransform);
            label.text = unlocked ? skill.DisplayName : $"{skill.DisplayName} (잠김)";

            return new Candidate
            {
                Slot = slot,
                Skill = skill,
                Rect = background.rectTransform,
                Background = background,
                Outline = outline,
                Label = label
            };
        }

        /// <summary>기술 수치 요약: 재사용 대기·비용·피해·범위</summary>
        public static string Describe(SkillDefinition skill)
        {
            var parts = new List<string>();

            if (skill.IsChanneled)
            {
                parts.Add("키 유지");
            }
            else if (skill.Cooldown > 0f)
            {
                parts.Add($"재사용 대기 {skill.Cooldown:0.#}초");
            }
            else
            {
                parts.Add("즉시 발동");
            }

            if (skill.ResourceCost > 0f)
            {
                parts.Add($"마나 {skill.ResourceCost:0}");
            }

            float coefficient = skill.GetBaseParameter(SkillParameterKeys.COEFFICIENT);

            if (coefficient > 0f)
            {
                parts.Add($"피해 {coefficient * 100f:0}%");
            }

            float radius = skill.GetBaseParameter(SkillParameterKeys.RADIUS);

            if (radius > 0f)
            {
                parts.Add($"범위 {radius:0.#}m");
            }

            float duration = skill.GetBaseParameter(SkillParameterKeys.DURATION);

            if (duration > 0f)
            {
                parts.Add($"지속 {duration:0.#}초");
            }

            return string.Join(" · ", parts);
        }
    }
}
