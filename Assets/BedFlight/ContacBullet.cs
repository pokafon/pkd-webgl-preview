using System;
using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// コンタックがクライマックスで放つ一撃。
    /// 発射時にプレイヤーの実位置へ向かう方向を計算して直進する
    /// （このシーンでは避けさせるための弾ではなく、演出上必ず命中する一撃として使う想定。
    /// クライマックス中はBedFlightController側でプレイヤー操作を止めているため、狙いは外れない）。
    ///
    /// Collider2D（Is Trigger）が必須。ヒット対象はAngerBattle.PlayerControllerを持つオブジェクト。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ContacBullet : MonoBehaviour
    {
        [Tooltip("1秒あたりの移動速度")]
        public float speed = 10f;

        [Tooltip("この距離を超えて何にも当たらなかったら自動的に消える（保険）")]
        public float maxTravelDistance = 30f;

        /// <summary>プレイヤーに命中した時に呼ばれる</summary>
        public event Action OnHitPlayer;

        private Vector3 startPosition;
        private Vector3 direction = Vector3.left;

        /// <summary>targetPositionへ向かう方向を確定させ、以降その方向へ直進する。</summary>
        public void Fire(Vector3 targetPosition)
        {
            Vector3 diff = targetPosition - transform.position;
            if (diff.sqrMagnitude > 0.0001f)
            {
                direction = diff.normalized;
            }
        }

        void Start()
        {
            startPosition = transform.position;
        }

        void Update()
        {
            transform.position += direction * speed * Time.deltaTime;

            if (Vector3.Distance(startPosition, transform.position) > maxTravelDistance)
            {
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<AngerBattle.PlayerController>() != null)
            {
                OnHitPlayer?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
