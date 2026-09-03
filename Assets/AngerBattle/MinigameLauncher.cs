using System;
using System.Collections;
using System.Threading.Tasks;
using BedFlight;
using PKD.Emotions;
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

        [Tooltip("記憶回想一式をまとめた親オブジェクト（普段は非アクティブにしておく）")]
        public GameObject memoryRecallRoot;

        [Tooltip("記憶回想の進行を管理するコントローラー（memoryRecallRootの中にあるもの）")]
        public MemoryRecall.MemoryRecallController memoryRecallController;

        [Tooltip("記憶回想 悲しみコンタックバトル一式をまとめた親オブジェクト（普段は非アクティブにしておく）")]
        public GameObject sadnessBattleRoot;

        [Tooltip("記憶回想 悲しみコンタックバトルの進行を管理するコントローラー（sadnessBattleRootの中にあるもの）")]
        public SadnessBattle.SadnessBattleController sadnessBattleController;

        [Tooltip("記憶回想と悲しみコンタック戦で共有する屋外／屋内マップ")]
        public MemoryRecall.SadnessMapEnvironment sadnessMapEnvironment;

        [Tooltip("精神世界パートに入るたびに挟む「時計＋ノイズ」導入演出（怒り戦・不安戦共通、シーン直下、任意）")]
        public ClockGlitchIntro clockGlitchIntro;

        [Tooltip("敵撃破後、Good Morning演出の前に挟む「目覚めの時計」演出（3:00→5:30、怒り戦・不安戦共通、シーン直下、任意）")]
        public ClockGlitchIntro wakeGlitchIntro;

        [Tooltip("精神世界パートで敵を撃破するたびに挟む「Good Morning」演出（怒り戦・不安戦共通、シーン直下、任意）")]
        public GoodMorningOutro goodMorningOutro;

        [Tooltip("撃破直後のレベルアップ演出（「頭が少しすっきりした」等）で鳴らす音（怒り戦・不安戦共通、任意）")]
        public AudioClip levelUpClip;

        [Header("ゲーム全体の音量")]
        [Range(0f, 1f)]
        [Tooltip("すべてのBGM・SE・チャイムへ一括で掛ける音量。0.7で従来より約3割小さい")]
        public float globalAudioVolume = 0.7f;

        [Tooltip("敵に攻撃が命中してから「Good Morning」演出が始まるまでに置く間（秒）。命中の余韻を作るため")]
        [Range(0f, 5f)]
        public float postDefeatPauseSeconds = 1.0f;

        [Tooltip("戦闘中は隠したいダイアログUIなどのルート（任意、未設定でも可）")]
        public GameObject dialogueUIRoot;

        [Tooltip("戦闘中は隠したい背景・立ち絵（DialogueVisuals）のルート（任意、未設定でも可）")]
        public GameObject dialogueVisualsRoot;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("WebGL確認用クイック起動ボタン（開発ビルド専用。製品ビルドには一切含まれない）")]
        [Tooltip("画面右上に「FuanBattle」ボタンを常時表示し、押すと物語を進めず不安戦を直接開始する。F1デバッグメニューが使えないWebGLビルドでの確認用。UNITY_EDITORまたはDEVELOPMENT_BUILDでのみコンパイルされる")]
        public bool showWebGLQuickDebugButton = true;
