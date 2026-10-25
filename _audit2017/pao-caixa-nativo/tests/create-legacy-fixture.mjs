import {mkdirSync,writeFileSync} from 'node:fs';
import {scryptSync} from 'node:crypto';
import {LocalDatabase} from './legacy/database.mjs';
import {Access,hashPassword} from './legacy/access.mjs';
const dir=process.argv[2];mkdirSync(dir,{recursive:true});
const db=new LocalDatabase(dir), credential=await hashPassword('Teste-Pdv-2026');
const access=new Access(db,credential);await access.login('biel.bitencourt01@gmail.com','Teste-Pdv-2026');
await access.createUser({email:'operador@teste.local',name:'Operador teste',role:'operator',password:'Teste-Pdv-2026'});
await access.createUser({email:'financeiro@teste.local',name:'Financeiro teste',role:'financial',password:'Teste-Pdv-2026'});
await access.createUser({email:'admin@teste.local',name:'Admin teste',role:'admin',password:'Teste-Pdv-2026'});
const command=(cmd,args)=>access.command(cmd,args,db.load().revision);
command('register.open',{opening:10000});
const state=db.load(),juice=state.products.find(p=>p.code==='010'),bread=state.products.find(p=>p.code==='001'),cheese=state.products.find(p=>p.code==='002'),misto=state.products.find(p=>p.code==='011');
command('sale.finish',{cart:[{productId:juice.id,quantity:5000},{productId:bread.id,quantity:1000}],discount:0,payments:[{method:'dinheiro',amount:6140}],received:10000,note:'Venda anterior à migração'});
command('sale.finish',{cart:[{productId:cheese.id,quantity:3000},{productId:misto.id,quantity:1000}],discount:0,payments:[{method:'credito',amount:2140}],received:0,note:''});
command('register.move',{kind:'supply',amount:1000,reason:'Troco'});
command('register.move',{kind:'withdrawal',amount:2000,reason:'Cofre'});
access.setDraft({cart:[{productId:cheese.id,quantity:1000}],discount:'0',note:'Pedido preservado'});
const operator=db.db.prepare("SELECT id FROM users WHERE role='operator'").get();db.db.prepare('INSERT INTO user_drafts VALUES(?,?)').run(operator.id,JSON.stringify({cart:[{productId:juice.id,quantity:1000}],discount:'',note:'Privado do operador'}));
writeFileSync(dir+'/before.json',JSON.stringify(db.load()));
writeFileSync(dir+'/identities.json',JSON.stringify(db.db.prepare('SELECT id,email,role,salt,password_hash,credential_version FROM users ORDER BY id').all()));
db.close();
writeFileSync(dir+'/vectors.json',JSON.stringify([
 {password:'',salt:'',N:16,r:1,p:1},
 {password:'pão-ç-🍞',salt:'0123456789abcdef0123456789abcdef',N:1024,r:8,p:1}
].map(x=>({...x,expected:scryptSync(x.password,x.salt,64,{N:x.N,r:x.r,p:x.p,maxmem:256*1024*1024}).toString('hex')}))));
console.log('Fixture 1.2.1 criada com contas e pagamentos de teste.');
