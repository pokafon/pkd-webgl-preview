using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;

namespace AngerBattle.EditorTools
{
    /// <summary>
    /// 起動直後のタイトル画面／メインメニューを _Presentation 配下に構築する。
    /// 新規イラストは使わず、単色パネル＋TMPテキスト＋ボタンのみで構成する
    /// （既存のバトルHUDや導入演出と同じ、procedural UIの方針を踏襲）。
    ///
    /// 実行後、Dialogue System（DialogueRunner）の autoStart を false にし、
    /// タイトル画面の「はじめる」ボタンから明示的に StartDialogue("Prologue") を
    /// 呼び出す構成へ変更する。
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FontPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string TitleObjectName = "TitleScreen";

        [MenuItem("Tools/PKD/Build Title Screen")]
        public static void BuildFromMenu()
        {
            try
            {
                BuildInternal();
                EditorUtility.DisplayDialog("TitleScreen", "タイトル画面を構築しました。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("TitleScreen", "構築に失敗しました。Consoleを確認してください。", "OK");
            }
        }

        public static void Build()
        {
            try
            {
                BuildInternal();
                Debug.Log("TITLESCREEN_BUILD_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("TITLESCREEN_BUILD_RESULT: FAIL: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildInternal()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            TMP_FontAsset font = RequireAsset<TMP_FontAsset>(FontPath);

            SceneHierarchyUtility.DestroyNamedObject(scene, TitleObjectName);

            GameObject canvasObject = new GameObject(TitleObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 他のUI・演出より必ず手前に描画する

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem(scene);

            GameObject rootObject = CreateUIObject("Root", canvasObject.transform, typeof(CanvasGroup), typeof(Image));
            Stretch(rootObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            CanvasGroup rootGroup = rootObject.GetComponent<CanvasGroup>();
            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.08f, 1f);

            TextMeshProUGUI title = CreateText(rootObject.transform, "TitleText", font, new Vector2(0.15f, 0.58f), new Vector2(0.85f, 0.82f), 96f, TextAlignmentOptions.Center);
            title.text = "PKD";
            title.color = Color.white;
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI subtitle = CreateText(rootObject.transform, "SubtitleText", font, new Vector2(0.15f, 0.50f), new Vector2(0.85f, 0.58f), 28f, TextAlignmentOptions.Center);
            subtitle.text = "（仮タイトル）";
            subtitle.color = new Color(0.7f, 0.7f, 0.75f, 1f);

            Button startButton = CreateButton(rootObject.transform, "StartButton", "はじめる", font, new Vector2(0.40f, 0.32f), new Vector2(0.60f, 0.40f));
            Button quitButton = CreateButton(rootObject.transform, "QuitButton", "終了", font, new Vector2(0.42f, 0.20f), new Vector2(0.58f, 0.27f));

            TitleScreenController controller = canvasObject.AddComponent<TitleScreenController>();
            controller.titleCanvasGroup = rootGroup;
            controller.startButton = startButton;
            controller.quitButton = quitButton;

            SceneHierarchyUtility.MoveUnderGroup(scene, canvasObject, SceneHierarchyUtility.PresentationGroupName);
            canvasObject.SetActive(true);

            DisableDialogueAutoStart(scene);

            Validate(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new Exception("シーンの保存に失敗しました: " + ScenePath);
            }
            AssetDatabase.SaveAssets();
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null) return;

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            SceneHierarchyUtility.MoveUnderGroup(scene, eventSystemObject, SceneHierarchyUtility.CoreGroupName);
        }

        private static void DisableDialogueAutoStart(Scene scene)
        {
            GameObject dialogueSystem = SceneHierarchyUtility.Find(scene, "Dialogue System");
            if (dialogueSystem == null)
            {
                throw new Exception("シーン内に「Dialogue System」が見つかりません。");
            }
            DialogueRunner runner = dialogueSystem.GetComponent<DialogueRunner>();
            if (runner == null)
            {
                throw new Exception("Dialogue SystemにDialogueRunnerが見つかりません。");
            }
            runner.autoStart = false;
            EditorUtility.SetDirty(runner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(runner);
        }

        private static GameObject CreateUIObject(string name, Transform parent, params Type[] components)
        {
            GameObject obj = new GameObject(name, components);
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            TMP_FontAsset font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUIObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
            Stretch(textObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject obj = CreateUIObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
            Stretch(obj.GetComponent<RectTransform>(), anchorMin, anchorMax);

            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.20f, 0.20f, 0.26f, 0.96f);

            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.2f, 1f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.85f, 1f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            TextMeshProUGUI labelText = CreateText(obj.transform, "Label", font, Vector2.zero, Vector2.one, 32f, TextAlignmentOptions.Center);
            labelText.text = label;
            labelText.fontStyle = FontStyles.Bold;

            return button;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new Exception("アセットが見つかりません: " + path);
            return asset;
        }

        private static void Validate(TitleScreenController controller)
        {
            if (controller.titleCanvasGroup == null || controller.startButton == null || controller.quitButton == null)
            {
                throw new Exception("TitleScreenControllerの必須参照が不足しています。");
            }
        }
    }
}
