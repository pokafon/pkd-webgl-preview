using System;
using System.Collections;
using PKD.Emotions;
using TMPro;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 不安戦（精神パート2）全体の進行を管理するコントローラー。
    /// 質問UIの描画・入力・侵食演出は AnxietyQuestionExperience に分離し、
    /// このクラスは既存の開始・BGM・敵登場・撃破フローだけを管理する。
    ///
    /// 【全体の流れ】
    /// 0. 石畳の上で不安とコンタックを見せ、コンタックの一言を表示する
    /// 1. 不安だけが上へ逃げ、停止したコンタックが画面下へ抜ける縦スクロールからYES / NO質問へ接続する
    /// 2. 仮の正解順を外すと1問目へ戻り、足跡を道標として残しながら5問進める
    /// 3. 質問を抜けたら、最後に1回だけ「不安」本体が上側に登場する
    ///    ・登場と同時にBGMを止める
    ///    ・コンタックを下側、不安を上側に置いた近い縦構図にする
    ///    ・即セリフは出さず、1拍分だけ間を置く
    /// 4. 一拍後、「不安」自身のセリフを表示し、スペースキーで読み進める
    /// 5. 続けてコンタックの返し（attackLine）を表示し、スペースキーで消す
    /// 6. セリフを消してから一拍待ち、プレイヤー操作なしで上方向へ弾を発射する
    /// 7. 一発ヒットで不安を撃破し、不安戦終了
    ///
    /// 質問はマウス、左右キー、Y/Nキーに対応する。正解表示はせず、順路を外すと1問目へ戻る。
    /// </summary>
    public class FuanBattleController : MonoBehaviour
    {
        [Serializable]
        public sealed class OpeningLayoutSettings
        {
            [Tooltip("冒頭開始時の不安の画面内位置。0〜1が画面内、Yが1より大きいと上へ画面外")]
            public Vector2 anxietyStartViewport = new Vector2(0.52f, 0.69f);
            [Tooltip("冒頭終了時の不安の画面内位置")]
            public Vector2 anxietyEndViewport = new Vector2(0.52f, 1.18f);
            [Tooltip("冒頭開始時のコンタックの画面内位置。演出中はその場に止まり、スクロール分だけ画面下へ抜ける")]
            public Vector2 contackStartViewport = new Vector2(0.48f, 0.18f);
            [Min(0.1f)]
            [Tooltip("冒頭で上へ進む画面数。1なら床1画面分をスクロールする")]
            public float scrollScreens = 1f;
            [Range(0.05f, 1f)]
            [Tooltip("不安の画面高に対する表示サイズ")]
            public float anxietyScreenHeight = 0.38f;
            [Range(0.05f, 1f)]
            [Tooltip("コンタックの画面高に対する表示サイズ")]
            public float contackScreenHeight = 0.40f;
        }

        [Serializable]
        public sealed class FaceOffLayoutSettings
        {
            [Tooltip("締めでのコンタックの画面内位置")]
            public Vector2 contackViewport = new Vector2(0.48f, 0.30f);
            [Tooltip("締めでの不安の画面内位置")]
            public Vector2 anxietyViewport = new Vector2(0.52f, 0.69f);
            [Range(0.05f, 1f)]
            [Tooltip("締めでのコンタックの画面高に対する表示サイズ")]
            public float contackScreenHeight = 0.36f;
            [Range(0.05f, 1f)]
            [Tooltip("締めでの不安の画面高に対する表示サイズ")]
            public float anxietyScreenHeight = 0.38f;
        }

        [Header("参照")]
        public PlayerController player;
        public EnemyAnger enemy;
        [Tooltip("BGM（ワスレナグサ）の再生を管理するコンポーネント")]
        public BattleBGM bgm;
        [Tooltip("攻撃弾の発射位置。未設定ならプレイヤーの位置から発射する")]
        public Transform bulletSpawnPoint;
        [Tooltip("Collider2D（Is Trigger）付きのDenialBulletプレハブ")]
        public GameObject denialBulletPrefab;

        [Header("不安・コンタックの実画像")]
        [Tooltip("不安の立ち絵。未設定ならResources/AngerBattle/AnxietyVerticalを読む")]
        public Sprite anxietyCharacterSprite;
        [Tooltip("コンタックの後ろ向き立ち絵。未設定ならResources/AngerBattle/ContackVerticalを読む")]
        public Sprite contackCharacterSprite;
        [Tooltip("冒頭で不安だけが上へ抜け、完全に画面外へ出るまでの秒数")]
        public float openingChaseDuration = 1.15f;
        [Tooltip("不安が完全に画面外へ出た後、床のスクロールを始めるまでの間")]
        public float openingAfterExitHoldSeconds = 0.45f;
        [Tooltip("不安が消えた後、質問画面まで床を上へスクロールする秒数")]
        public float openingScrollDuration = 1.35f;
        [Tooltip("冒頭の二人の位置・スクロール量・表示サイズ")]
        public OpeningLayoutSettings openingLayout = new OpeningLayoutSettings();
        [Tooltip("締めで向き合う二人の位置・表示サイズ")]
        public FaceOffLayoutSettings faceOffLayout = new FaceOffLayoutSettings();

        [Header("YES / NO 背景・入口")]
        [Tooltip("質問中に表示する石畳の床")]
        public Sprite questionFloorSprite;
        [Tooltip("YES側の楕円状の選択面")]
        public Sprite yesGateSprite;
        [Tooltip("NO側の楕円状の選択面")]
        public Sprite noGateSprite;
        [Tooltip("選択面画像の薄い全画面背景を除去する透過補正マテリアル")]
        public Material questionGateMaterial;
        [Tooltip("進行表示に使う左足跡")]
        public Sprite questionLeftFootSprite;
        [Tooltip("進行表示に使う右足跡")]
        public Sprite questionRightFootSprite;
        [Tooltip("選択面へ入る演出で動かすカメラ。未設定ならMain Cameraを使う")]
        public Camera questionCamera;
        [Tooltip("回答後、選択面へカメラが入るまでの秒数")]
        public float questionDiveDuration = 1.05f;
        [Tooltip("選択面へ寄った時のOrthographic Size")]
        public float questionDiveOrthographicSize = 0.90f;
        [Tooltip("質問中にUnity標準Particle Systemの雨を表示する")]
        public bool questionRainEnabled = true;
        [Range(10f, 180f)]
        [Tooltip("1秒あたりの雨粒生成数")]
        public float questionRainRate = 85f;
        [Tooltip("質問画面の雨粒に使うURPマテリアル")]
        public Material questionRainMaterial;
        [Tooltip("アスファルト上を走る足音。回答を選んで入口へ進む間だけ再生する")]
        public AudioClip questionRunningWetRoadClip;
        [Range(0f, 1f)]
        [Tooltip("アスファルト上を走る足音の音量")]
        public float questionRunningWetRoadVolume = 0.22f;

        [Header("YES / NO 質問")]
        [TextArea]
        public string[] questions = new string[]
        {
            "断っても、嫌われない？",
            "返事がないのは、怒っているから？",
            "失敗しても、取り返せる？",
            "このままで、本当に大丈夫？",
            "今まで選んだ答えは、本当に正しかった？"
        };
        [TextArea]
        public string[] intrusiveLines = new string[]
        {
            "失敗したな ダメだな 恥ずかしいな",
            "でも でも でも でも",
            "どうしよう ぐるぐる思考が止まらない",
            "考えない 考えない 考えない",
            "すべてリセットしたい"
        };
        [Tooltip("5問を抜ける仮の正解順。YES / NOで指定する")]
        public string[] correctAnswers = new string[]
        {
            "YES",
            "NO",
            "YES",
            "NO",
            "YES"
        };
        [Range(0f, 1.5f)]
        [Tooltip("質問UIの揺れ・ずれの強さ。0で動きを止められる")]
        public float questionMotionScale = 1f;
        [Tooltip("質問が現れるアニメーションの秒数")]
        public float questionEntryDuration = 0.38f;
        [Tooltip("回答を履歴へ刻んでから次問へ移るまでの秒数")]
        public float answerPauseSeconds = 0.42f;
        [Tooltip("最終回答後、全履歴を見せる秒数")]
        public float finalHistoryHoldSeconds = 1.15f;

        [Header("不安登場演出")]
        public float bpm = 95f;
        [Tooltip("不安登場からセリフ表示までに空ける拍数（現状は1拍）")]
        public float beatsBeforeAttackLine = 1f;
        [Tooltip("「それは異常です」をスペースで消してから、実際に弾を発射するまでに空ける拍数")]
        public float beatsBeforeFire = 1f;

        [Header("セリフ表示（開始演出・攻撃時、現実パートと同じ見た目）")]
        [Tooltip("セリフ本文を表示するTMP_Text（現実パートのLine Presenterと同じ位置・サイズ）")]
        public TMP_Text attackLineText;
        [Tooltip("話者名を表示するTMP_Text（現実パートのCharacter Nameと同じ見た目）")]
        public TMP_Text characterNameText;
        [Tooltip("attackLineTextの背景パネル（現実パートのLine Presenterと同じ見た目の黒背景）")]
        public GameObject lineBackground;
        [Tooltip("精神世界パートに切り替わった直後、プレイヤー操作待ちで表示するコンタックの一言（怒り戦と共通の文言）")]
        public string startLine = "コンタック: 心の声を鎮めなくちゃ。";
        [Tooltip("YES / NOの問いを抜けた先で表示する、不安本体の台詞")]
        [TextArea]
        public string anxietyLine = "不安: わたし、こわいよ。わからないかもしれないもん。できないかもしれないもん。あぶないことは、いやだもん。";
        [Tooltip("不安のセリフの後に表示するコンタックの返し。スペースキーで消すと、一拍後に自動で弾を発射する")]
        public string attackLine = "コンタック: それは異常です。";
        [Tooltip("撃破直後、Good Morning演出の前に表示する一言（レベルアップ演出）。空文字なら表示しない")]
        public string levelUpLine = "心が少し軽くなった。";

        private bool battleDefeated = false;
        private Action onBattleFinished;
        private AnxietyQuestionExperience questionExperience;
        private bool battleRunning;
        private bool initialPlayerStateCaptured;
        private Vector3 initialPlayerLocalPosition;
        private Vector3 initialPlayerLocalScale;
        private GameObject activeDenialBullet;
        private Sprite resolvedAnxietySprite;
        private Sprite resolvedContackSprite;

        private void Awake()
        {
            CaptureInitialPlayerState();
        }

        /// <summary>
        /// 外部（MinigameLauncherなど）から呼び出して戦闘を開始する。
        /// battleFinishedCallback は不安撃破時に呼ばれる。
        /// </summary>
        public void StartBattle(Action battleFinishedCallback)
        {
            if (battleRunning)
            {
                Debug.LogWarning("[FuanBattle] 戦闘中の二重起動を無視しました。", this);
                return;
            }

            ResetBattleState();
            onBattleFinished = battleFinishedCallback;
            battleDefeated = false;
            battleRunning = true;

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

        private void OnDisable()
        {
            StopAllCoroutines();
            battleRunning = false;
            battleDefeated = false;
            onBattleFinished = null;

            if (enemy != null)
            {
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
            if (questionExperience != null)
            {
                questionExperience.HideImmediately();
            }
            if (activeDenialBullet != null)
            {
                activeDenialBullet.SetActive(false);
                Destroy(activeDenialBullet);
                activeDenialBullet = null;
            }
            HideLine();
        }

        private IEnumerator RunBattleSequence()
        {
            ResolveCharacterSprites();
            bool questionReady = ConfigureQuestionExperience();
            if (questionReady)
            {
                questionExperience.PrepareChaseOpening(resolvedAnxietySprite, resolvedContackSprite, openingLayout);
            }

            // --- 0. 石畳の上で二人を見せ、コンタックの一言を表示する ---
            yield return StartCoroutine(ShowLineAndWaitForSpace(startLine));

            // --- 1. 不安を追う縦スクロールから、YES / NO質問へ接続する ---
            if (bgm != null)
            {
                bgm.PlayMusic();
            }

            if (questionReady)
            {
                yield return StartCoroutine(questionExperience.PlayChaseIntro(
                    openingChaseDuration,
                    openingAfterExitHoldSeconds,
                    openingScrollDuration));
            }
            yield return StartCoroutine(RunQuestionSequence(questionReady));

            // --- 2. 不安本体が登場。登場と同時にBGMを止める ---
            if (bgm != null)
            {
                bgm.StopMusic();
            }

            if (enemy == null)
            {
                Debug.LogError("[FuanBattle] enemy が設定されていません。", this);
                CompleteBattle();
                yield break;
            }

            enemy.OnDefeated -= HandleEnemyDefeated;
            enemy.OnDefeated += HandleEnemyDefeated;

            // 質問画面の不透明な暗幕の裏で、実画像の二人を縦の向かい合わせに配置する。
            PrepareVerticalFaceOff();

            // 準備が終わってから初めて射撃ステージを見せる。
            if (questionExperience != null)
            {
                yield return StartCoroutine(questionExperience.RevealBattle());
            }

            // 即セリフは出さず、一拍分だけ間を置く
            float secondsPerBeat = 60f / Mathf.Max(1f, bpm);
            yield return new WaitForSeconds(secondsPerBeat * beatsBeforeAttackLine);

            // --- 3. 一拍後、不安自身のセリフを表示し、スペースキーで読み進める ---
            yield return StartCoroutine(ShowLineAndWaitForSpace(anxietyLine));

            // --- 3b. 続けてコンタックの返しを表示。スペースで消すと、一拍待ってから自動で発射する ---
            yield return StartCoroutine(ShowLineAndWaitForSpace(attackLine));

            EmotionOutcome resolution = EmotionOutcome.Unresolved;
            yield return StartCoroutine(EmotionResolutionFlow.Choose(
                this,
                EmotionKind.Anxiety,
                enemy.transform,
                attackLineText,
                characterNameText,
                lineBackground,
                value => resolution = value));

            if (resolution == EmotionOutcome.Eliminated)
            {
                yield return new WaitForSeconds(secondsPerBeat * beatsBeforeFire);
                FireDenialBullet();

                // 命中で撃破
                while (!battleDefeated)
                {
                    yield return null;
                }
            }

            enemy.OnDefeated -= HandleEnemyDefeated;
            HideLine();

            // --- 5. 不安戦終了 ---
            CompleteBattle();
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

            // 直前の操作を誤って拾わないよう、1フレーム待ってから入力受付を始める
            yield return null;

            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }

            HideLine();
        }

        /// <summary>締めで、コンタックを下、不安を上へ置いた近い縦構図を作る。</summary>
        private void PrepareVerticalFaceOff()
        {
            if (player == null || enemy == null)
            {
                return;
            }

            player.enabled = false;
            Camera camera = questionCamera != null ? questionCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            FaceOffLayoutSettings layout = faceOffLayout ?? new FaceOffLayoutSettings();
            float distance = Mathf.Abs(camera.transform.position.z);
            Vector3 contackPosition = camera.ViewportToWorldPoint(
                new Vector3(layout.contackViewport.x, layout.contackViewport.y, distance));
            Vector3 anxietyPosition = camera.ViewportToWorldPoint(
                new Vector3(layout.anxietyViewport.x, layout.anxietyViewport.y, distance));
            contackPosition.z = 0f;
            anxietyPosition.z = 0f;

            SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
            if (playerRenderer != null && resolvedContackSprite != null)
            {
                playerRenderer.sprite = resolvedContackSprite;
                playerRenderer.color = Color.white;
                playerRenderer.sortingOrder = 80;
                SetRenderedHeight(
                    player.transform,
                    resolvedContackSprite,
                    camera.orthographicSize * 2f * Mathf.Max(0.05f, layout.contackScreenHeight));
            }
            player.transform.position = contackPosition;

            if (resolvedAnxietySprite != null)
            {
                enemy.SetBattleSprite(resolvedAnxietySprite);
                SetRenderedHeight(
                    enemy.transform,
                    resolvedAnxietySprite,
                    camera.orthographicSize * 2f * Mathf.Max(0.05f, layout.anxietyScreenHeight));
            }
            enemy.transform.position = anxietyPosition;
            SpriteRenderer enemyRenderer = enemy.GetComponent<SpriteRenderer>();
            if (enemyRenderer != null)
            {
                enemyRenderer.color = Color.white;
                enemyRenderer.sortingOrder = 81;
            }
            // 旧来の右から横へ入る登場アニメーションは使わない。
            enemy.SetPresent(true, false);
        }

        private IEnumerator RunQuestionSequence(bool alreadyConfigured)
        {
            if (player != null)
            {
                player.enabled = false;
            }

            if (!alreadyConfigured && !ConfigureQuestionExperience())
            {
                yield break;
            }

            yield return questionExperience.Play(questions, intrusiveLines, correctAnswers);
        }

        private bool ConfigureQuestionExperience()
        {
            Canvas canvas = attackLineText != null ? attackLineText.GetComponentInParent<Canvas>() : null;
            if (canvas == null || attackLineText == null || attackLineText.font == null)
            {
                Debug.LogError("[FuanBattle] 質問UIに必要なCanvasまたはTMPフォントが見つかりません。", this);
                return false;
            }

            if (questionExperience == null)
            {
                questionExperience = GetComponent<AnxietyQuestionExperience>();
                if (questionExperience == null)
                {
                    questionExperience = gameObject.AddComponent<AnxietyQuestionExperience>();
                }
            }

            questionExperience.Configure(
                canvas,
                attackLineText.font,
                questionMotionScale,
                questionEntryDuration,
                answerPauseSeconds,
                finalHistoryHoldSeconds,
                questionFloorSprite,
                yesGateSprite,
                noGateSprite,
                questionGateMaterial,
                questionLeftFootSprite,
                questionRightFootSprite,
                questionCamera,
                questionDiveDuration,
                questionDiveOrthographicSize,
                questionRainEnabled,
                questionRainRate,
                questionRainMaterial,
                questionRunningWetRoadClip,
                questionRunningWetRoadVolume);
            return true;
        }

        private void HandleEnemyDefeated()
        {
            battleDefeated = true;
        }

        private void FireDenialBullet()
        {
            if (denialBulletPrefab == null)
            {
                Debug.LogError("[FuanBattle] denialBulletPrefab が設定されていません。", this);
                return;
            }

            // 不安登場時にプレイヤーを中央へ揃えているため、プレイヤーの実位置から
            // 右方向（不安の方向）へ飛ばせば自然に命中する
            Vector3 spawnPos = player != null
                ? player.transform.position
                : (bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position);

            if (activeDenialBullet != null)
            {
                activeDenialBullet.SetActive(false);
                Destroy(activeDenialBullet);
            }
            activeDenialBullet = Instantiate(denialBulletPrefab, spawnPos, Quaternion.identity);
            DenialBullet bullet = activeDenialBullet.GetComponent<DenialBullet>();
            if (bullet != null && enemy != null)
            {
                Vector2 direction = enemy.transform.position - spawnPos;
                bullet.Configure(direction, Color.white);
            }
        }

        private void ResolveCharacterSprites()
        {
            resolvedAnxietySprite = anxietyCharacterSprite != null
                ? anxietyCharacterSprite
                : Resources.Load<Sprite>("AngerBattle/AnxietyVertical");
            resolvedContackSprite = contackCharacterSprite != null
                ? contackCharacterSprite
                : Resources.Load<Sprite>("AngerBattle/ContackVertical");

            if (resolvedAnxietySprite == null || resolvedContackSprite == null)
            {
                Debug.LogWarning("[FuanBattle] 不安またはコンタックの実画像を読み込めませんでした。", this);
            }
        }

        private static void SetRenderedHeight(Transform target, Sprite sprite, float targetHeight)
        {
            if (target == null || sprite == null)
            {
                return;
            }
            float scale = Mathf.Max(0.001f, targetHeight) / Mathf.Max(0.001f, sprite.bounds.size.y);
            target.localScale = new Vector3(scale, scale, 1f);
        }

        private void CaptureInitialPlayerState()
        {
            if (initialPlayerStateCaptured || player == null)
            {
                return;
            }

            initialPlayerLocalPosition = player.transform.localPosition;
            initialPlayerLocalScale = player.transform.localScale;
            initialPlayerStateCaptured = true;
        }

        private void ResetBattleState()
        {
            CaptureInitialPlayerState();
            if (player != null && initialPlayerStateCaptured)
            {
                player.transform.localPosition = initialPlayerLocalPosition;
                player.transform.localScale = initialPlayerLocalScale;
                player.enabled = false;
            }
            if (enemy != null)
            {
                enemy.OnDefeated -= HandleEnemyDefeated;
                enemy.SetPresent(false);
            }
            if (bgm != null)
            {
                bgm.StopMusic();
            }
            if (questionExperience != null)
            {
                questionExperience.HideImmediately();
            }
            if (activeDenialBullet != null)
            {
                activeDenialBullet.SetActive(false);
                Destroy(activeDenialBullet);
                activeDenialBullet = null;
            }
            HideLine();
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

        /// <summary>セリフを現実パートと同じ見た目（背景パネル＋話者名＋本文）で表示する。</summary>
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
