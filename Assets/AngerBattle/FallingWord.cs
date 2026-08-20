using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 怒り戦・不安戦などで右から左へ流れてくる「文字」1つ分のふるまい。
    ///
    /// プレイヤーに触れてもダメージやゲームオーバーにはならない
    /// （避けるのはあくまで演出目的で、ペナルティはない仕様）。
    /// 画面の左端まで流れきったら自動的に消える。
    ///
    /// 通常は右から左への直線移動（怒り戦）。
    /// erraticMovementをtrueにすると、不規則に方向転換しながら
    /// 加減速する「蛇行」移動になる（不安戦）。
    /// </summary>
    public class FallingWord : MonoBehaviour
    {
        [Tooltip("1秒あたりの移動速度（直線移動時。蛇行移動時はerraticSpeedRangeを使う）")]
        public float speed = 6f;

        [Tooltip("このX座標より左に出たら自動的に消える")]
        public float destroyXPosition = -12f;

        [Header("蛇行移動（不安戦など）")]
        [Tooltip("trueにすると、不規則に方向転換しながら加減速する動きになる")]
        public bool erraticMovement = false;

        [Tooltip("蛇行：方向・速度を変える間隔（秒）の基準値")]
        public float erraticChangeInterval = 0.3f;

        [Tooltip("蛇行：方向転換の角度範囲（度）。180=左方向を基準に、±この角度でばらける")]
        public float erraticAngleSpread = 50f;

        [Tooltip("蛇行：変化ごとの速度範囲")]
        public Vector2 erraticSpeedRange = new Vector2(3f, 11f);

        [Tooltip("蛇行：このY座標の範囲を超えないよう、上下の画面端で跳ね返る（画面外に出て読めなくなるのを防ぐ）")]
        public Vector2 erraticVerticalBounds = new Vector2(-4f, 4f);

        [Header("追い越し防止（同じ台詞内の文字順を保つ）")]
        [Tooltip("同じ台詞内で、この文字より先に出た文字。nullなら制約なし")]
        public FallingWord leader;

        [Tooltip("leaderより右側（x座標が大きい側）にこの間隔以上を保つ。leaderに追いつきそうになったらここで足止めされる")]
        public float minLeaderGap = 0.5f;

        private Vector2 erraticDirection = Vector2.left;
        private float erraticSpeed;
        private float erraticTimer;

        void Start()
        {
            erraticSpeed = speed;
        }

        void Update()
        {
            Vector3 nextPos;

            if (erraticMovement)
            {
                erraticTimer -= Time.deltaTime;
                if (erraticTimer <= 0f)
                {
                    erraticTimer = erraticChangeInterval + Random.Range(-0.1f, 0.15f);
                    float angleDeg = 180f + Random.Range(-erraticAngleSpread, erraticAngleSpread);
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    erraticDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                    erraticSpeed = Random.Range(erraticSpeedRange.x, erraticSpeedRange.y);
                }

                nextPos = transform.position + (Vector3)(erraticDirection * (erraticSpeed * Time.deltaTime));

                // 上下の画面端を超えそうになったら跳ね返す（読めない位置まで飛んでいくのを防ぐ）
                if (nextPos.y > erraticVerticalBounds.y)
                {
                    nextPos.y = erraticVerticalBounds.y;
                    erraticDirection.y = -Mathf.Abs(erraticDirection.y);
                }
                else if (nextPos.y < erraticVerticalBounds.x)
                {
                    nextPos.y = erraticVerticalBounds.x;
                    erraticDirection.y = Mathf.Abs(erraticDirection.y);
                }
            }
            else
            {
                nextPos = transform.position + Vector3.left * speed * Time.deltaTime;
            }

            // 同じ台詞内で先に出た文字（leader）を追い越さないよう、x座標を足止めする
            if (leader != null)
            {
                float minX = leader.transform.position.x + minLeaderGap;
                if (nextPos.x < minX)
                {
                    nextPos.x = minX;
                }
            }

            transform.position = nextPos;

            if (transform.position.x < destroyXPosition)
            {
                Destroy(gameObject);
            }
        }
    }
}
