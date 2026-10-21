using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 능력치 목록 (주능력치·공격·방어·보조·자원 5분류, 10/21). 캐릭터 창 오른쪽·인벤토리 상세 정보창 공용.
    /// 넘치면 마우스 휠로 스크롤 (넘칠 때만 스크롤 막대). bonus를 주면 그만큼을 초록 "+값"으로 함께 표시 (장비 보너스).
    /// </summary>
    public sealed class StatListView
    {
        public const float ROW = 30f;
        public const float HEADER = 36f;
        private const float GROUP_GAP = 14f;
        private const float SCROLL_STEP = 90f;
        private const float SCROLL_SMOOTH = 14f;

        private sealed class Entry
        {
            public string Name;
            public Func<string> Value;
            public StatType? Stat;
            public Text NameText;
            public Text ValueText;
            public Text BonusText;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<IJobGauge> _jobGauges = new List<IJobGauge>();
        private readonly List<IResourceGauge> _resources = new List<IResourceGauge>();
        private readonly GameObject _player;
        private readonly CharacterStats _stats;
        private readonly Health _health;
        private readonly Func<StatType, float> _bonus;
        private readonly RectTransform _viewport;
        private readonly RectTransform _content;
        private readonly RectTransform _scrollTrack;
        private readonly RectTransform _scrollHandle;
        private float _scrollTarget;
        private float _scroll;

        public StatListView(RectTransform parent, Vector2 position, Vector2 size, GameObject player,
            Func<StatType, float> bonus = null)
        {
            _player = player;
            _stats = player.GetComponent<CharacterStats>();
            _health = player.GetComponent<Health>();
            _bonus = bonus;

            _viewport = HudGraphics.CreateRect("StatViewport", parent);
            UiKit.PlaceTopLeft(_viewport, position, size);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _content = HudGraphics.CreateRect("StatContent", _viewport);

            float width = size.x;
            float y = 0f;
            y = Group("주능력치", y, width, new[]
            {
                Stat(StatType.Strength), Stat(StatType.Agility), Stat(StatType.Intelligence)
            });
            y = Group("공격", y + GROUP_GAP, width, new[]
            {
                Stat(StatType.AttackPower), Stat(StatType.AttackSpeed), Stat(StatType.CritChance), Stat(StatType.CritDamage),
                Stat(StatType.DamageBonus), Stat(StatType.PhysicalDamageBonus), Stat(StatType.ElementalDamageBonus)
            });
            y = Group("방어", y + GROUP_GAP, width, new[]
            {
                Stat(StatType.Armor), Stat(StatType.ElementalResistance), Stat(StatType.DodgeChance), Stat(StatType.DamageReduction),
                Custom("물리 피해 감소", () => _stats != null ? StatText.Value(StatType.DamageReduction, _stats.Sheet.GetMitigation(DamageType.Physical)) : "-")
            });
            y = Group("보조", y + GROUP_GAP, width, new[]
            {
                Stat(StatType.MoveSpeed), Stat(StatType.CooldownReduction), Stat(StatType.LifeRegen), Stat(StatType.PotionHealing)
            });
            y = Group("자원", y + GROUP_GAP, width, new[]
            {
                Custom("최대 체력", () => _health != null ? $"{Mathf.CeilToInt(_health.Max)}" : "-", StatType.MaxHealth),
                Custom("최대 기력", () => FindResource() is IResourceGauge r ? $"{Mathf.CeilToInt(r.Max)}" : "-"),
                Custom("최대 신앙", () => FindJobGauge() is IJobGauge g ? $"{Mathf.CeilToInt(g.Max)}" : "-"),
                Stat(StatType.ResourceCostReduction, "기력 소모 감소")
            });
            UiKit.PlaceTopLeft(_content, Vector2.zero, new Vector2(width, y + 8f));

            _scrollTrack = UiKit.Box("ScrollTrack", parent, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.25f)).rectTransform;
            UiKit.PlaceTopLeft(_scrollTrack, new Vector2(position.x + width + 10f, position.y), new Vector2(4f, size.y));
            _scrollHandle = UiKit.Box("ScrollHandle", _scrollTrack, UiTheme.WithAlpha(UiTheme.P.Accent, 0.8f)).rectTransform;
        }

        public void ResetScroll()
        {
            _scrollTarget = _scroll = 0f;
        }

        /// <summary>열려 있는 동안 매 프레임: 스크롤 + 값 갱신</summary>
        public void Tick()
        {
            UpdateScroll();
            Refresh();
        }

        private Entry Stat(StatType stat, string name = null)
        {
            return new Entry { Name = name ?? StatText.Name(stat), Stat = stat };
        }

        private static Entry Custom(string name, Func<string> value, StatType? bonusStat = null)
        {
            return new Entry { Name = name, Value = value, Stat = bonusStat };
        }

        private float Group(string title, float y, float width, Entry[] entries)
        {
            Text header = UiKit.Title(title, _content, 17, UiTheme.P.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(header.rectTransform, new Vector2(0f, y), new Vector2(width, 28f));
            header.text = title;
            Image line = UiKit.Box("Line", _content, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.45f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(0f, y + 29f), new Vector2(width, 1f));
            y += HEADER;

            foreach (Entry entry in entries)
            {
                entry.NameText = UiKit.Label("Name", _content, 15, UiTheme.P.TextDim, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(entry.NameText.rectTransform, new Vector2(8f, y), new Vector2(width - 120f, ROW));
                entry.NameText.text = entry.Name;
                entry.ValueText = UiKit.Label("Value", _content, 16, UiTheme.P.Text, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.PlaceTopLeft(entry.ValueText.rectTransform, new Vector2(width - 108f, y), new Vector2(100f, ROW));

                if (_bonus != null && entry.Stat.HasValue)
                {
                    entry.BonusText = UiKit.Label("Bonus", _content, 12, new Color(0.37f, 0.7f, 0.42f), TextAnchor.MiddleRight, FontStyle.Bold);
                    UiKit.PlaceTopLeft(entry.BonusText.rectTransform, new Vector2(width - 172f, y), new Vector2(60f, ROW));
                }

                _entries.Add(entry);
                y += ROW;
            }

            return y;
        }

        private float MaxScroll => Mathf.Max(0f, _content.sizeDelta.y - _viewport.rect.height);

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

        private void Refresh()
        {
            UiPalette p = UiTheme.P;
            StatSheet sheet = _stats != null ? _stats.Sheet : null;

            foreach (Entry entry in _entries)
            {
                if (entry.Value != null)
                {
                    entry.ValueText.text = entry.Value();
                }
                else if (entry.Stat.HasValue)
                {
                    StatType stat = entry.Stat.Value;
                    bool main = sheet != null && sheet.MainStat.HasValue && sheet.MainStat.Value == stat;
                    entry.NameText.text = main ? $"{entry.Name} (주)" : entry.Name;
                    entry.NameText.color = main ? p.Title : p.TextDim;
                    entry.ValueText.text = sheet != null ? StatText.Value(stat, sheet.Get(stat)) : "-";
                }

                if (entry.BonusText != null)
                {
                    float bonus = _bonus(entry.Stat.Value);
                    entry.BonusText.text = Mathf.Abs(bonus) > 0.0001f ? StatText.Signed(entry.Stat.Value, bonus) : string.Empty;
                }
            }
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
    }
}
