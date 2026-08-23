using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace MemoryRecall
{
    /// <summary>
    /// 話しかける対象1人分（友達など）。位置とセリフを持ち、一度話しかけたらhasTalkedがtrueになる。
    /// </summary>
    [Serializable]
    public class MapCharacter
    {
        [Tooltip("この対象の位置（インタラクション距離判定に使う）")]
        public Transform npcTransform;
        [Tooltip("話しかけたときに表示するセリフ（「話者: 本文」の形式）")]
        [TextArea]
        public string line;
        [HideInInspector]
        public bool hasTalked;
    }

    /// <summary>
    /// 「記憶回想」（子供のころの記憶を想起するシーン）の進行を管理するコントローラー。
    ///
    /// 【全体の流れ】
    /// 0. 開始演出：お母さんの一言（motherOpeningLine）を表示し、スペースキー待ち
    /// 1. プレイヤー操作を解禁。主人公は2Dマップ上を自由に移動できる
    /// 2. 友達3人に、それぞれ近づいてスペースキーで話しかけるとセリフが表示される（何度でも近づけるが、
    ///    一度聞いたセリフは記録される。順番は自由）
    /// 3. 友達3人全員に話しかけた後、お母さんに近づいてスペースキーで話しかけると、
    ///    「カラスが鳴いたら帰りましょう」（ナレーション、話者名なし）→お母さんの最後のセリフ、の順に表示され、
    ///    シーン終了（現実に戻る）
    ///
    /// セリフ表示は現実パートと同じ見た目（背景パネル＋話者名＋本文）を流用する。
    /// 話しかけ判定はCollider2D等は使わず、プレイヤーとの距離（interactRange）で簡易判定する。
    /// </summary>
    public class MemoryRecallController : MonoBehaviour
    {
        [Header("参照")]
        public AngerBattle.PlayerController player;

        [Header("お母さん")]
        [Tooltip("お母さんの位置")]
        public Transform motherTransform;
        [Tooltip("シーン開始時、自動で表示されるお母さんの一言")]
        [TextArea]
        public string motherOpeningLine = "お母さん: おはよう。お昼寝たくさんした？ご飯まだだから、お外で友達と遊んできな。";
        [Tooltip("友達3人全員に話しかけた後、お母さんに話しかけると流れるナレーション（話者名なし）")]
        public string crowLine = "カラスが鳴いたら帰りましょう";
        [Tooltip("ナレーションの後に表示するお母さんの最後のセリフ。これが終わるとシーン終了（現実に戻る）")]
        [TextArea]
        public string motherFinalLine = "お母さん: おかえり。ご飯できたから、手洗ってきなぁ。";

        [Header("友達（3人、順不同で話しかけられる）")]
        public MapCharacter[] friends = new MapCharacter[]
        {
            new MapCharacter { line = "友達: おさかなさんいっぱいいるよ" },
            new MapCharacter { line = "友達: みてみて。オニヤンマつかまえたよ" },
            new MapCharacter { line = "友達: 宿題やった？？？ぼくまだやってなーい" },
        };

        [Header("インタラクション")]
        [Tooltip("この距離以内にいる対象に、スペースキーで話しかけられる")]
        public float interactRange = 1.5f;

        [Header("セリフ表示（現実パートと同じ見た目）")]
        [Tooltip("セリフ本文を表示するTMP_Text")]
        public TMP_Text lineText;
        [Tooltip("話者名を表示するTMP_Text")]
        public TMP_Text characterNameText;
        [Tooltip("lineTextの背景パネル")]
        public GameObject lineBackground;

        private bool isBusy;
        private bool endingTriggered;
        private Action onFinished;

        /// <summary>外部（MinigameLauncherなど）から呼び出してシーンを開始する。finishedCallbackはシーン終了時（現実に戻るとき）に呼ばれる。</summary>
        public void StartExploration(Action finishedCallback)
        {
            onFinished = finishedCallback;
            endingTriggered = false;
            isBusy = false;

            foreach (var friend in friends)
            {
                friend.hasTalked = false;
            }

            if (player != null)
            {
                player.enabled = false;
            }
            HideLine();

            StartCoroutine(RunIntro());
        }

        private IEnumerator RunIntro()
        {
            isBusy = true;
            yield return StartCoroutine(ShowLineAndWaitForSpace(motherOpeningLine));
            isBusy = false;

            if (player != null)
            {
                player.enabled = true;
            }
        }

        private void Update()
        {
            if (isBusy || endingTriggered) return;
            if (!Input.GetKeyDown(KeyCode.Space)) return;

            MapCharacter nearestFriend = FindNearestUntalkedFriend();
            if (nearestFriend != null)
            {
                StartCoroutine(RunFriendLine(nearestFriend));
                return;
            }

            if (AllFriendsTalked() && IsPlayerNear(motherTransform))
            {
                StartCoroutine(RunEnding());
            }
        }

        private IEnumerator RunFriendLine(MapCharacter friend)
        {
            isBusy = true;
            if (player != null)
            {
                player.enabled = false;
            }

            yield return StartCoroutine(ShowLineAndWaitForSpace(friend.line));
            friend.hasTalked = true;

            if (player != null)
            {
                player.enabled = true;
            }
            isBusy = false;
        }

        private IEnumerator RunEnding()
        {
            endingTriggered = true;
            isBusy = true;
            if (player != null)
            {
                player.enabled = false;
            }

            yield return StartCoroutine(ShowLineAndWaitForSpace(crowLine));
            yield return StartCoroutine(ShowLineAndWaitForSpace(motherFinalLine));

            HideLine();
            onFinished?.Invoke();
        }

        private bool AllFriendsTalked()
        {
            foreach (var friend in friends)
            {
                if (!friend.hasTalked) return false;
            }
            return true;
        }

        private MapCharacter FindNearestUntalkedFriend()
        {
            MapCharacter nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var friend in friends)
            {
                if (friend.hasTalked || friend.npcTransform == null) continue;
                float dist = Vector2.Distance(player.transform.position, friend.npcTransform.position);
                if (dist <= interactRange && dist < nearestDist)
                {
                    nearest = friend;
                    nearestDist = dist;
                }
            }
            return nearest;
        }

        private bool IsPlayerNear(Transform target)
        {
            if (target == null || player == null) return false;
            return Vector2.Distance(player.transform.position, target.position) <= interactRange;
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
        /// 「: 」を含まない場合は話者名なし（ナレーション）として扱う。
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

            if (lineText != null)
            {
                lineText.text = body;
                lineText.gameObject.SetActive(true);
            }
            if (lineBackground != null)
            {
                lineBackground.SetActive(true);
            }
        }

        private void HideLine()
        {
            if (lineText != null)
            {
                lineText.gameObject.SetActive(false);
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
