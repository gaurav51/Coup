# Audio sources

Every clip in this folder is **CC0 / public domain**. Credit is optional under
that licence, but the provenance is recorded here so nobody has to guess later,
and so a clip can be traced back to its pack if it needs replacing.

No attribution is required in the shipped game.

## Sound effects — Kenney (kenney.nl), CC0 1.0

| Pack | Used for |
|---|---|
| Casino Audio | card deal, flip, slam, shuffle, exchange, chips for coins and the coin actions |
| RPG Audio | coin handling, the assassin's blade |
| Interface Sounds | click, back, refusal, turn cues |
| Impact Sounds | coup, block, influence lost, the elimination bell |
| Music Jingles | the challenge stab, and the rising / falling pizzicato for a challenge won or lost |

<http://creativecommons.org/publicdomain/zero/1.0/> — "You may use these assets
in personal and commercial projects. Credit would be nice but is not mandatory."

## Music — randommind, via OpenGameArt, CC0 1.0

| Track | Used for |
|---|---|
| Medieval: King's Feast (`Loop_Kings_Feast_0.wav`) | `LobbyTheme` — used whole, it already loops |
| Medieval: Exploration (`Exploration.wav`) | `TableTheme` — a 58s section from 0:36, crossfaded into a loop |
| Medieval: Victory Theme (`victory.wav`) | `Victory` — the opening 7s, faded |
| Medieval: Defeat Theme (`defeat.wav`) | `Defeat` — the opening 8.5s, faded |

<https://opengameart.org/users/randommind>

## What was done to them

Clip files are the source of truth; nothing here is authored by the game. Every
cue clip was levelled to its mix target by **Coup/Rebuild Audio Library**, which
rewrites it as a 16-bit WAV — see `CoupAudioLibraryBuilder.cs` for the targets.
That is the only edit: no filtering, pitching or layering. The originals are a
re-download away from the links above.
