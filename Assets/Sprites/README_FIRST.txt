COUP-INSPIRED PORTRAIT GAME — UNITY ART PACK
============================================

WHAT TO IMPORT FOR A PLAYABLE UI
--------------------------------
01_Individual_Sprites/  -> independent transparent PNG sprites with no embedded words or numbers.
02_Backgrounds/         -> full-screen portrait backgrounds.
03_Original_Text_Free_Sheets/ -> untouched high-resolution source sheets (some contain MULTIPLE items).
04_Characters_And_Art_Reference/ -> character card artwork from previous work. THESE STILL INCLUDE CARD TEXT.
05_Gameplay_Mockups_Reference/ -> flattened reference screenshots. THESE INCLUDE UI TEXT.

UNITY SETUP
-----------
1. Drag PNG sprites in 01_Individual_Sprites and 02_Backgrounds into Assets/UI.
2. For each independent PNG, Texture Type: Sprite (2D and UI); Sprite Mode: Single.
3. Texture Alpha Is Transparency: On; Compression: choose best fit for target mobile device.
4. Use Image components under a portrait Canvas with Canvas Scaler = Scale With Screen Size;
   reference resolution 1080 x 1920.
5. Add all button names, player names, coin amounts, deck counts, and round status via TextMeshPro.
6. Place player avatars under the transparent profile frame / mask as needed.
7. For actual gameplay, instantiate Card_Back and Single_Gold_Coin as separate GameObjects;
   don't animate precomposed deck/coin pile renders as if they were separately moving items.
8. Button art is still raster art. For responsive resizing, use Image Type = Simple until
   designing pixel-perfect 9-slice assets. Some ornaments may distort when stretched.

NOTES / LIMITATIONS
-------------------
- This package includes individually cropped sprites from the text-free transparent source sheets.
- The final frame's small inset cutout is a best-effort pixel mask, not an original vector opening.
- AI-created imagery contains subtle halo/shadow around some sprite edges.
- Character portraits and reference gameplay screenshots are separate from the text-free sprites
  because their captions were baked in when they were generated.
- This is artwork, not a complete Unity project or coded game.

Use the JSON sprite_manifest for dimensions and file inventory.
