## 2026-10-24 — Claude Code: tutorial layout fixed to the backdrop frame (finished)

User reported the tutorial UI broken at 1080x2400. Causes, all in GameTutorial.cs:
the illustration was stretched vertically by a non-uniform localScale (cards,
diamonds and captions about 35% too tall); Backdrop.png was stretched to the
screen, and the guide ignored the gold frame painted into it, so the heading,
explanation and nav buttons overlapped the frame and its corner flourishes; the
body text was taller than its plaque; tiles on Coins overlapped; the plaque art
was cropped in a 1280px space on a 1254px atlas and stretched without slicing.

Runtime code only. The backdrop now covers the screen without distortion, and the
guide is laid inside the frame's measured interior (intersected with the safe
area), clear of the corner flourishes. Skip sits under the crown band. Heading,
gold button and navy plaques are 9-sliced from exact atlas rects. Navy plaques
draw an unstretched marble crop under a fillCenter=false frame, because the
plaque's thin centre streaked when stretched. The explanation plaque is sized
once to the longest body text. Illustrations scale uniformly. Accent outlines
sit outside tiles. BACK dims on page 1.

The ornament atlas was importing at 1024px: the Tutorial CapFor line was added
after its import. Forced a reimport so the postprocessor applied 2048 (native
1254). No .meta hand-edits.

Verified in Play mode at native 1080x2400 and 1080x1920. All six pages: no TMP
overflow, body at full 38pt, content measured inside the frame and between the
heading and explanation. Start Playing closed the tutorial and restored
timeScale=1. Console zero errors/warnings. Removed the Coup.TutorialSeen key that
QA set; it was absent before. Captures/Tutorial_Before_*.png,
Tutorial_Fixed_*.png, Tutorial_Fixed_Overview.png, Tutorial_Before_After.png,
Tutorial_Fixed_Target_1920.png. Not tested on a device or with a notch safe area.
In one earlier session the tutorial buttons lost their onClick listeners. It did
not reproduce, and no game code clears them.

## 2026-10-24 — Codex: illustrated tutorial implemented (finished)

Implemented approved tutorial concept as six runtime pages using existing card
prefab instances, existing action icons and TMP copy. Generated text-free navy/
gold palace backdrop with built-in imagegen; saved under
Assets/Sprites/02_Backgrounds/Resources/Tutorial/Backdrop.png. Prompt recorded in
Documentation/Art/TutorialBackdrop.md. No authored prefab or scene regenerated.

GameTutorial now has safe-area fitting, consistent navigation and progress,
Duke/Tax and Ambassador/Exchange colored connections, visual costs and challenge/
block examples. Target page has tappable demo profiles cloned from live public
portrait masks, pulsing borders honoring reduced motion, and feedback; it does
not submit actions or spend coins. Practice pauses; completion/skip restores time.
Online remains unpaused and is explicitly labeled. Existing replay entry retained.

Verified all six pages in Play mode; screenshots checked at native 1080x1920 and
1080x2400. Text overflow scan across six pages found none. Demo click unchanged
coins; completion and skip restored timeScale=1. Fixed unsupported diamond glyph
using UI geometry and verified actual opponent portraits. Console zero errors.
Captures/Tutorial_Illustrated_Cards.png, Tutorial_Illustrated_Coins.png,
Tutorial_Illustrated_Challenge.png, Tutorial_Illustrated_Blocks.png,
Tutorial_Final_Influence_2400.png, Tutorial_Target_Verified_2400.png.
Stopped Play, reset tutorial-seen preference to 0. No APK rebuild/device test.

## 2026-10-24 — Codex: visual hand/action association (finished)

User rejected YOUR CARD text as a substitute for visual communication. Removed
that label entirely. Supported legal actions now use the role accent for a
pulsing outer rim, brighter border and tinted fill; matching hand cards have the
same accent outline. A small matching card image sits beside the full-size
original action icon. Duke/Tax purple, Ambassador/Exchange orange; other roles
use CoupArt accents. Unowned actions remain available as bluffs and affordability
still governs disabled state. Updated tutorial/caption to explain visual cues.
Reduced motion keeps a steady action outline. Runtime changes only.

Verified native1080x1920 six-player Duke+Ambassador fixture in Play mode:
Tax/Exchange visually matched hand cards, no YOUR CARD labels remain (count0).
Inspected Captures/Visual_Card_Action_Link.png. Compile/runtime console zero
errors. Stopped Play; tutorial seen preference reset for first-run presentation.
Previous APK does not contain this latest visual revision; no rebuild this pass.

## 2026-10-23 — Codex: onboarding APK build finished

Android buildd9344cb686 succeeded0errors/18warnings. Updated
Builds/Coup-Veiled-Court-Mobile.apk includes tutorial, owned-card action hints,
and explicit target selection. No device installation/validation in this pass.

## 2026-10-23 — Codex: tutorial and clearer action targeting

Added replayable six-page visual GameTutorial: influence/objective, card-supported
actions versus bluffs, costs, opponent targeting, challenge consequences, blocks.
Play -> Learn to play starts guided local practice; first local practice also
shows it automatically. Rules opens Tutorial or Rules & Roles. Practice pauses
and restores its prior timescale; online tutorial explicitly warns live match
continues. Back/Next/Skip/Start Playing controls; relevant UI gets a gold frame.
Opening tour waits for card-deal presentation. Runtime UI only, no prefab/YAML
regeneration. QA preference restored to unseen after checks.

ActionButtonView adds gold YOUR CARD badge/border using ONLY human hidden hand
and Rules.ClaimedRole. Duke+Ambassador fixture marked exactly Tax+Exchange;
unsupported role actions remain legal bluffs, and insufficient-coin constraints
remain unchanged. Your-turn caption explains the distinction.

