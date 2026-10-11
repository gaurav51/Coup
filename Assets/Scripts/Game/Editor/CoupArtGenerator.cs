using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Coup.Core;

namespace Coup.EditorTools
{
    /// <summary>
    /// Derives the sprites the card prefabs need from the supplied art:
    /// cut-out character portraits (the shipped card faces have baked-in captions
    /// and a white backing) plus a few generated 9-slice panels so UI chrome can be
    /// tinted per faction instead of stretching ornate raster art.
    /// </summary>
    public static class CoupArtGenerator
    {
        private const string Characters = "Assets/Sprites/04_Characters_And_Art_Reference";
        private const string OutDir = "Assets/Art/Generated";

        /// <summary>
        /// How much of the card to keep, measured from the top. The lower slice holds
        /// the baked-in name and ability text, which we re-draw with TextMeshPro.
        /// </summary>
        private const float KeepTopFraction = 0.70f;

        /// <summary>
        /// Trimmed from each side and the top to drop the printed card border, its
        /// corner brackets and the emblem panel. The card prefab draws its own frame,
        /// and leftover fragments of the old one read as artefacts against it.
        /// </summary>
        private const float SideInset = 0.09f;
        private const float TopInset = 0.07f;

        /// <summary>
        /// Width-to-height of the card prefab. Portraits are composed onto a canvas of
        /// this shape so the card can display them without cropping: a near-square
        /// cut-out dropped into a tall card otherwise runs off the bottom edge.
        /// </summary>
        private const float CardAspect = 300f / 440f;

        /// <summary>Clear space left above the head, as a fraction of canvas height.</summary>
        private const float HeadroomFraction = 0.02f;

        /// <summary>A pixel this bright, reached from the edge, is backing not artwork.</summary>
        private const float WhiteCutoff = 0.86f;
        private const float WhiteSoft = 0.70f;

        [MenuItem("Coup/Generate Card Art", priority = 10)]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            foreach (Role role in System.Enum.GetValues(typeof(Role)))
                GeneratePortrait(role);

            GenerateUIPanels();

            AssetDatabase.Refresh();
            Debug.Log("[Coup] Card art generated into " + OutDir);
        }

        // ------------------------------------------------------------- portraits

        private static void GeneratePortrait(Role role)
        {
            var src = LoadReadable($"{Characters}/Card_{role}.png");
            if (src == null) return;

            var bounds = FindContentBounds(src);
            if (bounds.width <= 0 || bounds.height <= 0)
            {
                Debug.LogWarning($"[Coup] Could not find card bounds for {role}.");
                return;
            }

            // Texture rows run bottom-up, so "the top of the card" is the high-y slice.
            // Keep the band running from TopInset down to KeepTopFraction, measured
            // from the top of the card: below the frame's top edge, above the caption.
            var inset = Mathf.RoundToInt(bounds.width * SideInset);
            var topInset = Mathf.RoundToInt(bounds.height * TopInset);
            var h = Mathf.RoundToInt(bounds.height * (KeepTopFraction - TopInset));

            var x = bounds.x + inset;
            var w = bounds.width - inset * 2;
            var y = bounds.y + bounds.height - topInset - h;

            var cut = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cut.SetPixels(src.GetPixels(x, y, w, h));
            cut.Apply();

            KeyOutBackingFromEdges(cut);

            var framed = ComposeToCardAspect(cut);

            SavePng(framed, $"{OutDir}/Portrait_{role}.png");
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(cut);
            if (framed != cut) Object.DestroyImmediate(framed);
        }

        /// <summary>
        /// Places the cut-out on a transparent canvas shaped like the card, anchored
        /// near the top. The figure then reads as a framed bust with the backdrop and
        /// scrim showing beneath it, instead of being sliced by the card border.
        /// </summary>
        private static Texture2D ComposeToCardAspect(Texture2D cut)
        {
            var canvasW = cut.width;
            var canvasH = Mathf.RoundToInt(canvasW / CardAspect);

            // A figure taller than the card shape widens the canvas instead, so it is
            // never scaled down or clipped.
            if (canvasH < cut.height)
            {
                canvasH = cut.height;
                canvasW = Mathf.RoundToInt(canvasH * CardAspect);
            }

            var canvas = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[canvasW * canvasH];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;

            var headroom = Mathf.RoundToInt(canvasH * HeadroomFraction);
            var xOffset = (canvasW - cut.width) / 2;              // centred horizontally
            var yOffset = canvasH - cut.height - headroom;        // textures run bottom-up
            yOffset = Mathf.Max(0, yOffset);

            var srcPixels = cut.GetPixels32();
            for (var y = 0; y < cut.height; y++)
            {
                var destY = y + yOffset;
                if (destY < 0 || destY >= canvasH) continue;

                for (var x = 0; x < cut.width; x++)
                {
                    var destX = x + xOffset;
                    if (destX < 0 || destX >= canvasW) continue;
                    pixels[destY * canvasW + destX] = srcPixels[y * cut.width + x];
                }
            }

            canvas.SetPixels32(pixels);
            canvas.Apply();
            return canvas;
        }

