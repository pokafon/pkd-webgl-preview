using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// プレイヤーがSpaceキーで発射する攻撃弾。
    /// 見た目はシンプルな弾（「それは異常です」というセリフ表示とは別物）。
    /// セリフの表示自体はAngerBattleController側が、怒りの登場と同時に自動で行う。
    ///
    /// 既定では右方向へ直進する。怒り戦の縦型レイアウトではConfigureで上方向へ切り替える。
    ///
    /// Collider2D（Is Trigger）が必須。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DenialBullet : MonoBehaviour
    {
        [Tooltip("1秒あたりの移動速度")]
        public float speed = 12f;

        [Tooltip("この距離を超えて何にも当たらなかったら自動的に消える（保険）")]
        public float maxTravelDistance = 30f;

        private Vector3 startPosition;
        private Vector2 direction = Vector2.right;
        private bool consumed;

        /// <summary>発射方向と色を、生成直後に戦闘側から指定する。</summary>
        public void Configure(Vector2 travelDirection, Color color)
        {
            direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.right;
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        void Start()
        {
            startPosition = transform.position;
        }

        void Update()
        {
            transform.position += (Vector3)direction * speed * Time.deltaTime;

            if (Vector3.Distance(startPosition, transform.position) > maxTravelDistance)
            {
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed) return;

            EnemyAnger enemy = other.GetComponent<EnemyAnger>();
            if (enemy != null && enemy.IsPresent())
            {
                consumed = true;
                enemy.TakeDamage();
                Destroy(gameObject);
            }
        }
    }
}
