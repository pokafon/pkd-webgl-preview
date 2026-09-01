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

                Transform generatedRoot = canvas.transform.Find("AnxietyQuestionExperience");
                if (generatedRoot == null)
                {
                    throw new Exception("質問UIのルートが生成されませんでした。");
                }
                Button[] answerButtons = generatedRoot.GetComponentsInChildren<Button>(true);
                if (answerButtons.Length != 4)
                {
                    throw new Exception("YES / NOの選択プレートと水面ボタンが計4個生成されていません。");
                }

                Button[] plateButtons =
                {
                    RequireButton(generatedRoot, "QuestionContent/QuestionStage/YESButton"),
                    RequireButton(generatedRoot, "QuestionContent/QuestionStage/NOButton")
                };
                foreach (Button button in plateButtons)
                {
                    Image answerImage = button.targetGraphic as Image;
                    if (answerImage == null || answerImage.color.a < 0.4f)
                    {
                        throw new Exception("YES / NOの選択プレートが表示状態になっていません。");
                    }
                    if (button.GetComponent<Outline>() == null)
                    {
                        throw new Exception("YES / NOの選択プレートに細い輪郭がありません。");
                    }
                    if (button.GetComponent<RectTransform>().anchorMax.y > 0.45f)
                    {
                        throw new Exception("YES / NOの選択プレートが水面の下へ配置されていません。");
                    }
                    if (HasComponentNamed(button.gameObject, "EllipseRaycastFilter"))
                    {
                        throw new Exception("選択プレートが楕円クリック判定のままです。");
                    }
                }

                Button[] puddleButtons =
                {
                    RequireButton(generatedRoot, "QuestionContent/QuestionStage/YESPuddleButton"),
                    RequireButton(generatedRoot, "QuestionContent/QuestionStage/NOPuddleButton")
                };
                foreach (Button button in puddleButtons)
                {
                    Image hitArea = button.targetGraphic as Image;
                    if (hitArea == null || hitArea.color.a > 0.01f)
                    {
                        throw new Exception("水面クリック領域が透明になっていません。");
                    }
                    if (!HasComponentNamed(button.gameObject, "EllipseRaycastFilter"))
                    {
                        throw new Exception("水面クリック領域が楕円に限定されていません。");
                    }
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
                Transform footsteps = generatedRoot.Find("QuestionContent/QuestionStage/FootstepProgress");
                Image[] footstepImages = footsteps != null ? footsteps.GetComponentsInChildren<Image>(true) : Array.Empty<Image>();
                if (footsteps == null || footstepImages.Length != 42)
                {
                    throw new Exception("初期の両足と、回答方向へ歩く連続経路用の足跡が生成されていません。");
                }
                int visibleFootsteps = 0;
                foreach (Image footstep in footstepImages)
                {
                    if (footstep.gameObject.activeSelf)
                    {
                        visibleFootsteps++;
                    }
                }
                if (visibleFootsteps != 2)
                {
                    throw new Exception("未回答の足跡が先に表示されています。");
                }
                if (footsteps.GetSiblingIndex() != 0)
                {
                    throw new Exception("足跡経路が質問や選択プレートより手前に重なっています。");
                }
                ValidateFootstepRoute(experience);
                ValidateQuestionScopedFootsteps(experience);
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

        private static void ValidateQuestionScopedFootsteps(AnxietyQuestionExperience experience)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo currentQuestionField = typeof(AnxietyQuestionExperience).GetField("currentQuestionIndex", flags);
            FieldInfo visibleCountField = typeof(AnxietyQuestionExperience).GetField("visibleFootstepCount", flags);
            FieldInfo centersField = typeof(AnxietyQuestionExperience).GetField("footstepPathCenters", flags);
            FieldInfo segmentsField = typeof(AnxietyQuestionExperience).GetField("footstepPathSegments", flags);
            FieldInfo imagesField = typeof(AnxietyQuestionExperience).GetField("footstepImages", flags);
            MethodInfo animate = typeof(AnxietyQuestionExperience).GetMethod("AnimateFootsteps", flags);
            if (currentQuestionField == null || visibleCountField == null || centersField == null
                || segmentsField == null || imagesField == null || animate == null)
            {
                throw new Exception("問題別の足跡表示状態を検証できません。");
            }

            var centers = centersField.GetValue(experience) as List<Vector2>;
            var segments = segmentsField.GetValue(experience) as List<int>;
            var images = imagesField.GetValue(experience) as Image[];
            visibleCountField.SetValue(experience, centers != null ? centers.Count : 0);
            currentQuestionField.SetValue(experience, 1);
            animate.Invoke(experience, null);

            int activeCount = 0;
            for (int i = 0; images != null && i < images.Length; i++)
            {
                if (images[i] == null || !images[i].gameObject.activeSelf)
                {
                    continue;
                }
                activeCount++;
                if (segments == null || i >= segments.Count || segments[i] != 1)
                {
                    throw new Exception("2問目に進んだ時、別の問題の足跡が残っています。");
                }
            }
            if (activeCount != 4)
            {
                throw new Exception("現在の問題に対応する足跡だけが表示されていません。");
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

        private static Button RequireButton(Transform root, string path)
        {
            Transform target = root.Find(path);
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                throw new Exception(path + " が生成されていません。");
            }
            return button;
        }

        private static bool HasComponentNamed(GameObject target, string typeName)
        {
            foreach (MonoBehaviour component in target.GetComponents<MonoBehaviour>())
            {
                if (component != null && component.GetType().Name == typeName)
                {
                    return true;
                }
            }
            return false;
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
        }
    }
}