Assassinate/Steal/Coup replace bottom actions with large eligible opponent name
buttons (3+2 for six players), plus explicit Cancel. Original portrait tapping
still works, with stronger gold pulse honoring reduced motion. Assassinate
status explicitly says choose a profile/name. Illegal targets revalidated before
submission; targeting clears when turn/phase changes. No new shader dependency:
lightweight UI tint/pulse effects preserve mobile rendering cost.

Play QA 1080x1920 six-player: five target buttons + cancel; cancel left coins5
and restored actions; select seat1 spent3 (coins2) and entered AwaitingChallenge
with correct pending target. Tutorial all six pages, completion closed overlay
and restored timeScale1. Final compile zero errors. Captures:
Tutorial_Portrait_Checked.png, Hand_Supported_Actions.png,
Target_Selection_Final.png, Tutorial_Last_Step.png. Final target styling inspected.
Stopped Play mode; Android build pending (see follow-up). Device FPS/audio not
measured. Coup Editor launched separately; chill-games Editor not modified.

## 2026-10-22 — Codex: voice-enabled APK ready

Android build84575f8d5e succeeded,0errors/18warnings,~78MiB APK at
Builds/Coup-Veiled-Court-Mobile.apk. Includes Vivox, table voice controls and all
latest lobby/table layout changes. No phone connected, so not installed by USB.
Live editor channel join/leave verified as described below; two-device audible
voice test remains pending. Backend public /ready healthy.

## 2026-10-22 — Codex: Vivox provisioned and live channel verified

After user signed in, enabled Voice and Text Chat for their dashboard Coup
project93eafc2d-b8b9-47e9-8881-301edd19ea24 (organization13469040852641), production
environment. Optional Safe Text unchecked. Setup complete and overview Vivox on
verified; proof Captures/Vivox_Enabled_Overview.png. Existing Editor cloud link
0ebff122... left intact: custom Vivox credentials are passed explicitly by SDK.

Stored connection settings/signing key ONLY in ignored server/.env.local with
mode0600, never client assets/review log. Enabled Reject UAS tokens: server-minted
VAT required for channel access. Restarted persistent local backend using
install-local-service.mjs; public HTTPS /ready healthy with authenticated Mongo.

Real Play-mode test with authenticated table: Vivox IsLoggedIn=true and one
active audio channel, participant Player(You), microphone remains muted. Speaker
toggle updates SOUND:OFF. Leave reaches logged=false/channels0; rejoin reaches
channels1. Returning to Lobby reaches logged=false/channels0. Repeated join
AFTER enabling Reject UAS tokens also successful. Final console zero errors.
No microphone capture requested and no two-device speech/hearing verification.
USB adb inventory empty. Android APK build84575f8d5e running; see follow-up result.

Tool caveat discovered: repeated identical execute_code snippets can return
stale-looking values; unique verification strings gave current SDK state.

## 2026-10-22 — Codex: table voice UI and Vivox integration (provisioning pending)

User chose opt-in current-table voice and asked Codex to enable the service.
Installed official com.unity.services.vivox16.12.1 via Package Manager; added
Core/Vivox assembly references. TableView attaches TableVoiceChat at runtime.
VOICE opens animated panel with join/leave, microphone, speaker controls and
participant/speaking list. Starts muted, microphone permission requested only
on unmute, backgrounding mutes mic, scene exit logs out. SDK operations serialized
across instances; server token provider never includes a secret in the client.
Android Gradle postprocessor adds mic/audio permissions and defers launch-time
permission dialog; iOS microphone purpose string set through Editor.

Server voice:access socket handler derives user and channel from authenticated
current seat. Channel hash includes code+creation timestamp; 90s HMAC login/join
tokens only, no arbitrary client target/channel/action. Server needs VIVOX_SERVER,
VIVOX_DOMAIN, VIVOX_ISSUER, VIVOX_SIGNING_KEY environment config. README updated.

Verified compile zero errors; node voice+socket tests6/6 passed. Play mode panel
capture Captures/Table_Voice_Controls.png at1080x1920 inspected. Offline join
shows unavailable, mic stays disabled. Returning to Lobby cleanup no errors.
Transient Unity Inspector preview MissingReference exception from selected
image destroyed on scene change was cleared by deselecting; not game code.
Stopped Play mode in Lobby. No APK rebuilt, no microphone recording/audio test.

BLOCKER: Unity Cloud in-app browser redirects to sign-in. User was asked to sign
in there; tab marked handoff. No Vivox cloud service enabled, no credentials
provisioned, no server restart/deployment. Live two-device communication not yet
verified; do not claim voice is live. Continue provisioning after user signs in.

## 2026-10-22 — Codex: tighter 1920 lobby spacing (finished)

Reduced compact endpoint gap40 ->22 reference units at safe height1920 per user.
Tall endpoint55 at2400 unchanged. Dependent rows/logo update automatically.
Verified Play capture Captures/Lobby_1920_Tighter.png at1080x1920, no compile
errors. Stopped Play mode. No APK rebuild.

## 2026-10-22 — Codex: responsive lobby gaps (finished)

Replaced fixed tall-screen row coordinates with height-aware gaps: 40 reference
units at safe height1920, interpolating to55 at2400. Each row derives from the
previous row's half-heights plus gap, preserving button dimensions. Logo minimum
center derives from feature row top + gap + logo half-height, preventing compact
layout crowding. Safe-area bottom anchoring and LobbyMotion rest positions kept.

Selected existing GameView QA presets and inspected native captures
Captures/Lobby_Adaptive_Short.png (1080x1920) and
Captures/Lobby_Adaptive_Tall.png (1080x2400). Both show distinct row spacing,
logo clearance and bottom shortcuts inside screen. Console zero errors. Stopped
Play mode. No APK rebuild/device verification.

## 2026-10-22 — Codex: more lobby padding (finished)

Further increased row gaps to approximately 50–58 reference units: Event220,
Casual/Ranked380, Play575, SeasonPass/RankedTier820; shortcuts remain73. Expanded
Actions band1000 and minimum logo center1080. Verified Play capture at
1080x2400, Captures/Lobby_Button_Padding_More.png. No compile errors. Stopped
Play mode; no APK rebuild.

