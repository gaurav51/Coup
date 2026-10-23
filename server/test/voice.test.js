import test from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { voiceAccess } from '../src/voice.js';
const env = { VIVOX_SERVER: 'https://voice.example', VIVOX_DOMAIN: 'voice.example', VIVOX_ISSUER: 'test', VIVOX_SIGNING_KEY: 'test-only' };
const socket = { id: 's1', data: { accountId: 'a1' } };
const seat = { room: { code: 'ABCD', createdAt: 1 }, player: { accountId: 'a1' } };
const store = { findBySocket: () => seat };
test('voice requires authenticated current seat and server config', () => {
  assert.throws(() => voiceAccess({ findBySocket: () => null }, socket, 'join', env));
  assert.throws(() => voiceAccess(store, { ...socket, data: { accountId: 'other' } }, 'join', env));
  assert.throws(() => voiceAccess(store, socket, 'join', {}));
  assert.throws(() => voiceAccess(store, socket, 'kick', env));
});
test('tokens have server-derived identity, room scope, expiry, signature, and unique nonce', () => {
  const config = voiceAccess(store, socket, 'config', env);
  assert.equal(config.token, undefined); assert.equal(config.key, undefined);
  const first = voiceAccess(store, socket, 'join', env).token;
  const [header, payload, sig] = first.split('.');
  const claims = JSON.parse(Buffer.from(payload, 'base64url'));
  assert.equal(claims.t, `sip:confctl-g-test.${config.channel}@voice.example`);
  assert.equal(claims.f, `sip:.test.${config.user}.@voice.example`);
  assert.ok(claims.exp > Date.now()/1000 && claims.exp < Date.now()/1000 + 91);
  assert.equal(sig, crypto.createHmac('sha256', env.VIVOX_SIGNING_KEY).update(`${header}.${payload}`).digest('base64url'));
  assert.notEqual(first, voiceAccess(store, socket, 'join', env).token);
  assert.equal(JSON.parse(Buffer.from(voiceAccess(store,socket,'login',env).token.split('.')[1], 'base64url')).t, undefined);
});
