using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AngerBattle
{
    /// <summary>
    /// 精神世界パート（怒り戦・不安戦とも）で敵を撃破した後に挟む、目覚めの合図演出。
    /// 時計演出（ClockGlitchIntro）とは対照的に、ジッターなし・ゆっくり静かに進行する。
    ///
    /// 白背景へゆっくりフェードイン（ベル音を鳴らす）→少し静止→onRevealを呼んで
    /// 背後の画面を会話画面に切り替え（このタイミングは画面が真っ白なので切り替えは見えない）
    /// →白から徐々に透明になり、会話画面へ溶け込むように戻る。
    /// </summary>
    public class GoodMorningOutro : MonoBehaviour
    {
        [Header("参照")]
        public Image backgroundPanel;
        public TMP_Text label;
        public AudioSource chimeAudioSource;
        public AudioClip chimeClip;

        [Header("タイミング")]
        public float fadeInDuration = 1.2f;
        public float holdDuration = 1.5f;
        public float fadeOutDuration = 1.5f;

        /// <summary>
        /// この演出を最初から最後まで再生する。onRevealは、画面が真っ白な静止中に
        /// 呼び出し元が背後の画面を切り替えるためのコールバック（フェードアウトで自然に見えてくる）。
        /// </summary>
        public IEnumerator Play(Action onReveal)
        {
            gameObject.SetActive(true);
            SetAlpha(0f);
            PlayChime();

            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Clamp01(t / fadeInDuration));
                yield return null;
            }
            SetAlpha(1f);

            yield return new WaitForSeconds(holdDuration);

            onReveal?.Invoke();

            t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                SetAlpha(1f - Mathf.Clamp01(t / fadeOutDuration));
                yield return null;
            }
            SetAlpha(0f);

            gameObject.SetActive(false);
        }

        private void SetAlpha(float alpha)
        {
            if (backgroundPanel != null)
            {
                Color bgColor = backgroundPanel.color;
                bgColor.a = alpha;
                backgroundPanel.color = bgColor;
            }
            if (label != null)
            {
                Color textColor = label.color;
                textColor.a = alpha;
                label.color = textColor;
            }
        }

        private void PlayChime()
        {
            if (chimeAudioSource != null && chimeClip != null)
            {
                chimeAudioSource.PlayOneShot(chimeClip);
            }
        }
    }
}
