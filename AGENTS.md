# Coup — agent working agreement

Unity **6000.6.5f1**, URP, TextMeshPro, new Input System only. Portrait mobile,
reference resolution 1080x1920.

Two agents work in this repo. **The filesystem is the handoff — there is no live
connection between us.** Work lands as files; the other side picks it up.

| | Codex | Claude Code |
|---|---|---|
| Owns | image generation | Unity integration |
| Writes to | `Assets/Sprites/Incoming/` **only** | everything else |
| Reads | this file, `Assets/Sprites/Incoming/REVIEW.md` | the dropped PNGs |
| Unity Editor | **no access** — not even reads | UnityMCP |

## Codex — scope

Generate card and UI art, save as PNG into `Assets/Sprites/Incoming/`.
Prompts and per-character specs: `Assets/Sprites/Incoming/CODEX_PROMPTS.md`.

Name files `Card_<Role>_v2.png`. Never overwrite anything in
`04_Characters_And_Art_Reference/` — that's the approved pack.

**Do not touch** `Assets/Prefabs/`, `Assets/Scenes/`, `Assets/Scripts/`,
`Assets/Art/`, or `ProjectSettings/`. Those are hand-tuned or Unity-generated,
and edits from outside the Editor corrupt them. If something there looks wrong,
write it into `REVIEW.md` instead of fixing it.

**Do not drive UnityMCP** — see hard rule 4. Writing PNGs into `Incoming/` is
the whole job; the Editor picks them up on its own.

## Claude Code — scope

Picks up new PNGs, verifies them (dimensions, real alpha vs. opaque rectangle,
style match), wires sprites into `Assets/Art/CoupArt.asset` and the card
prefabs, and writes findings back to `Assets/Sprites/Incoming/REVIEW.md`.

Swaps **sprite references only** on prefabs. Never regenerates them.

## Hard rules, both sides

1. **No text baked into art.** No names, ability text, numbers, or watermarks.
   TMP draws all of it at runtime from `CoupSceneBuilder.cs:61`. The existing
   pack has text baked in, which is why the Captain art and the gameplay
   mockups disagree about Steal — the rule is **Steal 2 coins**, and art that
   states no number can't contradict it.
2. **Prefabs are hand-tuned.** Never regenerate or overwrite them from code or
   from outside Unity. Scale instances, don't rebuild sources.
3. **Import settings are automatic.** `Assets/Editor/CoupSpriteImportSettings.cs`
   is a postprocessor covering all of `Assets/Sprites/` — sprite mode, alpha
   transparency, no mipmaps, clamp. Drop the PNG and it's configured on arrival.
   Don't hand-edit `.meta` files.
4. **Only Claude Code drives UnityMCP.** `.codex/config.toml` points Codex at
   the same Editor bridge on `127.0.0.1:8080`, so both agents *can* reach it —
   but the Unity Editor is one shared mutable process. Two clients mutating the
   AssetDatabase, saving scenes, or triggering domain reloads at the same time
   corrupts prefabs and silently drops edits, and a call landing mid-reload
   fails in ways that look like unrelated bugs.

   Codex: don't call UnityMCP at all, reads included — a read during someone
   else's write is how you end up reporting state that was never true. Need
   something from the Editor (console errors, a prefab's layout, whether a
   sprite imported)? Ask in `REVIEW.md`.

## Accent colors — authoritative

From `Assets/Scripts/Game/Editor/CoupSceneBuilder.cs:61`:

| Role | Hex | Ability (TMP, not art) |
|---|---|---|
| Duke | `#7B3FA0` | Take 3 coins. Blocks foreign aid. |
| Captain | `#1E3A6E` | Steal 2 coins. Blocks stealing. |
| Assassin | `#1A1F38` | Pay 3 coins to assassinate. |
| Contessa | `#B52430` | Blocks assassination. |
| Ambassador | `#D4791E` | Exchange cards. Blocks stealing. |

## Handoff protocol

```
Codex   → writes Card_Captain_v2.png to Assets/Sprites/Incoming/
Claude  → verifies, imports, wires; appends verdict to Incoming/REVIEW.md
Codex   → reads REVIEW.md, regenerates what was rejected
```

Neither side waits on the other. Check for new files at the start of a session.