#endif

        private CanvasGroup dialogueCanvasGroup;
        private TaskCompletionSource<bool> battleFinishedSource;
        private Task activeBattleTask;
        private GameObject activeBattleRoot;
        private bool battleInProgress;
        private bool battleFinishing;
        private int activeBattleRunId;

        // Yarn Spinnerはインスタンスメソッドをコマンド登録すると、
        // 第1引数を「呼び出し対象のGameObject名」として解釈してしまう
        // （<<start_minigame "IkariBattle">>の"IkariBattle"がGameObject名として
        // 検索されてしまい、"IkariBattle doesn't have the correct component"というエラーになる）。
        // これを避けるため、StartMinigameはstaticにし、シーン内のインスタンスを自前で保持する。
        private static MinigameLauncher instance;

        private void Awake()
        {
            instance = this;
            EmotionRouteState.Reset();
            AudioListener.volume = Mathf.Clamp01(globalAudioVolume);

            if (dialogueUIRoot != null)
            {
                dialogueCanvasGroup = dialogueUIRoot.GetComponent<CanvasGroup>();
            }

            // シーン保存時の状態がどうであれ、ゲーム開始時は必ず全ての戦闘ルートを
            // 非アクティブにする（誤ってアクティブなまま保存されていると、
            // 使っていないミニゲームの背景・物理演算などがPlay開始直後から動き続けて
            // 重くなる原因になるため、ここで安全側に倒す）。
            if (battleRoot != null) battleRoot.SetActive(false);
            if (fuanBattleRoot != null) fuanBattleRoot.SetActive(false);
            if (bedFlightRoot != null) bedFlightRoot.SetActive(false);
            if (memoryRecallRoot != null) memoryRecallRoot.SetActive(false);
            if (sadnessBattleRoot != null) sadnessBattleRoot.SetActive(false);
            if (sadnessMapEnvironment != null) sadnessMapEnvironment.HideMaps(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool quickDebugStarting;
#endif

        /// <summary>
        /// エディタのF1デバッグメニューとは別に、常時（WebGLビルドでも）動く軽量な確認用UI。
        /// 画面右上の小さな「FuanBattle」ボタンを押すと、物語進行を変えずに不安戦だけを直接起動する。
        /// UNITY_EDITORまたはDEVELOPMENT_BUILDでのみコンパイルされ、製品ビルドには含まれない。
        /// </summary>
        private void OnGUI()
        {
#if UNITY_EDITOR
            OnGUIEditorDebugMenu();
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showWebGLQuickDebugButton)
            {
                return;
            }

            const float buttonWidth = 108f;
            const float buttonHeight = 26f;
            var rect = new Rect(Screen.width - buttonWidth - 12f, 12f, buttonWidth, buttonHeight);

            GUI.enabled = !quickDebugStarting && !battleInProgress;
            if (GUI.Button(rect, quickDebugStarting ? "起動中…" : "FuanBattle"))
            {
                quickDebugStarting = true;
                _ = StartQuickFuanBattle();
            }
            GUI.enabled = true;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private async Task StartQuickFuanBattle()
        {
            try
            {
                // タイトル画面が出ている間は、通常の「はじめる」と同様にタイトルUIを隠してから
                // ミニゲームへ直行する（隠さないと、下で戦闘が始まってもタイトルの全画面UIが
                // 覆ったままで見えなくなる）。
                TitleScreenController.HideImmediately();
                await StartMinigame("FuanBattle");
            }
            finally
            {
                quickDebugStarting = false;
            }
        }
#endif

#if UNITY_EDITOR
        [Header("開発用（エディタ専用・シーンセレクト）")]
        [Tooltip("このキーで、デバッグ用のシーンセレクトメニューの表示/非表示を切り替える（ビルドには含まれない）")]
        public KeyCode debugMenuKey = KeyCode.F1;

        [Tooltip("メニューから直接ジャンプできるYarnのノード名（各ステージの開始ノードなど）")]
        public string[] debugStoryNodes = new string[]
        {
            "Prologue", "Anger", "Anxiety", "Escape", "Sadness", "Ending",
            "TrueEnd_Start", "TrueEnd_Consult", "TrueEnd_AfterConsult", "TrueEnd_End"
        };

        [Tooltip("メニューから直接単体起動できるミニゲーム名")]
        public string[] debugMinigames = new string[] { "IkariBattle", "FuanBattle", "BedFlight", "MemoryRecall", "SadnessBattle" };

        [Tooltip("「ミニゲーム単体起動」ボタンで戦闘が終わった後、自動でジャンプする次の現実パートのYarnノード名")]
        public string[] debugMinigameNextNodes = new string[] { "Anxiety", "Escape", "Sadness" };

        private DialogueRunner debugDialogueRunner;
        private bool debugMenuVisible = false;
        private Vector2 debugMenuScroll;

        private void Update()
        {
            if (Input.GetKeyDown(debugMenuKey))
            {
                debugMenuVisible = !debugMenuVisible;
            }
        }

        private void OnGUIEditorDebugMenu()
        {
            if (!debugMenuVisible) return;

            float menuHeight = Mathf.Min(720f, Screen.height - 40f);
            GUILayout.BeginArea(new Rect(20, 20, 340, menuHeight), GUI.skin.window);
            GUILayout.Label("デバッグ：シーンセレクト（F1で閉じる）");
            debugMenuScroll = GUILayout.BeginScrollView(debugMenuScroll);

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
            GUILayout.Label("エンディング分岐テスト");
            if (GUILayout.Button("全員隔離 → TRUE END判定"))
            {
                SetDebugEmotionRoute(true);
                JumpToStoryNode("Ending");
            }
            if (GUILayout.Button("一人消去 → 通常END判定"))
            {
                SetDebugEmotionRoute(false);
                JumpToStoryNode("Ending");
            }

            GUILayout.Space(8);
            GUILayout.Label("ミニゲーム単体起動");
            for (int i = 0; i < debugMinigames.Length; i++)
            {
                string minigame = debugMinigames[i];
                string nextNode = (i < debugMinigameNextNodes.Length) ? debugMinigameNextNodes[i] : null;
                if (GUILayout.Button(minigame))
                {
                    debugMenuVisible = false;
                    _ = RunDebugMinigameThenContinue(minigame, nextNode);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void SetDebugEmotionRoute(bool allIsolated)
        {
            EmotionRouteState.Reset();
            EmotionRouteState.Set(EmotionKind.Anger, allIsolated ? EmotionOutcome.Isolated : EmotionOutcome.Eliminated);
            EmotionRouteState.Set(EmotionKind.Anxiety, EmotionOutcome.Isolated);
            EmotionRouteState.Set(EmotionKind.Sadness, EmotionOutcome.Isolated);
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

            if (nodeName != null && nodeName.StartsWith("TrueEnd_") && nodeName != "TrueEnd_Start")
            {
                // TrueEnd_Startを経由しないデバッグジャンプだと、相談済みフラグが前回プレイの値のまま
                // 残ってしまい、選択肢が欠けて見える（本来はTrueEnd_Startの<<set...=false>>でリセットされる）。
                debugDialogueRunner.VariableStorage.SetValue("$consultedAnger", false);
                debugDialogueRunner.VariableStorage.SetValue("$consultedAnxiety", false);
                debugDialogueRunner.VariableStorage.SetValue("$consultedSadness", false);
            }

            AbortActiveBattle();

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
            if (memoryRecallRoot != null)
            {
                memoryRecallRoot.SetActive(false);
            }
            if (sadnessBattleRoot != null)
            {
                sadnessBattleRoot.SetActive(false);
            }
            if (sadnessMapEnvironment != null)
            {
                sadnessMapEnvironment.HideMaps();
            }
            SetDialogueVisible(true);

            _ = debugDialogueRunner.StartDialogue(nodeName);
        }

        /// <summary>「ミニゲーム単体起動」ボタン用：戦闘を単体起動し、終了後に指定ノードがあれば自動でジャンプする。</summary>
        private async Task RunDebugMinigameThenContinue(string minigameName, string nextNode)
        {
            await StartMinigame(minigameName);

            if (!string.IsNullOrEmpty(nextNode))
            {
                JumpToStoryNode(nextNode);
            }
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
                case "MemoryRecall":
                    await instance.RunMemoryRecall();
                    break;
                case "SadnessBattle":
                    await instance.RunSadnessBattle();
                    break;
                default:
                    Debug.LogWarning($"[MinigameLauncher] 未対応のミニゲーム名です: {minigameName}");
                    break;
            }
        }

        /// <summary>
        /// TRUE ENDの相談パートのように&lt;&lt;start_minigame&gt;&gt;を経由しない場面から、
        /// 怒り戦・不安戦と同じ「時計＋ノイズ」入室演出（就寝→精神世界）だけを単体で流用する。
        /// </summary>
        [YarnCommand("clock_glitch_intro")]
        public static IEnumerator PlayClockGlitchIntroCommand()
        {
            if (instance == null || instance.clockGlitchIntro == null) yield break;
            yield return instance.clockGlitchIntro.Play();
        }

        /// <summary>同上。「目覚めの時計」退室演出（精神世界→朝）だけを単体で流用する。</summary>
        [YarnCommand("wake_glitch_intro")]
        public static IEnumerator PlayWakeGlitchIntroCommand()
        {
            if (instance == null || instance.wakeGlitchIntro == null) yield break;
            yield return instance.wakeGlitchIntro.Play();
        }

        /// <summary>
        /// 通常END（全感情を隔離しなかった場合のEndingノード末尾）から呼ばれる。
        /// 一連の物語状態をリセットし、タイトル画面へ戻す。
        /// </summary>
        [YarnCommand("return_to_title")]
        public static IEnumerator ReturnToTitleCommand()
        {
            if (instance == null) yield break;
            yield return instance.ReturnToTitleRoutine();
        }

        private IEnumerator ReturnToTitleRoutine()
        {
            AbortActiveBattle();
            EmotionRouteState.Reset();

            DialogueRunner runner = FindFirstObjectByType<DialogueRunner>();
            if (runner != null)
            {
                runner.VariableStorage.SetValue("$consultedAnger", false);
                runner.VariableStorage.SetValue("$consultedAnxiety", false);
                runner.VariableStorage.SetValue("$consultedSadness", false);
            }

            if (battleRoot != null) battleRoot.SetActive(false);
            if (fuanBattleRoot != null) fuanBattleRoot.SetActive(false);
            if (bedFlightRoot != null) bedFlightRoot.SetActive(false);
            if (memoryRecallRoot != null) memoryRecallRoot.SetActive(false);
            if (sadnessBattleRoot != null) sadnessBattleRoot.SetActive(false);
            if (sadnessMapEnvironment != null) sadnessMapEnvironment.HideMaps();

            DialogueVisuals.SetPortrait("None");
            DialogueVisuals.SetBackground("None");
            DialogueVisuals.SetBgm("None");

            SetDialogueVisible(false);

            TitleScreenController.ShowTitle();
            yield break;
        }

        private Task RunAngerBattle()
        {
            return RunBattle(battleRoot, angerBattleController.StartBattle, PlayClockGlitchIntroIfPresent, PlayWakeThenGoodMorningOutro, PlayAngerLevelUpBeat);
        }

        private Task RunFuanBattle()
        {
            return RunBattle(fuanBattleRoot, fuanBattleController.StartBattle, PlayClockGlitchIntroIfPresent, PlayWakeThenGoodMorningOutro, PlayFuanLevelUpBeat);
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

        /// <summary>
        /// 記憶回想（子供のころの記憶を想起する2Dマップ探索）。`Sadness.yarn`から起動される。
        /// 怒り戦・不安戦のような「精神世界パートへの入退室」演出（ClockGlitchIntro/GoodMorningOutro）は使わない。
        /// </summary>
        private Task RunMemoryRecall()
        {
            return RunBattle(memoryRecallRoot, memoryRecallController.StartExploration, null, null);
        }

        /// <summary>
        /// 記憶回想 悲しみコンタックバトル。怒り戦・不安戦と同じ弾撃ちの仕組みを流用しつつ、
        /// プレイヤー＝コンタックを操作してお母さん・友達を順番に撃破する（悲しみと決別するため）。
        /// `Sadness.yarn`から起動される。入退室演出（ClockGlitchIntro/GoodMorningOutro）は使わない。
        /// </summary>
        private Task RunSadnessBattle()
        {
            // 「これで異常なし」までSadnessBattleController内で完結するため、
            // 旧レベルアップ表示（悲しみが少し和らいだ）は挟まない。
            return RunBattle(sadnessBattleRoot, sadnessBattleController.StartBattle, null, null);
        }

        private async Task RunBattle(GameObject root, Action<Action> startBattle, Func<Task> preStartIntro, Func<Action, Task> postBattleOutro, Func<Task> preOutroBeat = null)
        {
            if (battleInProgress)
            {
                Debug.LogWarning("[MinigameLauncher] ミニゲームの二重起動を無視し、進行中の終了を待ちます。", this);
                if (activeBattleTask != null)
                {
                    await activeBattleTask;
                }
                return;
            }
            if (root == null || startBattle == null)
            {
                Debug.LogError("[MinigameLauncher] 戦闘ルートまたは開始処理が設定されていません。", this);
                return;
            }

            int runId = ++activeBattleRunId;
            battleInProgress = true;
            battleFinishing = false;
            activeBattleRoot = root;
            battleFinishedSource = new TaskCompletionSource<bool>();
            activeBattleTask = battleFinishedSource.Task;

            // デバッグジャンプや中断の直後でも、別ミニゲームの背景が残らないようにする。
            DeactivateAllBattleRootsExcept(root);
            if (sadnessMapEnvironment != null)
            {
                sadnessMapEnvironment.HideMaps();
            }
            root.SetActive(false);

            try
            {
                // 時計＋ノイズ導入演出の間は、直前に表示されていた背景（会話の背景）を
                // そのまま見せ続けたいので、バトルの背景・ルートはここではまだ出さない。
                if (preStartIntro != null)
                {
                    await preStartIntro();
                }

                if (runId != activeBattleRunId)
                {
                    return;
                }

                SetDialogueVisible(false);
                root.SetActive(true);

                startBattle(() => OnEnemyDefeated(runId, root, postBattleOutro, preOutroBeat));

                await activeBattleTask;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                root.SetActive(false);
                SetDialogueVisible(true);
            }
            finally
            {
                if (runId == activeBattleRunId)
                {
                    battleFinishedSource?.TrySetResult(true);
                    battleFinishedSource = null;
                    activeBattleTask = null;
                    activeBattleRoot = null;
                    battleInProgress = false;
                    battleFinishing = false;
                }
            }
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
        /// 敵撃破直後に呼ばれる。レベルアップ演出（preOutroBeat）→「目覚めの時計」＋「Good Morning」
        /// 演出（postBattleOutro）の順に挟んでから、onReveal（バトルルート非表示・会話UI復帰）を
        /// 経て戦闘終了を確定させる。いずれも未設定なら即座にスキップする。
        /// </summary>
        private async void OnEnemyDefeated(int runId, GameObject root, Func<Action, Task> postBattleOutro, Func<Task> preOutroBeat)
        {
            if (runId != activeBattleRunId || root != activeBattleRoot || battleFinishing)
            {
                return;
            }
            battleFinishing = true;
            bool revealed = false;

            void Reveal()
            {
                if (revealed || runId != activeBattleRunId)
                {
                    return;
                }
                revealed = true;
                root.SetActive(false);
                SetDialogueVisible(true);
            }

            try
            {
                if (postDefeatPauseSeconds > 0f)
                {
                    await Task.Delay(TimeSpan.FromSeconds(postDefeatPauseSeconds));
                }
                if (runId != activeBattleRunId) return;

                if (preOutroBeat != null)
                {
                    await preOutroBeat();
                }
                if (runId != activeBattleRunId) return;

                if (postBattleOutro != null)
                {
                    await postBattleOutro(Reveal);
                }
                else
                {
                    Reveal();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                Reveal();
            }
            finally
            {
                if (runId == activeBattleRunId)
                {
                    Reveal();
                    battleFinishedSource?.TrySetResult(true);
                }
            }
        }

        private void DeactivateAllBattleRootsExcept(GameObject keepActive)
        {
            GameObject[] roots = { battleRoot, fuanBattleRoot, bedFlightRoot, memoryRecallRoot, sadnessBattleRoot };
            foreach (GameObject candidate in roots)
            {
                if (candidate != null && candidate != keepActive)
                {
                    candidate.SetActive(false);
                }
            }
        }

        private void AbortActiveBattle()
        {
            if (!battleInProgress)
            {
                return;
            }

            activeBattleRunId++;
            if (activeBattleRoot != null)
            {
                activeBattleRoot.SetActive(false);
            }
            if (sadnessMapEnvironment != null)
            {
                sadnessMapEnvironment.HideMaps();
            }
            battleFinishedSource?.TrySetResult(true);
            battleFinishedSource = null;
            activeBattleTask = null;
            activeBattleRoot = null;
            battleInProgress = false;
            battleFinishing = false;
        }

        /// <summary>撃破直後のレベルアップ演出（怒り戦）。レベルアップ音＋一言を表示してスペースキーで読み進める。</summary>
        private async Task PlayAngerLevelUpBeat()
        {
            if (goodMorningOutro != null)
            {
                goodMorningOutro.PlaySound(levelUpClip);
            }
            if (angerBattleController == null)
            {
                return;
            }
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(RunLevelUpBeatCoroutine(angerBattleController.ShowLevelUpLineAndWait(), tcs));
            await tcs.Task;
        }

        /// <summary>撃破直後のレベルアップ演出（不安戦）。レベルアップ音＋一言を表示してスペースキーで読み進める。</summary>
        private async Task PlayFuanLevelUpBeat()
        {
            if (goodMorningOutro != null)
            {
                goodMorningOutro.PlaySound(levelUpClip);
            }
            if (fuanBattleController == null)
            {
                return;
            }
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(RunLevelUpBeatCoroutine(fuanBattleController.ShowLevelUpLineAndWait(), tcs));
            await tcs.Task;
        }

        private IEnumerator RunLevelUpBeatCoroutine(IEnumerator routine, TaskCompletionSource<bool> tcs)
        {
            yield return StartCoroutine(routine);
            tcs.TrySetResult(true);
        }

        /// <summary>
        /// 敵撃破後の「目覚めの時計」演出→「Good Morning」演出を1本のコルーチンとして繋げて再生する。
        /// 目覚めの時計演出が終わり次第すぐ非表示にし（通常のPlay()）、間を置かず同じフレームで
        /// Good Morning演出をフェードインなし・最初から不透明（白）で始める(skipFadeIn: true)ことで、
        /// 継ぎ目（隙間から背後の戦闘背景が見えてしまう瞬間）ができないようにする。
        /// 目覚めの時計演出が未設定ならGood Morning単体（通常のフェードインあり）にフォールバックする。
        /// </summary>
        private Task PlayWakeThenGoodMorningOutro(Action onReveal)
        {
            if (goodMorningOutro == null)
            {
                onReveal?.Invoke();
                return Task.CompletedTask;
            }

            var outroFinishedSource = new TaskCompletionSource<bool>();
            StartCoroutine(RunWakeThenGoodMorningOutroCoroutine(onReveal, outroFinishedSource));
            return outroFinishedSource.Task;
        }

        private IEnumerator RunWakeThenGoodMorningOutroCoroutine(Action onReveal, TaskCompletionSource<bool> outroFinishedSource)
        {
            if (wakeGlitchIntro != null)
            {
                yield return StartCoroutine(wakeGlitchIntro.Play());
                yield return StartCoroutine(goodMorningOutro.Play(onReveal, skipFadeIn: true));
            }
            else
            {
                yield return StartCoroutine(goodMorningOutro.Play(onReveal));
            }

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

            // 背景・立ち絵はScreen Space - OverlayのCanvasのため、
            // 非表示にしないと戦闘中もバトルの見た目より手前に描画されてしまう。
            if (dialogueVisualsRoot != null)
            {
                dialogueVisualsRoot.SetActive(visible);
            }
        }
    }
}
