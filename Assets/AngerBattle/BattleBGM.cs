using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 怒り戦のBGM（Trick_style.mp3）再生を管理する。
    ///
    /// 曲全体をループするのではなく、指定した区間
    /// （loopStartSeconds 〜 loopEndSeconds）だけを繰り返しループ再生する。
    /// 敵が登場したタイミングでStopMusic()を呼んで停止する想定。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BattleBGM : MonoBehaviour
    {
        [Tooltip("再生するBGM（Trick_style.mp3 を想定）")]
        public AudioClip clip;

        [Tooltip("ループ開始位置（秒）。Trick_styleの場合は12秒")]
        public float loopStartSeconds = 12f;

        [Tooltip("ループ終了位置（秒）。ここまで来たらloopStartSecondsへ戻る。Trick_styleの場合は3:43.862 ≒ 223.862秒")]
        public float loopEndSeconds = 223.862f;

        private AudioSource audioSource;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            // ループはこのスクリプトが指定区間で手動管理するため、AudioSource自体のLoopはオフにする
            audioSource.loop = false;
        }

        /// <summary>ループ開始位置からBGMを再生する。</summary>
        public void PlayMusic()
        {
            if (clip == null)
            {
                Debug.LogWarning("[BattleBGM] AudioClipが設定されていません。");
                return;
            }

            audioSource.clip = clip;
            audioSource.time = loopStartSeconds;
            audioSource.Play();
        }

        /// <summary>BGMを停止する（敵登場のタイミングなどで呼ぶ）。</summary>
        public void StopMusic()
        {
            audioSource.Stop();
        }

        void Update()
        {
            if (audioSource.isPlaying && audioSource.time >= loopEndSeconds)
            {
                audioSource.time = loopStartSeconds;
            }
        }
    }
}
