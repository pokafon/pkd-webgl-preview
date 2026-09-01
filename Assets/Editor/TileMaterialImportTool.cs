using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Makes every sheet under Assets/Sprites/Tiles usable as sprite material:
// - floor_tiles.png / wall_tiles.png: mechanical 32x32 grid slice, but with
//   human-readable names instead of positional ones, and blank cells skipped.
//   Existing sprite IDs are preserved by rect position so the room already
//   built in SampleScene (which references these sprites) keeps working.
// - floorswalls_LRK.png: a catalog of whole material swatches (each block IS
//   one floor/wallpaper/panel/brick choice meant to be used as-is), sliced by
//   hand-identified rects, not a 16px mosaic.
// - furniture/decoration sheets: content-aware (alpha connected component)
//   slice so individual pieces aren't chopped mid-object.
public static class TileMaterialImportTool
{
    const string FloorTilesPath = "Assets/Sprites/Tiles/floor_tiles.png";
    const string WallTilesPath = "Assets/Sprites/Tiles/wall_tiles.png";
    const string FloorswallsPath = "Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/floorswalls_LRK.png";

    // floorswalls_LRK.png is a presentation sheet, not a uniform 16x16 atlas.
    // Keep each complete material region together while ensuring every edge
    // lands on the 16px grid. Rects are (x, yFromBottom, w, h).
    static readonly (string name, int x, int y, int w, int h)[] FloorswallsSwatches =
    {
        ("wallpaper_yellow", 16, 208, 64, 32),
        ("wallpaper_teal", 80, 208, 64, 32),
        ("wallpaper_white", 144, 208, 64, 32),
        ("panel_grey", 16, 176, 64, 32),
        ("panel_orange", 80, 176, 64, 32),
        // The old panel_brown rect was the complete 64x32 wall-panel sample.
        // It was mistakenly used as a floor, so every repeated copy brought its
        // top/bottom frame into the join. Use one real 16x16 dark-brown floor
        // cell from the plank region below it instead.
        ("floor_darkbrown", 160, 144, 16, 16),
        ("brick_tan", 16, 128, 64, 48),
        ("brick_white", 80, 128, 64, 48),
        ("brick_darkbrown", 144, 128, 64, 48),
        ("muted_pink", 16, 80, 64, 32),
        ("muted_green", 80, 80, 64, 32),
        ("muted_grey", 144, 80, 64, 32),
        ("muted_grey2", 16, 16, 64, 64),
        ("muted_oliveGrey", 80, 16, 64, 64),
        ("muted_charcoal", 144, 16, 64, 64),
    };

    // floor_tiles.png, 32px grid, (col,row) with row counted from the bottom.
    static readonly Dictionary<(int, int), string> FloorTilesNames = new Dictionary<(int, int), string>
    {
        {(0,8),"rug_oliveLattice"}, {(1,8),"rug_oliveLattice"}, {(2,8),"rug_oliveLattice"},
        {(3,8),"rug_tealDiamond"}, {(4,8),"rug_tealDiamond"}, {(5,8),"rug_tealDiamond"},
        {(0,7),"rug_oliveLattice"}, {(1,7),"rug_oliveLattice"}, {(2,7),"rug_oliveLattice"},
        {(3,7),"rug_tealDiamond"}, {(4,7),"rug_tealDiamond"}, {(5,7),"rug_tealDiamond"},
        {(0,6),"rug_oliveLattice"}, {(1,6),"rug_oliveLattice"}, {(2,6),"rug_oliveLattice"},
        {(3,6),"rug_tealDiamond"}, {(4,6),"rug_tealDiamond"}, {(5,6),"rug_tealDiamond"},
        {(0,5),"rug_mintCheck"}, {(1,5),"rug_mintCheck"}, {(2,5),"rug_mintCheck"},
        {(3,5),"rug_paleMintCheck"}, {(4,5),"rug_paleMintCheck"}, {(5,5),"rug_paleMintCheck"},
        {(0,4),"rug_mintCheck"}, {(1,4),"rug_mintCheck"}, {(2,4),"rug_mintCheck"},
        {(3,4),"rug_paleMintCheck"}, {(4,4),"rug_paleMintCheck"}, {(5,4),"rug_paleMintCheck"},
        {(0,3),"rug_mintCheck"}, {(1,3),"rug_mintCheck"}, {(2,3),"rug_mintCheck"},
        {(3,3),"rug_paleMintCheck"}, {(4,3),"rug_paleMintCheck"}, {(5,3),"rug_paleMintCheck"},
        {(0,2),"floor_herringboneTan"}, {(1,2),"floor_herringboneGold"}, {(2,2),"floor_weaveOrange"},
        {(3,2),"floor_greyPlank_top"}, {(4,2),"floor_oliveBorder_top"},
        {(0,1),"floor_parquetRed"}, {(1,1),"floor_parquetBrown"}, {(2,1),"floor_nightSparkle"},
        {(3,1),"floor_greyPlank_mid"}, {(4,1),"floor_oliveBorder_mid"},
        {(0,0),"floor_parquetTan"}, {(1,0),"floor_parquetGold"},
        {(3,0),"floor_greyPlank_bottom"}, {(4,0),"floor_oliveBorder_bottom"},
    };

