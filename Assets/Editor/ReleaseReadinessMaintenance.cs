using System;
using System.Linq;
using AngerBattle;
using BedFlight;
using MemoryRecall;
using SadnessBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PKD.EditorTools
{
    /// <summary>
    /// SampleSceneの旧プレースホルダーと散らかったルート階層を、現行仕様へ一度で移行する。
    /// 再実行可能にして、手作業による参照漏れを防ぐ。
    /// </summary>
    public static class ReleaseReadinessMaintenance
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ContackSpritePath = "Assets/Resources/AngerBattle/ContackVertical.png";
        private const string AngerSpritePath = "Assets/Resources/AngerBattle/AngerVertical.png";
        private const string AnxietySpritePath = "Assets/Resources/AngerBattle/AnxietyVertical.png";
        private const string ChildSpritePath = "Assets/Sprites/Tiles/pixelworld_complete_v.1.8/Characters/MaleCharacter/PNGs/M_idle_front-Sheet.png";
        private const string ChildSpriteName = "M_idle_front-Sheet_3";
        private const string MotherSpritePath = "Assets/Sprites/Tiles/pixelworld_complete_v.1.8/Characters/FemaleCharacter/PNGs/F_idle_left-Sheet.png";
        private const string MotherSpriteName = "F_idle_left-Sheet_3";
        private const string JapaneseFontAssetPath = "Assets/Fonts/NotoSansJP SDF.asset";

        private static readonly string[] ObsoleteAssetPaths =
        {
            "Assets/AngerBattle/Sprites/EnemySprite.png",
            "Assets/AngerBattle/Sprites/EnemySpriteWhite.png",
            "Assets/AngerBattle/Sprites/PlayerSprite.png",
            "Assets/FuanBattle/Sprites/EnemySpriteWhite.png",
            "Assets/Sprites/Player_Placeholder.png",
            "Assets/Sprites/mum.png",
            "Assets/Sprites/mum-20260823-203803.piskel",
            "Assets/SadnessBattle/Sprites/Mother.png",
            "Assets/Fonts/YuGothicUI SDF.asset",
            "Assets/Fonts/YuGothicUI SDF 1.asset",
            "Assets/Editor/RoomSetupTool.cs",
            "Assets/Editor/RoomValidationTool.cs",
        };

        [MenuItem("Tools/PKD/Prepare Release Candidate")]
        public static void ApplyFromMenu()
        {
            try
            {
                ApplyInternal();
                EditorUtility.DisplayDialog("PKD", "リリース候補用の整理が完了しました。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("PKD", "整理に失敗しました。Consoleを確認してください。", "OK");
            }
        }

        public static void Apply()
        {
            try
            {
                ApplyInternal();
                Debug.Log("PKD_RELEASE_MAINTENANCE_RESULT: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("PKD_RELEASE_MAINTENANCE_RESULT: FAIL\n" + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ApplyInternal()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Sprite contackSprite = RequireAsset<Sprite>(ContackSpritePath);
            Sprite angerSprite = RequireAsset<Sprite>(AngerSpritePath);
            Sprite anxietySprite = RequireAsset<Sprite>(AnxietySpritePath);
            Sprite childSprite = RequireSpriteSubAsset(ChildSpritePath, ChildSpriteName);
            Sprite motherSprite = RequireSpriteSubAsset(MotherSpritePath, MotherSpriteName);
            TMP_FontAsset japaneseFont = RequireAsset<TMP_FontAsset>(JapaneseFontAssetPath);

            OrganizeTopLevelHierarchy(scene);
            UpdateAngerBattle(scene, contackSprite, angerSprite);
            UpdateFuanBattle(scene, contackSprite, anxietySprite);
            UpdateBedFlight(scene, contackSprite);
            UpdateSadnessContent(scene, contackSprite, childSprite, motherSprite);
            RepairMissingFontReferences(scene, japaneseFont);
            ApplyInitialActiveState(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("SampleSceneの保存に失敗しました。");
            }

            DeleteObsoleteAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateReleaseScene(scene);
        }

        private static void OrganizeTopLevelHierarchy(Scene scene)
        {
            GameObject core = SceneHierarchyUtility.GetOrCreateGroup(scene, SceneHierarchyUtility.CoreGroupName);
            GameObject presentation = SceneHierarchyUtility.GetOrCreateGroup(scene, SceneHierarchyUtility.PresentationGroupName);
            GameObject minigames = SceneHierarchyUtility.GetOrCreateGroup(scene, SceneHierarchyUtility.MinigamesGroupName);
            GameObject world = SceneHierarchyUtility.GetOrCreateGroup(scene, SceneHierarchyUtility.WorldGroupName);

            foreach (string name in new[] { "Main Camera", "Global Light 2D", "Dialogue System", "MinigameLauncher" })
            {
                SceneHierarchyUtility.MoveUnderGroup(scene, SceneHierarchyUtility.Find(scene, name), core.name);
            }
            foreach (string name in new[] { "DialogueVisualsCanvas", "ClockGlitchIntro", "WakeGlitchIntro", "GoodMorningOutro" })
            {
                SceneHierarchyUtility.MoveUnderGroup(scene, SceneHierarchyUtility.Find(scene, name), presentation.name);
            }
            foreach (string name in new[] { "AngerBattleRoot", "FuanBattleRoot", "BedFlightRoot", "MemoryRecallRoot", "SadnessBattleRoot" })
            {
                SceneHierarchyUtility.MoveUnderGroup(scene, SceneHierarchyUtility.Find(scene, name), minigames.name);
            }
            SceneHierarchyUtility.MoveUnderGroup(scene, SceneHierarchyUtility.Find(scene, "SadnessMapEnvironment"), world.name);

            core.transform.SetSiblingIndex(0);
            presentation.transform.SetSiblingIndex(1);
            minigames.transform.SetSiblingIndex(2);
            world.transform.SetSiblingIndex(3);
        }

        private static void UpdateAngerBattle(Scene scene, Sprite contackSprite, Sprite angerSprite)
        {
            GameObject root = RequireSceneObject(scene, "AngerBattleRoot");
            AngerBattleController controller = RequireComponentInChildren<AngerBattleController>(root);

            controller.player.gameObject.name = "ContackPlayer";
            SetSprite(controller.player.gameObject, contackSprite);
            controller.playerBattleSprite = contackSprite;

            controller.enemy.gameObject.name = "AngerActor";
            SetSprite(controller.enemy.gameObject, angerSprite);
            controller.enemy.SetBattleSprite(angerSprite);
            controller.enemy.defeatedSprite = null;
            controller.enemyBattleSprite = angerSprite;
            EditorUtility.SetDirty(controller.enemy);

            RenameDirectChild(root, "BattleBackground", "Background");
            RenameDirectChild(root, "BGMPlayer", "Audio");
            RenameDirectChild(root, "BulletSpawnPoint", "PlayerShotOrigin");
            RenameDirectChild(root, "BattleUI", "DialogueUI");
            EditorUtility.SetDirty(controller);
        }

        private static void UpdateFuanBattle(Scene scene, Sprite contackSprite, Sprite anxietySprite)
        {
            GameObject root = RequireSceneObject(scene, "FuanBattleRoot");
            FuanBattleController controller = RequireComponentInChildren<FuanBattleController>(root);

            controller.player.gameObject.name = "ContackPlayer";
            SetSprite(controller.player.gameObject, contackSprite);
            controller.contackCharacterSprite = contackSprite;

            controller.enemy.gameObject.name = "AnxietyActor";
            SetSprite(controller.enemy.gameObject, anxietySprite);
            controller.enemy.SetBattleSprite(anxietySprite);
            controller.enemy.defeatedSprite = null;
            controller.anxietyCharacterSprite = anxietySprite;
            EditorUtility.SetDirty(controller.enemy);

            RenameDirectChild(root, "BattleBackground", "Background");
            RenameDirectChild(root, "BGMPlayer", "Audio");
            RenameDirectChild(root, "BulletSpawnPoint", "PlayerShotOrigin");
            RenameDirectChild(root, "BattleUI", "DialogueUI");
            EditorUtility.SetDirty(controller);
        }

        private static void UpdateBedFlight(Scene scene, Sprite contackSprite)
        {
            GameObject root = RequireSceneObject(scene, "BedFlightRoot");
            BedFlightController controller = RequireComponentInChildren<BedFlightController>(root);

            controller.player.gameObject.name = "FlyingBed";
            SpriteRenderer bedRenderer = controller.player.GetComponent<SpriteRenderer>();
            if (bedRenderer != null)
            {
                // BedFlightControllerが開始時に専用の矩形スプライトを生成する。
                bedRenderer.sprite = null;
                EditorUtility.SetDirty(bedRenderer);
            }

            controller.contac.gameObject.name = "Contack";
            controller.contac.sprite.sprite = contackSprite;
            EditorUtility.SetDirty(controller.contac.sprite);

            RenameDirectChild(root, "BGMPlayer", "Audio");
            RenameDirectChild(root, "BattleUI", "DialogueUI");
            EditorUtility.SetDirty(controller);
        }

        private static void UpdateSadnessContent(Scene scene, Sprite contackSprite, Sprite childSprite, Sprite motherSprite)
        {
            SadnessMapEnvironment environment = RequireComponent<SadnessMapEnvironment>(
                RequireSceneObject(scene, "SadnessMapEnvironment"));
            MemoryRecallController recall = RequireComponentInChildren<MemoryRecallController>(
                RequireSceneObject(scene, "MemoryRecallRoot"));
            SadnessBattleController battle = RequireComponentInChildren<SadnessBattleController>(
                RequireSceneObject(scene, "SadnessBattleRoot"));

            GameObject placedMother = SceneHierarchyUtility.Find(scene, MotherSpriteName);
            if (placedMother != null && placedMother.transform != recall.motherTransform)
            {
                environment.homeMotherSpot.position = placedMother.transform.position;
                UnityEngine.Object.DestroyImmediate(placedMother);
            }

            recall.player.gameObject.name = "ChildPlayer";
            SetSprite(recall.player.gameObject, childSprite);
            recall.motherTransform.gameObject.name = "MotherActor";
            recall.motherTransform.position = environment.homeMotherSpot.position;
            SetSprite(recall.motherTransform.gameObject, motherSprite);

            battle.player.gameObject.name = "ContackPlayer";
            SetSprite(battle.player.gameObject, contackSprite);
            for (int index = 0; index < battle.friendTargets.Length; index++)
            {
                battle.friendTargets[index].enemy.gameObject.name = $"FriendTarget{(char)('A' + index)}";
            }
            battle.motherTarget.enemy.gameObject.name = "MotherTarget";
            battle.motherTarget.enemy.transform.position = environment.homeMotherSpot.position;
            SetSprite(battle.motherTarget.enemy.gameObject, motherSprite);
            battle.motherTarget.enemy.SetBattleSprite(motherSprite);
            battle.motherTarget.enemy.defeatedSprite = null;
            EditorUtility.SetDirty(battle.motherTarget.enemy);
            battle.sadnessActor.name = "SadnessActor";

            RenameDirectChild(battle.transform.parent.gameObject, "BattleUI", "DialogueUI");
            EditorUtility.SetDirty(environment);
            EditorUtility.SetDirty(recall);
            EditorUtility.SetDirty(battle);
        }

        private static void ApplyInitialActiveState(Scene scene)
        {
            foreach (string name in new[] { "AngerBattleRoot", "FuanBattleRoot", "BedFlightRoot", "MemoryRecallRoot", "SadnessBattleRoot" })
            {
                RequireSceneObject(scene, name).SetActive(false);
            }
            foreach (string name in new[] { "ClockGlitchIntro", "WakeGlitchIntro", "GoodMorningOutro" })
            {
                RequireSceneObject(scene, name).SetActive(false);
            }

            SadnessMapEnvironment environment = RequireComponent<SadnessMapEnvironment>(
                RequireSceneObject(scene, "SadnessMapEnvironment"));
            environment.HideMaps();
        }

        private static void RepairMissingFontReferences(Scene scene, TMP_FontAsset japaneseFont)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font != null) continue;
                    text.font = japaneseFont;
                    EditorUtility.SetDirty(text);
                }
            }
        }

        private static void DeleteObsoleteAssets()
        {
            foreach (string assetPath in ObsoleteAssetPaths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null && !AssetDatabase.DeleteAsset(assetPath))
                {
                    throw new InvalidOperationException("旧アセットを削除できませんでした: " + assetPath);
                }
            }
        }

        private static void ValidateReleaseScene(Scene scene)
        {
            string[] expectedRoots =
            {
                SceneHierarchyUtility.CoreGroupName,
                SceneHierarchyUtility.PresentationGroupName,
                SceneHierarchyUtility.MinigamesGroupName,
                SceneHierarchyUtility.WorldGroupName,
            };
            string[] actualRoots = scene.GetRootGameObjects().Select(root => root.name).ToArray();
            string unexpectedRoot = actualRoots.FirstOrDefault(name => !expectedRoots.Contains(name));
            if (unexpectedRoot != null)
            {
                throw new InvalidOperationException("未整理のルートオブジェクトがあります: " + unexpectedRoot);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missingCount > 0)
                    {
                        throw new InvalidOperationException($"Missing Scriptがあります: {GetHierarchyPath(transform)} ({missingCount})");
                    }
                }

                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font == null)
                        throw new InvalidOperationException("フォント参照がありません: " + GetHierarchyPath(text.transform));
                }
            }

            foreach (string assetPath in ObsoleteAssetPaths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    throw new InvalidOperationException("旧アセットが残っています: " + assetPath);
                }
            }

            MinigameLauncher launcher = RequireComponent<MinigameLauncher>(RequireSceneObject(scene, "MinigameLauncher"));
            if (launcher.battleRoot == null || launcher.fuanBattleRoot == null || launcher.bedFlightRoot == null ||
                launcher.memoryRecallRoot == null || launcher.sadnessBattleRoot == null)
            {
                throw new InvalidOperationException("MinigameLauncherの必須ルート参照が不足しています。");
            }

            ValidateSprite(scene, "ContackPlayer", RequireAsset<Sprite>(ContackSpritePath));
            ValidateSprite(scene, "AngerActor", RequireAsset<Sprite>(AngerSpritePath));
            ValidateSprite(scene, "AnxietyActor", RequireAsset<Sprite>(AnxietySpritePath));
            ValidateSprite(scene, "ChildPlayer", RequireSpriteSubAsset(ChildSpritePath, ChildSpriteName));
            ValidateSprite(scene, "MotherActor", RequireSpriteSubAsset(MotherSpritePath, MotherSpriteName));
            ValidateSprite(scene, "MotherTarget", RequireSpriteSubAsset(MotherSpritePath, MotherSpriteName));

            TMP_FontAsset japaneseFont = RequireAsset<TMP_FontAsset>(JapaneseFontAssetPath);
            if (TMP_Settings.defaultFontAsset != japaneseFont)
                throw new InvalidOperationException("TMPの既定フォントがNotoSansJP SDFではありません。");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("検証後のSampleScene保存に失敗しました。");
            }
        }

        private static void RenameDirectChild(GameObject parent, string oldName, string newName)
        {
            Transform child = parent.transform.Find(oldName);
            if (child != null)
            {
                child.name = newName;
            }
        }

        private static void SetSprite(GameObject target, Sprite sprite)
        {
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException(target.name + " にSpriteRendererがありません。");
            }
            renderer.sprite = sprite;
            EditorUtility.SetDirty(renderer);
        }

        private static void ValidateSprite(Scene scene, string objectName, Sprite expected)
        {
            GameObject target = RequireSceneObject(scene, objectName);
            SpriteRenderer renderer = RequireComponent<SpriteRenderer>(target);
            if (renderer.sprite != expected)
            {
                throw new InvalidOperationException($"{objectName} のスプライトが現行素材ではありません。");
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private static GameObject RequireSceneObject(Scene scene, string name)
        {
            GameObject result = SceneHierarchyUtility.Find(scene, name);
            if (result == null) throw new InvalidOperationException("シーンオブジェクトが見つかりません: " + name);
            return result;
        }

        private static T RequireComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null) throw new InvalidOperationException($"{target.name} に {typeof(T).Name} がありません。");
            return component;
        }

        private static T RequireComponentInChildren<T>(GameObject target) where T : Component
        {
            T component = target.GetComponentInChildren<T>(true);
            if (component == null) throw new InvalidOperationException($"{target.name} の子に {typeof(T).Name} がありません。");
            return component;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("アセットが見つかりません: " + path);
            return asset;
        }

        private static Sprite RequireSpriteSubAsset(string path, string spriteName)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .FirstOrDefault(candidate => candidate.name == spriteName);
            if (sprite == null) throw new InvalidOperationException($"スプライトが見つかりません: {path} / {spriteName}");
            return sprite;
        }
    }
}
