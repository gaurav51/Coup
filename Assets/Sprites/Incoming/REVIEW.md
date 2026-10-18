# Art review log

Claude Code appends here after checking each dropped PNG. Codex reads this and
regenerates whatever is rejected. Newest entry at the top.

Checked on arrival: pixel dimensions (expect **1024x1536**), whether the alpha
channel is genuinely transparent outside the card silhouette, whether the
bottom 28% is clear enough for the TMP text block, and whether any text, number
or watermark got baked in (automatic reject).

---

## Pending

### 2026-10-18 — replace yellow game-over wash

The user wants the solid yellow screen effect at game over replaced with an
actual victory VFX. They approved the generated gold-laurel/cyan-star crest at:

`/Users/gaurav-bs/.cursor/projects/Users-gaurav-bs-Coup/assets/VFX_VictoryBurst_v2.jpg`

Please import that image as the centrepiece using an additive material so its
pure-black background disappears, then combine it with the existing transparent
imperial ring/spark VFX:

- trigger this for every `GameOver` event, regardless of whether the winner is
  the local player or an opponent; use `GameEvent.PlayerIndex` as the winning
  seat;
- centre the effect on the winning player/profile (screen centre is an
  acceptable fallback);
- pop the crest from 0.35 scale to 1.1, settle at 1.0, then fade;
- expand two thin gold rings at slightly different speeds;
- punch a cyan-white light at the crest's centre and emit a restrained radial
  burst of gold sparks;
- add a brief upward sparkle trail from the winning profile to the crest;
- keep the table visible throughout — no opaque overlay or full-screen colour
  wash;
- finish in roughly 1.2–1.5 seconds and do not block the result controls after
  the animation completes. The image and procedural VFX should play together.

The withdrawn request below is retained because its reasoning is worth not
repeating.

### Withdrawn: "the baked-in card text is unreadable"

The claim was that cards in hand showed art with captions baked into the PNG,
far too small to read, and that only text-free art could fix it. Both halves
were wrong.

**The game never renders `Card_*.png`.** `CardView` composites
`Assets/Art/Generated/Portrait_<Role>.png` (a cut-out) over its
`Backdrop_<Role>.png`, then draws name and ability with TextMeshPro from the
`CoupArt` entry. All five portraits exist. The composites in
`04_Characters_And_Art_Reference/` are reference material, not runtime assets,
so the captions baked into them never reach the screen at all.

**The text was never too small.** The measurement came from a 506px-wide
preview of a 1080px-wide canvas, understating every size by about 2.1x.
Cropped from the same capture at native resolution, "CAPTAIN / Steal 2 coins. /
Blocks stealing." is crisp and comfortably legible. `CoupPrefabBuilder`
auto-sizes the name 9-34pt and the ability 7-21pt, and at the hand's display
width it lands near the top of those ranges rather than the floor.

**Lesson for anything judged from a screenshot here:** check the file's real
dimensions first. `manage_camera` writes the PNG at full resolution and returns
a *downscaled* inline preview. Reading the preview as though it were the real
thing invents problems that aren't there — and a "fix" for an imaginary problem
can introduce a real one, which is exactly what happened to the seat names in
`8aa3c42`.

---

## Reviewed

### 2026-10-18 — imperial reference restyle integrated by Codex

User supplied Futuristic Imperial Strategy Game Table.png and requested that visual direction after rejecting stretched bottom backgrounds. Integrated a generated empty imperial table background at `02_Backgrounds/UI_ImperialTable_v2.png`. Replaced stretched art with native clipped-corner FrameFill/FrameLine sprites using 28px nine-slice borders. Added circle masks and rings around decorative seat portraits, horizontal live player badges, cyan/red/gold action frames, compact sans-serif HUD typography, a shared action console and two live influence markers.

Scene authored and saved through Unity; prefab sources and approved PNGs preserved. Runtime layout/readout changes are in PlayerPanelView, TableView and ActionButtonView. Ability rules remain unchanged: Steal takes 2 coins. Reference art's extra influence diamond and Steal 3 copy were not copied. Latest gameplay screenshot: `UI_Imperial_Final_Play-1.png`. Built-in image prompt and geometry notes: `UI_ImperialTable_v2_PROMPT.md`.

Verified six-player layout at 1080x1920 with zero name truncation and no console errors. Steal target selection activates all five opponents. The editor-side pointer probe reached six controls; Coup's center lies outside the editor Screen.width returned to execute_code, even though the Game view target and canvas are 1080x1920. Do not count that probe as a full device touch test. Selected the existing portrait Game view preset. No Claude handoff.

### 2026-10-18 — player panels and bottom backgrounds completed by Codex

Generated `UI_PlayerPanel_v2.png` with the built-in image tool: compact empty midnight-navy plaque, thin antique-gold art-deco frame, clipped corners, real transparent exterior, no text or icons. Integrated through Unity in all five opponent scene panels, the local You/coins HUD, and a new non-raycasting DockBackground behind all seven actions. Prefab sources were preserved.

Updated PlayerPanelView.LayoutReadout so the name, initial and coin count stay within the opponent frame; names auto-size with an ellipsis fallback. Saved scene through Unity. Verified the six-player game at 1080x1920: all five names render without truncation, card positions remain unchanged, and the console reports no errors. Final screenshot: `UI_Panels_Final_Play.png`. Returned to edit mode. No handoff is pending.

### 2026-10-18 — notification banner integrated by Codex

User explicitly requested Codex handle integration directly. Replaced the sprite on `Canvas/Table/Stage/StatusPlate/Bg` in `Assets/Scenes/Game.unity` through Unity MCP with `UI_NotificationBanner_v2.png`. Changed StatusTitle to warm gold and Status body to cream for contrast. Preserved all RectTransforms and prefab sources. Saved the scene through Unity.

Verified in Play mode at **1080x1920**: the live Your turn heading and instruction fit the banner and remain readable; cards and action controls render correctly. Console error query returned zero entries. Returned Editor to edit mode. Screenshot: `UI_Review_After_Play.png`. Play mode populated dynamic TextMeshPro font atlases, which Unity also marked changed. No source-script edits were required; pre-existing sprite importer changes were left intact.

Artwork is **2185x720 RGBA**, with genuine alpha (657,370 fully transparent pixels) and no baked-in text. Built-in image generation prompt: `UI_NotificationBanner_v2_PROMPT.md`. Imported sprite uses the existing automatic importer settings. This is a UI banner, so card-only size requirements do not apply.

Completed directly; no integration work is pending for Claude. Rules/settings/response dialogs were not part of this banner verification.
