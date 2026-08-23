using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 不安戦（精神パート2）全体の進行を管理するコントローラー。
    /// 怒り戦（AngerBattleController）と同じ大枠を踏襲しつつ、以下が異なる：
    /// ・弾幕は主軸3台詞（mainPhrases）＋合いの手2種（fillerPhrases、それぞれ複数回・ランダムな位置に挿入）
    /// ・合いの手は文字色が異なる（fillerColor、不安本体と同じ紫）
    /// ・弾幕の文字が不規則に方向転換しながら流れる（FallingWord.erraticMovement）
    /// ・敵（不安）のセリフが2ブロックに分かれており、それぞれスペースキーで読み進める
    ///
    /// 【全体の流れ】
    /// 0. 開始演出：コンタックの一言（startLine）を現実パートと同じ見た目で表示し、
    ///    スペースキーが押されるまで待つ（怒り戦と文言も共通）
    /// 1. BGM（ワスレナグサ）を再生しながら、主軸3台詞＋合いの手を
    ///    1文字ずつ、不規則に方向転換しながら右から左へ流す
    /// 2. すべて避け終えたら、最後に1回だけ「不安」本体が登場する
    ///    ・登場と同時にBGMを止める
    ///    ・登場と同時に、プレイヤーを不安の正面・画面中央へ自動移動させる
    ///    ・即セリフは出さず、1拍分だけ間を置く
    /// 3. 一拍後、「不安」自身のセリフをenemyLinesの順番で1ブロックずつ表示し、
    ///    それぞれスペースキーで読み進める
    /// 3b. 続けてコンタックの返し（attackLine）を表示し、スペースキーで消す
    /// 4. セリフを消してから一拍待ち、プレイヤー操作なしで自動的に弾を発射する
    /// 5. 一発ヒットで不安を撃破し、不安戦終了
    ///
    /// 被弾してもペナルティ・ゲームオーバーはない（避けるのは演出目的）。
    /// 戦闘中の操作キーはすべてスペースキーに統一している（現実パートの会話送りと同じキー）。
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
        [Tooltip("TMP_Textを持つ、1文字を表示するためのプレハブ")]
        public GameObject fallingCharacterPrefab;

        [Header("台詞（主軸3つ、この順番で1回ずつ・句読点なし）")]
        public string[] mainPhrases = new string[]
        {
            "失敗したなダメだな恥ずかしいな",
            "どうしようぐるぐる思考が止まらない",
            "すべてリセットしたい"
        };

        [Header("台詞（合いの手。主軸が流れている間、ランダムなタイミングで重なって出現・句読点なし）")]
        public string[] fillerPhrases = new string[]
        {
            "でも",
            "考えない"
        };
        [Tooltip("合いの手の台詞を、それぞれ何回ずつ挿入するか")]
        public int fillerRepeatCount = 3;
        [Tooltip("合いの手の台詞の文字色（不安本体の色に合わせた紫）")]
        public Color fillerColor = new Color(0.5764706f, 0.34509805f, 0.827451f, 1f);
        [Tooltip("主軸の台詞の文字色")]
        public Color mainPhraseColor = Color.white;

        [Header("弾幕タイミング（BGM: ワスレナグサ, 95BPM基準）")]
        [Tooltip("BGMのBPM")]
        public float bpm = 95f;

        [Tooltip("何拍ごとに1文字出すか（0.5=半拍、1=1拍、2=2拍）")]
        public float beatsPerCharacter = 1f;

        [Tooltip("台詞と台詞の間に空ける拍数")]
        public float phraseGapBeats = 2f;

        [Tooltip("出現タイミングのばらつき（秒）。0にするとジャストタイミングになる")]
        public float spawnJitter = 0.08f;

        [Header("文字の移動・出現範囲（不規則に方向転換しながら流れる）")]
        [Tooltip("文字が出現するX座標の範囲（画面右側）")]
        public Vector2 spawnXRange = new Vector2(10f, 12f);
        [Tooltip("文字が出現するY座標の範囲（高さのばらつき）")]
        public Vector2 spawnYRange = new Vector2(-3.5f, 3.5f);
        [Tooltip("このX座標より左に出た文字は消える")]
        public float wordDestroyXPosition = -12f;
        [Tooltip("蛇行：方向・速度を変える間隔（秒）の基準値")]
        public float erraticChangeInterval = 0.3f;
        [Tooltip("蛇行：方向転換の角度範囲（度）。左方向を基準に±この角度でばらける")]
        public float erraticAngleSpread = 50f;
        [Tooltip("蛇行：変化ごとの速度範囲")]
        public Vector2 erraticSpeedRange = new Vector2(2f, 7f);
        [Tooltip("最後の文字が画面外まで流れきるのを待つ時間の見積もりに使う速度")]
        public float travelTimeEstimateSpeed = 6f;
        [Tooltip("同じ台詞内で、後の文字が前の文字を追い越さないように保つ最小間隔")]
        public float leaderMinGap = 0.5f;
        [Tooltip("前の文字との間隔がこの範囲まで縮まったら、ぶつかる前に滑らかに減速し始める")]
        public float leaderCatchUpSoftZone = 1.5f;

        [Header("不安登場演出")]
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
        [Tooltip("不安自身が名乗るセリフ。ブロックごとにスペースキーで読み進める")]
        [TextArea]
        public string[] enemyLines = new string[]
        {
            "不安: わたしは不安。\n自分を傷つけるもの避けたい。穏やかな暮らしを送りたい。本当は悩みたくなんてない。",
            "不安: 優しい世界が欲しいだけ。"
        };
        [Tooltip("不安のセリフの後に表示するコンタックの返し。スペースキーで消すと、一拍後に自動で弾を発射する")]
        public string attackLine = "コンタック: それは異常です。";
        [Tooltip("撃破直後、Good Morning演出の前に表示する一言（レベルアップ演出）。空文字なら表示しない")]
        public string levelUpLine = "心が少し軽くなった。";

        [Header("不安登場時のプレイヤー移動")]
        [Tooltip("不安登場時に、プレイヤーが不安の正面・画面中央へ移動するのにかかる時間（秒）")]
        public float moveToCenterDuration = 0.3f;

        private bool battleDefeated = false;
        private Action onBattleFinished;

        /// <summary>
        /// 外部（MinigameLauncherなど）から呼び出して戦闘を開始する。
        /// battleFinishedCallback は不安撃破時に呼ばれる。
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

            // --- 1. BGMを再生しながら、主軸3台詞＋合いの手を流す ---
            if (bgm != null)
            {
                bgm.PlayMusic();
            }

            yield return StartCoroutine(RunAvoidPhases());

            // --- 2. 不安本体が登場。登場と同時にBGMを止める ---
            if (bgm != null)
            {
                bgm.StopMusic();
            }

            enemy.OnDefeated += HandleEnemyDefeated;
            enemy.SetPresent(true);

            // 不安の正面・画面中央へプレイヤーを移動させる（以後、命中まで手動移動はできない）
            yield return StartCoroutine(MovePlayerToCenter());

            // 即セリフは出さず、一拍分だけ間を置く
            float secondsPerBeat = 60f / bpm;
            yield return new WaitForSeconds(secondsPerBeat * beatsBeforeAttackLine);

            // --- 3. 一拍後、不安自身のセリフをブロックごとに表示し、スペースキーで読み進める ---
            for (int i = 0; i < enemyLines.Length; i++)
            {
                yield return StartCoroutine(ShowLineAndWaitForSpace(enemyLines[i]));
            }

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
            onBattleFinished?.Invoke();
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

            float t = 0f;
            while (t < moveToCenterDuration)
            {
                t += Time.deltaTime;
                player.transform.position = Vector3.Lerp(start, target, t / moveToCenterDuration);
                yield return null;
            }
            player.transform.position = target;
        }

        /// <summary>
        /// 主軸3台詞は順番通りに流しつつ、合いの手（でもでもでも／考えない考えない考えない）は
        /// 主軸が流れている間、ランダムなタイミングで重なって同時に流れる。
        /// </summary>
        private IEnumerator RunAvoidPhases()
        {
            float secondsPerBeat = 60f / bpm;
            float charInterval = secondsPerBeat * beatsPerCharacter;
            float phraseGap = secondsPerBeat * phraseGapBeats;

            // 主軸3台詞を流し終えるまでのおおよその時間を見積もる
            // （この時間の範囲内に、合いの手をランダムなタイミングで重ねて出す）
            float mainDuration = 0f;
            for (int i = 0; i < mainPhrases.Length; i++)
            {
                mainDuration += mainPhrases[i].Length * charInterval;
                if (i < mainPhrases.Length - 1)
                {
                    mainDuration += phraseGap;
                }
            }

            int runningCount = 0;

            runningCount++;
            StartCoroutine(RunPhraseSequence(mainPhrases, mainPhraseColor, charInterval, phraseGap, () => runningCount--));

            var fillerInstances = new List<string>();
            foreach (var f in fillerPhrases)
            {
                for (int i = 0; i < fillerRepeatCount; i++)
                {
                    fillerInstances.Add(f);
                }
            }

            foreach (var filler in fillerInstances)
            {
                float startDelay = UnityEngine.Random.Range(0f, mainDuration);
                runningCount++;
                StartCoroutine(RunScatteredFiller(filler, startDelay, charInterval, () => runningCount--));
            }

            yield return new WaitUntil(() => runningCount <= 0);

            // 最後の文字が画面外まで流れきるのを待つ（蛇行するため、直線移動より少し余裕を持たせる）
            float travelDistance = spawnXRange.y - wordDestroyXPosition;
            float travelTime = travelDistance / travelTimeEstimateSpeed;
            yield return new WaitForSeconds(travelTime);
        }

        /// <summary>複数の台詞を、この順番のまま1つずつ流す（主軸用）。</summary>
        private IEnumerator RunPhraseSequence(string[] phraseList, Color color, float charInterval, float phraseGap, Action onComplete)
        {
            for (int p = 0; p < phraseList.Length; p++)
            {
                FallingWord previous = null;
                foreach (char c in phraseList[p])
                {
                    if (!char.IsWhiteSpace(c))
                    {
                        previous = SpawnFallingCharacter(c.ToString(), color, previous);
                    }

                    float wait = charInterval + UnityEngine.Random.Range(-spawnJitter, spawnJitter);
                    wait = Mathf.Max(0.05f, wait);
                    yield return new WaitForSeconds(wait);
                }

                if (p < phraseList.Length - 1)
                {
                    yield return new WaitForSeconds(phraseGap);
                }
            }

            onComplete?.Invoke();
        }

        /// <summary>指定した遅延の後、1つの合いの手台詞を流す（主軸と並行して動く）。</summary>
        private IEnumerator RunScatteredFiller(string phrase, float startDelay, float charInterval, Action onComplete)
        {
            yield return new WaitForSeconds(startDelay);

            FallingWord previous = null;
            foreach (char c in phrase)
            {
                if (!char.IsWhiteSpace(c))
                {
                    previous = SpawnFallingCharacter(c.ToString(), fillerColor, previous);
                }

                float wait = charInterval + UnityEngine.Random.Range(-spawnJitter, spawnJitter);
                wait = Mathf.Max(0.05f, wait);
                yield return new WaitForSeconds(wait);
            }

            onComplete?.Invoke();
        }

        private void HandleEnemyDefeated()
        {
            battleDefeated = true;
        }

        /// <summary>
        /// 1文字分の落下文字を生成する。leaderに同じ台詞内で直前に出した文字を渡すと、
        /// 追い越さないよう足止めされる（戻り値を次の文字のleaderとして渡していく）。
        /// </summary>
        private FallingWord SpawnFallingCharacter(string character, Color color, FallingWord leader)
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
                text.color = color;
            }

            FallingWord falling = obj.GetComponent<FallingWord>();
            if (falling == null)
            {
                falling = obj.AddComponent<FallingWord>();
            }
            falling.destroyXPosition = wordDestroyXPosition;
            falling.erraticMovement = true;
            falling.erraticChangeInterval = erraticChangeInterval;
            falling.erraticAngleSpread = erraticAngleSpread;
            falling.erraticSpeedRange = erraticSpeedRange;
            falling.leader = leader;
            falling.minLeaderGap = leaderMinGap;
            falling.catchUpSoftZone = leaderCatchUpSoftZone;

            return falling;
        }

        private void FireDenialBullet()
        {
            // 不安登場時にプレイヤーを中央へ揃えているため、プレイヤーの実位置から
            // 右方向（不安の方向）へ飛ばせば自然に命中する
            Vector3 spawnPos = player != null
                ? player.transform.position
                : (bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position);

            Instantiate(denialBulletPrefab, spawnPos, Quaternion.identity);
        }

        /// <summary>セリフを現実パートと同じ見た目（背景パネル＋話者名＋本文）で表示する。</summary>
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
