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
    /// 0.5. （houseIntro設定時、任意）家からベッドが飛び出すアニメーション（HouseIntro）。
    ///    飛び出した後、家はその場から背景のビルと同じ速度でスクロールしていく
    /// 1. 開放フェーズ（freedomDurationSeconds）：プレイヤーは画面内を自由に移動できる。
    ///    追跡演出は一切出さず、空の色が徐々に明るく開けていく（CityBackgroundScroller.SetOpenness）
    /// 2. クライマックス：ベッドをコンタックの正面（画面中央）へ自動移動させてから、
    ///    コンタックが本登場（ContacChaser.AppearFully）。
    ///    セリフを表示してスペースキーで読み進めた後、一拍置いて必ず命中する一撃（ContacBullet）を放つ
    /// 3. 命中後、画面を暗転させてから終了を通知する（この直後、呼び出し元が元の画面に戻す）
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
        [Tooltip("BGM（湖面のワルツ）の再生を管理するコンポーネント（怒り戦・不安戦と同じBattleBGMを流用）")]
        public AngerBattle.BattleBGM bgm;

        [Tooltip("ベッド（プレイヤー）の色。実行時に専用の単色矩形を生成する")]
        public Color bedColor = new Color(0.62f, 0.45f, 0.32f);

        [Header("セリフ表示（現実パートと同じ見た目、任意）")]
        public TMP_Text attackLineText;
        public TMP_Text characterNameText;
        public GameObject lineBackground;
        [Tooltip("開始時、プレイヤー操作待ちで表示する一言。空文字なら演出をスキップしていきなり操作開始する")]
        public string startLine = "";
        [Tooltip("クライマックスでコンタック本登場後に表示するセリフ")]
        [TextArea]
        public string contacLine = "コンタック: 見つけた。\n現実に戻ろう。";

        [Header("開始演出：家からベッドが飛び出す（任意）")]
        [Tooltip("家のシルエット。設定すると、開始の一言の後、ここからベッドが飛び出すアニメーションが入る。" +
            "未設定なら従来通り、シーン配置のままの位置からいきなり操作可能になる")]
        public HouseIntro houseIntro;
        [Tooltip("家から飛び出して、開放フェーズの開始位置（シーン配置の値）へ移動するのにかける時間（秒）")]
        public float burstOutDuration = 0.4f;

        [Header("開放フェーズ（追跡演出なし・自由に飛べる）")]
        [Tooltip("開放フェーズの長さ（秒）。この間に空の色が徐々に明るく開けていく")]
        public float freedomDurationSeconds = 25f;

        [Header("クライマックス（コンタック本登場〜一撃）")]
        [Tooltip("コンタックの定位置（本登場後にとどまる位置。左端から登場するため負の値）")]
        public Vector3 contacRestingPosition = new Vector3(-4f, 0f, 0f);
        [Tooltip("敵登場と同時に、ベッドをこの秒数で自動移動させる（怒り戦のMovePlayerToCenterと同じ狙い）")]
        public float moveToRestingDuration = 0.3f;
        [Tooltip("本登場時、この距離だけ左側の画面外からスライドしてくる")]
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
        private Vector3 burstOutTargetPosition;
        private SpriteRenderer[] playerSpriteRenderers;
        private int[] playerOriginalSortingOrders;

        /// <summary>
        /// 外部（MinigameLauncherなど）から呼び出して開始する。
        /// battleFinishedCallback はコンタックに撃ち落とされ、暗転が終わった時点で呼ばれる。
        /// </summary>
        public void StartBattle(Action battleFinishedCallback)
        {
            onBattleFinished = battleFinishedCallback;

            if (player != null)
            {
                // 冒頭の一言を読み終える（スペースキーで進める）までは操作不可にする。
                // 有効にしたまま一言を表示すると、まだ読んでいる最中でもベッドが動かせてしまい、
                // 「もう始まっている」と誤解される原因になっていたため。
                player.enabled = false;
                var bedSprite = player.GetComponent<SpriteRenderer>();
                if (bedSprite != null)
                {
                    bedSprite.sprite = CreateSolidSquareSprite();
                    bedSprite.color = bedColor;
                }

                if (houseIntro != null)
                {
                    // 開放フェーズでの本来の開始位置（シーン配置の値）を、家から飛び出した後の
                    // 着地先として覚えておいてから、ベッドを一旦家の中の位置へ移す
                    burstOutTargetPosition = player.transform.position;
                    player.transform.position = houseIntro.GetLaunchStartPosition();

                    // 「家の中に隠れている」ように見せるため、ベッド（＋乗っている人）のSorting Orderを
                    // 一時的に家のシルエットより奥へ沈める。家から飛び出す瞬間（RunHouseBurstOut）に戻す
                    playerSpriteRenderers = player.GetComponentsInChildren<SpriteRenderer>(true);
                    playerOriginalSortingOrders = new int[playerSpriteRenderers.Length];
                    for (int i = 0; i < playerSpriteRenderers.Length; i++)
                    {
                        playerOriginalSortingOrders[i] = playerSpriteRenderers[i].sortingOrder;
                        playerSpriteRenderers[i].sortingOrder = houseIntro.silhouetteSortingOrder - 1;
                    }
                }
            }
            if (contac != null) contac.Hide();
            if (background != null) background.SetOpenness(0f);
            // 冒頭の一言をスペースキーで進めるまでは、背景（ビル・雲）のスクロールも止めておく。
            // プレイヤー操作と同様、動いていると「もう始まっている」と誤解されるため。
            if (background != null) background.SetScrolling(false);
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

            if (houseIntro != null)
            {
                yield return StartCoroutine(RunHouseBurstOut());
            }

            if (player != null) player.enabled = true;
            if (background != null) background.SetScrolling(true);
            if (bgm != null) bgm.PlayMusic();

            yield return StartCoroutine(RunFreedomPhase());
            yield return StartCoroutine(RunClimax());
            yield return StartCoroutine(RunEndFade());

            onBattleFinished?.Invoke();
        }

        /// <summary>家の中の位置から、開放フェーズ本来の開始位置まで、ベッドを一気に飛び出させる。</summary>
        private IEnumerator RunHouseBurstOut()
        {
            if (player == null) yield break;

            // 家の中に隠していたベッドを、飛び出す瞬間に見えるようにする
            if (playerSpriteRenderers != null)
            {
                for (int i = 0; i < playerSpriteRenderers.Length; i++)
                {
                    playerSpriteRenderers[i].sortingOrder = playerOriginalSortingOrders[i];
                }
            }

            Vector3 start = player.transform.position;
            Vector3 target = burstOutTargetPosition;

            float t = 0f;
            while (t < burstOutDuration)
            {
                t += Time.deltaTime;
                player.transform.position = Vector3.Lerp(start, target, t / burstOutDuration);
                yield return null;
            }
            player.transform.position = target;

            houseIntro.StartScrolling();
            // 家が飛び去った後、その場所に空き地（隙間）が残らないよう、ビルを1棟補充する
            if (background != null) background.FillExcludeZone();
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

        private IEnumerator RunClimax()
        {
            if (bgm != null) bgm.StopMusic();

            yield return StartCoroutine(MovePlayerToRestingPosition());

            if (contac != null)
            {
                Vector3 from = contacRestingPosition - new Vector3(appearFromOffsetX, 0f, 0f);
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

        /// <summary>
        /// クライマックス開始時、ベッドを画面中央（コンタックの正面）へ自動移動させる。
        /// 怒り戦・不安戦のMovePlayerToCenter()と同じ狙い（コンタックとベッドが重ならないようにする）。
        /// </summary>
        private IEnumerator MovePlayerToRestingPosition()
        {
            if (player == null) yield break;

            player.enabled = false;

            Vector3 start = player.transform.position;
            float centerX = (player.minBounds.x + player.maxBounds.x) / 2f;
            float targetY = Mathf.Clamp(contacRestingPosition.y, player.minBounds.y, player.maxBounds.y);
            Vector3 target = new Vector3(centerX, targetY, start.z);

            float t = 0f;
            while (t < moveToRestingDuration)
            {
                t += Time.deltaTime;
                player.transform.position = Vector3.Lerp(start, target, t / moveToRestingDuration);
                yield return null;
            }
            player.transform.position = target;
        }

        private IEnumerator FireAndWaitForHit()
        {
            if (contacBulletPrefab == null || player == null)
            {
                yield break;
            }

            Vector3 spawnPos = contac != null ? contac.transform.position : contacRestingPosition;
            GameObject bulletObj = Instantiate(contacBulletPrefab, spawnPos, Quaternion.identity);
            // contacBulletPrefabは（正式なPrefabアセットではなく）シーン上の非アクティブな
            // テンプレートオブジェクトを指すこともあるため、複製後は明示的にアクティブ化する
            bulletObj.SetActive(true);
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

        /// <summary>CityBackgroundScrollerの空・雲・ビルと同じ方式で、単色の正方形スプライトを実行時に生成する。</summary>
        private static Sprite CreateSolidSquareSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }
    }
}
