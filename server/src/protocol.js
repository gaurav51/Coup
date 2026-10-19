/** Wire names. Clients and the server both speak this set and nothing else. */

export const Client = Object.freeze({
  CREATE: "lobby:create",
  JOIN: "lobby:join",
  LEAVE: "lobby:leave",
  READY: "lobby:ready",
  START: "lobby:start",
  RESUME: "session:resume",
  DECLARE: "game:declare",
  CHALLENGE: "game:challenge",
  PASS: "game:pass",
  BLOCK: "game:block",
  LOSE: "game:lose",
  EXCHANGE: "game:exchange",
});

export const Server = Object.freeze({
  LOBBY: "lobby:state",
  LOG: "game:log",
  EVENT: "game:event",
  STATE: "game:state",
  OVER: "game:over",
  REJECTED: "action:rejected",
});

export const protocolDocument = {
  game: "coup",
  transport: "socket.io",
  players: { min: 2, max: 6 },
  client: {
    "lobby:create": { name: "string" },
    "lobby:join": { code: "string", name: "string" },
    "lobby:leave": {},
    "lobby:ready": { ready: "boolean, omitted toggles" },
    "lobby:start": { bots: "optional integer, private tables only" },
    "session:resume": { code: "string", token: "string from the join ack" },
    "game:declare": {
      action: "Income | ForeignAid | Coup | Tax | Steal | Assassinate | Exchange",
      target: "seat index, required for Coup, Steal, and Assassinate",
    },
    "game:challenge": "Challenge the action, or the block, depending on the phase.",
    "game:pass": "Pass the current challenge or block window.",
    "game:block": {
      role: "Duke | Captain | Assassin | Contessa | Ambassador",
      note: "During a challenge, the victim of Steal or Assassinate may block early. The server passes their challenge and plays the block once the claim survives.",
    },
    "game:lose": { role: "which of your own cards to reveal" },
    "game:exchange": { keep: "role names, one entry per card kept, duplicates allowed" },
  },
  server: {
    "lobby:state": "Public table list. Sent privately so each payload can mark which seat is yours.",
    "game:log": "Full public history. Replaces the client's log. Sent when a match starts and when a seat resumes.",
    "game:event": "One new public rules event, in order. Never contains a hidden card. hold is milliseconds the table should wait before the next event, so a title or a challenge can finish.",
    "game:state": "This socket's private view: own hand, public coins and revealed cards, and the moves this seat may make.",
    "game:over": "{ seat, name } once, when a winner is decided.",
    "action:rejected": "{ message } when a command is illegal. The ack is also { ok: false, error }.",
  },
  ack: "{ ok: true, code, token, seat } or { ok: false, error }",
};
