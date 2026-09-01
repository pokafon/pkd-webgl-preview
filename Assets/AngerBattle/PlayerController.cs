using UnityEngine;
using UnityEngine.Tilemaps;

namespace AngerBattle
{
    /// <summary>
    /// コンタック（プレイヤー）の移動を制御する。
    /// 画面内の決まった範囲（minBounds〜maxBounds）でのみ、
    /// 上下左右キー（または矢印キー）で自由に動ける。
    ///
    /// 横スクロールでキャラ自身が右へ進むタイプではなく、
    /// プレイヤーはその場にとどまり、文字（弾）だけが右から左へ流れてくる方式。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("1秒あたりの移動速度")]
        public float speed = 6f;

        [Header("移動可能範囲（ワールド座標）")]
        [Tooltip("移動できる範囲の左下")]
        public Vector2 minBounds = new Vector2(-6f, -3.5f);

        [Tooltip("移動できる範囲の右上")]
        public Vector2 maxBounds = new Vector2(2f, 3.5f);

        [Tooltip("設定した場合、このTilemapにタイルがあるマスへは移動できない（未設定なら壁判定なし）")]
        public Tilemap wallTilemap;

        void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 move = new Vector3(h, v, 0f) * speed * Time.deltaTime;
            Vector3 current = transform.position;
            Vector3 nextPos = current + move;

            nextPos.x = Mathf.Clamp(nextPos.x, minBounds.x, maxBounds.x);
            nextPos.y = Mathf.Clamp(nextPos.y, minBounds.y, maxBounds.y);

            if (wallTilemap != null)
            {
                // 軸ごとに壁判定することで、壁に斜めに近づいても沿って動けるようにする。
                Vector3 xMoved = new Vector3(nextPos.x, current.y, current.z);
                if (IsBlocked(xMoved))
                {
                    nextPos.x = current.x;
                }

                Vector3 yMoved = new Vector3(nextPos.x, nextPos.y, current.z);
                if (IsBlocked(yMoved))
                {
                    nextPos.y = current.y;
                }
            }

            transform.position = nextPos;
        }

        private bool IsBlocked(Vector3 worldPosition)
        {
            return wallTilemap.HasTile(wallTilemap.WorldToCell(worldPosition));
        }
    }
}