        /// <summary>Bounding box of everything that is not white page backing.</summary>
        private static RectInt FindContentBounds(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int minX = tex.width, minY = tex.height, maxX = -1, maxY = -1;

            for (var y = 0; y < tex.height; y++)
            for (var x = 0; x < tex.width; x++)
            {
                var c = px[y * tex.width + x];
                if (c.a < 8) continue;
                if (Brightness(c) >= WhiteCutoff) continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            return maxX < 0 ? new RectInt(0, 0, 0, 0)
                            : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Clears the white backing by flooding inwards from the border. Going from
        /// the edges rather than keying every white pixel keeps light details that sit
        /// inside the figure — the Ambassador's book, a bright collar — intact.
        /// </summary>
        private static void KeyOutBackingFromEdges(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var seen = new bool[w * h];
            var queue = new Queue<int>();

            void Seed(int x, int y)
            {
                var i = y * w + x;
                if (seen[i]) return;
                if (Brightness(px[i]) < WhiteSoft) return;
                seen[i] = true;
                queue.Enqueue(i);
            }

            for (var x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
            for (var y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }

            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                int x = i % w, y = i / w;

                // Fade rather than hard-cut, so anti-aliased edges stay smooth.
                var b = Brightness(px[i]);
                var alpha = b >= WhiteCutoff
                    ? 0f
                    : Mathf.InverseLerp(WhiteCutoff, WhiteSoft, b);
                px[i].a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * px[i].a);

                if (x > 0) Seed(x - 1, y);
                if (x < w - 1) Seed(x + 1, y);
                if (y > 0) Seed(x, y - 1);
                if (y < h - 1) Seed(x, y + 1);
            }

            tex.SetPixels32(px);
            tex.Apply();
        }

        private static float Brightness(Color32 c) =>
            Mathf.Min(c.r, Mathf.Min(c.g, c.b)) / 255f;

        // ------------------------------------------------------------ ui panels

        /// <summary>
        /// Flat 9-slice shapes. Tinting these keeps panels crisp at any size, which
        /// the ornate source art cannot do when stretched.
        /// </summary>
        private static void GenerateUIPanels()
        {
            SavePng(RoundedRect(96, 96, 26, filled: true, thickness: 0), $"{OutDir}/Panel_Rounded.png");
            SavePng(RoundedRect(96, 96, 26, filled: false, thickness: 4), $"{OutDir}/Panel_Border.png");
            SavePng(VerticalGradient(8, 128), $"{OutDir}/Scrim_Bottom.png");

            // Particle shapes for the UI effects layer.
            SavePng(RadialDot(64), $"{OutDir}/Vfx_Spark.png");
            SavePng(RadialRing(128, 0.78f, 0.1f), $"{OutDir}/Vfx_Ring.png");
        }

        private static Texture2D RoundedRect(int w, int h, int radius, bool filled, int thickness)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];

            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var d = RoundedRectDistance(x + 0.5f, y + 0.5f, w, h, radius);
                float a;

                if (filled)
                {
                    a = Mathf.Clamp01(0.5f - d);
                }
                else
                {
                    // Distance band gives an even outline on all four sides.
                    var ring = Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f;
                    a = Mathf.Clamp01(0.5f - ring);
                }

                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Signed distance to a rounded rectangle; negative is inside.</summary>
        private static float RoundedRectDistance(float x, float y, int w, int h, int radius)
        {
            var halfW = w * 0.5f;
            var halfH = h * 0.5f;
            var px = Mathf.Abs(x - halfW) - (halfW - radius);
            var py = Mathf.Abs(y - halfH) - (halfH - radius);
            var outside = new Vector2(Mathf.Max(px, 0), Mathf.Max(py, 0)).magnitude;
            return outside + Mathf.Min(Mathf.Max(px, py), 0) - radius;
        }

        /// <summary>A soft round particle: opaque centre falling off to clear.</summary>
        private static Texture2D RadialDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            var centre = (size - 1) * 0.5f;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                var a = Mathf.Clamp01(1f - d);
                a = a * a;   // tighter core, softer edge
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>A soft annulus, used for the expanding impact ring.</summary>
        private static Texture2D RadialRing(int size, float radius, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            var centre = (size - 1) * 0.5f;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                var band = 1f - Mathf.Clamp01(Mathf.Abs(d - radius) / Mathf.Max(thickness, 0.0001f));
                var a = band * band;
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D VerticalGradient(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];

            for (var y = 0; y < h; y++)
            {
                // Opaque at the bottom row, clear at the top.
                var t = 1f - (y / (float)(h - 1));
                var a = (byte)Mathf.RoundToInt(Mathf.Pow(t, 1.6f) * 255);
                for (var x = 0; x < w; x++) px[y * w + x] = new Color32(0, 0, 0, a);
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------------- io

        /// <summary>
        /// Reads the PNG straight off disk so the asset's own import settings
        /// (readable, compression) are irrelevant and left untouched.
        /// </summary>
        private static Texture2D LoadReadable(string assetPath)
        {
            var full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(full))
            {
                Debug.LogWarning($"[Coup] Missing source art: {assetPath}");
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(full));
            return tex;
        }

        private static void SavePng(Texture2D tex, string assetPath)
        {
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), tex.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureSprite(assetPath);
        }

        private static void ConfigureSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            // The generated panels are stretched, so they need 9-slice margins.
            var name = Path.GetFileNameWithoutExtension(assetPath);
            if (name.StartsWith("Panel_"))
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(30, 30, 30, 30);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            else if (name.StartsWith("Scrim_"))
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }
    }
}
