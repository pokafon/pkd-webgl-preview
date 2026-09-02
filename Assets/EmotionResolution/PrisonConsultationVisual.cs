using SadnessBattle;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

namespace PKD.Emotions
{
    /// <summary>TRUE ENDの相談中、隔離した感情と牢屋を会話UIの背面へ表示する。</summary>
    public static class PrisonConsultationVisual
    {
        private static GameObject visualRoot;

        [YarnCommand("show_prison")]
        public static void ShowPrison(string emotionName)
        {
            HidePrison();
            Sprite sprite = ResolveSprite(emotionName);
            if (sprite == null)
            {
                Debug.LogWarning($"[PrisonConsultation] 感情の画像を取得できません: {emotionName}");
                return;
            }

            visualRoot = new GameObject("PrisonConsultationVisual", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(visualRoot);
            Canvas canvas = visualRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 背景Canvas(-10)より手前、会話UI(0)より奥へ置く。
            canvas.sortingOrder = -5;

            CanvasScaler scaler = visualRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image shade = CreateImage(visualRoot.transform, "Shade", null, new Color(0.015f, 0.02f, 0.035f, 0.96f));
            Stretch(shade.rectTransform);

            Image character = CreateImage(visualRoot.transform, "Emotion", sprite, Color.white);
            character.preserveAspect = true;
            RectTransform characterRect = character.rectTransform;
            characterRect.anchorMin = new Vector2(0.5f, 0.08f);
            characterRect.anchorMax = new Vector2(0.5f, 0.88f);
            characterRect.pivot = new Vector2(0.5f, 0.5f);
            characterRect.anchoredPosition = Vector2.zero;
            characterRect.sizeDelta = new Vector2(720f, 0f);

            RectTransform cage = new GameObject("Cage", typeof(RectTransform)).GetComponent<RectTransform>();
            cage.SetParent(visualRoot.transform, false);
            cage.anchorMin = new Vector2(0.5f, 0.08f);
            cage.anchorMax = new Vector2(0.5f, 0.90f);
            cage.pivot = new Vector2(0.5f, 0.5f);
            cage.anchoredPosition = Vector2.zero;
            cage.sizeDelta = new Vector2(760f, 0f);

            Color iron = new Color(0.12f, 0.14f, 0.18f, 0.96f);
            CreateCageBar(cage, "Top", new Vector2(0.5f, 1f), new Vector2(1f, 0f), new Vector2(0f, 30f), iron);
            CreateCageBar(cage, "Bottom", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0f, 30f), iron);
            for (int index = 0; index < 7; index++)
            {
                float x = index / 6f;
                CreateCageBar(cage, $"Bar{index + 1}", new Vector2(x, 0.5f), new Vector2(0f, 1f), new Vector2(24f, 0f), iron);
            }
        }

        [YarnCommand("hide_prison")]
        public static void HidePrison()
        {
            if (visualRoot == null) return;
            Object.Destroy(visualRoot);
            visualRoot = null;
        }

        private static Sprite ResolveSprite(string emotionName)
        {
            switch (emotionName?.ToLowerInvariant())
            {
                case "anger":
                    return Resources.Load<Sprite>("AngerBattle/AngerVertical");
                case "anxiety":
                    return Resources.Load<Sprite>("AngerBattle/AnxietyVertical");
                case "sadness":
                    SadnessBattleController controller = Object.FindFirstObjectByType<SadnessBattleController>(FindObjectsInactive.Include);
                    return controller != null && controller.sadnessActor != null
                        ? controller.sadnessActor.GetComponent<SpriteRenderer>()?.sprite
                        : null;
                default:
                    return null;
            }
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateCageBar(RectTransform parent, string name, Vector2 anchor, Vector2 stretch, Vector2 size, Color color)
        {
            Image bar = CreateImage(parent, name, null, color);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = anchor - stretch * 0.5f;
            rect.anchorMax = anchor + stretch * 0.5f;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}
