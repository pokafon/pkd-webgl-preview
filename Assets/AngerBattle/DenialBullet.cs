using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// プレイヤーがEnterキーで発射する攻撃弾。
    /// 見た目はシンプルな弾（「それは異常です」というセリフ表示とは別物）。
    /// セリフの表示自体はAngerBattleController側が、怒りの登場と同時に自動で行う。
    ///
    /// 右方向へ直進し、EnemyAngerに当たるとダメージを与えて消える。
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

        void Start()
        {
            startPosition = transform.position;
        }

        void Update()
        {
            transform.position += Vector3.right * speed * Time.deltaTime;

            if (Vector3.Distance(startPosition, transform.position) > maxTravelDistance)
            {
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            EnemyAnger enemy = other.GetComponent<EnemyAnger>();
            if (enemy != null && enemy.IsPresent())
            {
                enemy.TakeDamage();
                Destroy(gameObject);
            }
        }
    }
}
