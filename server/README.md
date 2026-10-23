# Coup server

Authoritative multiplayer for the Unity Coup table. The rules match `Assets/Scripts/Core`: two to six players, the fifteen-card deck, challenges, blocks, and exchanges. Clients only send choices. The server deals, resolves, and tells each socket what that seat is allowed to know.

## Mac internet testing (configured)

The authenticated test backend is exposed at
https://curable-twistable-rising.ngrok-free.dev . `GET /ready` checks MongoDB
and the room worker lease. `GET /health` only checks that HTTP responds.

A macOS login service, `com.coup.local-backend`, starts MongoDB, the backend,
and ngrok. It restarts the stack after a process failure and starts when this
user logs in. The Mac must remain awake and connected to the internet.
This is development hosting on one Mac, not a highly available deployment.
No router port forwarding or static public IP is required.

- Backend: `127.0.0.1:3001`; MongoDB: `127.0.0.1:27017`, replica set `coupLocal`.
- Persistent data and logs: `server/.data/`; generated signing secret: `server/.env.local`.
  Both are ignored by Git. Keep the data and signing secret together when backing up.
- ngrok uses its normal per-user configuration. The request inspector is disabled
  so it does not retain authentication requests and private game snapshots.
- Reinstall/restart the login service: `node scripts/install-local-service.mjs`.
- Stop it: `launchctl bootout gui/$(id -u)/com.coup.local-backend`.
- Run manually after stopping the service: `npm run host:local`.
- Verify public HTTP and WebSocket flows:
  `node scripts/check-host.mjs https://curable-twistable-rising.ngrok-free.dev`.
  This creates a disposable guest and a test match; it does not make purchases.

The local launcher uses the cached MongoDB 8.0.17 binary from
`mongodb-memory-server`, with an explicit persistent dbpath. Development
dependencies must be installed for these hosting helpers. It is a single replica
member so transactions work; there is no database redundancy or automatic backup.

## Authenticated platform API

Native clients first call `POST /v1/auth/guest` with `{ "name": "Player" }`.
The response includes an access token, rotating refresh token, session ID, and
profile. Store refresh credentials securely on the device. Send access tokens
as `Authorization: Bearer ...` to HTTP APIs and as `auth.accessToken` in the
Socket.IO namespace handshake. Refresh with `POST /v1/auth/refresh`, passing
`sessionId` and `refreshToken`. Logout revokes the session.

Available APIs include profile read/name update, store catalog, daily missions
and claims, match history, leaderboard, wallet ledger, purchase verification,
and authenticated status under `/v1/`. Apple/Google identity exchange and account
linking use a server-created nonce from `/v1/auth/nonce`.

Apple/Google login requires registered app identifiers and provider client IDs.
Real purchases require the configured store product catalog, Apple verification
certificates/app identifiers, and Google Play service credentials. They fail
closed while unconfigured. Live provider sign-in and purchases have not been
verified. Unity now has account/profile, friends, private table, casual/ranked
queues, leaderboard, history, collection, missions, wallet, store, settings,
season/event availability and account deletion panels. The Unity client sends
an account JWT in the Socket.IO handshake, refreshes credentials, reconnects
with bounded backoff, and recovers a seat and private snapshot without replaying
unacknowledged game actions. A public-WSS Play Mode test forces a websocket
abort and verifies the same seat/hand is recovered. Turn timeouts prevent a
disconnected or idle player from permanently holding the match.

Apple/Google browser sign-in and linking require `PUBLIC_BASE_URL` plus provider
configuration. Google requires `GOOGLE_WEB_CLIENT_ID`, `GOOGLE_WEB_CLIENT_SECRET`
and its authorized redirect URI. Apple requires `APPLE_SIGNIN_CLIENT_ID` (web
Services ID), `APPLE_WEB_CLIENT_SECRET` (valid developer-signed JWT), and its
return URL. Redirects are `/v1/auth/browser/callback/google` and
`/v1/auth/browser/callback/apple` on that base URL. The app opens the system
browser and polls using a one-time secret; account tokens are never put in a
redirect URL. Login buttons stay unavailable until their provider is configured.
Apple account token revocation on deletion still needs provider integration
before an App Store release.

