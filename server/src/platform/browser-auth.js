import crypto from 'node:crypto';
import { hash } from './auth.js';
import { requireValue } from './errors.js';
export class BrowserAuth {
  constructor(store, accounts, env = process.env) { this.store = store; this.accounts = accounts; this.env = env; }
  configured(provider) {
    return !!this.env.PUBLIC_BASE_URL && (provider === 'google' ? !!(this.env.GOOGLE_WEB_CLIENT_ID && this.env.GOOGLE_WEB_CLIENT_SECRET) : provider === 'apple' && !!(this.env.APPLE_SIGNIN_CLIENT_ID && this.env.APPLE_WEB_CLIENT_SECRET));
  }
  redirect(provider) { return `${this.env.PUBLIC_BASE_URL}/v1/auth/browser/callback/${provider}`; }
  async start(provider, accountId) {
    requireValue(this.configured(provider), 503, `${provider} sign-in is not configured yet.`);
    const challenge = await this.accounts.nonce(provider);
    const state = crypto.randomBytes(32).toString('base64url'), pollToken = crypto.randomBytes(32).toString('base64url');
    const verifier = crypto.randomBytes(32).toString('base64url');
    await this.store.collection('browserAuth').insertOne({ _id: state, pollHash: hash(pollToken), provider, accountId, challengeId: challenge.challengeId, verifier, expiresAt: new Date(Date.now() + 300000) });
    const url = new URL(provider === 'google' ? 'https://accounts.google.com/o/oauth2/v2/auth' : 'https://appleid.apple.com/auth/authorize');
    url.search = new URLSearchParams({ client_id: provider === 'google' ? this.env.GOOGLE_WEB_CLIENT_ID : this.env.APPLE_SIGNIN_CLIENT_ID,
      redirect_uri: this.redirect(provider), response_type: 'code', scope: provider === 'google' ? 'openid profile' : 'name email',
      state, nonce: challenge.providerNonce,
      ...(provider === 'google' ? { code_challenge: crypto.createHash('sha256').update(verifier).digest('base64url'), code_challenge_method: 'S256' } : { response_mode: 'form_post' }) }).toString();
    return { url: url.href, state, pollToken };
  }
  async callback(provider, body) {
    const row = await this.store.collection('browserAuth').findOne({ _id: body.state, provider, expiresAt: { $gt: new Date() }, completed: { $ne: true } });
    requireValue(row, 400, 'Sign-in expired. Return to the game and try again.');
    if (body.error) { await this.store.collection('browserAuth').updateOne({ _id: row._id }, { $set: { completed: true, error: 'Sign-in was cancelled.' } }); return; }
    requireValue(typeof body.code === 'string' && body.code.length < 8192, 400, 'Invalid authorization response.');
    const response = await fetch(provider === 'google' ? 'https://oauth2.googleapis.com/token' : 'https://appleid.apple.com/auth/token', {
      method: 'POST', signal: AbortSignal.timeout(10000), headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ code: body.code, grant_type: 'authorization_code', redirect_uri: this.redirect(provider),
        client_id: provider === 'google' ? this.env.GOOGLE_WEB_CLIENT_ID : this.env.APPLE_SIGNIN_CLIENT_ID,
        client_secret: provider === 'google' ? this.env.GOOGLE_WEB_CLIENT_SECRET : this.env.APPLE_WEB_CLIENT_SECRET,
        ...(provider === 'google' ? { code_verifier: row.verifier } : {}) })
    });
    const tokens = await response.json();
    requireValue(response.ok && typeof tokens.id_token === 'string', 401, 'Sign-in could not be verified. Return to the game and try again.');
    await this.store.collection('browserAuth').updateOne({ _id: row._id, completed: { $ne: true } }, { $set: { completed: true, idToken: tokens.id_token }, $unset: { verifier: '' } });
  }
  async poll(state, pollToken) {
    requireValue(typeof state === 'string' && typeof pollToken === 'string' && pollToken.length < 256, 400, 'Invalid sign-in request.');
    const query = { _id: state, pollHash: hash(pollToken), expiresAt: { $gt: new Date() } };
    const row = await this.store.collection('browserAuth').findOne(query);
    requireValue(row, 410, 'Sign-in expired. Please try again.');
    if (!row.completed) return { pending: true };
    const consumed = await this.store.collection('browserAuth').findOneAndDelete({ ...query, completed: true });
    requireValue(consumed, 410, 'Sign-in was already completed.');
    requireValue(!consumed.error, 401, consumed.error);
    return this.accounts.social(row.provider, { challengeId: row.challengeId, idToken: row.idToken }, row.accountId);
  }
}
