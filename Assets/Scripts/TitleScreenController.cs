using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// 起動直後に表示するタイトル画面／メインメニュー。
/// 「はじめる」でPrologueダイアログを開始し、「終了」でアプリを終了する。
/// 通常END到達時は MinigameLauncher.ReturnToTitleCommand から <see cref="ShowTitle"/> を
/// 呼び出してもらい、再度この画面へ戻る。
///
/// MinigameLauncher.StartMinigameと同じ理由（Yarn Spinner関連コードとの整合、および
/// シーン内に唯一のインスタンスを自前で保持する既存の設計）に合わせて、
/// 他スクリプトから呼ばれる入口はstaticメソッドにしてある。
/// </summary>
public class TitleScreenController : MonoBehaviour
{
    [Tooltip("タイトル画面全体の表示・入力を切り替えるCanvasGroup")]
    public CanvasGroup titleCanvasGroup;

    [Tooltip("「はじめる」ボタン")]
    public Button startButton;

    [Tooltip("「終了」ボタン")]
    public Button quitButton;

    private static TitleScreenController instance;
    private DialogueRunner dialogueRunner;

    private void Awake()
    {
        instance = this;
        dialogueRunner = FindFirstObjectByType<DialogueRunner>();

        if (startButton != null) startButton.onClick.AddListener(HandleStart);
        if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>タイトル画面を表示する。通常ENDからの「タイトルへ戻る」で使う。</summary>
    public static void ShowTitle()
    {
        if (instance == null) return;

        instance.gameObject.SetActive(true);
        if (instance.titleCanvasGroup != null)
        {
            instance.titleCanvasGroup.alpha = 1f;
            instance.titleCanvasGroup.interactable = true;
            instance.titleCanvasGroup.blocksRaycasts = true;
        }
    }

    private void HandleStart()
    {
        if (titleCanvasGroup != null)
        {
            titleCanvasGroup.alpha = 0f;
            titleCanvasGroup.interactable = false;
            titleCanvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);

        DialogueVisuals.SetBlackout("off");

        if (dialogueRunner == null)
        {
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        }
        if (dialogueRunner != null)
        {
            dialogueRunner.StartDialogue("Prologue");
        }
        else
        {
            Debug.LogWarning("[TitleScreenController] シーン内にDialogueRunnerが見つからないため、ダイアログを開始できません。");
        }
    }

    private void HandleQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
