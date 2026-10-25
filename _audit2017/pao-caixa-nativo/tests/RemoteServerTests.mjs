import worker from '../servidor-cloudflare/worker.mjs';
import assert from 'node:assert/strict';
class MockKV {data=new Map();async get(key,mode){const raw=this.data.get(key)??null;return mode==='json'&&raw?JSON.parse(raw):raw;}async put(key, value){this.data.set(key,value);}async list({prefix,limit,cursor}){const keys=[...this.data.keys()].filter(x=>x.startsWith(prefix)).sort();const start=cursor?Number(cursor):0;return {keys:keys.slice(start,start+limit).map(name=>({name})),list_complete:start+limit>=keys.length,cursor:String(start+limit)};}}
const env={DEVICES:new MockKV(),ADMIN_TOKEN:'1234567890abcdef1234567890abcdef'};
const ID='8b9b8b9b-1111-4aaa-9abc-111111111111';
async function request(path,method='GET',value,token){return worker.fetch(new Request('https://example.workers.dev'+path,{method,headers:{...(token?{'Authorization':'Bearer '+token}:{}),'Content-Type':'application/json'},body:value===undefined?undefined:JSON.stringify(value)}),env);}
async function read(res){return res.json();}
let r=await request('/api/device','POST',{id:ID,computer:'PADARIA-PC',version:'2.0.3'});
assert.equal(r.status,200);assert.equal((await read(r)).status,'allowed');
r=await request('/api/admin/devices');assert.equal(r.status,401);
r=await request('/api/admin/devices','GET',undefined,env.ADMIN_TOKEN);assert.equal((await read(r)).devices.length,1);
r=await request('/api/admin/state','POST',{id:ID,blocked:true,reason:'Contrato encerrado'},env.ADMIN_TOKEN);assert.equal(r.status,200);
r=await request('/api/device','POST',{id:ID,computer:'PADARIA-PC',version:'2.0.3'});assert.deepEqual(await read(r),{id:ID,status:'blocked',reason:'Contrato encerrado'});
r=await request('/api/admin/name','POST',{id:ID,name:'Padaria Centro'},env.ADMIN_TOKEN);assert.equal(r.status,200);
r=await request('/api/admin/devices','GET',undefined,env.ADMIN_TOKEN);assert.equal((await read(r)).devices[0].name,'Padaria Centro');
r=await request('/api/admin/state','POST',{id:ID,blocked:false,reason:''},env.ADMIN_TOKEN);assert.equal(r.status,200);
r=await request('/api/device','POST',{id:ID,computer:'PADARIA-PC',version:'2.0.3'});assert.equal((await read(r)).status,'allowed');
r=await request('/api/admin/state','POST',{id:ID,blocked:true,reason:'x'},'senha-incorreta');assert.equal(r.status,401);
r=await request('/api/device','POST',{id:'invalido',computer:'X',version:'2.0.3'});assert.equal(r.status,400);
r=await request('/');assert.equal(r.status,200);assert.match(await r.text(),/Controle Remoto/);
console.log('OK: cadastro, autenticação, listagem, apelido, bloqueio, persistência, desbloqueio, entrada inválida e painel.');
