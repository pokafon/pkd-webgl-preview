using System;
using System.IO;
using System.Linq;
using MemoryRecall;
using SadnessBattle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SadnessImplementationSmokeTest
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            SadnessMapEnvironment environment = Resources.FindObjectsOfTypeAll<SadnessMapEnvironment>()
                .FirstOrDefault(item => item.gameObject.scene.IsValid());
            Require(environment != null, "SadnessMapEnvironment exists");
            Require(environment.outdoorGrid != null && environment.outdoorGrid.name == "SadnessOutdoorGrid", "outdoor Grid is wired and renamed");
            Require(environment.homeGrid != null && environment.homeGrid.name == "SadnessHomeGrid", "home Grid is wired and renamed");
            Require(environment.outdoorFriendSpots != null && environment.outdoorFriendSpots.Length == 3, "three outdoor friend markers exist");
            Require(environment.outdoorDoor != null && environment.homeDoor != null, "door markers exist");
            Require(!environment.outdoorGrid.activeSelf && !environment.homeGrid.activeSelf, "both maps start hidden");

            AngerBattle.MinigameLauncher launcher = Resources.FindObjectsOfTypeAll<AngerBattle.MinigameLauncher>()
                .FirstOrDefault(item => item.gameObject.scene.IsValid());
            Require(launcher != null, "MinigameLauncher exists");
            Require(launcher.sadnessMapEnvironment == environment, "shared map is wired to launcher");

            MemoryRecallController recall = Resources.FindObjectsOfTypeAll<MemoryRecallController>()
                .FirstOrDefault(item => item.gameObject.scene.IsValid());
            Require(recall != null && recall.mapEnvironment == environment, "MemoryRecallController uses shared map");
            Require(recall.friends != null && recall.friends.Length == 3 && recall.friends.All(friend => friend.npcTransform != null), "memory recall has three friends");
            Require(recall.motherTransform != null && recall.eveningChimeClip != null, "memory recall mother and chime are wired");

            SadnessBattleController battle = Resources.FindObjectsOfTypeAll<SadnessBattleController>()
                .FirstOrDefault(item => item.gameObject.scene.IsValid());
            Require(battle != null && battle.mapEnvironment == environment, "SadnessBattleController uses shared map");
            Require(battle.friendTargets != null && battle.friendTargets.Length == 3 && battle.friendTargets.All(target => target.enemy != null), "battle has three friend targets");
            Require(battle.motherTarget != null && battle.motherTarget.enemy != null, "battle mother target is wired");
            Require(battle.sadnessActor != null && battle.eveningChimeClip != null, "sadness actor and chime are wired");
            Require(launcher.memoryRecallRoot != null && !launcher.memoryRecallRoot.activeSelf, "MemoryRecallRoot starts hidden");
            Require(launcher.sadnessBattleRoot != null && !launcher.sadnessBattleRoot.activeSelf, "SadnessBattleRoot starts hidden");

            Require(File.Exists("Assets/Yarn/Sadness.yarn"), "Sadness.yarn exists");
            string escapeYarn = File.ReadAllText("Assets/Yarn/Escape.yarn");
            Require(escapeYarn.Contains("<<jump Sadness>>"), "Escape.yarn connects to Sadness");

            Debug.Log("SADNESS_IMPLEMENTATION_SMOKE_RESULT: SUCCESS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("SADNESS_IMPLEMENTATION_SMOKE_RESULT: FAIL: " + exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
