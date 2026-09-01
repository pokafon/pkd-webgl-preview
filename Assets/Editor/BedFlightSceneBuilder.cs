using System;
using System.Collections.Generic;
using System.IO;
using BedFlight;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BedFlight.EditorTools
{
    /// <summary>
    /// ベッド飛行（BedFlight）に必要なシーン階層・プレハブ・参照配線を
    /// エディタスクリプトから一括構築するためのビルダー。
    /// AngerBattle.EditorTools.AngerBattleSceneBuilderと同じ考え方で、
    /// 既製アセットは使わずコードで簡易スプライトを生成する（コンタックの見た目のみ、
    /// 怒り戦・不安戦のプレイヤースプライトをそのまま再利用して「見覚えのある姿」にする）。
    ///
    /// 実行方法：
    ///   Unityエディタのメニュー「Tools/BedFlight/Build Scene」から実行（推奨）。
    ///   バッチモードの場合は
    ///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt;
    ///     -executeMethod BedFlight.EditorTools.BedFlightSceneBuilder.Build
    ///
    /// 何度実行しても安全（BedFlightRootを一度破棄してから再構築する。
    /// 既存のMinigameLauncherは破棄せず、参照フィールドだけ追記する）。
    /// </summary>
    public static class BedFlightSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SpritesDir = "Assets/BedFlight/Sprites";
        private const string PrefabsDir = "Assets/BedFlight/Prefabs";
        private const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string PlayerSpritePathInAngerBattle = "Assets/Resources/AngerBattle/ContackVertical.png";
        private const string BulletSpritePathInAngerBattle = "Assets/AngerBattle/Sprites/BulletSprite.png";

        [MenuItem("Tools/BedFlight/Build Scene")]
        public static void BuildFromMenu()
        {
            try
            {
                BuildInternal();
                EditorUtility.DisplayDialog("BedFlight", "BedFlightRootの構築が完了しました。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[BedFlightSceneBuilder] 構築に失敗しました: " + e.Message + "\n" + e.StackTrace);
                EditorUtility.DisplayDialog("BedFlight", "構築に失敗しました。コンソールを確認してください。\n" + e.Message, "OK");
            }
        }

        public static void Build()
        {
            try
            {
                BuildInternal();
                Debug.Log("BEDFLIGHT_BUILD_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("BEDFLIGHT_BUILD_RESULT: FAIL: " + e.Message + "\n" + e.StackTrace);
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

            var contacSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePathInAngerBattle);
            if (contacSprite == null)
            {
                throw new Exception($"コンタックの見た目に使う既存スプライトが見つかりません: {PlayerSpritePathInAngerBattle}");
            }
            var bulletSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BulletSpritePathInAngerBattle);
            if (bulletSprite == null)
            {
                throw new Exception($"弾の見た目に使う既存スプライトが見つかりません: {BulletSpritePathInAngerBattle}");
            }

            var bedSprite = BedFlightBackdropInstaller.LoadFlyingBedSprite();
            var whiteSquareSprite = CreateFlatColorSprite(SpritesDir + "/WhiteSquare.png", Color.white);

            SceneHierarchyUtility.DestroyNamedObject(scene, "BedFlightRoot");

            GameObject contacBulletPrefab = BuildContacBulletPrefab(bulletSprite);

            // --- BedFlightRoot 階層 ---
            var root = new GameObject("BedFlightRoot");
            SceneHierarchyUtility.MoveUnderGroup(scene, root, SceneHierarchyUtility.MinigamesGroupName);

            // --- 背景（空＋街のパララックス） ---
            var cityGO = new GameObject("CityBackground", typeof(CityBackgroundScroller));
            cityGO.transform.SetParent(root.transform);
            cityGO.transform.position = Vector3.zero;
            var cityScroller = cityGO.GetComponent<CityBackgroundScroller>();

            var skyGO = new GameObject("Sky", typeof(SpriteRenderer));
            skyGO.transform.SetParent(cityGO.transform);
            skyGO.transform.localPosition = new Vector3(0f, 0f, 1f);
            skyGO.transform.localScale = new Vector3(40f, 20f, 1f);
            var skyRenderer = skyGO.GetComponent<SpriteRenderer>();
            skyRenderer.sprite = whiteSquareSprite;
            skyRenderer.sortingOrder = -10;
            cityScroller.sky = skyRenderer;

            // 逃避行専用の街・雲・ベビーメリーを提供素材から構築する。
            // 専用インストーラーと同じ処理を使い、ビルダー再実行後も背景演出が消えないようにする。
            BedFlightBackdropInstaller.ApplyToScene(scene, root);

            // --- プレイヤー（ベッドに乗った主人公） ---
            var playerGO = new GameObject(
                "FlyingBed",
                typeof(SpriteRenderer),
                typeof(AngerBattle.PlayerController),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D));
            playerGO.transform.SetParent(root.transform);
            playerGO.transform.position = new Vector3(-3f, 0f, 0f);
            playerGO.transform.localScale = Vector3.one * 0.35f;
            playerGO.GetComponent<SpriteRenderer>().sprite = bedSprite;
            playerGO.GetComponent<SpriteRenderer>().color = Color.white;
            var playerController = playerGO.GetComponent<AngerBattle.PlayerController>();
            var playerRb = playerGO.GetComponent<Rigidbody2D>();
            playerRb.bodyType = RigidbodyType2D.Dynamic;
            playerRb.gravityScale = 0f;
            playerRb.constraints = RigidbodyConstraints2D.FreezeAll;
            playerRb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var playerCol = playerGO.GetComponent<CircleCollider2D>();
            playerCol.isTrigger = true;
            playerCol.radius = 1.5f;

            // --- コンタック（追ってくる本体。見た目は怒り戦・不安戦のプレイヤーと同じ） ---
            var contacGO = new GameObject("Contack", typeof(SpriteRenderer), typeof(ContacChaser));
            contacGO.transform.SetParent(root.transform);
            contacGO.transform.position = new Vector3(4f, 0f, 0f);
            contacGO.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            contacGO.GetComponent<SpriteRenderer>().sprite = contacSprite;
            contacGO.GetComponent<SpriteRenderer>().sortingOrder = 2;
            var contacChaser = contacGO.GetComponent<ContacChaser>();
            contacChaser.sprite = contacGO.GetComponent<SpriteRenderer>();

            // --- セリフ表示UI（現実パートと同じ見た目：背景パネル＋話者名＋本文）＋終了暗転パネル ---
            var battleUIGO = new GameObject("DialogueUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
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

            // 終了暗転パネル。BattleUIの最後の子にして、他のUIより手前に描画されるようにする
            var endFadeGO = new GameObject("EndFadePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            endFadeGO.transform.SetParent(battleUIGO.transform, false);
            var endFadeRect = endFadeGO.GetComponent<RectTransform>();
            endFadeRect.anchorMin = Vector2.zero;
            endFadeRect.anchorMax = Vector2.one;
            endFadeRect.offsetMin = Vector2.zero;
            endFadeRect.offsetMax = Vector2.zero;
            var endFadeImage = endFadeGO.GetComponent<Image>();
            endFadeImage.color = Color.black;
            endFadeImage.raycastTarget = false;
            var endFadeGroup = endFadeGO.GetComponent<CanvasGroup>();
            endFadeGroup.alpha = 0f;
            endFadeGroup.interactable = false;
            endFadeGroup.blocksRaycasts = false;

            // --- 進行管理コントローラー ---
            var controllerGO = new GameObject("BedFlightController", typeof(BedFlightController));
            controllerGO.transform.SetParent(root.transform);
            var controller = controllerGO.GetComponent<BedFlightController>();
            controller.player = playerController;
            controller.background = cityScroller;
            controller.contac = contacChaser;
            controller.contacBulletPrefab = contacBulletPrefab;
            controller.attackLineText = attackText;
            controller.characterNameText = nameText;
            controller.lineBackground = lineBackgroundGO;
            controller.endFadeGroup = endFadeGroup;
            controller.contacRestingPosition = contacGO.transform.position;

            root.SetActive(false);

            // --- 既存のMinigameLauncherに参照を追記する（新規作成はしない） ---
            var launcherGO = GameObject.Find("MinigameLauncher");
            if (launcherGO == null)
            {
                throw new Exception("シーン内に既存の'MinigameLauncher'が見つかりません。先にAngerBattle/FuanBattleのセットアップが必要です。");
            }
            var launcher = launcherGO.GetComponent<AngerBattle.MinigameLauncher>();
            launcher.bedFlightRoot = root;
            launcher.bedFlightController = controller;

            // 既にシーンに保存されているdebugMinigamesの値には、C#側のフィールド初期値変更は反映されない
            // （シリアライズ済みの値が優先されるため）。ここで明示的に追記する。
            bool alreadyListed = false;
            foreach (var name in launcher.debugMinigames)
            {
                if (name == "BedFlight") { alreadyListed = true; break; }
            }
            if (!alreadyListed)
            {
                var list = new List<string>(launcher.debugMinigames) { "BedFlight" };
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

        private static void ValidateController(BedFlightController c)
        {
            var missing = new List<string>();
            if (c.player == null) missing.Add("player");
            if (c.background == null) missing.Add("background");
            if (c.contac == null) missing.Add("contac");
            if (c.contacBulletPrefab == null) missing.Add("contacBulletPrefab");
            if (c.attackLineText == null) missing.Add("attackLineText");
            if (c.endFadeGroup == null) missing.Add("endFadeGroup");
            if (missing.Count > 0)
            {
                throw new Exception("BedFlightControllerの参照が未設定です: " + string.Join(", ", missing));
            }
        }

        private static GameObject BuildContacBulletPrefab(Sprite bulletSprite)
        {
            var go = new GameObject(
                "ContacBulletPrefab",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(ContacBullet));
            go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            go.GetComponent<SpriteRenderer>().sprite = bulletSprite;
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            go.GetComponent<CircleCollider2D>().isTrigger = true;

            string path = PrefabsDir + "/ContacBulletPrefab.prefab";
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

        private static Sprite CreateRectSprite(string path, Color fill, Color outline, int width = 160, int height = 96, int ppu = 128)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            int border = Mathf.Max(3, height / 12);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x < border || y < border || x >= width - border || y >= height - border;
                    pixels[y * width + x] = isBorder ? outline : fill;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return SaveTextureAsSprite(tex, path, ppu);
        }

        private static Sprite CreateFlatColorSprite(string path, Color fill, int size = 4, int ppu = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
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