## 2026-10-22 — Codex: lobby button spacing (finished)

Increased vertical gaps in LobbyResponsiveLayout: bottom shortcuts y73,
Event198, Casual/Ranked338, Play510, SeasonPass/RankedTier730. Previously upper
rows were y182/305/455/655; gaps now approximately 28–35 reference units.
Actions band height900, minimum logo center990 to clear the expanded stack.
Uses LobbyMotion.SetRestingPosition to preserve spacing through entrance/idle
animations, retaining authored horizontal anchors and sizes. No prefab edits.

Verified Play-mode native 1080x2400 capture Captures/Lobby_Button_Padding.png:
visible row gaps, safe bottom margin, logo above cards. Compilation console zero
errors. Stopped Play mode. No APK rebuilt.

## 2026-10-22 — Codex: piles immediately above turn bar (finished)

Per user placement request, draw/discard center follows StatusPlate y +185 on
compact screens (+260 tall), with x +/-70 compact / +/-120 tall. Captions stay
between the decks and turn bar. Coin pile enlarged .24 -> .42 compact (.55 tall)
and placed above the shared decks (+185 compact / +245 tall). All use bottom
anchors and respond to safe-area layout changes. Prefabs untouched.

Verified six-player 1080x1920 Play mode; screenshot
Captures/Piles_Above_Turn_Bar.png inspected. Coin, draw, discard root bounds each
have zero intersections with active CardViews. Final console zero errors.
Stopped Play mode. No APK rebuild or device verification in this pass.

## 2026-10-22 — Codex: coin pile restored (finished)

Removed compact-screen coin-pile hiding from TableResponsiveLayout. Compact
screens show it at .24 scale, centered 170 units above draw/discard, in the gap
below the top opponent cards. Taller layouts retain existing placement/scale.
Verified six-player Play mode at 1080x1920: active=true, scale=.24, zero coin/card
bounds intersections. Inspected Captures/Coin_Pile_Restored.png. Compilation
console zero errors. Stopped Play mode; no APK rebuild.

## 2026-10-22 — Claude Code: audio replaced with CC0 recordings (finished)

User heard the synthesised pass from the previous entry and said it was bad.
They were right and I had no way to know: I can measure that a clip is valid
audio, not that it sounds good. Replaced every sound with real CC0 recordings.

Downloaded, all CC0, commercial use fine, attribution optional:
seven Kenney packs (casino, rpg, interface, ui, impact, digital, music jingles)
and four tracks from randommind on OpenGameArt. Provenance and what was done to
each file is recorded in **Assets/Audio/CREDITS.md**. Nothing here is licensed
music or a sample that needs clearing.

Mapping is by what the recording is, not by vibes: card-slide for a deal,
chips-collide for a steal, drawKnife for the assassin, impactBell_heavy for an
elimination. Two picks could not be made from a filename — which pizzicato
jingle rises and which falls — so all thirteen candidates were imported and
measured through AudioClip.GetData; PIZZI16 rises 407->757 Hz and PIZZI00 falls
857->311 Hz, so they became ChallengeWon and ChallengeLost.

Music: King's Feast loops as-is for the lobby. Exploration is a 3:54 piece, so a
58s section from 0:36 was cut and crossfaded into a loop — seam 0.0041 against a
mean sample step of 0.0134, measured. Victory and Defeat themes trimmed to 7s
and 8.5s stings with fades.

**CoupAudioGenerator.cs is deleted.** Its menu item would have overwritten the
new clips with the synthesised ones. In its place CoupAudioLibraryBuilder.cs
(**Coup/Rebuild Audio Library**) scans Assets/Audio, claims each file by its
name — Coup.wav, CardDeal_1.wav — and builds the library. Swapping a sound is
now dropping a file in and running the menu item. The runtime layer, the cue
list and every hook from the previous entry are unchanged.

The mix is measured, not guessed. Clips from different packs arrive up to 20 dB
apart, which reads as bad audio however good each one is, and a play-time gain
cannot fix it because Unity clamps a voice's volume at 1 — a quiet source can
never be lifted to meet a loud one. So the builder rewrites each clip at the
level its moment deserves, with a soft knee near full scale so transients keep
their snap. Result, measured through GetData: a bot's turn at 0.026 RMS up to a
coup at 0.170, a 16.5 dB range, music bed at 0.028 underneath, nothing clipping.

Verified in Play mode: lobby on King's Feast, crossfade into the table theme on
scene load, 35/35 lobby buttons bound, one listener, 12 cues fired at once took
12 voices with the right clips, Tax fired on a real declare, master output live
with zero samples at full scale. Console zero errors.

Still not done: I have not heard any of this either. What is verified is that
the sources are professionally recorded, the semantic mapping is right and the
levels are measured — not that the result is pleasant. Assets/Audio is 26 MB of
source WAV, 23 MB of it the two music loops; it imports as Vorbis so the shipped
size is far smaller, but the loops can be trimmed if the repo size matters.
No build produced, nothing committed.

---

## 2026-10-22 — Codex: six-player short-screen layout (finished)

Fixed the user screenshot's turn banner overlapping the lowest opponents' cards.
For six players at 1080x1920, lowest seats move up 50 reference units, status and
hand bands down 100, HUD down 60. Adjustments interpolate to zero by stage height
2400; existing card/button scales and other player-count layouts are preserved.
TableResponsiveLayout calls RefreshSeatLayout on safe-area/size/player-count
changes so opponent positions adapt alongside the lower bands. Prefabs untouched.

Verified six-player local match in Play mode at 1080x1920: zero CardView/status
bounds intersections. Inspected Captures/Six_Player_Layout_Final.png: lower
opponent nameplates clear upper row cards, banner clear both opponent cards and
hand, HUD above action grid. Final console zero errors. Stopped Play mode.
No APK rebuilt; device validation pending.

## 2026-10-21 — Codex: panel and screen transitions (finished)

