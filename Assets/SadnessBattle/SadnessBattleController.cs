using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace SadnessBattle
{
    /// <summary>
    /// 撃破対象1体分（お母さん・友達など）。記憶回想シーンで実際に言っていたセリフと、
    /// 見た目・被弾判定を担当するEnemyAnger（怒り戦・不安戦と共通の登場・被弾コンポーネント）を持つ。
    /// </summary>
    [Serializable]
    public class SadnessTarget
    {
        [Tooltip("登場時に表示するセリフ（記憶回想シーンで本人が言っていたものと同じ、「話者: 本文」の形式）")]
        [TextArea]
        public string line;
        [Tooltip("この対象の見た目・登場演出・被弾判定を担当するコンポーネント（怒り戦・不安戦と共通のEnemyAngerを流用）")]
        public AngerBattle.EnemyAnger enemy;
    }

    /// <summary>
    /// 「記憶回想 悲しみコンタックバトル」の進行を管理するコントローラー。
    /// 怒り戦・不安戦とは主人公とコンタックの関係が入れ替わっており、
    /// プレイヤー＝コンタックを操作し、記憶回想シーンに登場した
    /// お母さん・友達3人を、スペースキーで1体ずつ撃破していく（悲しみと決別するため）。
    ///
    /// 【全体の流れ】
    /// 0. 開始演出：コンタックの一言（startLine）を表示し、スペースキー待ち
    /// 1. targetsの順番通りに、1体ずつ登場させる
    ///    ・登場時、その人物が記憶回想シーンで言っていたセリフを表示する
    ///    ・そのセリフをスペースキーで消すと同時に、コンタックが攻撃弾を放って撃破する
    ///      （「セリフを読み終えてスペースを押す」動作そのものが攻撃になる）
    /// 2. 全員撃破したら戦闘終了
    ///
    /// 弾・被弾判定は怒り戦・不安戦のDenialBullet／EnemyAngerをそのまま流用する。
    /// </summary>
    public class SadnessBattleController : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("プレイヤー（＝コンタック）。攻撃弾はこの位置から発射される")]
        public AngerBattle.PlayerController player;
        [Tooltip("BGMの再生を管理するコンポーネント（任意）")]
        public AngerBattle.BattleBGM bgm;
        [Tooltip("攻撃弾の発射位置。未設定ならプレイヤーの位置から発射する")]
        public Transform bulletSpawnPoint;
        [Tooltip("Collider2D（Is Trigger）付きのDenialBulletプレハブ（怒り戦・不安戦と共通）")]
        public GameObject denialBulletPrefab;

        [Header("撃破対象（この順番で1体ずつ登場・撃破）")]
        public SadnessTarget[] targets;

        [Header("セリフ表示（現実パートと同じ見た目）")]
        public TMP_Text attackLineText;
        public TMP_Text characterNameText;
        public GameObject lineBackground;
        [Tooltip("バトル開始時に表示するコンタックの一言")]
        public string startLine = "コンタック: 悲しみと、決別しなくちゃ。";
        [Tooltip("全員撃破後、終了前に表示する一言。空文字なら表示しない")]
        public string levelUpLine = "悲しみが少し、和らいだ。";

        [Header("対象登場時のプレイヤー移動")]
        [Tooltip("対象登場時に、プレイヤーが対象の正面・画面中央へ移動するのにかかる時間（秒）")]
        public float moveToCenterDuration = 0.3f;
        [Tooltip("1体撃破してから次が登場するまでの間（秒）")]
        public float gapBetweenTargets = 0.6f;

        [Header("開始演出：コンタックが家から飛び出す（任意、BedFlightのHouseIntroを流用）")]
        [Tooltip("設定すると、開始の一言の後、コンタックが家の中から本来の開始位置まで飛び出す演出を挟む。未設定なら演出なし（従来通り）")]
        public BedFlight.HouseIntro houseIntro;
        [Tooltip("家の中から本来の開始位置まで飛び出すのにかかる時間（秒）")]
        public float burstOutDuration = 0.4f;

        private bool currentDefeated;
        private Action onBattleFinished;
        private Vector3 burstOutTargetPosition;
        private SpriteRenderer[] playerSpriteRenderers;
        private int[] playerOriginalSortingOrders;

        /// <summary>外部（MinigameLauncherなど）から呼び出して戦闘を開始する。battleFinishedCallbackは全員撃破時に呼ばれる。</summary>
        public void StartBattle(Action battleFinishedCallback)
        {
            onBattleFinished = battleFinishedCallback;

            if (player != null)
            {
                player.enabled = false;

                if (houseIntro != null)
                {
                    // 開始位置（シーン配置の値）を、家から飛び出した後の着地先として覚えておいてから、
                    // コンタックを一旦家の中の位置へ移す（BedFlightController.StartBattle()と同じ考え方）
                    burstOutTargetPosition = player.transform.position;
                    player.transform.position = houseIntro.GetLaunchStartPosition();

                    playerSpriteRenderers = player.GetComponentsInChildren<SpriteRenderer>(true);
                    playerOriginalSortingOrders = new int[playerSpriteRenderers.Length];
                    for (int i = 0; i < playerSpriteRenderers.Length; i++)
                    {
                        playerOriginalSortingOrders[i] = playerSpriteRenderers[i].sortingOrder;
                        playerSpriteRenderers[i].sortingOrder = houseIntro.silhouetteSortingOrder - 1;
                    }
                }
            }
            foreach (var target in targets)
            {
                if (target.enemy != null)
                {
                    target.enemy.SetPresent(false);
                }
            }
            HideLine();

            StartCoroutine(RunBattleSequence());
        }

        private IEnumerator RunBattleSequence()
        {
            yield return StartCoroutine(ShowLineAndWaitForSpace(startLine));

            if (houseIntro != null)
            {
                yield return StartCoroutine(RunHouseBurstOut());
            }

            if (bgm != null)
            {
                bgm.PlayMusic();
            }

            for (int i = 0; i < targets.Length; i++)
            {
                yield return StartCoroutine(RunSingleTarget(targets[i]));

                if (i < targets.Length - 1)
                {
                    yield return new WaitForSeconds(gapBetweenTargets);
                }
            }

            if (bgm != null)
            {
                bgm.StopMusic();
            }

            HideLine();
            onBattleFinished?.Invoke();
        }

        private IEnumerator RunSingleTarget(SadnessTarget target)
        {
            currentDefeated = false;
            target.enemy.OnDefeated += HandleTargetDefeated;
            target.enemy.SetPresent(true);

            yield return StartCoroutine(MovePlayerToCenter(target.enemy.transform));

            // セリフをスペースキーで消すと同時に、コンタックが攻撃を放つ
            yield return StartCoroutine(ShowLineAndWaitForSpace(target.line));
            FireDenialBullet();

            while (!currentDefeated)
            {
                yield return null;
            }

            target.enemy.OnDefeated -= HandleTargetDefeated;

            // 怒り戦・不安戦とは異なり、撃破した人物は白く残さずそのまま消える
            // （思い出のキャラクターと決別する、という演出意図のため）
            target.enemy.SetPresent(false);
        }

        /// <summary>撃破後のレベルアップ一言（levelUpLine）を表示し、スペースキーで読み進める。MinigameLauncher側から呼ぶ。</summary>
        public IEnumerator ShowLevelUpLineAndWait()
        {
            if (string.IsNullOrEmpty(levelUpLine)) yield break;
            yield return StartCoroutine(ShowLineAndWaitForSpace(levelUpLine));
        }

        private void HandleTargetDefeated()
        {
            currentDefeated = true;
        }

        /// <summary>家の中の位置から、本来の開始位置まで、コンタックを一気に飛び出させる。</summary>
        private IEnumerator RunHouseBurstOut()
        {
            if (player == null) yield break;

            // 家の中に隠していたコンタックを、飛び出す瞬間に見えるようにする
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
        }

        /// <summary>対象登場時に、プレイヤーを対象の正面・画面中央へ移動させる。移動中〜移動後は手動操作を止める。</summary>
        private IEnumerator MovePlayerToCenter(Transform target)
        {
            if (player == null || target == null) yield break;

            player.enabled = false;

            Vector3 start = player.transform.position;
            float centerX = (player.minBounds.x + player.maxBounds.x) / 2f;
            float targetY = Mathf.Clamp(target.position.y, player.minBounds.y, player.maxBounds.y);
            Vector3 dest = new Vector3(centerX, targetY, start.z);

            float t = 0f;
            while (t < moveToCenterDuration)
            {
                t += Time.deltaTime;
                player.transform.position = Vector3.Lerp(start, dest, t / moveToCenterDuration);
                yield return null;
            }
            player.transform.position = dest;
        }

        private void FireDenialBullet()
        {
            Vector3 spawnPos = player != null
                ? player.transform.position
                : (bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position);

            Instantiate(denialBulletPrefab, spawnPos, Quaternion.identity);
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
