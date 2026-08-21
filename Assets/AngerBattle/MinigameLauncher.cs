using System;
using System.Collections;
using System.Threading.Tasks;
using BedFlight;
using UnityEngine;
using Yarn.Unity;

namespace AngerBattle
{
    /// <summary>
    /// Yarn Spinnerの &lt;&lt;start_minigame "..."&gt;&gt; コマンドを受け取り、
    /// 各ステージの精神世界バトル（怒り戦＝IkariBattle、不安戦＝FuanBattleなど）を
    /// 起動・終了するランチャー。
    ///
    /// ミニゲームは別Unity Sceneには分けず、
    /// 普段は非アクティブにしてある battleRoot / fuanBattleRoot を
    /// アクティブ化／非アクティブ化することで同じシーン内で切り替える。
    ///
    /// Anger.yarn 側では Anger_TakeMed ノードの最後に &lt;&lt;start_minigame "IkariBattle"&gt;&gt;、
    /// Anxiety.yarn 側では Anxiety_TakeMed ノードの最後に &lt;&lt;start_minigame "FuanBattle"&gt;&gt;
    /// を追加することで、この処理が呼ばれる想定。
    /// </summary>
    public class MinigameLauncher : MonoBehaviour
    {
        [Tooltip("怒り戦一式をまとめた親オブジェクト（普段は非アクティブにしておく）")]
        public GameObject battleRoot;

        [Tooltip("怒り戦の進行を管理するコントローラー（battleRootの中にあるもの）")]
        public AngerBattleController angerBattleController;

        [Tooltip("不安戦一式をまとめた親オブジェクト（普段は非アクティブにしておく）")]
        public GameObject fuanBattleRoot;

        [Tooltip("不安戦の進行を管理するコントローラー（fuanBattleRootの中にあるもの）")]
        public FuanBattleController fuanBattleController;

        [Tooltip("ベッド飛行一式をまとめた親オブジェクト（普段は非アクティブにしておく）")]
        public GameObject bedFlightRoot;

        [Tooltip("ベッド飛行の進行を管理するコントローラー（bedFlightRootの中にあるもの）")]
        public BedFlightController bedFlightController;

        [Tooltip("精神世界パートに入るたびに挟む「時計＋ノイズ」導入演出（怒り戦・不安戦共通、シーン直下、任意）")]
        public ClockGlitchIntro clockGlitchIntro;

        [Tooltip("精神世界パートで敵を撃破するたびに挟む「Good Morning」演出（怒り戦・不安戦共通、シーン直下、任意）")]
        public GoodMorningOutro goodMorningOutro;

        [Tooltip("敵に攻撃が命中してから「Good Morning」演出が始まるまでに置く間（秒）。命中の余韻を作るため")]
        public float postDefeatPauseSeconds = 1.0f;

        [Tooltip("戦闘中は隠したいダイアログUIなどのルート（任意、未設定でも可）")]
        public GameObject dialogueUIRoot;

        private CanvasGroup dialogueCanvasGroup;
        private TaskCompletionSource<bool> battleFinishedSource;

        // Yarn Spinnerはインスタンスメソッドをコマンド登録すると、
        // 第1引数を「呼び出し対象のGameObject名」として解釈してしまう
        // （<<start_minigame "IkariBattle">>の"IkariBattle"がGameObject名として
        // 検索されてしまい、"IkariBattle doesn't have the correct component"というエラーになる）。
        // これを避けるため、StartMinigameはstaticにし、シーン内のインスタンスを自前で保持する。
        private static MinigameLauncher instance;

