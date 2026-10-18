# Validation — 2026-10-18

Integrated and verified directly by Codex through Unity MCP. No handoff pending.

- Assassination declaration creates a small card-target cue, without playing a hit. Failure cancels it. A confirmed hit throws a dagger and emits a compact impact; a block sends the blade sideways. Neither branch emits the large magical shockwave.
- Coup has a short contraction/gather stage, a downward impact, two expanding gold/red shockwaves, and falling metallic shards. It fits the existing 1.3-second declaration beat.
- Tax, Steal, Exchange, Income, Foreign Aid and Challenge effects have distinct smaller presentations. Steal streaks travel from the target toward the actor. Exchange uses anonymous card backs.
- Deterministic Play-mode probes passed pending/failure cancellation, dagger hit and deflection, Coup layered emission and retirement, and component-disable cancellation. Fifteen repeat bursts/exchanges reused a 55-image pool without growth. Expired effects returned to zero live images.
- A separate rules fixture verified that the discard top card shows a genuinely public lost role. Empty discard and empty draw states use understated slots; the draw pile uses the new card-stack sprite.
- Latest controlled probes and compilation produced zero console errors or warnings. Device FPS and touch performance were not profiled.
- Review stills freeze a rendered animation pose only for capture. Normal animation timing and input are restored after inspection. Earlier timed screenshot probes were affected by editor/MCP frame stalls and are superseded by deterministic lifecycle checks.
- Unity-generated scene YAML has serializer whitespace; no external cleanup or hand-editing was performed on scene or prefab files. Prefab sources and approved reference PNGs were preserved.
