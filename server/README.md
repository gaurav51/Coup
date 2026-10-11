# Coup server

Authoritative multiplayer for the Unity Coup table. The rules match `Assets/Scripts/Core`: two to six players, the fifteen-card deck, challenges, blocks, and exchanges. Clients only send choices. The server deals, resolves, and tells each socket what that seat is allowed to know.

## Run

```bash
cd server
npm install
npm test
npm start
```

The process listens on port `3000`, or `PORT` if you set it. `GET /health` returns `{ "ok": true }`. `GET /protocol` returns the event list.

Unity plays this server when you press Play. The match object connects to `http://localhost:3000`, opens a table, and fills the other seats from the server. Leave `join code` empty on the host. A second player pastes that code into `join code` and turns off `auto start online`.

`lobby:start` accepts `{ "bots": 5 }` to fill empty seats. Those seats answer on the server.

## Table

| Client event | Payload | Who |
| --- | --- | --- |
| `lobby:create` | `{ "name": "Ann" }` | Opens a table and becomes host |
| `lobby:join` | `{ "code": "K7QP", "name": "Ben" }` | Takes the next seat |
| `lobby:ready` | `{ "ready": true }` | Ready up. Omit `ready` to toggle |
| `lobby:start` | `{ "seed": 1 }` optional | Host, once everyone is ready |
| `lobby:leave` | `{}` | Leave the lobby. During a match this only disconnects the seat |
| `session:resume` | `{ "code", "token" }` | Same seat after a drop |

Every call can pass a Socket.io acknowledgement. Success is `{ "ok": true, "code", "token", "seat" }`. Failure is `{ "ok": false, "error" }` and an `action:rejected` event.

Keep the `token` from create or join. It is the seat, not the socket.

## Playing

Send these only when `game:state.allowed` says the move is legal.

| Client event | Payload |
| --- | --- |
| `game:declare` | `{ "action": "Steal", "target": 2 }` |
| `game:challenge` | `{}` — challenges the action, or the block, based on the phase |
| `game:pass` | `{}` |
| `game:block` | `{ "role": "Contessa" }` |
| `game:lose` | `{ "role": "Duke" }` |
| `game:exchange` | `{ "keep": ["Ambassador", "Duke"] }` |

Actions: `Income`, `ForeignAid`, `Coup`, `Tax`, `Steal`, `Assassinate`, `Exchange`.

`Coup`, `Steal`, and `Assassinate` need `target` as a seat index. Coup costs 7 and cannot be stopped. Assassinate costs 3 up front. A caught bluff refunds that fee. A block does not.

On your own turn at 10 or more coins, the only legal action is Coup. In a two-player game the starting seat begins with 1 coin and the other seat with 2.

Steal and Assassinate can be blocked by their target, including while other players are still deciding whether to challenge. Send `game:block` during that challenge. The server passes your challenge and plays the block if the claim survives. Foreign Aid can be blocked by anyone else, with Duke. Steal can be blocked with Captain or Ambassador. Assassinate can be blocked with Contessa.

`game:exchange` `keep` is a list of role names, one per card. Two copies of the same role are two entries.

## What the server sends

| Event | When |
| --- | --- |
| `lobby:state` | The table changes. `you.host` tells you if you can start |
| `game:log` | Match start and resume. Replace your log with `events` |
| `game:event` | One new public rules event, in order |
| `game:state` | Your private view, after every change |
| `game:over` | `{ "seat", "name" }` once |
| `action:rejected` | `{ "message" }` |

`game:state.hand` is your cards. Other seats are `players[]` with coins, how many cards they have left, and the cards they have revealed. `allowed` is the only menu you should draw:

```json
{
  "you": 0,
  "phase": "AwaitingChallenge",
  "yourTurn": true,
  "hand": ["Captain", "Duke"],
  "allowed": {
    "declare": [],
    "challenge": true,
    "pass": true,
    "block": ["Captain", "Ambassador"],
    "lose": false,
    "exchange": null
  }
}
```

During your own exchange, `allowed.exchange` is `{ "pool": ["..."], "keep": 2 }`. That pool is not sent to anyone else, and it is not written into `game:event`.

`game:event.text` is a sentence you can drop straight into the log. The event also has `kind`, `player`, `target`, `action`, `role`, `amount`, and `reason`. A replacement card is never named. The next `game:state` for that seat shows the new hand.
