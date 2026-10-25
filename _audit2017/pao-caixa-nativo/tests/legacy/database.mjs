import { DatabaseSync } from 'node:sqlite';
import { existsSync, mkdirSync, readdirSync, unlinkSync } from 'node:fs';
import path from 'node:path';
import { createSeed, validateState } from './domain.mjs';

const COLLECTIONS = ['products', 'sales', 'sessions', 'cashMoves', 'stockMoves'];
const TABLES = { products: 'products', sales: 'sales', sessions: 'sessions', cashMoves: 'cash_moves', stockMoves: 'stock_moves' };
export class ConflictError extends Error { constructor() { super('Os dados mudaram. Confira a venda e tente novamente.'); this.code = 'CONFLICT'; } }

export class LocalDatabase {
  constructor(directory) {
    this.directory = directory;
    this.file = path.join(directory, 'padaria.sqlite');
    this.backups = path.join(directory, 'backups');
    mkdirSync(this.backups, { recursive: true });
    const existing = existsSync(this.file);
    this.db = new DatabaseSync(this.file);
    try {
      this.db.exec('PRAGMA busy_timeout=5000; PRAGMA synchronous=FULL;');
      if (existing && this.db.prepare('PRAGMA integrity_check').get().integrity_check !== 'ok') throw new Error('O banco precisa ser recuperado. O arquivo original foi preservado.');
      const version = this.db.prepare('PRAGMA user_version').get().user_version;
      if (version > 2) throw new Error('Este banco foi aberto por uma versão mais nova. Instale novamente a versão mais recente.');
      if (version === 0) {
        if (this.db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").all().length) throw new Error('Banco desconhecido. O arquivo foi preservado.');
        this.db.exec(`BEGIN IMMEDIATE;
          CREATE TABLE metadata (singleton INTEGER PRIMARY KEY CHECK(singleton=1), revision INTEGER NOT NULL, next_sale INTEGER NOT NULL, settings TEXT NOT NULL);
          ${COLLECTIONS.map(k => `CREATE TABLE ${TABLES[k]} (id TEXT PRIMARY KEY, position INTEGER NOT NULL, payload TEXT NOT NULL);`).join('\n')}
          CREATE TABLE draft (singleton INTEGER PRIMARY KEY CHECK(singleton=1), payload TEXT NOT NULL);
          PRAGMA user_version=1; COMMIT;`);
        this.write(createSeed(), null);
      }
      this.db.exec('PRAGMA journal_mode=WAL;');
      this.load(); // Invalid data must never be replaced by sample records.
      if (version < 2) {
        if (existing) this.backup('antes-contas');
        this.db.exec(`BEGIN IMMEDIATE;
          CREATE TABLE users (id TEXT PRIMARY KEY, email TEXT NOT NULL UNIQUE COLLATE NOCASE, name TEXT NOT NULL, role TEXT NOT NULL CHECK(role IN ('admin','operator','financial')), is_owner INTEGER NOT NULL DEFAULT 0 CHECK(is_owner IN (0,1)), active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)), salt TEXT NOT NULL, password_hash TEXT NOT NULL, credential_version INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL);
          CREATE UNIQUE INDEX only_one_owner ON users(is_owner) WHERE is_owner=1;
          CREATE TRIGGER protect_owner_delete BEFORE DELETE ON users WHEN OLD.is_owner=1 BEGIN SELECT RAISE(ABORT,'A conta proprietária não pode ser removida.'); END;
          CREATE TRIGGER protect_owner_update BEFORE UPDATE ON users WHEN OLD.is_owner=1 AND (NEW.id<>OLD.id OR NEW.email<>OLD.email OR NEW.role<>'admin' OR NEW.is_owner<>1 OR NEW.active<>1) BEGIN SELECT RAISE(ABORT,'A conta proprietária não pode ser alterada ou bloqueada.'); END;
          CREATE TABLE user_drafts (user_id TEXT PRIMARY KEY REFERENCES users(id), payload TEXT NOT NULL);
          CREATE TABLE login_attempts (singleton INTEGER PRIMARY KEY CHECK(singleton=1), failures INTEGER NOT NULL, blocked_until INTEGER NOT NULL);
          CREATE TABLE installation_control (singleton INTEGER PRIMARY KEY CHECK(singleton=1), blocked INTEGER NOT NULL DEFAULT 0, reason TEXT NOT NULL DEFAULT '', changed_at TEXT);
          INSERT INTO installation_control(singleton) VALUES(1);
          CREATE TABLE access_audit (id INTEGER PRIMARY KEY, at TEXT NOT NULL, actor_id TEXT NOT NULL, action TEXT NOT NULL, detail TEXT NOT NULL);
          PRAGMA user_version=2; COMMIT;`);
      }
    } catch (e) { this.db.close(); throw e; }
  }
  load() {
    const meta = this.db.prepare('SELECT * FROM metadata WHERE singleton=1').get();
    if (!meta) throw new Error('O banco está incompleto. Restaure um backup.');
    const state = { schemaVersion: 1, revision: meta.revision, nextSale: meta.next_sale, settings: JSON.parse(meta.settings) };
    for (const k of COLLECTIONS) state[k] = this.db.prepare(`SELECT payload FROM ${TABLES[k]} ORDER BY position`).all().map(r => JSON.parse(r.payload));
    return validateState(state);
  }
  write(incoming, expectedRevision, clearDraft = false, draftUserId = null) {
    const state = validateState(incoming);
    if (expectedRevision !== null && (!Number.isSafeInteger(expectedRevision) || state.revision !== expectedRevision + 1)) throw new Error('Revisão inválida.');
    this.db.exec('BEGIN IMMEDIATE;');
    try {
      const current = this.db.prepare('SELECT revision,next_sale FROM metadata WHERE singleton=1').get();
      if (expectedRevision !== null && current?.revision !== expectedRevision) throw new ConflictError();
      this.db.prepare('INSERT INTO metadata VALUES(1,?,?,?) ON CONFLICT(singleton) DO UPDATE SET revision=excluded.revision,next_sale=excluded.next_sale,settings=excluded.settings').run(state.revision, state.nextSale, JSON.stringify(state.settings));
      for (const k of COLLECTIONS) {
        const table = TABLES[k];
        const old = new Map(this.db.prepare(`SELECT * FROM ${table}`).all().map(r => [r.id, r]));
        const put = this.db.prepare(`INSERT INTO ${table} VALUES(?,?,?) ON CONFLICT(id) DO UPDATE SET position=excluded.position,payload=excluded.payload`);
        state[k].forEach((row, position) => {
          const payload = JSON.stringify(row), previous = old.get(row.id);
          if (!previous || previous.position !== position || previous.payload !== payload) put.run(row.id, position, payload);
          old.delete(row.id);
        });
        const del = this.db.prepare(`DELETE FROM ${table} WHERE id=?`);
        for (const id of old.keys()) del.run(id);
      }
      if (clearDraft || (current && state.nextSale > current.next_sale)) {
        this.setDraft({cart:[],discount:'',note:''});
        if (draftUserId) this.db.prepare('DELETE FROM user_drafts WHERE user_id=?').run(draftUserId);
        else if (clearDraft) this.db.exec('DELETE FROM user_drafts;');
      }
      this.db.exec('COMMIT;');
      return state;
    } catch (e) { this.db.exec('ROLLBACK;'); throw e; }
  }
  restore(incoming, expectedRevision) {
    const valid = validateState(incoming);
    if (this.load().revision !== expectedRevision) throw new ConflictError();
    this.backup('antes-restauracao');
    const result = this.write({ ...valid, revision: expectedRevision + 1 }, expectedRevision, true);
    return result;
  }
  getDraft() {
    const raw = this.db.prepare('SELECT payload FROM draft WHERE singleton=1').get();
    return raw ? JSON.parse(raw.payload) : { cart: [], discount: '', note: '' };
  }
  setDraft(draft) {
    if (!draft || !Array.isArray(draft.cart) || draft.cart.length > 10000 || typeof draft.discount !== 'string' || draft.discount.length > 50 || typeof draft.note !== 'string' || draft.note.length > 200) throw new Error('Venda em andamento inválida.');
    const seen = new Set();
    for (const item of draft.cart) {
      if (!item || typeof item.productId !== 'string' || seen.has(item.productId) || !Number.isSafeInteger(item.quantity) || item.quantity <= 0) throw new Error('Item da venda inválido.');
      seen.add(item.productId);
    }
    this.db.prepare('INSERT INTO draft VALUES(1,?) ON CONFLICT(singleton) DO UPDATE SET payload=excluded.payload').run(JSON.stringify(draft));
  }
  backup(reason = 'manual') {
    if (!/^[a-z-]+$/.test(reason)) throw new Error('Nome de backup inválido.');
    const destination = path.join(this.backups, `${new Date().toISOString().replaceAll(':', '-')}-${reason}-${crypto.randomUUID().slice(0, 8)}.sqlite`);
    // SQLite takes a consistent snapshot, including committed data in the WAL.
    this.db.prepare('VACUUM INTO ?').run(destination);
    return destination;
  }
  dailyBackup() {
    const day = new Date().toISOString().slice(0, 10);
    if (!readdirSync(this.backups).some(f => f.startsWith(day) && f.includes('-diario-'))) this.backup('diario');
    const daily = readdirSync(this.backups).filter(f => f.includes('-diario-') && f.endsWith('.sqlite')).sort().reverse();
    daily.slice(30).forEach(f => unlinkSync(path.join(this.backups, f)));
  }
  close() { this.db.close(); }
}
