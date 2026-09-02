using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Yarn.Unity;

namespace PKD.Emotions
{
    public enum EmotionKind
    {
        Anger,
        Anxiety,
        Sadness
    }

    public enum EmotionOutcome
    {
        Unresolved,
        Isolated,
        Eliminated
    }

    /// <summary>一回のゲーム進行中に、三感情を隔離したか消去したかを保持する。</summary>
    public static class EmotionRouteState
    {
        public static EmotionOutcome Anger { get; private set; }
        public static EmotionOutcome Anxiety { get; private set; }
        public static EmotionOutcome Sadness { get; private set; }

        public static void Reset()
        {
            Anger = EmotionOutcome.Unresolved;
            Anxiety = EmotionOutcome.Unresolved;
            Sadness = EmotionOutcome.Unresolved;
        }

        public static void Set(EmotionKind kind, EmotionOutcome outcome)
        {
            switch (kind)
            {
                case EmotionKind.Anger: Anger = outcome; break;
                case EmotionKind.Anxiety: Anxiety = outcome; break;
                case EmotionKind.Sadness: Sadness = outcome; break;
            }
        }

        [YarnFunction("all_emotions_isolated")]
        public static bool AllEmotionsIsolated()
        {
            return Anger == EmotionOutcome.Isolated &&
                   Anxiety == EmotionOutcome.Isolated &&
                   Sadness == EmotionOutcome.Isolated;
        }
    }

    /// <summary>
    /// 感情戦共通の「隔離する／消去する」選択と、仮牢屋の落下演出。
    /// 専用アートへ差し替えるまで、実行時生成した鉄格子で演出する。
    /// </summary>
    public static class EmotionResolutionFlow
    {
        private static Sprite whiteSprite;

        public static IEnumerator Choose(
            MonoBehaviour host,
            EmotionKind kind,
            Transform target,
            TMP_Text lineText,
            TMP_Text nameText,
            GameObject lineBackground,
            Action<EmotionOutcome> onChosen)
        {
            int selected = 0;
            if (nameText != null)
            {
                nameText.text = string.Empty;
                nameText.gameObject.SetActive(false);
            }
            if (lineBackground != null) lineBackground.SetActive(true);
            if (lineText != null) lineText.gameObject.SetActive(true);

            UpdateChoiceText(lineText, selected);
            yield return null;
            while (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return)) yield return null;

            bool decided = false;
            while (!decided)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    selected = 0;
                    UpdateChoiceText(lineText, selected);
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    selected = 1;
                    UpdateChoiceText(lineText, selected);
                }
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    decided = true;
                }
                yield return null;
            }

            EmotionOutcome outcome = selected == 0 ? EmotionOutcome.Isolated : EmotionOutcome.Eliminated;
            EmotionRouteState.Set(kind, outcome);
            if (lineText != null) lineText.gameObject.SetActive(false);
            if (lineBackground != null) lineBackground.SetActive(false);

            if (outcome == EmotionOutcome.Isolated && target != null)
            {
                yield return host.StartCoroutine(PlayIsolation(target));
            }
            onChosen?.Invoke(outcome);
        }

        private static void UpdateChoiceText(TMP_Text text, int selected)
        {
            if (text == null) return;
            text.text = selected == 0
                ? "どうする？\n\n▶ 隔離する\n　消去する"
                : "どうする？\n\n　隔離する\n▶ 消去する";
        }

        private static IEnumerator PlayIsolation(Transform target)
        {
            SpriteRenderer targetRenderer = target.GetComponentInChildren<SpriteRenderer>();
            Bounds bounds = targetRenderer != null
                ? targetRenderer.bounds
                : new Bounds(target.position, new Vector3(2f, 3f, 0f));

            float width = Mathf.Clamp(bounds.size.x + 0.65f, 1.8f, 5.2f);
            float height = Mathf.Clamp(bounds.size.y + 0.55f, 2.4f, 7.2f);
            Vector3 landing = new Vector3(bounds.center.x, bounds.center.y, target.position.z);
            GameObject cage = BuildCage(width, height, targetRenderer != null ? targetRenderer.sortingOrder + 20 : 20);
            cage.transform.SetParent(target.parent, true);
            cage.transform.position = landing + Vector3.up * Mathf.Max(6f, height + 2f);

            float duration = 0.55f;
            float elapsed = 0f;
            Vector3 start = cage.transform.position;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                cage.transform.position = Vector3.LerpUnclamped(start, landing, eased);
                yield return null;
            }
            cage.transform.position = landing;
            PlayLockSound(cage);

            Vector3 baseScale = cage.transform.localScale;
            cage.transform.localScale = new Vector3(baseScale.x * 1.08f, baseScale.y * 0.92f, 1f);
            float impactElapsed = 0f;
            while (impactElapsed < 0.16f)
            {
                impactElapsed += Time.unscaledDeltaTime;
                cage.transform.localScale = Vector3.Lerp(cage.transform.localScale, baseScale, impactElapsed / 0.16f);
                yield return null;
            }
            cage.transform.localScale = baseScale;
            yield return new WaitForSecondsRealtime(0.65f);
            UnityEngine.Object.Destroy(cage);
        }

        private static GameObject BuildCage(float width, float height, int sortingOrder)
        {
            EnsureWhiteSprite();
            GameObject root = new GameObject("IsolationCage");
            Color iron = new Color(0.16f, 0.18f, 0.22f, 1f);
            float thickness = 0.13f;
            AddBar(root.transform, "Top", new Vector2(0f, height * 0.5f), new Vector2(width, thickness * 1.5f), iron, sortingOrder);
            AddBar(root.transform, "Bottom", new Vector2(0f, -height * 0.5f), new Vector2(width, thickness * 1.5f), iron, sortingOrder);
            for (int index = 0; index < 6; index++)
            {
                float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, index / 5f);
                AddBar(root.transform, $"Bar{index + 1}", new Vector2(x, 0f), new Vector2(thickness, height), iron, sortingOrder);
            }
            return root;
        }

        private static void AddBar(Transform parent, string name, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            GameObject bar = new GameObject(name, typeof(SpriteRenderer));
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = position;
            bar.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = bar.GetComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static void EnsureWhiteSprite()
        {
            if (whiteSprite != null) return;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "RuntimeCagePixel";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "RuntimeCageSprite";
        }

        private static void PlayLockSound(GameObject cage)
        {
            AudioSource source = cage.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0.72f;
            source.clip = BuildLockClip();
            source.Play();
        }

        private static AudioClip BuildLockClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.34f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 14f);
                float metal = Mathf.Sin(2f * Mathf.PI * 105f * time) + 0.45f * Mathf.Sin(2f * Mathf.PI * 318f * time);
                samples[index] = metal * envelope * 0.35f;
            }
            AudioClip clip = AudioClip.Create("IsolationLock", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
