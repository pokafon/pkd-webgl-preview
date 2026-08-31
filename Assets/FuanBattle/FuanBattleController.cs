using System;
using System.Collections;
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
    /// 0. 開始演出：コンタックの一言（startLine）を現実パートと同じ見た目で表示し、
    ///    スペースキーが押されるまで待つ（怒り戦と文言も共通）
    /// 1. BGM（ワスレナグサ）を再生しながら、正解を示さないYES / NO質問を5問進める
    /// 2. 質問を抜けたら、最後に1回だけ「不安」本体が登場する
    ///    ・登場と同時にBGMを止める
    ///    ・登場と同時に、プレイヤーを不安の正面・画面中央へ自動移動させる
    ///    ・即セリフは出さず、1拍分だけ間を置く
    /// 3. 一拍後、「不安」自身のセリフを表示し、スペースキーで読み進める
    /// 3b. 続けてコンタックの返し（attackLine）を表示し、スペースキーで消す
    /// 4. セリフを消してから一拍待ち、プレイヤー操作なしで自動的に弾を発射する
    /// 5. 一発ヒットで不安を撃破し、不安戦終了
    ///
    /// 質問はマウス、左右キー、Y/Nキーに対応する。正誤判定やペナルティはない。
    /// </summary>
    public class FuanBattleController : MonoBehaviour
    {
        [Header("参照")]
        public PlayerController player;
        public EnemyAnger enemy;
        [Tooltip("BGM（ワスレナグサ）の再生を管理するコンポーネント")]
        public BattleBGM bgm;
        [Tooltip("攻撃弾の発射位置。未設定ならプレイヤーの位置から発射する")]
        public Transform bulletSpawnPoint;
        [Tooltip("Collider2D（Is Trigger）付きのDenialBulletプレハブ")]
        public GameObject denialBulletPrefab;

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

        [Header("不安登場時のプレイヤー移動")]
        [Tooltip("不安登場時に、プレイヤーが不安の正面・画面中央へ移動するのにかかる時間（秒）")]
        public float moveToCenterDuration = 0.3f;

        private bool battleDefeated = false;
        private Action onBattleFinished;
        private AnxietyQuestionExperience questionExperience;
        private bool battleRunning;
        private bool initialPlayerStateCaptured;
        private Vector3 initialPlayerLocalPosition;
        private Vector3 initialPlayerLocalScale;
        private GameObject activeDenialBullet;

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
            // --- 0. 開始演出：コンタックの一言を表示し、スペースキー入力を待つ ---
            yield return StartCoroutine(ShowLineAndWaitForSpace(startLine));

            // --- 1. BGMを再生しながら、正解を示さないYES / NO質問を進める ---
            if (bgm != null)
            {
                bgm.PlayMusic();
            }

            yield return StartCoroutine(RunQuestionSequence());

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
            enemy.SetPresent(true);

            // 質問画面の不透明な暗幕の裏で、不安とプレイヤーを戦闘位置へ準備する。
            yield return StartCoroutine(MovePlayerToCenter());

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

            yield return new WaitForSeconds(secondsPerBeat * beatsBeforeFire);

            FireDenialBullet();

            // --- 4. 命中で撃破 ---
            while (!battleDefeated)
            {
                yield return null;
            }

            enemy.OnDefeated -= HandleEnemyDefeated;
            // 撃破後は敵を白くするだけで、非表示にはしない
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

        /// <summary>不安登場時に、プレイヤーを不安の正面・画面中央へ移動させる。移動中〜移動後は手動操作を止める。</summary>
        private IEnumerator MovePlayerToCenter()
        {
            if (player == null || enemy == null) yield break;

            player.enabled = false;

            Vector3 start = player.transform.position;
            float centerX = (player.minBounds.x + player.maxBounds.x) / 2f;
            float targetY = Mathf.Clamp(enemy.transform.position.y, player.minBounds.y, player.maxBounds.y);
            Vector3 target = new Vector3(centerX, targetY, start.z);

            float duration = Mathf.Max(0.01f, moveToCenterDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                player.transform.position = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }
            player.transform.position = target;
        }

        private IEnumerator RunQuestionSequence()
        {
            if (player != null)
            {
                player.enabled = false;
            }

            Canvas canvas = attackLineText != null ? attackLineText.GetComponentInParent<Canvas>() : null;
            if (canvas == null || attackLineText == null || attackLineText.font == null)
            {
                Debug.LogError("[FuanBattle] 質問UIに必要なCanvasまたはTMPフォントが見つかりません。", this);
                yield break;
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

            yield return questionExperience.Play(questions, intrusiveLines);
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
