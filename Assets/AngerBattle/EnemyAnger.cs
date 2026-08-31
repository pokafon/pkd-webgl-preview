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
        /// <summary>ダメージを受けた直後に、残りHPと最大HPを通知する。</summary>
        public event Action<int, int> OnHealthChanged;

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
        private bool isDefeated;
        private bool damageEnabled = true;
        private bool useDefeatedSprite = true;

        public int CurrentHealth => Mathf.Max(0, hitsToDefeatThisTurn - currentHits);
        public int MaxHealth => Mathf.Max(1, hitsToDefeatThisTurn);

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
            SetPresent(present, true);
        }

        /// <summary>登場アニメーションの有無を指定して表示状態を切り替える。</summary>
        public void SetPresent(bool present, bool animate)
        {
            isPresent = present;
            currentHits = 0;
            isDefeated = false;
            damageEnabled = true;

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
                if (animate)
                {
                    appearCoroutine = StartCoroutine(AppearFromRight());
                }
                else
                {
                    restingPosition = transform.position;
                }
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

        /// <summary>怒り戦用にHP数と登場方法を指定して戦闘を始める。</summary>
        public void BeginBattle(int hitPoints, bool animate, bool swapSpriteWhenDefeated)
        {
            hitsToDefeatThisTurn = Mathf.Max(1, hitPoints);
            useDefeatedSprite = swapSpriteWhenDefeated;
            restingPosition = transform.position;
            SetPresent(true, animate);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>節目のセリフ中など、一時的に連続被弾を止める。</summary>
        public void SetDamageEnabled(bool enabled)
        {
            damageEnabled = enabled;
        }

        /// <summary>縦型戦闘で使う見た目を、このオブジェクトの通常スプライトとして設定する。</summary>
        public void SetBattleSprite(Sprite sprite)
        {
            if (sprite == null || spriteRenderer == null)
            {
                return;
            }

            originalSprite = sprite;
            spriteRenderer.sprite = sprite;
            originalColor = Color.white;
            spriteRenderer.color = originalColor;
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
            if (!isPresent || isDefeated || !damageEnabled) return;

            currentHits++;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (currentHits >= hitsToDefeatThisTurn)
            {
                isDefeated = true;
                // 撃破演出：非表示にはせず、白いスプライトに差し替えるだけ
                // （素材のColorは乗算ティントなので、色付きスプライトのままColorを白にしても見た目は変わらない）
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.white;
                    if (useDefeatedSprite && defeatedSprite != null)
                    {
                        spriteRenderer.sprite = defeatedSprite;
                    }
                }
                OnDefeated?.Invoke();
            }
        }
    }
}
