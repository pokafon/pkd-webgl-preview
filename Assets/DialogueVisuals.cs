using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// 現実パートの会話中に、背景画像・話者の立ち絵を表示するための汎用の仕組み。
/// Yarnの台本から &lt;&lt;background "名前"&gt;&gt; / &lt;&lt;portrait "名前"&gt;&gt; で切り替える。
/// 名前を空文字または"None"にすると非表示になる。
///
/// MinigameLauncher.StartMinigameと同じ理由（Yarn Spinnerがインスタンスメソッドの
/// 第1引数をGameObject名として解釈してしまう問題を避けるため）で、
/// コマンドはstaticにし、シーン内のインスタンスを自前で保持する。
/// </summary>
public class DialogueVisuals : MonoBehaviour
{
    [System.Serializable]
    public struct NamedSprite
    {
        public string name;
        public Sprite sprite;
    }

    [Tooltip("背景画像を表示するImage（画面全体を覆うもの）")]
    public Image backgroundImage;

    [Tooltip("立ち絵を表示するImage")]
    public Image portraitImage;

    [Tooltip("<<background \"名前\">>で参照できる背景画像の一覧")]
    public NamedSprite[] backgrounds;

    [Tooltip("<<portrait \"名前\">>で参照できる立ち絵の一覧")]
    public NamedSprite[] portraits;

    private static DialogueVisuals instance;
    private Dictionary<string, Sprite> backgroundLookup;
    private Dictionary<string, Sprite> portraitLookup;

    private void Awake()
    {
        instance = this;
        backgroundLookup = BuildLookup(backgrounds);
        portraitLookup = BuildLookup(portraits);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private static Dictionary<string, Sprite> BuildLookup(NamedSprite[] entries)
    {
        var lookup = new Dictionary<string, Sprite>();
        if (entries == null) return lookup;

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.name))
            {
                lookup[entry.name] = entry.sprite;
            }
        }
        return lookup;
    }

    [YarnCommand("background")]
    public static void SetBackground(string name)
    {
        if (instance == null || instance.backgroundImage == null) return;
        SetImage(instance.backgroundImage, instance.backgroundLookup, name, "背景");
    }

    [YarnCommand("portrait")]
    public static void SetPortrait(string name)
    {
        if (instance == null || instance.portraitImage == null) return;
        SetImage(instance.portraitImage, instance.portraitLookup, name, "立ち絵");
    }

    private static void SetImage(Image image, Dictionary<string, Sprite> lookup, string name, string label)
    {
        if (string.IsNullOrEmpty(name) || name == "None")
        {
            image.enabled = false;
            return;
        }

        if (lookup.TryGetValue(name, out Sprite sprite))
        {
            image.sprite = sprite;
            image.enabled = true;
        }
        else
        {
            Debug.LogWarning($"[DialogueVisuals] {label}が見つかりません: {name}");
        }
    }
}
