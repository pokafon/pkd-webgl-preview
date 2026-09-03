using System;
using System.Collections;
using PKD.Emotions;
using TMPro;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 縦型の怒り戦を管理する。
    /// 上側の怒りが赤い弾幕を撃ち、コンタックが青い弾で12HPを削る。
    /// 3発ごとの節目で怒りの台詞を通常表示し、弾幕を次のパターンへ切り替える。
    /// </summary>
    public class AngerBattleController : MonoBehaviour
    {
        [Header("参照")]
        public PlayerController player;
        public EnemyAnger enemy;
        [Tooltip("BGM（Trick_style）の再生を管理するコンポーネント")]
        public BattleBGM bgm;
        [Tooltip("攻撃弾の発射位置。未設定ならプレイヤーの位置から発射する")]
        public Transform bulletSpawnPoint;
        [Tooltip("Collider2D（Is Trigger）付きのDenialBulletプレハブ")]
        public GameObject denialBulletPrefab;
        [Tooltip("Hierarchy上で直接調整できる怒り戦専用HUD")]
        public AngerBattleHUD hud;

        [Header("縦型レイアウト")]
        public Vector3 playerBattlePosition = new Vector3(0f, -3.15f, 0f);
        public Vector3 enemyBattlePosition = new Vector3(0f, 3.05f, 0f);
        public Vector3 playerBattleScale = new Vector3(0.36f, 0.36f, 1f);
        public Vector3 enemyBattleScale = new Vector3(0.5f, 0.5f, 1f);
        [Tooltip("怒り戦中のコンタック移動速度")]
        public float playerBattleSpeed = 6.7f;
        [Tooltip("キャラクター全身と画面端の間に残すワールド座標の余白")]
        public float screenEdgeMargin = 0.12f;
        [Tooltip("未設定ならResources/AngerBattle/ContackVerticalを読み込む")]
        public Sprite playerBattleSprite;
        [Tooltip("未設定ならResources/AngerBattle/AngerVerticalを読み込む")]
        public Sprite enemyBattleSprite;
        [Tooltip("プレイヤーと怒り本体の描画順")]
        public int combatantSortingOrder = 10;
        [Tooltip("怒りが放つ赤い弾の描画順")]
        public int enemyBulletSortingOrder = 20;
        [Tooltip("コンタックが放つ青い弾の描画順")]
        public int playerBulletSortingOrder = 21;
        [Tooltip("カメラから表示範囲を取得できない場合に使う移動範囲")]
        public Vector2 fallbackPlayerMinBounds = new Vector2(-4.6f, -4.6f);
        public Vector2 fallbackPlayerMaxBounds = new Vector2(4.6f, 4.6f);

        [Header("HPと節目の台詞")]
        public int enemyMaxHealth = 12;
        public int playerMaxHealth = 4;
        public string[] thresholdLines = new string[]
        {
            "怒り: 人から奪うだけのくせに。",
            "怒り: 消えてしまえばいいのに。",
            "怒り: すべてリセットしたい。"
        };
        [Tooltip("節目の台詞を通常表示しておく秒数")]
        public float thresholdLineDuration = 1.8f;
        [Tooltip("撃破直後、隔離／消去の選択に入る前に表示する怒りの最後の訴えとコンタックの返答")]
        public string[] defeatExchangeLines = new string[]
        {
            "怒り: ぼく、もうイヤなんだ。ほしいものを、ほしいって言いたい。やりたいことを、やりたいって言いたい。もう、がまんなんてしたくない。",
            "コンタック: それは異常です。"
        };
        [Tooltip("3発目の命中時に時間を止める秒数")]
        public float phaseHitStopDuration = 0.08f;

        [Header("コンタックの青い弾")]
        public float playerShotCooldown = 0.16f;
        [Tooltip("コンタック中心から青い弾を出す相対位置")]
        public Vector3 playerBulletSpawnOffset = new Vector3(0f, 0.7f, 0f);
        public Color playerBulletColor = new Color(0.15f, 0.72f, 1f, 1f);
        [Tooltip("見た目より小さくするコンタック中心の当たり判定半径")]
        public float playerCollisionRadius = 0.20f;
        [Tooltip("赤弾に当たった後の無敵時間")]
        public float playerInvulnerabilityDuration = 1f;

        [Header("怒りの赤い弾幕")]
        public Color enemyBulletColor = new Color(1f, 0.08f, 0.12f, 1f);
        [Tooltip("カーテン弾以外の赤弾速度")]
        public float standardEnemyBulletSpeed = 5.0f;
        [Tooltip("レベル3のカーテン弾速度")]
        public float curtainBulletSpeed = 4.2f;
        public float enemyBulletScale = 0.28f;
        [Range(-1f, 1f)]
        [Tooltip("怒りの見た目の中心から、半身の高さに対する発射位置。0.25で胸付近")]
        public float enemyBulletOriginHeight = 0.25f;
        [Tooltip("HP段階0〜3の発射間隔")]
        public float[] phaseShotIntervals = new float[] { 0.90f, 0.78f, 0.95f, 0.55f };
        [Tooltip("レベル3のカーテン弾同士の横間隔")]
        public float curtainBulletSpacing = 1.05f;
        [Tooltip("レベル3の安全地帯の半幅")]
        public float curtainSafeGapHalfWidth = 1.45f;
        [Tooltip("安全地帯が左右に動く範囲")]
        public float curtainSafeGapRange = 2.4f;
        [Tooltip("安全地帯の一列ごとの移動速度。0.38なら最大移動量は約0.9")]
        public float curtainSafeGapStep = 0.38f;
        [Tooltip("最終フェーズで怒りが左右に動く幅")]
        public float finalPhaseMoveRange = 2.2f;
        public float finalPhaseMoveSpeed = 1.35f;

        [Header("セリフ表示（開始演出・攻撃時、現実パートと同じ見た目）")]
        [Tooltip("セリフ本文を表示するTMP_Text（現実パートのLine Presenterと同じ位置・サイズ）")]
        public TMP_Text attackLineText;
        [Tooltip("話者名を表示するTMP_Text（現実パートのCharacter Nameと同じ見た目）。未設定なら「名前: 本文」のまま1つのテキストに表示する")]
        public TMP_Text characterNameText;
        [Tooltip("attackLineTextの背景パネル（現実パートのLine Presenterと同じ見た目の黒背景）")]
        public GameObject lineBackground;
        [Tooltip("精神世界パートに切り替わった直後、プレイヤー操作待ちで表示するコンタックの一言")]
        public string startLine = "コンタック: 心の声を鎮めなくちゃ。";
        [Tooltip("撃破直後、Good Morning演出の前に表示する一言（レベルアップ演出）。空文字なら表示しない")]
        public string levelUpLine = "頭が少しすっきりした。";

        [Header("被弾・命中演出")]
        [Tooltip("揺らす対象。未設定ならこのコントローラーの親（戦闘ルート）を使う")]
        public Transform shakeTarget;
        public float contactCooldown = 0.45f;
        public float contactEffectDuration = 0.22f;
        public float shakeStrength = 0.12f;
        public float shakeFrequency = 42f;
        public int flashCount = 2;
        public Color contactFlashColor = new Color(1f, 0.2f, 0.18f, 1f);
        [Tooltip("接触時にプレイヤーを一瞬だけ膨らませる倍率")]
        public float contactPunchScale = 1.08f;
        [Tooltip("怒りが青い弾を受けた時の点滅・揺れ時間")]
        public float enemyHitEffectDuration = 0.14f;
        public float enemyHitShakeStrength = 0.07f;
        [Tooltip("1なら拡大なし。0.055なら最大約5.5%拡大")]
        public float enemyHitPunchAmount = 0.055f;
        public float enemyHitFlashBrightness = 0.8f;

        private bool battleDefeated = false;
        private Action onBattleFinished;
        private float nextContactEffectTime;
        private Coroutine contactEffectCoroutine;
        private Transform activeShakeTarget;
        private Vector3 activeShakeOriginalPosition;
        private SpriteRenderer activePlayerRenderer;
        private Color activePlayerOriginalColor;
        private Vector3 activePlayerOriginalScale;
        private bool contactEffectStateCaptured;
        private bool battleRunning;
        private bool playerDefeated;
        private bool playerInvulnerable;
        private int currentPlayerHealth;
        private bool initialPlayerStateCaptured;
        private Vector3 initialPlayerLocalPosition;
        private Vector3 initialPlayerLocalScale;
        private Vector2 initialPlayerMinBounds;
        private Vector2 initialPlayerMaxBounds;
        private bool canPlayerFire;
        private bool thresholdLinePlaying;
        private int currentPhase;
        private int lastHandledPhase;
        private int volleyIndex;
        private float nextPlayerShotTime;
        private Coroutine enemyPatternCoroutine;
        private Coroutine thresholdLineCoroutine;
        private Coroutine playerInvulnerabilityCoroutine;
        private Coroutine enemyHitCoroutine;
        private Sprite runtimeBulletSprite;
        private SpriteRenderer enemyRenderer;
        private Vector3 enemyHomePosition;
        private bool enemyHitFeedbackPlaying;
        private bool hitStopActive;
        private float timeScaleBeforeHitStop = 1f;
#if UNITY_EDITOR
        private bool editorPresentationQueued;
#endif
        [NonSerialized] public bool autoAdvanceDialogueForTests;

        private void Awake()
        {
            CaptureInitialPlayerState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || editorPresentationQueued)
            {
                return;
            }
            editorPresentationQueued = true;
            UnityEditor.EditorApplication.delayCall += ApplyEditorPresentation;
        }

        private void ApplyEditorPresentation()
        {
            editorPresentationQueued = false;
            if (this == null || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || !gameObject.scene.IsValid())
            {
                return;
            }

            ApplyVerticalLayout();
            EnsureHud();
            if (transform.parent != null)
            {
                Transform oldUi = transform.parent.Find("BattleUI");
                if (oldUi != null)
                {
                    oldUi.name = "DialogueUI";
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        private void Update()
        {
            UpdateEnemyMovement();

            if (!battleRunning || !canPlayerFire || thresholdLinePlaying || Time.time < nextPlayerShotTime)
            {
                return;
            }

            if (Input.GetKey(KeyCode.Space))
            {
                FireDenialBullet();
                nextPlayerShotTime = Time.time + Mathf.Max(0.05f, playerShotCooldown);
            }
        }

        /// <summary>
        /// 外部（MinigameLauncherなど）から呼び出して戦闘を開始する。
        /// battleFinishedCallback は怒り撃破時に呼ばれる。
        /// </summary>
        public void StartBattle(Action battleFinishedCallback)
        {
            if (battleRunning)
            {
                Debug.LogWarning("[AngerBattle] 戦闘中の二重起動を無視しました。", this);
                return;
            }

            ResetBattleState();
            onBattleFinished = battleFinishedCallback;
            battleDefeated = false;
            battleRunning = true;
            ApplyVerticalLayout();
            EnsureHud();
            hud.ResetRound();

            if (player != null)
            {
                // 開始演出（コンタックのセリフ）が表示されている間は移動できないようにしておく
                player.enabled = false;
            }
            if (enemy != null)
            {
                enemy.SetPresent(false);
            }
            HideLine();

            StartCoroutine(RunBattleSequence());
        }

        private IEnumerator RunBattleSequence()
        {
            if (player == null || enemy == null || denialBulletPrefab == null)
            {
                Debug.LogError("[AngerBattle] player / enemy / denialBulletPrefab の参照が不足しています。", this);
                CompleteBattle();
                yield break;
            }

            bool showStartLine = true;
            while (battleRunning)
            {
                ApplyVerticalLayout();
                EnsureHud();
                hud.ResetRound();

                if (showStartLine)
                {
                    yield return StartCoroutine(ShowLineAndWaitForSpace(startLine));
                }
                yield return null;

                PrepareRound();
                while (!battleDefeated && !playerDefeated)
                {
                    yield return null;
                }

                StopEnemyPattern();
                canPlayerFire = false;
                player.enabled = false;
                CleanupPlayerBullets();
                enemy.OnHealthChanged -= HandleEnemyHealthChanged;
                enemy.OnDefeated -= HandleEnemyDefeated;

                if (playerDefeated)
                {
                    ClearEnemyBullets();
                    if (bgm != null) bgm.StopMusic();
                    yield return StartCoroutine(RunPlayerDefeatAndRetry());
                    showStartLine = false;
                    continue;
                }

                yield return new WaitForSecondsRealtime(0.5f);
                ClearEnemyBullets();
                if (bgm != null) bgm.StopMusic();
                HideLine();

                if (defeatExchangeLines != null)
                {
                    foreach (string line in defeatExchangeLines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        yield return StartCoroutine(ShowLineAndWaitForSpace(line));
                    }
                }

                EmotionOutcome resolution = EmotionOutcome.Unresolved;
                yield return StartCoroutine(EmotionResolutionFlow.Choose(
                    this,
                    EmotionKind.Anger,
                    enemy.transform,
                    attackLineText,
                    characterNameText,
                    lineBackground,
                    value => resolution = value));

                // 怒り戦では最後の一撃までを戦闘として遊ぶ。隔離時はその場の怒りを
                // 牢へ閉じ込め、消去時は消滅演出（白フラッシュ+破片飛散+フェード）を
                // 再生してから終了する。
                if (resolution == EmotionOutcome.Eliminated)
                {
                    yield return StartCoroutine(EmotionResolutionFlow.PlayElimination(enemy.transform));
                }

                CompleteBattle();
                yield break;
            }
        }

        private void PrepareRound()
        {
            battleDefeated = false;
            playerDefeated = false;
            playerInvulnerable = false;
            thresholdLinePlaying = false;
            currentPhase = 0;
            lastHandledPhase = 0;
            volleyIndex = 0;
            currentPlayerHealth = Mathf.Max(1, playerMaxHealth);

            enemy.OnHealthChanged -= HandleEnemyHealthChanged;
            enemy.OnDefeated -= HandleEnemyDefeated;
            enemy.OnHealthChanged += HandleEnemyHealthChanged;
            enemy.OnDefeated += HandleEnemyDefeated;
            enemyMaxHealth = 12;
            enemy.SetPresent(false);
            enemy.transform.localPosition = enemyHomePosition;
            enemy.BeginBattle(enemyMaxHealth, false, false);
            enemy.SetDamageEnabled(true);

            RestorePlayerRenderer();
            hud.SetEnemyHealth(enemyMaxHealth, enemyMaxHealth);
            hud.SetPlayerHealth(currentPlayerHealth, playerMaxHealth);

            if (bgm != null) bgm.PlayMusic();
            player.enabled = true;
            canPlayerFire = true;
            nextPlayerShotTime = Time.time + 0.12f;
            enemyPatternCoroutine = StartCoroutine(RunEnemyPatterns());
        }

        private void StopEnemyPattern()
        {
            if (enemyPatternCoroutine != null)
            {
                StopCoroutine(enemyPatternCoroutine);
                enemyPatternCoroutine = null;
            }
        }

        private IEnumerator RunPlayerDefeatAndRetry()
        {
            yield return StartCoroutine(hud.PlayPlayerDeathEffect());
            yield return null;
            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }
            hud.HideRetryOverlay();
            enemy.SetPresent(false);
            HideLine();
        }

        /// <summary>撃破直後のレベルアップ一言（levelUpLine）を表示し、スペースキーで読み進める。MinigameLauncher側から呼ぶ。</summary>
        public IEnumerator ShowLevelUpLineAndWait()
        {
            if (string.IsNullOrEmpty(levelUpLine)) yield break;
            yield return StartCoroutine(ShowLineAndWaitForSpace(levelUpLine));
        }

        /// <summary>指定したセリフを表示し、スペースキーが押されるまで待ってから隠す。</summary>
        private IEnumerator ShowLineAndWaitForSpace(string text)
        {
            ShowLine(text);

            // 直前の操作（例：戦闘開始の合図になったYarn側のスペース入力）を
            // 誤って拾わないよう、1フレーム待ってから入力受付を始める
            yield return null;

            if (autoAdvanceDialogueForTests)
            {
                HideLine();
                yield break;
            }

            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }

            HideLine();
        }

        private IEnumerator RunEnemyPatterns()
        {
            while (battleRunning && !battleDefeated && !playerDefeated)
            {
                if (thresholdLinePlaying || enemy == null || !enemy.IsPresent())
                {
                    yield return null;
                    continue;
                }

                FireEnemyPattern(currentPhase, volleyIndex++);
                yield return new WaitForSeconds(GetPhaseInterval(currentPhase));
            }
        }

        private float GetPhaseInterval(int phase)
        {
            if (phaseShotIntervals == null || phaseShotIntervals.Length == 0)
            {
                return 0.7f;
            }
            return Mathf.Max(0.12f, phaseShotIntervals[Mathf.Clamp(phase, 0, phaseShotIntervals.Length - 1)]);
        }

        private void FireEnemyPattern(int phase, int volley)
        {
            Vector3 origin = GetEnemyBulletOrigin();
            Vector2 aimed = player != null
                ? ((Vector2)(player.transform.position - origin)).normalized
                : Vector2.down;

            switch (Mathf.Clamp(phase, 0, 3))
            {
                case 0:
                    SpawnEnemyBullet(origin, aimed);
                    break;
                case 1:
                    SpawnFan(origin, aimed, volley % 2 == 0 ? 3 : 5, volley % 2 == 0 ? 15f : 11f);
                    break;
                case 2:
                    SpawnLaneCurtain(
                        origin.y,
                        Mathf.Sin(volley * curtainSafeGapStep) * curtainSafeGapRange,
                        curtainSafeGapHalfWidth);
                    break;
                default:
                    // レベル4はカーテンなし。7方向弾→狙い弾1発→狙い弾1発を繰り返す。
                    if (volley % 3 == 0)
                    {
                        SpawnFan(origin, aimed, 7, 12f);
                    }
                    else
                    {
                        SpawnEnemyBullet(origin, aimed);
                    }
                    break;
            }
        }

        private Vector3 GetEnemyBulletOrigin()
        {
            if (enemy == null) return Vector3.zero;

            SpriteRenderer enemyRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
            if (enemyRenderer == null)
            {
                return enemy.transform.position;
            }

            Bounds bounds = enemyRenderer.bounds;
            return new Vector3(
                bounds.center.x,
                bounds.center.y + bounds.extents.y * enemyBulletOriginHeight,
                enemy.transform.position.z);
        }

        private void SpawnFan(Vector3 origin, Vector2 centerDirection, int count, float angleStep)
        {
            float center = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                SpawnEnemyBullet(origin, Rotate(centerDirection, (i - center) * angleStep));
            }
        }

        private void SpawnLaneCurtain(float originY, float safeGapX, float gapHalfWidth)
        {
            float minX = player != null ? player.minBounds.x : -4.5f;
            float maxX = player != null ? player.maxBounds.x : 4.5f;
            float spacing = Mathf.Max(0.2f, curtainBulletSpacing);
            for (float x = minX; x <= maxX; x += spacing)
            {
                if (Mathf.Abs(x - safeGapX) < gapHalfWidth) continue;
                SpawnEnemyBullet(new Vector3(x, originY, 0f), Vector2.down, curtainBulletSpeed);
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }

        private void SpawnEnemyBullet(Vector3 origin, Vector2 direction, float speedOverride = -1f)
        {
            GameObject bullet = new GameObject(
                "AngerBullet",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(AngerBullet));
            bullet.transform.SetParent(transform, true);
            bullet.transform.position = origin;
            bullet.transform.localScale = Vector3.one * Mathf.Max(0.08f, enemyBulletScale);

            SpriteRenderer renderer = bullet.GetComponent<SpriteRenderer>();
            renderer.sprite = GetRuntimeBulletSprite();
            renderer.color = enemyBulletColor;
            renderer.sortingOrder = enemyBulletSortingOrder;

            Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            bullet.GetComponent<CircleCollider2D>().isTrigger = true;
            float speed = speedOverride > 0f ? speedOverride : standardEnemyBulletSpeed;
            bullet.GetComponent<AngerBullet>().Configure(direction, speed, player, playerCollisionRadius, HandleBulletHit);
        }

        private void HandleEnemyHealthChanged(int currentHealth, int maxHealth)
        {
            if (hud != null)
            {
                hud.SetEnemyHealth(currentHealth, maxHealth);
                if (currentHealth < maxHealth) hud.FlashEnemyHealth();
            }
            if (currentHealth < maxHealth)
            {
                PlayEnemyHitFeedback();
            }

            int damage = maxHealth - currentHealth;
            int totalPhases = thresholdLines != null ? Mathf.Clamp(thresholdLines.Length, 0, 3) : 0;
            if (damage <= 0 || damage >= maxHealth || totalPhases <= 0 || lastHandledPhase >= totalPhases)
            {
                return;
            }

            // 節目は「maxHealthを3で割った位置」固定ではなく、maxHealthをフェーズ数+1で均等分割した割合で判定する。
            // Inspectorでenemyの最大HPを変えても、3の倍数固定ではなく全フェーズが必ず順番に発生する。
            int phase = lastHandledPhase + 1;
            int phaseThreshold = Mathf.Max(1, Mathf.RoundToInt(maxHealth * (float)phase / (totalPhases + 1)));
            if (damage < phaseThreshold)
            {
                return;
            }

            lastHandledPhase = phase;
            enemy.SetDamageEnabled(false);
            canPlayerFire = false;
            CleanupPlayerBullets();
            if (thresholdLineCoroutine != null)
            {
                StopCoroutine(thresholdLineCoroutine);
            }
            thresholdLineCoroutine = StartCoroutine(PlayThresholdLine(phase));
        }

        private IEnumerator PlayThresholdLine(int phase)
        {
            thresholdLinePlaying = true;
            yield return StartCoroutine(PlayPhaseHitStop());
            ClearEnemyBullets();
            if (hud != null) hud.PulsePhaseBackground(phase);
            string line = thresholdLines != null && phase - 1 < thresholdLines.Length
                ? thresholdLines[phase - 1]
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(line))
            {
                ShowLine(line);
                yield return new WaitForSeconds(Mathf.Max(0.1f, thresholdLineDuration));
                HideLine();
            }

            currentPhase = Mathf.Clamp(phase, 0, 3);
            thresholdLinePlaying = false;
            thresholdLineCoroutine = null;
            if (battleRunning && !battleDefeated)
            {
                enemy.SetDamageEnabled(true);
                canPlayerFire = true;
                nextPlayerShotTime = Time.time + 0.08f;
            }
        }

        private void HandleEnemyDefeated()
        {
            canPlayerFire = false;
            ShatterEnemyBullets();
            battleDefeated = true;
        }

        private void HandleBulletHit()
        {
            if (playerDefeated || playerInvulnerable || Time.time < nextContactEffectTime)
            {
                return;
            }

            nextContactEffectTime = Time.time + contactCooldown;
            currentPlayerHealth = Mathf.Max(0, currentPlayerHealth - 1);
            if (hud != null)
            {
                hud.SetPlayerHealth(currentPlayerHealth, playerMaxHealth);
                hud.FlashPlayerHealth();
            }
            if (contactEffectCoroutine != null)
            {
                StopCoroutine(contactEffectCoroutine);
                contactEffectCoroutine = null;
                RestoreContactEffectState();
            }
            contactEffectCoroutine = StartCoroutine(PlayContactEffect());

            if (currentPlayerHealth <= 0)
            {
                playerDefeated = true;
                canPlayerFire = false;
                player.enabled = false;
                return;
            }

            if (playerInvulnerabilityCoroutine != null)
            {
                StopCoroutine(playerInvulnerabilityCoroutine);
            }
            playerInvulnerabilityCoroutine = StartCoroutine(PlayPlayerInvulnerability());
        }

        private IEnumerator PlayPhaseHitStop()
        {
            if (hitStopActive || phaseHitStopDuration <= 0f) yield break;
            hitStopActive = true;
            timeScaleBeforeHitStop = Time.timeScale;
            if (timeScaleBeforeHitStop > 0f) Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(phaseHitStopDuration);
            RestoreTimeScaleAfterHitStop();
        }

        private void RestoreTimeScaleAfterHitStop()
        {
            if (!hitStopActive) return;
            Time.timeScale = timeScaleBeforeHitStop;
            hitStopActive = false;
        }

        private IEnumerator PlayPlayerInvulnerability()
        {
            playerInvulnerable = true;
            SpriteRenderer renderer = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0.05f, playerInvulnerabilityDuration) && !playerDefeated)
            {
                elapsed += Time.unscaledDeltaTime;
                if (renderer != null) renderer.enabled = Mathf.FloorToInt(elapsed / 0.09f) % 2 == 0;
                yield return null;
            }
            if (renderer != null) renderer.enabled = true;
            playerInvulnerable = false;
            playerInvulnerabilityCoroutine = null;
        }

        private void RestorePlayerRenderer()
        {
            SpriteRenderer renderer = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.color = Color.white;
            }
        }

        private void PlayEnemyHitFeedback()
        {
            if (enemy == null || !enemy.IsPresent()) return;
            if (enemyHitCoroutine != null)
            {
                StopCoroutine(enemyHitCoroutine);
                enemyHitCoroutine = null;
                enemyHitFeedbackPlaying = false;
                if (enemyRenderer != null) enemyRenderer.color = Color.white;
                enemy.transform.localScale = enemyBattleScale;
            }
            enemyHitCoroutine = StartCoroutine(PlayEnemyHitFeedbackCoroutine());
        }

        private IEnumerator PlayEnemyHitFeedbackCoroutine()
        {
            enemyHitFeedbackPlaying = true;
            enemyRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
            Vector3 basePosition = enemy.transform.localPosition;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, enemyHitEffectDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float envelope = 1f - t;
                enemy.transform.localPosition = basePosition +
                    (Vector3)(UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, enemyHitShakeStrength) * envelope);
                enemy.transform.localScale = enemyBattleScale *
                    (1f + Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, enemyHitPunchAmount));
                if (enemyRenderer != null)
                {
                    float brightness = 1f + envelope * Mathf.Max(0f, enemyHitFlashBrightness);
                    enemyRenderer.color = new Color(brightness, brightness, brightness, 1f);
                }
                yield return null;
            }
            enemy.transform.localPosition = basePosition;
            enemy.transform.localScale = enemyBattleScale;
            if (enemyRenderer != null) enemyRenderer.color = Color.white;
            enemyHitFeedbackPlaying = false;
            enemyHitCoroutine = null;
        }

        private void ShatterEnemyBullets()
        {
            Color shatterColor = new Color(0.55f, 0.9f, 1f, 1f);
            foreach (AngerBullet bullet in GetComponentsInChildren<AngerBullet>(true))
            {
                if (bullet != null) bullet.Shatter(shatterColor);
            }
        }

        private void UpdateEnemyMovement()
        {
            if (!battleRunning || enemy == null || !enemy.IsPresent() || thresholdLinePlaying || enemyHitFeedbackPlaying || playerDefeated)
            {
                return;
            }

            Vector3 position = enemy.transform.localPosition;
            if (currentPhase >= 3)
            {
                position.x = enemyHomePosition.x + Mathf.Sin(Time.time * finalPhaseMoveSpeed) * finalPhaseMoveRange;
            }
            else
            {
                position.x = Mathf.MoveTowards(position.x, enemyHomePosition.x, Time.deltaTime * 4f);
            }
            position.y = enemyHomePosition.y;
            enemy.transform.localPosition = position;
        }

        private void EnsureHud()
        {
            if (hud == null)
            {
                hud = transform.parent != null ? transform.parent.GetComponentInChildren<AngerBattleHUD>(true) : null;
            }
            if (hud == null)
            {
                GameObject hudObject = new GameObject("AngerBattleHUD", typeof(RectTransform), typeof(AngerBattleHUD));
                hudObject.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
                hud = hudObject.GetComponent<AngerBattleHUD>();
            }
            hud.Build(attackLineText);
        }

        private IEnumerator PlayContactEffect()
        {
            activeShakeTarget = shakeTarget != null
                ? shakeTarget
                : (transform.parent != null ? transform.parent : transform);
            activeShakeOriginalPosition = activeShakeTarget.localPosition;
            activePlayerRenderer = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            activePlayerOriginalColor = activePlayerRenderer != null ? activePlayerRenderer.color : Color.white;
            activePlayerOriginalScale = player != null ? player.transform.localScale : Vector3.one;
            contactEffectStateCaptured = true;

            float elapsed = 0f;
            int safeFlashCount = Mathf.Max(1, flashCount);
            float duration = Mathf.Max(0.01f, contactEffectDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float envelope = 1f - normalized;
                float noiseTime = Time.unscaledTime * Mathf.Max(1f, shakeFrequency);
                Vector2 noise = new Vector2(
                    Mathf.PerlinNoise(noiseTime, 0.17f) * 2f - 1f,
                    Mathf.PerlinNoise(0.71f, noiseTime) * 2f - 1f);
                activeShakeTarget.localPosition = activeShakeOriginalPosition
                    + (Vector3)(noise * shakeStrength * envelope);

                if (activePlayerRenderer != null)
                {
                    float phase = normalized * safeFlashCount * 2f;
                    Color flash = contactFlashColor;
                    flash.a = activePlayerOriginalColor.a;
                    activePlayerRenderer.color = Mathf.FloorToInt(phase) % 2 == 0
                        ? Color.Lerp(activePlayerOriginalColor, flash, envelope)
                        : activePlayerOriginalColor;
                }

                if (player != null)
                {
                    float punch = 1f + Mathf.Sin(normalized * Mathf.PI) * Mathf.Max(0f, contactPunchScale - 1f);
                    player.transform.localScale = activePlayerOriginalScale * punch;
                }
                yield return null;
            }

            RestoreContactEffectState();
            contactEffectCoroutine = null;
        }

        private void RestoreContactEffectState()
        {
            if (!contactEffectStateCaptured)
            {
                return;
            }

            if (activeShakeTarget != null)
            {
                activeShakeTarget.localPosition = activeShakeOriginalPosition;
            }
            if (activePlayerRenderer != null)
            {
                activePlayerRenderer.color = activePlayerOriginalColor;
            }
            if (player != null)
            {
                player.transform.localScale = activePlayerOriginalScale;
            }

            activeShakeTarget = null;
            activePlayerRenderer = null;
            contactEffectStateCaptured = false;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            contactEffectCoroutine = null;
            enemyPatternCoroutine = null;
            thresholdLineCoroutine = null;
            playerInvulnerabilityCoroutine = null;
            enemyHitCoroutine = null;
            RestoreTimeScaleAfterHitStop();
            RestoreContactEffectState();
            RestorePlayerRenderer();
            battleRunning = false;
            battleDefeated = false;
            playerDefeated = false;
            playerInvulnerable = false;
            canPlayerFire = false;
            thresholdLinePlaying = false;
            onBattleFinished = null;

            if (enemy != null)
            {
                enemy.OnHealthChanged -= HandleEnemyHealthChanged;
                enemy.OnDefeated -= HandleEnemyDefeated;
                enemy.SetPresent(false);
            }
            if (bgm != null)
            {
                bgm.StopMusic();
            }
            if (player != null)
            {
                player.enabled = false;
            }
            CleanupRuntimeObjects();
            HideLine();
        }

        private void FireDenialBullet()
        {
            if (!battleRunning || !canPlayerFire || denialBulletPrefab == null || player == null)
            {
                return;
            }

            Vector3 spawnPos = player.transform.position + playerBulletSpawnOffset;
            if (bulletSpawnPoint != null)
            {
                bulletSpawnPoint.position = spawnPos;
            }
            GameObject bullet = Instantiate(denialBulletPrefab, spawnPos, Quaternion.identity, transform);
            DenialBullet denialBullet = bullet.GetComponent<DenialBullet>();
            if (denialBullet != null)
            {
                denialBullet.Configure(Vector2.up, playerBulletColor);
            }
            SpriteRenderer renderer = bullet.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = GetRuntimeBulletSprite();
                renderer.color = playerBulletColor;
                renderer.sortingOrder = playerBulletSortingOrder;
            }
        }

        private Sprite GetRuntimeBulletSprite()
        {
            if (runtimeBulletSprite != null)
            {
                return runtimeBulletSprite;
            }

            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "AngerBattleBulletTexture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            runtimeBulletSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            runtimeBulletSprite.name = "AngerBattleBulletSprite";
            return runtimeBulletSprite;
        }

        private void ApplyVerticalLayout()
        {
            if (player == null || enemy == null)
            {
                return;
            }

            if (playerBattleSprite == null)
            {
                playerBattleSprite = Resources.Load<Sprite>("AngerBattle/ContackVertical");
            }
            if (enemyBattleSprite == null)
            {
                enemyBattleSprite = Resources.Load<Sprite>("AngerBattle/AngerVertical");
            }

            SpriteRenderer playerRenderer = player.GetComponentInChildren<SpriteRenderer>();
            if (playerRenderer != null && playerBattleSprite != null)
            {
                playerRenderer.sprite = playerBattleSprite;
                playerRenderer.color = Color.white;
                playerRenderer.sortingOrder = combatantSortingOrder;
            }
            if (enemyBattleSprite != null)
            {
                enemy.SetBattleSprite(enemyBattleSprite);
            }

            player.transform.localScale = playerBattleScale;
            player.transform.localPosition = playerBattlePosition;
            player.speed = Mathf.Max(0.1f, playerBattleSpeed);
            enemy.transform.localPosition = enemyBattlePosition;
            enemy.transform.localScale = enemyBattleScale;
            enemyHomePosition = enemy.transform.localPosition;
            enemyRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
            if (enemyRenderer != null)
            {
                enemyRenderer.sortingOrder = combatantSortingOrder;
            }

            Camera battleCamera = Camera.main;
            if (battleCamera != null && battleCamera.orthographic && playerRenderer != null)
            {
                float halfHeight = battleCamera.orthographicSize;
                float halfWidth = halfHeight * battleCamera.aspect;
                Vector3 cameraPosition = battleCamera.transform.position;
                Vector3 extents = playerRenderer.bounds.extents;
                float margin = Mathf.Max(0f, screenEdgeMargin);
                Vector2 min = new Vector2(
                    cameraPosition.x - halfWidth + extents.x + margin,
                    cameraPosition.y - halfHeight + extents.y + margin);
                Vector2 max = new Vector2(
                    cameraPosition.x + halfWidth - extents.x - margin,
                    cameraPosition.y + halfHeight - extents.y - margin);
                if (min.x > max.x) min.x = max.x = cameraPosition.x;
                if (min.y > max.y) min.y = max.y = cameraPosition.y;
                player.minBounds = min;
                player.maxBounds = max;
                Vector3 position = player.transform.position;
                position.x = Mathf.Clamp(position.x, min.x, max.x);
                position.y = Mathf.Clamp(position.y, min.y, max.y);
                player.transform.position = position;
            }
            else
            {
                player.minBounds = fallbackPlayerMinBounds;
                player.maxBounds = fallbackPlayerMaxBounds;
            }
        }

        private void CaptureInitialPlayerState()
        {
            if (initialPlayerStateCaptured || player == null)
            {
                return;
            }

            initialPlayerLocalPosition = player.transform.localPosition;
            initialPlayerLocalScale = player.transform.localScale;
            initialPlayerMinBounds = player.minBounds;
            initialPlayerMaxBounds = player.maxBounds;
            initialPlayerStateCaptured = true;
        }

        private void ResetBattleState()
        {
            CaptureInitialPlayerState();
            if (enemyPatternCoroutine != null)
            {
                StopCoroutine(enemyPatternCoroutine);
                enemyPatternCoroutine = null;
            }
            if (thresholdLineCoroutine != null)
            {
                StopCoroutine(thresholdLineCoroutine);
                thresholdLineCoroutine = null;
            }
            if (contactEffectCoroutine != null)
            {
                StopCoroutine(contactEffectCoroutine);
                contactEffectCoroutine = null;
            }
            if (playerInvulnerabilityCoroutine != null)
            {
                StopCoroutine(playerInvulnerabilityCoroutine);
                playerInvulnerabilityCoroutine = null;
            }
            if (enemyHitCoroutine != null)
            {
                StopCoroutine(enemyHitCoroutine);
                enemyHitCoroutine = null;
            }
            RestoreTimeScaleAfterHitStop();
            RestoreContactEffectState();
            RestorePlayerRenderer();
            battleDefeated = false;
            playerDefeated = false;
            playerInvulnerable = false;
            canPlayerFire = false;
            thresholdLinePlaying = false;

            if (player != null && initialPlayerStateCaptured)
            {
                player.transform.localPosition = initialPlayerLocalPosition;
                player.transform.localScale = initialPlayerLocalScale;
                player.minBounds = initialPlayerMinBounds;
                player.maxBounds = initialPlayerMaxBounds;
                player.enabled = false;
            }
            if (enemy != null)
            {
                enemy.OnHealthChanged -= HandleEnemyHealthChanged;
                enemy.OnDefeated -= HandleEnemyDefeated;
                enemy.SetPresent(false);
            }
            if (bgm != null)
            {
                bgm.StopMusic();
            }
            CleanupRuntimeObjects();
            HideLine();
        }

        private void CleanupRuntimeObjects()
        {
            ClearEnemyBullets();
            CleanupPlayerBullets();
        }

        private void ClearEnemyBullets()
        {
            foreach (AngerBullet bullet in GetComponentsInChildren<AngerBullet>(true))
            {
                if (bullet != null)
                {
                    bullet.gameObject.SetActive(false);
                    Destroy(bullet.gameObject);
                }
            }
        }

        private void CleanupPlayerBullets()
        {
            foreach (DenialBullet bullet in GetComponentsInChildren<DenialBullet>(true))
            {
                if (bullet != null)
                {
                    bullet.gameObject.SetActive(false);
                    Destroy(bullet.gameObject);
                }
            }
        }

        private void CompleteBattle()
        {
            if (!battleRunning)
            {
                return;
            }

            battleRunning = false;
            Action callback = onBattleFinished;
            onBattleFinished = null;
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            if (runtimeBulletSprite != null)
            {
                Texture2D texture = runtimeBulletSprite.texture;
                Destroy(runtimeBulletSprite);
                if (texture != null)
                {
                    Destroy(texture);
                }
                runtimeBulletSprite = null;
            }
        }

        /// <summary>
        /// セリフを現実パートと同じ見た目（背景パネル＋話者名＋本文）で表示する。
        /// text は「話者名: 本文」の形式を想定し、最初の「: 」で話者名と本文に分割する。
        /// </summary>
        private void ShowLine(string text)
        {
            string speaker = null;
            string body = text ?? string.Empty;

            int separatorIndex = body.IndexOf(": ", StringComparison.Ordinal);
            if (separatorIndex >= 0)
            {
                speaker = text.Substring(0, separatorIndex);
                body = text.Substring(separatorIndex + 2);
            }

            if (characterNameText != null)
            {
                if (!string.IsNullOrEmpty(speaker))
                {
                    characterNameText.text = speaker;
                    characterNameText.gameObject.SetActive(true);
                }
                else
                {
                    characterNameText.gameObject.SetActive(false);
                }
            }
            else if (speaker != null)
            {
                // 話者名専用のTMP_Textが無い場合は、今まで通り本文に含めて表示する
                body = text;
            }

            if (attackLineText != null)
            {
                attackLineText.text = body;
                attackLineText.gameObject.SetActive(true);
            }
            if (lineBackground != null)
            {
                lineBackground.SetActive(true);
            }
        }

        private void HideLine()
        {
            if (attackLineText != null)
            {
                attackLineText.gameObject.SetActive(false);
            }
            if (characterNameText != null)
            {
                characterNameText.gameObject.SetActive(false);
            }
            if (lineBackground != null)
            {
                lineBackground.SetActive(false);
            }
        }
    }
}
