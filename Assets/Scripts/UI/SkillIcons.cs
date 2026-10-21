using System.Collections.Generic;
using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 기술 아이콘 (10/21 기술 창 개편). 기술 ID별 도형을 코드로 그린 흰 스프라이트 (Image.color로 물들임).
    /// 모르는 기술은 기본 문양(UiSprites.Rune). HUD 기술 칸·기술 창 공용.
    /// </summary>
    public static class SkillIcons
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Get(SkillDefinition skill)
        {
            if (skill == null)
            {
                return UiSprites.Rune;
            }

            if (_cache.TryGetValue(skill.SkillId, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            System.Func<float, float, bool> shape = Shape(skill.SkillId);

            if (shape == null)
            {
                return UiSprites.Rune;
            }

            sprite = UiSprites.MakeSuper("SkillIcon_" + skill.SkillId, 64, (u, v) => shape(u * 32f, (1f - v) * 32f) ? Color.white : Color.clear);
            _cache[skill.SkillId] = sprite;
            return sprite;
        }

        private static System.Func<float, float, bool> Shape(string id)
        {
            switch (id)
            {
                case "strike":
                    // 사선 베기 + 불꽃 두 점
                    return (x, y) => Poly(x, y, 4f, 24f, 22f, 6f, 26f, 10f, 8f, 28f) || Dist(x, y, 25f, 22f) < 2.2f || Dist(x, y, 9f, 7f) < 2.2f;
                case "radiant_smite":
                    // 햇살: 가운데 원 + 광선 8줄
                    return (x, y) => Dist(x, y, 16f, 16f) < 5.5f || Ray(x, y, 8, 7f, 14.5f, 1.6f);
                case "advancing_oath":
                    // 돌진 화살표 + 속도선
                    return (x, y) => Poly(x, y, 10f, 13f, 19f, 13f, 19f, 8f, 28f, 16f, 19f, 24f, 19f, 19f, 10f, 19f) ||
                        (y > 9f && y < 11f && x > 3f && x < 11f) || (y > 21f && y < 23f && x > 3f && x < 11f) || (y > 15f && y < 17f && x > 1f && x < 8f);
                case "unyielding_aegis":
                    // 방패 (가운데 홈)
                    return (x, y) => Poly(x, y, 16f, 3f, 27f, 7f, 26f, 18f, 16f, 29f, 6f, 18f, 5f, 7f) && !(x > 14.8f && x < 17.2f && y > 7f && y < 24f);
                case "battle_cry":
                    // 외침: 원 + 퍼지는 호 3개
                    return (x, y) => Dist(x, y, 9f, 16f) < 4f || Arc(x, y, 9f, 16f, 9f, 1.6f, 55f) || Arc(x, y, 9f, 16f, 14f, 1.6f, 50f) ||
                        Arc(x, y, 9f, 16f, 19f, 1.6f, 45f);
                case "crushing_blow":
                    // 내려치는 철퇴 + 충격선
                    return (x, y) => Poly(x, y, 5f, 25f, 18.5f, 11.5f, 21.5f, 14.5f, 8f, 28f) || Poly(x, y, 16f, 9.5f, 22.5f, 3f, 29f, 9.5f, 22.5f, 16f) ||
                        (y > 27f && y < 29f && x > 14f && x < 29f);
                case "heavenly_whirl":
                    // 회오리: 끊긴 고리 두 겹
                    return (x, y) => Arc(x, y, 16f, 16f, 11f, 2f, 140f, 0f) || Arc(x, y, 16f, 16f, 11f, 2f, 140f, 180f) ||
                        Arc(x, y, 16f, 16f, 5.5f, 1.8f, 120f, 90f) || Arc(x, y, 16f, 16f, 5.5f, 1.8f, 120f, 270f);
                case "reach_shift":
                    // 양방향 화살표
                    return (x, y) => (y > 14.5f && y < 17.5f && x > 7f && x < 25f) || Poly(x, y, 3f, 16f, 10f, 10f, 10f, 22f) || Poly(x, y, 29f, 16f, 22f, 10f, 22f, 22f);
                case "sovereign_quake":
                    // 진동: 동심원 3겹
                    return (x, y) => Dist(x, y, 16f, 16f) < 3.5f || Mathf.Abs(Dist(x, y, 16f, 16f) - 8.5f) < 1.5f || Mathf.Abs(Dist(x, y, 16f, 16f) - 13.5f) < 1.5f;
                default:
                    return null;
            }
        }

        private static float Dist(float x, float y, float cx, float cy)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        }

        // 가운데에서 count개 방향 광선 (반지름 from~to, 반폭 half)
        private static bool Ray(float x, float y, int count, float from, float to, float half)
        {
            float dx = x - 16f, dy = y - 16f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);

            if (r < from || r > to)
            {
                return false;
            }

            float angle = Mathf.Atan2(dy, dx);
            float step = Mathf.PI * 2f / count;
            float nearest = Mathf.Round(angle / step) * step;
            float offset = Mathf.Abs(Mathf.Sin(angle - nearest)) * r;
            return offset < half * (1f - (r - from) / (to - from) * 0.5f);
        }

        // 호: 중심(cx,cy), 반지름 radius, 두께 half×2, 가운데 방향 center(도, 0 = 오른쪽)에서 ±spread/2
        private static bool Arc(float x, float y, float cx, float cy, float radius, float half, float spread, float center = 0f)
        {
            float r = Dist(x, y, cx, cy);

            if (Mathf.Abs(r - radius) > half)
            {
                return false;
            }

            float angle = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(angle, center)) < spread * 0.5f;
        }

        private static bool Poly(float x, float y, params float[] p)
        {
            bool inside = false;
            int n = p.Length / 2;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];

                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