    // wall_tiles.png, 32px grid, (col,row) with row counted from the bottom.
    static readonly Dictionary<(int, int), string> WallTilesNames = new Dictionary<(int, int), string>
    {
        {(0,0),"wall_pinkBlock"}, {(1,0),"wall_pinkBlock"}, {(2,0),"wall_pinkBlock"}, {(3,0),"wall_pinkBlock"},
        {(0,1),"wall_oliveTanBlock"}, {(1,1),"wall_oliveTanBlock"}, {(2,1),"wall_oliveTanBlock"}, {(3,1),"wall_oliveTanBlock"},
        {(0,2),"wall_navyBlock"}, {(1,2),"wall_navyBlock"}, {(2,2),"wall_navyBlock"}, {(3,2),"wall_navyBlock"},
        {(0,3),"window_creamWide_part1"}, {(1,3),"window_creamWide_part2"}, {(2,3),"window_creamWide_part3"}, {(3,3),"window_creamWide_part4"},
        {(0,4),"wall_navyStripe_base"}, {(1,4),"wall_navyStripe_base"}, {(2,4),"wall_navyStripe_base"},
        {(3,4),"wall_navyStripe_base"}, {(5,4),"wall_navyStripe_base"}, {(4,4),"wall_navyStripe_windowSlit_base"},
        {(0,5),"wall_navyStripe_top"}, {(1,5),"wall_navyStripe_top"}, {(2,5),"wall_navyStripe_top"},
        {(3,5),"wall_navyStripe_top"}, {(5,5),"wall_navyStripe_top"}, {(4,5),"wall_navyStripe_windowSlit_top"},
        {(6,5),"door_darkRed_paneled"}, {(7,5),"door_tan_wood"},
        {(0,6),"wall_burgundyStripe_base"}, {(1,6),"wall_burgundyStripe_base"}, {(2,6),"wall_burgundyStripe_base"},
        {(3,6),"wall_burgundyStripe_base"}, {(5,6),"wall_burgundyStripe_base"}, {(4,6),"wall_burgundyStripe_windowSlit_base"},
        {(6,6),"door_dark_closed"}, {(7,6),"window_mullioned"},
        {(0,7),"wall_burgundyStripe_top"}, {(1,7),"wall_burgundyStripe_top"}, {(2,7),"wall_burgundyStripe_top"},
        {(3,7),"wall_burgundyStripe_top"}, {(5,7),"wall_burgundyStripe_top"}, {(4,7),"wall_burgundyStripe_windowSlit_top"},
        {(7,7),"wall_brickRed"}, {(8,7),"wall_brickRed"},
        {(0,8),"wall_maroonDiamond_base"}, {(1,8),"wall_maroonDiamond_base"}, {(2,8),"wall_maroonDiamond_base"},
        {(3,8),"wall_maroonDiamond_base"}, {(5,8),"wall_maroonDiamond_base"}, {(4,8),"wall_maroonDiamond_windowSlit_base"},
        {(7,8),"wall_brickRed"}, {(8,8),"wall_brickRed"},
        {(0,9),"wall_maroonDiamond_top"}, {(1,9),"wall_maroonDiamond_top"}, {(2,9),"wall_maroonDiamond_top"},
        {(3,9),"wall_maroonDiamond_top"}, {(5,9),"wall_maroonDiamond_top"}, {(4,9),"wall_maroonDiamond_windowSlit_top"},
        {(7,9),"wall_chimneyStone"}, {(8,9),"wall_chimneyStone"},
        {(0,10),"wall_yellowStripe_base"}, {(1,10),"wall_yellowStripe_base"}, {(2,10),"wall_yellowStripe_base"},
        {(3,10),"wall_yellowStripe_base"}, {(5,10),"wall_yellowStripe_base"}, {(4,10),"wall_yellowStripe_windowSlit_base"},
        {(7,10),"wall_chimneyStone"}, {(8,10),"wall_chimneyStone"},
        {(0,11),"wall_yellowStripe_top"}, {(1,11),"wall_yellowStripe_top"}, {(2,11),"wall_yellowStripe_top"},
        {(3,11),"wall_yellowStripe_top"}, {(5,11),"wall_yellowStripe_top"}, {(4,11),"wall_yellowStripe_windowSlit_top"},
        {(7,11),"wall_chimneyStone"}, {(8,11),"wall_chimneyStone"},
    };

