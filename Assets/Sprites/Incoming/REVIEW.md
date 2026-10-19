## 2026-10-20 — Claude: HUD bars hug the screen on tall displays (finished)

Reported at 2960x1440 portrait: the header sat below the top of the screen with
table art showing above it.

The canvas scaler matches on width against a 1080x1920 reference, so a 2.06
aspect grows the canvas to 1080x2220 while the Stage stays a fixed 1920 tall,
centred. That leaves 150 units of slack at each end. TopBar and ActionBar are
anchored to the Stage, not the canvas, so both sat 150 units inside the screen:
header 163 from the top, action bar 200 from the bottom.

Both are now placed absolutely against the canvas edge, keeping the inset they
were authored with (13 and 50).

Absolute rather than nudged, and that distinction mattered. The first version
added the slack to a cached starting position; something captured an
already-shifted value and both bars ended up displaced by exactly twice the
slack. Caught by re-measuring rather than trusting the first reading. The
current form derives the target only from the canvas and the bar's own height,
so running it any number of times gives the same answer.

Verified at 1080x2220: header 13 from the top, action bar 50 from the bottom,
stable across frames with no drift. Opponent seats still inside from the
earlier fix.

Not exercised: the 1080x1920 reference aspect itself, where slack is zero and
the method early-returns, so nothing moves by construction.

---

## 2026-10-20 — Claude: opponent seats no longer hang off the canvas (finished)

Reported on a four-player table: the left and right opponent badges were cut
off at the screen edges.

`EnsureSeats` restores the authored positions only when there are exactly five
opponents, the number Game.unity has panels for. Any other count falls back to
`SeatSpots`, whose numbers were tuned when a seat was a narrow vertical plaque.
Codex's badge is 420 wide, so the three-opponent row at +-400 and the
four-opponent row at +-420 both pushed their outer chairs past a 1080 canvas.

Measured, not guessed: at four players the outer seats spanned -572..572
against a half-width of 540, so 32 units off each side.

The first attempt clamped on the panel's own RectTransform and was not enough —
the portrait halo is drawn outside the badge on its left, so the rect fitted
while the art still clipped, and the two outer chairs are not mirror images.
`KeepSeatOnStage` now measures the union of every active child's world corners
and shifts the seat by exactly its overflow, leaving a 22px margin.

Authored five-seat positions are deliberately left unclamped: they are placed
by hand and were verified to fit (-505..481). Those seats overlap heavily in x
and are separated by y, which a clamp has no business touching.

Verified per player count, one layout per frame because `_layoutSeatCount`
short-circuits within a frame and a single-frame loop reads stale numbers:
four players now sit at screen 22..399, 320..725 and 681..1058, symmetric 22px
margins; five players clamp from +-420 to -348/+375; six players use the
authored row untouched.

---

## 2026-10-20 — Claude: queue countdown on the waiting card (finished)

`queueDeadline` was already on the wire from the server change; nothing drew
it, so a player waiting out the window just saw a static search card.

The card now reads "FINDING PLAYERS / CASUAL · 1 of 4 seated" with a live
"Starting in 22s, or sooner if the table fills." below it. The target is 4 for
casual and 2 for ranked, matching what the server actually does, so the wait is
legible rather than mysterious.

The countdown is rewritten in `Pump`, which already runs every frame, because
`ShowLobby` only rebuilds when a `lobby:state` arrives and that is far too
rarely to count anything down. It stops as soon as `game:state` lands.

Verified live: queued casual, read "1 of 4 seated / Starting in 22s" off the
card at native resolution, let the window lapse, and watched it deal as
Player + Valca, Orsin, Meridia with the turn clock running at 9s.

---

## 2026-10-20 — Claude: casual queue window and a visible turn clock (finished)

Two asks. One of them already existed.

**Turn timer.** The server has had an authoritative one all along: 30s to act,
20s to answer, swept every second by `sockets.js`, with `deadline` and
`serverTime` already in `game:state` so the client can correct for clock skew,
and `OnlineSession.SecondsRemaining` already parsing it. What was missing was
visibility — it rendered as a text suffix on the turn label in the top bar, the
opposite corner from where anyone looks while reading their hand.

There is now a drain bar hugging the underside of the status plate and the
seconds top-right inside it, gold above ten, amber under ten, red and pulsing
under five. `OnlineSession` learns each window's length from the first sight of
a new deadline, because the server sends when a turn ends and never how long it
was, and the length differs by phase. Built in code, not authored into
Game.unity, so the change is one script and the scene is untouched.