Added PanelTransition: .24s fade/scale entrance and .16s dismissal for
PlatformPanels and ModalPanel. Preserves authored anchors, positions and resting
scale, blocks backdrop clicks while dismissing, disables panel controls during
motion, and cancels interrupted motion safely. Existing async panel-load
cancellation remains intact. No prefab/scene regeneration or YAML edits.

Added persistent ScreenTransition overlay: .22s navy fade out/in around async
Lobby/Game navigation, including MatchController.ReturnToLobby. Duplicate scene
requests are ignored while loading; cover intercepts pointer input and releases
it after the destination layout pass. Both helpers use unscaled time and honor
Coup.ReducedMotion. Existing GameOverPanel presentation remains in place.

Play-mode verification: panel alpha=1/input=true after opening; close/reopen
Settings finishes active at scale(1,1,1), alpha=1/input=true; dismissal deactivates
root. Modal opens/reopens while timeScale=0. Reduced-motion panel opens and hides
immediately (original preference restored). Lobby -> Game -> Lobby completes,
scene cover blocksRaycasts=false / IsTransitioning=false afterward. Duplicate
Game request tested. Console: zero errors on final check. Native 1440x2560
capture Captures/Panel_Transitions_Verified.png inspected for resting layout.
Stopped Play mode in Lobby. No APK rebuilt for this change; previous APK does
not include these transitions. Phone verification remains pending.

## 2026-10-21 — Codex: larger mobile table, texture quality, final APK

User confirmed Codex has the Editor. Implemented mobile presentation changes,
including follow-up fixes from user screenshots. This supersedes the earlier
verification-pending entry for the code/build work; sustained device FPS testing
is still incomplete because the phone disconnected.

TableResponsiveLayout initializes in Awake before opening animations, stretches
Stage to safe area and uses width-based scaling. Actions form a 4+3 grid with
238x180 buttons and larger labels/effects. Response buttons are 320x190, with
stretch borders matching fills. Hand, HUD, status and action/response bands use
bottom anchors; opponent panels use top anchors. Player labels/coins enlarged.
HUD shifted to retain the portrait inside the screen. Gameplay prefabs untouched.

Draw/discard piles use a central corridor on crowded tables, with the coin pile
above. Compact screens use smaller decorative piles/captions and omit the coin
pile; duels put piles outside the single opponent's card columns. An occupied
180px-wide discard-card fixture and draw-deck bounds were checked against
opponent CardView bounds for 2–6 players at 1080x1920: zero intersections in the
final check. Earlier 1080x2400 checks also returned zero for all seat counts.
Verified deck remains at .45 scale after opening animations on compact screens;
initializing layout late could otherwise restore the old .75 scale.

Authored Game scene saved through Editor: DockBackground/DockBorder stretch,
left/right 0, top/bottom -10, pivot .5/.5, scale 1 per user screenshot. Response
border children also stretch with zero offsets. Runtime layout repeats these
constraints. Screenshot Response_Buttons_Fit_Final.png shows aligned fills and
outlines; Table_Response_BG_Final.png shows occupied discard clear of opponents
and the inset grid background (captured during opening card animation).

Texture importer now uses ASTC 4x4 / quality 100 on Android/iOS; card long-edge
cap 1024, HUD/panel 1024, backgrounds 4096 maximum. Reimported 76 referenced PNG/
JPG dependencies through AssetDatabase. Captain Android override read back 1024,
ASTC_4x4, quality 100. Mobile_RPAsset renderScale changed .8 -> 1; PC asset also
set to 1 for parity. Source imagery is not upscaled by these caps. Clean coin and
extended lobby v7 art are included. Do not hand-edit .meta files.

MobileFrameRate startup requests 60 FPS with renderFrameInterval=1; Android frame
pacing enabled. Earlier USB-installed revision produced a short SurfaceFlinger
sample: 125 intervals, median/mean 16.65ms, about 60.1 FPS. This is not sustained
proof; later longer attempts had no active timestamps while phone slept, then
USB disconnected. Thermal-service status was 0 during that check. Do not claim
sustained 60 FPS or latest-revision phone visual verification.

Final non-development Android build succeeded at 23:16 IST, job
build-d4b7c12647: zero errors, 18 warnings, APK about 65 MiB at
Builds/Coup-Veiled-Court-Mobile.apk. This final APK was NOT installed: adb device
list is empty. An earlier revision was installed with adb install -r, preserving
data; it predates the last pile/background/texture fixes. User can reconnect for
installation/profiling, or install the delivered APK manually. Editor left in
edit mode. Broader legal/compliance work remains incomplete and separate.

---

## 2026-10-21 — Claude Code: in-game audio, synthesised in the editor (finished)

User asked for audio in the game; there was none — no clips anywhere, and the
only trace of sound was a Coup.AudioMuted pref driving AudioListener.volume from
the lobby settings row. Built the whole layer.

The fal audio provider is **not configured** (generate_audio list_providers
reports configured: false), so no AI-generated audio was available. Sound is
synthesised in the editor instead, the way the card art is: **Coup/Generate
Audio** (CoupAudioGenerator.cs) bakes 38 WAVs into Assets/Audio and fills
Assets/Resources/CoupAudio.asset. The recipes in that file are the master; the
WAVs are build output. Re-tune a number, run the menu item again. A generated
take can be swapped for a recording later by dropping it into the library.

Runtime: CoupAudio (clip library, shaped like CoupArt — per-cue takes, gain and
pitch spread), AudioDirector (boots itself before the first scene, 12 pooled
voices, two music channels with a 1.1s crossfade, owns the AudioListener when a
scene ships none), Sfx (the static facade every caller uses), UiClickSound.

26 cues: interface, cards and coins, turn order, one per ActionKind, block /
challenge / won / lost, influence lost, elimination, victory, defeat. Two music
loops — a four-bar lute figure over Dm–Bb–F–A for the lobby, a drone and pulse
for the table. Both loops fold their reverb tail onto the head, so the seam is
continuous (measured: 0.001 and 0.0036 against a mean sample step of 0.0017).

