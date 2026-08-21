using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// 新ミニゲーム「ベッド飛行」全体の進行を管理するコントローラー。
    ///
    /// 【企画意図】
    /// コンタックを飲んで怒り・不安を倒し社会になじんだ主人公が、疲れてしまって
    /// 「どこか遠くへ行きたい」と思ったらベッドが宙に浮かび、街の上を自由に飛び回れる。
    /// ただし最終的にはコンタックが現れ、主人公を一撃で撃ち落として現実へ連れ戻す。
    ///
    /// 【全体の流れ】
    /// 0. （任意）開始演出の一言を表示し、スペースキー入力を待つ
    /// 1. 開放フェーズ（freedomDurationSeconds）：プレイヤーは画面内を自由に移動できる。
    ///    追跡演出は一切出さず、空の色が徐々に明るく開けていく（CityBackgroundScroller.SetOpenness）
    /// 2. チラ見せフェーズ（chaseDurationSeconds）：開放感を保ったまま、コンタックが画面端に
    ///    一瞬だけ姿を見せては引っ込む（ContacChaser.Peek）。時間とともに頻度が増え、距離が近くなる
    /// 3. クライマックス：プレイヤー操作を止め、コンタックが本登場（ContacChaser.AppearFully）。
    ///    セリフを表示してスペースキーで読み進めた後、一拍置いて必ず命中する一撃（ContacBullet）を放つ
    /// 4. 命中後、画面を暗転させてから終了を通知する（この直後、呼び出し元が元の画面に戻す）
    ///
    /// このミニゲームには現状「よけて生き残る」要素はない
    /// （企画上、最後は必ずコンタックに見つかって連れ戻される想定のため）。
    /// 本編（Yarn）との接続はまだ無く、MinigameLauncherのデバッグメニューから単体起動して確認する想定。
    /// </summary>
    public class BedFlightController : MonoBehaviour
    {
        [Header("参照")]
        public AngerBattle.PlayerController player;
        public CityBackgroundScroller background;
        public ContacChaser contac;
        [Tooltip("Collider2D（Is Trigger）付きのContacBulletプレハブ")]
        public GameObject contacBulletPrefab;

        [Header("セリフ表示（現実パートと同じ見た目、任意）")]
        public TMP_Text attackLineText;
        public TMP_Text characterNameText;
        public GameObject lineBackground;
        [Tooltip("開始時、プレイヤー操作待ちで表示する一言。空文字なら演出をスキップしていきなり操作開始する")]
        public string startLine = "主人公: 少しだけ、遠くに行きたい。";
        [Tooltip("クライマックスでコンタック本登場後に表示するセリフ")]
        [TextArea]
        public string contacLine = "コンタック: 見つけた。\n現実に戻ろう。";

        [Header("開放フェーズ（追跡演出なし・自由に飛べる）")]
        [Tooltip("開放フェーズの長さ（秒）。この間に空の色が徐々に明るく開けていく")]
        public float freedomDurationSeconds = 14f;

        [Header("チラ見せフェーズ（開放感を保ちつつ、少しずつ緊張を積む）")]
        [Tooltip("チラ見せフェーズの長さ（秒）")]
        public float chaseDurationSeconds = 9f;
        [Tooltip("チラ見せの間隔（フェーズ開始直後・秒）")]
        public float peekIntervalStart = 3.5f;
        [Tooltip("チラ見せの間隔（フェーズ終盤・秒）。だんだん短くなる")]
        public float peekIntervalEnd = 0.9f;
        [Tooltip("1回のチラ見せを表示し続ける時間（秒）")]
        public float peekShowDuration = 0.5f;
        [Tooltip("チラ見せが出現するX座標（フェーズ開始直後、画面端寄り）")]
        public float peekEdgeXFar = 7.5f;
        [Tooltip("チラ見せが出現するX座標（フェーズ終盤、少し内側まで近づく）")]
        public float peekEdgeXNear = 5f;
        [Tooltip("チラ見せが出現するY座標の範囲")]
        public Vector2 peekYRange = new Vector2(-3f, 3f);

        [Header("クライマックス（コンタック本登場〜一撃）")]
        [Tooltip("コンタックの定位置（本登場後にとどまる位置）")]
        public Vector3 contacRestingPosition = new Vector3(4f, 0f, 0f);
        [Tooltip("本登場時、この距離だけ右側の画面外からスライドしてくる")]
        public float appearFromOffsetX = 6f;
        [Tooltip("本登場のスライドインにかける時間（秒）")]
        public float appearDuration = 0.8f;
        [Tooltip("本登場からセリフ表示までに空ける間（秒）")]
        public float beatBeforeLineSeconds = 0.6f;
        [Tooltip("セリフを消してから、実際に弾を発射するまでに空ける間（秒）")]
        public float beatBeforeFireSeconds = 0.6f;
        [Tooltip("命中してから画面が暗転し始めるまでの間（秒）")]
        public float postHitPauseSeconds = 0.6f;

        [Header("終了演出（現実へ戻る暗転）")]
        [Tooltip("暗転に使う全画面パネル（Image、初期アルファ0）")]
        public CanvasGroup endFadeGroup;
        public float fadeDuration = 1.2f;

        private Action onBattleFinished;

        /// <summary>
        /// 外部（MinigameLauncherなど）から呼び出して開始する。
        /// battleFinishedCallback はコンタックに撃ち落とされ、暗転が終わった時点で呼ばれる。
        /// </summary>
        public void StartBattle(Action battleFinishedCallback)
        {
            onBattleFinished = battleFinishedCallback;

            if (player != null) player.enabled = true;
            if (contac != null) contac.Hide();
            if (background != null) background.SetOpenness(0f);
            if (background != null) background.SetTension(0f);
            if (endFadeGroup != null) endFadeGroup.alpha = 0f;
            HideLine();

            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            if (!string.IsNullOrEmpty(startLine))
            {
                yield return StartCoroutine(ShowLineAndWaitForSpace(startLine));
            }

            yield return StartCoroutine(RunFreedomPhase());
            yield return StartCoroutine(RunPeekPhase());
            yield return StartCoroutine(RunClimax());
            yield return StartCoroutine(RunEndFade());

            onBattleFinished?.Invoke();
        }

        private IEnumerator RunFreedomPhase()
        {
            float t = 0f;
            while (t < freedomDurationSeconds)
            {
                t += Time.deltaTime;
                if (background != null)
                {
                    background.SetOpenness(t / freedomDurationSeconds);
                }
                yield return null;
            }
            if (background != null) background.SetOpenness(1f);
        }

        private IEnumerator RunPeekPhase()
        {
            float elapsed = 0f;
            while (elapsed < chaseDurationSeconds)
            {
                float progress = Mathf.Clamp01(elapsed / chaseDurationSeconds);
                if (background != null) background.SetTension(progress);

                float interval = Mathf.Lerp(peekIntervalStart, peekIntervalEnd, progress);
                float wait = UnityEngine.Random.Range(interval * 0.7f, interval * 1.3f);
                yield return new WaitForSeconds(wait);
                elapsed += wait;

                if (contac != null)
                {
                    Vector3 peekPos = RandomPeekPosition(progress);
                    contac.Peek(peekPos, peekShowDuration);
                }
            }
        }

        private Vector3 RandomPeekPosition(float progress)
        {
            float edgeX = Mathf.Lerp(peekEdgeXFar, peekEdgeXNear, progress);
            float y = UnityEngine.Random.Range(peekYRange.x, peekYRange.y);
            return new Vector3(edgeX, y, 0f);
        }

        private IEnumerator RunClimax()
        {
            if (player != null) player.enabled = false;

            if (contac != null)
            {
                Vector3 from = contacRestingPosition + new Vector3(appearFromOffsetX, 0f, 0f);
                yield return StartCoroutine(contac.AppearFully(from, contacRestingPosition, appearDuration));
            }

            yield return new WaitForSeconds(beatBeforeLineSeconds);

            if (!string.IsNullOrEmpty(contacLine))
            {
                yield return StartCoroutine(ShowLineAndWaitForSpace(contacLine));
            }

            yield return new WaitForSeconds(beatBeforeFireSeconds);

            yield return StartCoroutine(FireAndWaitForHit());

            yield return new WaitForSeconds(postHitPauseSeconds);
        }

        private IEnumerator FireAndWaitForHit()
        {
            if (contacBulletPrefab == null || player == null)
            {
                yield break;
            }

            Vector3 spawnPos = contac != null ? contac.transform.position : contacRestingPosition;
            GameObject bulletObj = Instantiate(contacBulletPrefab, spawnPos, Quaternion.identity);
            ContacBullet bullet = bulletObj.GetComponent<ContacBullet>();

            bool hit = false;
            bullet.OnHitPlayer += () => hit = true;
            bullet.Fire(player.transform.position);

            while (!hit)
            {
                yield return null;
            }
        }

        private IEnumerator RunEndFade()
        {
            if (endFadeGroup == null) yield break;

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                endFadeGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            endFadeGroup.alpha = 1f;
        }

        /// <summary>指定したセリフを表示し、スペースキーが押されるまで待ってから隠す。</summary>
        private IEnumerator ShowLineAndWaitForSpace(string text)
        {
            ShowLine(text);

            // 直前の操作（戦闘開始の合図など）を誤って拾わないよう、1フレーム待ってから入力受付を始める
            yield return null;

            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }

            HideLine();
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
            if (attackLineText != null) attackLineText.gameObject.SetActive(false);
            if (characterNameText != null) characterNameText.gameObject.SetActive(false);
            if (lineBackground != null) lineBackground.SetActive(false);
        }
    }
}
