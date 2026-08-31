using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace MemoryRecall
{
    [Serializable]
    public class MapCharacter
    {
        public Transform npcTransform;
        [TextArea] public string line;
        [HideInInspector] public bool hasTalked;
    }

    /// <summary>
    /// 悲しみ編の幼少期の記憶回想。
    /// 家の中で母の言葉を聞く→外へ出る→友達3人と話す→夕方の曲→帰宅→母の言葉、
    /// という2マップ探索を管理する。
    /// </summary>
    public class MemoryRecallController : MonoBehaviour
    {
        private enum RecallPhase
        {
            Intro,
            LeavingHome,
            VisitingFriends,
            ReturningHome,
            FinalHome,
            Finished
        }

        [Header("参照")]
        public AngerBattle.PlayerController player;
        public SadnessMapEnvironment mapEnvironment;

        [Header("お母さん")]
        public Transform motherTransform;
        [TextArea]
        public string motherOpeningLine = "お母さん: おはよう。お昼寝たくさんした？ご飯まだだから、お外で友達と遊んできな。";
        [TextArea]
        public string motherFinalLine = "お母さん: おかえり。ご飯できたから、手洗ってきなぁ。";

        [Header("友達（3人、順不同）")]
        public MapCharacter[] friends = new MapCharacter[]
        {
            new MapCharacter { line = "友達: おさかなさんいっぱいいるよ" },
            new MapCharacter { line = "友達: みてみて。オニヤンマつかまえたよ" },
            new MapCharacter { line = "友達: 宿題やった？？？ぼくまだやってなーい" },
        };

        [Header("インタラクション")]
        public float interactRange = 1.5f;

        [Header("夕方の曲")]
        public AudioSource eveningChimeSource;
        public AudioClip eveningChimeClip;

        [Header("セリフ表示")]
        public TMP_Text lineText;
        public TMP_Text characterNameText;
        public GameObject lineBackground;

        private RecallPhase phase;
        private bool isBusy;
        private bool homeUnlocked;
        private Action onFinished;

        public void StartExploration(Action finishedCallback)
        {
            StopAllCoroutines();
            onFinished = finishedCallback;
            phase = RecallPhase.Intro;
            isBusy = false;
            homeUnlocked = false;

            foreach (MapCharacter friend in friends)
            {
                friend.hasTalked = false;
                if (friend.npcTransform != null)
                {
                    friend.npcTransform.gameObject.SetActive(true);
                }
            }

            if (motherTransform != null)
            {
                motherTransform.gameObject.SetActive(true);
            }

            if (player != null)
            {
                player.enabled = false;
            }

            HideLine();
            mapEnvironment.ShowHome(player, true);
            StartCoroutine(RunIntro());
        }

        private IEnumerator RunIntro()
        {
            isBusy = true;
            yield return ShowLineAndWaitForSpace(motherOpeningLine);
            isBusy = false;
            phase = RecallPhase.LeavingHome;
            if (player != null) player.enabled = true;
        }

        private void Update()
        {
            if (isBusy || phase == RecallPhase.Finished || player == null)
            {
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Space))
            {
                return;
            }

            switch (phase)
            {
                case RecallPhase.LeavingHome:
                    if (IsPlayerNear(mapEnvironment.homeDoor))
                    {
                        mapEnvironment.ShowOutdoor(player, true);
                        phase = RecallPhase.VisitingFriends;
                    }
                    break;

                case RecallPhase.VisitingFriends:
                    MapCharacter nearestFriend = FindNearestUntalkedFriend();
                    if (nearestFriend != null)
                    {
                        StartCoroutine(RunFriendLine(nearestFriend));
                    }
                    break;

                case RecallPhase.ReturningHome:
                    if (homeUnlocked && IsPlayerNear(mapEnvironment.outdoorDoor))
                    {
                        mapEnvironment.ShowHome(player, true);
                        phase = RecallPhase.FinalHome;
                    }
                    break;

                case RecallPhase.FinalHome:
                    if (IsPlayerNear(motherTransform))
                    {
                        StartCoroutine(RunEnding());
                    }
                    break;
            }
        }

        private IEnumerator RunFriendLine(MapCharacter friend)
        {
            isBusy = true;
            if (player != null) player.enabled = false;

            yield return ShowLineAndWaitForSpace(friend.line);
            friend.hasTalked = true;

            if (AllFriendsTalked())
            {
                homeUnlocked = true;
                phase = RecallPhase.ReturningHome;
                if (eveningChimeSource != null && eveningChimeClip != null)
                {
                    eveningChimeSource.PlayOneShot(eveningChimeClip);
                }
            }

            if (player != null) player.enabled = true;
            isBusy = false;
        }

        private IEnumerator RunEnding()
        {
            isBusy = true;
            phase = RecallPhase.Finished;
            if (player != null) player.enabled = false;

            yield return ShowLineAndWaitForSpace(motherFinalLine);

            HideLine();
            mapEnvironment.HideMaps();
            onFinished?.Invoke();
        }

        private bool AllFriendsTalked()
        {
            foreach (MapCharacter friend in friends)
            {
                if (!friend.hasTalked) return false;
            }
            return true;
        }

        private MapCharacter FindNearestUntalkedFriend()
        {
            MapCharacter nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (MapCharacter friend in friends)
            {
                if (friend.hasTalked || friend.npcTransform == null) continue;
                float distance = Vector2.Distance(player.transform.position, friend.npcTransform.position);
                if (distance <= interactRange && distance < nearestDistance)
                {
                    nearest = friend;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private bool IsPlayerNear(Transform target)
        {
            return target != null && mapEnvironment != null &&
                   Vector2.Distance(player.transform.position, target.position) <= interactRange;
        }

        private IEnumerator ShowLineAndWaitForSpace(string text)
        {
            ShowLine(text);
            yield return null;
            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }
            HideLine();
        }

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
                characterNameText.text = speaker ?? string.Empty;
                characterNameText.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            }
            if (lineText != null)
            {
                lineText.text = body;
                lineText.gameObject.SetActive(true);
            }
            if (lineBackground != null) lineBackground.SetActive(true);
        }

        private void HideLine()
        {
            if (lineText != null) lineText.gameObject.SetActive(false);
            if (characterNameText != null) characterNameText.gameObject.SetActive(false);
            if (lineBackground != null) lineBackground.SetActive(false);
        }
    }
}
