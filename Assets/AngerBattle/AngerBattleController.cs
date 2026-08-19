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
    /// 1. BGM（Trick_style）を再生しながら、3つの台詞を順番に
    ///    1文字ずつバラバラの高さ・タイミングで右から左へ流す
    ///    （「人から奪うだけのくせに…」→「消えてしまえばいいのに」→「全部気に入らない」）
    /// 2. 3つとも避け終えたら、最後に1回だけ「怒り」本体が登場する
    ///    ・登場と同時にBGMを止める
    ///    ・即セリフは出さず、1拍分だけ間を置く
    /// 3. 一拍後、自動で「それは異常です」というセリフを表示する（プレイヤー操作なし）
    /// 4. プレイヤーがEnterキーを押すと、見た目はシンプルな弾を発射する
    /// 5. 一発ヒットで怒りを撃破し、怒り戦終了
    ///
    /// 被弾してもペナルティ・ゲームオーバーはない（避けるのは演出目的）。
    ///
    /// ※「敵が登場後、一言喋ってからセリフを出す」演出は保留中。
    ///   将来追加する場合は、ShowAttackLine() を呼ぶ前に
    ///   敵のセリフ表示処理を挟む形になる想定。
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
            "人から奪うだけのくせに…",
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

        [Header("怒り登場演出")]
        [Tooltip("怒り登場からセリフ表示までに空ける拍数（現状は1拍）")]
        public float beatsBeforeAttackLine = 1f;

        [Header("攻撃時のセリフ表示（怒りの登場から一拍後に自動表示）")]
        [Tooltip("「それは異常です」などのセリフを表示するTMP_Text（Yarnの通常セリフ表示と似た見た目のものを用意する）")]
        public TMP_Text attackLineText;
        [Tooltip("怒りが登場した後に自動で表示するセリフ")]
        public string attackLine = "それは異常です";

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

            if (enemy != null)
            {
                enemy.SetPresent(false);
            }
            HideAttackLine();

            StartCoroutine(RunBattleSequence());
        }

        private IEnumerator RunBattleSequence()
        {
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

            // 即セリフは出さず、一拍分だけ間を置く
            float secondsPerBeat = 60f / bpm;
            yield return new WaitForSeconds(secondsPerBeat * beatsBeforeAttackLine);

            // --- 3. 一拍後、自動でセリフを表示 ---
            ShowAttackLine();

            // --- 4. Enterで攻撃、命中で撃破 ---
            while (!battleDefeated)
            {
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    FireDenialBullet();
                }
                yield return null;
            }

            enemy.OnDefeated -= HandleEnemyDefeated;
            enemy.SetPresent(false);
            HideAttackLine();

            // --- 5. 怒り戦終了 ---
            onBattleFinished?.Invoke();
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
            Vector3 spawnPos = bulletSpawnPoint != null
                ? bulletSpawnPoint.position
                : (player != null ? player.transform.position : transform.position);

            Instantiate(denialBulletPrefab, spawnPos, Quaternion.identity);
        }

        private void ShowAttackLine()
        {
            if (attackLineText == null) return;
            attackLineText.text = attackLine;
            attackLineText.gameObject.SetActive(true);
        }

        private void HideAttackLine()
        {
            if (attackLineText == null) return;
            attackLineText.gameObject.SetActive(false);
        }
    }
}