        private void Awake()
        {
            instance = this;

            if (dialogueUIRoot != null)
            {
                dialogueCanvasGroup = dialogueUIRoot.GetComponent<CanvasGroup>();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

#if UNITY_EDITOR
        [Header("開発用（エディタ専用・シーンセレクト）")]
        [Tooltip("このキーで、デバッグ用のシーンセレクトメニューの表示/非表示を切り替える（ビルドには含まれない）")]
        public KeyCode debugMenuKey = KeyCode.F1;

        [Tooltip("メニューから直接ジャンプできるYarnのノード名（各ステージの開始ノードなど）")]
        public string[] debugStoryNodes = new string[] { "Anger", "Anxiety" };

        [Tooltip("メニューから直接単体起動できるミニゲーム名")]
        public string[] debugMinigames = new string[] { "IkariBattle", "FuanBattle", "BedFlight" };

        private DialogueRunner debugDialogueRunner;
        private bool debugMenuVisible = false;

        private void Update()
        {
            if (Input.GetKeyDown(debugMenuKey))
            {
                debugMenuVisible = !debugMenuVisible;
            }
        }

        private void OnGUI()
        {
            if (!debugMenuVisible) return;

            GUILayout.BeginArea(new Rect(20, 20, 260, 400), GUI.skin.window);
            GUILayout.Label("デバッグ：シーンセレクト（F1で閉じる）");

            GUILayout.Space(8);
            GUILayout.Label("ストーリー開始ノード");
            foreach (var node in debugStoryNodes)
            {
                if (GUILayout.Button(node))
                {
                    JumpToStoryNode(node);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("ミニゲーム単体起動");
            foreach (var minigame in debugMinigames)
            {
                if (GUILayout.Button(minigame))
                {
                    debugMenuVisible = false;
                    _ = StartMinigame(minigame);
                }
            }

            GUILayout.EndArea();
        }

        private void JumpToStoryNode(string nodeName)
        {
            if (debugDialogueRunner == null)
            {
                debugDialogueRunner = FindFirstObjectByType<DialogueRunner>();
            }
            if (debugDialogueRunner == null)
            {
                Debug.LogWarning("[MinigameLauncher] シーン内にDialogueRunnerが見つからないため、ノードへジャンプできません。");
                return;
            }

            debugMenuVisible = false;

            if (battleRoot != null)
            {
                battleRoot.SetActive(false);
            }
            if (fuanBattleRoot != null)
            {
                fuanBattleRoot.SetActive(false);
            }
            if (bedFlightRoot != null)
            {
                bedFlightRoot.SetActive(false);
            }
            SetDialogueVisible(true);

            _ = debugDialogueRunner.StartDialogue(nodeName);
        }
#endif

        [YarnCommand("start_minigame")]
        public static async Task StartMinigame(string minigameName)
        {
            if (instance == null)
            {
                Debug.LogError("[MinigameLauncher] シーン内にMinigameLauncherが見つかりません。");
                return;
            }

            switch (minigameName)
            {
                case "IkariBattle":
                    await instance.RunAngerBattle();
                    break;
                case "FuanBattle":
                    await instance.RunFuanBattle();
                    break;
                case "BedFlight":
                    await instance.RunBedFlight();
                    break;
                default:
                    Debug.LogWarning($"[MinigameLauncher] 未対応のミニゲーム名です: {minigameName}");
                    break;
            }
        }

        private Task RunAngerBattle()
        {
            return RunBattle(battleRoot, angerBattleController.StartBattle, PlayClockGlitchIntroIfPresent, PlayGoodMorningOutroIfPresent);
        }

        private Task RunFuanBattle()
        {
            return RunBattle(fuanBattleRoot, fuanBattleController.StartBattle, PlayClockGlitchIntroIfPresent, PlayGoodMorningOutroIfPresent);
        }

        /// <summary>
        /// ベッド飛行はコンタックを飲んで怒り・不安を倒した後の話であり、
        /// 服薬直後の「時計＋ノイズ」導入演出（ClockGlitchIntro）や
        /// 撃破後の「Good Morning」演出（GoodMorningOutro、どちらも精神世界への入退室用）は使わない。
        /// 終了演出（暗転）はBedFlightController側で完結させている。
        /// </summary>
        private Task RunBedFlight()
        {
            return RunBattle(bedFlightRoot, bedFlightController.StartBattle, null, null);
        }

        private async Task RunBattle(GameObject root, Action<Action> startBattle, Func<Task> preStartIntro, Func<Action, Task> postBattleOutro)
        {
            battleFinishedSource = new TaskCompletionSource<bool>();

            SetDialogueVisible(false);
            root.SetActive(true);

            if (preStartIntro != null)
            {
                await preStartIntro();
            }

            startBattle(() => OnEnemyDefeated(root, postBattleOutro));

            await battleFinishedSource.Task;
        }

        /// <summary>不安戦開始前の「時計＋ノイズ」導入演出。未設定なら何もしない。</summary>
        private Task PlayClockGlitchIntroIfPresent()
        {
            if (clockGlitchIntro == null)
            {
                return Task.CompletedTask;
            }

            var introFinishedSource = new TaskCompletionSource<bool>();
            StartCoroutine(RunClockGlitchIntroCoroutine(introFinishedSource));
            return introFinishedSource.Task;
        }

        private IEnumerator RunClockGlitchIntroCoroutine(TaskCompletionSource<bool> introFinishedSource)
        {
            yield return StartCoroutine(clockGlitchIntro.Play());
            introFinishedSource.TrySetResult(true);
        }

        /// <summary>
        /// 敵撃破直後に呼ばれる。「Good Morning」演出（未設定なら即座に）で
        /// onReveal（バトルルート非表示・会話UI復帰）を挟んでから、戦闘終了を確定させる。
        /// </summary>
        private async void OnEnemyDefeated(GameObject root, Func<Action, Task> postBattleOutro)
        {
            void Reveal()
            {
                root.SetActive(false);
                SetDialogueVisible(true);
            }

            if (postDefeatPauseSeconds > 0f)
            {
                await Task.Delay(TimeSpan.FromSeconds(postDefeatPauseSeconds));
            }

            if (postBattleOutro != null)
            {
                await postBattleOutro(Reveal);
            }
            else
            {
                Reveal();
            }

            battleFinishedSource?.TrySetResult(true);
        }

        /// <summary>敵撃破後の「Good Morning」演出。未設定ならonRevealを即座に呼ぶだけ。</summary>
        private Task PlayGoodMorningOutroIfPresent(Action onReveal)
        {
            if (goodMorningOutro == null)
            {
                onReveal?.Invoke();
                return Task.CompletedTask;
            }

            var outroFinishedSource = new TaskCompletionSource<bool>();
            StartCoroutine(RunGoodMorningOutroCoroutine(onReveal, outroFinishedSource));
            return outroFinishedSource.Task;
        }

        private IEnumerator RunGoodMorningOutroCoroutine(Action onReveal, TaskCompletionSource<bool> outroFinishedSource)
        {
            yield return StartCoroutine(goodMorningOutro.Play(onReveal));
            outroFinishedSource.TrySetResult(true);
        }

        /// <summary>
        /// 会話UIの表示・非表示を切り替える。
        /// dialogueUIRoot自体をSetActiveで非表示にすると、会話がちょうどタイプライター中
        /// （1文字ずつ表示している最中）だった場合にTextMeshProのCanvas参照が壊れて
        /// NullReferenceExceptionになることがあるため、代わりにCanvasGroupの
        /// alpha/interactable/blocksRaycastsで見た目だけを隠す（Canvas階層は維持する）。
        /// </summary>
        private void SetDialogueVisible(bool visible)
        {
            if (dialogueCanvasGroup != null)
            {
                dialogueCanvasGroup.alpha = visible ? 1f : 0f;
                dialogueCanvasGroup.interactable = visible;
                dialogueCanvasGroup.blocksRaycasts = visible;
            }
            else if (dialogueUIRoot != null)
            {
                // CanvasGroupが見つからない場合のフォールバック。
                dialogueUIRoot.SetActive(visible);
            }
        }
    }
}
