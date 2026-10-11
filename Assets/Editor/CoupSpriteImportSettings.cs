using System.IO;
using UnityEditor;
using UnityEngine;

namespace Coup.EditorTools
{
    /// <summary>
    /// Everything under Assets/Sprites is UI art, so it is imported as a sprite with
    /// alpha transparency and no mipmaps. Running as a postprocessor means art dropped
    /// into the folder later is configured on arrival instead of by hand.
    /// </summary>
    public class CoupSpriteImportSettings : AssetPostprocessor
    {
        private const string SpriteRoot = "Assets/Sprites/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;          // UI is drawn at a fixed scale
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 2048;

            // Full-screen backgrounds sit behind everything and never need alpha.
            var isBackground = assetPath.Contains("/02_Backgrounds/");
            importer.alphaSource = isBackground
                ? TextureImporterAlphaSource.None
                : TextureImporterAlphaSource.FromInput;
        }

        [MenuItem("Coup/Reimport Sprite Art")]
        public static void ReimportAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            var count = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in guids)
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),
                        ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Coup] Reimported {count} sprite textures.");
        }
    }
}
