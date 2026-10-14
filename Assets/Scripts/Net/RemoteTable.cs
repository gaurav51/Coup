using System;
using System.Collections.Generic;
using Coup.Core;

namespace Coup.Net
{
    /// <summary>Turns a private <c>game:state</c> payload into the board the table already knows how to draw.</summary>
    public static class RemoteTable
    {
        public static TablePresentation Read(JsonValue state)
        {
            var you = state.Int("you");
            var presentation = new TablePresentation
            {
                Phase = ParsePhase(state.Str("phase")),
                CurrentPlayer = state.Int("currentPlayer"),
                Turn = Math.Max(1, state.Int("turn", 1)),
                DeckCount = Math.Max(0, state.Int("deckCount", 0)),
                Waiting = Ints(state.Arr("waitingOn")),
                Seats = new List<SeatPresentation>()
            };

            var pending = state.Get("pending");
            if (pending != null && pending.Type == JsonValue.Kind.Object)
            {
                presentation.Actor = pending.Int("actor");
                presentation.Action = ParseAction(pending.Str("action"));
                presentation.Target = pending.Int("target");
                presentation.Claim = ParseRole(pending.Str("claim"));
                presentation.Blocker = pending.Int("blocker");
                presentation.BlockClaim = ParseRole(pending.Str("blockClaim"));
            }

            if (presentation.Phase == GamePhase.AwaitingInfluenceLoss)
            {
                presentation.Loser = state.Int("loser");
                presentation.LossReason = ParseLoss(state.Str("lossReason"));
            }

            var exchange = state.Get("allowed")?.Get("exchange");
            if (exchange != null && exchange.Type == JsonValue.Kind.Object)
            {
                presentation.ExchangePool = Roles(exchange.Arr("pool"));
                presentation.ExchangeKeep = exchange.Int("keep", 0);
            }

            var players = state.Arr("players");
            if (players == null) return presentation;
            var hand = Roles(state.Arr("hand"));
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var seat = player.Int("seat", i);
                presentation.Seats.Add(new SeatPresentation
                {
                    Name = player.Str("name"),
                    Coins = player.Int("coins", 0),
                    Hidden = seat == you ? hand : null,
                    HiddenCount = player.Int("influence", 0),
                    Revealed = Roles(player.Arr("revealed"))
                });
            }

            return presentation;
        }

        public static GameEvent ReadEvent(JsonValue payload)
        {
            return new GameEvent
            {
                Kind = ParseEvent(payload.Str("kind")),
                PlayerIndex = payload.Int("player"),
                TargetIndex = payload.Int("target"),
                Action = ParseAction(payload.Str("action")),
                Role = ParseRole(payload.Str("role")),
                Amount = payload.Int("amount", 0),
                Reason = ParseLoss(payload.Str("reason")),
                HoldMs = Math.Max(0, payload.Int("hold", 0))
            };
        }

        public static int Sequence(JsonValue payload) => payload.Int("seq", 0);

        private static List<int> Ints(List<JsonValue> values)
        {
            var list = new List<int>();
            if (values == null) return list;
            for (var i = 0; i < values.Count; i++)
                if (values[i].Type == JsonValue.Kind.Number) list.Add((int)values[i].Number);
            return list;
        }

        private static List<Role> Roles(List<JsonValue> values)
        {
            var list = new List<Role>();
            if (values == null) return list;
            for (var i = 0; i < values.Count; i++)
            {
                var role = ParseRole(values[i].Type == JsonValue.Kind.String ? values[i].String : null);
                if (role.HasValue) list.Add(role.Value);
            }
            return list;
        }

        private static GamePhase ParsePhase(string value) =>
            Enum.TryParse(value, out GamePhase phase) ? phase : GamePhase.AwaitingAction;

        private static ActionKind? ParseAction(string value) =>
            Enum.TryParse(value, out ActionKind action) ? action : (ActionKind?)null;

        private static Role? ParseRole(string value) =>
            Enum.TryParse(value, out Role role) ? role : (Role?)null;

        private static LossReason? ParseLoss(string value) =>
            Enum.TryParse(value, out LossReason reason) ? reason : (LossReason?)null;

        private static GameEventKind ParseEvent(string value) =>
            Enum.TryParse(value, out GameEventKind kind) ? kind : GameEventKind.GameStarted;
    }
}
