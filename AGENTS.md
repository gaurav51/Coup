# Coup — agent working agreement

Unity **6000.6.5f1**, URP, TextMeshPro, new Input System only. Portrait mobile,
reference resolution 1080x1920.

Two agents work in this repo: **Codex** and **Claude Code**. Both generate
images, both drive the Unity Editor over MCP, and both edit code. Neither needs
the other's permission for any part of the job.

There is no live connection between them. The filesystem is the handoff:
`Assets/Sprites/Incoming/REVIEW.md` is the shared log, newest entry at the top.
Write what you did and what you verified; read it before starting.

## Who does what

Whoever the user is talking to does the work, end to end — generate the art,
import it, wire it up, verify it in Play mode, report in `REVIEW.md`. Don't
hand a task across unless the user asks for that, and don't leave work parked
for the other agent to finish.

An earlier version of this file reserved Unity for Claude Code and limited
Codex to dropping PNGs in `Incoming/`. That was wrong and it blocked real work.
It is gone; no part of the project is off-limits to either agent.

## The one thing to coordinate

The Unity Editor is a single shared mutable process, and both agents reach it
on `127.0.0.1:8080`. Two clients mutating the AssetDatabase, saving scenes or
triggering domain reloads at the same moment will corrupt prefabs and silently
drop edits, and a call landing mid-reload fails as what looks like an unrelated
bug.

So: **one agent in the Editor at a time.** The user decides who is active. If
you start a session and the other agent's last `REVIEW.md` entry doesn't read
as finished, ask before driving the Editor. This is a turn-taking convention,
not a permission system — nothing enforces it.

## Invariants, both sides

1. **Prefabs are hand-tuned.** Never regenerate or overwrite them from code or
   from outside Unity. Scale instances; don't rebuild sources. The scene is
   likewise authored — save it through the Editor, never by writing YAML.
2. **No text baked into generated art.** Names, ability text, numbers and
   watermarks are TextMeshPro's job, drawn at runtime from the `CoupArt` entry,
   so they stay crisp, restyleable and translatable. Art that states a number
   can also contradict the rules, which is how the reference mockups ended up
   disagreeing about Steal.
3. **Steal takes 2 coins.** `CoupSceneBuilder.cs` is authoritative for every
   ability string. Some reference art says 3; it is wrong.
4. **Import settings are automatic.** `Assets/Editor/CoupSpriteImportSettings.cs`
   is a postprocessor over all of `Assets/Sprites/`: sprite mode, alpha
   transparency, no mipmaps, clamp, plus a per-folder `maxTextureSize` and
   Android/iOS ASTC overrides sized to how wide the art is actually drawn. Drop
   a PNG and it is configured on arrival. Don't hand-edit `.meta` files; if a
   folder needs a different cap, change `CapFor` instead.
5. **Judge rendering at native resolution.** `manage_camera` writes the PNG at
   full size but returns a *downscaled* inline preview. Reading the preview as
   though it were the real thing invents problems that aren't there — check the
   file's real dimensions, and crop rather than squint.

## Accent colors — authoritative

From `Assets/Scripts/Game/Editor/CoupSceneBuilder.cs`:

| Role | Hex | Ability (TMP, not art) |
|---|---|---|
| Duke | `#7B3FA0` | Take 3 coins. Blocks foreign aid. |
| Captain | `#1E3A6E` | Steal 2 coins. Blocks stealing. |
| Assassin | `#1A1F38` | Pay 3 coins to assassinate. |
| Contessa | `#B52430` | Blocks assassination. |
| Ambassador | `#D4791E` | Exchange cards. Blocks stealing. |
