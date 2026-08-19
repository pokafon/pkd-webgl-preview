using System;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 「怒り」本体。
    /// 台詞（文字の弾幕）が流れきった後にSetPresent(true)で登場し、
    /// プレイヤーの攻撃弾（DenialBullet）を受けるとOnDefeatedイベントを発火する。
    ///
    /// 「それは異常です」というセリフの表示は、このスクリプトではなく
    /// AngerBattleController側（AttackLineText）が、登場と同時に自動で行う。
    /// このスクリプトはあくまで「怒り本体」の登場・被弾・撃破だけを担当する。
    ///
    /// このスクリプトを付けるオブジェクトには、
    /// DenialBulletのOnTriggerEnter2Dで検出できるようCollider2D（Is Trigger推奨）が必要。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyAnger : MonoBehaviour
    {
        /// <summary>このターンで倒された時に呼ばれる</summary>
        public event Action OnDefeated;

        [Tooltip("このターンで倒すために必要なヒット数（仕様上は基本1）")]
        public int hitsToDefeatThisTurn = 1;

        private int currentHits = 0;
        private bool isPresent = false;

        void Awake()
        {
            // 最初は非表示（台詞が流れきるまで登場しない）
            SetPresent(false);
        }

        /// <summary>怒り本体の登場・非表示を切り替える。登場のたびにヒット数はリセットされる。</summary>
        public void SetPresent(bool present)
        {
            isPresent = present;
            gameObject.SetActive(present);
            currentHits = 0;
        }

        public bool IsPresent()
        {
            return isPresent;
        }

        /// <summary>攻撃弾がヒットした時にDenialBulletから呼ばれる。</summary>
        public void TakeDamage()
        {
            if (!isPresent) return;

            currentHits++;
            if (currentHits >= hitsToDefeatThisTurn)
            {
                OnDefeated?.Invoke();
            }
        }
    }
}
