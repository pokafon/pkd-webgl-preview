using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PKD.EditorTools
{
    /// <summary>
    /// 既存のDialogueVisualsに、暗転用Image・BGM/SE用AudioSourceを追加配線する。
    /// 既に配線済みなら何もしない（再実行しても安全）。
    /// </summary>
    public static class DialogueVisualsUpgradeTool
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string GirlfriendPortraitPath = "Assets/Sprites/ChatGPT Image 2026年8月22日 23_44_26.png";

        // 既存ミニゲームで既に使われている音源を、DialogueVisuals（現実パート）からも
        // <<bgm>>/<<se>>で流用できるよう登録する。同じAudioClipを複数の場所から
        // 参照しても問題ないため、新規音源は用意していない。
        private static readonly (string name, string path)[] BgmEntries =
        {
            ("TrickStyle", "Assets/Audio/Trick_style.mp3"),
            ("Wasurenagusa", "Assets/Audio/ワスレナグサ.mp3"),
            ("LakeWaltz", "Assets/Audio/湖面のワルツ.mp3"),
        };

        private static readonly (string name, string path)[] SeEntries =
        {
            ("ClockTick", "Assets/Audio/ClockSound.mp3"),
            ("Bell", "Assets/Audio/Bell_Accent03-1(Dry).mp3"),
            ("EveningChime", "Assets/Audio/夕焼け小焼け 防災行政無線チャイム 17時.mp3"),
            ("WhipCrack", "Assets/Audio/鞭を振り回す1.mp3"),
            ("RunWetRoad", "Assets/Audio/雨で濡れた道路を走る.mp3"),
            ("RunAsphalt", "Assets/Audio/アスファルトの上を走る1.mp3"),
        };

        [MenuItem("Tools/DialogueVisuals/Upgrade Scene")]
        public static void UpgradeFromMenu()
        {
            try
            {
                UpgradeInternal();
                EditorUtility.DisplayDialog("DialogueVisuals", "暗転・BGM・SEの配線を追加しました。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("DialogueVisuals", "更新に失敗しました。Consoleを確認してください。", "OK");
            }
        }

        public static void Upgrade()
        {
            try
            {
                UpgradeInternal();
                Debug.Log("DIALOGUEVISUALS_UPGRADE_RESULT: SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("DIALOGUEVISUALS_UPGRADE_RESULT: FAIL: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void UpgradeInternal()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var visuals = UnityEngine.Object.FindFirstObjectByType<DialogueVisuals>(FindObjectsInactive.Include);
            if (visuals == null)
            {
                throw new InvalidOperationException("シーン内にDialogueVisualsが見つかりません。");
            }

            EnsureBlackoutImage(scene, visuals);
            EnsureAudioSources(visuals);
            EnsureNamedPortrait(visuals, "Girlfriend", GirlfriendPortraitPath);

            foreach ((string name, string path) in BgmEntries)
            {
                EnsureNamedClip(visuals, isBgm: true, name, path);
            }
            foreach ((string name, string path) in SeEntries)
            {
                EnsureNamedClip(visuals, isBgm: false, name, path);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("シーンの保存に失敗しました。");
            }
        }

        private static void EnsureBlackoutImage(Scene scene, DialogueVisuals visuals)
        {
            if (visuals.blackoutImage != null) return;

            GameObject canvasGo = SceneHierarchyUtility.Find(scene, "BlackoutCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("BlackoutCanvas", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                SceneHierarchyUtility.MoveUnderGroup(scene, canvasGo, SceneHierarchyUtility.PresentationGroupName);

                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // 背景・立ち絵（DialogueVisualsCanvas、sortingOrder -10）より手前、
                // 会話テキストのCanvas（sortingOrder 0）より奥にする。
                // 暗転中もセリフだけは読める状態を保つため。
                canvas.sortingOrder = -1;

                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            Transform imageTransform = canvasGo.transform.Find("BlackoutImage");
            GameObject imageGo = imageTransform != null
                ? imageTransform.gameObject
                : new GameObject("BlackoutImage", typeof(RectTransform), typeof(Image));

            if (imageTransform == null)
            {
                imageGo.transform.SetParent(canvasGo.transform, false);
            }

            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageGo.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            // 未指定時は現状維持（＝暗転していない）がデフォルト。
            image.enabled = false;

            visuals.blackoutImage = image;
        }

        /// <summary>backgrounds/portraits配列に名前付きスプライトを追加・更新する（同名があれば差し替え）。</summary>
        private static void EnsureNamedPortrait(DialogueVisuals visuals, string name, string assetPath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("立ち絵のスプライトが読み込めません: " + assetPath);
            }

            List<DialogueVisuals.NamedSprite> portraits = (visuals.portraits ?? Array.Empty<DialogueVisuals.NamedSprite>()).ToList();
            int existingIndex = portraits.FindIndex(entry => entry.name == name);
            var updated = new DialogueVisuals.NamedSprite { name = name, sprite = sprite };

            if (existingIndex >= 0)
            {
                portraits[existingIndex] = updated;
            }
            else
            {
                portraits.Add(updated);
            }

            visuals.portraits = portraits.ToArray();
        }

        /// <summary>bgmClips/seClips配列に名前付き音源を追加・更新する（同名があれば差し替え）。</summary>
        private static void EnsureNamedClip(DialogueVisuals visuals, bool isBgm, string name, string assetPath)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                throw new InvalidOperationException("音源が読み込めません: " + assetPath);
            }

            List<DialogueVisuals.NamedClip> clips = ((isBgm ? visuals.bgmClips : visuals.seClips) ?? Array.Empty<DialogueVisuals.NamedClip>()).ToList();
            int existingIndex = clips.FindIndex(entry => entry.name == name);
            var updated = new DialogueVisuals.NamedClip { name = name, clip = clip };

            if (existingIndex >= 0)
            {
                clips[existingIndex] = updated;
            }
            else
            {
                clips.Add(updated);
            }

            if (isBgm) visuals.bgmClips = clips.ToArray();
            else visuals.seClips = clips.ToArray();
        }

        private static void EnsureAudioSources(DialogueVisuals visuals)
        {
            if (visuals.bgmSource == null)
            {
                var bgmSource = visuals.gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                visuals.bgmSource = bgmSource;
            }

            if (visuals.seSource == null)
            {
                var seSource = visuals.gameObject.AddComponent<AudioSource>();
                seSource.playOnAwake = false;
                seSource.loop = false;
                visuals.seSource = seSource;
            }
        }
    }
}
