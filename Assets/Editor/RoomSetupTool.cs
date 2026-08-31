using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class RoomSetupTool
{
    const string WallTexPath = "Assets/Sprites/Tiles/wall_tiles.png";
    const string FloorTexPath = "Assets/Sprites/Tiles/floor_tiles.png";
    const string PlayerSpritePath = "Assets/Sprites/Player_Placeholder.png";

    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/PKD/Build Starter Room")]
    public static void BuildStarterRoom()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Texture slicing is owned by TileMaterialImportTool. Re-slicing here used
        // to generate fresh sprite IDs and could break already-painted Tilemaps.
        var floorTile = CreateTileAsset("Assets/Sprites/Tiles/Floor_Plain.asset", FloorTexPath, "floor_parquetTan", Tile.ColliderType.None);
        var wallTile = CreateTileAsset("Assets/Sprites/Tiles/Wall_Plain.asset", WallTexPath, "wall_brickRed", Tile.ColliderType.Grid);

        CreatePlayerPlaceholderSprite();

        Scene scene = SceneManager.GetActiveScene();
        GameObject gridGO = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(g => g.name == "SadnessHomeGrid" ||
                                 (g.GetComponent<Grid>() != null &&
                                  g.transform.Find("Floor") != null &&
                                  g.transform.Find("Wall") != null));
        if (gridGO == null)
        {
            Debug.LogError("RoomSetupTool: indoor Grid was not found in active scene.");
            return;
        }

        Transform floorTr = gridGO.transform.Find("Floor");
        Transform wallTr = gridGO.transform.Find("Wall");
        if (floorTr == null || wallTr == null)
        {
            Debug.LogError("RoomSetupTool: 'Floor' or 'Wall' child not found under the indoor Grid.");
            return;
        }

        Tilemap floorTM = floorTr.GetComponent<Tilemap>();
        Tilemap wallTM = wallTr.GetComponent<Tilemap>();

        PaintRoom(floorTM, wallTM, floorTile, wallTile);

        floorTr.GetComponent<TilemapRenderer>().sortingOrder = 0;
        wallTr.GetComponent<TilemapRenderer>().sortingOrder = 2;

        CreateRugDecorationLayer(gridGO);
        SetupWallCollider(wallTr.gameObject);
        CreateOrUpdatePlayer(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("RoomSetupTool: BuildStarterRoom finished.");
    }

    static Tile CreateTileAsset(string assetPath, string textureAssetPath, string spriteName, Tile.ColliderType colliderType)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(textureAssetPath).OfType<Sprite>().FirstOrDefault(s => s.name == spriteName);
        if (sprite == null)
        {
            Debug.LogError($"RoomSetupTool: sprite '{spriteName}' not found in {textureAssetPath}");
            return null;
        }

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.colliderType = colliderType;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static void CreatePlayerPlaceholderSprite()
    {
        if (!File.Exists(PlayerSpritePath))
        {
            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var fill = new Color(0.95f, 0.25f, 0.55f, 1f);
            var outline = new Color(0.35f, 0.05f, 0.2f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    tex.SetPixel(x, y, edge ? outline : fill);
                }
            }
            tex.Apply();
            File.WriteAllBytes(PlayerSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PlayerSpritePath);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(PlayerSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static void PaintRoom(Tilemap floorTM, Tilemap wallTM, Tile floorTile, Tile wallTile)
    {
        for (int x = -4; x <= 4; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                floorTM.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }

        for (int x = -5; x <= 5; x++)
        {
            wallTM.SetTile(new Vector3Int(x, -4, 0), wallTile);
            wallTM.SetTile(new Vector3Int(x, 4, 0), wallTile);
        }
        for (int y = -4; y <= 4; y++)
        {
            wallTM.SetTile(new Vector3Int(-5, y, 0), wallTile);
            wallTM.SetTile(new Vector3Int(5, y, 0), wallTile);
        }
    }

    static void CreateRugDecorationLayer(GameObject gridGO)
    {
        if (gridGO.transform.Find("Rug_Decoration") != null) return;

        var rugGO = new GameObject("Rug_Decoration");
        rugGO.transform.SetParent(gridGO.transform, false);
        rugGO.AddComponent<Tilemap>();
        var renderer = rugGO.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 1;
    }

    static void SetupWallCollider(GameObject wallGO)
    {
        var tmCollider = wallGO.GetComponent<TilemapCollider2D>();
        if (tmCollider == null) tmCollider = wallGO.AddComponent<TilemapCollider2D>();
        tmCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        var rb = wallGO.GetComponent<Rigidbody2D>();
        if (rb == null) rb = wallGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var composite = wallGO.GetComponent<CompositeCollider2D>();
        if (composite == null) composite = wallGO.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
    }

    static void CreateOrUpdatePlayer(Scene scene)
    {
        GameObject player = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "RoomPlayer");
        if (player == null)
        {
            player = new GameObject("RoomPlayer");
            SceneManager.MoveGameObjectToScene(player, scene);
        }
        player.transform.position = new Vector3(0, 0, 0);

        var sr = player.GetComponent<SpriteRenderer>();
        if (sr == null) sr = player.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
        sr.sortingOrder = 5;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) rb = player.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = player.GetComponent<CircleCollider2D>();
        if (col == null) col = player.AddComponent<CircleCollider2D>();
        col.radius = 0.32f;

        var move = player.GetComponent<PlayerMove>();
        if (move == null) move = player.AddComponent<PlayerMove>();
    }
}
