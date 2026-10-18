# Codex card-art prompt pack

Paste these into Codex. Ask it to **write the PNGs into this folder**
(`Assets/Sprites/Incoming/`) rather than only showing them in chat — Codex has
this repo as a project, so it should be able to save directly.

Naming: `Card_<Role>_v2.png` — do not overwrite the existing
`04_Characters_And_Art_Reference/Card_*.png` until the new ones are approved.

---

## Shared style preamble

Prefix every prompt with this, and attach
`Assets/Sprites/04_Characters_And_Art_Reference/Card_Duke.png` as a style
reference image:

> Collectible card game card face, 1024x1536 portrait (2:3). Match the attached
> reference's art direction exactly: photoreal digital painting of a character
> bust, dramatic side key light, soft radial glow behind the figure in the card's
> accent hue, over a thin angular art-deco frame in gold and the accent hue with
> notched corners and a small shield plate in the top-right holding a single flat
> white glyph. Background outside the card silhouette must be fully transparent.
>
> **Render absolutely no text, letters, numbers, captions, or watermarks
> anywhere on the image.** Leave the bottom 28% of the card as uncluttered
> drapery/shadow in the accent hue — a text layer is composited there at runtime.

---

## Per character

| Role | Accent | Glyph (top-right plate) | Character brief |
|---|---|---|---|
| **Duke** | `#7B3FA0` | crown | Imposing bald statesman, late 50s, heavy velvet-and-brocade mantle, gold collar clasps. *(reference card — regenerate only if you want it text-free)* |
| **Captain** | `#1E3A6E` | anchor or crossed sabres | Weathered naval commander, high-collared deep-navy greatcoat with gold braid and epaulettes, sea-wind hair. |
| **Assassin** | `#1A1F38` | dagger | Hooded figure, face mostly in shadow with one lit eye, matte black layered leathers, faint cold rim light. |
| **Contessa** | `#B52430` | shield or rose | Regal woman, crimson silk gown, ornate gold choker and headpiece, composed defiant expression. |
| **Ambassador** | `#D4791E` | scroll or twin masks | Shrewd diplomat, amber-and-gold embroidered robes, layered chains of office, knowing half-smile. |

Build each prompt as: *shared preamble* + *"Accent hue `#XXXXXX`. Glyph: X.
Character: …"* from the row.

---

## Why text-free matters

The ability text already lives in code, in
[CoupSceneBuilder.cs:61](../../Scripts/Game/Editor/CoupSceneBuilder.cs#L61):

```
Role(Core.Role.Duke,       "Duke",       "Take 3 coins.\nBlocks foreign aid.", "#7B3FA0", ...)
Role(Core.Role.Captain,    "Captain",    "Steal 2 coins.\nBlocks stealing.",   "#1E3A6E", ...)
Role(Core.Role.Assassin,   "Assassin",   "Pay 3 coins to assassinate.",        "#1A1F38", ...)
Role(Core.Role.Contessa,   "Contessa",   "Blocks assassination.",              "#B52430", ...)
Role(Core.Role.Ambassador, "Ambassador", "Exchange cards.\nBlocks stealing.",  "#D4791E", ...)
```

TMP is the single source of truth for rules text. Baking numbers into art is
what produced the existing Steal 2-vs-3 mismatch between the card art and the
gameplay mockups — keeping art text-free makes that class of bug impossible.

---

## After you drop the PNGs here

Tell me and I'll handle the Unity side:

- Texture Type `Sprite (2D and UI)`, Sprite Mode `Single`, Alpha Is Transparency on
- Mip maps off, appropriate max size and compression for mobile
- Wire the sprites into `Assets/Art/CoupArt.asset`
- Point the card prefabs at the new sprites — **swapping sprite references only**,
  never regenerating the hand-tuned prefabs
