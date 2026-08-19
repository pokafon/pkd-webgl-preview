using System.Threading.Tasks;
using UnityEngine;
using Yarn.Unity;

namespace AngerBattle
{
    /// <summary>
    /// Yarn Spinnerの &lt;&lt;start_minigame "IkariBattle"&gt;&gt; コマンドを受け取り、
    /// 怒り戦（AngerBattleController）を起動・終了するランチャー。
    ///
    /// ミニゲームは別Unity Sceneには分けず、
    /// 普段は非アクティブにしてある battleRoot を
    /// アクティブ化／非アクティブ化することで同じシーン内で切り替える。
    ///
    /// Anger.yarn 側では、Anger_TakeMed ノードの最後に
    ///     &lt;&lt;start_minigame "IkariBattle"&gt;&gt;
    /// を追加することで、この処理が呼ばれる想定。
    /// </summary>
    public class MinigameLauncher : MonoBehaviour
    {
        [Tooltip("怒り戦一式をまとめた親オブジェクト（普段は非アクティブにしておく）")]
        public GameObject battleRoot;

        [Tooltip("怒り戦の進行を管理するコントローラー（battleRootの中にあるもの）")]
        public AngerBattleController angerBattleController;

        [Tooltip("戦闘中は隠したいダイアログUIなどのルート（任意、未設定でも可）")]
        public GameObject dialogueUIRoot;

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
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

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
                default:
                    Debug.LogWarning($"[MinigameLauncher] 未対応のミニゲーム名です: {minigameName}");
                    break;
            }
        }

        private Task RunAngerBattle()
        {
            battleFinishedSource = new TaskCompletionSource<bool>();

            if (dialogueUIRoot != null)
            {
                dialogueUIRoot.SetActive(false);
            }
            battleRoot.SetActive(true);

            angerBattleController.StartBattle(OnBattleFinished);

            return battleFinishedSource.Task;
        }

        private void OnBattleFinished()
        {
            battleRoot.SetActive(false);

            if (dialogueUIRoot != null)
            {
                dialogueUIRoot.SetActive(true);
            }

            battleFinishedSource?.TrySetResult(true);
        }
    }
}
