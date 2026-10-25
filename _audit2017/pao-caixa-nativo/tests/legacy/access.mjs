import { randomBytes, randomUUID, scrypt, timingSafeEqual } from 'node:crypto';
import { promisify } from 'node:util';
import { activeSession, openRegister, finalizeSale, closeRegister, moveCash, cancelSale, saveProduct, adjustStock, saveSettings, validateState } from './domain.mjs';
import { ConflictError } from './database.mjs';

export const OWNER_ID = 'owner-biel';
export const OWNER_EMAIL = 'biel.bitencourt01@gmail.com';
const derive = promisify(scrypt);
const options = { N: 131072, r: 8, p: 1, maxmem: 256 * 1024 * 1024 };
const emptyDraft = () => ({ cart: [], discount: '', note: '' });
export async function hashPassword(password, salt = randomBytes(16).toString('hex')) {
  if (typeof password !== 'string' || password.length < 8 || password.length > 128) throw new Error('A senha deve ter entre 8 e 128 caracteres.');
  return { salt, password_hash: (await derive(password, salt, 64, options)).toString('hex') };
}
async function verify(password, record) {
  if (typeof password !== 'string' || password.length > 128) return false;
  const actual = await derive(password, record.salt, 64, options);
  const expected = Buffer.from(record.password_hash, 'hex');
  return expected.length === actual.length && timingSafeEqual(expected, actual);
}
const publicUser = row => ({ id: row.id, email: row.email, name: row.name, role: row.role, owner: !!row.is_owner, active: !!row.active });
function emailAddress(raw) {
  if (typeof raw !== 'string' || raw.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(raw.trim())) throw new Error('Informe um e-mail válido.');
  return raw.trim().toLowerCase();
}
function userName(raw) {
  if (typeof raw !== 'string' || !raw.trim() || raw.trim().length > 60) throw new Error('Informe um nome de até 60 caracteres.');
  return raw.trim();
}
function roleName(raw) { if (!['admin','operator','financial'].includes(raw)) throw new Error('Perfil inválido.'); return raw; }

