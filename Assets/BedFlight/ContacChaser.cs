using System.Collections;
using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// 追ってくるコンタック本体。
    /// 怒り戦・不安戦でプレイヤー（コンタック）として使っている見た目（青い丸）をそのまま流用し、
    /// 「見覚えのある姿」として一目でコンタックだと分かるようにしている。
    ///
    /// 開放フェーズの後半（チラ見せフェーズ）では、画面端に一瞬だけ姿を見せて引っ込む
    /// （Peek）を繰り返して追われている緊張感を積み上げ、
    /// 最後にAppearFullyで本登場してから、BedFlightController側がセリフ表示・弾の発射を行う。
    /// </summary>
    public class ContacChaser : MonoBehaviour
    {
        [Tooltip("チラ見せ・本登場の見た目に使うSpriteRenderer")]
        public SpriteRenderer sprite;

        // シーン配置時のスケールを基準の等倍として覚えておく
        private Vector3 baseScale;

        [Tooltip("チラ見せ時の透明度（遠くにいる印象を出す）")]
        public float peekAlpha = 0.55f;
        [Tooltip("チラ見せ時のスケール倍率（基準スケールに対して）")]
        public float peekScale = 0.6f;
        [Tooltip("本登場時のスケール倍率（基準スケールに対して）")]
        public float appearScale = 1.4f;

        // 進行中のチラ見せ（Peek）コルーチン。本登場・非表示への切り替え時に
        // 古いPeekが「見せた後、勝手に引っ込める」ことで表示を壊さないよう、必ず止めてから切り替える
        private Coroutine peekRoutine;

        void Awake()
        {
            baseScale = transform.localScale;
            Hide();
        }

        public void Hide()
        {
            StopPeekIfRunning();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 指定位置に一瞬だけ姿を見せてから引っ込む。呼び出し元をブロックしない想定で使う
        /// （内部で自分自身にStartCoroutineする）。
        /// 非アクティブなGameObject上ではStartCoroutineが呼べないため、
        /// コルーチンを開始する前に必ず先にアクティブ化しておく。
        /// </summary>
        public void Peek(Vector3 position, float showDuration)
        {
            StopPeekIfRunning();

            gameObject.SetActive(true);
            transform.position = position;
            transform.localScale = baseScale * peekScale;
            SetAlpha(peekAlpha);

            peekRoutine = StartCoroutine(PeekRoutine(showDuration));
        }

        private IEnumerator PeekRoutine(float showDuration)
        {
            yield return new WaitForSeconds(showDuration);

            peekRoutine = null;
            gameObject.SetActive(false);
        }

        /// <summary>画面外（fromPosition）から定位置（targetPosition）まで、本登場としてゆっくりスライドインする。</summary>
        public IEnumerator AppearFully(Vector3 fromPosition, Vector3 targetPosition, float duration)
        {
            StopPeekIfRunning();

            gameObject.SetActive(true);
            transform.localScale = baseScale * appearScale;
            SetAlpha(1f);
            transform.position = fromPosition;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(fromPosition, targetPosition, t / duration);
                yield return null;
            }
            transform.position = targetPosition;
        }

        private void StopPeekIfRunning()
        {
            if (peekRoutine != null)
            {
                StopCoroutine(peekRoutine);
                peekRoutine = null;
            }
        }

        private void SetAlpha(float a)
        {
            if (sprite == null) return;
            Color c = sprite.color;
            c.a = a;
            sprite.color = c;
        }
    }
}
