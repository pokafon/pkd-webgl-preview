using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// 現実パートの会話中に、背景画像・話者の立ち絵・BGM・SE・暗転を制御するための汎用の仕組み。
/// Yarnの台本から &lt;&lt;background "名前"&gt;&gt; / &lt;&lt;portrait "名前"&gt;&gt; /
/// &lt;&lt;portrait_position "left|center|right"&gt;&gt; / &lt;&lt;bgm "名前"&gt;&gt; /
/// &lt;&lt;se "名前"&gt;&gt; / &lt;&lt;blackout "on|off"&gt;&gt; で切り替える。
/// 名前を空文字または"none"（大文字小文字を区別しない）にすると非表示・停止になる。
/// これらのコマンドが書かれていない限り、直前の状態を維持する
/// （シナリオMarkdown→Yarn変換の「未指定なら現状維持」ルールは、この仕組みが
/// 単に明示的なコマンドでしか状態を変えないことでそのまま成立する）。
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

    [System.Serializable]
    public struct NamedClip
    {
        public string name;
        public AudioClip clip;
    }

    [Tooltip("背景画像を表示するImage（画面全体を覆うもの）")]
    public Image backgroundImage;

    [Tooltip("立ち絵を表示するImage")]
    public Image portraitImage;

    [Tooltip("<<background \"名前\">>で参照できる背景画像の一覧")]
    public NamedSprite[] backgrounds;

    [Tooltip("<<portrait \"名前\">>で参照できる立ち絵の一覧")]
    public NamedSprite[] portraits;

    [Tooltip("立ち絵の左右の余白（ピクセル、参照解像度基準）。<<portrait_position>>の位置計算に使う")]
    public float portraitPositionMargin = 40f;

    [Tooltip("<<bgm \"名前\">>を再生するAudioSource（ループ再生）")]
    public AudioSource bgmSource;

    [Tooltip("<<se \"名前\">>を再生するAudioSource（単発再生）")]
    public AudioSource seSource;

    [Tooltip("<<bgm \"名前\">>で参照できるBGMの一覧")]
    public NamedClip[] bgmClips;

    [Tooltip("<<se \"名前\">>で参照できるSEの一覧")]
    public NamedClip[] seClips;

    [Tooltip("<<blackout \"on|off\">>で表示・非表示を切り替える画面全体を覆う黒画像")]
    public Image blackoutImage;

    private static DialogueVisuals instance;
    private Dictionary<string, Sprite> backgroundLookup;
    private Dictionary<string, Sprite> portraitLookup;
    private Dictionary<string, AudioClip> bgmLookup;
    private Dictionary<string, AudioClip> seLookup;

    private void Awake()
    {
        instance = this;
        backgroundLookup = BuildLookup(backgrounds, e => e.name, e => e.sprite);
        portraitLookup = BuildLookup(portraits, e => e.name, e => e.sprite);
        bgmLookup = BuildLookup(bgmClips, e => e.name, e => e.clip);
        seLookup = BuildLookup(seClips, e => e.name, e => e.clip);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private static Dictionary<string, TValue> BuildLookup<TEntry, TValue>(TEntry[] entries, Func<TEntry, string> getName, Func<TEntry, TValue> getValue)
    {
        var lookup = new Dictionary<string, TValue>();
        if (entries == null) return lookup;

        foreach (var entry in entries)
        {
            string name = getName(entry);
            if (!string.IsNullOrEmpty(name))
            {
                lookup[name] = getValue(entry);
            }
        }
        return lookup;
    }

    private static bool IsNone(string name)
    {
        return string.IsNullOrEmpty(name) || string.Equals(name, "none", StringComparison.OrdinalIgnoreCase);
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

    [YarnCommand("portrait_position")]
    public static void SetPortraitPosition(string position)
    {
        if (instance == null || instance.portraitImage == null) return;

        var rectTransform = instance.portraitImage.rectTransform;
        var parentRect = rectTransform.parent as RectTransform;
        float parentWidth = parentRect != null ? parentRect.rect.width : 1920f;
        float margin = instance.portraitPositionMargin;
        float width = rectTransform.sizeDelta.x;

        float x;
        switch (position)
        {
            case "left":
                x = margin;
                break;
            case "right":
                x = parentWidth - width - margin;
                break;
            case "center":
                x = (parentWidth - width) / 2f;
                break;
            default:
                Debug.LogWarning($"[DialogueVisuals] 立ち絵の位置が不正です: {position}（left/center/rightのいずれかを指定してください）");
                return;
        }

        rectTransform.anchoredPosition = new Vector2(x, rectTransform.anchoredPosition.y);
    }

    [YarnCommand("bgm")]
    public static void SetBgm(string name)
    {
        if (instance == null || instance.bgmSource == null) return;

        if (IsNone(name))
        {
            instance.bgmSource.Stop();
            return;
        }

        if (instance.bgmLookup.TryGetValue(name, out AudioClip clip))
        {
            if (instance.bgmSource.clip == clip && instance.bgmSource.isPlaying) return;
            instance.bgmSource.clip = clip;
            instance.bgmSource.loop = true;
            instance.bgmSource.Play();
        }
        else
        {
            Debug.LogWarning($"[DialogueVisuals] BGMが見つかりません: {name}");
        }
    }

    [YarnCommand("se")]
    public static void PlaySe(string name)
    {
        if (instance == null || instance.seSource == null) return;
        if (IsNone(name)) return;

        if (instance.seLookup.TryGetValue(name, out AudioClip clip))
        {
            instance.seSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"[DialogueVisuals] SEが見つかりません: {name}");
        }
    }

    [YarnCommand("blackout")]
    public static void SetBlackout(string state)
    {
        if (instance == null || instance.blackoutImage == null) return;

        if (string.Equals(state, "on", StringComparison.OrdinalIgnoreCase))
        {
            instance.blackoutImage.enabled = true;
        }
        else if (string.Equals(state, "off", StringComparison.OrdinalIgnoreCase))
        {
            instance.blackoutImage.enabled = false;
        }
        else
        {
            Debug.LogWarning($"[DialogueVisuals] 暗転の指定が不正です: {state}（on/offのいずれかを指定してください）");
        }
    }

    private static void SetImage(Image image, Dictionary<string, Sprite> lookup, string name, string label)
    {
        if (IsNone(name))
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
