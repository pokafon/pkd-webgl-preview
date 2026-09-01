using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MemoryRecall;
using SadnessBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SadnessBattle.EditorTools
{
    /// <summary>共有の屋外／屋内Gridを使って悲しみコンタック戦を構築する。</summary>
    public static class SadnessBattleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string ContactSpritePath = "Assets/Resources/AngerBattle/ContackVertical.png";
        private const string MotherSpritePath = "Assets/Sprites/Tiles/pixelworld_complete_v.1.8/Characters/FemaleCharacter/PNGs/F_idle_left-Sheet.png";
        private const string MotherSpriteName = "F_idle_left-Sheet_3";
        private const string BulletPrefabPath = "Assets/AngerBattle/Prefabs/DenialBulletPrefab.prefab";
        private const string EveningChimePath = "Assets/Audio/夕焼け小焼け 防災行政無線チャイム 17時.mp3";
        private const string GeneratedSpritesDirectory = "Assets/SadnessBattle/Sprites";
        private const string SadnessPlaceholderPath = GeneratedSpritesDirectory + "/SadnessPlaceholder.png";
        private static readonly string[] MemoryFriendNames = { "MemoryFriendA", "MemoryFriendB", "MemoryFriendC" };
        private static readonly string[] LegacyPlacedFriendNames = { "Player_2", "Player_Actions_4", "Player_9" };

        [MenuItem("Tools/SadnessBattle/Build Scene")]
        public static void BuildFromMenu()
        {
            try
            {
                BuildInternal();
                EditorUtility.DisplayDialog("SadnessBattle", "2マップ構成の悲しみ戦を構築しました。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("SadnessBattle", "構築に失敗しました。Consoleを確認してください。", "OK");
            }
        }

        public static void Build()
        {
            try
            {
                BuildInternal();
                Debug.Log("SADNESSBATTLE_BUILD_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("SADNESSBATTLE_BUILD_RESULT: FAIL: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildInternal()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SadnessMapEnvironment environment = SadnessMapEditorUtility.EnsureEnvironment(scene);

            TMP_FontAsset font = RequireAsset<TMP_FontAsset>(FontPath);
            Sprite contactSprite = RequireAsset<Sprite>(ContactSpritePath);
            Sprite motherSprite = RequireSpriteSubAsset(MotherSpritePath, MotherSpriteName);
            Sprite[] friendSprites = FindCurrentFriendSprites(scene);
            GameObject bulletPrefab = RequireAsset<GameObject>(BulletPrefabPath);
            AudioClip eveningChime = RequireAsset<AudioClip>(EveningChimePath);
            Sprite sadnessSprite = CreateSadnessPlaceholder();

            SceneHierarchyUtility.DestroyNamedObject(scene, "SadnessBattleRoot");
            GameObject root = new GameObject("SadnessBattleRoot");
            SceneManager.MoveGameObjectToScene(root, scene);
            SceneHierarchyUtility.MoveUnderGroup(scene, root, SceneHierarchyUtility.MinigamesGroupName);

            GameObject playerObject = new GameObject("ContackPlayer", typeof(SpriteRenderer), typeof(AngerBattle.PlayerController));
            playerObject.transform.SetParent(root.transform);
            playerObject.transform.position = environment.outdoorStart.position;
            SpriteRenderer playerRenderer = playerObject.GetComponent<SpriteRenderer>();
            playerRenderer.sprite = contactSprite;
            playerRenderer.sortingOrder = 5;
            AngerBattle.PlayerController player = playerObject.GetComponent<AngerBattle.PlayerController>();
            player.speed = 5f;

            SadnessTarget[] friends = new SadnessTarget[3];
            string[] lines =
            {
                "友達: おさかなさんいっぱいいるよ",
                "友達: みてみて。オニヤンマつかまえたよ",
                "友達: 宿題やった？？？ぼくまだやってなーい"
            };
            for (int index = 0; index < friends.Length; index++)
            {
                AngerBattle.EnemyAnger enemy = BuildTarget(
                    root.transform,
                    $"FriendTarget{(char)('A' + index)}",
                    friendSprites[index],
                    environment.outdoorFriendSpots[index].position);
                friends[index] = new SadnessTarget { line = lines[index], enemy = enemy };
            }

            AngerBattle.EnemyAnger motherEnemy = BuildTarget(
                root.transform,
                "MotherTarget",
                motherSprite,
                environment.homeMotherSpot.position);
            SadnessTarget mother = new SadnessTarget { line = "お母さん: おかえり。", enemy = motherEnemy };

            GameObject sadnessActor = new GameObject("SadnessActor", typeof(SpriteRenderer));
            sadnessActor.transform.SetParent(root.transform);
            sadnessActor.transform.position = environment.homeMotherSpot.position + new Vector3(1.4f, 0f, 0f);
            sadnessActor.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            SpriteRenderer sadnessRenderer = sadnessActor.GetComponent<SpriteRenderer>();
            sadnessRenderer.sprite = sadnessSprite;
            sadnessRenderer.sortingOrder = 4;
            sadnessActor.SetActive(false);

            BuildDialogueUI(root.transform, font, out TMP_Text nameText, out TMP_Text lineText, out GameObject lineBackground);

            GameObject controllerObject = new GameObject(
                "SadnessBattleController",
                typeof(AudioSource),
                typeof(SadnessBattleController));
            controllerObject.transform.SetParent(root.transform);
            AudioSource audioSource = controllerObject.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            SadnessBattleController controller = controllerObject.GetComponent<SadnessBattleController>();
            controller.player = player;
            controller.mapEnvironment = environment;
            controller.denialBulletPrefab = bulletPrefab;
            controller.friendTargets = friends;
            controller.motherTarget = mother;
            controller.sadnessActor = sadnessActor;
            controller.attackLineText = lineText;
            controller.characterNameText = nameText;
            controller.lineBackground = lineBackground;
            controller.eveningChimeSource = audioSource;
            controller.eveningChimeClip = eveningChime;

            root.SetActive(false);
            environment.outdoorGrid.SetActive(false);
            environment.homeGrid.SetActive(false);

            AngerBattle.MinigameLauncher launcher = FindLauncher();
            launcher.sadnessBattleRoot = root;
            launcher.sadnessBattleController = controller;
            launcher.sadnessMapEnvironment = environment;
            launcher.debugStoryNodes = AppendUnique(launcher.debugStoryNodes, "Sadness");
            launcher.debugMinigames = AppendUnique(launcher.debugMinigames, "SadnessBattle");

            Validate(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new Exception("シーンの保存に失敗しました: " + ScenePath);
            }
            AssetDatabase.SaveAssets();
        }

        private static AngerBattle.EnemyAnger BuildTarget(Transform parent, string name, Sprite sprite, Vector3 position)
        {
            GameObject target = new GameObject(
                name,
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(BoxCollider2D),
                typeof(AngerBattle.EnemyAnger));
            target.transform.SetParent(parent);
            target.transform.position = position;
            target.GetComponent<SpriteRenderer>().sprite = sprite;
            target.GetComponent<SpriteRenderer>().sortingOrder = 4;

            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            BoxCollider2D collider = target.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;

            AngerBattle.EnemyAnger enemy = target.GetComponent<AngerBattle.EnemyAnger>();
            enemy.appearDuration = 0f;
            return enemy;
        }

        private static Sprite[] FindCurrentFriendSprites(Scene scene)
        {
            Transform[] transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
            var sprites = new Sprite[MemoryFriendNames.Length];

            for (int index = 0; index < sprites.Length; index++)
            {
                Transform actor = transforms.FirstOrDefault(item => item.name == MemoryFriendNames[index]) ??
                                  transforms.FirstOrDefault(item => item.name == LegacyPlacedFriendNames[index]);
                SpriteRenderer renderer = actor != null ? actor.GetComponent<SpriteRenderer>() : null;
                if (renderer == null || renderer.sprite == null)
                {
                    throw new Exception($"最新の友達{index + 1}のSpriteRendererが見つかりません。");
                }
                sprites[index] = renderer.sprite;
            }

            return sprites;
        }

        private static void BuildDialogueUI(
            Transform parent,
            TMP_FontAsset font,
            out TMP_Text nameText,
            out TMP_Text lineText,
            out GameObject lineBackground)
        {
            GameObject canvasObject = new GameObject("DialogueUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            lineBackground = new GameObject("AttackLineBackground", typeof(RectTransform), typeof(Image));
            lineBackground.transform.SetParent(canvasObject.transform, false);
            ConfigureBottomRect(lineBackground.GetComponent<RectTransform>(), 80f, 140f);
            lineBackground.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.84f);
            lineBackground.GetComponent<Image>().raycastTarget = false;

            TextMeshProUGUI body = BuildText(canvasObject.transform, "AttackLineText", font, 40f, 80f, 96f);
            TextMeshProUGUI speaker = BuildText(canvasObject.transform, "AttackLineCharacterName", font, 32f, 176f, 44f);
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

        private static Sprite CreateSadnessPlaceholder()
        {
            Directory.CreateDirectory(GeneratedSpritesDirectory);
            if (!File.Exists(SadnessPlaceholderPath))
            {
                const int size = 64;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[size * size];
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        pixels[y * size + x] = distance <= 28f
                            ? new Color(0.35f, 0.38f, 0.45f, 0.82f)
                            : Color.clear;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(SadnessPlaceholderPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SadnessPlaceholderPath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(SadnessPlaceholderPath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return RequireAsset<Sprite>(SadnessPlaceholderPath);
        }

        private static Sprite RequireSpriteSubAsset(string path, string spriteName)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .FirstOrDefault(candidate => candidate.name == spriteName);
            if (sprite == null) throw new Exception($"スプライトが見つかりません: {path} / {spriteName}");
            return sprite;
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

        private static void Validate(SadnessBattleController controller)
        {
            if (controller.player == null || controller.mapEnvironment == null ||
                controller.denialBulletPrefab == null || controller.friendTargets.Length != 3 ||
                controller.motherTarget?.enemy == null || controller.attackLineText == null ||
                controller.eveningChimeClip == null)
            {
                throw new Exception("SadnessBattleControllerの必須参照が不足しています。");
            }
        }

    }
}
