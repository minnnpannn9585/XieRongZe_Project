using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Qingxi.Editor
{
    /// <summary>Import settings and named sprite slices, kept separate from runtime gameplay.</summary>
    public static class QingxiArtImporter
    {
        public const string Folder = "Assets/Art/Illustrations/";
        public static readonly string[] LandmarkNames =
        {
            "river_dock", "livestock_farm", "fish_pond", "recycling_yard",
            "village_houses", "reservoir_dock", "fish_cages", "fertilizer_factory",
            "lakeside_restaurant", "water_intake", "creek_sampling", "mountain_guesthouse",
            "stone_quarry", "mine_entrance", "irrigation_fields", "player_investigator"
        };

        [MenuItem("Qingxi/Import Illustration Sprites")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!Directory.Exists(Folder)) throw new DirectoryNotFoundException(Folder);
            foreach (string path in Directory.GetFiles(Folder, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                if (assetPath.EndsWith("investigation_atlas.png"))
                    ImportAtlas(assetPath, 4, 4, LandmarkNames, true);
                else if (assetPath.EndsWith("environment_atlas.png"))
                    ImportAtlas(assetPath, 2, 2, new[] { "broadleaf_tree", "pine_tree", "reeds", "rocks" }, true, 680f / 1254f);
                else if (assetPath.EndsWith("terrain_atlas.png"))
                    ImportAtlas(assetPath, 1, 3, new[] { "river_town_map", "reservoir_map", "mountain_creek_map" }, false);
                else ImportSingle(assetPath);
            }
            AssetDatabase.SaveAssets();
        }

        static TextureImporter Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = 100f;
            importer.wrapMode = TextureWrapMode.Clamp;
            return importer;
        }

        static void ImportSingle(string path)
        {
            var importer = Configure(path);
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        public static void ImportAtlas(string path, int columns, int rows, string[] names, bool trim, float firstRowFraction = 0f)
        {
            var importer = Configure(path);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!pixels.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
                var rects = new List<SpriteRect>();
                for (int i = 0; i < names.Length; i++)
                {
                    int column = i % columns;
                    int rowFromBottom = rows - 1 - i / columns;
                    int x0 = column * pixels.width / columns;
                    int x1 = (column + 1) * pixels.width / columns;
                    int y0 = rowFromBottom * pixels.height / rows;
                    int y1 = (rowFromBottom + 1) * pixels.height / rows;
                    if (path.EndsWith("investigation_atlas.png"))
                    {
                        int row = i / columns;
                        int[] rowCuts = { 0, 323, 627, 919, 1254 };
                        int[] columnCuts = row == 3 ? new[] { 0, 323, 629, 964, 1254 } : new[] { 0, 323, 629, 945, 1254 };
                        x0 = Mathf.RoundToInt(pixels.width * columnCuts[column] / 1254f);
                        x1 = Mathf.RoundToInt(pixels.width * columnCuts[column + 1] / 1254f);
                        y0 = pixels.height - Mathf.RoundToInt(pixels.height * rowCuts[row + 1] / 1254f);
                        y1 = pixels.height - Mathf.RoundToInt(pixels.height * rowCuts[row] / 1254f);
                    }
                    if (rows == 2 && firstRowFraction > 0f)
                    {
                        int split = pixels.height - Mathf.RoundToInt(pixels.height * firstRowFraction);
                        y0 = rowFromBottom == 0 ? 0 : split;
                        y1 = rowFromBottom == 0 ? split : pixels.height;
                    }
                    // The terrain sheet has three painted panels, separated at source rows 616 and 1234.
                    if (path.EndsWith("terrain_atlas.png"))
                    {
                        int[] sourceRows = { 0, Mathf.RoundToInt(pixels.height * 616f / 1852f),
                            Mathf.RoundToInt(pixels.height * 1234f / 1852f), pixels.height };
                        y0 = pixels.height - sourceRows[i + 1] + 1;
                        y1 = pixels.height - sourceRows[i] - 1;
                        x0++; x1--;
                    }
                    var rect = new RectInt(x0, y0, x1 - x0, y1 - y0);
                    if (trim) rect = TrimBounds(pixels, rect);
                    rects.Add(new SpriteRect
                    {
                        name = names[i], rect = new Rect(rect.x, rect.y, rect.width, rect.height),
                        alignment = trim ? SpriteAlignment.BottomCenter : SpriteAlignment.Center,
                        pivot = trim ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f),
                        spriteID = previous.TryGetValue(names[i], out GUID id) ? id : GUID.Generate()
                    });
                }
                provider.SetSpriteRects(rects.ToArray());
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                    rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                provider.Apply();
                importer.SaveAndReimport();
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }

        static RectInt TrimBounds(Texture2D texture, RectInt cell)
        {
            int minX = cell.xMax, minY = cell.yMax, maxX = cell.xMin, maxY = cell.yMin;
            for (int y = cell.yMin; y < cell.yMax; y++)
            for (int x = cell.xMin; x < cell.xMax; x++)
            {
                if (texture.GetPixel(x, y).a < 0.08f) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY) throw new InvalidDataException("Empty illustration atlas cell " + cell);
            minX = Math.Max(cell.xMin, minX - 2); minY = Math.Max(cell.yMin, minY - 2);
            maxX = Math.Min(cell.xMax - 1, maxX + 2); maxY = Math.Min(cell.yMax - 1, maxY + 2);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
