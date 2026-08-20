using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 怒り戦（精神パート1）全体の進行を管理するコントローラー。
    ///
    /// 【全体の流れ】
    /// 0. 開始演出：コンタックの一言（startLine）を現実パートと同じ見た目で表示し、
    ///    スペースキーが押されるまで待つ
    /// 1. BGM（Trick_style）を再生しながら、3つの台詞を順番に
    ///    1文字ずつバラバラの高さ・タイミングで右から左へ流す
    ///    （「人から奪うだけのくせに。」→「消えてしまえばいいのに」→「全部気に入らない」）
    /// 2. 3つとも避け終えたら、最後に1回だけ「怒り」本体が登場する
    ///    ・登場と同時にBGMを止める
    ///    ・登場と同時に、プレイヤーを怒りの正面・画面中央へ自動移動させる
    ///    ・即セリフは出さず、1拍分だけ間を置く
    /// 3. 一拍後、怒り自身のセリフ（enemyLine）を表示し、スペースキーで読み進める
    /// 3b. 続けてコンタックの返し（attackLine）を表示し、スペースキーで消す
    /// 4. セリフを消してから一拍待ち、プレイヤー操作なしで自動的に弾を発射する
    /// 5. 一発ヒットで怒りを撃破し、怒り戦終了
    ///
    /// 被弾してもペナルティ・ゲームオーバーはない（避けるのは演出目的）。
    /// 戦闘中の操作キーはすべてスペースキーに統一している（現実パートの会話送りと同じキー）。
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
        [Tooltip("TMP_Textを持つ、1文字を表示するためのプレハブ")]
        public GameObject fallingCharacterPrefab;

        [Header("台詞（3つ、順番固定）")]
        public string[] phrases = new string[]
        {
            "人から奪うだけのくせに",
            "消えてしまえばいいのに",
            "全部気に入らない"
        };

        [Header("弾幕タイミング（BGM: Trick_style, 145BPM基準）")]
        [Tooltip("BGMのBPM")]
        public float bpm = 145f;

        [Tooltip("何拍ごとに1文字出すか（0.5=半拍、1=1拍、2=2拍）")]
        public float beatsPerCharacter = 1f;

        [Tooltip("台詞と台詞の間に空ける拍数")]
        public float phraseGapBeats = 2f;

        [Tooltip("出現タイミングのばらつき（秒）。0にするとジャストタイミングになる")]
        public float spawnJitter = 0.08f;

        [Header("文字の移動・出現範囲")]
        public float wordSpeed = 6f;
        [Tooltip("文字が出現するX座標の範囲（画面右側）")]
        public Vector2 spawnXRange = new Vector2(10f, 12f);
        [Tooltip("文字が出現するY座標の範囲（高さのばらつき）")]
        public Vector2 spawnYRange = new Vector2(-3.5f, 3.5f);
        [Tooltip("このX座標より左に出た文字は消える")]
        public float wordDestroyXPosition = -12f;
        [Tooltip("文字の大きさをランダムに変える倍率の範囲（1=通常サイズ）。荒々しさを出すための演出")]
        public Vector2 fontSizeScaleRange = new Vector2(1f, 1.6f);

        [Header("怒り登場演出")]
        [Tooltip("怒り登場からセリフ表示までに空ける拍数（現状は1拍）")]
        public float beatsBeforeAttackLine = 1f;
        [Tooltip("「それは異常です」をスペースで消してから、実際に弾を発射するまでに空ける拍数")]
        public float beatsBeforeFire = 1f;

        [Header("セリフ表示（開始演出・攻撃時、現実パートと同じ見た目）")]
        [Tooltip("セリフ本文を表示するTMP_Text（現実パートのLine Presenterと同じ位置・サイズ）")]
        public TMP_Text attackLineText;
        [Tooltip("話者名を表示するTMP_Text（現実パートのCharacter Nameと同じ見た目）。未設定なら「名前: 本文」のまま1つのテキストに表示する")]
        public TMP_Text characterNameText;
        [Tooltip("attackLineTextの背景パネル（現実パートのLine Presenterと同じ見た目の黒背景）")]
        public GameObject lineBackground;
        [Tooltip("精神世界パートに切り替わった直後、プレイヤー操作待ちで表示するコンタックの一言")]
        public string startLine = "コンタック: 心の声を震めなくちゃ。";
        [Tooltip("怒り自身が名乗るセリフ。表示後、スペースキーで読み進める")]
        [TextArea]
        public string enemyLine = "怒り: わたしは怒り。\n自分を不当に扱うもの、自分を軽視するもの、自分を脅かすものを拒絶したい。";
        [Tooltip("怒りのセリフの後に表示するコンタックの返し。スペースキーで消すと、一拍後に自動で弾を発射する")]
        public string attackLine = "コンタック: それは異常です。";

        [Header("怒り登場時のプレイヤー移動")]
        [Tooltip("怒り登場時に、プレイヤーが怒りの正面・画面中央へ移動するのにかかる時間（秒）")]
        public float moveToCenterDuration = 0.3f;

        private bool battleDefeated = false;
        private Action onBattleFinished;

        /// <summary>
        /// 外部（MinigameLauncherなど）から呼び出して戦闘を開始する。
        /// battleFinishedCallback は怒り撃破時に呼ばれる。
        /// </summary>
        public void StartBattle(Action battleFinishedCallback)
        {
            onBattleFinished = battleFinishedCallback;
            battleDefeated = false;

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
            // --- 0. 開始演出：コンタックの一言を表示し、スペースキー入力を待つ ---
            yield return StartCoroutine(ShowLineAndWaitForSpace(startLine));

            // セリフが消えたら、避けフェーズに向けて移動を解禁する
            if (player != null)
            {
                player.enabled = true;
            }

            // --- 1. BGMを再生しながら、3つの台詞を順番に流す ---
            if (bgm != null)
            {
                bgm.PlayMusic();
            }

            yield return StartCoroutine(RunAvoidPhases());

            // --- 2. 怒り本体が登場。登場と同時にBGMを止める ---
            if (bgm != null)
            {
                bgm.StopMusic();
            }

            enemy.OnDefeated += HandleEnemyDefeated;
            enemy.SetPresent(true);

            // 怒りの正面・画面中央へプレイヤーを移動させる（以後、命中まで手動移動はできない）
            yield return StartCoroutine(MovePlayerToCenter());

            // 即セリフは出さず、一拍分だけ間を置く
            float secondsPerBeat = 60f / bpm;
            yield return new WaitForSeconds(secondsPerBeat * beatsBeforeAttackLine);

            // --- 3. 一拍後、怒り自身のセリフを表示し、スペースキーで読み進める ---
            yield return StartCoroutine(ShowLineAndWaitForSpace(enemyLine));

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

            // --- 5. 怒り戦終了 ---
            onBattleFinished?.Invoke();
        }

        /// <summary>指定したセリフを表示し、スペースキーが押されるまで待ってから隠す。</summary>
        private IEnumerator ShowLineAndWaitForSpace(string text)
        {
            ShowLine(text);

            // 直前の操作（例：戦闘開始の合図になったYarn側のスペース入力）を
            // 誤って拾わないよう、1フレーム待ってから入力受付を始める
            yield return null;

            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }

            HideLine();
        }

        /// <summary>怒り登場時に、プレイヤーを怒りの正面・画面中央へ移動させる。移動中〜移動後は手動操作を止める。</summary>
        private IEnumerator MovePlayerToCenter()
        {
            if (player == null || enemy == null) yield break;

            player.enabled = false;

            Vector3 start = player.transform.position;
            float centerX = (player.minBounds.x + player.maxBounds.x) / 2f;
            float targetY = Mathf.Clamp(enemy.transform.position.y, player.minBounds.y, player.maxBounds.y);
            Vector3 target = new Vector3(centerX, targetY, start.z);

            float t = 0f;
            while (t < moveToCenterDuration)
            {
                t += Time.deltaTime;
                player.transform.position = Vector3.Lerp(start, target, t / moveToCenterDuration);
                yield return null;
            }
            player.transform.position = target;
        }

        private IEnumerator RunAvoidPhases()
        {
            float secondsPerBeat = 60f / bpm;
            float charInterval = secondsPerBeat * beatsPerCharacter;
            float phraseGap = secondsPerBeat * phraseGapBeats;

            for (int p = 0; p < phrases.Length; p++)
            {
                string phrase = phrases[p];

                foreach (char c in phrase)
                {
                    // 全角スペースや改行など、見えない文字は飛ばす
                    if (!char.IsWhiteSpace(c))
                    {
                        SpawnFallingCharacter(c.ToString());
                    }

                    float wait = charInterval + UnityEngine.Random.Range(-spawnJitter, spawnJitter);
                    wait = Mathf.Max(0.05f, wait);
                    yield return new WaitForSeconds(wait);
                }

                // 最後の台詞でなければ、次の台詞との間に少し間を空ける
                if (p < phrases.Length - 1)
                {
                    yield return new WaitForSeconds(phraseGap);
                }
            }

            // 最後の文字が画面外まで流れきるのを待つ
            float travelDistance = spawnXRange.y - wordDestroyXPosition;
            float travelTime = travelDistance / wordSpeed;
            yield return new WaitForSeconds(travelTime);
        }

        private void HandleEnemyDefeated()
        {
            battleDefeated = true;
        }

        private void SpawnFallingCharacter(string character)
        {
            float x = UnityEngine.Random.Range(spawnXRange.x, spawnXRange.y);
            float y = UnityEngine.Random.Range(spawnYRange.x, spawnYRange.y);

            GameObject obj = Instantiate(
                fallingCharacterPrefab,
                new Vector3(x, y, 0f),
                Quaternion.identity,
                transform
            );

            TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = character;
                text.fontSize *= UnityEngine.Random.Range(fontSizeScaleRange.x, fontSizeScaleRange.y);
            }

            FallingWord falling = obj.GetComponent<FallingWord>();
            if (falling == null)
            {
                falling = obj.AddComponent<FallingWord>();
            }
            falling.speed = wordSpeed;
            falling.destroyXPosition = wordDestroyXPosition;
        }

        private void FireDenialBullet()
        {
            // 怒り登場時にプレイヤーを中央へ揃えているため、プレイヤーの実位置から
            // 右方向（怒りの方向）へ飛ばせば自然に命中する
            Vector3 spawnPos = player != null
                ? player.transform.position
                : (bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position);

            Instantiate(denialBulletPrefab, spawnPos, Quaternion.identity);
        }

        /// <summary>
        /// セリフを現実パートと同じ見た目（背景パネル＋話者名＋本文）で表示する。
        /// text は「話者名: 本文」の形式を想定し、最初の「: 」で話者名と本文に分割する。
        /// </summary>
        private void ShowLine(string text)
        {
            string speaker = null;
            string body = text;

            int separatorIndex = text.IndexOf(": ", StringComparison.Ordinal);
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