Hooks: TableView.PlayBeat gives every rules event a cue, PlayOpening stages the
deal and the coin collect, and UIFactory.Button attaches a click sound at play
time only — never serialised, so no prefab or scene was rebuilt. Authored
buttons are picked up by a pass when the active scene changes. The lobby
settings row now has separate Sound and Music toggles.

Assets/Editor/CoupAudioImportSettings.cs is the audio twin of the sprite
postprocessor: cues decompress-on-load mono ADPCM, themes stream as Vorbis, with
Android/iPhone overrides. Drop a clip in Assets/Audio and it is configured on
arrival. Do not hand-edit the .meta files.

Verified in Play mode: lobby theme loops at 0.34, crossfades to the table theme
on scene load, all 35 lobby selectables bound, exactly one listener live, every
cue resolves to a clip, 12 cues fired at once took 12 distinct voices with the
right gains and pitch variation, and master output measured non-zero through
AudioListener.GetOutputData. Console zero errors. Offline practice match played
through Tax, a lost challenge and an influence loss with the matching cues.

Not done: nobody has listened to these on a device, and the mix is judged by
waveform analysis and by ear-free measurement only — levels may want another
pass once heard on a phone. No build produced. Assets/Audio is 9.3 MB of source
WAV; it compresses on import and is not the shipped size.

One coordination note: the Editor was in Play mode when this session started and
the last REVIEW entry read finished, so I took it and exited Play mode at 22:09
to run the generator. Single_Gold_Coin.png, Assets/Scripts/Runtime/MobileFrameRate.cs
and TableResponsiveLayout.cs changed on disk at 22:04-22:08 from outside this
session; none of them were touched here.

---

## 2026-10-21 — Codex: tall-phone layout / 60 FPS — verification pending

Source changes ready, but Editor testing is NOT finished. Play mode repeatedly
stopped between tool calls while new audio code appeared, so asked user to clarify
which agent has the Editor. Awaiting that answer before further Editor driving
or building. Do not concurrently mutate Editor.

- Lobby: edge anchors, width-based CanvasScaler, safe-area header/actions and logo
  placement via LobbyResponsiveLayout. Generated extended 9:21 background v7
  (821x1916), imported and saved into authored Lobby via Editor as separate
  EnvelopeParent child so logo parent retains full canvas bounds. This replaces
  the interim aspect-fit version that showed navy top/bottom bars. Tall render
  bounds measured 1080x2520 covering 1080x2400. Final visual QA interrupted.
- Coin: replaced Single_Gold_Coin.png with clean transparent generated coin,
  preserving GUID/references, reimported through Editor. Gameplay/device preview
  still pending. No hand-edited metadata.
- Table: TableResponsiveLayout stretches Stage to safe area, pins hand/HUD/status/
  response/action bands from bottom and raises/separates three-opponent layout.
  TableView records seats before converting them to top anchors. Not yet visually
  verified across seat counts; no claim this fixes every overlap.
- Performance: discovered targetFrameRate=-1, added MobileFrameRate startup target
  60 and OnDemandRendering interval=1. Android optimized frame pacing enabled.
  Connected phone detected (com.gauravbs.coup installed at 22:00). SurfaceFlinger
  latency query returned no samples; sustained device FPS not measured. No new
  APK built/installed for these fixes.
- Fixed compile blocker in new CoupAudioImportSettings: preloadAudioData moved to
  default/override sample settings for Unity 6. Latest console read zero errors.
  Preserved the concurrently added audio work otherwise.
- Multiplayer host was running but readiness failed. Gracefully restarted managed
  stack; public /ready passed. scripts/check-host.mjs passed sign-in, protected
  HTTP/WebSocket, match start, refresh, reconnect/private hand recovery and logout.
  URL https://curable-twistable-rising.ngrok-free.dev; Mac must stay awake/online.

Required next: obtain Editor turn, compile check, test reference/tall UI and
2–6-player tables, produce APK, then install/profile on connected phone if user
wants current device session replaced. Existing installed build is still older.

---

## 2026-10-21 — Codex: approved brand logo and launcher icons (finished)

User approved generated COUP / VEILED COURT custom brand mark. Imported into
Assets/Sprites/Branding/Coup_Veiled_Court_Logo.png and replaced prior TMP title
composition on Hall/LobbyLogo via Editor. This explicitly requested brand-logo
art includes lettering; gameplay text remains unchanged. Play preview verified
at 1080x1920, screenshot Assets/Screenshots/Lobby_Brand_Logo_Final.png; no errors.

Created matching crowned-mask launcher icon, default icon and iOS application
icon slots assigned through PlayerSettings. Android adaptive icons now have
separate navy background and transparent emblem foreground at all six sizes
432,324,216,162,108,81. Initially flat icon assignment left foreground missing;
user screenshot caught this during setup. Replaced with two-layer assets and
read back every slot: both textures present at all sizes, console zero errors.
Settings saved; Editor returned to edit mode. No APK/iOS build or physical-device
launcher verification claimed. Generated originals remain in Codex image folder;
project assets copied under Assets/Sprites/Branding with automatic import settings.

---

## 2026-10-21 — Codex: lobby cards replaced with refined wordmark (finished)

User redirected card-art replacement to a COUP logo. Removed the three lobby
card instances through the Editor, retaining character art elsewhere. Replaced
with editable TMP COUP / VEILED COURT in Cinzel, champagne-gold gradient, small
diamond and hairline gold rules on a restrained navy backing. Removed the first
iteration's bulky black shadow and crown after user feedback. LobbyMotion now
targets Hall/LobbyLogo. Scene saved through Editor; no prefab source rebuilt.

Verified in Play mode at 1080x1920, no console errors. Final screenshot:
Assets/Screenshots/Lobby_Logo_Refined.png. Returned to edit mode. Initial generated
card fan was superseded by user's request and was not imported or wired.
No APK build. Existing compliance work remains separate and incomplete.

