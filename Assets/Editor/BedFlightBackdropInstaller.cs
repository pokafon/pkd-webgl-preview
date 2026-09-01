using System;
using System.Collections.Generic;
using BedFlight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BedFlight.EditorTools
{
    /// <summary>
    /// 逃避行（BedFlight）専用の街・雲・ベビーメリーを、既存シーンを壊さず追加する。
    /// BedFlightSceneBuilderからも呼べるよう、処理本体は冪等なApplyToSceneにまとめる。
    /// </summary>
    public static class BedFlightBackdropInstaller
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ArtDirectory = "Assets/Sprites/touhikou";
        private const string ForegroundPath = ArtDirectory + "/mati_mae.png";
        private const string BackgroundPath = ArtDirectory + "/mati_ushiro.png";
        private const string CloudPath = ArtDirectory + "/kumo.png";
        private const string MobilePath = ArtDirectory + "/marry.png";
        private const string CordPath = ArtDirectory + "/maryy_himo.png";
        private const string FlyingBedPath = ArtDirectory + "/flying_bed_player.png";

        [MenuItem("Tools/BedFlight/Install Escape Backdrop")]
        public static void InstallFromMenu()
        {
            try
            {
                InstallInternal();
                EditorUtility.DisplayDialog("BedFlight", "逃避行の背景とメリーを追加しました。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogError("[BedFlightBackdropInstaller] " + exception);
                EditorUtility.DisplayDialog("BedFlight", "追加に失敗しました。Consoleを確認してください。", "OK");
            }
        }

        /// <summary>Unityの-batchmode -executeMethodから呼ぶ入口。</summary>
        public static void Install()
        {
            try
            {
                InstallInternal();
                Debug.Log("BEDFLIGHT_BACKDROP_INSTALL_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("BEDFLIGHT_BACKDROP_INSTALL_RESULT: FAIL\n" + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void InstallInternal()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = FindSceneObject(scene, "BedFlightRoot");
            if (root == null) throw new Exception("BedFlightRootが見つかりません。");

            ApplyToScene(scene, root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new Exception("シーンの保存に失敗しました: " + ScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void ApplyToScene(Scene scene, GameObject bedFlightRoot)
        {
            if (bedFlightRoot == null) throw new ArgumentNullException(nameof(bedFlightRoot));

            CityBackgroundScroller background =
                bedFlightRoot.GetComponentInChildren<CityBackgroundScroller>(true);
            if (background == null)
            {
                throw new Exception("BedFlightRoot内にCityBackgroundScrollerが見つかりません。");
            }

            background.foregroundCitySprite = LoadNamedSprite(ForegroundPath, "mati_mae_0");
            background.backgroundCitySprites = LoadUsableSprites(BackgroundPath, 0.15f, 0.3f);
            background.suppliedCloudSprites = LoadUsableSprites(CloudPath, 0.2f, 0.2f);
            background.offscreenPreloadPadding = 6f;
            background.hasBuildingExcludeZone = false;
            EditorUtility.SetDirty(background);

            BedFlightController controller =
                bedFlightRoot.GetComponentInChildren<BedFlightController>(true);
            if (controller != null)
            {
                controller.houseIntro = null;
                EditorUtility.SetDirty(controller);
            }

            Transform obsoleteHouse = bedFlightRoot.transform.Find("HouseIntro");
            if (obsoleteHouse != null)
            {
                UnityEngine.Object.DestroyImmediate(obsoleteHouse.gameObject);
            }

            ApplyFlyingBedVisual(bedFlightRoot);

            Transform existing = bedFlightRoot.transform.Find("BabyMobileAnchor");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            Sprite mobileSprite = LoadNamedSprite(MobilePath, "marry_0");
            Sprite cordSprite = LoadNamedSprite(CordPath, "maryy_himo_0");
            Camera mainCamera = FindMainCamera(scene);
            if (mainCamera == null) throw new Exception("Main Cameraが見つかりません。");

            var anchor = new GameObject("BabyMobileAnchor", typeof(BabyMobileAmbient));
            anchor.transform.SetParent(bedFlightRoot.transform, false);

            var cordPivot = new GameObject("CordPivot");
            cordPivot.transform.SetParent(anchor.transform, false);
            cordPivot.transform.localScale = Vector3.one;

            var cordVisual = new GameObject("Cord", typeof(SpriteRenderer));
            cordVisual.transform.SetParent(cordPivot.transform, false);
            cordVisual.transform.localPosition = new Vector3(0f, -cordSprite.bounds.extents.y, 0f);
            SpriteRenderer cordRenderer = cordVisual.GetComponent<SpriteRenderer>();
            cordRenderer.sprite = cordSprite;
            cordRenderer.sortingOrder = -6;

            var mobilePivot = new GameObject("MobilePivot");
            mobilePivot.transform.SetParent(cordPivot.transform, false);
            // 元画像では紐とフックが少し重なるため、紐の全長より手前に接続点を置く。
            mobilePivot.transform.localPosition =
                new Vector3(0f, -cordSprite.bounds.size.y * 0.7f, 0f);

            var mobileVisual = new GameObject("Mobile", typeof(SpriteRenderer));
            mobileVisual.transform.SetParent(mobilePivot.transform, false);
            mobileVisual.transform.localPosition = new Vector3(0f, -mobileSprite.bounds.extents.y, 0f);
            SpriteRenderer mobileRenderer = mobileVisual.GetComponent<SpriteRenderer>();
            mobileRenderer.sprite = mobileSprite;
            mobileRenderer.sortingOrder = -6;

            BabyMobileAmbient ambient = anchor.GetComponent<BabyMobileAmbient>();
            ambient.targetCamera = mainCamera;
            ambient.viewportAnchor = new Vector2(0.68f, 1.01f);
            ambient.worldPlaneZ = 0f;
            ambient.visualScale = 0.65f;
            ambient.sortingOrder = -6;
            ambient.visualTint = new Color32(38, 50, 74, 220);
            ambient.cordPivot = cordPivot.transform;
            ambient.mobilePivot = mobilePivot.transform;
            ambient.cordRenderer = cordRenderer;
            ambient.mobileRenderer = mobileRenderer;
            ambient.cordAngle = 0.35f;
            ambient.mobileAngle = 0.8f;
            ambient.swayFrequency = 0.18f;
            ambient.bobAmplitude = 0.05f;
            ambient.bobFrequency = 0.14f;
            ambient.mobilePhaseLag = 0.55f;
            ambient.ApplyVisualSettings();
            EditorUtility.SetDirty(ambient);

            AngerBattle.MinigameLauncher launcher =
                FindSceneObject(scene, "MinigameLauncher")?.GetComponent<AngerBattle.MinigameLauncher>();
            if (launcher != null)
            {
                launcher.globalAudioVolume = 0.7f;
                EditorUtility.SetDirty(launcher);
            }
        }

        private static void ApplyFlyingBedVisual(GameObject bedFlightRoot)
        {
            Transform flyingBed = bedFlightRoot.transform.Find("FlyingBed");
            if (flyingBed == null) return;

            SpriteRenderer renderer = flyingBed.GetComponent<SpriteRenderer>();
            if (renderer == null) return;

            renderer.sprite = LoadSingleSprite(FlyingBedPath);
            renderer.color = Color.white;
            renderer.sortingOrder = 0;
            flyingBed.localScale = Vector3.one * 0.35f;

            Transform obsoleteRider = flyingBed.Find("Rider");
            if (obsoleteRider != null)
            {
                UnityEngine.Object.DestroyImmediate(obsoleteRider.gameObject);
            }

            CircleCollider2D collider = flyingBed.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.offset = Vector2.zero;
                collider.radius = 1.5f;
            }

            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(flyingBed);
            if (collider != null) EditorUtility.SetDirty(collider);
        }

        public static Sprite LoadFlyingBedSprite()
        {
            return LoadSingleSprite(FlyingBedPath);
        }

        private static Sprite LoadSingleSprite(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            }
            if (importer == null) throw new Exception("画像を読み込めません: " + assetPath);

            bool needsReimport = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.spritePixelsPerUnit != 100f ||
                importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled;
            if (needsReimport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) throw new Exception("スプライトを読み込めません: " + assetPath);
            return sprite;
        }

        private static Sprite LoadNamedSprite(string assetPath, string spriteName)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
            }

            throw new Exception($"スプライトが見つかりません: {assetPath} ({spriteName})");
        }

        private static Sprite[] LoadUsableSprites(string assetPath, float minimumWidth, float minimumHeight)
        {
            var sprites = new List<Sprite>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (!(asset is Sprite sprite)) continue;
                if (sprite.bounds.size.x < minimumWidth || sprite.bounds.size.y < minimumHeight) continue;
                sprites.Add(sprite);
            }

            sprites.Sort((left, right) => left.rect.x.CompareTo(right.rect.x));
            if (sprites.Count == 0) throw new Exception("使用可能なスプライトがありません: " + assetPath);
            return sprites.ToArray();
        }

        private static Camera FindMainCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.CompareTag("MainCamera")) return camera;
                }
            }
            return null;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName) return child.gameObject;
                }
            }
            return null;
        }
    }
}
