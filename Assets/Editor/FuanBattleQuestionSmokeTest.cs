using System;
using System.Collections.Generic;
using System.Reflection;
using AngerBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AngerBattle.EditorTools
{
    /// <summary>
    /// 不安戦の参照と、質問UIを実行時生成できることを検証する軽量スモークテスト。
    /// Play Modeへ入らないため、レイアウト構築の破損を短時間で検出できる。
    /// </summary>
    public static class FuanBattleQuestionSmokeTest
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        public static void Run()
        {
            GameObject testRoot = null;
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                FuanBattleController controller = FindSceneController();
                ValidateController(controller);

                testRoot = new GameObject("__FuanQuestionSmokeTest");
                GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObject.transform.SetParent(testRoot.transform, false);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                GameObject experienceObject = new GameObject("Experience", typeof(AnxietyQuestionExperience));
                experienceObject.transform.SetParent(testRoot.transform, false);
                AnxietyQuestionExperience experience = experienceObject.GetComponent<AnxietyQuestionExperience>();
                experience.Configure(
                    canvas,
                    controller.attackLineText.font,
                    1f,
                    0.1f,
                    0.1f,
                    0.1f,
                    controller.questionFloorSprite,
                    controller.yesGateSprite,
                    controller.noGateSprite,
                    controller.questionGateMaterial,
                    controller.questionLeftFootSprite,
                    controller.questionRightFootSprite,
                    controller.questionCamera,
                    controller.questionDiveDuration,
                    controller.questionDiveOrthographicSize,
                    controller.questionRainEnabled,
                    controller.questionRainRate,
                    controller.questionRainMaterial,
                    controller.questionRunningWetRoadClip,
                    controller.questionRunningWetRoadVolume);
                Sprite anxietySprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Resources/AngerBattle/AnxietyVertical.png");
                Sprite contackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Resources/AngerBattle/ContackVertical.png");
                if (anxietySprite == null || contackSprite == null)
                {
                    throw new Exception("不安またはコンタックの実画像がSpriteとして読み込めません。");
                }
                experience.PrepareChaseOpening(anxietySprite, contackSprite, controller.openingLayout);
                experience.ConfigureFallStage(
                    controller.player,
                    controller.fallStageCamera != null ? controller.fallStageCamera : controller.questionCamera,
                    controller.attackLineText.font,
                    controller.fallStageDescendSpeed,
                    controller.fallStageMoveSpeed,
                    controller.fallStageSlowDuration,
                    controller.fallStageSlowCooldown,
                    controller.fallStageSlowFactor,
                    controller.fallStageDistance,
                    controller.fallStageCorridorHalfWidth);

                Transform generatedRoot = canvas.transform.Find("AnxietyQuestionExperience");
                if (generatedRoot == null)
                {
                    throw new Exception("質問UIのルートが生成されませんでした。");
                }
                // 地上のクリック選択（YES／NOボタン・水面クリック領域）は廃止し、
                // WASD移動＋落下ステージのYES／NO分岐へ置き換えたため、UI上のButtonはもう生成しない。
                Button[] answerButtons = generatedRoot.GetComponentsInChildren<Button>(true);
                if (answerButtons.Length != 0)
                {
                    throw new Exception("地上のクリック選択ボタンが廃止されず残っています。");
                }

                FallStageController fallStage = experienceObject.GetComponent<FallStageController>();
                if (fallStage == null || !fallStage.IsReady)
                {
                    throw new Exception("落下ステージ(FallStageController)が構成されていません。");
                }

                Component worldVisualsForHole = null;
                foreach (Component component in experienceObject.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "AnxietyQuestionWorldVisuals")
                    {
                        worldVisualsForHole = component;
                        break;
                    }
                }
                PropertyInfo questionCameraProperty = worldVisualsForHole != null
                    ? worldVisualsForHole.GetType().GetProperty("QuestionCamera", BindingFlags.Instance | BindingFlags.Public)
                    : null;
                object resolvedQuestionCamera = questionCameraProperty?.GetValue(worldVisualsForHole);
                if (worldVisualsForHole == null || resolvedQuestionCamera == null)
                {
                    throw new Exception("地上の穴の位置計算に必要なワールド演出／カメラ参照が構成されていません。");
                }

                Image questionBackground = generatedRoot.GetComponent<Image>();
                if (questionBackground == null || questionBackground.color.a < 0.12f)
                {
                    throw new Exception("質問画面を夜色へ締めるカラーグレーディングが不足しています。");
                }
                if (generatedRoot.Find("QuestionContent") == null)
                {
                    throw new Exception("暗幕と別にフェードする質問内容ルートが生成されていません。");
                }
                if (generatedRoot.Find("QuestionContent/QuestionStage/QuestionPanel") == null)
                {
                    throw new Exception("入口と文字を分離する上部質問パネルが生成されていません。");
                }
                Transform eyebrow = generatedRoot.Find("QuestionContent/Eyebrow");
                Transform doubt = generatedRoot.Find("QuestionContent/QuestionStage/Doubt");
                if (eyebrow == null || eyebrow.gameObject.activeSelf || doubt == null || doubt.gameObject.activeSelf)
                {
                    throw new Exception("上部質問パネルが質問文だけの構成になっていません。");
                }
                Transform history = generatedRoot.Find("QuestionContent/HistoryPanel");
                Transform intrusive = generatedRoot.Find("QuestionContent/IntrusiveThoughts");
                if (history == null || history.gameObject.activeSelf || intrusive == null || intrusive.gameObject.activeSelf)
                {
                    throw new Exception("不要な回答履歴または不安の思考文が表示対象に残っています。");
                }
                // 足跡UI（FootstepProgress）は地上のクリック選択と一緒に廃止し、ワールドアート版では生成しない。
                // 足跡の経路計算ロジック自体は将来の転用に備えて残しているため、データ構造だけ検証する。
                Transform footsteps = generatedRoot.Find("QuestionContent/QuestionStage/FootstepProgress");
                if (footsteps != null)
                {
                    throw new Exception("廃止したはずの足跡UIが残っています。");
                }
                ValidateFootstepRoute(experience);
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    throw new Exception("マウス入力に必要なGraphicRaycasterが生成されていません。");
                }
                if (testRoot.GetComponentInChildren<EventSystem>(true) == null)
                {
                    throw new Exception("ボタン入力に必要なEventSystemが生成されていません。");
                }
                if (testRoot.GetComponentsInChildren<SpriteRenderer>(true).Length < 8)
                {
                    throw new Exception("追跡用の床・二人と、質問用の床・YES入口・NO入口が生成されていません。");
                }
                SpriteRenderer yesGateRenderer = null;
                SpriteRenderer noGateRenderer = null;
                SpriteRenderer anxietyActorRenderer = null;
                SpriteRenderer contackActorRenderer = null;
                foreach (SpriteRenderer spriteRenderer in testRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (spriteRenderer.name == "YesGate")
                    {
                        yesGateRenderer = spriteRenderer;
                    }
                    else if (spriteRenderer.name == "NoGate")
                    {
                        noGateRenderer = spriteRenderer;
                    }
                    else if (spriteRenderer.name == "AnxietyActor")
                    {
                        anxietyActorRenderer = spriteRenderer;
                    }
                    else if (spriteRenderer.name == "ContackActor")
                    {
                        contackActorRenderer = spriteRenderer;
                    }
                }
                if (anxietyActorRenderer == null || anxietyActorRenderer.sprite != anxietySprite
                    || contackActorRenderer == null || contackActorRenderer.sprite != contackSprite)
                {
                    throw new Exception("冒頭の追跡演出に不安とコンタックの実画像が割り当てられていません。");
                }
                if (!anxietyActorRenderer.gameObject.activeSelf || !contackActorRenderer.gameObject.activeSelf
                    || yesGateRenderer == null || yesGateRenderer.gameObject.activeSelf
                    || noGateRenderer == null || noGateRenderer.gameObject.activeSelf)
                {
                    throw new Exception("冒頭で二人だけを見せる前に、質問エリアが映り込んでいます。");
                }
                ValidateOpeningExitLayout(controller);
                if (yesGateRenderer == null || noGateRenderer == null
                    || yesGateRenderer.sharedMaterial == null || noGateRenderer.sharedMaterial == null
                    || yesGateRenderer.sharedMaterial.shader == null || noGateRenderer.sharedMaterial.shader == null
                    || yesGateRenderer.sharedMaterial.shader.name != "PKD/AnxietyGateAlphaRemap"
                    || noGateRenderer.sharedMaterial.shader.name != "PKD/AnxietyGateAlphaRemap")
                {
                    throw new Exception("入口画像の薄い全画面レイヤーを除去する透過補正が適用されていません。");
                }
                if (yesGateRenderer.sharedMaterial == noGateRenderer.sharedMaterial
                    || yesGateRenderer.sharedMaterial.mainTexture != yesGateRenderer.sprite.texture
                    || noGateRenderer.sharedMaterial.mainTexture != noGateRenderer.sprite.texture)
                {
                    throw new Exception("YES / NO入口が別々の画像を描画する専用マテリアルになっていません。");
                }
                ValidateFaceOffFloor(experienceObject, yesGateRenderer, noGateRenderer);
                if (testRoot.GetComponentsInChildren<LineRenderer>(true).Length != 0)
                {
                    throw new Exception("水面ホバーに不要な丸い輪郭が残っています。");
                }
                ParticleSystem[] rainLayers = testRoot.GetComponentsInChildren<ParticleSystem>(true);
                if (rainLayers.Length != 2)
                {
                    throw new Exception("明部・暗部の両方で見える二層の雨が生成されていません。");
                }
                ParticleSystem rain = null;
                ParticleSystem rainContrast = null;
                foreach (ParticleSystem layer in rainLayers)
                {
                    if (layer.name == "Rain") rain = layer;
                    if (layer.name == "RainContrast") rainContrast = layer;
                }
                if (rain == null || rainContrast == null)
                {
                    throw new Exception("明るい雨筋または暗青灰色のコントラスト雨が不足しています。");
                }
                ParticleSystemRenderer rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
                if (rainRenderer == null || rainRenderer.renderMode != ParticleSystemRenderMode.Stretch)
                {
                    throw new Exception("雨粒が細長いストレッチ描画になっていません。");
                }
                ParticleSystem.MainModule rainMain = rain.main;
                ParticleSystem.EmissionModule rainEmission = rain.emission;
                ParticleSystem.VelocityOverLifetimeModule rainVelocity = rain.velocityOverLifetime;
                if (rainMain.maxParticles < 200 || rainEmission.rateOverTime.constant < 60f || !rainVelocity.enabled)
                {
                    throw new Exception("雨の粒子数・発生量・落下速度が視認可能な設定になっていません。");
                }
                if (rainVelocity.x.mode != rainVelocity.y.mode || rainVelocity.y.mode != rainVelocity.z.mode)
                {
                    throw new Exception("雨のVelocity Curve Modeが統一されていません。");
                }
                if (rainRenderer.sharedMaterial == null || !rainRenderer.sharedMaterial.name.Contains("Cinematic Weather"))
                {
                    throw new Exception("Cinematic Weatherの雨粒マテリアルが適用されていません。");
                }
                ParticleSystemRenderer contrastRenderer = rainContrast.GetComponent<ParticleSystemRenderer>();
                ParticleSystem.VelocityOverLifetimeModule contrastVelocity = rainContrast.velocityOverLifetime;
                if (contrastRenderer == null
                    || contrastRenderer.renderMode != ParticleSystemRenderMode.Stretch
                    || contrastRenderer.sharedMaterial == null
                    || !contrastRenderer.sharedMaterial.name.Contains("Contrast")
                    || !contrastVelocity.enabled
                    || contrastVelocity.x.mode != contrastVelocity.y.mode
                    || contrastVelocity.y.mode != contrastVelocity.z.mode)
                {
                    throw new Exception("石畳上で雨を見せる暗青灰色レイヤーが正しく構成されていません。");
                }
                AudioSource runningAudio = testRoot.GetComponentInChildren<AudioSource>(true);
                if (runningAudio == null
                    || runningAudio.clip != controller.questionRunningWetRoadClip
                    || !runningAudio.loop
                    || runningAudio.volume < 0.45f
                    || runningAudio.playOnAwake)
                {
                    throw new Exception("選択時にはっきり聞こえるアスファルト上の足音が構成されていません。");
                }
                if (AssetDatabase.GetAssetPath(controller.questionRunningWetRoadClip) != "Assets/Audio/アスファルトの上を走る1.mp3")
                {
                    throw new Exception("選択時の足音が指定されたアスファルト走行音へ差し替わっていません。");
                }

                Debug.Log("FUANBATTLE_QUESTION_SMOKETEST_RESULT: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("FUANBATTLE_QUESTION_SMOKETEST_RESULT: FAIL: " + ex);
                EditorApplication.Exit(1);
            }
            finally
            {
                if (testRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(testRoot);
                }
            }
        }

        private static FuanBattleController FindSceneController()
        {
            foreach (FuanBattleController controller in Resources.FindObjectsOfTypeAll<FuanBattleController>())
            {
                if (controller.gameObject.scene.IsValid())
                {
                    return controller;
                }
            }
            throw new Exception("SampleSceneにFuanBattleControllerが見つかりません。");
        }

        private static void ValidateFootstepRoute(AnxietyQuestionExperience experience)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo append = typeof(AnxietyQuestionExperience).GetMethod("AppendFootstepSegment", flags);
            FieldInfo centersField = typeof(AnxietyQuestionExperience).GetField("footstepPathCenters", flags);
            FieldInfo anglesField = typeof(AnxietyQuestionExperience).GetField("footstepPathAngles", flags);
            FieldInfo segmentsField = typeof(AnxietyQuestionExperience).GetField("footstepPathSegments", flags);
            if (append == null || centersField == null || anglesField == null || segmentsField == null)
            {
                throw new Exception("足跡経路の生成状態を検証できません。");
            }

            var centers = centersField.GetValue(experience) as List<Vector2>;
            var angles = anglesField.GetValue(experience) as List<float>;
            var segments = segmentsField.GetValue(experience) as List<int>;
            string[] routeAnswers = { "YES", "NO", "YES", "NO", "YES" };

            for (int answerIndex = 0; answerIndex < routeAnswers.Length; answerIndex++)
            {
                int before = centers != null ? centers.Count : 0;
                append.Invoke(experience, new object[] { routeAnswers[answerIndex], answerIndex });
                int after = centers != null ? centers.Count : 0;
                if (after - before != 4)
                {
                    throw new Exception("1経路が4歩の直線として生成されていません。");
                }

                Vector2 end = centers[after - 1];
                float expectedX = routeAnswers[answerIndex] == "YES" ? 0.295f : 0.705f;
                if (Mathf.Abs(end.x - expectedX) > 0.002f)
                {
                    throw new Exception("足跡が選択したYES／NO側へ一直線に到達していません。");
                }

                Vector2 firstDelta = centers[before + 1] - centers[before];
                for (int i = before + 2; i < after; i++)
                {
                    Vector2 delta = centers[i] - centers[i - 1];
                    if (Vector2.Angle(firstDelta, delta) > 0.1f)
                    {
                        throw new Exception("足跡経路に右往左往する曲がりが残っています。");
                    }
                }

                append.Invoke(experience, new object[] { routeAnswers[answerIndex], answerIndex });
                if (centers.Count != after)
                {
                    throw new Exception("同じ回答を再び通った時に足跡が重複生成されています。");
                }
            }

            int beforeAlternate = centers.Count;
            append.Invoke(experience, new object[] { "NO", 0 });
            if (centers.Count - beforeAlternate != 4)
            {
                throw new Exception("同じ問題の反対側を試した履歴が、別の道標として残りません。");
            }

            if (centers == null || angles == null || segments == null
                || centers.Count != angles.Count || centers.Count != segments.Count)
            {
                throw new Exception("足跡の位置・角度・回答履歴の対応が壊れています。");
            }
            for (int i = 0; i < centers.Count; i++)
            {
                if (Mathf.Abs(angles[i]) > 48.01f)
                {
                    throw new Exception("足跡が横倒しになる角度で生成されています。");
                }
            }
        }

        private static void ValidateOpeningExitLayout(FuanBattleController controller)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            Type visualsType = typeof(AnxietyQuestionExperience).Assembly.GetType(
                "AngerBattle.AnxietyQuestionWorldVisuals");
            MethodInfo method = visualsType?.GetMethod("GetFullyOffscreenAnxietyViewport", flags);
            if (method == null)
            {
                throw new Exception("不安を完全に画面外へ退場させる計算を検証できません。");
            }

            Vector2 viewport = (Vector2)method.Invoke(null, new object[] { controller.openingLayout });
            float requiredY = 1f + controller.openingLayout.anxietyScreenHeight * 0.5f;
            if (viewport.y <= requiredY)
            {
                throw new Exception("不安の退場位置に画像下端が残ります。");
            }
            if (controller.openingChaseDuration <= 0f
                || controller.openingAfterExitHoldSeconds <= 0f
                || controller.openingScrollDuration <= 0f)
            {
                throw new Exception("不安の退場・間・スクロールの時間がInspector調整可能な正値になっていません。");
            }
        }

        private static void ValidateFaceOffFloor(
            GameObject experienceObject,
            SpriteRenderer yesGateRenderer,
            SpriteRenderer noGateRenderer)
        {
            Component worldVisuals = null;
            foreach (Component component in experienceObject.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "AnxietyQuestionWorldVisuals")
                {
                    worldVisuals = component;
                    break;
                }
            }
            MethodInfo prepareFaceOff = worldVisuals != null
                ? worldVisuals.GetType().GetMethod("PrepareFaceOffBackground", BindingFlags.Instance | BindingFlags.Public)
                : null;
            if (prepareFaceOff == null)
            {
                throw new Exception("締め用の床表示を検証できません。");
            }

            prepareFaceOff.Invoke(worldVisuals, null);
            if (yesGateRenderer.gameObject.activeSelf || noGateRenderer.gameObject.activeSelf)
            {
                throw new Exception("締めでYES／NOの穴が床に残っています。");
            }
        }

        private static void ValidateController(FuanBattleController controller)
        {
            if (controller.player == null || controller.enemy == null || controller.bgm == null)
            {
                throw new Exception("FuanBattleControllerの戦闘参照が不足しています。");
            }
            if (controller.denialBulletPrefab == null || controller.attackLineText == null)
            {
                throw new Exception("FuanBattleControllerの弾またはUI参照が不足しています。");
            }
            if (controller.attackLineText.font == null)
            {
                throw new Exception("不安戦UIのTMPフォントが未設定です。");
            }
            if (controller.questions == null || controller.questions.Length != 5)
            {
                throw new Exception("不安戦の質問が5問になっていません。");
            }
            if (controller.openingLayout == null || controller.faceOffLayout == null)
            {
                throw new Exception("冒頭または締めの位置調整値がInspectorへ公開されていません。");
            }
            if (controller.openingLayout.anxietyStartViewport.y
                - controller.openingLayout.contackStartViewport.y < 0.4f)
            {
                throw new Exception("冒頭の不安とコンタックの初期距離が近すぎます。");
            }
            if (controller.openingLayout.scrollScreens < 0.1f)
            {
                throw new Exception("停止したコンタックが画面下へ抜けるスクロール量が不足しています。");
            }
            if (controller.intrusiveLines == null || controller.intrusiveLines.Length < 5)
            {
                throw new Exception("不安台詞が5本設定されていません。");
            }
            string[] expectedRoute = { "YES", "NO", "YES", "NO", "YES" };
            if (controller.correctAnswers == null || controller.correctAnswers.Length != expectedRoute.Length)
            {
                throw new Exception("不安戦の仮の正解順が5問分設定されていません。");
            }
            for (int i = 0; i < expectedRoute.Length; i++)
            {
                if (!string.Equals(controller.correctAnswers[i], expectedRoute[i], StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("仮の正解順がYES→NO→YES→NO→YESになっていません。");
                }
            }
            if (controller.questionFloorSprite == null || controller.yesGateSprite == null || controller.noGateSprite == null)
            {
                throw new Exception("不安質問用の床・YES入口・NO入口画像が未設定です。");
            }
            if (controller.questionGateMaterial == null
                || controller.questionLeftFootSprite == null
                || controller.questionRightFootSprite == null)
            {
                throw new Exception("入口透過補正または足跡素材が未設定です。");
            }
            if (controller.questionCamera == null)
            {
                throw new Exception("不安質問のカメラ侵入演出用Cameraが未設定です。");
            }
            if (controller.questionDiveOrthographicSize > 1f)
            {
                throw new Exception("入口が画面を覆う深さまでカメラが寄る設定になっていません。");
            }
            if (!controller.questionRainEnabled || controller.questionRainRate < 60f)
            {
                throw new Exception("不安質問の雨が有効、かつ視認可能な雨量に設定されていません。");
            }
            if (controller.questionRainMaterial == null || controller.questionRunningWetRoadClip == null)
            {
                throw new Exception("雨マテリアルまたはアスファルト上の走行音が未設定です。");
            }
            if (controller.fallStageDistance <= 0f || controller.fallStageCorridorHalfWidth <= 0f
                || controller.fallStageDescendSpeed <= 0f || controller.fallStageMoveSpeed <= 0f)
            {
                throw new Exception("落下ステージの距離・幅・速度がInspectorで正値になっていません。");
            }
            if (controller.fallStageSlowDuration <= 0f || controller.fallStageSlowCooldown < 0f
                || controller.fallStageSlowFactor <= 0f || controller.fallStageSlowFactor >= 1f)
            {
                throw new Exception("落下ステージの仮スロー継続秒数・クールタイム・倍率が想定範囲外です。");
            }
        }
    }
}
