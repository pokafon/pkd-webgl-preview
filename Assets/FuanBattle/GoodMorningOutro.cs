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
        [Tooltip("白へフェードインする時間（秒）")]
        [Range(0.1f, 10f)]
        public float fadeInDuration = 1.2f;
        [Tooltip("真っ白なまま静止する時間（秒）")]
        [Range(0f, 10f)]
        public float holdDuration = 1.5f;
        [Tooltip("白から透明へフェードアウトする時間（秒）")]
        [Range(0.1f, 10f)]
        public float fadeOutDuration = 1.5f;

        /// <summary>
        /// この演出を最初から最後まで再生する。onRevealは、画面が真っ白な静止中に
        /// 呼び出し元が背後の画面を切り替えるためのコールバック（フェードアウトで自然に見えてくる）。
        /// skipFadeInをtrueにすると、フェードインをせず最初から不透明（白）で始める
        /// （直前に別の演出（目覚めの時計など）が既に画面を白く覆っている場合、
        /// 継ぎ目なく繋げるために使う）。
        /// </summary>
        public IEnumerator Play(Action onReveal, bool skipFadeIn = false)
        {
            PlayChime();

            float t;
            if (skipFadeIn)
            {
                SetAlpha(1f);
            }
            else
            {
                SetAlpha(0f);
                t = 0f;
                while (t < fadeInDuration)
                {
                    t += Time.deltaTime;
                    SetAlpha(Mathf.Clamp01(t / fadeInDuration));
                    yield return null;
                }
                SetAlpha(1f);
            }

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

        /// <summary>
        /// ベル音だけを単独で鳴らす（撃破後のレベルアップ演出等、この演出本体を再生しない場面用）。
        /// このGameObjectは普段非アクティブにしてあるため、非アクティブなままだと
        /// AudioSource.PlayOneShotが音を鳴らさない（エラーも出ない）。先に必ずアクティブ化する。
        /// </summary>
        public void PlayChime()
        {
            PlaySound(chimeClip);
        }

        /// <summary>
        /// chimeAudioSourceを使って任意のクリップを単独で鳴らす（撃破直後のレベルアップ演出など、
        /// この演出本体とは別の音を、同じAudioSourceで鳴らしたい場面用）。
        /// </summary>
        public void PlaySound(AudioClip clip)
        {
            gameObject.SetActive(true);

            if (chimeAudioSource != null && clip != null)
            {
                chimeAudioSource.PlayOneShot(clip);
            }
        }
    }
}
