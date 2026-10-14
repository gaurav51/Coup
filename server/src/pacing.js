/**
 * How long the table holds after an event, in milliseconds, before the next
 * event should be shown. The Unity client waits this long between beats, and
 * bots wait the sum before they answer. Keep the numbers in step with
 * MatchController.HoldSeconds.
 */
export function animationHold(event) {
  if (!event) return 0;
  switch (event.kind) {
    case "ActionDeclared":
      return animatedAction(event.action) ? 1300 : 450;
    case "BlockDeclared":
      return 1100;
    case "ChallengeDeclared":
      return 1300;
    case "ChallengeWon":
      return 1500;
    case "ChallengeLost":
      return 800;
    case "CardReplaced":
      return 1700;
    case "InfluenceLost":
      return event.reason === "FailedChallenge" ? 1450 : 1100;
    case "CoinsChanged":
      return 700;
    case "PlayerEliminated":
      return 900;
    case "ExchangeCompleted":
      return 900;
    case "ExchangeDrawn":
      return 400;
    case "ActionResolved":
    case "ActionFailed":
      return 500;
    case "TurnStarted":
      return 450;
    case "GameOver":
      return 800;
    default:
      return 0;
  }
}

/** Tax, Steal, Exchange, Assassinate and Coup each have a title to slam in. */
function animatedAction(action) {
  return action === "Tax" || action === "Steal" || action === "Assassinate" || action === "Exchange" || action === "Coup";
}

/** Total hold for a burst of events. An empty burst does not add a pause. */
export function reactionBeat(events) {
  let beat = 0;
  for (const event of events ?? []) beat += animationHold(event);
  return beat;
}
