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
    private const string SourceFontPath = "Assets/Fonts/NotoSansJP-Regular.ttf";
    private const string OutputFontAssetPath = "Assets/Fonts/NotoSansJP SDF.asset";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string YarnScriptsDir = "Assets/Yarn";
    private static readonly string[] OldFontAssetPaths =
    {
        "Assets/Fonts/YuGothicUI SDF.asset",
        "Assets/Fonts/YuGothicUI SDF 1.asset",
    };

    // 各ミニゲームのコントローラーには、Yarnと未接続のままC#のstringリテラルとして
    // セリフがハードコードされている箇所がある（例：BedFlightControllerの「遠くに行きたい」
    // 「現実に戻ろう」、FuanBattleControllerの「傷」等）。Yarnスクリプトだけを走査していると、
    // そこにしか出てこない漢字がフォントアセットに焼き込まれず、実行時に表示できなくなる
    // （文字が抜けるたびに気づいた分だけ追加、では対症療法になるため、ミニゲーム全体を対象にする）。
    private static readonly string[] MinigameScriptsDirs =
    {
        "Assets/AngerBattle",
        "Assets/FuanBattle",
        "Assets/BedFlight",
        "Assets/MemoryRecall",
        "Assets/SadnessBattle",
    };

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

        // Must be created as Dynamic to allow TryAddCharacters to populate the atlas;
        // TryAddCharacters refuses to add anything once atlasPopulationMode is Static.
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,                       // sampling point size
            5,                        // atlas padding
            GlyphRenderMode.SDFAA,
            2048, 2048,
            AtlasPopulationMode.Dynamic);

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

        // Lock the atlas as Static now that all needed glyphs are baked in, so no
        // runtime (OS-dependent) glyph generation is attempted on WebGL.
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        // Preserve the existing asset's GUID (if any) so scene/prefab references
        // to it aren't broken by delete+recreate.
        string existingGuid = AssetDatabase.AssetPathToGUID(OutputFontAssetPath);
        bool hadExistingAsset = !string.IsNullOrEmpty(existingGuid) &&
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath) != null;

        if (hadExistingAsset)
        {
            AssetDatabase.DeleteAsset(OutputFontAssetPath);
        }

        AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        foreach (var tex in fontAsset.atlasTextures)
        {
            if (tex != null)
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(OutputFontAssetPath);

        if (hadExistingAsset)
        {
            string metaPath = OutputFontAssetPath + ".meta";
            string metaText = File.ReadAllText(metaPath);
            metaText = System.Text.RegularExpressions.Regex.Replace(
                metaText, @"guid: [0-9a-f]{32}", "guid: " + existingGuid);
            File.WriteAllText(metaPath, metaText);
            AssetDatabase.ImportAsset(OutputFontAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[JapaneseFontAssetGenerator] Restored original GUID {existingGuid} for {OutputFontAssetPath}.");
        }

        Debug.Log($"[JapaneseFontAssetGenerator] Created {OutputFontAssetPath} " +
                  $"({characterSet.Length} characters baked, atlas 2048x2048, Static population).");

        int replaced = ReplaceFontReferencesInOpenScenes(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath));
        Debug.Log($"[JapaneseFontAssetGenerator] Replaced font asset on {replaced} TMP_Text component(s) " +
                  "in the currently open scene(s). Save the scene to keep the change.");

        Selection.activeObject = fontAsset;
        EditorGUIUtility.PingObject(fontAsset);
    }

    /// <summary>CI/リリース確認用。SampleSceneを開いて再生成・参照更新・保存まで行う。</summary>
    public static void GenerateForRelease()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Generate();
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("SampleSceneの保存に失敗しました。");

            AssetDatabase.SaveAssets();
            Debug.Log("JAPANESE_FONT_RELEASE_RESULT: PASS");
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("JAPANESE_FONT_RELEASE_RESULT: FAIL\n" + exception);
            EditorApplication.Exit(1);
        }
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

        // Kanji (and any other CJK ideographs) actually present in the current dialogue scripts.
        sb.Append(CollectKanjiFromFiles(YarnScriptsDir, "*.yarn"));
        foreach (var dir in MinigameScriptsDirs)
        {
            sb.Append(CollectKanjiFromFiles(dir, "*.cs"));
        }

        return new string(sb.ToString().Distinct().ToArray());
    }

    private static string CollectKanjiFromFiles(string dir, string searchPattern)
    {
        var sb = new StringBuilder();
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories)
            : new string[0];

        foreach (var path in files)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) // CJK Unified Ideographs
                    sb.Append(c);
            }
        }

        Debug.Log($"[JapaneseFontAssetGenerator] Scanned {files.Length} {searchPattern} file(s) under {dir}.");
        return new string(sb.ToString().Distinct().ToArray());
    }

    private static int ReplaceFontReferencesInOpenScenes(TMP_FontAsset newFont)
    {
        var oldFonts = OldFontAssetPaths
            .Select(path => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path))
            .Where(font => font != null)
            .ToArray();
        int count = 0;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    // 対象は「まだ旧フォント（YuGothicUI SDF）のまま」か「既にこのNotoSansJP SDFを
                    // 使っている」もの。後者も対象に含めるのは、このメソッドは毎回フォントアセットを
                    // 削除→再作成しており、アセット内部のMaterialサブアセットのfileIDが再生成のたびに
                    // 変わるため。既にnewFontを指しているTMP_Textでも、tmp.font = newFontを
                    // 再代入し直さないとm_sharedMaterialが古いfileIDのまま壊れて残ってしまう。
                    bool usesOldFont = oldFonts.Contains(tmp.font);
                    bool usesCurrentNewFont = tmp.font == newFont;
                    if (!usesOldFont && !usesCurrentNewFont) continue;

                    Undo.RecordObject(tmp, "Assign Japanese Font Asset");
                    tmp.font = newFont;
                    EditorUtility.SetDirty(tmp);
                    count++;
                }
            }

            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
        }

        TMP_Settings.defaultFontAsset = newFont;
        EditorUtility.SetDirty(TMP_Settings.instance);

        return count;
    }
}