Store offers come from `STORE_PRODUCTS_JSON`; Unity IAP 5.4.4 fetches localized
prices, binds purchases to the account, and only confirms after the server grant.
Google license test purchases require explicit `GOOGLE_PLAY_ENV=test`; Apple
sandbox receipts require `APPLE_STORE_ENV=sandbox`. Neither setting should be
used for a live production economy. No store products or credentials are currently
configured, so no chargeable offers appear. Store notification/refund ingestion
and real sandbox/device purchase tests remain release blockers.

Season/event screens use `LIVE_CONTENT_JSON` (optional `season`/`event` objects
with `title` and `description`). They honestly show no active content when none
is configured; no paid season or event reward rules have been invented.

MongoDB checkpoints private room state transactionally before acknowledging
commands. Production startup requires `MONGODB_URI` and `AUTH_SECRET`; room
worker ownership is fenced with a renewable Mongo lease. Multiple workers also
need application-level room routing before this can be horizontally scaled.

## Rules server development

Use Node.js 22 or newer.


```bash
cd server
npm install
npm test
npm start
```

The process listens on port `3000`, or `PORT` if you set it. `GET /health` returns `{ "ok": true }`. `GET /protocol` returns the event list.

The Unity client uses `PlatformClient.DefaultUrl`, currently the authenticated
ngrok endpoint on this Mac. The lobby offers online casual/ranked matching,
private host/join, and local practice. A private host may start with humans or
explicitly fill open seats with bots. Public matches require human accounts.
The old unauthenticated process on port 3000 has been stopped.

`lobby:start` accepts `{ "bots": 5 }` to fill empty seats. Those seats answer on the server.

The seed cannot be chosen by network clients. Deck shuffling uses cryptographic randomness.

## Table

| Client event | Payload | Who |
| --- | --- | --- |
| `lobby:create` | `{ "name": "Ann" }` | Opens a table and becomes host |
| `lobby:join` | `{ "code": "K7QP", "name": "Ben" }` | Takes the next seat |
| `lobby:ready` | `{ "ready": true }` | Ready up. Omit `ready` to toggle |
| `lobby:start` | `{ "bots": 0 }` optional | Host, once everyone is ready |
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

## Frontend verification

Unity Edit Mode tests include core/AI regression cases, socket URL validation,
JSON control-character/Unicode handling, and launch-mode isolation. Play Mode
integration tests load every panel from the live backend, inspect TMP overflow,
exercise cancellation, start an authenticated match, force a real websocket abort,
verify seat/hand recovery, and start a two-account ranked game. Run
`Coup.Frontend.PlayTests` with the configured backend and tunnel available. These
integration tests create ephemeral Editor guest accounts.

The mobile vault uses Android Keystore AES-GCM or iOS Keychain. Editor credentials
stay in memory. Native bridge source compilation has been checked; physical-device
lifecycle, encrypted-backup restore, offline-to-online switching, store sandbox and
provider login tests are still required before release. Automated tests cannot
establish that every possible production bug is covered.

### Table voice (Vivox)

Enable Vivox for the linked Unity Cloud project. Configure these **server-only**
environment variables using the credentials supplied by Unity:

- `VIVOX_SERVER`: Vivox API server URL.
- `VIVOX_DOMAIN`: Vivox domain.
- `VIVOX_ISSUER`: token issuer.
- `VIVOX_SIGNING_KEY`: secret signing key; never include in Unity resources/builds.

Restart the game service after configuration. `voice:access` issues config or
90-second login/join tokens only for the authenticated socket's current seat.
Channel IDs include table creation time to isolate reused lobby codes. No client
can request an arbitrary user, channel, moderation action or signing lifetime.
The client joins audio-only, starts muted, and disconnects on table exit.

Verification still required after provisioning: two-device join/hearing, mic
permission denial, individual devices' audio routing, disconnect/reconnect,
background mute, and release Android/iOS builds. Current unit/socket tests verify
server authorization and token signing, not actual Vivox connectivity.
