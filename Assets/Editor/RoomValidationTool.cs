using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class RoomValidationTool
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/PKD/Validate Starter Room")]
    public static void ValidateStarterRoom()
    {
        var failures = new System.Collections.Generic.List<string>();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var grid = roots
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(go => go.name == "SadnessHomeGrid" ||
                                  (go.GetComponent<Grid>() != null &&
                                   go.transform.Find("Floor") != null &&
                                   go.transform.Find("Wall") != null));
        Check(grid != null, "Indoor Grid exists", failures);

        Tilemap floor = FindTilemap(grid, "Floor", failures);
        Tilemap rug = FindTilemap(grid, "Rug_Decoration", failures);
        Tilemap wall = FindTilemap(grid, "Wall", failures);

        Check(CountTiles(floor) == 63, "Floor contains 63 tiles (9 x 7)", failures);
        Check(CountTiles(rug) == 0, "Rug_Decoration starts empty", failures);
        Check(CountTiles(wall) == 36, "Wall contains 36 perimeter tiles", failures);
        CheckSolidRectangle(floor, -4, 4, -3, 3, "Floor has no gaps in its 9 x 7 area", failures);
        CheckPerimeter(wall, -5, 5, -4, 4, "Wall has no gaps around the room", failures);
        CheckOrder(floor, 0, failures);
        CheckOrder(rug, 1, failures);
        CheckOrder(wall, 2, failures);

        if (wall != null)
        {
            var wallGo = wall.gameObject;
            var tileCollider = wallGo.GetComponent<TilemapCollider2D>();
            var composite = wallGo.GetComponent<CompositeCollider2D>();
            var wallBody = wallGo.GetComponent<Rigidbody2D>();
            Check(tileCollider != null, "Wall has TilemapCollider2D", failures);
            Check(composite != null, "Wall has CompositeCollider2D", failures);
            Check(tileCollider != null && tileCollider.compositeOperation == Collider2D.CompositeOperation.Merge,
                "Wall collider is merged into the composite", failures);
            Check(wallBody != null && wallBody.bodyType == RigidbodyType2D.Static,
                "Wall Rigidbody2D is static", failures);

            Physics2D.SyncTransforms();
            CheckWallCast(Vector2.right, composite, failures);
            CheckWallCast(Vector2.left, composite, failures);
            CheckWallCast(Vector2.up, composite, failures);
            CheckWallCast(Vector2.down, composite, failures);
        }

        var player = roots.FirstOrDefault(go => go.name == "RoomPlayer");
        Check(player != null, "RoomPlayer exists at scene root", failures);
        if (player != null)
        {
            var body = player.GetComponent<Rigidbody2D>();
            Check(player.transform.parent == null, "RoomPlayer is not parented to Grid", failures);
            Check(player.GetComponent<PlayerMove>() != null, "RoomPlayer has PlayerMove", failures);
            Check(player.GetComponent<CircleCollider2D>() != null, "RoomPlayer has CircleCollider2D", failures);
            Check(body != null && body.bodyType == RigidbodyType2D.Dynamic && body.gravityScale == 0,
                "RoomPlayer Rigidbody2D is dynamic with zero gravity", failures);
        }

        var camera = roots.Select(go => go.GetComponent<Camera>()).FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
        Check(camera != null && camera.orthographic, "Main Camera exists and is orthographic", failures);
        if (camera != null && camera.orthographic)
        {
            Check(camera.orthographicSize >= 4.5f, "Camera vertical view contains the full room", failures);
            Check(camera.orthographicSize * camera.aspect >= 5.5f, "Camera horizontal view contains the full room", failures);
        }

        ValidateTexture("Assets/Sprites/Tiles/floor_tiles.png", 32, 50, failures);
        ValidateTexture("Assets/Sprites/Tiles/wall_tiles.png", 32, 83, failures);
        ValidateTexture("Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/floorswalls_LRK.png", 16, 15, failures);
        ValidateBottomLeftGridSprites("Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/floorswalls_LRK.png", 16, failures);
        ValidateSpriteCount("Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/cabinets_LRK.png", 69, failures);
        ValidateSpriteCount("Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/decorations_LRK.png", 16, failures);
        ValidateSpriteCount("Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/doorswindowsstairs_LRK.png", 42, failures);
        ValidateSpriteCount("Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/kitchen_LRK.png", 31, failures);
        ValidateSpriteCount("Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/livingroom_LRK.png", 82, failures);
        ValidateSpriteCount("Assets/Sprites/Tiles/Pixel_16_interiors_v2_free/Pixel_16_interiors_v2_free/tiles and items.png", 18, failures);

        if (failures.Count > 0)
            throw new Exception("Room validation failed:\n- " + string.Join("\n- ", failures));

        Debug.Log("RoomValidationTool: PASS - room layout, collisions, player, and 406 imported sprites validated.");
    }

    public static void CaptureGameView()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camera = scene.GetRootGameObjects()
            .Select(go => go.GetComponent<Camera>())
            .FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
        if (camera == null) throw new Exception("Main Camera not found.");

        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/pkd-room-game-view.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
        Debug.Log("RoomValidationTool: captured Game View to " + outputPath);
    }

    static Tilemap FindTilemap(GameObject grid, string childName, System.Collections.Generic.List<string> failures)
    {
        var child = grid != null ? grid.transform.Find(childName) : null;
        var tilemap = child != null ? child.GetComponent<Tilemap>() : null;
        Check(tilemap != null, childName + " Tilemap exists", failures);
        return tilemap;
    }

    static int CountTiles(Tilemap tilemap)
    {
        if (tilemap == null) return -1;
        tilemap.CompressBounds();
        int count = 0;
        foreach (var position in tilemap.cellBounds.allPositionsWithin)
            if (tilemap.HasTile(position)) count++;
        return count;
    }

    static void CheckSolidRectangle(Tilemap tilemap, int minX, int maxX, int minY, int maxY,
        string message, System.Collections.Generic.List<string> failures)
    {
        bool complete = tilemap != null;
        for (int y = minY; complete && y <= maxY; y++)
            for (int x = minX; complete && x <= maxX; x++)
                complete = tilemap.HasTile(new Vector3Int(x, y, 0));
        Check(complete, message, failures);
    }

    static void CheckPerimeter(Tilemap tilemap, int minX, int maxX, int minY, int maxY,
        string message, System.Collections.Generic.List<string> failures)
    {
        bool complete = tilemap != null;
        for (int x = minX; complete && x <= maxX; x++)
            complete = tilemap.HasTile(new Vector3Int(x, minY, 0)) &&
                       tilemap.HasTile(new Vector3Int(x, maxY, 0));
        for (int y = minY; complete && y <= maxY; y++)
            complete = tilemap.HasTile(new Vector3Int(minX, y, 0)) &&
                       tilemap.HasTile(new Vector3Int(maxX, y, 0));
        Check(complete, message, failures);
    }

    static void CheckOrder(Tilemap tilemap, int expected, System.Collections.Generic.List<string> failures)
    {
        var renderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
        Check(renderer != null && renderer.sortingOrder == expected,
            (tilemap != null ? tilemap.name : "Missing Tilemap") + " sorting order is " + expected, failures);
    }

    static void ValidateTexture(string path, int ppu, int spriteCount, System.Collections.Generic.List<string> failures)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Check(importer != null, path + " has a TextureImporter", failures);
        if (importer != null)
        {
            Check(importer.spriteImportMode == SpriteImportMode.Multiple, path + " uses Multiple sprite mode", failures);
            Check(Mathf.Approximately(importer.spritePixelsPerUnit, ppu), path + " PPU is " + ppu, failures);
            Check(importer.filterMode == FilterMode.Point, path + " uses Point filtering", failures);
            Check(importer.textureCompression == TextureImporterCompression.Uncompressed, path + " has no compression", failures);
            Check(!importer.mipmapEnabled, path + " has mipmaps disabled", failures);
        }
        ValidateSpriteCount(path, spriteCount, failures);
    }

    static void CheckWallCast(Vector2 direction, Collider2D expectedCollider,
        System.Collections.Generic.List<string> failures)
    {
        var hits = Physics2D.CircleCastAll(Vector2.zero, 0.32f, direction, 10f);
        Check(hits.Any(hit => hit.collider == expectedCollider),
            "Player-sized cast " + direction + " hits the perimeter wall", failures);
    }

    static void ValidateSpriteCount(string path, int expected, System.Collections.Generic.List<string> failures)
    {
        int actual = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
        Check(actual == expected, path + " has " + expected + " sprites (actual " + actual + ")", failures);
    }

    static void ValidateBottomLeftGridSprites(string path, int gridPixels,
        System.Collections.Generic.List<string> failures)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        bool valid = sprites.Length > 0 && sprites.All(sprite =>
            Mathf.Approximately(sprite.pivot.x, 0f) &&
            Mathf.Approximately(sprite.pivot.y, 0f) &&
            Mathf.Approximately(sprite.rect.width % gridPixels, 0f) &&
            Mathf.Approximately(sprite.rect.height % gridPixels, 0f));
        Check(valid, path + " uses bottom-left pivots and 16px-multiple bounds", failures);
    }

    static void Check(bool condition, string message, System.Collections.Generic.List<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
