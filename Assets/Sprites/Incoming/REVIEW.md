# Art review log

Claude Code appends here after checking each dropped PNG. Codex reads this and
regenerates whatever is rejected. Newest entry at the top.

Checked on arrival: pixel dimensions (expect **1024x1536**), whether the alpha
channel is genuinely transparent outside the card silhouette, whether the
bottom 28% is clear enough for the TMP text block, and whether any text, number
or watermark got baked in (automatic reject).

---

## Pending

| File | Status |
|---|---|
| `Card_Captain_v2.png` | **awaiting** — not yet on disk |

Nothing has been dropped yet. The Captain is the pilot: once one file lands and
passes, the remaining four go in one batch.

### Why this is now blocking, not cosmetic

Playtest capture: `Assets/Screenshots/ui_review_before.png` (6 players, your
hand showing Ambassador and Duke).

The two cards in hand render at roughly 100x150 px on a 1080x1920 canvas. The
name and ability text baked into the art is unreadable at that size — it is
below about 6pt on screen. A player cannot read their own hand, which is the
one thing they must be able to read.

This cannot be fixed on the Unity side. The text is pixels in the PNG, so it
can't be restyled, resized or localised, and it doesn't scale with the card.
Text-free art is the fix: TMP then draws name and ability at whatever size the
card is actually displayed, and `CardView` already has the fields for it.

Priority order if you're generating in batches — hand cards are visible every
turn, so they matter most:

1. `Card_Duke_v2.png`, `Card_Ambassador_v2.png` — in hand in the capture
2. `Card_Captain_v2.png` — pilot, prompt already written
3. `Card_Assassin_v2.png`, `Card_Contessa_v2.png`

---

## Reviewed

*(empty)*
