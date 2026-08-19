using System;
using System.Collections.Generic;
using System.IO;
using AngerBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AngerBattle.EditorTools
{
    /// <summary>
    /// 怒り戦（AngerBattle）に必要なシーン階層・プレハブ・参照配線を
    /// エディタスクリプトから一括構築するためのビルダー。
    ///
    /// 実行方法（バッチモード）:
    ///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt;
    ///     -executeMethod AngerBattle.EditorTools.AngerBattleSceneBuilder.Build
    ///
    /// 何度実行しても安全（AngerBattleRoot/MinigameLauncherを一度破棄してから再構築する）。
    /// </summary>
    public static class AngerBattleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SpritesDir = "Assets/AngerBattle/Sprites";
        private const string PrefabsDir = "Assets/AngerBattle/Prefabs";
        private const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string BgmClipPath = "Assets/Audio/Trick_style.mp3";

        public static void Build()
        {
            try
            {
                BuildInternal();
                Debug.Log("ANGERBATTLE_BUILD_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("ANGERBATTLE_BUILD_RESULT: FAIL: " + e.Message + "\n" + e.StackTrace);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildInternal()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(SpritesDir);
            Directory.CreateDirectory(PrefabsDir);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                throw new Exception($"フォントが見つかりません: {FontPath}");
            }

            var playerSprite = CreateCircleSprite(SpritesDir + "/PlayerSprite.png", new Color(0.30f, 0.75f, 1f), new Color(0.10f, 0.35f, 0.55f));
            var enemySprite = CreateSquareSprite(SpritesDir + "/EnemySprite.png", new Color(0.85f, 0.15f, 0.15f), new Color(0.4f, 0.03f, 0.03f));
            var bulletSprite = CreateCircleSprite(SpritesDir + "/BulletSprite.png", new Color(1f, 0.95f, 0.4f), new Color(0.6f, 0.5f, 0.05f));

            // --- 冪等性のため、既存のビルド結果を一度破棄する ---
            // 注: AngerBattleRootは非アクティブで保存されるため、GameObject.Find()では
            // 見つけられない（Findは非アクティブなオブジェクトを無視する）。
            // シーンのルートオブジェクトを直接走査して確実に破棄する。
            DestroyIfExists(scene, "AngerBattleRoot");
            DestroyIfExists(scene, "MinigameLauncher");

            // --- プレハブ ---
            GameObject fallingCharPrefab = BuildFallingCharacterPrefab(font);
            GameObject denialBulletPrefab = BuildDenialBulletPrefab(bulletSprite);

            // --- AngerBattleRoot 階層 ---
            var root = new GameObject("AngerBattleRoot");

            var playerGO = new GameObject("Player", typeof(SpriteRenderer), typeof(PlayerController));
            playerGO.transform.SetParent(root.transform);
            playerGO.transform.position = new Vector3(-3f, 0f, 0f);
            playerGO.GetComponent<SpriteRenderer>().sprite = playerSprite;
            var playerController = playerGO.GetComponent<PlayerController>();

            var enemyGO = new GameObject("Enemy", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(EnemyAnger));
            enemyGO.transform.SetParent(root.transform);
            enemyGO.transform.position = new Vector3(4f, 0f, 0f);
            enemyGO.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            enemyGO.GetComponent<SpriteRenderer>().sprite = enemySprite;
            var enemyRb = enemyGO.GetComponent<Rigidbody2D>();
            // 注: Box2D(Unity 2D物理)はKinematic同士・Kinematic-Static間ではトリガー判定が発生しない。
            // DenialBullet側はKinematic（毎フレームtransformで移動させるため）なので、
            // Enemy側はDynamic+重力ゼロ+全拘束フリーズにして、確実にトリガー判定が成立するようにする。
            enemyRb.bodyType = RigidbodyType2D.Dynamic;
            enemyRb.gravityScale = 0f;
            enemyRb.constraints = RigidbodyConstraints2D.FreezeAll;
            enemyRb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var enemyCol = enemyGO.GetComponent<BoxCollider2D>();
            enemyCol.isTrigger = true;
            // 注: BoxCollider2DはスクリプトからのAddComponentではsizeが(0.0001, 0.0001)という
            // 事実上ゼロの値になる（CircleCollider2Dのradius=0.5のような妥当な既定値にはならない）。
            // 明示的に設定しないと弾が素通りしてしまう。
            enemyCol.size = Vector2.one;
            var enemyAnger = enemyGO.GetComponent<EnemyAnger>();

            var bulletSpawnGO = new GameObject("BulletSpawnPoint");
            bulletSpawnGO.transform.SetParent(root.transform);
            bulletSpawnGO.transform.position = new Vector3(-2f, 0f, 0f);

            // --- 攻撃セリフ表示用UI（Yarnの通常セリフ表示に似た見た目） ---
            var battleUIGO = new GameObject("BattleUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            battleUIGO.transform.SetParent(root.transform, false);
            var canvas = battleUIGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = battleUIGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 注: Image(Graphic)とTextMeshProUGUI(Graphic)は同一GameObjectに共存できないため、
            // 背景パネルは使わずTMPのoutlineで視認性を確保する（見た目のみの妥協、参照配線は仕様通り）。
            var attackLineGO = new GameObject("AttackLineText", typeof(RectTransform), typeof(TextMeshProUGUI));
            attackLineGO.transform.SetParent(battleUIGO.transform, false);
            var attackRect = attackLineGO.GetComponent<RectTransform>();
            attackRect.anchorMin = new Vector2(0f, 0f);
            attackRect.anchorMax = new Vector2(1f, 0f);
            attackRect.pivot = new Vector2(0.5f, 0f);
            attackRect.anchoredPosition = new Vector2(0f, 80f);
            attackRect.sizeDelta = new Vector2(-400f, 140f);
            var attackText = attackLineGO.GetComponent<TextMeshProUGUI>();
            attackText.font = font;
            attackText.fontSize = 40;
            attackText.fontStyle = FontStyles.Bold;
            attackText.alignment = TextAlignmentOptions.Center;
            attackText.color = Color.white;
            attackText.outlineWidth = 0.2f;
            attackText.outlineColor = Color.black;
            attackText.text = "それは異常です";
            attackText.raycastTarget = false;
            attackLineGO.SetActive(false);

            // --- BGM ---
            var bgmGO = new GameObject("BGMPlayer", typeof(AudioSource), typeof(BattleBGM));
            bgmGO.transform.SetParent(root.transform);
            var bgm = bgmGO.GetComponent<BattleBGM>();
            var bgmAudioSource = bgmGO.GetComponent<AudioSource>();
            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.loop = false;
            bgmAudioSource.spatialBlend = 0f;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmClipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[AngerBattleSceneBuilder] BGMファイルが見つかりません: {BgmClipPath} 。BattleBGM.Clip は未設定のままです。");
            }
            bgm.clip = clip;
            bgm.loopStartSeconds = 12f;
            bgm.loopEndSeconds = 223.862f;

            // --- 進行管理コントローラー ---
            var controllerGO = new GameObject("AngerBattleController", typeof(AngerBattleController));
            controllerGO.transform.SetParent(root.transform);
            var controller = controllerGO.GetComponent<AngerBattleController>();
            controller.player = playerController;
            controller.enemy = enemyAnger;
            controller.bgm = bgm;
            controller.bulletSpawnPoint = bulletSpawnGO.transform;
            controller.denialBulletPrefab = denialBulletPrefab;
            controller.fallingCharacterPrefab = fallingCharPrefab;
            controller.attackLineText = attackText;

            root.SetActive(false);

            // --- MinigameLauncher（Dialogue Systemと同階層に配置） ---
            GameObject dialogueSystem = GameObject.Find("Dialogue System");
            GameObject dialogueCanvas = null;
            if (dialogueSystem != null)
            {
                var t = dialogueSystem.transform.Find("Canvas");
                if (t != null) dialogueCanvas = t.gameObject;
            }
            if (dialogueCanvas == null)
            {
                Debug.LogWarning("[AngerBattleSceneBuilder] 'Dialogue System/Canvas' が見つかりませんでした。MinigameLauncher.dialogueUIRoot は未設定のままにします。");
            }

            var launcherGO = new GameObject("MinigameLauncher", typeof(MinigameLauncher));
            var launcher = launcherGO.GetComponent<MinigameLauncher>();
            launcher.battleRoot = root;
            launcher.angerBattleController = controller;
            launcher.dialogueUIRoot = dialogueCanvas;

            ValidateController(controller);
            ValidateLauncher(launcher);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                throw new Exception("シーンの保存に失敗しました: " + ScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ValidateController(AngerBattleController c)
        {
            var missing = new List<string>();
            if (c.player == null) missing.Add("player");
            if (c.enemy == null) missing.Add("enemy");
            if (c.bgm == null) missing.Add("bgm");
            if (c.bulletSpawnPoint == null) missing.Add("bulletSpawnPoint");
            if (c.denialBulletPrefab == null) missing.Add("denialBulletPrefab");
            if (c.fallingCharacterPrefab == null) missing.Add("fallingCharacterPrefab");
            if (c.attackLineText == null) missing.Add("attackLineText");
            if (missing.Count > 0)
            {
                throw new Exception("AngerBattleControllerの参照が未設定です: " + string.Join(", ", missing));
            }
            if (c.bgm.clip == null)
            {
                Debug.LogWarning("[AngerBattleSceneBuilder] BattleBGM.Clip が未設定です（Trick_style.mp3が見つからなかった可能性があります）。");
            }
        }

        private static void ValidateLauncher(MinigameLauncher l)
        {
            var missing = new List<string>();
            if (l.battleRoot == null) missing.Add("battleRoot");
            if (l.angerBattleController == null) missing.Add("angerBattleController");
            if (missing.Count > 0)
            {
                throw new Exception("MinigameLauncherの参照が未設定です: " + string.Join(", ", missing));
            }
            if (l.dialogueUIRoot == null)
            {
                Debug.LogWarning("[AngerBattleSceneBuilder] MinigameLauncher.dialogueUIRoot が未設定です（任意項目）。");
            }
        }

        private static void DestroyIfExists(UnityEngine.SceneManagement.Scene scene, string name)
        {
            // ルートを直接走査（非アクティブなオブジェクトもGameObject.Findと違い見つけられる）。
            // 過去の実行で複製が残っている場合に備え、該当する名前のルートを全て破棄する。
            var roots = scene.GetRootGameObjects();
            for (int i = roots.Length - 1; i >= 0; i--)
            {
                if (roots[i].name == name)
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }
        }

        private static GameObject BuildFallingCharacterPrefab(TMP_FontAsset font)
        {
            var go = new GameObject("FallingCharacterPrefab", typeof(TextMeshPro), typeof(FallingWord));
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSize = 10;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = "字";
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            string path = PrefabsDir + "/FallingCharacterPrefab.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildDenialBulletPrefab(Sprite bulletSprite)
        {
            var go = new GameObject("DenialBulletPrefab", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(DenialBullet));
            go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            go.GetComponent<SpriteRenderer>().sprite = bulletSprite;
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            go.GetComponent<CircleCollider2D>().isTrigger = true;

            string path = PrefabsDir + "/DenialBulletPrefab.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
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

        private static Sprite CreateSquareSprite(string path, Color fill, Color outline, int size = 128, int ppu = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            int border = Mathf.Max(3, size / 16);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = isBorder ? outline : fill;
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