---

## 2026-10-21 — Codex: selected title and initial privacy implementation

User selected **Coup: Veiled Court**. Set PlayerSettings.productName via Editor;
login and startup show COUP with VEILED COURT subtitle, OAuth return page uses
full title. Internal namespaces, IDs and gameplay action names unchanged.
Name selection is not trademark/licensing clearance. No build produced.

Also present from the preceding interrupted compliance task: Fiesta games support
contact, public deletion-instructions route, authenticated allowlisted account
export, pending browser-auth cleanup on deletion and a deleted-account purchase
guard. Server suite passed 58/58 including export privacy and deletion/purchase
checks. These are initial fixes; legal notices, age handling, retention, safety,
provider lifecycles and remaining audit findings are still outstanding. Privacy
panel opened in Play mode; full visual QA was interrupted. Returned to edit mode
for title update. Do not treat the broader compliance task as complete.

---

## 2026-10-21 — Codex: original-game legal risk and competitor audit (finished)

Repository-wide inventory and focused legal-relevant static review for a planned
worldwide launch with purchases and no cash prizes. User confirmed no Coup licence
and wants an original game. No implementation changes made in this audit.

Reviewed account/auth, deletion/retention, social/UGC, wallet/store/refund, rules,
art derivation prompts/references, packages, font licences, publishing settings
and hosting docs. Inventory covers 48 client C# and 21 server JS source files;
this is not a line-by-line functional or forensic certification. No player DB,
secret configuration, private contracts or store consoles inspected.

Delivered a Canvas risk register with 23 findings, 76 valid file/line references,
primary legal/policy links, fix order, data map, positive foundations, limitations
and 8 comparable games. Location:
/Users/gaurav-bs/.cursor/projects/Users-gaurav-bs-Coup/canvases/game-release-legal-audit.canvas.tsx
TypeScript check passed. All referenced evidence paths/line bounds checked.

Priority work: clear a new public brand and asset provenance, accurate notices
and age strategy, complete data lifecycle/external deletion, iOS login, payment
refund lifecycle and useful products, moderation/grievance controls, publisher/
territory/licence/disclosure readiness. Mechanics vs expression distinguished;
no allegation that the prior generated card art is a commercial-board-game scan.
No claim that worldwide law is fully cleared. Indian gaming Rules 2026 and
phased DPDP commencement included as current review issues.

BGA explicitly documents authorized Coup publication; other mobile/browser
listings' licences and service operation not verified. Font OFL texts found;
xmlhttprequest-ssl lockfile licence metadata missing, but actual LICENSE inspected
and found MIT-style grant. No need inferred to replace fonts or MongoDB.
Unity eligibility, MongoDB licensing, trader/tax and publisher questions flagged
for evidence/counsel rather than asserted violations. Game and Editor untouched.

---

## 2026-10-21 — Codex: lobby UI animation (finished)

Added runtime LobbyMotion, bootstrapped by LobbyScreen. The header fades/slides
as one aligned group; cards, side panels, Play, modes, event and bottom navigation
follow with a stagger over roughly 0.9s. Once settled, Play breathes by 0.8% and
the displayed cards float by 3 units. Idle movement pauses behind platform panels.
All motion uses unscaled delta time and authored resting positions/scales, with
restoration on disable and no animation during login/startup. Supports the
Coup.ReducedMotion preference (skips entrance and ambient motion).

LobbyPressFeedback reports pointer hover/press state; motion smoothly scales
interactive buttons without replacing their Button callbacks. Exit, release,
disable and focus loss clear press state. Header buttons included.

Verified compile/Play console with zero errors, login hiding and lobby re-entry,
card movement (-2.97 units sampled), idle Play scale (1.0076), held scale (0.9578),
release reset, and full authored pose restoration when motion is disabled:
header y=0, card y=0, Play scale=1, alpha=1. Reference screenshot:
Assets/Screenshots/Lobby_Animated_UI_Final.png (static frame, not a video).
Used presentation-only login bypass for preview; no new auth/network test claimed.
Returned to edit mode. No scene or prefab sources changed; APK not rebuilt.

---

## 2026-10-21 — Codex: lobby header alignment (finished)

Aligned the authored lobby header through the Editor, saving Lobby.unity only.
Profile/currency/gems/settings share y=-100, height 90 and exactly 30-unit gaps.
Row bounds are -500..500, leaving 40-unit side margins on the reference canvas.
Reduced portrait/ring to 120 from 140/146, centered on the same row. Profile name
and level share a left edge with clearer line spacing; level progress aligns
with its baseline. Currency icons/values/plus signs use matching internal columns;
coin/gem art is inset and settings cog reduced to 48 for balanced padding.

Verified saved scene in Play mode at 1080x1920, all header TMP fields report no
overflow, all panel centers/heights/gaps measured, and console has zero errors.
Native header crop: Assets/Screenshots/Lobby_Header_Alignment_Detail.png.
Full capture: Assets/Screenshots/Lobby_Header_Alignment_20261021.png.
Returned to edit mode. Existing Game.unity changes preserved; no prefab sources
or scripts changed. APK not rebuilt.

---

## 2026-10-20 — Claude: UI sprite atlas, and my bar-fitting removed (finished)

**Atlas.** The sprite packer was switched off entirely (`m_SpritePackerMode: 0`)
and no atlases existed. Measured on a six-player table first: 123 active Images
drawing from **24 distinct textures**, every switch breaking the canvas batch.
The repeated chrome turned out to be small once imported — FrameLine 128px used
16 times, Panel_Rounded 96px used 15, Ring 256px used 12 — about 1.11M pixels
all told against 4.19M in a 2048 atlas.

`Assets/Sprites/UI_Chrome.spriteatlasv2` packs that chrome plus the Buttons and
Coins folders and Card_Back: 23 sprites on one 1024x2048 ASTC 6x6 page. Rotation
and tight packing are off, because the panel and frame sprites are 9-sliced and
either setting corrupts them. Mobile overrides match what `CapFor` already does
for loose sprites. Card faces, backdrops, the table background and the draw deck
are deliberately excluded: large and few, so they would fill the atlas without
reducing switches.

