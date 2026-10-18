# Validation — 2026-10-18

Integrated and verified directly by Codex through Unity MCP. No handoff pending.

Latest assassination correction: the dagger is now visibly poised from declaration through the response window, with a stronger target-card frame. Resolution launches a 0.42-second throw instead of the barely visible 0.22-second throw. A separate CoupGame fixture drove Declare, PassChallenge, PassBlock and ChooseInfluenceToLose through the real TableView event handler; verified visible pending dagger, thrown dagger after resolution, completed public loss and zero remaining effects after cleanup. The live match's rules state was not modified by that fixture. Latest console query: zero warnings/errors. No screenshot freeze was used for this verification.

- Assassination declaration creates a small card-target cue, without playing a hit. Failure cancels it. A confirmed hit throws a dagger and emits a compact impact; a block sends the blade sideways. Neither branch emits the large magical shockwave.
- Coup has a short contraction/gather stage, a downward impact, two expanding gold/red shockwaves, and falling metallic shards. It fits the existing 1.3-second declaration beat.
- Tax, Steal, Exchange, Income, Foreign Aid and Challenge effects have distinct smaller presentations. Steal streaks travel from the target toward the actor. Exchange uses anonymous card backs.
- Deterministic Play-mode probes passed pending/failure cancellation, dagger hit and deflection, Coup layered emission and retirement, and component-disable cancellation. Fifteen repeat bursts/exchanges reused a 55-image pool without growth. Expired effects returned to zero live images.
- A separate rules fixture verified that the discard top card shows a genuinely public lost role. Empty discard and empty draw states use understated slots; the draw pile uses the new card-stack sprite.
- Latest controlled probes and compilation produced zero console errors or warnings. Device FPS and touch performance were not profiled.
- Review stills freeze a rendered animation pose only for capture. Normal animation timing and input are restored after inspection. Earlier timed screenshot probes were affected by editor/MCP frame stalls and are superseded by deterministic lifecycle checks.
- Unity-generated scene YAML has serializer whitespace; no external cleanup or hand-editing was performed on scene or prefab files. Prefab sources and approved reference PNGs were preserved.
