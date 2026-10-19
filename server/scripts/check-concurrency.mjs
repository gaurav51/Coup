// Isolated local load smoke test, not an AWS capacity guarantee.
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { performance } from 'node:perf_hooks';
import { io as connect } from 'socket.io-client';
import { MongoClient } from 'mongodb';
import { createPlatform } from '../src/platform/runtime.js';
import { createApp } from '../src/index.js';
const count=100;
const database='coup_load_check_'+crypto.randomBytes(8).toString('hex');
const uri='mongodb://127.0.0.1:27017/?replicaSet=coupLocal';
const platform=await createPlatform({MONGODB_URI:uri,MONGODB_DATABASE:database,AUTH_SECRET:crypto.randomBytes(48).toString('base64url'),WORKER_ID:'load-check'});
const app=createApp({platform});const sockets=[];
try {
  await new Promise(resolve=>app.httpServer.listen(0,'127.0.0.1',resolve));
  const url=`http://127.0.0.1:${app.httpServer.address().port}`;
  const accounts=await Promise.all(Array.from({length:count},(_,i)=>platform.accounts.guest('Load '+i)));
  await Promise.all(accounts.map(account=>new Promise((resolve,reject)=>{
    const socket=connect(url,{transports:['websocket'],auth:{accessToken:account.accessToken},reconnection:false,timeout:15000});
    sockets.push(socket);socket.once('connect',resolve);socket.once('connect_error',reject);
  })));
  const times=await Promise.all(sockets.map((socket,i)=>new Promise((resolve,reject)=>{
    const start=performance.now();socket.timeout(15000).emit('lobby:create',{name:'Load '+i},(err,result)=>{
      if(err||!result.ok)reject(err||new Error(result.error));else resolve(performance.now()-start);
    });
  })));
  assert.equal(platform.rooms.rooms.size,count);
  times.sort((a,b)=>a-b);
  console.log(JSON.stringify({clients:count,createdRooms:count,p50AckMs:Math.round(times[49]),p95AckMs:Math.round(times[94]),maxAckMs:Math.round(times[99]),scope:'local concurrent authenticated lobby creation'}));
} finally {
  for(const socket of sockets)socket.disconnect();
  await new Promise(resolve=>app.io.close(resolve));await platform.close();
  const cleanup=new MongoClient(uri);await cleanup.connect();await cleanup.db(database).dropDatabase();await cleanup.close();
}