Result on the same table: **24 distinct textures down to 7**, with 97 of 123
Images drawing from the single page. The six left are exactly the excluded ones.
Verified rendering and console after packing; 9-slicing intact.

**Removal.** Codex re-anchored TopBar and ActionBar to their parent's top and
bottom edges in `c5ace98` and `232d7bb`, which is the better fix. My runtime
`FitBarsToScreen` did centre-based arithmetic on `anchoredPosition`, which is
meaningless for an edge-anchored rect, so it computed an inset of 972 against a
240 clamp and pushed the header off screen entirely. It is gone; the scene
anchoring does the job.

Still open, and not mine to decide while Codex is mid-edit: the Stage is still a
fixed 1080x1920 centred rect, so the bars hug the *Stage* edges rather than the
canvas. On a 2.06 aspect that is still 150 units of gap. Making Stage stretch
vertically would fix it in one scene change now that the bars are edge-anchored,
but the scene has uncommitted changes and belongs to whoever is in it.

---

## 2026-10-20 — Codex: integrated countdown clock redesign (finished)

User clarified that the timer's appearance, not just its PPU settings, was wrong.
Removed the detached thin rail and tiny seconds plaque. Countdown now sits inside
StatusPlate as a 96-unit navy circular dial, fine gold bezel, draining gold arc,
large numeric count and small SEC caption. Status copy reserves space only while
the clock is visible, restoring authored text rects when hidden. Amber/red timing
and server deadline inputs preserved. No new art or source prefab/scene changes.

Added TurnClockRing, a Canvas-native annular mesh, avoiding sprite slice/PPU
issues entirely. Initial preview failed to render because the custom Graphic
needed CanvasRenderer explicitly; fixed with both a RequireComponent attribute
and creation before the Graphic. Verified actual populated meshes (480 vertices
for full circles and 384 for 80% arc), native 1080x1920 crop, urgent sample and
real online play: connected=true, 18s remaining, 0.878 progress, timer visible.
Captures Timer_Dial_Fixed.png, Timer_Dial_Online.png, Timer_Dial_Urgent.png.
Compilation: zero errors. Editor console also contains recurring Unity PlayerLoop
recursive-call errors during MCP inspection; do not claim a clean console.
Returned to edit mode. No APK rebuild in this pass.

---

## 2026-10-20 — Codex: timer slice / PPU correction (finished)

Fixed timer-specific Image multipliers rather than changing shared sprite imports.
The rail uses 30px slice borders on a 14-unit-high rect: multiplier 10 now gives
3-unit borders. The badge frame has 70px vertical borders; multiplier 6 fits
inside its 36-unit height without Unity compressing both corners together.
Badge fill uses multiplier 4; the 4-unit highlight uses multiplier 20.

Changed drain graphics from Filled (which stretches corner pixels and ignores
sliced-border sizing) to Sliced, resizing their rects to the remaining fraction.
Native 1080x1920 screenshot and crop inspected: Timer_PPU_Corrected.png.
Verified full/half/5% widths of 822/411/41.1 units; zero hides the fill.
No compile/console errors. Returned to edit mode. No scene, prefab or sprite
import changes. Prior unrelated workspace changes preserved.

---

## 2026-10-20 — Codex: styled turn countdown rail (finished)

Redesigned Claude's runtime turn timer in TableView: slim inset navy rail with
gold rim, ten subtle divisions, bright upper edge and a diamond following the
remaining fill. A matching gold-framed seconds badge sits at the right. Tightened
its spacing after native inspection caught the first layout touching the cards.
It now fits directly below StatusPlate, above the hand. All copy is TMP; existing
shared sprites are reused. No scene or prefab sources changed.

Preserved the authoritative OnlineSecondsRemaining/OnlineTurnProgress inputs.
Gold above ten, amber down to six, red at five with a restrained badge pulse.
The whole timer hides for game over, disconnect, missing deadline and local mode;
the previous local-mode early return could leave a previously created timer up.
All timer graphics have raycastTarget=false.

Verified compilation (zero errors) and Play mode visual samples at 24s/80%,
8s/27%, 4s/13% and zero. Zero fill and hide verified; no graphics intercept input.
Native 1080x1920 capture inspected with a crop:
Assets/Screenshots/Timer_Gold_Verified.png; amber/red captures alongside it.
This was a local presentation preview with timer inputs injected through Editor
reflection, not a new live-server countdown test. Existing server timing untouched.
Returned to edit mode. An overlapping Android build temporarily delayed preview;
this entry does not claim a rebuilt APK containing the timer design.

---

## 2026-10-20 — Codex: startup loading screen (finished)

Added StartupLoadingScreen before the login UI on first app entry. Uses current
card-back sprites, Coup fonts and the shared sliced navy/gold panel images. Has
an animated indeterminate activity bar, a short legible opening, resource warmup,
and a fade into login. Existing credentials restore under the loading curtain;
failed restore returns to an actionable login page. No anonymous account is
created just to show startup. Later lobby visits skip the opening curtain.

Login stays hidden while startup is active, so labels/buttons do not show through
the opaque curtain. Loader animation and transitions use unscaled time. No
prefab/scene sources rewritten; no additional generated artwork needed.

Verified: four frontend Play Mode tests passed; after the opacity/visibility
refinement, the two affected login/panel integration tests passed again. No
compile errors. Android development ARM64 APK rebuilt successfully with 0 errors
and 7 existing warnings at Builds/FrontendQA/Coup.apk; this build also includes
the prior Google/Guest icons and API logging. Initial screenshot exposed the
login showing through, which was corrected. Final manual preview was skipped
because the user had already moved to Game in Play mode; left their state alone.

---

## 2026-10-20 — Codex: API request logging (finished)

