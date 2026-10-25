using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 로딩 화면 (10/26, Figma 13-A): 세계관 소개가 끝나면 나타나 월드 씬을 비동기로 불러옵니다.
    /// - 배경: 타이틀과 같은 밤하늘 → 지평선 금빛 + 떠오르는 빛 입자 + 가장자리 어둡게
    /// - 가운데: "첫 번째 권역" / 천 계 / 금색 선 / 부제
    /// - 왼쪽 아래: TIP 1개 (무작위) · 아래: 진행 문구 + % + 금색 진행 막대(끝에 빛 점) · 오른쪽 아래: 회전 인장
    /// 흐름: 페이드 인 → 불러오기(실제 진행률) → 100% 후 잠시 유지 → 씬 전환 → 월드 위에서 페이드 아웃 → 스스로 삭제.
    /// 씬이 바뀌어도 남도록 DontDestroyOnLoad. LoadingScreen.Begin("World")로 시작합니다.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const float WIDTH = 1920f;
        private const float HEIGHT = 1080f;
        private const float MARGIN = 96f;
        private const float BAR_WIDTH = WIDTH - MARGIN * 2f;
        private const float BAR_HEIGHT = 14f;
        private const float FADE_IN = 0.6f;
        private const float HOLD = 0.6f;
        private const float FADE_OUT = 0.8f;
        private const float MIN_SHOW = 1.8f;     // 너무 빨리 끝나도 화면을 읽을 시간
        private const int MOTE_COUNT = 40;

        private static readonly (string title, string body)[] TIPS =
        {
            ("귀환 포털", "T 키를 누르면 1.5초 뒤 마을로 돌아가는 포털이 열립니다.\n보스와 싸우는 동안에는 열 수 없습니다."),
            ("웨이포인트", "웨이포인트 위에 서면 발견한 곳으로 순간 이동할 수 있습니다.\n쓰러지면 가장 가까운 웨이포인트에서 다시 일어납니다."),
            ("지도", "M 키는 반투명 지도, Tab 키는 전체 지도를 엽니다.\n지도에서 마우스 휠로 확대·축소할 수 있습니다."),
            ("이랑진군", "체력이 70%, 35% 아래로 떨어지면 새로운 패턴이 열립니다.\n바닥의 경고 범위를 보고 피하세요."),
            ("거래", "마을 상인을 클릭하면 대화창이 열립니다.\n숫자 키로 선택지를 고를 수 있습니다."),
        };

        private string _scene;
        private CanvasGroup _group;
        private RectTransform _fill;
        private RectTransform _spark;
        private Text _percent;
        private RectTransform _seal;
        private RectTransform[] _motes;
        private float[] _moteSpeed;

        /// <summary>로딩 화면을 띄우고 씬을 불러옴</summary>
        public static LoadingScreen Begin(string scene)
        {
            var go = new GameObject("LoadingScreen");
            DontDestroyOnLoad(go);
            LoadingScreen screen = go.AddComponent<LoadingScreen>();
            screen._scene = scene;
            return screen;
        }

        private void Start()
        {
            Build();
            StartCoroutine(Run());
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _seal.Rotate(0f, 0f, -120f * dt);

            for (int i = 0; i < _motes.Length; i++)
            {
                Vector2 pos = _motes[i].anchoredPosition;
                pos.y += _moteSpeed[i] * dt;
                pos.x += Mathf.Sin(Time.unscaledTime * 0.6f + i) * 6f * dt;

                if (pos.y > HEIGHT + 20f)
                {
                    pos = new Vector2(Random.Range(0f, WIDTH), -20f);
                }

                _motes[i].anchoredPosition = pos;
            }
        }

        // ------------------------------------------------------------------
        // 흐름
        // ------------------------------------------------------------------

        private IEnumerator Run()
        {
            float started = Time.unscaledTime;
            yield return Fade(0f, 1f, FADE_IN);

            AsyncOperation load = SceneManager.LoadSceneAsync(_scene);

            if (load == null)
            {
                Debug.LogError($"LoadingScreen: '{_scene}' 씬을 불러오지 못했습니다. Build Profiles 씬 목록을 확인하세요 (메뉴: BrokenSovereign/Scenes/빌드 씬 순서 맞추기).", this);
                yield return Fade(1f, 0f, FADE_OUT);
                Destroy(gameObject);
                yield break;
            }

            load.allowSceneActivation = false;
            float shown = 0f;

            // 불러오기 0~90% (Unity는 활성화 전까지 0.9에서 멈춤) → 표시 0~100%.
            // 표시 = min(실제 진행, 최소 표시 시간 경과율)을 부드럽게 따라감 (너무 빨리 끝나도 화면을 읽을 시간)
            while (load.progress < 0.9f || shown < 0.999f)
            {
                float actual = Mathf.Clamp01(load.progress / 0.9f);
                float byTime = Mathf.Clamp01((Time.unscaledTime - started) / MIN_SHOW);
                shown = Mathf.MoveTowards(shown, Mathf.Min(actual, byTime), Time.unscaledDeltaTime * 1.5f);
                SetProgress(shown);
                yield return null;
            }

            SetProgress(1f);
            yield return new WaitForSecondsRealtime(HOLD);

            load.allowSceneActivation = true;

            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;   // 월드의 HUD·카메라가 첫 프레임을 그릴 때까지
            yield return Fade(1f, 0f, FADE_OUT);
            Destroy(gameObject);
        }

        private void SetProgress(float value)
        {
            float width = BAR_WIDTH * Mathf.Clamp01(value);
            _fill.sizeDelta = new Vector2(width, BAR_HEIGHT);
            _spark.anchoredPosition = new Vector2(width, 0f);
            _percent.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private IEnumerator Fade(float from, float to, float time)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / time)
            {
                _group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            _group.alpha = to;
        }

        // ------------------------------------------------------------------
        // 화면 구성 (Figma 13-A)
        // ------------------------------------------------------------------

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(WIDTH, HEIGHT);
            scaler.matchWidthOrHeight = 0.5f;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            RectTransform root = (RectTransform)transform;
            UiPalette p = UiTheme.P;

            // 배경 (불투명 검정 바탕 위 하늘)
            Image back = UiKit.Box("Back", root, Color.black);
            HudGraphics.Stretch(back.rectTransform);
            Image sky = HudGraphics.CreateRect("Sky", root).gameObject.AddComponent<Image>();
            sky.sprite = TitleScreen.MakeSky();
            sky.raycastTarget = false;
            HudGraphics.Stretch(sky.rectTransform);

            _motes = new RectTransform[MOTE_COUNT];
            _moteSpeed = new float[MOTE_COUNT];

            for (int i = 0; i < MOTE_COUNT; i++)
            {
                Image mote = HudGraphics.CreateRect("Mote" + i, root).gameObject.AddComponent<Image>();
                mote.sprite = HudGraphics.CircleSprite;
                mote.raycastTarget = false;
                float size = Random.Range(3f, 9f);
                mote.color = new Color(1f, 0.84f, 0.54f, Random.Range(0.15f, 0.55f));
                UiKit.Place(mote.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f),
                    new Vector2(Random.Range(0f, WIDTH), Random.Range(0f, HEIGHT)), new Vector2(size, size));
                _motes[i] = mote.rectTransform;
                _moteSpeed[i] = Random.Range(10f, 34f);
            }

            Image vignette = HudGraphics.CreateRect("Vignette", root).gameObject.AddComponent<Image>();
            vignette.sprite = TitleScreen.MakeVignette();
            vignette.raycastTarget = false;
            HudGraphics.Stretch(vignette.rectTransform);

            BuildRegionTitle(root, p);
            BuildTip(root, p);
            BuildProgress(root, p);
            BuildSeal(root, p);
        }

        // 가운데: 첫 번째 권역 / 천 계 / 금색 선 / 부제 (Figma: 위쪽 300px부터)
        private static void BuildRegionTitle(RectTransform root, UiPalette p)
        {
            Text label = UiKit.Label("Region", root, 22, p.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.text = "첫  번 째   권 역";
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(800f, 32f));

            Text name = UiKit.Title("Name", root, 120, p.Title, TextAnchor.MiddleCenter);
            name.text = "천  계";
            UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(1000f, 160f));
            Outline glow = name.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(1f, 0.6f, 0.24f, 0.4f);
            glow.effectDistance = new Vector2(3f, -3f);

            Image line = UiKit.Box("Line", root, UiTheme.WithAlpha(p.Accent, 0.7f));
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -512f), new Vector2(620f, 2f));

            Text sub = UiKit.Title("Sub", root, 26, p.Text, TextAnchor.MiddleCenter);
            sub.text = "옥황상제의 신역 — 빛을 잃은 하늘 아래 갈라진 석단";
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(1200f, 44f));
        }

        // 왼쪽 아래 TIP 상자 (무작위 1개)
        private static void BuildTip(RectTransform root, UiPalette p)
        {
            (string title, string body) tip = TIPS[Random.Range(0, TIPS.Length)];
            Image box = UiKit.Box("Tip", root, UiTheme.WithAlpha(p.PlateBottom, 0.82f));
            RectTransform rect = box.rectTransform;
            UiKit.Place(rect, Vector2.zero, Vector2.zero, new Vector2(MARGIN, 186f), new Vector2(700f, 136f));
            Outline stroke = box.gameObject.AddComponent<Outline>();
            stroke.effectColor = UiTheme.WithAlpha(p.PlateStroke, 0.9f);
            stroke.effectDistance = new Vector2(1.5f, -1.5f);

            Image badge = UiKit.Box("Badge", rect, p.Accent);
            UiKit.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(44f, 24f));
            Text badgeText = UiKit.Label("Text", badge.rectTransform, 14, p.PlateBottom, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(badgeText.rectTransform);
            badgeText.text = "TIP";
            badgeText.GetComponent<Shadow>().enabled = false;

            Text title = UiKit.Label("Title", rect, 20, p.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, -20f), new Vector2(560f, 28f));
            title.text = tip.title;

            Text body = UiKit.Label("Body", rect, 18, p.Text, TextAnchor.UpperLeft);
            UiKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -58f), new Vector2(640f, 70f));
            body.lineSpacing = 1.25f;
            body.text = tip.body;
        }

        // 아래: 진행 문구 + % + 진행 막대 (금색 그라데이션) + 빛 점
        private void BuildProgress(RectTransform root, UiPalette p)
        {
            Text status = UiKit.Label("Status", root, 20, p.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            status.text = "신역으로 향하는 중…";
            UiKit.Place(status.rectTransform, Vector2.zero, Vector2.zero, new Vector2(MARGIN, 104f), new Vector2(600f, 30f));

            _percent = UiKit.Label("Percent", root, 20, p.Title, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Place(_percent.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-MARGIN, 104f), new Vector2(200f, 30f));

            Image track = UiKit.Box("Track", root, p.Inset);
            RectTransform trackRect = track.rectTransform;
            UiKit.Place(trackRect, Vector2.zero, Vector2.zero, new Vector2(MARGIN, 80f), new Vector2(BAR_WIDTH, BAR_HEIGHT));
            Outline trackStroke = track.gameObject.AddComponent<Outline>();
            trackStroke.effectColor = UiTheme.WithAlpha(p.PlateStroke, 0.8f);
            trackStroke.effectDistance = new Vector2(1f, -1f);

            Image fill = HudGraphics.CreateRect("Fill", trackRect).gameObject.AddComponent<Image>();
            fill.sprite = MakeBarGradient();
            fill.raycastTarget = false;
            _fill = fill.rectTransform;
            UiKit.Place(_fill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, BAR_HEIGHT));

            RectTransform spark = HudGraphics.CreateRect("Spark", trackRect);
            UiKit.Place(spark, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Image halo = HudGraphics.CreateRect("Halo", spark).gameObject.AddComponent<Image>();
            halo.sprite = HudGraphics.CircleSprite;
            halo.color = new Color(1f, 0.85f, 0.5f, 0.35f);
            halo.raycastTarget = false;
            UiKit.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            Image dot = HudGraphics.CreateRect("Dot", spark).gameObject.AddComponent<Image>();
            dot.sprite = HudGraphics.CircleSprite;
            dot.color = p.CrackInner;
            dot.raycastTarget = false;
            UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            _spark = spark;
            SetProgress(0f);
        }

        // 오른쪽 아래 회전 인장: 3/4 원호 + 가운데 마름모
        private void BuildSeal(RectTransform root, UiPalette p)
        {
            _seal = HudGraphics.CreateRect("Seal", root);
            UiKit.Place(_seal, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-MARGIN - 42f, 240f), new Vector2(84f, 84f));
            Image ring = HudGraphics.CreateRect("Ring", _seal).gameObject.AddComponent<Image>();
            ring.sprite = MakeArc();
            ring.color = UiTheme.WithAlpha(p.Accent, 0.9f);
            ring.raycastTarget = false;
            HudGraphics.Stretch(ring.rectTransform);

            Image diamond = UiKit.Box("Diamond", _seal, p.Title);
            UiKit.Place(diamond.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        // ------------------------------------------------------------------
        // 그림 (코드 생성)
        // ------------------------------------------------------------------

        private static Sprite MakeBarGradient()
        {
            const int w = 64;
            var texture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { name = "LoadingBar", wrapMode = TextureWrapMode.Clamp };
            Color from = new Color(0.69f, 0.48f, 0.18f);
            Color to = new Color(1f, 0.835f, 0.54f);

            for (int x = 0; x < w; x++)
            {
                texture.SetPixel(x, 0, Color.Lerp(from, to, x / (w - 1f)));
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, w, 1f), new Vector2(0.5f, 0.5f));
        }

        // 두께 3.5% 원호 270° (흰색, Image.color로 색 지정)
        private static Sprite MakeArc()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LoadingArc", wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.47f) / 0.022f);
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;   // -180 ~ 180
                    bool gap = angle > 0f && angle < 90f;                  // 오른쪽 위 1/4 비움
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, gap ? 0f : ring));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
