using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>UI 테마 (10/21 "부서진 격" 시안). Figma: BrokenSovereign UI — Dark Fantasy</summary>
    public enum UiThemeId
    {
        /// <summary>시안 B: 어두운 금속판 + 균열 사이로 새는 빛 (기본)</summary>
        SovereignIron,

        /// <summary>시안 A′: 천계 백옥(한 톤 어둡게) + 금빛 균열 (보관용)</summary>
        CelestialJade
    }

    /// <summary>테마 색 묶음. 화면 코드는 색을 직접 쓰지 않고 UiTheme.P를 읽습니다.</summary>
    public sealed class UiPalette
    {
        public Color PlateTop, PlateBottom, PlateStroke, InnerLine, Inset, InsetStroke;
        public Color TileTop, TileBottom, Icon;
        public Color Text, TextDim, TextDisabled, Title, Accent;
        public Color Hp, HpDark, Trail, Resource, ResourceDark, Xp, Shield, Crystal, Poise, Stagger;
        public Color CrackOuter, CrackInner;
        public bool CrackGlow;
        public Color OnScene, SceneShadow, Status, Damage, Crit, Taken, Danger, Warning, Buff;
        public Color EquipTop, EquipBottom, EquipText, Dim, RowA, RowB, Hover;
    }

    /// <summary>
    /// 현재 UI 테마와 서체. HudView가 Awake에서 Apply로 정합니다 (기본 B안).
    /// 스프라이트(판·칸·막대·인장)는 HudGraphics가 테마 색으로 만들고, 테마가 바뀌면 다시 만듭니다.
    /// 서체: 제목 Noto Serif KR Bold(완성형 2350자), 본문 Noto Sans KR Regular·Bold (Resources/UI/Fonts, OFL).
    /// </summary>
    public static class UiTheme
    {
        private static readonly UiPalette IRON = new UiPalette
        {
            PlateTop = Hex("#2E3038"), PlateBottom = Hex("#14151A"), PlateStroke = Hex("#6A6E7A"), InnerLine = new Color(1f, 1f, 1f, 0.08f),
            Inset = Hex("#0C0D11"), InsetStroke = Hex("#3A3D47"),
            TileTop = Hex("#30323A"), TileBottom = Hex("#16171C"), Icon = Hex("#7C808C"),
            Text = Hex("#ECE6D8"), TextDim = Hex("#9AA0AA"), TextDisabled = Hex("#5A5F69"), Title = Hex("#FFD58A"), Accent = Hex("#E9B45A"),
            Hp = Hex("#D23A2C"), HpDark = Hex("#4A0D09"), Trail = Hex("#F0C27A"), Resource = Hex("#F0C25A"), ResourceDark = Hex("#6A4A10"),
            Xp = Hex("#E9B45A"), Shield = Hex("#8FD3FF"), Crystal = Hex("#B48CFF"), Poise = Hex("#E8B04A"), Stagger = Hex("#FF7A2B"),
            CrackOuter = Hex("#FF9A3C"), CrackInner = Hex("#FFF1C8"), CrackGlow = true,
            OnScene = Hex("#ECE6D8"), SceneShadow = new Color(0f, 0f, 0f, 0.9f), Status = Hex("#FFA24C"), Damage = Color.white,
            Crit = Hex("#FFD58A"), Taken = Hex("#FF5A4A"), Danger = Hex("#D93A2E"), Warning = Hex("#FF7A6A"), Buff = Hex("#8FD3FF"),
            EquipTop = Hex("#7A5418"), EquipBottom = Hex("#3A2608"), EquipText = Hex("#FFE7B0"), Dim = new Color(0f, 0f, 0f, 0.6f),
            RowA = new Color(0.047f, 0.051f, 0.067f, 0.85f), RowB = new Color(0.047f, 0.051f, 0.067f, 0.55f), Hover = new Color(1f, 0.78f, 0.45f, 0.18f)
        };

        private static readonly UiPalette JADE = new UiPalette
        {
            PlateTop = Hex("#D3CBB6"), PlateBottom = Hex("#A0957A"), PlateStroke = Hex("#86662A"), InnerLine = new Color(1f, 1f, 1f, 0.6f),
            Inset = Hex("#B8AE94"), InsetStroke = Hex("#877A5A"),
            TileTop = Hex("#DDD5C1"), TileBottom = Hex("#ACA187"), Icon = Hex("#86662A"),
            Text = Hex("#2B2620"), TextDim = Hex("#5E5547"), TextDisabled = Hex("#8A806E"), Title = Hex("#6B4A16"), Accent = Hex("#2F7D6B"),
            Hp = Hex("#B5352A"), HpDark = Hex("#6A1A13"), Trail = Hex("#F0C27A"), Resource = Hex("#D9A93A"), ResourceDark = Hex("#7A5A12"),
            Xp = Hex("#2F7D6B"), Shield = Hex("#5FA9C9"), Crystal = Hex("#7A55C9"), Poise = Hex("#D9A93A"), Stagger = Hex("#E0602A"),
            CrackOuter = Hex("#7A5520"), CrackInner = Hex("#F6D27A"), CrackGlow = false,
            OnScene = Hex("#2B2620"), SceneShadow = new Color(1f, 1f, 1f, 0.7f), Status = Hex("#B4541A"), Damage = Hex("#2B2620"),
            Crit = Hex("#A8701A"), Taken = Hex("#B5352A"), Danger = Hex("#B5352A"), Warning = Hex("#B5352A"), Buff = Hex("#2F7D6B"),
            EquipTop = Hex("#2F7D6B"), EquipBottom = Hex("#1E5446"), EquipText = Color.white, Dim = new Color(0.83f, 0.8f, 0.71f, 0.6f),
            RowA = new Color(0.72f, 0.68f, 0.58f, 0.7f), RowB = new Color(0.72f, 0.68f, 0.58f, 0.35f), Hover = new Color(0.18f, 0.49f, 0.42f, 0.2f)
        };

        private const string FONT_PATH = "UI/Fonts/";
        private static Font _title;
        private static Font _body;
        private static Font _bold;

        public static UiThemeId Current { get; private set; } = UiThemeId.SovereignIron;

        /// <summary>현재 테마 색</summary>
        public static UiPalette P => Current == UiThemeId.CelestialJade ? JADE : IRON;

        /// <summary>테마가 바뀌면 HudGraphics가 스프라이트를 다시 만듦</summary>
        public static int Version { get; private set; }

        public static void Apply(UiThemeId theme)
        {
            if (theme == Current)
            {
                return;
            }

            Current = theme;
            Version++;
        }

        /// <summary>제목·이름 (세리프)</summary>
        public static Font TitleFont => _title != null ? _title : _title = LoadFont("NotoSerifKR-Bold");

        /// <summary>본문</summary>
        public static Font BodyFont => _body != null ? _body : _body = LoadFont("NotoSansKR-Regular");

        /// <summary>굵은 본문·숫자</summary>
        public static Font BoldFont => _bold != null ? _bold : _bold = LoadFont("NotoSansKR-Bold");

        /// <summary>
        /// 굵게 표시할 글자는 Bold 서체로 바꾸고 fontStyle을 Normal로 (가짜 굵기 방지).
        /// 제목 서체를 쓰는 글자는 그대로 둠.
        /// </summary>
        public static void ApplyBody(Text text, bool bold)
        {
            text.font = bold ? BoldFont : BodyFont;
            text.fontStyle = FontStyle.Normal;
        }

        public static void ApplyTitle(Text text)
        {
            text.font = TitleFont;
            text.fontStyle = FontStyle.Normal;
        }

        private static Font LoadFont(string fileName)
        {
            Font font = Resources.Load<Font>(FONT_PATH + fileName);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
