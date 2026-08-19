using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 怒り戦で右から左へ流れてくる「文字」1つ分のふるまい。
    ///
    /// プレイヤーに触れてもダメージやゲームオーバーにはならない
    /// （避けるのはあくまで演出目的で、ペナルティはない仕様）。
    /// 画面の左端まで流れきったら自動的に消える。
    /// </summary>
    public class FallingWord : MonoBehaviour
    {
        [Tooltip("1秒あたりの移動速度")]
        public float speed = 6f;

        [Tooltip("このX座標より左に出たら自動的に消える")]
        public float destroyXPosition = -12f;

        void Update()
        {
            transform.position += Vector3.left * speed * Time.deltaTime;

            if (transform.position.x < destroyXPosition)
            {
                Destroy(gameObject);
            }
        }
    }
}