    static readonly string[] AutoSliceFiles16 =
    {
        "Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/cabinets_LRK.png",
        "Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/decorations_LRK.png",
        "Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/doorswindowsstairs_LRK.png",
        "Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/kitchen_LRK.png",
        "Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/livingroom_LRK.png",
        "Assets/Sprites/Tiles/Pixel_16_interiors_v2_free/Pixel_16_interiors_v2_free/tiles and items.png",
    };

    [MenuItem("Tools/PKD/Import All Tile Materials")]
    public static void ImportAllTileMaterials()
    {
        SliceGridNamed(FloorTilesPath, 32, FloorTilesNames);
        SliceGridNamed(WallTilesPath, 32, WallTilesNames);
        SliceNamedRects(FloorswallsPath, 16, FloorswallsSwatches);
        foreach (var f in AutoSliceFiles16) SliceAutomatic(f, minAreaPx: 6);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TileMaterialImportTool: ImportAllTileMaterials finished.");
    }

    [MenuItem("Tools/PKD/Import LRK Floors and Walls")]
    public static void ImportLrkFloorsAndWalls()
    {
        SliceNamedRects(FloorswallsPath, 16, FloorswallsSwatches);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TileMaterialImportTool: ImportLrkFloorsAndWalls finished.");
    }

    static void ApplyBaseImportSettings(TextureImporter importer, int ppu)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
    }

    // Re-slices a texture on a fixed grid, skipping fully-transparent cells and
    // using human-readable names. Reuses the existing spriteID for any rect that
    // already existed at the same position, so prior references (Tile assets,
    // already-painted Tilemap cells) keep pointing at the right sprite.
    static void SliceGridNamed(string assetPath, int cell, Dictionary<(int, int), string> names)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        ApplyBaseImportSettings(importer, cell);
        importer.isReadable = true;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        var existingIds = new Dictionary<(int, int), GUID>();
        foreach (var r in dataProvider.GetSpriteRects())
        {
            int ex = Mathf.RoundToInt(r.rect.x / cell);
            int ey = Mathf.RoundToInt(r.rect.y / cell);
            existingIds[(ex, ey)] = r.spriteID;
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        int w = tex.width, h = tex.height;
        Color32[] pixels = tex.GetPixels32();
        int cols = w / cell;
        int rows = h / cell;
        string baseName = Path.GetFileNameWithoutExtension(assetPath);

        var rects = new List<SpriteRect>();
        var usedNames = new HashSet<string>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (IsCellBlank(pixels, w, h, col * cell, row * cell, cell))
                    continue;

                string baseLabel = names.TryGetValue((col, row), out var n) ? n : $"{baseName}_c{col}r{row}";
                string label = baseLabel;
                int suffix = 2;
                while (!usedNames.Add(label))
                    label = $"{baseLabel}_{suffix++}";

                GUID id = existingIds.TryGetValue((col, row), out var existing) ? existing : GUID.Generate();

                rects.Add(new SpriteRect
                {
                    name = label,
                    rect = new Rect(col * cell, row * cell, cell, cell),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                    spriteID = id
                });
            }
        }

        dataProvider.SetSpriteRects(rects.ToArray());
        var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameFileIdProvider.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        dataProvider.Apply();

        importer.isReadable = false;
        importer.SaveAndReimport();

        Debug.Log($"TileMaterialImportTool: {assetPath} -> named grid slice, {rects.Count} tiles ({cols}x{rows} grid, blanks skipped).");
    }

    static bool IsCellBlank(Color32[] pixels, int texWidth, int texHeight, int x0, int y0, int cell)
    {
        for (int y = y0; y < y0 + cell && y < texHeight; y++)
        {
            for (int x = x0; x < x0 + cell && x < texWidth; x++)
            {
                if (pixels[y * texWidth + x].a > 10) return false;
            }
        }
        return true;
    }

    static void SliceNamedRects(string assetPath, int ppu, (string name, int x, int y, int w, int h)[] swatches)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        ApplyBaseImportSettings(importer, ppu);
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        var existingIds = dataProvider.GetSpriteRects()
            .GroupBy(r => r.name)
            .ToDictionary(group => group.Key, group => group.First().spriteID);

        // Preserve references made before the bad large sprite was discovered.
        // After reimport, objects that pointed at panel_brown resolve to the
        // corrected floor_darkbrown sprite instead of becoming missing.
        if (!existingIds.ContainsKey("floor_darkbrown") && existingIds.TryGetValue("panel_brown", out var legacyPanelBrownId))
            existingIds["floor_darkbrown"] = legacyPanelBrownId;

        var rects = swatches.Select(s => new SpriteRect
        {
            name = s.name,
            rect = new Rect(s.x, s.y, s.w, s.h),
            // Complete floor/wall parts are placed from their lower-left grid
            // corner, so integer Transform positions always land on cell edges.
            alignment = s.name == "floor_darkbrown" ? SpriteAlignment.Center : SpriteAlignment.Custom,
            pivot = s.name == "floor_darkbrown" ? new Vector2(0.5f, 0.5f) : Vector2.zero,
            border = Vector4.zero,
            spriteID = existingIds.TryGetValue(s.name, out var existing) ? existing : GUID.Generate()
        }).ToArray();

        dataProvider.SetSpriteRects(rects);
        var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameFileIdProvider.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        dataProvider.Apply();
        importer.SaveAndReimport();

        Debug.Log($"TileMaterialImportTool: {assetPath} -> {rects.Length} named material swatches.");
    }

    static void SliceAutomatic(string assetPath, int minAreaPx)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        ApplyBaseImportSettings(importer, 16);
        importer.isReadable = true;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        int w = tex.width, h = tex.height;
        Color32[] pixels = tex.GetPixels32();

        var visited = new bool[w * h];
        var stack = new Stack<int>();
        var boxes = new List<RectInt>();
        int[] dxs = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dys = { 0, 0, 1, -1, 1, -1, 1, -1 };

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (visited[idx]) continue;
                visited[idx] = true;
                if (pixels[idx].a <= 10) continue;

                int minX = x, maxX = x, minY = y, maxY = y, area = 0;
                stack.Push(idx);
                while (stack.Count > 0)
                {
                    int cidx = stack.Pop();
                    int cx = cidx % w, cy = cidx / w;
                    area++;
                    if (cx < minX) minX = cx;
                    if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    for (int k = 0; k < 8; k++)
                    {
                        int nx = cx + dxs[k], ny = cy + dys[k];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        int nidx = ny * w + nx;
                        if (visited[nidx]) continue;
                        visited[nidx] = true;
                        if (pixels[nidx].a <= 10) continue;
                        stack.Push(nidx);
                    }
                }

                if (area >= minAreaPx)
                    boxes.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        var rects = new List<SpriteRect>();
        int n = 0;
        foreach (var b in boxes)
        {
            rects.Add(new SpriteRect
            {
                name = $"{baseName}_item_{n}",
                rect = new Rect(b.x, b.y, b.width, b.height),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
                spriteID = GUID.Generate()
            });
            n++;
        }

        dataProvider.SetSpriteRects(rects.ToArray());
        var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameFileIdProvider.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        dataProvider.Apply();

        importer.isReadable = false;
        importer.SaveAndReimport();

        Debug.Log($"TileMaterialImportTool: {assetPath} -> auto-sliced into {rects.Count} items.");
    }
}
