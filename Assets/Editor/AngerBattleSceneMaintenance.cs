using System;
using AngerBattle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AngerBattle.EditorTools
{
    /// <summary>既存の怒り戦だけを壊さず、新しい縦型表示と編集可能HUDへ移行する。</summary>
    public static class AngerBattleSceneMaintenance
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PlayerSpritePath = "Assets/Resources/AngerBattle/ContackVertical.png";
        private const string EnemySpritePath = "Assets/Resources/AngerBattle/AngerVertical.png";

        public static void Apply()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                GameObject root = SceneHierarchyUtility.Find(scene, "AngerBattleRoot");
                if (root == null) throw new InvalidOperationException("AngerBattleRootが見つかりません。");

                AngerBattleController controller = root.GetComponentInChildren<AngerBattleController>(true);
                if (controller == null || controller.player == null || controller.enemy == null)
                {
                    throw new InvalidOperationException("怒り戦のController / Player / Enemy参照が不足しています。");
                }

                Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
                Sprite enemySprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemySpritePath);
                if (playerSprite == null || enemySprite == null)
                {
                    throw new InvalidOperationException("新しいコンタックまたは怒りの画像が見つかりません。");
                }

                controller.playerBattleSprite = playerSprite;
                controller.enemyBattleSprite = enemySprite;
                controller.playerBattleSpeed = 6.7f;
                controller.playerCollisionRadius = 0.20f;
                controller.standardEnemyBulletSpeed = 5.0f;
                controller.curtainBulletSpeed = 4.2f;
                controller.phaseShotIntervals = new[] { 0.90f, 0.78f, 0.95f, 0.55f };
                controller.curtainBulletSpacing = 1.05f;
                controller.curtainSafeGapHalfWidth = 1.45f;
                controller.curtainSafeGapRange = 2.4f;
                controller.curtainSafeGapStep = 0.38f;

                SpriteRenderer playerRenderer = controller.player.GetComponentInChildren<SpriteRenderer>(true);
                SpriteRenderer enemyRenderer = controller.enemy.GetComponentInChildren<SpriteRenderer>(true);
                if (playerRenderer == null || enemyRenderer == null)
                {
                    throw new InvalidOperationException("PlayerまたはEnemyのSpriteRendererが見つかりません。");
                }

                playerRenderer.sprite = playerSprite;
                playerRenderer.sortingOrder = 10;
                controller.player.transform.localPosition = controller.playerBattlePosition;
                controller.player.transform.localScale = controller.playerBattleScale;
                controller.player.speed = controller.playerBattleSpeed;

                controller.enemy.SetBattleSprite(enemySprite);
                enemyRenderer.sortingOrder = 10;
                controller.enemy.transform.localPosition = controller.enemyBattlePosition;
                controller.enemy.transform.localScale = controller.enemyBattleScale;

                Transform dialogueUi = root.transform.Find("BattleUI");
                if (dialogueUi != null) dialogueUi.name = "DialogueUI";

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child.name == "EnemyAura")
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }

                AngerBattleHUD[] huds = root.GetComponentsInChildren<AngerBattleHUD>(true);
                AngerBattleHUD hud = huds.Length > 0 ? huds[0] : null;
                for (int i = 1; i < huds.Length; i++)
                {
                    if (huds[i] != null) UnityEngine.Object.DestroyImmediate(huds[i].gameObject);
                }
                if (hud == null)
                {
                    GameObject hudObject = new GameObject("AngerBattleHUD", typeof(RectTransform), typeof(AngerBattleHUD));
                    hudObject.transform.SetParent(root.transform, false);
                    hud = hudObject.GetComponent<AngerBattleHUD>();
                }
                hud.gameObject.name = "AngerBattleHUD";
                hud.Build(controller.attackLineText);
                controller.hud = hud;

                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(controller.player);
                EditorUtility.SetDirty(controller.enemy);
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("SampleSceneの保存に失敗しました。");
                }

                AssetDatabase.SaveAssets();
                Debug.Log("ANGERBATTLE_MAINTENANCE_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("ANGERBATTLE_MAINTENANCE_RESULT: FAIL\n" + exception);
                EditorApplication.Exit(1);
            }
        }

    }
}
