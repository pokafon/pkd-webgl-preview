using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// OSにインストール済みの日本語フォントからTMP Font Asset(DynamicOS)を作成し、
/// プロジェクトのデフォルトフォントとシーン内のTMP_Textに適用する。
/// </summary>
static class JapaneseFontSetup
{
    // 優先度順の候補。見つかった最初のフォントを使用する。
    static readonly string[] CandidateFamilies =
    {
        "Yu Gothic UI",
        "Meiryo UI",
        "Meiryo",
        "Yu Gothic",
        "MS Gothic",
        "MS PGothic",
    };

    [MenuItem("PKD/Setup Japanese Font For TMP")]
    static void Setup()
    {
        if (TMP_Settings.instance == null)
        {
            Debug.LogError("TMP Essential Resources が見つかりません。Window > TextMeshPro > Import TMP Essential Resources を先に実行してください。");
            return;
        }

        TMP_FontAsset fontAsset = null;
        string usedFamily = null;

        foreach (var family in CandidateFamilies)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(family, "Regular");
            if (fontAsset != null)
            {
                usedFamily = family;
                break;
            }
        }

        if (fontAsset == null)
        {
            Debug.LogError("日本語対応フォントが見つかりませんでした。候補: " + string.Join(", ", CandidateFamilies));
            return;
        }

        const string dir = "Assets/Fonts";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Fonts");

        string assetName = usedFamily.Replace(" ", "");
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{assetName} SDF.asset");

        AssetDatabase.CreateAsset(fontAsset, assetPath);

        var texture = fontAsset.atlasTextures[0];
        texture.name = assetName + " Atlas";
        AssetDatabase.AddObjectToAsset(texture, fontAsset);

        var mat = fontAsset.material;
        mat.name = texture.name + " Material";
        AssetDatabase.AddObjectToAsset(mat, fontAsset);

        AssetDatabase.SaveAssets();

        TMP_Settings.defaultFontAsset = fontAsset;
        EditorUtility.SetDirty(TMP_Settings.instance);

        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in texts)
        {
            t.font = fontAsset;
            EditorUtility.SetDirty(t);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"日本語フォント '{usedFamily}' を適用しました ({assetPath})。TMP_Text {texts.Length}件に反映し、シーンを保存しました。");
    }
}
