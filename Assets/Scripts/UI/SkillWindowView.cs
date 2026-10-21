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

        private const float WIDTH = 1100f;
        private const float ROW_HEIGHT = 74f;
        private const float CANDIDATE_WIDTH = 150f;

        private sealed class Candidate
        {
            public SkillSlot Slot;
            public SkillDefinition Skill;
            public RectTransform Rect;
            public Image Background;
            public Image Glow;
            public Text Label;
        }

        private SkillCaster _caster;
        private RectTransform _panel;
        private RectTransform _rows;
        private Image _close;
        private WindowDrag _drag;
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
            UiPalette p = UiTheme.P;

            float height = 100f + SLOTS.Length * ROW_HEIGHT + 56f;
            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(WIDTH, height));
            _drag = new WindowDrag(_panel, (RectTransform)UiKit.TitleTab(_panel, "기술").transform.parent);

            Text help = UiKit.Label("Help", _panel, 14, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(help.rectTransform, new Vector2(30f, 38f), new Vector2(420f, 22f));
            help.text = "해금한 후보를 눌러 장착 · 전투 중에는 교체 불가";

            _close = UiKit.CloseButton(_panel);

            _rows = HudGraphics.CreateRect("Rows", _panel);
            UiKit.PlaceTopLeft(_rows, new Vector2(24f, 84f), new Vector2(WIDTH - 48f, SLOTS.Length * ROW_HEIGHT));

            _message = UiKit.Label("Message", _panel, 16, p.Title, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(WIDTH, 26f));

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
            else if (IsOpen && UiKit.IsCloseClicked(_close))
            {
                Close();
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            _drag.Tick();

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
                candidate.Background.sprite = equipped ? UiSprites.ButtonEquip : UiSprites.Button;
                candidate.Background.color = !unlocked ? new Color(1f, 1f, 1f, 0.5f)
                    : hover && !equipped ? new Color(1f, 0.92f, 0.8f) : Color.white;
                candidate.Glow.enabled = equipped || (hover && unlocked);
                candidate.Glow.color = UiTheme.WithAlpha(UiTheme.P.CrackOuter, equipped ? 0.35f : 0.2f);
                candidate.Label.color = !unlocked ? UiTheme.P.TextDisabled : equipped ? UiTheme.P.EquipText : UiTheme.P.Text;

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
            UiPalette p = UiTheme.P;

            for (int i = _rows.childCount - 1; i >= 0; i--)
            {
                Destroy(_rows.GetChild(i).gameObject);
            }

            float rowWidth = WIDTH - 48f;

            for (int i = 0; i < SLOTS.Length; i++)
            {
                SkillSlot slot = SLOTS[i];
                Image row = UiKit.Round($"Row_{slot}", _rows, i % 2 == 0 ? p.RowA : p.RowB);
                UiKit.PlaceTopLeft(row.rectTransform, new Vector2(0f, i * ROW_HEIGHT), new Vector2(rowWidth, ROW_HEIGHT - 6f));

                SkillDefinition equipped = _caster.GetSkill(slot);
                bool open = equipped != null || slot == SkillSlot.LeftClick;

                // 기술 칸 (HUD와 같은 모양)
                Image tile = UiKit.Sliced("Tile", row.transform, UiSprites.Tile);
                UiKit.PlaceTopLeft(tile.rectTransform, new Vector2(8f, 5f), new Vector2(ROW_HEIGHT - 16f, ROW_HEIGHT - 16f));
                tile.color = open ? Color.white : new Color(1f, 1f, 1f, 0.45f);

                if (open)
                {
                    Image rune = UiKit.Sliced("Rune", tile.transform, UiSprites.Rune);
                    rune.type = Image.Type.Simple;
                    HudGraphics.Stretch(rune.rectTransform);
                    rune.rectTransform.offsetMin = new Vector2(15f, 15f);
                    rune.rectTransform.offsetMax = new Vector2(-15f, -15f);
                }

                Text key = UiKit.Label("Key", tile.transform, 11, open ? p.Title : p.TextDisabled, TextAnchor.LowerRight, FontStyle.Bold);
                HudGraphics.Stretch(key.rectTransform);
                key.rectTransform.offsetMin = new Vector2(6f, 4f);
                key.rectTransform.offsetMax = new Vector2(-6f, -4f);
                key.text = KEYS[i];

                Text name = UiKit.Title("Name", row.transform, 19, open ? p.Text : p.TextDisabled, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(name.rectTransform, new Vector2(ROW_HEIGHT + 4f, equipped != null ? 8f : 18f), new Vector2(360f, 28f));
                name.text = equipped != null ? equipped.DisplayName
                    : slot == SkillSlot.LeftClick ? "기본 공격" : "미개방";

                if (equipped != null)
                {
                    Text info = UiKit.Label("Info", row.transform, 13, p.TextDim, TextAnchor.MiddleLeft);
                    UiKit.PlaceTopLeft(info.rectTransform, new Vector2(ROW_HEIGHT + 4f, 38f), new Vector2(420f, 20f));
                    info.text = Describe(equipped);
                }

                IReadOnlyList<SkillDefinition> candidates = _caster.Loadout.GetCandidates(slot);
                float x = rowWidth - 14f - CANDIDATE_WIDTH;

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
            Image glow = UiKit.Sliced($"Glow_{skill.SkillId}", row, UiSprites.Glow);
            glow.type = Image.Type.Simple;
            UiKit.PlaceTopLeft(glow.rectTransform, new Vector2(x - 14f, 2f), new Vector2(CANDIDATE_WIDTH + 28f, ROW_HEIGHT - 10f));
            glow.enabled = false;

            Image background = UiKit.Button($"Candidate_{skill.SkillId}", row, string.Empty);
            UiKit.PlaceTopLeft(background.rectTransform, new Vector2(x, 13f), new Vector2(CANDIDATE_WIDTH, ROW_HEIGHT - 32f));

            bool unlocked = _caster.Loadout.IsUnlocked(skill);
            Text label = background.GetComponentInChildren<Text>();
            label.fontSize = 14;
            label.text = unlocked ? skill.DisplayName : $"{skill.DisplayName} · 잠김";

            return new Candidate
            {
                Slot = slot,
                Skill = skill,
                Rect = background.rectTransform,
                Background = background,
                Glow = glow,
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
                parts.Add($"기력 {skill.ResourceCost:0}");
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
