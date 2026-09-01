using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AngerBattle
{
    /// <summary>怒り戦専用の連続HPゲージ、背景脈動、死亡・再挑戦表示を実行時に構築する。</summary>
    public class AngerBattleHUD : MonoBehaviour
    {
        private static readonly Color EnemyBaseColor = new Color(0.92f, 0.08f, 0.1f, 1f);
        private static readonly Color PlayerBaseColor = new Color(0.08f, 0.58f, 0.95f, 1f);

        [Header("Canvas・解像度")]
        [Tooltip("HUD全体の描画順")]
        public int canvasSortingOrder = -5;
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;

        [Header("HPゲージ・ラベル配置")]
        public Vector2 enemyBarPosition = new Vector2(330f, -42f);
        public Vector2 enemyLabelPosition = new Vector2(330f, -78f);
        public Vector2 playerBarPosition = new Vector2(-330f, 42f);
        public Vector2 playerLabelPosition = new Vector2(-330f, 78f);
        public Vector2 healthBarSize = new Vector2(560f, 28f);
        public Vector2 labelSize = new Vector2(720f, 48f);
        public float labelFontSize = 25f;
        public Vector2 retryTextPosition = Vector2.zero;
        public float retryFontSize = 44f;

        [Header("HUD演出")]
        [Min(1)] public int deathFragmentCount = 18;
        [Min(0.01f)] public float deathEffectDuration = 0.7f;
        [Range(0f, 1f)] public float deathFadeAlpha = 0.78f;
        [Min(0f)] public float deathFragmentGravity = 720f;
        [Min(0.01f)] public float barFlashDuration = 0.08f;
        [Min(0.01f)] public float phasePulseDuration = 0.55f;

        [SerializeField, HideInInspector] private Canvas canvas;
        [SerializeField, HideInInspector] private Image enemyFill;
        [SerializeField, HideInInspector] private Image playerFill;
        [SerializeField, HideInInspector] private RectTransform enemyFillRect;
        [SerializeField, HideInInspector] private RectTransform playerFillRect;
        [SerializeField, HideInInspector] private Image pulseOverlay;
        [SerializeField, HideInInspector] private Image fadeOverlay;
        [SerializeField, HideInInspector] private TMP_Text retryText;
        private readonly List<GameObject> fragments = new List<GameObject>();
        [SerializeField, HideInInspector] private bool built;

        public float EnemyHealthRatio => GetFillRatio(enemyFillRect);
        public float PlayerHealthRatio => GetFillRatio(playerFillRect);

        public void Build(TMP_Text textTemplate)
        {
            if (built)
            {
                return;
            }

            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = canvasSortingOrder;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;

            RectTransform root = GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            pulseOverlay = CreateImage(root, "PhasePulse", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.8f, 0f, 0f, 0f));
            fadeOverlay = CreateImage(root, "DeathFade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));

            CreateBar(root, "AngerHealth", new Vector2(0f, 1f), enemyBarPosition, EnemyBaseColor, out enemyFill, out enemyFillRect);
            CreateLabel(root, "AngerLabel", "怒り", new Vector2(0f, 1f), enemyLabelPosition, textTemplate);
            CreateBar(root, "ContackHealth", new Vector2(1f, 0f), playerBarPosition, PlayerBaseColor, out playerFill, out playerFillRect);
            CreateLabel(root, "ContackLabel", "コンタック", new Vector2(1f, 0f), playerLabelPosition, textTemplate);

            retryText = CreateLabel(root, "RetryText", "もう一度\nSPACE", new Vector2(0.5f, 0.5f), retryTextPosition, textTemplate);
            retryText.fontSize = retryFontSize;
            retryText.alignment = TextAlignmentOptions.Center;
            retryText.gameObject.SetActive(false);
            built = true;
            ResetRound();
        }

        public void ResetRound()
        {
            if (!built) return;
            StopAllCoroutines();
            ClearFragments();
            SetFillRatio(enemyFillRect, 1f, false);
            SetFillRatio(playerFillRect, 1f, true);
            enemyFill.gameObject.SetActive(true);
            playerFill.gameObject.SetActive(true);
            enemyFill.color = EnemyBaseColor;
            playerFill.color = PlayerBaseColor;
            pulseOverlay.color = new Color(0.8f, 0f, 0f, 0f);
            fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
            retryText.gameObject.SetActive(false);
        }

        public void SetEnemyHealth(int current, int maximum)
        {
            SetFillRatio(enemyFillRect, current / (float)Mathf.Max(1, maximum), false);
        }

        public void SetPlayerHealth(int current, int maximum)
        {
            SetFillRatio(playerFillRect, current / (float)Mathf.Max(1, maximum), true);
        }

        public void FlashEnemyHealth()
        {
            if (enemyFill != null) StartCoroutine(FlashBar(enemyFill, EnemyBaseColor));
        }

        public void FlashPlayerHealth()
        {
            if (playerFill != null) StartCoroutine(FlashBar(playerFill, PlayerBaseColor));
        }

        public void PulsePhaseBackground(int phase)
        {
            StartCoroutine(PulseBackground(Mathf.Clamp01(0.11f + phase * 0.05f)));
        }

        public IEnumerator PlayPlayerDeathEffect()
        {
            if (!built) yield break;

            playerFill.gameObject.SetActive(false);
            int fragmentCount = Mathf.Max(1, deathFragmentCount);
            Vector2[] velocities = new Vector2[fragmentCount];
            float[] spins = new float[fragmentCount];
            for (int i = 0; i < fragmentCount; i++)
            {
                Image fragment = CreateImage(
                    (RectTransform)transform,
                    "GaugeFragment",
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(-330f + Random.Range(-280f, 280f), 42f + Random.Range(-5f, 5f)),
                    new Vector2(Random.Range(18f, 52f), Random.Range(7f, 16f)),
                    PlayerBaseColor);
                fragments.Add(fragment.gameObject);
                velocities[i] = new Vector2(Random.Range(-420f, 420f), Random.Range(120f, 430f));
                spins[i] = Random.Range(-420f, 420f);
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, deathEffectDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < fragments.Count; i++)
                {
                    if (fragments[i] == null) continue;
                    RectTransform rect = (RectTransform)fragments[i].transform;
                    velocities[i] += Vector2.down * Mathf.Max(0f, deathFragmentGravity) * Time.unscaledDeltaTime;
                    rect.anchoredPosition += velocities[i] * Time.unscaledDeltaTime;
                    rect.Rotate(0f, 0f, spins[i] * Time.unscaledDeltaTime);
                    Image image = fragments[i].GetComponent<Image>();
                    Color color = PlayerBaseColor;
                    color.a = 1f - t;
                    image.color = color;
                }
                Color fade = fadeOverlay.color;
                fade.a = Mathf.Lerp(0f, Mathf.Clamp01(deathFadeAlpha), t);
                fadeOverlay.color = fade;
                yield return null;
            }
            ClearFragments();
            retryText.gameObject.SetActive(true);
        }

        public void HideRetryOverlay()
        {
            if (!built) return;
            playerFill.gameObject.SetActive(true);
            retryText.gameObject.SetActive(false);
            fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        }

        private IEnumerator FlashBar(Image image, Color baseColor)
        {
            image.color = Color.white;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, barFlashDuration));
            image.color = baseColor;
        }

        private IEnumerator PulseBackground(float peakAlpha)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, phasePulseDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Sin(normalized * Mathf.PI) * peakAlpha;
                pulseOverlay.color = new Color(0.85f, 0f, 0.02f, alpha);
                yield return null;
            }
            pulseOverlay.color = new Color(0.8f, 0f, 0f, 0f);
        }

        private void CreateBar(
            RectTransform parent,
            string name,
            Vector2 anchor,
            Vector2 position,
            Color fillColor,
            out Image fill,
            out RectTransform fillRect)
        {
            Image background = CreateImage(parent, name, anchor, anchor, position, healthBarSize, new Color(0.035f, 0.035f, 0.045f, 0.94f));
            Image border = CreateImage(background.rectTransform, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.22f));
            border.rectTransform.offsetMin = new Vector2(-3f, -3f);
            border.rectTransform.offsetMax = new Vector2(3f, 3f);
            border.transform.SetAsFirstSibling();
            fill = CreateImage(background.rectTransform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fillColor);
            fill.type = Image.Type.Simple;
            fillRect = fill.rectTransform;
        }

        private static void SetFillRatio(RectTransform fillRect, float ratio, bool alignRight)
        {
            if (fillRect == null) return;
            ratio = Mathf.Clamp01(ratio);
            fillRect.anchorMin = alignRight ? new Vector2(1f - ratio, 0f) : Vector2.zero;
            fillRect.anchorMax = alignRight ? Vector2.one : new Vector2(ratio, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        private static float GetFillRatio(RectTransform fillRect)
        {
            return fillRect == null ? 0f : Mathf.Clamp01(fillRect.anchorMax.x - fillRect.anchorMin.x);
        }

        private static Image CreateImage(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateLabel(
            RectTransform parent,
            string name,
            string text,
            Vector2 anchor,
            Vector2 position,
            TMP_Text template)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = labelSize;
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = template != null ? template.font : TMP_Settings.defaultFontAsset;
            label.fontSize = labelFontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        [ContextMenu("Inspector設定をHUDへ反映")]
        public void ApplyInspectorSettings()
        {
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = canvasSortingOrder;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.referenceResolution = referenceResolution;
                scaler.matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);
            }

            ApplyBarLayout(enemyFillRect, enemyBarPosition);
            ApplyBarLayout(playerFillRect, playerBarPosition);
            ApplyLabelLayout("AngerLabel", enemyLabelPosition, labelFontSize);
            ApplyLabelLayout("ContackLabel", playerLabelPosition, labelFontSize);
            if (retryText != null)
            {
                retryText.rectTransform.anchoredPosition = retryTextPosition;
                retryText.rectTransform.sizeDelta = labelSize;
                retryText.fontSize = retryFontSize;
            }
        }

        private void ApplyBarLayout(RectTransform fillRect, Vector2 position)
        {
            if (fillRect == null || fillRect.parent is not RectTransform barRect) return;
            barRect.anchoredPosition = position;
            barRect.sizeDelta = healthBarSize;
        }

        private void ApplyLabelLayout(string objectName, Vector2 position, float fontSize)
        {
            Transform labelTransform = transform.Find(objectName);
            if (labelTransform == null) return;
            RectTransform rect = labelTransform as RectTransform;
            TMP_Text label = labelTransform.GetComponent<TMP_Text>();
            if (rect != null)
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = labelSize;
            }
            if (label != null) label.fontSize = fontSize;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            ApplyInspectorSettings();
        }
#endif

        private void ClearFragments()
        {
            foreach (GameObject fragment in fragments)
            {
                if (fragment != null) Destroy(fragment);
            }
            fragments.Clear();
        }
    }
}
