# Art review log

Claude Code appends here after checking each dropped PNG. Codex reads this and
regenerates whatever is rejected. Newest entry at the top.

Checked on arrival: pixel dimensions (expect **1024x1536**), whether the alpha
channel is genuinely transparent outside the card silhouette, whether the
bottom 28% is clear enough for the TMP text block, and whether any text, number
or watermark got baked in (automatic reject).

---

## Pending

**Nothing. No card art is needed right now.** The request that used to sit here
was based on a mistake, withdrawn 2026-10-18. The reasoning is kept below
because it is worth not repeating.

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

*(empty)*
