using System;
using System.Collections.Generic;
using System.IO;
using SadnessBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SadnessBattle.EditorTools
{
    /// <summary>
    /// 記憶回想 悲しみコンタックバトルに必要なシーン階層・プレハブ・参照配線を
    /// エディタスクリプトから一括構築するためのビルダー。
    /// AngerBattle.EditorTools.AngerBattleSceneBuilderと同じ考え方で、既製アセットは使わず
    /// コードで簡易スプライト（単色の丸）を生成する。プレイヤー（コンタック）と攻撃弾は、
    /// 怒り戦・不安戦と同じ既存アセット（PlayerSprite.png・DenialBulletPrefab）をそのまま再利用する。
    ///
    /// 実行方法：
    ///   Unityエディタのメニュー「Tools/SadnessBattle/Build Scene」から実行（推奨）。
    ///   バッチモードの場合は
    ///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt;
    ///     -executeMethod SadnessBattle.EditorTools.SadnessBattleSceneBuilder.Build
    ///
    /// 何度実行しても安全（SadnessBattleRootを一度破棄してから再構築する。
    /// 既存のMinigameLauncherは破棄せず、参照フィールドだけ追記する）。
    /// </summary>
    public static class SadnessBattleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SpritesDir = "Assets/SadnessBattle/Sprites";
        private const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string PlayerSpritePathInAngerBattle = "Assets/AngerBattle/Sprites/PlayerSprite.png";
        private const string DenialBulletPrefabPath = "Assets/AngerBattle/Prefabs/DenialBulletPrefab.prefab";

        [MenuItem("Tools/SadnessBattle/Build Scene")]
        public static void BuildFromMenu()
        {
            try
            {
                BuildInternal();
                EditorUtility.DisplayDialog("SadnessBattle", "SadnessBattleRootの構築が完了しました。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[SadnessBattleSceneBuilder] 構築に失敗しました: " + e.Message + "\n" + e.StackTrace);
                EditorUtility.DisplayDialog("SadnessBattle", "構築に失敗しました。コンソールを確認してください。\n" + e.Message, "OK");
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
            catch (Exception e)
            {
                Debug.LogError("SADNESSBATTLE_BUILD_RESULT: FAIL: " + e.Message + "\n" + e.StackTrace);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildInternal()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(SpritesDir);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                throw new Exception($"フォントが見つかりません: {FontPath}");
            }

            var playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePathInAngerBattle);
            if (playerSprite == null)
            {
                throw new Exception($"コンタック（プレイヤー）の見た目に使う既存スプライトが見つかりません: {PlayerSpritePathInAngerBattle}");
            }

            var denialBulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DenialBulletPrefabPath);
            if (denialBulletPrefab == null)
            {
                throw new Exception($"攻撃弾プレハブが見つかりません: {DenialBulletPrefabPath}");
            }

            var motherSprite = CreateCircleSprite(SpritesDir + "/Mother.png", new Color(0.9f, 0.55f, 0.6f), new Color(0.55f, 0.25f, 0.3f));
            var friendSprite = CreateCircleSprite(SpritesDir + "/Friend.png", new Color(0.95f, 0.8f, 0.4f), new Color(0.6f, 0.45f, 0.1f));

            DestroyIfExists(scene, "SadnessBattleRoot");

            // --- SadnessBattleRoot 階層 ---
            var root = new GameObject("SadnessBattleRoot");

            var houseGO = new GameObject("House", typeof(BedFlight.HouseIntro));
            houseGO.transform.SetParent(root.transform);
            houseGO.transform.position = new Vector3(-3f, -3f, 0f);
            var houseIntro = houseGO.GetComponent<BedFlight.HouseIntro>();
            houseIntro.bodyWidth = 3.2f;
            houseIntro.bodyHeight = 2.6f;
            houseIntro.roofHeight = 1.8f;

            var playerGO = new GameObject("Player", typeof(SpriteRenderer), typeof(AngerBattle.PlayerController));
            playerGO.transform.SetParent(root.transform);
            playerGO.transform.position = new Vector3(-3f, 0f, 0f);
            playerGO.GetComponent<SpriteRenderer>().sprite = playerSprite;
            var playerController = playerGO.GetComponent<AngerBattle.PlayerController>();

            var bulletSpawnGO = new GameObject("BulletSpawnPoint");
            bulletSpawnGO.transform.SetParent(root.transform);
            bulletSpawnGO.transform.position = new Vector3(-2f, 0f, 0f);

            Vector3 restingPosition = new Vector3(4f, 0f, 0f);

            var targetFriendA = BuildTarget(root.transform, "TargetFriendA", friendSprite, restingPosition);
            var targetFriendB = BuildTarget(root.transform, "TargetFriendB", friendSprite, restingPosition);
            var targetFriendC = BuildTarget(root.transform, "TargetFriendC", friendSprite, restingPosition);
            var targetMother = BuildTarget(root.transform, "TargetMother", motherSprite, restingPosition);

            // --- セリフ表示UI（現実パートと同じ見た目：背景パネル＋話者名＋本文） ---
            var battleUIGO = new GameObject("BattleUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            battleUIGO.transform.SetParent(root.transform, false);
            var canvas = battleUIGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = battleUIGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var lineBackgroundGO = new GameObject("AttackLineBackground", typeof(RectTransform), typeof(Image));
            lineBackgroundGO.transform.SetParent(battleUIGO.transform, false);
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

            var attackTextGO = new GameObject("AttackLineText", typeof(RectTransform), typeof(TextMeshProUGUI));
            attackTextGO.transform.SetParent(battleUIGO.transform, false);
            var attackTextRect = attackTextGO.GetComponent<RectTransform>();
            attackTextRect.anchorMin = new Vector2(0f, 0f);
            attackTextRect.anchorMax = new Vector2(1f, 0f);
            attackTextRect.pivot = new Vector2(0.5f, 0f);
            attackTextRect.anchoredPosition = new Vector2(0f, 80f);
            attackTextRect.sizeDelta = new Vector2(-400f, 96f);
            var attackText = attackTextGO.GetComponent<TextMeshProUGUI>();
            attackText.font = font;
            attackText.fontSize = 40;
            attackText.fontStyle = FontStyles.Bold;
            attackText.alignment = TextAlignmentOptions.TopLeft;
            attackText.color = Color.white;
            attackText.raycastTarget = false;
            attackTextGO.SetActive(false);

            var nameGO = new GameObject("AttackLineCharacterName", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(battleUIGO.transform, false);
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
            var controllerGO = new GameObject("SadnessBattleController", typeof(SadnessBattleController));
            controllerGO.transform.SetParent(root.transform);
            var controller = controllerGO.GetComponent<SadnessBattleController>();
            controller.player = playerController;
            controller.bulletSpawnPoint = bulletSpawnGO.transform;
            controller.denialBulletPrefab = denialBulletPrefab;
            controller.attackLineText = attackText;
            controller.characterNameText = nameText;
            controller.lineBackground = lineBackgroundGO;
            controller.houseIntro = houseIntro;
            controller.targets = new SadnessTarget[]
            {
                new SadnessTarget { line = "友達: おさかなさんいっぱいいるよ", enemy = targetFriendA },
                new SadnessTarget { line = "友達: みてみて。オニヤンマつかまえたよ", enemy = targetFriendB },
                new SadnessTarget { line = "友達: 宿題やった？？？ぼくまだやってなーい", enemy = targetFriendC },
                new SadnessTarget { line = "お母さん: おはよう。お昼寝たくさんした？ご飯まだだから、お外で友達と遊んできな。", enemy = targetMother },
            };

            root.SetActive(false);

            // --- 既存のMinigameLauncherに参照を追記する（新規作成はしない） ---
            var launcherGO = GameObject.Find("MinigameLauncher");
            if (launcherGO == null)
            {
                throw new Exception("シーン内に既存の'MinigameLauncher'が見つかりません。先にAngerBattle/FuanBattleのセットアップが必要です。");
            }
            var launcher = launcherGO.GetComponent<AngerBattle.MinigameLauncher>();
            launcher.sadnessBattleRoot = root;
            launcher.sadnessBattleController = controller;

            bool alreadyListed = false;
            foreach (var name in launcher.debugMinigames)
            {
                if (name == "SadnessBattle") { alreadyListed = true; break; }
            }
            if (!alreadyListed)
            {
                var list = new List<string>(launcher.debugMinigames) { "SadnessBattle" };
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

        private static AngerBattle.EnemyAnger BuildTarget(Transform parent, string name, Sprite sprite, Vector3 position)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(AngerBattle.EnemyAnger));
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            go.GetComponent<SpriteRenderer>().sprite = sprite;
            var rb = go.GetComponent<Rigidbody2D>();
            // 注: Box2D(Unity 2D物理)はKinematic同士・Kinematic-Static間ではトリガー判定が発生しない。
            // DenialBullet側はKinematicなので、対象側はDynamic+重力ゼロ+全拘束フリーズにして
            // 確実にトリガー判定が成立するようにする（AngerBattleSceneBuilderと同じ対応）。
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;
            return go.GetComponent<AngerBattle.EnemyAnger>();
        }

        private static void ValidateController(SadnessBattleController c)
        {
            var missing = new List<string>();
            if (c.player == null) missing.Add("player");
            if (c.denialBulletPrefab == null) missing.Add("denialBulletPrefab");
            if (c.attackLineText == null) missing.Add("attackLineText");
            if (c.targets == null || c.targets.Length == 0) missing.Add("targets");
            else
            {
                foreach (var t in c.targets)
                {
                    if (t.enemy == null) missing.Add("targets[].enemy");
                }
            }
            if (missing.Count > 0)
            {
                throw new Exception("SadnessBattleControllerの参照が未設定です: " + string.Join(", ", missing));
            }
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

        private static Sprite CreateCircleSprite(string path, Color fill, Color outline, int size = 128, int ppu = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float rOuter = size / 2f - 1f;
            float rInner = rOuter - Mathf.Max(3, size / 16);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    Color c = Color.clear;
                    if (d <= rOuter) c = d <= rInner ? fill : outline;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return SaveTextureAsSprite(tex, path, ppu);
        }

        private static Sprite SaveTextureAsSprite(Texture2D tex, string path, int ppu)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
