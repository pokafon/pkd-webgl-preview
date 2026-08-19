using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// One-click generator for a Dynamic SDF font asset from Italiana-Regular.ttf
/// (the "Good Morning" outro's typeface), and wires it onto the Label TMP_Text
/// under GoodMorningOutro in SampleScene.unity.
/// Run via Tools > Yarn Dialogue > Generate Italiana Font Asset,
/// or headless via -executeMethod ItalianaFontAssetGenerator.Generate.
/// </summary>
public static class ItalianaFontAssetGenerator
{
    private const string SourceFontPath = "Assets/Fonts/Italiana-Regular.ttf";
    private const string OutputFontAssetPath = "Assets/Fonts/Italiana SDF.asset";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TargetParentName = "GoodMorningOutro";
    private const string TargetObjectName = "Label";

    [MenuItem("Tools/Yarn Dialogue/Generate Italiana Font Asset")]
    public static void Generate()
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[ItalianaFontAssetGenerator] Source font not found at {SourceFontPath}.");
            return;
        }

        // "Good Morning"は固定の英字のみなので、事前ベイクが必要なStaticではなく、
        // 表示時に必要なグリフだけをその場でラスタライズするDynamicモードを使う
        // （Static + TryAddCharactersの組み合わせがこの環境でグリフを一切焼き込めなかったため）。
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,                       // sampling point size
            5,                        // atlas padding
            GlyphRenderMode.SDFAA,
            1024, 1024,
            AtlasPopulationMode.Dynamic);

        if (fontAsset == null)
        {
            Debug.LogError("[ItalianaFontAssetGenerator] TMP_FontAsset.CreateFontAsset failed.");
            return;
        }

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

        Debug.Log($"[ItalianaFontAssetGenerator] Created {OutputFontAssetPath} " +
                  "(Dynamic population, atlas 1024x1024; glyphs rasterize on first use).");

        ApplyToScene(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath));
    }

    private static void ApplyToScene(TMP_FontAsset fontAsset)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.gameObject.name != TargetObjectName) continue;
                if (tmp.transform.parent == null) continue;
                if (tmp.transform.parent.gameObject.name != TargetParentName) continue;

                Undo.RecordObject(tmp, "Assign Italiana Font Asset");
                tmp.font = fontAsset;
                EditorUtility.SetDirty(tmp);
                count++;
            }
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[ItalianaFontAssetGenerator] Applied font to {count} \"{TargetObjectName}\" component(s) " +
                  $"under \"{TargetParentName}\" in scene. Saved: {count > 0}");
    }
}