export class Access {
  constructor(database, bootstrap) {
    this.database = database; this.sql = database.db; this.session = null; this.loginBusy = false; this.generation = 0;
    if (!this.sql.prepare('SELECT id FROM users WHERE is_owner=1').get()) {
      if (!bootstrap || !/^[a-f0-9]{32}$/.test(bootstrap.salt) || !/^[a-f0-9]{128}$/.test(bootstrap.password_hash)) throw new Error('Configuração da conta proprietária ausente.');
      const state = database.load(), draft = database.getDraft();
      this.sql.exec('BEGIN IMMEDIATE;');
      try {
        this.sql.prepare('INSERT INTO users(id,email,name,role,is_owner,active,salt,password_hash,created_at) VALUES(?,?,?,?,1,1,?,?,?)').run(OWNER_ID, OWNER_EMAIL, 'Biel Carreon', 'admin', bootstrap.salt, bootstrap.password_hash, new Date().toISOString());
        // Preserve a pre-login sale draft for the owner, never give it to a new operator.
        this.sql.prepare('INSERT INTO user_drafts VALUES(?,?)').run(OWNER_ID, JSON.stringify(draft));
        database.setDraft(emptyDraft());
        for (const session of state.sessions.filter(s => s.status === 'open' && !s.userId)) {
          session.userId = OWNER_ID;
          this.sql.prepare('UPDATE sessions SET payload=? WHERE id=?').run(JSON.stringify(session), session.id);
        }
        this.sql.exec('COMMIT;');
      } catch (e) { this.sql.exec('ROLLBACK;'); throw e; }
    }
    const owner = this.sql.prepare('SELECT * FROM users WHERE is_owner=1').get();
    if (owner.id !== OWNER_ID || owner.email !== OWNER_EMAIL || owner.role !== 'admin' || !owner.active) throw new Error('Conta proprietária inválida. Recupere um backup.');
    this.dummy = { salt: bootstrap.salt, password_hash: bootstrap.password_hash };
  }
  audit(actor, action, detail = '') { this.sql.prepare('INSERT INTO access_audit(at,actor_id,action,detail) VALUES(?,?,?,?)').run(new Date().toISOString(), actor.id, action, detail); }
  control() { return { ...this.sql.prepare('SELECT blocked,reason,changed_at AS changedAt FROM installation_control WHERE singleton=1').get(), blocked: !!this.sql.prepare('SELECT blocked FROM installation_control WHERE singleton=1').get().blocked }; }
  current(allowBlocked = false) {
    const row = this.session && this.sql.prepare('SELECT * FROM users WHERE id=?').get(this.session.id);
    if (!row || !row.active || row.credential_version !== this.session.version) { this.session = null; throw new Error('Entre com seu e-mail e senha para continuar.'); }
    if (!allowBlocked && !row.is_owner && this.control().blocked) throw new Error('O acesso desta instalação foi suspenso. Entre em contato com o responsável pelo sistema.');
    return publicUser(row);
  }
  me() { if (!this.session) return null; try { return this.current(); } catch { this.session = null; return null; } }
  owner() { const user = this.current(); if (!user.owner) throw new Error('Este painel é exclusivo da conta proprietária.'); return user; }
  admin() { const user = this.current(); if (user.role !== 'admin') throw new Error('Somente administradores podem realizar esta operação.'); return user; }
  cashier() { const user = this.current(); if (user.role === 'financial') throw new Error('O perfil financeiro possui acesso de consulta e não pode operar o caixa.'); return user; }
  async login(email, password) {
    if (this.loginBusy) throw new Error('Aguarde a tentativa de entrada terminar.');
    this.loginBusy = true; const generation = ++this.generation;
    try {
      if (this.session) throw new Error('Saia da conta atual antes de entrar em outra.');
      const attempts = this.sql.prepare('SELECT * FROM login_attempts WHERE singleton=1').get();
      if (attempts?.blocked_until > Date.now()) throw new Error(`Muitas tentativas. Aguarde ${Math.ceil((attempts.blocked_until-Date.now())/1000)} segundos.`);
      const normalized = typeof email === 'string' ? email.trim().toLowerCase().slice(0,254) : '';
      const record = this.sql.prepare('SELECT * FROM users WHERE email=?').get(normalized);
      const correct = await verify(password, record || this.dummy);
      if (generation !== this.generation) throw new Error('Tentativa de entrada cancelada.');
      if (!record || !record.active || !correct) {
        const failures = (attempts?.failures || 0) + 1;
        const blockedUntil = failures >= 5 ? Date.now() + Math.min(600000, 30000 * 2 ** Math.min(5, failures-5)) : 0;
        this.sql.prepare('INSERT INTO login_attempts VALUES(1,?,?) ON CONFLICT(singleton) DO UPDATE SET failures=excluded.failures,blocked_until=excluded.blocked_until').run(failures,blockedUntil);
        throw new Error('E-mail ou senha inválidos, ou conta desativada.');
      }
      if (!record.is_owner && this.control().blocked) throw new Error('O acesso desta instalação foi suspenso. Entre em contato com o responsável pelo sistema.');
      this.sql.exec('DELETE FROM login_attempts;');
      this.session = { id: record.id, version: record.credential_version };
      const user = this.current(); this.audit(user, 'login'); return user;
    } finally { this.loginBusy = false; }
  }
  logout() { this.generation++; this.session = null; }
  view() {
    const user = this.current(), state = this.database.load();
    if (user.role !== 'operator') return state;
    const open = activeSession(state), own = open?.userId === user.id ? open : null;
    return {
      ...state, registerOccupied: !!open && !own,
      settings: { ...state.settings, operator: user.name },
      products: state.products.map(p => ({ ...p, cost: 0 })),
      sessions: own ? [own] : [],
      sales: own ? state.sales.filter(s => s.sessionId === own.id).map(s => ({ ...s, items: s.items.map(i => ({ ...i, cost: 0 })) })) : [],
      cashMoves: own ? state.cashMoves.filter(m => m.sessionId === own.id) : [], stockMoves: []
    };
  }
  getDraft() {
    const user = this.current(); if (user.role === 'financial') return emptyDraft();
    const row = this.sql.prepare('SELECT payload FROM user_drafts WHERE user_id=?').get(user.id);
    return row ? JSON.parse(row.payload) : emptyDraft();
  }
  setDraft(draft) {
    const user = this.cashier();
    // Reuse the existing strict validator without keeping a shared draft.
    this.sql.exec('BEGIN IMMEDIATE;');
    try {
      this.database.setDraft(draft);
      this.sql.prepare('INSERT INTO user_drafts VALUES(?,?) ON CONFLICT(user_id) DO UPDATE SET payload=excluded.payload').run(user.id,JSON.stringify(draft));
      this.database.setDraft(emptyDraft()); this.sql.exec('COMMIT;');
    } catch(e) { this.sql.exec('ROLLBACK;'); throw e; }
  }
  hasDrafts() { return this.sql.prepare('SELECT payload FROM user_drafts').all().some(r => JSON.parse(r.payload).cart.length); }
  command(command, args, revision) {
    const user = this.current(), state = this.database.load();
    if (state.revision !== revision) throw new ConflictError();
    if (!args || typeof args !== 'object' || Array.isArray(args)) throw new Error('Operação inválida.');
    const open = activeSession(state);
    const ownRegister = () => { this.cashier(); if (user.role === 'operator' && open?.userId !== user.id) throw new Error('Você só pode operar seu próprio caixa aberto.'); };
    let next;
    switch (command) {
      case 'register.open':
        this.cashier(); next = openRegister(state, args.opening, user.name); next.sessions[0].userId = user.id; break;
      case 'register.close': ownRegister(); next = closeRegister(state, args.counted); break;
      case 'register.move': ownRegister(); next = moveCash(state,args.kind,args.amount,args.reason); break;
      case 'sale.finish':
        ownRegister(); next = finalizeSale(state,args.cart,args.discount,args.payments,args.received,args.note); next.sales[0].userId = user.id; next.sales[0].operator = user.name; break;
      case 'sale.cancel':
        ownRegister(); if (user.role === 'operator' && state.sales.find(s => s.id === args.id)?.userId !== user.id) throw new Error('Você só pode cancelar suas próprias vendas do caixa atual.');
        next = cancelSale(state,args.id,args.reason); break;
      case 'product.save': this.admin(); next = saveProduct(state,args.product); break;
      case 'stock.adjust': this.admin(); next = adjustStock(state,args.id,args.quantity,args.reason); break;
      case 'settings.save': this.admin(); next = saveSettings(state,args.settings); break;
      default: throw new Error('Operação não permitida.');
    }
    this.database.write(next,revision,false,user.id); return this.view();
  }
  restore(incoming, revision) {
    const user = this.admin(), state = structuredClone(validateState(incoming));
    // Imported business backups cannot assign a cash register to another user.
    for (const session of state.sessions) { delete session.userId; if (session.status === 'open') session.userId = user.id; }
    for (const sale of state.sales) delete sale.userId;
    this.database.restore(state,revision); this.audit(user,'restore'); return this.view();
  }
  panel() {
    this.owner(); return { users: this.sql.prepare('SELECT * FROM users ORDER BY is_owner DESC,created_at').all().map(publicUser), control: this.control(), audit: this.sql.prepare('SELECT at,action,detail FROM access_audit ORDER BY id DESC LIMIT 50').all() };
  }
  async createUser(args) {
    const owner = this.owner(), email = emailAddress(args.email), name = userName(args.name), role = roleName(args.role);
    if (this.sql.prepare('SELECT id FROM users WHERE email=?').get(email)) throw new Error('Já existe uma conta com este e-mail.');
    const credential = await hashPassword(args.password); this.owner();
    const id = randomUUID();
    this.sql.prepare('INSERT INTO users(id,email,name,role,salt,password_hash,created_at) VALUES(?,?,?,?,?,?,?)').run(id,email,name,role,credential.salt,credential.password_hash,new Date().toISOString());
    this.audit(owner,'account.create',email); return this.panel();
  }
  async updateUser(args) {
    const owner = this.owner(), row = this.sql.prepare('SELECT * FROM users WHERE id=?').get(args.id);
    if (!row || row.is_owner) throw new Error('A conta proprietária é protegida e não pode ser alterada por este painel.');
    const name = userName(args.name), role = roleName(args.role);
    if (typeof args.active !== 'boolean') throw new Error('Situação da conta inválida.');
    const credential = args.password ? await hashPassword(args.password) : { salt: row.salt, password_hash: row.password_hash };
    this.owner();
    this.sql.prepare('UPDATE users SET name=?,role=?,active=?,salt=?,password_hash=?,credential_version=credential_version+1 WHERE id=?').run(name,role,args.active?1:0,credential.salt,credential.password_hash,row.id);
    this.audit(owner,'account.update',row.email); return this.panel();
  }
  setBlocked(blocked, reason) {
    const owner = this.owner(); if (typeof blocked !== 'boolean' || typeof reason !== 'string' || reason.length > 200) throw new Error('Controle de acesso inválido.');
    this.sql.prepare('UPDATE installation_control SET blocked=?,reason=?,changed_at=? WHERE singleton=1').run(blocked?1:0,reason.trim(),new Date().toISOString());
    this.audit(owner,blocked?'installation.block':'installation.unblock',reason.trim()); return this.panel();
  }
  async changePassword(currentPassword, newPassword) {
    const user = this.current(), row = this.sql.prepare('SELECT * FROM users WHERE id=?').get(user.id);
    if (!await verify(currentPassword,row)) throw new Error('A senha atual está incorreta.');
    const credential = await hashPassword(newPassword); const current = this.current();
    if (current.id !== user.id) throw new Error('A sessão mudou. Entre novamente.');
    this.sql.prepare('UPDATE users SET salt=?,password_hash=?,credential_version=credential_version+1 WHERE id=?').run(credential.salt,credential.password_hash,user.id);
    this.session.version++; this.audit(user,'password.change'); return this.current();
  }
}
