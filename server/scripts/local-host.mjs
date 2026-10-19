// Development hosting on this Mac. One persistent Mongo replica member, not HA.
import { spawn } from 'node:child_process';
import { mkdir, writeFile, chmod, access } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import crypto from 'node:crypto';
import { MongoClient } from 'mongodb';
import { MongoBinary } from 'mongodb-memory-server';

const root = fileURLToPath(new URL('../', import.meta.url));
const data = path.join(root, '.data');
await mkdir(path.join(data, 'mongo'), { recursive: true, mode: 0o700 });
await chmod(data, 0o700);
const config = path.join(root, '.env.local');
try {
  await writeFile(config, `AUTH_SECRET=${crypto.randomBytes(48).toString('base64url')}\n`, { flag: 'wx', mode: 0o600 });
} catch (err) { if (err.code !== 'EEXIST') throw err; }
await chmod(config, 0o600);
process.loadEnvFile(config);
const children = [];
let stopping = false;
function start(command, args, options = {}) {
  const child = spawn(command, args, { cwd: root, stdio: 'inherit', ...options });
  children.push(child);
  child.once('error', err => { console.error(`[host] ${path.basename(command)}: ${err.code}`); stop(1); });
  child.once('exit', (code) => { if (!stopping) { console.error(`[host] ${path.basename(command)} exited (${code})`); stop(1); } });
  return child;
}
async function stop(code = 0) {
  if (stopping) return;
  stopping = true;
  // Stop public ingress before the application and database.
  for (const child of [...children].reverse()) {
    if (child.exitCode !== null || child.signalCode !== null) continue;
    await new Promise(resolve => {
      const kill = setTimeout(() => { child.kill('SIGKILL'); resolve(); }, 10000);
      child.once('exit', () => { clearTimeout(kill); resolve(); });
      child.kill('SIGTERM');
    });
  }
  process.exit(code);
}
process.on('SIGTERM', () => stop());
process.on('SIGINT', () => stop());
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
try {
  const mongod = await MongoBinary.getPath({ version: '8.0.17' });
  await access(mongod);
  start(mongod, ['--dbpath', path.join(data, 'mongo'), '--bind_ip', '127.0.0.1', '--port', '27017',
    '--replSet', 'coupLocal', '--logpath', path.join(data, 'mongo.log'), '--logappend']);
  let direct;
  for (let attempt = 0; attempt < 60; attempt++) {
    try {
      direct = new MongoClient('mongodb://127.0.0.1:27017/?directConnection=true', { serverSelectionTimeoutMS: 500 });
      await direct.connect(); break;
    } catch { await direct?.close(); direct = null; await pause(500); }
  }
  if (!direct) throw new Error('Local MongoDB did not start.');
  try {
    try { await direct.db('admin').command({ replSetGetStatus: 1 }); }
    catch (err) {
      if (err.code !== 94) throw err;
      await direct.db('admin').command({ replSetInitiate: { _id: 'coupLocal', members: [{ _id: 0, host: '127.0.0.1:27017' }] } });
    }
    let primary = false;
    for (let attempt = 0; attempt < 60; attempt++) {
      const hello = await direct.db('admin').command({ hello: 1 });
      if (hello.setName !== 'coupLocal') throw new Error('Unexpected local Mongo replica set.');
      if (hello.isWritablePrimary) { primary = true; break; }
      await pause(500);
    }
    if (!primary) throw new Error('Local MongoDB primary not ready.');
  } finally { await direct.close(); }
  const port = process.env.LOCAL_PORT || '3001';
  start(process.execPath, ['src/index.js'], { env: { ...process.env,
    HOST: '127.0.0.1', PORT: port, NODE_ENV: 'production', WORKER_ID: 'mac-local', TRUST_PROXY: '1',
    MONGODB_URI: 'mongodb://127.0.0.1:27017/?replicaSet=coupLocal', MONGODB_DATABASE: 'coup',
  } });
  let ready = false;
  for (let attempt = 0; attempt < 90; attempt++) {
    try { ready = (await fetch(`http://127.0.0.1:${port}/ready`, { signal: AbortSignal.timeout(1000) })).ok; }
    catch { /* Wait for startup / previous worker lease expiry. */ }
    if (ready) break;
    await pause(500);
  }
  if (!ready) throw new Error('Authenticated backend did not become ready.');
  console.log(`[host] Authenticated MongoDB backend ready on 127.0.0.1:${port}`);
  if (!process.argv.includes('--no-tunnel')) {
    const hostname = process.env.NGROK_HOSTNAME || 'curable-twistable-rising.ngrok-free.dev';
    if (!/^[a-z0-9.-]+$/.test(hostname)) throw new Error('Invalid NGROK_HOSTNAME.');
    start('/opt/homebrew/bin/ngrok', ['http', `http://127.0.0.1:${port}`, '--url', `https://${hostname}`, '--inspect=false', '--log', 'stdout', '--log-level', 'warn']);
    console.log(`[host] Starting HTTPS ingress: https://${hostname}`);
  }
} catch (err) {
  console.error('[host] Startup failed:', err.message);
  await stop(1);
}
