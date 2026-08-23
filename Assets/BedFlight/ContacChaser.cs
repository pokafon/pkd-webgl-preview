using System.Collections;
using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// 追ってくるコンタック本体。
    /// 怒り戦・不安戦でプレイヤー（コンタック）として使っている見た目（青い丸）をそのまま流用し、
    /// 「見覚えのある姿」として一目でコンタックだと分かるようにしている。
    ///
    /// 開放フェーズの後、AppearFullyで本登場してから、BedFlightController側がセリフ表示・弾の発射を行う。
    /// </summary>
    public class ContacChaser : MonoBehaviour
    {
        [Tooltip("本登場の見た目に使うSpriteRenderer")]
        public SpriteRenderer sprite;

        // シーン配置時のスケールを基準の等倍として覚えておく
        private Vector3 baseScale;

        [Tooltip("本登場時のスケール倍率（基準スケールに対して）")]
        public float appearScale = 1.4f;

        // 本登場時のSorting Order。シーン配置時の値（手前に出す想定）をそのまま使う
        private int appearSortingOrder;

        void Awake()
        {
            baseScale = transform.localScale;
            if (sprite != null) appearSortingOrder = sprite.sortingOrder;
            Hide();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>画面外（fromPosition）から定位置（targetPosition）まで、本登場としてゆっくりスライドインする。</summary>
        public IEnumerator AppearFully(Vector3 fromPosition, Vector3 targetPosition, float duration)
        {
            gameObject.SetActive(true);
            transform.localScale = baseScale * appearScale;
            SetAlpha(1f);
            if (sprite != null) sprite.sortingOrder = appearSortingOrder;
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

        private void SetAlpha(float a)
        {
            if (sprite == null) return;
            Color c = sprite.color;
            c.a = a;
            sprite.color = c;
        }
    }
}