PlatformClient now prints numbered request/response metadata in the Unity Console
and development Android logs: method, endpoint, HTTP status, outcome and elapsed
milliseconds. Release builds compile these debug log calls out. Request bodies,
response bodies, Authorization headers, OAuth query parameters and receipts are
not logged. Cancellation and connection failures produce a completion log too.

Backend /v1 requests log JSON route/status/duration on finish or client abort.
Query strings are excluded and friend/mission identifiers become :id. Restarted
the existing local host to activate logging. Verified real guest/profile HTTP200
and unauthenticated profile HTTP401 entries in server/.data/host.log and Unity
Console; no compile errors. Returned Editor to edit mode. APK not rebuilt for
this logging-only change.

---

## 2026-10-20 — Codex: game-over background and firework confetti (finished)

Replaced WinBg/LoseBg bitmap plates with the existing shared sliced navy/gold
panel frame. The old image contained its own opaque exterior and inset border;
scaling it as the entire card produced a mismatched background. Added a restrained
crest glow, resized the border with the sheet, and extended the modal dimmer.
All lettering remains TMP; no scene or prefab sources rewritten.

Replaced the top-edge ribbon shower with five staggered radial firework bursts.
Only the instantiated WinConfetti systems are configured: no continuous emission,
no ribbon particles, small gold/teal foil, outward spark streaks, deceleration,
gentle gravity and short fades. The celebration finishes rather than looping.
Closing, retrying, reopening and disabling cancel the burst coroutine and clear
particles. Source WinConfetti.prefab is unchanged.

Verified in Play mode using local preview, at 1080x1920; inspected the native win
capture rather than judging the inline thumbnail. Captures:
Assets/Screenshots/GameOver_Fireworks_Final.png and GameOver_Defeat_Final.png.
Defeat has zero particles; hide clears to zero; retry and lobby callbacks each
fire once and close the panel. Compilation and console: zero errors/warnings.
Returned to edit mode. Existing unrelated workspace edits preserved.

---

## 2026-10-20 — Codex: Google and Guest login icons (finished)

Generated GuestGold_v1.png with real alpha: a gold anonymous bust matching the
imperial UI. Imported GoogleG.png from Google's official identity branding page,
keeping its colors unchanged. Saved both in Assets/Sprites/Login and assigned
references on Resources/PanelTheme.asset. LoginScreen now draws each beside its
TMP label, with preserveAspect, no raycast interception and consistent spacing.
Busy sign-in dims the complete button, including its frame, label and icon.

Verified script compilation with no errors and inspected the actual Game view
capture Assets/Screenshots/Login_With_Icons.png. A later inspection call found
the user had moved into Game scene; did not disturb that editor state. No new
functional tests for these visual-only changes. APK from previous entry has not
been rebuilt for these two new icons.

---

## 2026-10-20 — Codex: login page and shared panel image treatment (finished)

Added LoginScreen over the authored lobby, using existing hall art, TMP fonts,
image-backed navy/gold containers and buttons. First launch now waits for an
explicit Guest or Google choice instead of silently creating a guest. Saved
sessions restore automatically. Guest reaches the lobby, sign-out and account
deletion return to login. Google uses the existing backend browser OAuth flow,
with cancellation, timeout and error handling; no secrets are embedded in Unity.
This is Google account OAuth, not the separate Play Games Services SDK. Live
Google provider credential setup/testing remains unfinished from the earlier
account-setup work; this entry does not claim live Google login verified.

Added Resources/PanelTheme as the shared image style for platform panels,
network/waiting/reconnect overlays, game decision dialogs and result buttons.
Existing custom victory/defeat art remains in use. No prefab sources rewritten.
Fixed Rules text clipping by allowing long content rows to grow within scrolling.

Corrected Plaque slice borders through TextureImporter (160/130/160/130),
not by editing metadata text. Panel rim PPU multiplier 3, button rim multiplier 2;
container centers use an opaque navy fill and an image rim with fillCenter=false.
This avoids scaling the thin cropped banner texture across a tall container and
prevents alpha holes from showing the lobby through the panel.

Verified: four Play Mode integration tests pass, including explicit guest choice,
session restoration, sign-out, all platform panel text overflow checks, forced
WebSocket disconnect/recovery without historical VFX, and ranked matchmaking.
Initial online tests failed while backend readiness was false; restarted the
existing local service, then /ready and the public HTTP/WSS smoke check passed.
Android development ARM64 APK rebuilt successfully at Builds/FrontendQA/Coup.apk,
0 errors, 7 existing/configuration warnings. Google Play licensing/products and
physical-device sign-in/purchase tests remain release blockers.

---

## 2026-10-20 — Claude: HUD bars fit short screens as well as tall (finished)

Follow-up to the tall-display fix, which only engaged when the canvas was
taller than the Stage.

A 4:3 tablet is the same problem inverted and considerably worse. Measured at
1536x2048: the canvas comes out 1080x1440, *shorter* than the 1920 Stage, and
both bars fall outside it entirely — the header above the top edge at 823..948
against a half-height of 720, the action buttons below the bottom. The game is
unplayable there; the screenshot showed no header at all and only the
"CHOOSE AN ACTION" caption with its buttons cut off.

The guard is gone, so the same absolute placement now runs in both directions.
Verified at both aspects: 4:3 pulls the bars in to 583..708 and -670..-450,
both on screen; the 2.06 tall case is unchanged at 13 and 50.

Known and not fixed: on 4:3 the action bar now overlaps the lower half of the
hand and hides the You coin bar. That is inherent — 1920 units of authored
composition do not fit a 1440 unit canvas, so something has to give, and
overlapping beats being off screen. Fixing it properly means scaling the Stage
to fit vertically (0.75 on this aspect) and letterboxing the sides, which also
touches the seat clamp, since that measures against the unscaled Stage rect.

Added a "Coup Tablet 4:3 (1536x2048)" entry to the Android Game view sizes for
this, alongside the existing Coup Portrait QA. The view was restored to
2960x1440 Portrait afterwards.

---

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