**Casual queue.** This was genuinely missing: `quickJoin` paired the first two
humans and a lone player waited forever with nothing to tell them so. A casual
table now opens a 40s window and deals the moment a fourth human sits down;
if the window lapses first, bots take the empty seats up to four. The window
belongs to the table, so a late joiner never resets the wait for whoever was
already there. `lobbyView` carries `queueDeadline` and `serverTime`.

Ranked is deliberately untouched — it still pairs at two humans and seats no
bots, because `completeMatch` only rates an all-human table, so filling one
would make it unratable. That same rule is what stops bot fill being farmable.

Verified against the live server, not just in tests: queued casual from the
Editor, watched the window lapse, and the table dealt as Player + Valca, Orsin,
Meridia — one human, three bots, four seats. Then read the bar through a real
match and caught all three colour states (#D6B054 above ten, amber at nine,
#E05555 at five). Backend 57/57. Console clean.

Not covered: four real humans queueing at once was not tested, only the bot-fill
path and the unit test for the four-human start.

---

## 2026-10-19 — Claude: session cache for per-packet authentication (finished)

The socket middleware authenticates every packet, so a burst of commands was a
session read each. `Accounts.authenticate` now holds a verified session for five
seconds: twenty calls cost one read, asserted in the test rather than assumed.

The cache is the only thing between a revoked session and continued access, so
it is deliberately short and every revocation this process performs evicts the
entry outright — logout and account deletion in `http.js`, and the reuse
revocation in `refresh`. Those stay immediate; the window only bounds a
revocation performed by some *other* worker. Failures are never cached, and an
entry never outlives the token that proved it. The map is capped at 20,000,
evicting expired entries first and then the oldest insertion.

Backend suite 55/55, up one. The new test proves the read actually disappears,
then pins the tradeoff: a row deleted behind the cache's back is still served
inside the window, `forget` takes effect immediately, the window closes on its
own, and a replayed refresh token cannot ride the cache.

Not done: the running `com.coup.local-backend` service still has the previous
code. It needs a restart to pick this up; I did not restart a managed service
I did not install.

---

## 2026-10-19 — Claude: refresh-token reuse detection (finished)

Rotation alone only refused a replayed refresh token: the thief learned their
copy was stale while the real session kept running, and the orphaned row sat
there until its 30-day expiry.

`Accounts.refresh` now records each retired hash on the session, capped at ten
by `$slice`. A token presented after being spent proves two holders exist, so
the session row is deleted rather than merely refused. Destroying the row cuts
live access too: HTTP calls and the socket middleware both re-check the
sessions collection, the latter on every packet, so a connected socket drops on
its next command. Only that session dies; other devices keep their own rows.

Client half: `PlatformClient.AccessToken` treated a 401 from `/v1/auth/refresh`
as fatal and retried the same dead credentials forever, so a revoked or
expired session stranded the app on an error it could never clear. It now
drops them and opens a fresh guest session. Cleared inline rather than through
`Forget()` so no `Changed` handler runs while the refresh lock is still held.

Worth recording: revocation does not cost a legitimate client anything it was
not already losing. If a refresh response is lost in flight the server has
rotated and the only copy of the new token died with the response, so that
client was locked out either way. The change deletes the dead row instead of
leaving it, and cuts off anyone else holding the spent token.

Backend suite 54/54, up one: the existing rotation test now asserts the reuse
message, and a new test covers revocation of the session, the newest token and
the access token together, an untouched second device, and a never-issued token
being refused without revoking anything. Client recovery verified in Play mode
by poisoning the stored credentials — it opened a new guest session instead of
throwing. Console clean.

---

## 2026-10-19 — Claude: server URL made configurable (finished)

`PlatformClient.DefaultUrl` was a const holding the ngrok hostname, read in
three places and assigned nowhere, so every build was permanently bound to one
tunnel with no way to repoint it.

New `PlatformEndpoint` ScriptableObject resolves the address in three layers:
a `-coupServer <url>` argument or `COUP_SERVER_URL` variable (development and
QA only — release players ignore it, so a shipped build cannot be repointed);
then the `PlatformEndpoint` asset in Resources, which is the build-time
setting; then a compiled fallback of `http://127.0.0.1:3001`. Anything that is
not an absolute http/https URL is rejected with a warning rather than used.

The fallback is local on purpose: a build that lost its endpoint should fail
obviously against localhost rather than quietly reach whatever host happened to
be compiled in last.

`Assets/Resources/PlatformEndpoint.asset` was created through the Editor
holding the current ngrok URL, so today's behaviour is unchanged.

Verified in Play mode: the asset resolves, an env override replaces it and has
its trailing slash trimmed, an invalid override is ignored and falls back to
the asset, and clearing it restores the asset value. End to end the client
authenticated and loaded its profile from the live server. Console clean apart
from the deliberate bad-override warning. Backend untouched; its suite is
53/53.

---

## 2026-10-19 — Claude: panel sweep, text fit and input chrome (finished)

Walked Shop, Missions, Settings, Profile and Wallet in Play mode at 1080x1920,
cropped each at native resolution. Settings and Wallet needed nothing. Two
faults turned up that the Play page had not exposed:

`Input` never got the plaque treatment — the name field on Profile was a bare
square black box among rounded gold-rimmed rows, reading as a hole in the
panel. It now uses the same plaque, sunk darker with a dimmer rim so it still
looks like something you type into.

`Text` rows were a flat 120px whatever they held, so a single line left about
80px of slack. Harmless while the card padded everything out; obvious once the
card fitted its content. Rows now measure the wrapped text and fit it. Missions
went 968 -> 865, Profile 1298 -> 1246, and the gaps between a mission's label
and its Claim button closed.

Disabled rows verified: Shop's "Restore / retry pending purchases" and Missions'
"Claim reward" both grey their fill, rim and label. Every card came in under the
1480 cap, so all five size to content. Console: zero errors, zero warnings.
Returned to edit mode. One script; no scene, prefab or art touched.

Minor and not addressed: the input fill reads very slightly green against the
navy rows. Cosmetic.

---

## 2026-10-19 — Claude: platform panel chrome and fit (finished)

Redid the runtime modal panels built by `PlatformPanels.cs` — Play, Collection,
Shop, Missions, Settings and every other page share one builder, so this is all
of them rather than Play alone.

Two problems. The card height was a flat `min(1480, canvas*0.88)`, so the Play
menu's four rows (about 400px) sat in a 1480px card with a third of the screen
empty beneath them. And the chrome was flat fills with an `Outline` component,
which reads as programmer art beside the marble-and-gold lobby behind it.

The card and its rows now use `CoupArt.roundedPanel` / `roundedBorder`, the same
9-slice pair the card and player frames already use, via a `Plaque` helper.
`FitCard` sizes the card to its content between 560 and the old maximum, and the
status band is only reserved when status has text. Back is narrower and quieter
so it reads as a way out rather than a fifth option.

Verified in Play mode at 1080x1920, cropped at native resolution: Play fits
snugly with no dead space, Collection grows to the cap and scrolls with its
status text and card thumbnails intact. Console: zero errors, zero warnings.
Returned to edit mode. No scene, prefab or art changes — this is one script.

Not covered: the lobby screen behind the modals, and panels that need a live
backend were only exercised against whatever the local server returned.

---

## 2026-10-19 — Codex: Mac backend hosting (hosting finished; game platform still in progress)

Configured authenticated MongoDB backend on loopback 3001, persistent single-member MongoDB 8.0.17 replica set on loopback 27017, and stable ngrok HTTPS ingress at https://curable-twistable-rising.ngrok-free.dev. ngrok credentials were configured by the user; no token copied into source or this log. Request inspection disabled. Generated signing secret is in ignored .env.local (0600); persistent database/logs are in ignored .data (0700). Installed macOS login service com.coup.local-backend to start/restart the stack. Mac must stay awake and online. Existing legacy process on 3000 is not the ngrok upstream.

Verified 49 server tests; actual HTTPS and WSS guest authentication, invalid-token rejection, token refresh, starting a bot match, disconnect/resume with same private hand, and logout revocation. Restarted the managed stack and verified account/session persistence. Added hosting/check/install scripts, /ready lease+database check, and README instructions. No Unity scene/prefab changes in this hosting pass.

Not complete: Unity auth/reconnect integration and account/store screens, public matchmaking, production multiworker routing, configured Apple/Google app credentials/products, live provider and IAP tests, refund notification ingestion. This is Mac internet testing, not a production HA deployment or an AWS deployment.

---

## 2026-10-19 — Codex: header alignment correction (finished)

Header only: uniform 80px profile/currency/settings frames, equal 35px gaps, consistent icon/value/plus columns, inset smaller gem and coin, aligned profile left edges, raised level/progress baseline. Background and lower lobby unchanged. Checked native Play screenshot Assets/Screenshots/Lobby_Header_Aligned_Final.png and console (zero errors). Saved through Editor, returned to edit mode.

---

## 2026-10-19 — Codex: realistic lobby using current game cast (finished)

Latest user direction supersedes the comic exploration: return to realistic rendering and use current in-game cards. Active background is Assets/Sprites/02_Backgrounds/UI_Lobby_CurrentCast_Realistic_v6.png, generated with built-in imagegen from the current Captain, Contessa, Assassin cards and the actual table theme. Prompt: LOBBY_REALISTIC_PROMPT.md. No comic background is active.

The background pedestal is empty in the generated PNG. Hall/CurrentGameCards contains the exact CoupArt.cardFace sprite references for Contessa, Captain, Assassin, layered in Unity. Avatar is a Sprite crop referencing the existing Captain texture directly. Approved character files and prefabs were not overwritten. New generated art contains no text; any card text is from the pre-existing approved sprites.

Lobby now follows the supplied reference hierarchy: profile/coins/gems/settings, Season Pass and Ranked side panels, gold Play, Casual/Ranked, event banner, Collection/Shop/Missions. Header shares one centerline and regular gaps. Chest and crown were reduced and checked: all corners are inside their containers. Rajdhani fonts come from the current CoupArt asset; all visible action labels fit their text bounds after font metric adjustments. Play/Casual start local matches. Collection opens role information; Settings provides audio, friends and rules. New rank/reward/event/shop/currency/profile controls explain that their systems are not available yet; displayed economy/progression values are UI placeholders.

Verified actual card sprite identity for all three roles, icon containment, main navigation/modal callbacks and local scene launch during this session. Native final preview: Assets/Screenshots/Lobby_Current_Cards_Realistic_Final.png (1080x1920). Console: zero errors. Lobby saved via Editor; returned to edit mode with overlays hidden. No multiplayer connection or purchases tested.

---

## 2026-10-19 — Codex: lobby reference polish and working menus (finished)

User authorized direct Unity integration despite the older pasted scope agreement. Retained the existing text-free throne hall and real Play/Friends/This Device flows. Generated new transparent gold Play plaque, thin gold/navy plaque, and an optional reward chest with built-in imagegen. PNGs and LOBBY_PROMPTS.md are in Incoming. The chest is an optional asset, not wired to a nonexistent rewards system.

Integrated trimmed Sprite assets through the Editor; the main gold button preserves its aspect ratio, navy panels use sliced borders. Fixed obscured PLAYER header, tightened the bottom control layout and anchored header/footer groups to screen edges. Scene saved via Unity; no prefabs regenerated. Added Rules, Roles/Court, and Settings sheets. Audio toggle persists its mute setting. Labels remain Cinzel TMP; no baked text. No premium currency, ranked, shop or season functionality added.

Verified in Play mode: Play loads Game; Rules/Court/Friends sheets open, Back and dim close; modal Back receives the UI raycast; audio toggles and restores. Rules and roles report no text overflow. Checked portrait screenshots at 1080x1920 and tall 1440x2960. Console: zero errors. Multiplayer server connection was not tested. Final Lobby scene is saved in edit mode with sheets hidden. Main preview: Assets/Screenshots/Lobby_Final_1080.png.

---

# Art review log

Claude Code appends here after checking each dropped PNG. Codex reads this and
regenerates whatever is rejected. Newest entry at the top.

Checked on arrival: pixel dimensions (expect **1024x1536**), whether the alpha
channel is genuinely transparent outside the card silhouette, whether the
bottom 28% is clear enough for the TMP text block, and whether any text, number
or watermark got baked in (automatic reject).

---

## 2026-10-19 — Lobby chrome matched to the concept

Replaced the flat bars with the concept shapes: marble plaques with a gold rim, a swallowtail Play banner with sparkles, and a thin Court rule with diamond caps. Icons are a card fan, a laurel crown, a key, and a plain gear. The name plate tucks behind the portrait. Checked in the portrait Game view. Console: 0 errors.

## 2026-10-19 — Lobby matched to the throne-hall concept

The first scene pass put a dark band through the courtiers and sat Table and Court on their waists. The band is gone. Table and Court now sit on the marble under the cards, with a card stack and a crown, and the rest of the stack follows: Play, Friends, This Device, The Court, Rules, Roles, Settings. The header is a name plate, a gold coin reading 12, and a gear. Checked in the Game view at portrait size. Console: 0 errors.

## 2026-10-19 — Lobby scene

`Assets/Scenes/Lobby.unity` is the home screen, first in the build list, in front of Game. The throne hall is a text-free plate (`Sprites/02_Backgrounds/UI_Lobby_Hall.jpg`). Labels are Cinzel. Play and This Device load Game offline; checked in Play mode, the match deals and waits for the human (`online=False`, `AwaitingAction`). Friends opens a sheet to host or join with a code, then loads Game online. Return to Lobby on the game-over panel now loads this scene. Court, Rules, Roles and Settings are on the screen and do not navigate yet. Console: 0 errors.

## 2026-10-19 — Lobby concept, throne hall

Second pass after the waiting-room plate. The reference is a bright palace home screen: three courtiers at a round table, cards on a dais, gold PLAY banner, side plaques and a bottom row. Rebuilt in that layout with Coup's cast — Captain left, Contessa on the throne, a black-and-gold court figure right — and the real actions: Friends, This Device, Rules, Roles, Settings. Coin count is 12. No gems, season pass, or ranked tier.

`Assets/Sprites/Incoming/UI_Lobby_Concept_v2.jpg`. Concept only, type is baked in. Do not wire it as a sprite.

## 2026-10-19 — Lobby concept

Portrait waiting-room mockup for the online table, in the imperial chamber
language (navy marble, antique gold, night city, crimson/purple/amber banners).
Copy is baked in on purpose so the layout can be judged: COUP / THE COURT,
table code VELA, seats ARIA (host) and MARCUS (ready), two open seats, START
MATCH and PLAY HERE. Empty seats are the courtier fill the host already starts
with.

`Assets/Sprites/Incoming/UI_Lobby_Concept_v1.jpg` (720×1280). Concept only.
Do not wire it as a sprite — the letters have to become TextMeshPro, same rule
as the game-over plate.

## 2026-10-19 — Unused drops removed

Deleted Incoming review captures and rejected plates that nothing in the scene,
prefabs, or materials referenced, plus the duplicate confetti copies. Runtime
art stays in `Resources/GameOver/` and the sprites the scene still uses.

## 2026-10-19 — Win confetti is a Particle System

The foil pieces are no longer moved by `UiVfx`. `Assets/Resources/GameOver/WinConfetti.prefab`
is a Unity Particle System (ribbon, diamond, flake, spark) and the win panel
only calls Play. They render through a Confetti overlay camera so they sit on
top of the screen-space camera HUD. Checked in Play mode: the four cut-outs
fall and tumble across YOU WON.

## 2026-10-18 — Win confetti pieces

Generated four cut-out particles for the YOU WON rain and wired them into
`UiVfx.Confetti`: a gold foil ribbon, a teal-and-gold diamond, a gold flake,
and a four-point spark. Black was keyed out so they composite on the panel.
They drift down across the laurel and title with a slow spin, instead of the
old coin-and-triangle burst. Checked in Play mode: 28 live pieces, the ribbon,
diamond, flake and spark all drawing over YOU WON.

`Assets/Resources/GameOver/ConfettiRibbon.png` (226×433),
`ConfettiDiamond.png` (315×369), `ConfettiGold.png` (519×366),
`ConfettiSpark.png` (431×428). Copies in `Assets/Sprites/Incoming/`.

## Pending

Nothing.

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

### 2026-10-18 — Game Over matched to the YOU WON concept

Generated pieces from `UI_GameOver_YouWon_Concept_v1` and loaded them with `Resources.Load` so a missing scene reference cannot fall back to the plain card.

- `Resources/GameOver/WinBg.jpg` — sunburst, marble, gold frame, empty interior
- `Resources/GameOver/LoseBg.jpg` — quieter bronze frame
- `Resources/GameOver/Laurel.png` — concept wreath, real alpha
- `Resources/GameOver/BtnBlue.png` / `BtnGold.png` — concept button shells, black cut out

Portrait is a circular mask. Copy is TMP. Verified in Play: `UI_GO_ConceptMatch-1.png`.

### 2026-10-18 — Game Over rebuilt from separate components

User rejected the single composite plate (white hole, leftover slots). Generated one sprite per piece, no baked text, and assembled them in Unity:

- `UI_GO_WinBg.jpg` / `UI_GO_LoseBg.jpg` — panel backgrounds
- `UI_GO_Laurel.png` — gold wreath, real alpha
- `UI_GO_Ring.png` — portrait ring, real alpha
- `UI_GO_BtnNavy.jpg` / `UI_GO_BtnGold.jpg` — empty buttons
- `UI_GO_CircleMask.png` — circular portrait mask

`GameOverPanel` no longer uses the all-in-one wash plate. Portrait is a masked RawImage. Copy is TMP. Verified in Play: `UI_GO_Assembled_Win-1.png`, `UI_GO_Assembled_Lose.png`. Scene saved through the Editor.

### 2026-10-18 — Game Over panel plates generated

Generated text-free plates and empty button shells, then wired them to `UiVfx` / `GameOverPanel`. Copy stays TMP.

- `UI_GameOver_WinPlate.jpg` — gold sunburst, sculpted wreath, empty portrait hole, two stacked recesses
- `UI_GameOver_LosePlate.jpg` — muted bronze wreath, charcoal plate, same holes
- `UI_GameOver_ButtonPrimary.jpg` / `UI_GameOver_ButtonGold.jpg` — empty imperial buttons (plate recesses are what show)

Verified in Play at 1080x1920. Win: `UI_GameOver_WinPlate_Play-3.png`. Lose: `UI_GameOver_LosePlate_Play.png`. No baked letters. Console: 0 errors. Scene saved through the Editor.

### 2026-10-18 — Game Over rebuilt after empty-card miss

The first concept and the first Unity sheet were the same empty navy card with a stamp portrait. Replaced both. New win plate uses `UI_VictoryWash.jpg` (gold sunburst, ornate corners, no baked text) as a 960x1280 sheet, a 520px circular cut-out, laurel, and `WinSparkle` particles. Title is TMP "YOU WON". Loss drops the wash, shrinks the portrait, steel/crimson, no particles. Scene saved through the Editor.

Verified in Play at 1080x1920. Native title crop reads YOU WON / THE COURT IS YOURS. Screenshots: `UI_GameOver_YouWon_Rebuild-1.png`, `UI_GameOver_YouLose_Rebuild.png`. Console: 0 errors.

### 2026-10-18 — Game Over popup + YOU WON particles

Authored `GameOverPanel` under `Table/GameOver` and saved `Assets/Scenes/Game.unity` through the Editor. After the 1.45s victory crest, `TableView` holds 1.55s then opens the popup. Retry calls `MatchController.RetryMatch`; lobby calls `ReturnToLobby`. Copy is TextMeshPro. Steal is still 2 coins.

YOU WON is gold/cyan, circular winner portrait inside a generated gold laurel (`UI_VictoryLaurel.png`, real alpha, no baked text), and `UiVfx.WinSparkle`: rings, bloom, burst, rising sparks, coin flies, 6s opaque confetti, plus coins parked on the upper wreath. Overlay particles stay UI Images. YOU LOSE uses the same sheet in steel/crimson, muted wreath, no celebration.

Verified in Play mode at 1080x1920. Native title crop reads **YOU WON** / **THE COURT IS YOURS**; the downscaled preview can look like "YOUWONEL" because coins pass over the N. Lose capture: **YOU LOSE**, no particles. Screenshots: `UI_GameOver_YouWon_Play-1.png`, `UI_GameOver_YouLose_Play-1.png`. Console: 0 errors.

### 2026-10-18 — game-over victory VFX implemented

Imported the approved `VFX_VictoryBurst_v2.jpg` into `Incoming/` and assigned
it to the scene's `UiVfx`. Replaced the `GameOver` full-screen yellow/red wash
with `UiVfx.Victory`, keyed by `GameEvent.PlayerIndex`, so local and opponent
winners use the same path.

The crest uses the existing additive UI shader, pops from 0.35 to 1.1, settles,
pulses, and fades over 1.45 seconds. It plays together with a gold trail from
the winning seat, two expanding gold/cyan rings, a cyan bloom, and radial gold
sparks. The effect is clamped away from screen edges and does not tint or cover
the table.

Verified in Play mode using an opponent-seat source. The crest's black
background disappeared under additive blending and the combined effect rendered
over the opponent side. Screenshot: `Victory_VFX_Crest_Verified.png`. Unity
reported zero console errors; edited scripts have zero linter diagnostics.

Follow-up: the additive crest looked too translucent. Created
`VFX_VictoryBurst_v2_Alpha.png`, preserving the opaque dark medallion while
cutting out the exterior background, switched the crest to normal UI
compositing, and reassigned the scene reference. The crest is now solid while
the surrounding rings and sparks retain their glow. Verified in Play mode:
`Victory_VFX_Opaque_Verified.png`; zero console and lint errors.

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
