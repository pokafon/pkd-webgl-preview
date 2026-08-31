using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AngerBattle
{
    /// <summary>
    /// 不安戦のYES / NO体験を描画する専用コンポーネント。
    /// 回答を評価せず、選択した方向へ伸びる足跡と入口への没入演出を積み上げる。
    /// </summary>
    public sealed class AnxietyQuestionExperience : MonoBehaviour
    {
        private readonly List<string> answers = new List<string>();
        private const int MaxTrailFootsteps = 40;
        private readonly Image[] footstepImages = new Image[MaxTrailFootsteps];
        private readonly RectTransform[] footstepRects = new RectTransform[MaxTrailFootsteps];
        private readonly List<Vector2> footstepPathCenters = new List<Vector2>();
        private readonly List<float> footstepPathAngles = new List<float>();
        private readonly List<int> footstepPathSegments = new List<int>();

        private Canvas hostCanvas;
        private TMP_FontAsset font;
        private float motionScale = 1f;
        private float entryDuration = 0.38f;
        private float answerPause = 0.42f;
        private float finalHold = 1.15f;
        private float diveDuration = 1.05f;

        private GameObject root;
        private CanvasGroup rootGroup;
        private CanvasGroup contentGroup;
        private Image background;
        private RectTransform stageRect;
        private CanvasGroup stageGroup;
        private TMP_Text eyebrowText;
        private TMP_Text counterText;
        private TMP_Text doubtText;
        private TMP_Text questionShadowA;
        private TMP_Text questionShadowB;
        private TMP_Text questionText;
        private TMP_Text historyText;
        private TMP_Text intrusiveText;
        private TMP_Text footerText;
        private Image questionPanel;
        private RectTransform historyPanel;
        private CanvasGroup historyGroup;
        private Button yesButton;
        private Button noButton;
        private Button yesPuddleButton;
        private Button noPuddleButton;
        private CanvasGroup yesGroup;
        private CanvasGroup noGroup;
        private Image startLeftFootImage;
        private Image startRightFootImage;
        private RectTransform startLeftFootRect;
        private RectTransform startRightFootRect;
        private Sprite leftFootSprite;
        private Sprite rightFootSprite;
        private string pendingAnswer;
        private float animationClock;
        private int currentQuestionIndex;
        private bool currentFootstepCommitted;
        private float footstepCommitPulse;
        private int visibleFootstepCount;
        private EventSystem localEventSystem;
        private AnxietyQuestionWorldVisuals worldVisuals;
        private bool useWorldArt;

        private static readonly Color BackgroundStart = new Color(0.025f, 0.022f, 0.045f, 1f);
        private static readonly Color BackgroundEnd = new Color(0.075f, 0.018f, 0.10f, 1f);
        private static readonly Color ArtTintStart = new Color(0.004f, 0.012f, 0.030f, 0.16f);
        private static readonly Color ArtTintEnd = new Color(0.025f, 0.008f, 0.055f, 0.24f);
        private static readonly Color DiveBlack = new Color(0.002f, 0.002f, 0.006f, 1f);
        private static readonly Color Violet = new Color(0.67f, 0.40f, 0.88f, 1f);
        private static readonly Color SoftWhite = new Color(0.94f, 0.92f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.58f, 0.70f, 1f);

        public void Configure(
            Canvas canvas,
            TMP_FontAsset fontAsset,
            float requestedMotionScale,
            float requestedEntryDuration,
            float requestedAnswerPause,
            float requestedFinalHold,
            Sprite floorSprite,
            Sprite yesGateSprite,
            Sprite noGateSprite,
            Material gateMaterial,
            Sprite requestedLeftFootSprite,
            Sprite requestedRightFootSprite,
            Camera questionCamera,
            float requestedDiveDuration,
            float requestedDiveSize,
            bool rainEnabled,
            float requestedRainRate,
            Material rainMaterial,
            AudioClip runningWetRoadClip,
            float runningWetRoadVolume)
        {
            hostCanvas = canvas;
            font = fontAsset;
            motionScale = Mathf.Max(0f, requestedMotionScale);
            entryDuration = Mathf.Max(0.05f, requestedEntryDuration);
            answerPause = Mathf.Max(0.05f, requestedAnswerPause);
            finalHold = Mathf.Max(0.1f, requestedFinalHold);
            diveDuration = Mathf.Max(0.2f, requestedDiveDuration);
            leftFootSprite = requestedLeftFootSprite;
            rightFootSprite = requestedRightFootSprite;

            if (worldVisuals == null)
            {
                worldVisuals = GetComponent<AnxietyQuestionWorldVisuals>();
                if (worldVisuals == null)
                {
                    worldVisuals = gameObject.AddComponent<AnxietyQuestionWorldVisuals>();
                }
            }
            useWorldArt = worldVisuals.Configure(
                questionCamera != null ? questionCamera : Camera.main,
                floorSprite,
                yesGateSprite,
                noGateSprite,
                gateMaterial,
                fontAsset,
                requestedDiveSize,
                rainEnabled,
                requestedRainRate,
                rainMaterial,
                runningWetRoadClip,
                runningWetRoadVolume);
            EnsureUI();
        }

        public IEnumerator Play(string[] questions, string[] intrusiveLines)
        {
            EnsureUI();
            answers.Clear();
            animationClock = 0f;
            pendingAnswer = null;
            currentQuestionIndex = 0;
            currentFootstepCommitted = false;
            footstepCommitPulse = 0f;
            visibleFootstepCount = 0;
            footstepPathCenters.Clear();
            footstepPathAngles.Clear();
            footstepPathSegments.Clear();

            if (useWorldArt)
            {
                worldVisuals.Show();
            }
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
            contentGroup.alpha = 0f;
            background.color = GetQuestionBackground(0f);
            historyText.text = string.Empty;
            intrusiveText.text = string.Empty;
            doubtText.text = string.Empty;
            footerText.text = "← / Y    YES        NO    N / →";

            int questionCount = questions != null ? questions.Length : 0;
            for (int i = 0; i < questionCount; i++)
            {
                float intensity = questionCount <= 1 ? 1f : (float)i / (questionCount - 1);
                PrepareQuestion(questions[i], intrusiveLines, i, questionCount, intensity);

                if (i == 0)
                {
                    yield return Fade(contentGroup, 0f, 1f, 0.35f);
                }
                else if (useWorldArt)
                {
                    yield return FadeBackground(DiveBlack, GetQuestionBackground(intensity), 0.32f);
                    yield return Fade(contentGroup, 0f, 1f, 0.22f);
                }

                yield return AnimateQuestionIn(intensity);
                yield return WaitForAnswer(intensity);

                answers.Add(pendingAnswer);
                if (!useWorldArt)
                {
                    UpdateHistory(questions, i);
                }
                yield return AnimateAnswerCommit(i, intensity);

                // 1〜4問目は、選んだ入口へ入って暗転した先から次の問いを始める。
                if (useWorldArt && i < questionCount - 1)
                {
                    yield return Fade(contentGroup, 1f, 0f, 0.16f);
                    yield return DiveToBlack(pendingAnswer);
                    worldVisuals.ResetView();
                }
            }

            if (!useWorldArt && questionCount > 0)
            {
                eyebrowText.text = useWorldArt ? string.Empty : "RECORD";
                counterText.text = useWorldArt ? string.Empty : $"{questionCount:00} / {questionCount:00}";
                doubtText.text = useWorldArt ? string.Empty : "答えは、返ってこない。";
                questionText.text = "今まで選んだ答えは、本当に正しかった？";
                questionShadowA.text = questionText.text;
                questionShadowB.text = questionText.text;
                yesGroup.alpha = 0f;
                noGroup.alpha = 0f;
                stageGroup.alpha = 1f;
                footerText.text = string.Empty;
                historyGroup.alpha = 1f;
                currentQuestionIndex = questionCount;
                currentFootstepCommitted = true;

                float elapsed = 0f;
                while (elapsed < finalHold)
                {
                    elapsed += Time.unscaledDeltaTime;
                    AnimateAmbient(1f);
                    yield return null;
                }
            }

            yesButton.interactable = false;
            noButton.interactable = false;
            if (yesPuddleButton != null)
            {
                yesPuddleButton.interactable = false;
                noPuddleButton.interactable = false;
            }
            yield return Fade(contentGroup, 1f, 0f, 0.20f);

            if (useWorldArt && answers.Count > 0)
            {
                yield return DiveToBlack(answers[answers.Count - 1]);
                worldVisuals.Hide();
            }
            else
            {
                yield return FadeBackground(background.color, DiveBlack, 0.24f);
            }
        }

        private IEnumerator DiveToBlack(string answer)
        {
            Color startColor = background.color;
            yield return worldVisuals.PlayDive(answer, diveDuration, progress =>
            {
                // 入口の暗部が画面を覆い始めてから黒を重ね、画像の闇から連続して暗転させる。
                float blackout = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, progress));
                background.color = Color.Lerp(startColor, DiveBlack, blackout);
            });
            worldVisuals.StopWalkingAudio();
            background.color = DiveBlack;
        }

        /// <summary>
        /// 質問終了後も残していた不透明な暗幕を開き、準備済みの戦闘画面を初めて見せる。
        /// </summary>
        public IEnumerator RevealBattle(float duration = 0.45f)
        {
            if (root == null || !root.activeSelf)
            {
                yield break;
            }

            rootGroup.interactable = false;
            yield return Fade(rootGroup, 1f, 0f, Mathf.Max(0.05f, duration));
            root.SetActive(false);
            rootGroup.alpha = 1f;
            contentGroup.alpha = 0f;
        }

        /// <summary>中断・再入場時に質問UIと入力遮蔽を即座に片づける。</summary>
        public void HideImmediately()
        {
            if (worldVisuals != null)
            {
                worldVisuals.Hide();
            }
            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            rootGroup.alpha = 1f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            contentGroup.alpha = 0f;
            pendingAnswer = null;
        }

        private Color GetQuestionBackground(float intensity)
        {
            return useWorldArt
                ? Color.Lerp(ArtTintStart, ArtTintEnd, Mathf.Clamp01(intensity))
                : Color.Lerp(BackgroundStart, BackgroundEnd, Mathf.Clamp01(intensity));
        }

        private void PrepareQuestion(string question, string[] intrusiveLines, int index, int total, float intensity)
        {
            pendingAnswer = null;
            currentQuestionIndex = index;
            currentFootstepCommitted = false;
            footstepCommitPulse = 0f;
            questionText.text = question;
            questionShadowA.text = question;
            questionShadowB.text = question;
            eyebrowText.text = useWorldArt ? string.Empty : "QUESTION";
            counterText.text = useWorldArt ? string.Empty : $"{index + 1:00} / {total:00}";
            doubtText.text = useWorldArt ? string.Empty : BuildDoubtLine(index);
            intrusiveText.text = useWorldArt ? string.Empty : BuildIntrusiveText(intrusiveLines, index);
            intrusiveText.alpha = useWorldArt ? 0f : Mathf.Lerp(0f, 0.72f, intensity);
            historyGroup.alpha = useWorldArt ? 0f : (index == 0 ? 0f : Mathf.Lerp(0.48f, 0.88f, intensity));
            footerText.text = useWorldArt && index == 0 ? "Y / N   またはクリック" : string.Empty;

            yesButton.interactable = true;
            noButton.interactable = true;
            if (yesPuddleButton != null)
            {
                yesPuddleButton.interactable = true;
                noPuddleButton.interactable = true;
            }
            yesGroup.alpha = 1f;
            noGroup.alpha = 1f;
            stageGroup.alpha = 0f;
            stageRect.localScale = new Vector3(0.985f, 0.985f, 1f);

            if (EventSystem.current != null)
            {
                // 同じボタンが選択されたままだと2問目以降はOnSelectが再発火しない。
                // 一度選択を外してからYESへ戻し、プレートと水面の反応を毎問復帰させる。
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(yesButton.gameObject);
            }
        }

        private IEnumerator AnimateQuestionIn(float intensity)
        {
            float elapsed = 0f;
            while (elapsed < entryDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutCubic(elapsed / entryDuration);
                stageGroup.alpha = t;
                stageRect.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, t);
                AnimateAmbient(intensity);
                yield return null;
            }
            stageGroup.alpha = 1f;
            stageRect.localScale = Vector3.one;
        }

        private IEnumerator WaitForAnswer(float intensity)
        {
            while (pendingAnswer == null)
            {
                if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    Choose("YES");
                }
                else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    Choose("NO");
                }

                AnimateAmbient(intensity);
                yield return null;
            }
        }

        private IEnumerator AnimateAnswerCommit(int answerIndex, float intensity)
        {
            yesButton.interactable = false;
            noButton.interactable = false;
            if (yesPuddleButton != null)
            {
                yesPuddleButton.interactable = false;
                noPuddleButton.interactable = false;
            }
            currentFootstepCommitted = true;
            footstepCommitPulse = 1f;
            int revealStart = visibleFootstepCount;
            AppendFootstepSegment(pendingAnswer, answerIndex);
            int revealTarget = footstepPathCenters.Count;

            CanvasGroup selected = pendingAnswer == "YES" ? yesGroup : noGroup;
            CanvasGroup rejected = pendingAnswer == "YES" ? noGroup : yesGroup;
            float elapsed = 0f;
            while (elapsed < answerPause)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / answerPause);
                int segmentLength = revealTarget - revealStart;
                visibleFootstepCount = Mathf.Min(
                    revealTarget,
                    revealStart + Mathf.CeilToInt(segmentLength * EaseOutCubic(t)));
                selected.alpha = 0.72f + Mathf.Sin(t * Mathf.PI) * 0.28f;
                rejected.alpha = Mathf.Lerp(1f, 0.16f, t);
                stageGroup.alpha = Mathf.Lerp(1f, 0.56f, t);
                if (useWorldArt)
                {
                    worldVisuals.SetAnswerEmphasis(pendingAnswer, t);
                }
                AnimateAmbient(Mathf.Min(1f, intensity + 0.15f));
                yield return null;
            }
            visibleFootstepCount = revealTarget;

            if (!useWorldArt && answerIndex >= 1)
            {
                historyGroup.alpha = Mathf.Min(1f, historyGroup.alpha + 0.12f);
            }
        }

        private void AnimateAmbient(float intensity)
        {
            animationClock += Time.unscaledDeltaTime;
            footstepCommitPulse = Mathf.MoveTowards(footstepCommitPulse, 0f, Time.unscaledDeltaTime * 2.8f);
            background.color = GetQuestionBackground(intensity);

            float driftX = Mathf.Sin(animationClock * 1.7f) * 2.2f;
            float driftY = Mathf.Sin(animationClock * 2.3f + 1.1f) * 1.4f;
            float noiseX = (Mathf.PerlinNoise(animationClock * 7f, 0.31f) - 0.5f) * 5f;
            float noiseY = (Mathf.PerlinNoise(0.73f, animationClock * 8f) - 0.5f) * 3f;
            stageRect.anchoredPosition = useWorldArt
                ? Vector2.zero
                : new Vector2(driftX + noiseX * intensity, driftY + noiseY * intensity)
                    * motionScale * intensity;

            if (!useWorldArt)
            {
                float echo = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, intensity));
                questionShadowA.alpha = echo * (0.11f + Mathf.Sin(animationClock * 3.1f) * 0.025f);
                questionShadowB.alpha = echo * (0.09f + Mathf.Sin(animationClock * 2.7f + 2f) * 0.02f);
                questionShadowA.rectTransform.anchoredPosition = new Vector2(-2.5f, 0.7f) * echo * motionScale;
                questionShadowB.rectTransform.anchoredPosition = new Vector2(2.5f, -0.7f) * echo * motionScale;
            }

            AnimateFootsteps();

            if (!useWorldArt)
            {
                intrusiveText.alpha = Mathf.Clamp01(intensity * 0.58f + Mathf.Sin(animationClock * 1.9f) * 0.06f);
                historyPanel.anchoredPosition = new Vector2(
                    Mathf.Sin(animationClock * 0.9f) * intensity * motionScale,
                    Mathf.Sin(animationClock * 1.2f + 0.5f) * intensity * 0.6f * motionScale);
            }
        }

        private void AnimateFootsteps()
        {
            if (!useWorldArt)
            {
                return;
            }

            float starterPulse = 0.5f + Mathf.Sin(animationClock * 1.8f) * 0.5f;
            float starterAlpha = footstepPathCenters.Count == 0
                ? Mathf.Lerp(0.34f, 0.44f, starterPulse)
                : 0.16f;
            Color starterColor = new Color(0.56f, 0.67f, 0.78f, starterAlpha);
            if (startLeftFootImage != null)
            {
                startLeftFootImage.color = starterColor;
                startRightFootImage.color = starterColor;
                startLeftFootRect.localScale = Vector3.one;
                startRightFootRect.localScale = Vector3.one;
            }

            for (int i = 0; i < footstepImages.Length; i++)
            {
                Image footstep = footstepImages[i];
                RectTransform footRect = footstepRects[i];
                if (footstep == null || footRect == null)
                {
                    continue;
                }

                bool visible = i < visibleFootstepCount && i < footstepPathCenters.Count;
                footstep.gameObject.SetActive(visible && footstep.sprite != null);
                if (!visible)
                {
                    continue;
                }

                Vector2 center = footstepPathCenters[i];
                float perspective = Mathf.InverseLerp(0.07f, 0.36f, center.y);
                Vector2 size = Vector2.Lerp(new Vector2(0.034f, 0.102f), new Vector2(0.021f, 0.063f), perspective);
                Vector2 halfSize = size * 0.5f;
                Stretch(footRect, center - halfSize, center + halfSize);
                footRect.localEulerAngles = new Vector3(0f, 0f, footstepPathAngles[i]);

                int latestAnsweredSegment = currentFootstepCommitted
                    ? currentQuestionIndex
                    : currentQuestionIndex - 1;
                int segmentAge = Mathf.Max(0, latestAnsweredSegment - footstepPathSegments[i]);
                float historyAlpha = segmentAge == 0 ? 0.62f : segmentAge == 1 ? 0.36f : 0.21f;
                bool newest = i == visibleFootstepCount - 1 && currentFootstepCommitted;
                float committedGlow = newest ? footstepCommitPulse : 0f;
                footstep.color = new Color(0.58f, 0.70f, 0.82f, historyAlpha + committedGlow * 0.22f);
                footRect.localScale = Vector3.one * (1f + committedGlow * 0.08f);
            }
        }

        private void AppendFootstepSegment(string answer, int answerIndex)
        {
            if (!useWorldArt || footstepPathCenters.Count >= MaxTrailFootsteps)
            {
                return;
            }

            Vector2 start = footstepPathCenters.Count > 0
                ? footstepPathCenters[footstepPathCenters.Count - 1]
                : new Vector2(0.5f, 0.082f);
            bool choseYes = string.Equals(answer, "YES", System.StringComparison.Ordinal);
            float progress = Mathf.Clamp01(answerIndex / 4f);
            Vector2 target = new Vector2(choseYes ? 0.285f : 0.715f, Mathf.Lerp(0.145f, 0.395f, progress));

            // 回答ごとに一段上へ進むS字軌道にする。横断距離が長くても
            // 足跡同士が団子にならず、過去の左右への迷いが一本の道として読める。
            Vector2 delta = target - start;
            Vector2 weightedDelta = new Vector2(delta.x * 0.62f, delta.y);
            int stepCount = Mathf.Clamp(Mathf.CeilToInt(weightedDelta.magnitude / 0.082f), 1, 6);
            Vector2 controlA = start + new Vector2(delta.x * 0.18f, delta.y * 0.58f);
            Vector2 controlB = target - new Vector2(delta.x * 0.18f, delta.y * 0.42f);
            Vector2 previous = start;

            for (int step = 1; step <= stepCount && footstepPathCenters.Count < MaxTrailFootsteps; step++)
            {
                float t = step / (float)stepCount;
                float oneMinusT = 1f - t;
                Vector2 center =
                    oneMinusT * oneMinusT * oneMinusT * start
                    + 3f * oneMinusT * oneMinusT * t * controlA
                    + 3f * oneMinusT * t * t * controlB
                    + t * t * t * target;

                Vector2 stepDirection = center - previous;
                float angle = -Mathf.Atan2(stepDirection.x, Mathf.Max(0.001f, stepDirection.y)) * Mathf.Rad2Deg;
                angle = Mathf.Clamp(angle, -48f, 48f);
                footstepPathCenters.Add(center);
                footstepPathAngles.Add(angle);
                footstepPathSegments.Add(answerIndex);
                previous = center;
            }
        }

        private string BuildDoubtLine(int questionIndex)
        {
            if (questionIndex <= 0 || answers.Count == 0)
            {
                return "正解は表示されません。";
            }

            string previous = answers[answers.Count - 1];
            return $"さっきは「{previous}」を選んだ。確信はある？";
        }

        private static string BuildIntrusiveText(string[] lines, int questionIndex)
        {
            if (lines == null || lines.Length == 0 || questionIndex == 0)
            {
                return string.Empty;
            }

            int count = Mathf.Min(questionIndex + 1, lines.Length);
            var builder = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }
                builder.Append(lines[i]);
            }
            return builder.ToString();
        }

        private void UpdateHistory(string[] questions, int lastAnsweredIndex)
        {
            if (lastAnsweredIndex < questions.Length - 1)
            {
                historyText.text = $"記録 {lastAnsweredIndex + 1:00}   <color=#A96CE0>{answers[lastAnsweredIndex]}</color>";
                return;
            }

            var builder = new StringBuilder();
            for (int i = 0; i <= lastAnsweredIndex; i++)
            {
                builder.Append($"{i + 1:00}   <color=#A96CE0>{answers[i]}</color>   ");
                builder.Append(questions[i]);
                if (i < lastAnsweredIndex)
                {
                    builder.Append('\n');
                }
            }
            historyText.text = builder.ToString();
        }

        private void Choose(string answer)
        {
            if (pendingAnswer == null)
            {
                if (useWorldArt)
                {
                    worldVisuals.BeginChoiceRipple(answer);
                }
                pendingAnswer = answer;
            }
        }

        internal void SetChoiceHover(string answer, bool hovered)
        {
            if (useWorldArt && worldVisuals != null)
            {
                worldVisuals.SetChoiceHover(answer, hovered);
            }
        }

        private void EnsureUI()
        {
            if (root != null)
            {
                return;
            }
            if (hostCanvas == null || font == null)
            {
                throw new System.InvalidOperationException("AnxietyQuestionExperience.Configure を先に呼んでください。");
            }

            if (hostCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                hostCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            if (EventSystem.current == null)
            {
                GameObject eventSystemObject = new GameObject(
                    "AnxietyEventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystemObject.transform.SetParent(transform, false);
                localEventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            root = CreateUIObject("AnxietyQuestionExperience", hostCanvas.transform, typeof(CanvasGroup), typeof(Image));
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            rootGroup = root.GetComponent<CanvasGroup>();
            background = root.GetComponent<Image>();
            background.color = GetQuestionBackground(0f);
            background.raycastTarget = true;

            GameObject content = CreateUIObject("QuestionContent", root.transform, typeof(CanvasGroup));
            Stretch(content.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            contentGroup = content.GetComponent<CanvasGroup>();

            if (!useWorldArt)
            {
                CreateFrameLine(content.transform, new Vector2(0.035f, 0.91f), new Vector2(0.965f, 0.914f), new Color(0.55f, 0.34f, 0.72f, 0.38f));
                CreateFrameLine(content.transform, new Vector2(0.035f, 0.085f), new Vector2(0.965f, 0.089f), new Color(0.55f, 0.34f, 0.72f, 0.24f));
            }

            eyebrowText = CreateText("Eyebrow", content.transform, new Vector2(0.055f, 0.925f), new Vector2(0.46f, 0.972f), 15f, TextAlignmentOptions.Left);
            eyebrowText.color = Violet;
            eyebrowText.characterSpacing = 7f;
            counterText = CreateText("Counter", content.transform, new Vector2(0.67f, 0.925f), new Vector2(0.945f, 0.972f), 15f, TextAlignmentOptions.Right);
            counterText.color = Muted;
            eyebrowText.gameObject.SetActive(!useWorldArt);
            counterText.gameObject.SetActive(!useWorldArt);

            GameObject stage = CreateUIObject("QuestionStage", content.transform, typeof(CanvasGroup));
            stageRect = stage.GetComponent<RectTransform>();
            Stretch(stageRect, useWorldArt ? Vector2.zero : new Vector2(0.14f, 0.43f), useWorldArt ? Vector2.one : new Vector2(0.86f, 0.82f));
            stageGroup = stage.GetComponent<CanvasGroup>();

            if (useWorldArt)
            {
                GameObject panel = CreateUIObject("QuestionPanel", stage.transform, typeof(Image));
                Stretch(panel.GetComponent<RectTransform>(), new Vector2(0.10f, 0.750f), new Vector2(0.90f, 0.885f));
                questionPanel = panel.GetComponent<Image>();
                questionPanel.color = new Color(0.008f, 0.015f, 0.038f, 0.78f);
                questionPanel.raycastTarget = false;
                CreateFrameLine(panel.transform, new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.035f), new Color(0.42f, 0.58f, 0.86f, 0.32f));
            }

            doubtText = CreateText("Doubt", stage.transform, useWorldArt ? new Vector2(0.13f, 0.825f) : new Vector2(0.05f, 0.76f), useWorldArt ? new Vector2(0.87f, 0.875f) : new Vector2(0.95f, 0.94f), 19f, TextAlignmentOptions.Center);
            doubtText.color = Muted;
            doubtText.gameObject.SetActive(!useWorldArt);
            Vector2 questionMin = useWorldArt ? new Vector2(0.13f, 0.765f) : new Vector2(0f, 0.23f);
            Vector2 questionMax = useWorldArt ? new Vector2(0.87f, 0.875f) : new Vector2(1f, 0.78f);
            float questionFontSize = useWorldArt ? 40f : 44f;
            questionShadowA = CreateText("QuestionEchoA", stage.transform, questionMin, questionMax, questionFontSize, TextAlignmentOptions.Center);
            questionShadowA.color = new Color(0.95f, 0.15f, 0.32f, 0f);
            questionShadowB = CreateText("QuestionEchoB", stage.transform, questionMin, questionMax, questionFontSize, TextAlignmentOptions.Center);
            questionShadowB.color = new Color(0.20f, 0.55f, 1f, 0f);
            questionShadowA.gameObject.SetActive(!useWorldArt);
            questionShadowB.gameObject.SetActive(!useWorldArt);
            questionText = CreateText("Question", stage.transform, questionMin, questionMax, questionFontSize, TextAlignmentOptions.Center);
            questionText.color = SoftWhite;
            questionText.fontStyle = FontStyles.Bold;

            yesButton = CreateButton("YES", stage.transform, useWorldArt ? new Vector2(0.202f, 0.320f) : new Vector2(0.08f, 0f), useWorldArt ? new Vector2(0.308f, 0.372f) : new Vector2(0.47f, 0.20f), out yesGroup);
            noButton = CreateButton("NO", stage.transform, useWorldArt ? new Vector2(0.678f, 0.320f) : new Vector2(0.53f, 0f), useWorldArt ? new Vector2(0.784f, 0.372f) : new Vector2(0.92f, 0.20f), out noGroup);
            yesButton.onClick.AddListener(() => Choose("YES"));
            noButton.onClick.AddListener(() => Choose("NO"));

            if (useWorldArt)
            {
                yesPuddleButton = CreatePuddleButton("YES", stage.transform, new Vector2(0.125f, 0.405f), new Vector2(0.390f, 0.650f));
                noPuddleButton = CreatePuddleButton("NO", stage.transform, new Vector2(0.595f, 0.400f), new Vector2(0.865f, 0.650f));
                CreateFootstepProgress(stage.transform);
            }

            GameObject history = CreateUIObject("HistoryPanel", content.transform, typeof(CanvasGroup), typeof(Image));
            historyPanel = history.GetComponent<RectTransform>();
            Stretch(historyPanel, new Vector2(0.045f, 0.065f), useWorldArt ? new Vector2(0.50f, 0.285f) : new Vector2(0.68f, 0.39f));
            historyGroup = history.GetComponent<CanvasGroup>();
            Image historyBackground = history.GetComponent<Image>();
            historyBackground.color = new Color(0.08f, 0.055f, 0.12f, 0.62f);
            historyBackground.raycastTarget = false;
            historyText = CreateText("History", history.transform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.92f), 20f, TextAlignmentOptions.BottomLeft);
            historyText.color = new Color(0.78f, 0.74f, 0.84f, 1f);
            historyText.lineSpacing = 8f;
            history.SetActive(!useWorldArt);

            intrusiveText = CreateText("IntrusiveThoughts", content.transform, useWorldArt ? new Vector2(0.60f, 0.065f) : new Vector2(0.56f, 0.09f), useWorldArt ? new Vector2(0.955f, 0.285f) : new Vector2(0.95f, 0.43f), 21f, TextAlignmentOptions.BottomRight);
            intrusiveText.color = new Color(0.73f, 0.39f, 0.88f, 0.7f);
            intrusiveText.fontStyle = FontStyles.Italic;
            intrusiveText.lineSpacing = 14f;
            intrusiveText.gameObject.SetActive(!useWorldArt);

            footerText = CreateText("InputHint", content.transform, useWorldArt ? new Vector2(0.79f, 0.018f) : new Vector2(0.34f, 0.025f), useWorldArt ? new Vector2(0.955f, 0.055f) : new Vector2(0.66f, 0.075f), 14f, useWorldArt ? TextAlignmentOptions.Right : TextAlignmentOptions.Center);
            footerText.color = new Color(0.50f, 0.47f, 0.58f, 0.8f);
            footerText.characterSpacing = 4f;

            root.SetActive(false);
        }

        private void CreateFootstepProgress(Transform parent)
        {
            GameObject trail = CreateUIObject("FootstepProgress", parent);
            Stretch(trail.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            trail.transform.SetAsFirstSibling();

            startLeftFootImage = CreateFootstepImage(
                "StartLeftFoot",
                trail.transform,
                leftFootSprite,
                new Vector2(0.478f, 0.064f),
                new Vector2(0.036f, 0.108f),
                -14f,
                out startLeftFootRect);
            startRightFootImage = CreateFootstepImage(
                "StartRightFoot",
                trail.transform,
                rightFootSprite,
                new Vector2(0.524f, 0.080f),
                new Vector2(0.036f, 0.108f),
                12f,
                out startRightFootRect);

            for (int i = 0; i < footstepImages.Length; i++)
            {
                Image image = CreateFootstepImage(
                    $"Footstep{i + 1:00}",
                    trail.transform,
                    i % 2 == 0 ? leftFootSprite : rightFootSprite,
                    new Vector2(0.5f, 0.12f),
                    new Vector2(0.032f, 0.096f),
                    0f,
                    out RectTransform rect);
                image.gameObject.SetActive(false);
                footstepImages[i] = image;
                footstepRects[i] = rect;
            }

            AnimateFootsteps();
        }

        private static Image CreateFootstepImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 center,
            Vector2 size,
            float angle,
            out RectTransform rect)
        {
            GameObject step = CreateUIObject(objectName, parent, typeof(Image));
            rect = step.GetComponent<RectTransform>();
            Vector2 halfSize = size * 0.5f;
            Stretch(rect, center - halfSize, center + halfSize);
            rect.localEulerAngles = new Vector3(0f, 0f, angle);

            Image image = step.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            step.SetActive(sprite != null);
            return image;
        }

        private Button CreatePuddleButton(string answer, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = CreateUIObject(answer + "PuddleButton", parent, typeof(Image), typeof(Button));
            Stretch(obj.GetComponent<RectTransform>(), anchorMin, anchorMax);
            Image image = obj.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;

            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.95f, 1f, 1f);
            colors.disabledColor = Color.clear;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => Choose(answer));

            obj.AddComponent<EllipseRaycastFilter>();
            AnxietyChoiceHoverRelay relay = obj.AddComponent<AnxietyChoiceHoverRelay>();
            relay.Configure(this, answer);
            return button;
        }

        private Button CreateButton(string label, Transform parent, Vector2 anchorMin, Vector2 anchorMax, out CanvasGroup group)
        {
            GameObject obj = CreateUIObject(label + "Button", parent, typeof(CanvasGroup), typeof(Image), typeof(Button));
            Stretch(obj.GetComponent<RectTransform>(), anchorMin, anchorMax);
            group = obj.GetComponent<CanvasGroup>();
            Image image = obj.GetComponent<Image>();
            bool yes = string.Equals(label, "YES", System.StringComparison.Ordinal);
            image.color = useWorldArt
                ? (yes ? new Color(0.025f, 0.075f, 0.13f, 0.48f) : new Color(0.085f, 0.035f, 0.12f, 0.48f))
                : new Color(0.16f, 0.10f, 0.21f, 0.96f);
            if (useWorldArt)
            {
                Outline outline = obj.AddComponent<Outline>();
                outline.effectColor = yes
                    ? new Color(0.37f, 0.67f, 0.88f, 0.42f)
                    : new Color(0.69f, 0.38f, 0.84f, 0.42f);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;
            }

            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = useWorldArt ? Color.white : new Color(0.82f, 0.75f, 0.89f, 1f);
            colors.highlightedColor = useWorldArt ? new Color(1.16f, 1.16f, 1.20f, 1f) : Color.white;
            colors.selectedColor = useWorldArt ? new Color(1.12f, 1.12f, 1.18f, 1f) : new Color(0.91f, 0.80f, 1f, 1f);
            colors.pressedColor = useWorldArt ? new Color(0.78f, 0.82f, 0.92f, 1f) : new Color(0.73f, 0.48f, 0.91f, 1f);
            colors.disabledColor = useWorldArt ? new Color(0.52f, 0.52f, 0.60f, 0.62f) : new Color(0.34f, 0.29f, 0.40f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.10f;
            button.colors = colors;

            TMP_Text labelText = CreateText("Label", obj.transform, Vector2.zero, Vector2.one, useWorldArt ? 29f : 27f, TextAlignmentOptions.Center);
            labelText.text = label;
            labelText.color = useWorldArt
                ? (yes ? new Color(0.73f, 0.88f, 1f, 1f) : new Color(0.92f, 0.76f, 1f, 1f))
                : new Color(0.08f, 0.045f, 0.11f, 1f);
            labelText.fontStyle = useWorldArt ? FontStyles.Normal : FontStyles.Bold;
            labelText.characterSpacing = useWorldArt ? 18f : 10f;
            if (useWorldArt)
            {
                obj.AddComponent<AnxietyChoicePlateFeedback>();
                AnxietyChoiceHoverRelay relay = obj.AddComponent<AnxietyChoiceHoverRelay>();
                relay.Configure(this, label);
            }
            return button;
        }

        private TMP_Text CreateText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject obj = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
            Stretch(obj.GetComponent<RectTransform>(), anchorMin, anchorMax);
            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void CreateFrameLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject line = CreateUIObject("FrameLine", parent, typeof(Image));
            Stretch(line.GetComponent<RectTransform>(), anchorMin, anchorMax);
            Image image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static GameObject CreateUIObject(string objectName, Transform parent, params System.Type[] components)
        {
            var types = new List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            GameObject obj = new GameObject(objectName, types.ToArray());
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(elapsed / duration));
                yield return null;
            }
            group.alpha = to;
        }

        private IEnumerator FadeBackground(Color from, Color to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                background.color = Color.Lerp(from, to, EaseOutCubic(elapsed / duration));
                yield return null;
            }
            background.color = to;
        }

        private static float EaseOutCubic(float value)
        {
            float t = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private void OnDisable()
        {
            HideImmediately();
        }
    }

    /// <summary>透明なUIボタンのクリック判定を長方形ではなく楕円内に限定する。</summary>
    internal sealed class EllipseRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private RectTransform rectTransform;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }
            if (rectTransform == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            Rect rect = rectTransform.rect;
            float radiusX = Mathf.Max(0.001f, rect.width * 0.5f);
            float radiusY = Mathf.Max(0.001f, rect.height * 0.5f);
            float normalizedX = (localPoint.x - rect.center.x) / radiusX;
            float normalizedY = (localPoint.y - rect.center.y) / radiusY;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
        }
    }

    /// <summary>選択プレートへ、視認性を損なわない小さな浮き沈みを付ける。</summary>
    internal sealed class AnxietyChoicePlateFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private RectTransform rectTransform;
        private bool hovered;
        private bool pressed;
        private bool selected;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
        }

        private void Update()
        {
            if (rectTransform == null)
            {
                return;
            }

            float target = pressed ? 0.98f : (hovered || selected ? 1.025f : 1f);
            float current = rectTransform.localScale.x;
            float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            rectTransform.localScale = Vector3.one * next;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) => selected = false;
    }

    /// <summary>プレートと水面のホバー状態を、ワールド側の波紋へ伝える。</summary>
    internal sealed class AnxietyChoiceHoverRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private AnxietyQuestionExperience owner;
        private string answer;

        public void Configure(AnxietyQuestionExperience requestedOwner, string requestedAnswer)
        {
            owner = requestedOwner;
            answer = requestedAnswer;
        }

        public void OnPointerEnter(PointerEventData eventData) => owner?.SetChoiceHover(answer, true);
        public void OnPointerExit(PointerEventData eventData) => owner?.SetChoiceHover(answer, false);
        public void OnSelect(BaseEventData eventData) => owner?.SetChoiceHover(answer, true);
        public void OnDeselect(BaseEventData eventData) => owner?.SetChoiceHover(answer, false);

        private void OnDisable()
        {
            owner?.SetChoiceHover(answer, false);
        }
    }

    /// <summary>
    /// 質問専用の石畳・選択面・雨と、選択面へ入るカメラ移動を管理する。
    /// 質問ロジックとは分離し、質問中だけ既存の射撃ステージを手前から覆う。
    /// </summary>
    internal sealed class AnxietyQuestionWorldVisuals : MonoBehaviour
    {
        private static readonly Vector2 YesViewportCenter = new Vector2(0.255f, 0.524f);
        private static readonly Vector2 NoViewportCenter = new Vector2(0.731f, 0.515f);
        private static readonly Vector2 YesLabelViewportCenter = new Vector2(0.255f, 0.685f);
        private static readonly Vector2 NoLabelViewportCenter = new Vector2(0.731f, 0.685f);

        private Camera questionCamera;
        private Sprite floorSprite;
        private Sprite yesGateSprite;
        private Sprite noGateSprite;
        private Material gateMaterial;
        private TMP_FontAsset font;
        private float diveOrthographicSize = 0.90f;
        private bool rainEnabled = true;
        private float rainRate = 85f;
        private Material requestedRainMaterial;
        private AudioClip runningWetRoadClip;
        private float runningWetRoadVolume = 0.22f;
        private bool ready;
        private bool cameraStateCaptured;

        private GameObject visualRoot;
        private SpriteRenderer floorSafetyRenderer;
        private SpriteRenderer floorRenderer;
        private SpriteRenderer yesGateRenderer;
        private SpriteRenderer noGateRenderer;
        private Material yesGateRuntimeMaterial;
        private Material noGateRuntimeMaterial;
        private TextMeshPro yesLabel;
        private TextMeshPro noLabel;
        private ParticleSystem rain;
        private ParticleSystem rainShadow;
        private Material rainMaterial;
        private Material rainShadowMaterial;
        private AudioSource runningAudio;
        private Vector3 originalCameraPosition;
        private float originalCameraSize;
        private Quaternion originalCameraRotation;
        private Vector3 yesWorldCenter;
        private Vector3 noWorldCenter;
        private bool yesHovered;
        private bool noHovered;
        private string activeChoiceAnswer;
        private float activeChoiceTime;
        private string emphasizedAnswer;
        private float emphasisAmount;

        public bool Configure(
            Camera camera,
            Sprite floor,
            Sprite yesGate,
            Sprite noGate,
            Material requestedGateMaterial,
            TMP_FontAsset fontAsset,
            float requestedDiveSize,
            bool requestedRainEnabled,
            float requestedRainRate,
            Material rainMaterialAsset,
            AudioClip requestedRunningWetRoadClip,
            float requestedRunningWetRoadVolume)
        {
            questionCamera = camera;
            floorSprite = floor;
            yesGateSprite = yesGate;
            noGateSprite = noGate;
            gateMaterial = requestedGateMaterial;
            font = fontAsset;
            diveOrthographicSize = Mathf.Max(0.8f, requestedDiveSize);
            rainEnabled = requestedRainEnabled;
            rainRate = Mathf.Clamp(requestedRainRate, 10f, 180f);
            requestedRainMaterial = rainMaterialAsset;
            runningWetRoadClip = requestedRunningWetRoadClip;
            runningWetRoadVolume = Mathf.Clamp01(requestedRunningWetRoadVolume);
            ready = questionCamera != null
                && questionCamera.orthographic
                && floorSprite != null
                && yesGateSprite != null
                && noGateSprite != null
                && font != null;

            if (!ready)
            {
                if (visualRoot != null)
                {
                    visualRoot.SetActive(false);
                }
                return false;
            }

            EnsureVisuals();
            return true;
        }

        public void Show()
        {
            if (!ready)
            {
                return;
            }

            CaptureCameraState();
            LayoutToCamera();
            yesHovered = false;
            noHovered = false;
            ResetGateState();
            visualRoot.SetActive(true);

            if (rain != null)
            {
                rain.gameObject.SetActive(rainEnabled);
                if (rainEnabled)
                {
                    rain.Clear(true);
                    rain.Play(true);
                }
            }
            if (rainShadow != null)
            {
                rainShadow.gameObject.SetActive(rainEnabled);
                if (rainEnabled)
                {
                    rainShadow.Clear(true);
                    rainShadow.Play(true);
                }
            }
        }

        public void Hide()
        {
            RestoreCamera();
            yesHovered = false;
            noHovered = false;
            if (runningAudio != null)
            {
                runningAudio.Stop();
            }
            if (rain != null)
            {
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (rainShadow != null)
            {
                rainShadow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
        }

        public void ResetView()
        {
            if (!ready)
            {
                return;
            }

            RestoreCamera();
            CaptureCameraState();
            LayoutToCamera();
            ResetGateState();
            if (rainEnabled && rain != null && !rain.isPlaying)
            {
                rain.Play(true);
            }
            if (rainEnabled && rainShadow != null && !rainShadow.isPlaying)
            {
                rainShadow.Play(true);
            }
        }

        public void SetChoiceHover(string answer, bool hovered)
        {
            if (string.Equals(answer, "YES", System.StringComparison.Ordinal))
            {
                yesHovered = hovered;
            }
            else if (string.Equals(answer, "NO", System.StringComparison.Ordinal))
            {
                noHovered = hovered;
            }
        }

        public void BeginChoiceRipple(string answer)
        {
            activeChoiceAnswer = answer;
            activeChoiceTime = 0f;
            StartWalkingAudio();
        }

        private void Update()
        {
            if (!ready || visualRoot == null || !visualRoot.activeInHierarchy)
            {
                return;
            }

            activeChoiceTime += Time.unscaledDeltaTime;
            AnimateGateReaction();
            if (activeChoiceTime >= 0.52f)
            {
                activeChoiceAnswer = null;
            }
        }

        private void AnimateGateReaction()
        {
            if (yesGateRenderer == null || noGateRenderer == null)
            {
                return;
            }

            Color yesTarget = GetGateReactionColor(true);
            Color noTarget = GetGateReactionColor(false);
            float blend = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            yesGateRenderer.color = Color.Lerp(yesGateRenderer.color, yesTarget, blend);
            noGateRenderer.color = Color.Lerp(noGateRenderer.color, noTarget, blend);
        }

        private Color GetGateReactionColor(bool yes)
        {
            string answer = yes ? "YES" : "NO";
            bool chosen = string.Equals(emphasizedAnswer, answer, System.StringComparison.Ordinal);
            bool choicePulse = string.Equals(activeChoiceAnswer, answer, System.StringComparison.Ordinal);
            bool hovered = yes ? yesHovered : noHovered;

            if (emphasisAmount > 0f)
            {
                return chosen
                    ? Color.Lerp(Color.white, new Color(1.15f, 1.15f, 1.24f, 1f), emphasisAmount)
                    : Color.Lerp(Color.white, new Color(0.55f, 0.55f, 0.64f, 0.46f), emphasisAmount);
            }
            if (choicePulse)
            {
                float t = Mathf.Clamp01(activeChoiceTime / 0.52f);
                float glow = Mathf.Sin(t * Mathf.PI);
                return Color.Lerp(Color.white, new Color(1.13f, 1.13f, 1.22f, 1f), glow);
            }
            if (hovered)
            {
                float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 2.8f) * 0.5f;
                return Color.Lerp(Color.white, new Color(1.08f, 1.10f, 1.16f, 1f), Mathf.Lerp(0.55f, 1f, pulse));
            }
            return Color.white;
        }

        public void SetAnswerEmphasis(string answer, float amount)
        {
            if (!ready)
            {
                return;
            }

            float t = Mathf.Clamp01(amount);
            bool choseYes = string.Equals(answer, "YES", System.StringComparison.Ordinal);
            TextMeshPro selectedLabel = choseYes ? yesLabel : noLabel;
            TextMeshPro rejectedLabel = choseYes ? noLabel : yesLabel;

            emphasizedAnswer = answer;
            emphasisAmount = t;
            selectedLabel.color = Color.Lerp(GetLabelColor(choseYes), Color.white, t);
            rejectedLabel.color = Color.Lerp(GetLabelColor(!choseYes), new Color(0.48f, 0.48f, 0.58f, 0.35f), t);
            selectedLabel.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, Mathf.Sin(t * Mathf.PI * 0.5f));
            rejectedLabel.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, t);
        }

        public IEnumerator PlayDive(string answer, float duration, System.Action<float> onProgress = null)
        {
            if (!ready)
            {
                yield break;
            }

            bool choseYes = string.Equals(answer, "YES", System.StringComparison.Ordinal);
            Vector3 startPosition = questionCamera.transform.position;
            float startSize = questionCamera.orthographicSize;
            Quaternion startRotation = questionCamera.transform.rotation;
            Vector3 gateCenter = choseYes ? yesWorldCenter : noWorldCenter;
            Vector3 targetPosition = new Vector3(gateCenter.x, gateCenter.y, startPosition.z);
            Quaternion targetRotation = startRotation * Quaternion.Euler(0f, 0f, choseYes ? -0.8f : 0.8f);
            float safeDuration = Mathf.Max(0.2f, duration);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / safeDuration);
                const float anticipationEnd = 0.12f;
                if (normalized < anticipationEnd)
                {
                    float anticipation = Mathf.SmoothStep(0f, 1f, normalized / anticipationEnd);
                    questionCamera.orthographicSize = Mathf.Lerp(startSize, startSize * 1.025f, anticipation);
                    onProgress?.Invoke(0f);
                    yield return null;
                    continue;
                }

                float diveProgress = Mathf.InverseLerp(anticipationEnd, 1f, normalized);
                float accelerated = Mathf.Pow(diveProgress, 2.15f);
                float shakeEnvelope = diveProgress * diveProgress * (1f - diveProgress);
                float shakeX = (Mathf.PerlinNoise(elapsed * 19f, 0.17f) - 0.5f) * 0.09f * shakeEnvelope;
                float shakeY = (Mathf.PerlinNoise(0.63f, elapsed * 21f) - 0.5f) * 0.07f * shakeEnvelope;
                Vector3 cameraPosition = Vector3.LerpUnclamped(startPosition, targetPosition, accelerated);
                cameraPosition += new Vector3(shakeX, shakeY, 0f);
                questionCamera.transform.position = cameraPosition;
                questionCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, accelerated);
                questionCamera.orthographicSize = Mathf.Lerp(startSize * 1.025f, diveOrthographicSize, accelerated);
                SetAnswerEmphasis(answer, Mathf.Lerp(0.7f, 1f, accelerated));
                onProgress?.Invoke(accelerated);
                yield return null;
            }

            questionCamera.transform.position = targetPosition;
            questionCamera.transform.rotation = targetRotation;
            questionCamera.orthographicSize = diveOrthographicSize;
            onProgress?.Invoke(1f);
        }

        private void EnsureVisuals()
        {
            if (visualRoot == null)
            {
                visualRoot = new GameObject("AnxietyQuestionWorldVisuals");
                visualRoot.transform.SetParent(transform, false);
                floorSafetyRenderer = CreateSpriteRenderer("FloorSafety", visualRoot.transform, 39);
                floorRenderer = CreateSpriteRenderer("Floor", visualRoot.transform, 40);
                yesGateRenderer = CreateSpriteRenderer("YesGate", visualRoot.transform, 41);
                noGateRenderer = CreateSpriteRenderer("NoGate", visualRoot.transform, 42);
                yesLabel = CreateWorldLabel("YES", visualRoot.transform, 70, new Color(0.62f, 0.80f, 1f, 1f));
                noLabel = CreateWorldLabel("NO", visualRoot.transform, 70, new Color(0.86f, 0.67f, 1f, 1f));
                yesLabel.gameObject.SetActive(false);
                noLabel.gameObject.SetActive(false);
                CreateRain(visualRoot.transform);
                CreateRainShadow(visualRoot.transform);
                CreateRunningAudio(visualRoot.transform);
            }

            floorSafetyRenderer.sprite = floorSprite;
            floorRenderer.sprite = floorSprite;
            yesGateRenderer.sprite = yesGateSprite;
            noGateRenderer.sprite = noGateSprite;
            ApplyGateMaterial(yesGateRenderer, yesGateSprite, ref yesGateRuntimeMaterial, "Runtime Anxiety YES Gate");
            ApplyGateMaterial(noGateRenderer, noGateSprite, ref noGateRuntimeMaterial, "Runtime Anxiety NO Gate");
            yesLabel.font = font;
            noLabel.font = font;
            if (runningAudio != null)
            {
                runningAudio.clip = runningWetRoadClip;
                runningAudio.volume = Mathf.Max(0.46f, runningWetRoadVolume);
            }
            if (rain != null)
            {
                ParticleSystem.EmissionModule emission = rain.emission;
                emission.rateOverTime = rainRate * 0.72f;
                rain.gameObject.SetActive(rainEnabled);
            }
            if (rainShadow != null)
            {
                ParticleSystem.EmissionModule emission = rainShadow.emission;
                emission.rateOverTime = rainRate * 0.34f;
                rainShadow.gameObject.SetActive(rainEnabled);
            }
            visualRoot.SetActive(false);
        }

        private void LayoutToCamera()
        {
            if (!ready)
            {
                return;
            }

            float worldHeight = originalCameraSize * 2f;
            float worldWidth = worldHeight * questionCamera.aspect;
            Vector3 cameraCenter = new Vector3(originalCameraPosition.x, originalCameraPosition.y, 0f);
            LayoutSprite(floorSafetyRenderer, cameraCenter, worldWidth, worldHeight, 1.14f);
            LayoutSprite(floorRenderer, cameraCenter, worldWidth, worldHeight);
            LayoutSprite(yesGateRenderer, cameraCenter, worldWidth, worldHeight);
            LayoutSprite(noGateRenderer, cameraCenter, worldWidth, worldHeight);

            float distance = Mathf.Abs(originalCameraPosition.z);
            yesWorldCenter = questionCamera.ViewportToWorldPoint(new Vector3(YesViewportCenter.x, YesViewportCenter.y, distance));
            noWorldCenter = questionCamera.ViewportToWorldPoint(new Vector3(NoViewportCenter.x, NoViewportCenter.y, distance));
            yesWorldCenter.z = 0f;
            noWorldCenter.z = 0f;
            Vector3 yesLabelPosition = questionCamera.ViewportToWorldPoint(new Vector3(YesLabelViewportCenter.x, YesLabelViewportCenter.y, distance));
            Vector3 noLabelPosition = questionCamera.ViewportToWorldPoint(new Vector3(NoLabelViewportCenter.x, NoLabelViewportCenter.y, distance));
            yesLabelPosition.z = 0f;
            noLabelPosition.z = 0f;
            yesLabel.transform.position = yesLabelPosition;
            noLabel.transform.position = noLabelPosition;

            if (rain != null)
            {
                rain.transform.position = new Vector3(cameraCenter.x, cameraCenter.y + worldHeight * 0.58f, 0f);
                ParticleSystem.ShapeModule shape = rain.shape;
                shape.scale = new Vector3(worldWidth * 1.12f, 0.15f, 0.1f);
                ParticleSystem.MainModule main = rain.main;
                main.startLifetime = worldHeight / 8.5f * 1.18f;
            }
            if (rainShadow != null)
            {
                rainShadow.transform.position = new Vector3(cameraCenter.x, cameraCenter.y + worldHeight * 0.58f, 0f);
                ParticleSystem.ShapeModule shape = rainShadow.shape;
                shape.scale = new Vector3(worldWidth * 1.12f, 0.15f, 0.1f);
                ParticleSystem.MainModule main = rainShadow.main;
                main.startLifetime = worldHeight / 8.5f * 1.18f;
            }
        }

        private static void LayoutSprite(SpriteRenderer renderer, Vector3 cameraCenter, float worldWidth, float worldHeight, float overscan = 1f)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            Vector2 spriteSize = renderer.sprite.bounds.size;
            float scale = Mathf.Max(worldWidth / Mathf.Max(0.001f, spriteSize.x), worldHeight / Mathf.Max(0.001f, spriteSize.y))
                * Mathf.Max(1f, overscan);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            Vector3 scaledCenter = renderer.sprite.bounds.center * scale;
            renderer.transform.position = cameraCenter - scaledCenter;
        }

        private void CaptureCameraState()
        {
            if (cameraStateCaptured || questionCamera == null)
            {
                return;
            }

            originalCameraPosition = questionCamera.transform.position;
            originalCameraSize = questionCamera.orthographicSize;
            originalCameraRotation = questionCamera.transform.rotation;
            cameraStateCaptured = true;
        }

        private void RestoreCamera()
        {
            if (!cameraStateCaptured || questionCamera == null)
            {
                return;
            }

            questionCamera.transform.position = originalCameraPosition;
            questionCamera.orthographicSize = originalCameraSize;
            questionCamera.transform.rotation = originalCameraRotation;
            cameraStateCaptured = false;
        }

        private void ResetGateState()
        {
            yesGateRenderer.color = Color.white;
            noGateRenderer.color = Color.white;
            yesLabel.color = GetLabelColor(true);
            noLabel.color = GetLabelColor(false);
            yesLabel.transform.localScale = Vector3.one;
            noLabel.transform.localScale = Vector3.one;
            activeChoiceAnswer = null;
            activeChoiceTime = 0f;
            emphasizedAnswer = null;
            emphasisAmount = 0f;
        }

        private static Color GetLabelColor(bool yes)
        {
            return yes
                ? new Color(0.62f, 0.80f, 1f, 1f)
                : new Color(0.86f, 0.67f, 1f, 1f);
        }

        private static SpriteRenderer CreateSpriteRenderer(string objectName, Transform parent, int sortingOrder)
        {
            GameObject obj = new GameObject(objectName, typeof(SpriteRenderer));
            obj.transform.SetParent(parent, false);
            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplyGateMaterial(
            SpriteRenderer renderer,
            Sprite sprite,
            ref Material runtimeMaterial,
            string runtimeName)
        {
            if (renderer == null || sprite == null || gateMaterial == null)
            {
                return;
            }

            if (runtimeMaterial == null || runtimeMaterial.shader != gateMaterial.shader)
            {
                DestroyRuntimeMaterial(runtimeMaterial);
                runtimeMaterial = new Material(gateMaterial)
                {
                    name = runtimeName
                };
            }
            else
            {
                runtimeMaterial.CopyPropertiesFromMaterial(gateMaterial);
            }

            runtimeMaterial.SetTexture("_MainTex", sprite.texture);
            renderer.sharedMaterial = runtimeMaterial;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", sprite.texture);
            renderer.SetPropertyBlock(properties);
        }

        private TextMeshPro CreateWorldLabel(string text, Transform parent, int sortingOrder, Color color)
        {
            GameObject obj = new GameObject(text + "Label", typeof(TextMeshPro));
            obj.transform.SetParent(parent, false);
            TextMeshPro label = obj.GetComponent<TextMeshPro>();
            label.text = text;
            label.font = font;
            label.fontSize = 4.2f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = color;
            label.characterSpacing = 8f;
            label.outlineWidth = 0.12f;
            label.outlineColor = new Color32(8, 10, 22, 220);
            label.rectTransform.sizeDelta = new Vector2(4.5f, 1.6f);
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            return label;
        }

        private void CreateRain(Transform parent)
        {
            GameObject rainObject = new GameObject("Rain", typeof(ParticleSystem));
            rainObject.transform.SetParent(parent, false);
            rain = rainObject.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = rain.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.82f, 0.94f, 0.42f),
                new Color(0.90f, 0.94f, 1f, 0.68f));
            main.maxParticles = 260;

            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = rainRate;

            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = rain.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var rainFade = new Gradient();
            rainFade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.82f, 0.90f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.88f, 0.08f),
                    new GradientAlphaKey(0.82f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(rainFade);

            ParticleSystem.VelocityOverLifetimeModule velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.68f, -0.24f);
            velocity.y = new ParticleSystem.MinMaxCurve(-9.6f, -8.7f);
            // x / y / z のCurveModeを揃えないとUnity 6でParticle Systemが停止する。
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystemRenderer renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.14f;
            renderer.lengthScale = 1.75f;
            renderer.sortingOrder = 60;

            if (requestedRainMaterial != null)
            {
                rainMaterial = new Material(requestedRainMaterial)
                {
                    name = "Runtime Anxiety Rain (Cinematic Weather)"
                };
                if (rainMaterial.HasProperty("_SoftParticlesEnabled"))
                {
                    rainMaterial.SetFloat("_SoftParticlesEnabled", 0f);
                }
                rainMaterial.DisableKeyword("_SOFTPARTICLES_ON");
                renderer.sharedMaterial = rainMaterial;
            }
            else
            {
                Shader rainShader = Shader.Find("Sprites/Default");
                if (rainShader != null)
                {
                    rainMaterial = new Material(rainShader)
                    {
                        name = "Runtime Anxiety Rain"
                    };
                    renderer.sharedMaterial = rainMaterial;
                }
            }
            rainObject.SetActive(rainEnabled);
        }

        private void CreateRainShadow(Transform parent)
        {
            GameObject rainObject = new GameObject("RainContrast", typeof(ParticleSystem));
            rainObject.transform.SetParent(parent, false);
            rainShadow = rainObject.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = rainShadow.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.026f, 0.042f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.08f, 0.15f, 0.24f, 0.18f),
                new Color(0.16f, 0.25f, 0.36f, 0.32f));
            main.maxParticles = 160;

            ParticleSystem.EmissionModule emission = rainShadow.emission;
            emission.rateOverTime = rainRate * 0.34f;

            ParticleSystem.ShapeModule shape = rainShadow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = rainShadow.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var rainFade = new Gradient();
            rainFade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.78f, 0.86f, 0.95f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.86f, 0.10f),
                    new GradientAlphaKey(0.78f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(rainFade);

            ParticleSystem.VelocityOverLifetimeModule velocity = rainShadow.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.68f, -0.24f);
            velocity.y = new ParticleSystem.MinMaxCurve(-9.6f, -8.7f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystemRenderer renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.13f;
            renderer.lengthScale = 1.55f;
            renderer.sortingOrder = 61;

            Shader shadowShader = Shader.Find("Sprites/Default");
            if (shadowShader != null)
            {
                rainShadowMaterial = new Material(shadowShader)
                {
                    name = "Runtime Anxiety Rain Contrast"
                };
                renderer.sharedMaterial = rainShadowMaterial;
            }
            rainObject.SetActive(rainEnabled);
        }

        private void CreateRunningAudio(Transform parent)
        {
            GameObject audioObject = new GameObject("WetRoadRunningAudio", typeof(AudioSource));
            audioObject.transform.SetParent(parent, false);
            runningAudio = audioObject.GetComponent<AudioSource>();
            runningAudio.playOnAwake = false;
            runningAudio.loop = true;
            runningAudio.spatialBlend = 0f;
            runningAudio.dopplerLevel = 0f;
            runningAudio.priority = 20;
            runningAudio.clip = runningWetRoadClip;
            runningAudio.volume = Mathf.Max(0.46f, runningWetRoadVolume);
        }

        private void StartWalkingAudio()
        {
            if (runningAudio == null || runningWetRoadClip == null)
            {
                return;
            }

            runningAudio.Stop();
            runningAudio.clip = runningWetRoadClip;
            runningAudio.volume = Mathf.Max(0.46f, runningWetRoadVolume);
            runningAudio.time = 0f;
            runningAudio.Play();
        }

        public void StopWalkingAudio()
        {
            if (runningAudio != null)
            {
                runningAudio.Stop();
            }
        }

        private void OnDisable()
        {
            RestoreCamera();
        }

        private void OnDestroy()
        {
            RestoreCamera();
            DestroyRuntimeMaterial(rainMaterial);
            DestroyRuntimeMaterial(rainShadowMaterial);
            DestroyRuntimeMaterial(yesGateRuntimeMaterial);
            DestroyRuntimeMaterial(noGateRuntimeMaterial);
        }

        private static void DestroyRuntimeMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
