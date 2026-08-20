using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AngerBattle
{
    /// <summary>
    /// 精神世界パート（怒り戦・不安戦とも）に入る直前に毎回挟む導入演出。
    /// キャラクター表示は出さず、時計とノイズと背景ブラックアウトのみで構成する。
    ///
    /// 時計はstartTime（就寝目安、0:00）からendTime（朝方の目安、4:03）まで、
    /// 一定の速いテンポ（tickInterval間隔）でカチカチと前にしか進まない時刻を刻み続ける
    /// （固定の数個の時刻で止まるのではなく、連続的に多数の時刻を通過する）。
    /// 時計の文字は演出全体を通じて途切れずズームイン（startScale→endScale）しながら
    /// ガタツキ（ジッター）で震え、背景は徐々にブラックアウトしていく
    /// （画面のグリッチノイズ（noiseBlocks）も同時に段階的に増える）。
    /// 背景が完全に黒くなった後も、blackHoldDuration秒ほど時計を刻み続けながら
    /// 黒いままにしてから終了する。演出が終わり切ると画面は真っ暗になっており、
    /// そのまま一瞬で本編に切り替わる。
    /// </summary>
    public class ClockGlitchIntro : MonoBehaviour
    {
        [Header("参照")]
        public TMP_Text clockText;
        [Tooltip("演出の背後を覆う黒背景パネル（演出中に段々ブラックアウトさせる）")]
        public Image backgroundPanel;
        [Tooltip("あらかじめ用意しておくグリッチ用の矩形（普段は非アクティブ）")]
        public RectTransform[] noiseBlocks;
        public AudioSource tickAudioSource;
        public AudioClip tickClip;

        [Header("時刻（前にしか進まない。startTimeが就寝目安、endTimeが朝方の目安）")]
        public string startTime = "0:00";
        public string endTime = "4:03";

        [Header("演出全体の尺とテンポ")]
        [Tooltip("演出全体の長さ（秒）")]
        [Range(1f, 20f)]
        public float totalDuration = 6f;
        [Tooltip("カチカチと刻む間隔（秒）。最初から一定の速いテンポで刻み続ける")]
        [Range(0.02f, 1f)]
        public float tickInterval = 0.12f;

        [Header("時計の文字のズームイン（演出全体を通じて連続的に拡大）")]
        [Tooltip("開始時の拡大率")]
        [Range(0.5f, 5f)]
        public float startScale = 1f;
        [Tooltip("終了時の拡大率")]
        [Range(0.5f, 10f)]
        public float endScale = 3f;
        [Tooltip("時計の文字のガタツキ（ジッター）の振れ幅（ピクセル）")]
        [Range(0f, 50f)]
        public float jitterAmount = 6f;

        [Header("ノイズ（進行につれて段階的に増える）")]
        [Tooltip("開始時に同時表示するノイズ矩形の数")]
        [Range(0, 10)]
        public int noiseBlockCountStart = 1;
        [Tooltip("終了時に同時表示するノイズ矩形の数")]
        [Range(0, 10)]
        public int noiseBlockCountEnd = 10;

        [Header("背景ブラックアウト（演出全体を通じて0→1へ）")]
        [Tooltip("開始時の背景の不透明度（0=透明）")]
        [Range(0f, 1f)]
        public float backgroundStartAlpha = 0f;
        [Tooltip("終了時の背景の不透明度（1=真っ黒）")]
        [Range(0f, 1f)]
        public float backgroundEndAlpha = 1f;

        [Tooltip("背景が完全に黒くなった後も、時計を刻み続けながら黒いままにする追加時間（秒）")]
        [Range(0f, 10f)]
        public float blackHoldDuration = 1.5f;

        /// <summary>この演出を最初から最後まで再生する。終わるまで呼び出し元をブロックする想定。</summary>
        public IEnumerator Play()
        {
            gameObject.SetActive(true);
            HideAllNoiseBlocks();
            SetBackgroundAlpha(backgroundStartAlpha);
            if (clockText != null)
            {
                clockText.rectTransform.anchoredPosition = Vector2.zero;
            }

            int startMinutes = ParseToMinutes(startTime);
            int endMinutes = ParseToMinutes(endTime);
            Vector3 startScaleVector = Vector3.one * startScale;
            Vector3 endScaleVector = Vector3.one * endScale;

            int tickCount = Mathf.Max(1, Mathf.RoundToInt(totalDuration / tickInterval));

            for (int i = 0; i < tickCount; i++)
            {
                float progress = (float)(i + 1) / tickCount;
                int minutes = startMinutes + Mathf.RoundToInt((endMinutes - startMinutes) * progress);

                SetClockText(FormatMinutes(minutes));

                if (clockText != null)
                {
                    clockText.rectTransform.localScale = Vector3.Lerp(startScaleVector, endScaleVector, progress);
                    clockText.rectTransform.anchoredPosition = new Vector2(
                        Random.Range(-jitterAmount, jitterAmount),
                        Random.Range(-jitterAmount, jitterAmount)
                    );
                }

                int noiseCount = Mathf.RoundToInt(Mathf.Lerp(noiseBlockCountStart, noiseBlockCountEnd, progress));
                RandomizeNoiseBlocks(noiseCount);

                SetBackgroundAlpha(Mathf.Lerp(backgroundStartAlpha, backgroundEndAlpha, progress));

                PlayTick();

                yield return new WaitForSeconds(tickInterval);
            }

            // --- 完全に黒くなった後も、時計を刻み続けながらしばらく黒いままにする ---
            float perTickMinutes = (float)(endMinutes - startMinutes) / tickCount;
            float continuingMinutes = endMinutes;
            int extraTicks = Mathf.Max(0, Mathf.RoundToInt(blackHoldDuration / tickInterval));

            for (int i = 0; i < extraTicks; i++)
            {
                continuingMinutes += perTickMinutes;
                SetClockText(FormatMinutes(Mathf.RoundToInt(continuingMinutes)));

                if (clockText != null)
                {
                    clockText.rectTransform.anchoredPosition = new Vector2(
                        Random.Range(-jitterAmount, jitterAmount),
                        Random.Range(-jitterAmount, jitterAmount)
                    );
                }

                PlayTick();

                yield return new WaitForSeconds(tickInterval);
            }

            HideAllNoiseBlocks();
            if (clockText != null)
            {
                clockText.rectTransform.localScale = startScaleVector;
                clockText.rectTransform.anchoredPosition = Vector2.zero;
            }
            gameObject.SetActive(false);
        }

        private void SetClockText(string text)
        {
            if (clockText != null)
            {
                clockText.text = text;
            }
        }

        private void SetBackgroundAlpha(float alpha)
        {
            if (backgroundPanel == null) return;

            Color color = backgroundPanel.color;
            color.a = alpha;
            backgroundPanel.color = color;
        }

        private void PlayTick()
        {
            if (tickAudioSource != null && tickClip != null)
            {
                tickAudioSource.PlayOneShot(tickClip);
            }
        }

        private static int ParseToMinutes(string time)
        {
            string[] parts = time.Split(':');
            int hour = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int minute = int.Parse(parts[1], CultureInfo.InvariantCulture);
            return hour * 60 + minute;
        }

        private static string FormatMinutes(int totalMinutes)
        {
            int hour = (totalMinutes / 60) % 24;
            int minute = totalMinutes % 60;
            return $"{hour}:{minute:00}";
        }

        private void RandomizeNoiseBlocks(int visibleCount)
        {
            if (noiseBlocks == null) return;

            RectTransform area = transform as RectTransform;
            float halfWidth = area != null ? area.rect.width / 2f : 960f;
            float halfHeight = area != null ? area.rect.height / 2f : 540f;

            for (int i = 0; i < noiseBlocks.Length; i++)
            {
                RectTransform block = noiseBlocks[i];
                if (block == null) continue;

                bool visible = i < visibleCount;
                block.gameObject.SetActive(visible);
                if (!visible) continue;

                block.sizeDelta = new Vector2(Random.Range(60f, 480f), Random.Range(6f, 32f));
                block.anchoredPosition = new Vector2(
                    Random.Range(-halfWidth, halfWidth),
                    Random.Range(-halfHeight, halfHeight)
                );
            }
        }

        private void HideAllNoiseBlocks()
        {
            if (noiseBlocks == null) return;

            foreach (var block in noiseBlocks)
            {
                if (block != null) block.gameObject.SetActive(false);
            }
        }
    }
}
