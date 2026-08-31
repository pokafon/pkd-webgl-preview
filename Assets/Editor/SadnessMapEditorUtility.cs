using System;
using System.Linq;
using MemoryRecall;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

internal static class SadnessMapEditorUtility
{
    internal const string EnvironmentName = "SadnessMapEnvironment";
    internal const string OutdoorGridName = "SadnessOutdoorGrid";
    internal const string HomeGridName = "SadnessHomeGrid";

    internal static SadnessMapEnvironment EnsureEnvironment(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject environmentObject = roots.FirstOrDefault(go => go.name == EnvironmentName);
        if (environmentObject == null)
        {
            environmentObject = new GameObject(EnvironmentName);
            SceneManager.MoveGameObjectToScene(environmentObject, scene);
        }

        SadnessMapEnvironment environment = environmentObject.GetComponent<SadnessMapEnvironment>();
        if (environment == null)
        {
            environment = environmentObject.AddComponent<SadnessMapEnvironment>();
        }

        GameObject homeGrid = FindGrid(scene, HomeGridName, IsHomeGrid);
        GameObject outdoorGrid = FindGrid(scene, OutdoorGridName, go => go != homeGrid && go.GetComponent<Grid>() != null);
        if (homeGrid == null || outdoorGrid == null)
        {
            throw new Exception("悲しみ編に使う2つのGridを特定できません。屋内はFloor/Wallを持つGrid、屋外はもう一方のGridとして判定します。");
        }

        homeGrid.name = HomeGridName;
        outdoorGrid.name = OutdoorGridName;
        homeGrid.transform.SetParent(environmentObject.transform, true);
        outdoorGrid.transform.SetParent(environmentObject.transform, true);

        Bounds outdoorBounds = CalculateWorldBounds(outdoorGrid);
        Bounds homeBounds = CalculateWorldBounds(homeGrid);

        environment.outdoorGrid = outdoorGrid;
        environment.homeGrid = homeGrid;
        environment.gameplayCamera = Camera.main;
        environment.outdoorMinBounds = InsetMin(outdoorBounds, 0.5f);
        environment.outdoorMaxBounds = InsetMax(outdoorBounds, 0.5f);
        environment.homeMinBounds = InsetMin(homeBounds, 0.5f);
        environment.homeMaxBounds = InsetMax(homeBounds, 0.5f);

        // 初期値は現在のTilemap範囲から決める。マーカーはScene上で後から自由に移動でき、
        // ビルダーを再実行しても既存位置を上書きしない。
        environment.outdoorStart = GetOrCreateMarker(
            environmentObject.transform,
            "OutdoorStart",
            Point(outdoorBounds, -0.08f, -0.18f));
        environment.outdoorDoor = GetOrCreateMarker(
            environmentObject.transform,
            "OutdoorDoor",
            Point(outdoorBounds, -0.30f, 0.23f));
        environment.homeStart = GetOrCreateMarker(
            environmentObject.transform,
            "HomeStart",
            Point(homeBounds, 0f, -0.18f));
        environment.homeDoor = GetOrCreateMarker(
            environmentObject.transform,
            "HomeDoor",
            Point(homeBounds, 0f, -0.30f));
        environment.homeMotherSpot = GetOrCreateMarker(
            environmentObject.transform,
            "HomeMotherSpot",
            Point(homeBounds, -0.15f, 0.12f));

        environment.outdoorFriendSpots = new[]
        {
            GetOrCreateMarker(environmentObject.transform, "OutdoorFriendA", Point(outdoorBounds, -0.22f, 0.08f)),
            GetOrCreateMarker(environmentObject.transform, "OutdoorFriendB", Point(outdoorBounds, 0f, -0.13f)),
            GetOrCreateMarker(environmentObject.transform, "OutdoorFriendC", Point(outdoorBounds, 0.22f, 0.05f)),
        };

        // 作業用に残っている単体プレイヤーは、悲しみ編では各ミニゲーム側のプレイヤーを使う。
        GameObject roomPlayer = roots.FirstOrDefault(go => go.name == "RoomPlayer");
        if (roomPlayer != null)
        {
            roomPlayer.SetActive(false);
        }

        outdoorGrid.SetActive(false);
        homeGrid.SetActive(false);
        environmentObject.SetActive(true);
        return environment;
    }

    private static GameObject FindGrid(Scene scene, string preferredName, Func<GameObject, bool> predicate)
    {
        Transform[] transforms = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .ToArray();

        GameObject preferred = transforms
            .Select(transform => transform.gameObject)
            .FirstOrDefault(go => go.name == preferredName && go.GetComponent<Grid>() != null);
        if (preferred != null) return preferred;

        return transforms
            .Select(transform => transform.gameObject)
            .FirstOrDefault(predicate);
    }

    private static bool IsHomeGrid(GameObject candidate)
    {
        if (candidate.GetComponent<Grid>() == null) return false;
        return candidate.transform.Find("Floor") != null && candidate.transform.Find("Wall") != null;
    }

    private static Bounds CalculateWorldBounds(GameObject grid)
    {
        Tilemap[] tilemaps = grid.GetComponentsInChildren<Tilemap>(true);
        bool found = false;
        Bounds result = default;

        foreach (Tilemap tilemap in tilemaps)
        {
            tilemap.CompressBounds();
            Bounds localBounds = tilemap.localBounds;
            Vector3 min = tilemap.transform.TransformPoint(localBounds.min);
            Vector3 max = tilemap.transform.TransformPoint(localBounds.max);
            Bounds worldBounds = new Bounds((min + max) * 0.5f, new Vector3(
                Mathf.Abs(max.x - min.x),
                Mathf.Abs(max.y - min.y),
                0f));

            if (!found)
            {
                result = worldBounds;
                found = true;
            }
            else
            {
                result.Encapsulate(worldBounds.min);
                result.Encapsulate(worldBounds.max);
            }
        }

        if (!found)
        {
            throw new Exception($"{grid.name} の下にTilemapがありません。");
        }
        return result;
    }

    private static Transform GetOrCreateMarker(Transform parent, string name, Vector3 defaultPosition)
    {
        Transform marker = parent.Find(name);
        if (marker != null) return marker;

        GameObject markerObject = new GameObject(name);
        markerObject.transform.SetParent(parent, true);
        markerObject.transform.position = defaultPosition;
        return markerObject.transform;
    }

    private static Vector3 Point(Bounds bounds, float normalizedX, float normalizedY)
    {
        return new Vector3(
            bounds.center.x + bounds.size.x * normalizedX,
            bounds.center.y + bounds.size.y * normalizedY,
            0f);
    }

    private static Vector2 InsetMin(Bounds bounds, float inset)
    {
        return new Vector2(bounds.min.x + inset, bounds.min.y + inset);
    }

    private static Vector2 InsetMax(Bounds bounds, float inset)
    {
        return new Vector2(bounds.max.x - inset, bounds.max.y - inset);
    }
}
