using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class KenneyTinyDungeonImporter : AssetPostprocessor
{
    private const string Tiles = "Assets/Art/KenneyTinyDungeon/Tiles/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Tiles, StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
    }

    [InitializeOnLoadMethod]
    private static void ConfigureExistingImports()
    {
        EditorApplication.delayCall += () =>
        {
            if (!Directory.Exists(Tiles)) return;
            foreach (string path in Directory.GetFiles(Tiles, "*.png", SearchOption.AllDirectories))
            {
                string asset = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.spritePixelsPerUnit != 16
                    || importer.filterMode != FilterMode.Point
                    || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
                }
            }
        };
    }
}
