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

            // Full-screen backgrounds sit behind everything and never need alpha.
            var isBackground = assetPath.Contains("/02_Backgrounds/");
            importer.alphaSource = isBackground
                ? TextureImporterAlphaSource.None
                : TextureImporterAlphaSource.FromInput;

            var cap = CapFor(assetPath);
            importer.maxTextureSize = cap;

            // Fine gold borders and enlarged cards need smaller ASTC blocks on phones.
            // Keep caps based on the drawn asset; compression must not erase those details.
            foreach (var platform in MobilePlatforms)
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = cap;
                settings.format = TextureImporterFormat.ASTC_4x4;
                settings.compressionQuality = 100;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        private static readonly string[] MobilePlatforms = { "Android", "iPhone" };

        /// <summary>
        /// The import cap for a sprite, chosen from the widest the game ever draws it.
        /// Measured rather than guessed:
        ///   cards      Enlarged mobile hands and previews retain a 1024px long edge.
        ///   callouts   PanelCover clamps banner width to 420. The source art is
        ///              2172 wide, which is over five times what reaches the screen.
        ///   buttons    the action button icon is 74 square in a 136 wide button.
        ///   HUD        panel frames and coin piles sit around 300.
        /// Caps are the next power of two above the display size, which leaves roughly
        /// double for a high-density screen without paying for the full source.
        /// </summary>
        private static int CapFor(string path)
        {
            if (path.Contains("/02_Backgrounds/")) return 4096;   // full-resolution portrait phones
            if (path.Contains("/Actions/")) return 512;
            if (path.Contains("/04_Characters_And_Art_Reference/")) return 1024;
            if (path.Contains("/Cards/")) return 1024;
            if (path.Contains("/Buttons/")) return 256;
            if (path.Contains("/Coins/")) return 256;
            if (path.Contains("/HUD/")) return 1024;
            if (path.Contains("/Panels/")) return 1024;
            return 1024;                                          // anything unsorted
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
