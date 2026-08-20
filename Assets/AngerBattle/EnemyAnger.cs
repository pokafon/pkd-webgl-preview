using System;
using System.Collections;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 「怒り」本体。
    /// 台詞（文字の弾幕）が流れきった後にSetPresent(true)で登場し、
    /// プレイヤーの攻撃弾（DenialBullet）を受けるとOnDefeatedイベントを発火する。
    /// 登場時は、シーンに配置されている定位置へ向かって右側からゆっくりスライドしてくる。
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

        [Header("登場演出")]
        [Tooltip("シーンに配置した定位置から、この距離だけ右側の画面外からスライドして登場する")]
        public float appearFromOffsetX = 6f;
        [Tooltip("登場時に定位置までスライドしてくるのにかかる時間（秒）")]
        public float appearDuration = 0.6f;

        [Header("撃破演出")]
        [Tooltip("撃破時に差し替える白いスプライト（未設定なら見た目は変わらない。素材のColorは乗算ティントのため、色付きスプライトをColor変更だけで白くすることはできない）")]
        public Sprite defeatedSprite;

        private int currentHits = 0;
        private bool isPresent = false;
        private Vector3 restingPosition;
        private Coroutine appearCoroutine;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Sprite originalSprite;

        void Awake()
        {
            // シーンに配置されている位置を「定位置」として覚えておく
            restingPosition = transform.position;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                originalSprite = spriteRenderer.sprite;
            }

            // 最初は非表示（台詞が流れきるまで登場しない）
            SetPresent(false);
        }

        /// <summary>怒り本体の登場・非表示を切り替える。登場のたびにヒット数はリセットされる。</summary>
        public void SetPresent(bool present)
        {
            isPresent = present;
            currentHits = 0;

            if (appearCoroutine != null)
            {
                StopCoroutine(appearCoroutine);
                appearCoroutine = null;
            }

            if (present)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = originalColor;
                    spriteRenderer.sprite = originalSprite;
                }
                gameObject.SetActive(true);
                appearCoroutine = StartCoroutine(AppearFromRight());
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public bool IsPresent()
        {
            return isPresent;
        }

        /// <summary>定位置より右側の画面外から、定位置までゆっくりスライドしてくる。</summary>
        private IEnumerator AppearFromRight()
        {
            Vector3 start = restingPosition + new Vector3(appearFromOffsetX, 0f, 0f);
            transform.position = start;

            float t = 0f;
            while (t < appearDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, restingPosition, t / appearDuration);
                yield return null;
            }
            transform.position = restingPosition;
        }

        /// <summary>攻撃弾がヒットした時にDenialBulletから呼ばれる。</summary>
        public void TakeDamage()
        {
            if (!isPresent) return;

            currentHits++;
            if (currentHits >= hitsToDefeatThisTurn)
            {
                // 撃破演出：非表示にはせず、白いスプライトに差し替えるだけ
                // （素材のColorは乗算ティントなので、色付きスプライトのままColorを白にしても見た目は変わらない）
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.white;
                    if (defeatedSprite != null)
                    {
                        spriteRenderer.sprite = defeatedSprite;
                    }
                }
                OnDefeated?.Invoke();
            }
        }
    }
}
