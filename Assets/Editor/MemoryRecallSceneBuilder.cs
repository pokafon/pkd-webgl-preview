using System;
using System.Collections.Generic;
using System.IO;
using MemoryRecall;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryRecall.EditorTools
{
    /// <summary>
    /// 記憶回想（子供のころの記憶を想起する2Dマップ探索）に必要なシーン階層・
    /// プレハブ・参照配線をエディタスクリプトから一括構築するためのビルダー。
    /// 背景はユーザー作成のドット絵一枚絵（Assets/Sprites/MAP.png）を読み込んで使用する。
    /// お母さん・友達の見た目はユーザー作成のドット絵（mum.png/frendA.png）、プレイヤーの見た目は
    /// 既存の怒り戦用スプライトをそのまま再利用する。
    ///
    /// 実行方法：
    ///   Unityエディタのメニュー「Tools/MemoryRecall/Build Scene」から実行（推奨）。
    ///   バッチモードの場合は
    ///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt;
    ///     -executeMethod MemoryRecall.EditorTools.MemoryRecallSceneBuilder.Build
    ///
    /// 何度実行しても安全（MemoryRecallRootを一度破棄してから再構築する。
    /// 既存のMinigameLauncherは破棄せず、参照フィールドだけ追記する）。
    /// </summary>
    public static class MemoryRecallSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string PlayerSpritePathInAngerBattle = "Assets/AngerBattle/Sprites/PlayerSprite.png";
        private const string MapImagePath = "Assets/Sprites/MAP.png";
        // MAP.pngは32x32pxのドット絵一枚絵（ユーザー作成）。カメラのorthographic size=5（縦の表示範囲=10ユニット）に
        // 合わせて画像の縦幅がちょうど10ユニットになるよう、Pixels Per Unitを32/10=3.2に設定する。
        private const float MapPixelsPerUnit = 3.2f;
        private const float MapWorldSize = 10f;

        private const string MotherImagePath = "Assets/Sprites/mum.png";
        private const string FriendImagePath = "Assets/Sprites/frendA.png";
        // キャラクターの絵はマップより高いPixels Per Unitにし、家（ワールド上で約2.25ユニット）に対して
        // 人物が半分弱くらいの背丈になるよう調整する。
        private const float CharacterPixelsPerUnit = 16f;

        [MenuItem("Tools/MemoryRecall/Build Scene")]
        public static void BuildFromMenu()
        {
            try
            {
                BuildInternal();
                EditorUtility.DisplayDialog("MemoryRecall", "MemoryRecallRootの構築が完了しました。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[MemoryRecallSceneBuilder] 構築に失敗しました: " + e.Message + "\n" + e.StackTrace);
                EditorUtility.DisplayDialog("MemoryRecall", "構築に失敗しました。コンソールを確認してください。\n" + e.Message, "OK");
            }
        }

        public static void Build()
        {
            try
            {
                BuildInternal();
                Debug.Log("MEMORYRECALL_BUILD_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("MEMORYRECALL_BUILD_RESULT: FAIL: " + e.Message + "\n" + e.StackTrace);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildInternal()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                throw new Exception($"フォントが見つかりません: {FontPath}");
            }

            var playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePathInAngerBattle);
            if (playerSprite == null)
            {
                throw new Exception($"プレイヤーの見た目に使う既存スプライトが見つかりません: {PlayerSpritePathInAngerBattle}");
            }

            var motherSprite = LoadCharacterSprite(MotherImagePath);
            var friendSprite = LoadCharacterSprite(FriendImagePath);

            var mapSprite = LoadMapSprite();

            DestroyIfExists(scene, "MemoryRecallRoot");

            // --- MemoryRecallRoot 階層 ---
            var root = new GameObject("MemoryRecallRoot");

            // --- 背景（ユーザー作成のドット絵一枚絵。家・川・橋・草地はすべてこの絵に描かれている） ---
            var mapGO = new GameObject("MapBackground", typeof(SpriteRenderer));
            mapGO.transform.SetParent(root.transform);
            mapGO.transform.position = new Vector3(0f, 0f, 1f);
            var mapRenderer = mapGO.GetComponent<SpriteRenderer>();
            mapRenderer.sprite = mapSprite;
            mapRenderer.sortingOrder = -10;

            // 以下の座標は、MAP.png（32x32px、ワールド上は-5〜5の10ユニット四方に対応）上で
            // 家・玄関・川・橋がある場所を目視でおおよそ合わせたもの。要実機調整。
            var motherGO = new GameObject("Mother", typeof(SpriteRenderer));
            motherGO.transform.SetParent(root.transform);
            motherGO.transform.position = new Vector3(-3.1f, 3f, 0f);
            var motherRenderer = motherGO.GetComponent<SpriteRenderer>();
            motherRenderer.sprite = motherSprite;
            motherRenderer.sortingOrder = 1;

            var friendAGO = new GameObject("FriendA", typeof(SpriteRenderer));
            friendAGO.transform.SetParent(root.transform);
            friendAGO.transform.position = new Vector3(-3.8f, 1.3f, 0f);
            friendAGO.GetComponent<SpriteRenderer>().sprite = friendSprite;

            var friendBGO = new GameObject("FriendB", typeof(SpriteRenderer));
            friendBGO.transform.SetParent(root.transform);
            friendBGO.transform.position = new Vector3(1.4f, -1.4f, 0f);
            friendBGO.GetComponent<SpriteRenderer>().sprite = friendSprite;

            var friendCGO = new GameObject("FriendC", typeof(SpriteRenderer));
            friendCGO.transform.SetParent(root.transform);
            friendCGO.transform.position = new Vector3(3.8f, -2.8f, 0f);
            friendCGO.GetComponent<SpriteRenderer>().sprite = friendSprite;

            var playerGO = new GameObject("Player", typeof(SpriteRenderer), typeof(AngerBattle.PlayerController));
            playerGO.transform.SetParent(root.transform);
            playerGO.transform.position = new Vector3(-3.1f, 2.3f, 0f);
            playerGO.GetComponent<SpriteRenderer>().sprite = playerSprite;
            playerGO.GetComponent<SpriteRenderer>().sortingOrder = 1;
            var playerController = playerGO.GetComponent<AngerBattle.PlayerController>();
            // マップ画像の範囲（-5〜5）から少し内側に収まるよう移動可能範囲を制限する
            playerController.minBounds = new Vector2(-4.7f, -4.7f);
            playerController.maxBounds = new Vector2(4.7f, 4.7f);

            // --- セリフ表示UI（現実パートと同じ見た目：背景パネル＋話者名＋本文） ---
            var mapUIGO = new GameObject("MapUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            mapUIGO.transform.SetParent(root.transform, false);
            var canvas = mapUIGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = mapUIGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var lineBackgroundGO = new GameObject("LineBackground", typeof(RectTransform), typeof(Image));
            lineBackgroundGO.transform.SetParent(mapUIGO.transform, false);
            var lineBackgroundRect = lineBackgroundGO.GetComponent<RectTransform>();
            lineBackgroundRect.anchorMin = new Vector2(0f, 0f);
            lineBackgroundRect.anchorMax = new Vector2(1f, 0f);
            lineBackgroundRect.pivot = new Vector2(0.5f, 0f);
            lineBackgroundRect.anchoredPosition = new Vector2(0f, 80f);
            lineBackgroundRect.sizeDelta = new Vector2(-400f, 140f);
            var lineBackgroundImage = lineBackgroundGO.GetComponent<Image>();
            lineBackgroundImage.color = new Color(0f, 0f, 0f, 0.8352941f);
            lineBackgroundImage.raycastTarget = false;
            lineBackgroundGO.SetActive(false);

            var lineTextGO = new GameObject("LineText", typeof(RectTransform), typeof(TextMeshProUGUI));
            lineTextGO.transform.SetParent(mapUIGO.transform, false);
            var lineTextRect = lineTextGO.GetComponent<RectTransform>();
            lineTextRect.anchorMin = new Vector2(0f, 0f);
            lineTextRect.anchorMax = new Vector2(1f, 0f);
            lineTextRect.pivot = new Vector2(0.5f, 0f);
            lineTextRect.anchoredPosition = new Vector2(0f, 80f);
            lineTextRect.sizeDelta = new Vector2(-400f, 96f);
            var lineText = lineTextGO.GetComponent<TextMeshProUGUI>();
            lineText.font = font;
            lineText.fontSize = 40;
            lineText.fontStyle = FontStyles.Bold;
            lineText.alignment = TextAlignmentOptions.TopLeft;
            lineText.color = Color.white;
            lineText.raycastTarget = false;
            lineTextGO.SetActive(false);

            var nameGO = new GameObject("CharacterNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(mapUIGO.transform, false);
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 176f);
            nameRect.sizeDelta = new Vector2(-400f, 44f);
            var nameText = nameGO.GetComponent<TextMeshProUGUI>();
            nameText.font = font;
            nameText.fontSize = 32;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.TopLeft;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
            nameGO.SetActive(false);

            // --- 進行管理コントローラー ---
            var controllerGO = new GameObject("MemoryRecallController", typeof(MemoryRecallController));
            controllerGO.transform.SetParent(root.transform);
            var controller = controllerGO.GetComponent<MemoryRecallController>();
            controller.player = playerController;
            controller.motherTransform = motherGO.transform;
            controller.friends[0].npcTransform = friendAGO.transform;
            controller.friends[1].npcTransform = friendBGO.transform;
            controller.friends[2].npcTransform = friendCGO.transform;
            controller.lineText = lineText;
            controller.characterNameText = nameText;
            controller.lineBackground = lineBackgroundGO;

            root.SetActive(false);

            // --- 既存のMinigameLauncherに参照を追記する（新規作成はしない） ---
            var launcherGO = GameObject.Find("MinigameLauncher");
            if (launcherGO == null)
            {
                throw new Exception("シーン内に既存の'MinigameLauncher'が見つかりません。先にAngerBattle/FuanBattleのセットアップが必要です。");
            }
            var launcher = launcherGO.GetComponent<AngerBattle.MinigameLauncher>();
            launcher.memoryRecallRoot = root;
            launcher.memoryRecallController = controller;

            bool alreadyListed = false;
            foreach (var name in launcher.debugMinigames)
            {
                if (name == "MemoryRecall") { alreadyListed = true; break; }
            }
            if (!alreadyListed)
            {
                var list = new List<string>(launcher.debugMinigames) { "MemoryRecall" };
                launcher.debugMinigames = list.ToArray();
            }

            ValidateController(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                throw new Exception("シーンの保存に失敗しました: " + ScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ValidateController(MemoryRecallController c)
        {
            var missing = new List<string>();
            if (c.player == null) missing.Add("player");
            if (c.motherTransform == null) missing.Add("motherTransform");
            if (c.lineText == null) missing.Add("lineText");
            if (c.lineBackground == null) missing.Add("lineBackground");
            foreach (var friend in c.friends)
            {
                if (friend.npcTransform == null) missing.Add("friends[].npcTransform");
            }
            if (missing.Count > 0)
            {
                throw new Exception("MemoryRecallControllerの参照が未設定です: " + string.Join(", ", missing));
            }
        }

        /// <summary>
        /// ユーザーが用意したドット絵背景（MAP.png）を読み込み、ドット絵らしくクッキリ表示されるよう
        /// インポート設定（Point Filter・Pixels Per Unit）を調整する。
        /// </summary>
        private static Sprite LoadMapSprite()
        {
            if (!File.Exists(MapImagePath))
            {
                throw new Exception($"マップ画像が見つかりません: {MapImagePath}");
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(MapImagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = MapPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(MapImagePath);
        }

        /// <summary>ユーザーが用意したキャラクターのドット絵（mum.png/frendA.pngなど）を、マップと同じ方針で読み込む。</summary>
        private static Sprite LoadCharacterSprite(string path)
        {
            if (!File.Exists(path))
            {
                throw new Exception($"キャラクター画像が見つかりません: {path}");
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = CharacterPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DestroyIfExists(UnityEngine.SceneManagement.Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = roots.Length - 1; i >= 0; i--)
            {
                if (roots[i].name == name)
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }
        }

    }
}
