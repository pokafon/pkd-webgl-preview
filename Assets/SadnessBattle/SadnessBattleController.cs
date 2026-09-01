using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace SadnessBattle
{
    [Serializable]
    public class SadnessTarget
    {
        [TextArea] public string line;
        public AngerBattle.EnemyAnger enemy;
        [HideInInspector] public bool defeated;
    }

    /// <summary>
    /// 2つの記憶マップ上で進行する悲しみコンタック戦。
    /// 屋外の友達3人を撃破→夕方の曲→帰宅→母「おかえり。」→悲しみ登場→母を撃破、
    /// までを一つのミニゲームとして扱う。
    /// </summary>
    public class SadnessBattleController : MonoBehaviour
    {
        private enum BattlePhase
        {
            Intro,
            Friends,
            ReturningHome,
            HomeFinale,
            Finished
        }

        [Header("参照")]
        public AngerBattle.PlayerController player;
        public MemoryRecall.SadnessMapEnvironment mapEnvironment;
        public GameObject denialBulletPrefab;

        [Header("屋外の友達3人")]
        public SadnessTarget[] friendTargets;

        [Header("家の中")]
        public SadnessTarget motherTarget;
        public GameObject sadnessActor;
        [TextArea] public string motherReturnLine = "お母さん: おかえり。";
        [TextArea] public string sadnessLine = "悲しみ: あ…あ…あ…";
        [TextArea] public string finalLine = "コンタック: これで異常なし";
        public float motherLineHoldSeconds = 1.25f;

        [Header("開始")]
        [TextArea] public string startLine = "コンタック: 心の声を鎮めなくちゃ。";

        [Header("インタラクション")]
        public float interactRange = 1.7f;

        [Header("夕方の曲")]
        public AudioSource eveningChimeSource;
        public AudioClip eveningChimeClip;

        [Header("セリフ表示")]
        public TMP_Text attackLineText;
        public TMP_Text characterNameText;
        public GameObject lineBackground;

        private BattlePhase phase;
        private bool isBusy;
        private bool currentTargetDefeated;
        private Action onBattleFinished;

        public void StartBattle(Action battleFinishedCallback)
        {
            StopAllCoroutines();
            onBattleFinished = battleFinishedCallback;
            phase = BattlePhase.Intro;
            isBusy = false;
            currentTargetDefeated = false;

            if (player != null) player.enabled = false;
            HideLine();

            foreach (SadnessTarget target in friendTargets)
            {
                target.defeated = false;
                PrepareTarget(target, true);
            }
            PrepareTarget(motherTarget, false);
            if (sadnessActor != null) sadnessActor.SetActive(false);

            mapEnvironment.ShowOutdoor(player, true);
            StartCoroutine(RunIntro());
        }

        private IEnumerator RunIntro()
        {
            isBusy = true;
            yield return ShowLineAndWaitForSpace(startLine);
            phase = BattlePhase.Friends;
            isBusy = false;
            if (player != null) player.enabled = true;
        }

        private void Update()
        {
            if (isBusy || player == null || !Input.GetKeyDown(KeyCode.Space))
            {
                return;
            }

            if (phase == BattlePhase.Friends)
            {
                SadnessTarget nearest = FindNearestLivingFriend();
                if (nearest != null)
                {
                    StartCoroutine(RunFriendAttack(nearest));
                }
            }
            else if (phase == BattlePhase.ReturningHome && IsPlayerNear(mapEnvironment.outdoorDoor))
            {
                StartCoroutine(RunHomeFinale());
            }
        }

        private IEnumerator RunFriendAttack(SadnessTarget target)
        {
            isBusy = true;
            if (player != null) player.enabled = false;
            SetOnlyColliderEnabled(target);

            yield return ShowLineAndWaitForSpace(target.line);

            currentTargetDefeated = false;
            target.enemy.OnDefeated += HandleTargetDefeated;
            FireToward(target.enemy.transform);
            while (!currentTargetDefeated)
            {
                yield return null;
            }
            target.enemy.OnDefeated -= HandleTargetDefeated;
            target.enemy.SetPresent(false);
            target.defeated = true;
            RestoreFriendColliders();

            if (AllFriendsDefeated())
            {
                phase = BattlePhase.ReturningHome;
                if (eveningChimeSource != null && eveningChimeClip != null)
                {
                    eveningChimeSource.PlayOneShot(eveningChimeClip);
                }
            }

            if (player != null) player.enabled = true;
            isBusy = false;
        }

        private IEnumerator RunHomeFinale()
        {
            isBusy = true;
            phase = BattlePhase.HomeFinale;
            if (player != null) player.enabled = false;

            mapEnvironment.ShowHome(player, true);
            PrepareTarget(motherTarget, true);

            ShowLine(motherReturnLine);
            yield return new WaitForSeconds(motherLineHoldSeconds);

            if (sadnessActor != null) sadnessActor.SetActive(true);
            ShowLine(sadnessLine);

            // 「あ…あ…あ…」まで自動で見せ、この次のスペースキーを母への発砲にする。
            yield return null;
            while (!Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }
            HideLine();

            currentTargetDefeated = false;
            motherTarget.enemy.OnDefeated += HandleTargetDefeated;
            FireToward(motherTarget.enemy.transform);
            while (!currentTargetDefeated)
            {
                yield return null;
            }
            motherTarget.enemy.OnDefeated -= HandleTargetDefeated;
            motherTarget.enemy.SetPresent(false);
            motherTarget.defeated = true;
            if (sadnessActor != null) sadnessActor.SetActive(false);

            yield return ShowLineAndWaitForSpace(finalLine);

            phase = BattlePhase.Finished;
            mapEnvironment.HideMaps();
            onBattleFinished?.Invoke();
        }

        private void PrepareTarget(SadnessTarget target, bool present)
        {
            if (target == null || target.enemy == null) return;
            target.enemy.SetPresent(present, false);
            target.enemy.SetDamageEnabled(true);
        }

        private SadnessTarget FindNearestLivingFriend()
        {
            SadnessTarget nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (SadnessTarget target in friendTargets)
            {
                if (target.defeated || target.enemy == null) continue;
                float distance = Vector2.Distance(player.transform.position, target.enemy.transform.position);
                if (distance <= interactRange && distance < nearestDistance)
                {
                    nearest = target;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private bool AllFriendsDefeated()
        {
            foreach (SadnessTarget target in friendTargets)
            {
                if (!target.defeated) return false;
            }
            return true;
        }

        private bool IsPlayerNear(Transform target)
        {
            return target != null &&
                   Vector2.Distance(player.transform.position, target.position) <= interactRange;
        }

        private void SetOnlyColliderEnabled(SadnessTarget selected)
        {
            foreach (SadnessTarget target in friendTargets)
            {
                if (target.enemy == null) continue;
                Collider2D collider = target.enemy.GetComponent<Collider2D>();
                if (collider != null) collider.enabled = target == selected;
            }
        }

        private void RestoreFriendColliders()
        {
            foreach (SadnessTarget target in friendTargets)
            {
                if (target.enemy == null || target.defeated) continue;
                Collider2D collider = target.enemy.GetComponent<Collider2D>();
                if (collider != null) collider.enabled = true;
            }
        }

        private void FireToward(Transform target)
        {
            if (denialBulletPrefab == null || player == null || target == null) return;
            Vector3 spawnPosition = player.transform.position;
            GameObject bullet = Instantiate(denialBulletPrefab, spawnPosition, Quaternion.identity);
            AngerBattle.DenialBullet denialBullet = bullet.GetComponent<AngerBattle.DenialBullet>();
            if (denialBullet != null)
            {
                denialBullet.Configure(target.position - spawnPosition, Color.white);
            }
        }

        private void HandleTargetDefeated()
        {
            currentTargetDefeated = true;
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
            if (attackLineText != null)
            {
                attackLineText.text = body;
                attackLineText.gameObject.SetActive(true);
            }
            if (lineBackground != null) lineBackground.SetActive(true);
        }

        private void HideLine()
        {
            if (attackLineText != null) attackLineText.gameObject.SetActive(false);
            if (characterNameText != null) characterNameText.gameObject.SetActive(false);
            if (lineBackground != null) lineBackground.SetActive(false);
        }
    }
}
