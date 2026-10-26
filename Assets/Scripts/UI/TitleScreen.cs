using System.Collections;
using BrokenSovereign.Sound;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 타이틀 화면 + 세계관 소개 (10/26 인트로).
    /// 1. 타이틀: 밤하늘 그라데이션 · 지평선 금빛 · 떠오르는 빛 입자, 로고(BROKEN SOVEREIGN)와 [시작] [종료]
    /// 2. 세계관: 검은 화면에 4줄이 차례로 페이드 (클릭·Space·Enter = 다음 줄, Esc = 전부 건너뛰기)
    /// 3. 월드 씬 비동기 로드
    /// 다른 화면과 같이 EventSystem 없이 마우스 위치로 판정 (UiKit). Title 씬은 TitleSceneTools 메뉴로 생성합니다.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        private const float REFERENCE_WIDTH = 1920f;
        private const float REFERENCE_HEIGHT = 1080f;
        private const int MOTE_COUNT = 36;

        [SerializeField] private string _worldScene = "World";
        [SerializeField] private float _lineFadeIn = 1.2f;
        [SerializeField] private float _lineHold = 2.6f;
        [SerializeField] private float _lineFadeOut = 0.9f;

        [TextArea]
        [SerializeField] private string[] _storyLines =
        {
            "신들은 영웅들의 격(格)을 거두어 스스로를 키웠다.",
            "격을 빼앗긴 영웅은 이름을 잃고, 필멸로 떨어졌다.",
            "빛을 잃은 신역, 갈라진 하늘 아래 —\n이름 없는 자가 다시 눈을 뜬다.",
            "첫 번째 권역, 천계.\n빼앗긴 격을 되찾아라.",
        };

        private RectTransform _root;
        private CanvasGroup _titleGroup;
        private CanvasGroup _storyGroup;
        private Text _storyText;
        private Text _skipHint;
        private Image _fade;
        private Image _startButton;
        private Image _quitButton;
        private Image _settingsButton;          // 10/26: 시작 아래 [설정]
        private SettingsWindowView _settings;
        private RectTransform[] _motes;
        private float[] _moteSpeed;
        private Text _logo;
        private bool _busy;
        private Image _hovered;

        private void Awake()
        {
            Build();
        }

        private void Start()
        {
            SoundPlayer.PlayMusic(SoundId.BGM_TITLE, 2.5f);
            StartCoroutine(FadeFromBlack());
        }

        private void Update()
        {
            AnimateMotes();
            AnimateLogo();

            if (_busy || (_settings != null && _settings.IsOpen))
            {
                return;
            }

            UpdateButton(_startButton);
            UpdateButton(_settingsButton);
            UpdateButton(_quitButton);
            UpdateHoverSound();
            Keyboard keyboard = Keyboard.current;

            if ((UiKit.Clicked && UiKit.IsHover(_startButton.rectTransform)) ||
                (keyboard != null && keyboard.enterKey.wasPressedThisFrame))
            {
                SoundPlayer.Play(SoundId.UI_START, 1f, 0f);
                SoundPlayer.PlayMusic(null, 2.5f);   // 세계관 소개는 효과음만 (끝나면 필드 음악)
                StartCoroutine(PlayStory());
            }
            else if (UiKit.Clicked && UiKit.IsHover(_settingsButton.rectTransform))
            {
                SoundPlayer.Play(SoundId.UI_CLICK, 1f, 0f);
                _settings.Open();
            }
            else if (UiKit.Clicked && UiKit.IsHover(_quitButton.rectTransform))
            {
                SoundPlayer.Play(SoundId.UI_CLICK, 1f, 0f);
                Quit();
            }
        }

        // ------------------------------------------------------------------
        // 화면 구성
        // ------------------------------------------------------------------

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);
            scaler.matchWidthOrHeight = 0.5f;

            _root = (RectTransform)transform;
            UiPalette p = UiTheme.P;

            // 하늘: 위 짙은 남색 → 아래 지평선 금빛
            Image sky = HudGraphics.CreateRect("Sky", _root).gameObject.AddComponent<Image>();
            sky.sprite = MakeSky();
            sky.raycastTarget = false;
            HudGraphics.Stretch(sky.rectTransform);

            // 떠오르는 빛 입자 (격의 파편)
            _motes = new RectTransform[MOTE_COUNT];
            _moteSpeed = new float[MOTE_COUNT];

            for (int i = 0; i < MOTE_COUNT; i++)
            {
                Image mote = HudGraphics.CreateRect("Mote" + i, _root).gameObject.AddComponent<Image>();
                mote.sprite = HudGraphics.CircleSprite;
                mote.raycastTarget = false;
                float size = Random.Range(3f, 9f);
                mote.color = new Color(1f, 0.82f, 0.5f, Random.Range(0.15f, 0.55f));
                UiKit.Place(mote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                    new Vector2(Random.Range(-REFERENCE_WIDTH * 0.5f, REFERENCE_WIDTH * 0.5f), Random.Range(0f, REFERENCE_HEIGHT)),
                    new Vector2(size, size));
                _motes[i] = mote.rectTransform;
                _moteSpeed[i] = Random.Range(12f, 40f);
            }

            // 가장자리 어둡게
            Image vignette = HudGraphics.CreateRect("Vignette", _root).gameObject.AddComponent<Image>();
            vignette.sprite = MakeVignette();
            vignette.raycastTarget = false;
            HudGraphics.Stretch(vignette.rectTransform);

            // 타이틀 묶음
            RectTransform title = HudGraphics.CreateRect("Title", _root);
            HudGraphics.Stretch(title);
            _titleGroup = title.gameObject.AddComponent<CanvasGroup>();

            _logo = UiKit.Title("Logo", title, 112, p.Title, TextAnchor.MiddleCenter);
            _logo.text = "BROKEN  SOVEREIGN";
            UiKit.Place(_logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1800f, 140f));
            _logo.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.CrackOuter, 0.5f);

            Image lineTop = UiKit.Box("LineTop", title, UiTheme.WithAlpha(p.Accent, 0.7f));
            UiKit.Place(lineTop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(760f, 2f));

            Text tagline = UiKit.Title("Tagline", title, 30, p.Text, TextAnchor.MiddleCenter);
            tagline.text = "격 을   빼 앗 긴   영 웅 의   귀 환";
            UiKit.Place(tagline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 32f), new Vector2(1200f, 44f));   // 10/26: 금색 선과 간격 +8

            _startButton = UiKit.Button("Start", title, "시작", 22);
            UiKit.Place(_startButton.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(300f, 60f));
            // 10/26 (Figma 14-B): 시작 → 설정 → 종료
            _settingsButton = UiKit.Button("Settings", title, "설정", 22);
            UiKit.Place(_settingsButton.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -186f), new Vector2(300f, 60f));
            _quitButton = UiKit.Button("Quit", title, "종료", 22);
            UiKit.Place(_quitButton.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -262f), new Vector2(300f, 60f));

            Text version = UiKit.Label("Version", title, 15, p.TextDim, TextAnchor.LowerRight);
            version.text = "Prototype · Unity 6";
            UiKit.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 20f), new Vector2(400f, 24f));

            // 세계관 묶음 (검은 화면 + 가운데 글)
            RectTransform story = HudGraphics.CreateRect("Story", _root);
            HudGraphics.Stretch(story);
            _storyGroup = story.gameObject.AddComponent<CanvasGroup>();
            Image storyBack = UiKit.Box("Back", story, Color.black);
            HudGraphics.Stretch(storyBack.rectTransform);
            _storyText = UiKit.Title("Line", story, 40, p.Text, TextAnchor.MiddleCenter);
            _storyText.lineSpacing = 1.35f;
            UiKit.Place(_storyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 300f));
            // 10/26: 조작 안내는 오른쪽 위, 크고 굵게
            _skipHint = UiKit.Label("Skip", story, 22, p.Text, TextAnchor.UpperRight, FontStyle.Bold);
            _skipHint.text = "클릭 · Space  다음      Esc  건너뛰기";
            UiKit.Place(_skipHint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -32f), new Vector2(800f, 34f));
            _storyGroup.alpha = 0f;
            story.gameObject.SetActive(false);

            // 전체 페이드 (맨 위)
            // 설정 창 (10/26, Figma 14-A) — 페이드보다 아래
            _settings = SettingsWindowView.Create(_root);
            _settings.HideEscHint();

            _fade = UiKit.Box("Fade", _root, Color.black);
            HudGraphics.Stretch(_fade.rectTransform);
        }

        // 버튼 위에 처음 올라갈 때 한 번
        private void UpdateHoverSound()
        {
            Image hovered = UiKit.IsHover(_startButton.rectTransform) ? _startButton
                : UiKit.IsHover(_settingsButton.rectTransform) ? _settingsButton
                : UiKit.IsHover(_quitButton.rectTransform) ? _quitButton : null;

            if (hovered != null && hovered != _hovered)
            {
                SoundPlayer.Play(SoundId.UI_HOVER, 0.6f, 0.02f);
            }

            _hovered = hovered;
        }

        private static void UpdateButton(Image button)
        {
            bool hover = UiKit.IsHover(button.rectTransform);
            Text label = button.GetComponentInChildren<Text>();
            label.color = hover ? UiTheme.P.Title : UiTheme.P.Text;
            button.color = hover ? Color.white : new Color(0.82f, 0.82f, 0.85f);
            float scale = hover ? 1.04f : 1f;
            button.rectTransform.localScale = Vector3.Lerp(button.rectTransform.localScale, new Vector3(scale, scale, 1f), 0.25f);
        }

        private void AnimateMotes()
        {
            for (int i = 0; i < _motes.Length; i++)
            {
                Vector2 pos = _motes[i].anchoredPosition;
                pos.y += _moteSpeed[i] * Time.unscaledDeltaTime;
                pos.x += Mathf.Sin(Time.unscaledTime * 0.6f + i) * 6f * Time.unscaledDeltaTime;

                if (pos.y > REFERENCE_HEIGHT + 20f)
                {
                    pos = new Vector2(Random.Range(-REFERENCE_WIDTH * 0.5f, REFERENCE_WIDTH * 0.5f), -20f);
                }

                _motes[i].anchoredPosition = pos;
            }
        }

        // 로고가 천천히 숨 쉬듯 밝아졌다 어두워짐
        private void AnimateLogo()
        {
            float t = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 1.2f);
            Color c = UiTheme.P.Title;
            _logo.color = new Color(c.r * t, c.g * t, c.b * t, 1f);
        }

        // ------------------------------------------------------------------
        // 흐름
        // ------------------------------------------------------------------

        private IEnumerator FadeFromBlack()
        {
            _busy = true;
            yield return Fade(_fade, 1f, 0f, 1.2f);
            _fade.gameObject.SetActive(false);
            _busy = false;
        }

        private IEnumerator PlayStory()
        {
            _busy = true;

            // 타이틀 → 검은 화면
            _storyGroup.gameObject.SetActive(true);
            _storyText.text = string.Empty;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1f)
            {
                _storyGroup.alpha = t;
                yield return null;
            }

            _storyGroup.alpha = 1f;
            _titleGroup.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.4f);

            foreach (string line in _storyLines)
            {
                _storyText.text = line;
                SoundPlayer.Play(SoundId.STORY_LINE, 0.8f, 0.02f);
                bool skipAll = false;
                bool next = false;
                float total = _lineFadeIn + _lineHold + _lineFadeOut;

                for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
                {
                    if (EscPressed())
                    {
                        skipAll = true;
                        break;
                    }

                    // 다음 줄: 글자가 다 보인 뒤에는 바로 사라지는 단계로
                    if (!next && NextPressed() && t > 0.2f)
                    {
                        next = true;
                        t = Mathf.Max(t, _lineFadeIn + _lineHold);
                    }

                    float alpha = t < _lineFadeIn ? t / _lineFadeIn
                        : t < _lineFadeIn + _lineHold ? 1f
                        : 1f - (t - _lineFadeIn - _lineHold) / _lineFadeOut;
                    SetStoryAlpha(alpha);
                    yield return null;
                }

                SetStoryAlpha(0f);

                if (skipAll)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            _skipHint.enabled = false;
            yield return LoadWorld();
        }

        // 10/26: 로딩 화면(Figma 13-A)이 월드 씬을 불러오고, 전환 후 월드 위에서 사라짐
        private IEnumerator LoadWorld()
        {
            _storyText.text = string.Empty;
            LoadingScreen.Begin(_worldScene);
            yield break;
        }

        private void SetStoryAlpha(float alpha)
        {
            Color c = _storyText.color;
            _storyText.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
        }

        private static bool EscPressed()
        {
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        private static bool NextPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return UiKit.Clicked || (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));
        }

        private static IEnumerator Fade(Image image, float from, float to, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / time));
                yield return null;
            }

            image.color = new Color(0f, 0f, 0f, to);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        // 그림 (코드 생성)
        // ------------------------------------------------------------------

        internal static Sprite MakeSky()
        {
            const int h = 256;
            var texture = new Texture2D(4, h, TextureFormat.RGBA32, false) { name = "TitleSky", wrapMode = TextureWrapMode.Clamp };
            Color top = new Color(0.03f, 0.035f, 0.06f);
            Color mid = new Color(0.08f, 0.09f, 0.15f);
            Color horizon = new Color(0.32f, 0.22f, 0.12f);

            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);   // 0 = 아래
                Color c = v > 0.45f ? Color.Lerp(mid, top, (v - 0.45f) / 0.55f) : Color.Lerp(horizon, mid, Mathf.SmoothStep(0f, 1f, v / 0.45f));
                float glow = Mathf.Exp(-Mathf.Pow((v - 0.18f) / 0.08f, 2f)) * 0.25f;   // 지평선 금빛 띠
                c += new Color(glow, glow * 0.75f, glow * 0.4f);

                for (int x = 0; x < 4; x++)
                {
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, h), new Vector2(0.5f, 0.5f));
        }

        internal static Sprite MakeVignette()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TitleVignette", wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.707f;
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0.35f, 1f, d) * 0.85f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
