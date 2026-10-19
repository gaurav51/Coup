import assert from 'node:assert/strict';
import { io } from 'socket.io-client';
const base = process.argv[2] || 'http://127.0.0.1:3001';
async function request(route, body, token) {
  const response = await fetch(base + route, { method: body ? 'POST' : 'GET',
    headers: { 'ngrok-skip-browser-warning': '1', ...(body ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(15000) });
  return { status: response.status, body: await response.json() };
}
function connect(token, shouldReject = false) {
  return new Promise((resolve, reject) => {
    const socket = io(base, { transports: ['websocket'], auth: { accessToken: token }, reconnection: false, timeout: 15000 });
    socket.once('connect', () => shouldReject ? (socket.disconnect(), reject(new Error('Unauthenticated socket was accepted'))) : resolve(socket));
    socket.once('connect_error', error => { socket.disconnect(); shouldReject ? resolve(null) : reject(error); });
  });
}
function command(socket, event, body = {}) {
  return new Promise((resolve, reject) => socket.timeout(15000).emit(event, body, (err, result) => {
    if (err) reject(err); else if (!result.ok) reject(new Error(result.error)); else resolve(result);
  }));
}
let socket, account;
try {
  assert.equal((await request('/ready')).body.authenticated, true);
  assert.equal((await request('/v1/profile')).status, 401);
  await connect('invalid-test-token', true);
  const guest = await request('/v1/auth/guest', { name: 'Hosting Check' });
  assert.equal(guest.status, 200); account = guest.body;
  assert.equal((await request('/v1/profile', null, account.accessToken)).body.profile.id, account.accountId);
  socket = await connect(account.accessToken);
  const table = await command(socket, 'lobby:create', { name: 'Hosting Check' });
  await command(socket, 'lobby:ready', { ready: true });
  let snapshot;
  socket.on('game:state', state => { snapshot = state; });
  await command(socket, 'lobby:start', { bots: 1, seed: 12345 });
  assert.ok(snapshot?.hand?.length === 2);
  const hand = [...snapshot.hand];
  socket.disconnect();
  const rotated = await request('/v1/auth/refresh', account);
  assert.equal(rotated.status, 200);
  account = { ...account, ...rotated.body };
  socket = await connect(account.accessToken);
  const restored = new Promise(resolve => socket.once('game:state', resolve));
  await command(socket, 'session:resume', { code: table.code, token: table.token });
  assert.deepEqual((await restored).hand, hand);
  await command(socket, 'lobby:leave');
  assert.equal((await request('/v1/auth/logout', {}, account.accessToken)).status, 200);
  assert.equal((await request('/v1/profile', null, account.accessToken)).status, 401);
  console.log('PASS: readiness, protected HTTP/WebSocket, guest sign-in, refresh, match start, private hand recovery, logout.');
} finally {
  socket?.disconnect();
}
