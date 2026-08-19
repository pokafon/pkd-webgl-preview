using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// One-click generator for a Static SDF Japanese font asset (Noto Sans JP),
/// replacing the Dynamic-OS "YuGothicUI SDF" asset that caused glyph
/// corruption (wrong characters shown for kanji) in the dialogue UI.
/// Run via Tools > Yarn Dialogue > Generate Japanese Font Asset.
/// </summary>
public static class JapaneseFontAssetGenerator
{
    private const string SourceFontPath = "Assets/Fonts/NotoSansJP-VF.ttf";
    private const string OutputFontAssetPath = "Assets/Fonts/NotoSansJP SDF.asset";
    private const string OldFontAssetPath = "Assets/Fonts/YuGothicUI SDF.asset";

    // Kanji actually used in Prologue.yarn / SampleDialogue.yarn, extracted 2026-08-18.
    private const string UsedKanji =
        "一上下中乗了今代仮会何作僕入出分助動化叫司境変夜奥字導少屋席帰常床張怒所打持数料日明時景暗更月机校正死気波浮源演点焼照現環画疲目直真眠示社私立終絵続置聞背胸腰自薄薬表見資起近週遅違選部重開間電面鞄音響頑頭飲高黒";

    [MenuItem("Tools/Yarn Dialogue/Generate Japanese Font Asset")]
    public static void Generate()
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[JapaneseFontAssetGenerator] Source font not found at {SourceFontPath}. " +
                            "Make sure NotoSansJP-VF.ttf was imported.");
            return;
        }

        string characterSet = BuildCharacterSet();

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,                       // sampling point size
            5,                        // atlas padding
            GlyphRenderMode.SDFAA,
            2048, 2048,
            AtlasPopulationMode.Static);

        if (fontAsset == null)
        {
            Debug.LogError("[JapaneseFontAssetGenerator] TMP_FontAsset.CreateFontAsset failed.");
            return;
        }

        fontAsset.TryAddCharacters(characterSet, out string missingChars);
        if (!string.IsNullOrEmpty(missingChars))
        {
            Debug.LogWarning($"[JapaneseFontAssetGenerator] {missingChars.Length} character(s) could not be " +
                              $"baked (not present in source font): {missingChars}");
        }

        // Delete any previous output so re-running the tool is idempotent.
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath) != null)
        {
            AssetDatabase.DeleteAsset(OutputFontAssetPath);
        }

        AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        foreach (var tex in fontAsset.atlasTextures)
        {
            AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(OutputFontAssetPath);

        Debug.Log($"[JapaneseFontAssetGenerator] Created {OutputFontAssetPath} " +
                  $"({characterSet.Length} characters baked, atlas 2048x2048, Static population).");

        int replaced = ReplaceFontReferencesInOpenScenes(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath));
        Debug.Log($"[JapaneseFontAssetGenerator] Replaced font asset on {replaced} TMP_Text component(s) " +
                  "in the currently open scene(s). Save the scene to keep the change.");

        Selection.activeObject = fontAsset;
        EditorGUIUtility.PingObject(fontAsset);
    }

    private static string BuildCharacterSet()
    {
        var sb = new StringBuilder();

        // ASCII printable range (covers half-width alphanumerics/punctuation).
        for (int c = 0x0020; c <= 0x007E; c++) sb.Append((char)c);

        // Full hiragana block.
        for (int c = 0x3041; c <= 0x3096; c++) sb.Append((char)c);

        // Full katakana block (incl. prolonged sound mark).
        for (int c = 0x30A1; c <= 0x30FA; c++) sb.Append((char)c);
        sb.Append('ー'); // ー

        // Japanese punctuation / symbols commonly used in dialogue.
        sb.Append("　、。「」『』・ー～！？…‥（）｛｝〜");

        // Full-width forms sometimes used in localized UI text.
        for (int c = 0xFF01; c <= 0xFF5E; c++) sb.Append((char)c);

        // Kanji actually present in the current dialogue scripts.
        sb.Append(UsedKanji);

        return new string(sb.ToString().Distinct().ToArray());
    }

    private static int ReplaceFontReferencesInOpenScenes(TMP_FontAsset newFont)
    {
        var oldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OldFontAssetPath);
        int count = 0;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (oldFont != null && tmp.font != oldFont) continue;
                    Undo.RecordObject(tmp, "Assign Japanese Font Asset");
                    tmp.font = newFont;
                    EditorUtility.SetDirty(tmp);
                    count++;
                }
            }

            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
        }

        return count;
    }
}
