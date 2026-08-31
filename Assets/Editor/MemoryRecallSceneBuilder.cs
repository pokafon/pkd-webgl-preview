using System;
using System.Collections.Generic;
using System.IO;
using MemoryRecall;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MemoryRecall.EditorTools
{
    /// <summary>手作りの屋外／屋内Gridを使って記憶回想を構築する。</summary>
    public static class MemoryRecallSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string PlayerSpritePath = "Assets/Sprites/Player_Placeholder.png";
        private const string FallbackPlayerSpritePath = "Assets/AngerBattle/Sprites/PlayerSprite.png";
        private const string MotherSpritePath = "Assets/Sprites/mum.png";
        private const string FriendSpritePath = "Assets/Sprites/frendA.png";
        private const string EveningChimePath = "Assets/Audio/夕焼け小焼け 防災行政無線チャイム 17時.mp3";

        [MenuItem("Tools/MemoryRecall/Build Scene")]
        public static void BuildFromMenu()
        {
            try
            {
                BuildInternal();
                EditorUtility.DisplayDialog("MemoryRecall", "2マップ構成の記憶回想を構築しました。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("MemoryRecall", "構築に失敗しました。Consoleを確認してください。", "OK");
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
            catch (Exception exception)
            {
                Debug.LogError("MEMORYRECALL_BUILD_RESULT: FAIL: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildInternal()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SadnessMapEnvironment environment = SadnessMapEditorUtility.EnsureEnvironment(scene);

            TMP_FontAsset font = RequireAsset<TMP_FontAsset>(FontPath);
            Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath) ??
                                  RequireAsset<Sprite>(FallbackPlayerSpritePath);
            Sprite motherSprite = RequireCharacterSprite(MotherSpritePath);
            Sprite friendSprite = RequireCharacterSprite(FriendSpritePath);
            AudioClip eveningChime = RequireAsset<AudioClip>(EveningChimePath);

            DestroyRoot(scene, "MemoryRecallRoot");
            GameObject root = new GameObject("MemoryRecallRoot");
            SceneManager.MoveGameObjectToScene(root, scene);

            GameObject mother = BuildActor(root.transform, "Mother", motherSprite, environment.homeMotherSpot.position);
            GameObject[] friends = new GameObject[3];
            for (int index = 0; index < friends.Length; index++)
            {
                friends[index] = BuildActor(
                    root.transform,
                    $"Friend{(char)('A' + index)}",
                    friendSprite,
                    environment.outdoorFriendSpots[index].position);
            }

            GameObject playerObject = new GameObject("ChildPlayer", typeof(SpriteRenderer), typeof(AngerBattle.PlayerController));
            playerObject.transform.SetParent(root.transform);
            playerObject.transform.position = environment.homeStart.position;
            SpriteRenderer playerRenderer = playerObject.GetComponent<SpriteRenderer>();
            playerRenderer.sprite = playerSprite;
            playerRenderer.sortingOrder = 5;
            AngerBattle.PlayerController player = playerObject.GetComponent<AngerBattle.PlayerController>();
            player.speed = 5f;

            BuildDialogueUI(root.transform, font, out TMP_Text nameText, out TMP_Text lineText, out GameObject lineBackground);

            GameObject controllerObject = new GameObject(
                "MemoryRecallController",
                typeof(AudioSource),
                typeof(MemoryRecallController));
            controllerObject.transform.SetParent(root.transform);
            AudioSource audioSource = controllerObject.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            MemoryRecallController controller = controllerObject.GetComponent<MemoryRecallController>();
            controller.player = player;
            controller.mapEnvironment = environment;
            controller.motherTransform = mother.transform;
            controller.lineText = lineText;
            controller.characterNameText = nameText;
            controller.lineBackground = lineBackground;
            controller.eveningChimeSource = audioSource;
            controller.eveningChimeClip = eveningChime;
            controller.friends[0].npcTransform = friends[0].transform;
            controller.friends[1].npcTransform = friends[1].transform;
            controller.friends[2].npcTransform = friends[2].transform;

            root.SetActive(false);
            environment.outdoorGrid.SetActive(false);
            environment.homeGrid.SetActive(false);

            AngerBattle.MinigameLauncher launcher = FindLauncher();
            launcher.memoryRecallRoot = root;
            launcher.memoryRecallController = controller;
            launcher.sadnessMapEnvironment = environment;
            launcher.debugStoryNodes = AppendUnique(launcher.debugStoryNodes, "Sadness");
            launcher.debugMinigames = AppendUnique(launcher.debugMinigames, "MemoryRecall");

            Validate(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new Exception("シーンの保存に失敗しました: " + ScenePath);
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildActor(Transform parent, string name, Sprite sprite, Vector3 position)
        {
            GameObject actor = new GameObject(name, typeof(SpriteRenderer));
            actor.transform.SetParent(parent);
            actor.transform.position = position;
            SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 4;
            return actor;
        }

        private static void BuildDialogueUI(
            Transform parent,
            TMP_FontAsset font,
            out TMP_Text nameText,
            out TMP_Text lineText,
            out GameObject lineBackground)
        {
            GameObject canvasObject = new GameObject("MapUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            lineBackground = new GameObject("LineBackground", typeof(RectTransform), typeof(Image));
            lineBackground.transform.SetParent(canvasObject.transform, false);
            RectTransform backgroundRect = lineBackground.GetComponent<RectTransform>();
            ConfigureBottomRect(backgroundRect, 80f, 140f);
            lineBackground.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.84f);
            lineBackground.GetComponent<Image>().raycastTarget = false;

            TextMeshProUGUI body = BuildText(canvasObject.transform, "LineText", font, 40f, 80f, 96f);
            TextMeshProUGUI speaker = BuildText(canvasObject.transform, "CharacterNameText", font, 32f, 176f, 44f);
            lineText = body;
            nameText = speaker;
            lineBackground.SetActive(false);
            body.gameObject.SetActive(false);
            speaker.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI BuildText(
            Transform parent,
            string name,
            TMP_FontAsset font,
            float fontSize,
            float y,
            float height)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            ConfigureBottomRect(textObject.GetComponent<RectTransform>(), y, height);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureBottomRect(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-400f, height);
        }

        private static Sprite RequireCharacterSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new Exception("画像が見つかりません: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return RequireAsset<Sprite>(path);
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new Exception("アセットが見つかりません: " + path);
            return asset;
        }

        private static AngerBattle.MinigameLauncher FindLauncher()
        {
            AngerBattle.MinigameLauncher launcher = UnityEngine.Object.FindFirstObjectByType<AngerBattle.MinigameLauncher>();
            if (launcher == null) throw new Exception("MinigameLauncherが見つかりません。");
            return launcher;
        }

        private static string[] AppendUnique(string[] values, string value)
        {
            var list = new List<string>(values ?? Array.Empty<string>());
            if (!list.Contains(value)) list.Add(value);
            return list.ToArray();
        }

        private static void Validate(MemoryRecallController controller)
        {
            if (controller.player == null || controller.mapEnvironment == null ||
                controller.motherTransform == null || controller.lineText == null ||
                controller.eveningChimeClip == null)
            {
                throw new Exception("MemoryRecallControllerの必須参照が不足しています。");
            }
            foreach (MapCharacter friend in controller.friends)
            {
                if (friend.npcTransform == null) throw new Exception("友達の参照が不足しています。");
            }
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
