// src/domain.ts
var METHODS = ["dinheiro", "pix", "debito", "credito"];
var METHOD_LABEL = { dinheiro: "Dinheiro", pix: "Pix", debito: "D\xE9bito", credito: "Cr\xE9dito" };
var id = () => crypto.randomUUID();
var now = () => (/* @__PURE__ */ new Date()).toISOString();
var integer = (n, label, minimum = 0) => {
  if (!Number.isSafeInteger(n) || n < minimum || n > 1e9) throw new Error(`${label}: informe um valor v\xE1lido.`);
};
var requireText = (s, label) => {
  if (!s.trim()) throw new Error(`Preencha ${label}.`);
};
function money(n) {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(n / 100);
}
function qty(n, unit) {
  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: unit === "kg" ? 3 : 0 }).format(n / 1e3);
}
function parseDecimal(raw, scale) {
  const text = raw.trim().replace(",", ".");
  if (!/^\d+(\.\d+)?$/.test(text)) throw new Error("Informe um n\xFAmero positivo v\xE1lido.");
  const n = Math.round(Number(text) * scale);
  integer(n, "Valor");
  return n;
}
function localDay(iso) {
  const d = new Date(iso);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
function activeSession(s) {
  return s.sessions.find((x) => x.status === "open");
}
function next(s, fn) {
  const d = structuredClone(s);
  fn(d);
  d.revision++;
  return d;
}
function createSeed() {
  const rows = [
    ["P\xE3o franc\xEAs", "001", "P\xE3es", "kg", 1890, 820, 18e3, 3e3, "bread"],
    ["P\xE3o de queijo", "002", "Salgados", "un", 350, 130, 48e3, 1e4, "bread"],
    ["Croissant tradicional", "003", "P\xE3es", "un", 790, 310, 24e3, 5e3, "croissant"],
    ["P\xE3o na chapa", "004", "Lanches", "un", 550, 190, 4e4, 8e3, "sandwich"],
    ["Caf\xE9 espresso", "005", "Bebidas", "un", 450, 100, 8e4, 1e4, "coffee"],
    ["Caf\xE9 com leite", "006", "Bebidas", "un", 650, 210, 6e4, 1e4, "coffee"],
    ["Coxinha de frango", "007", "Salgados", "un", 690, 270, 36e3, 8e3, "croissant"],
    ["Bolo de cenoura", "008", "Doces", "un", 750, 280, 16e3, 5e3, "cake"],
    ["Sonho de creme", "009", "Doces", "un", 590, 220, 18e3, 5e3, "cake"],
    ["Suco de laranja", "010", "Bebidas", "un", 850, 340, 25e3, 6e3, "juice"],
    ["Misto quente", "011", "Lanches", "un", 1090, 430, 3e4, 6e3, "sandwich"],
    ["\xC1gua mineral 500 ml", "012", "Bebidas", "un", 350, 120, 48e3, 12e3, "juice"],
    ["Baguete artesanal", "013", "P\xE3es", "un", 990, 380, 12e3, 4e3, "bread"],
    ["Cookie de chocolate", "014", "Doces", "un", 650, 230, 22e3, 6e3, "cake"],
    ["Empada de frango", "015", "Salgados", "un", 650, 240, 6e3, 8e3, "croissant"],
    ["P\xE3o integral", "016", "P\xE3es", "kg", 2490, 1050, 6500, 2e3, "bread"]
  ];
  return { schemaVersion: 1, revision: 0, nextSale: 1, settings: { name: "P\xE3o & Caixa", operator: "Operador", footer: "Obrigado pela prefer\xEAncia. Volte sempre!" }, products: rows.map(([name, code, category, unit, price, cost, stock, minStock, icon]) => ({ id: id(), name, code, category, unit, price, cost, stock, minStock, icon, active: true })), sales: [], sessions: [], cashMoves: [], stockMoves: [] };
}
function cartTotals(s, lines, discount = 0) {
  integer(discount, "Desconto");
  const seen = /* @__PURE__ */ new Set();
  const items = lines.map((line) => {
    if (seen.has(line.productId)) throw new Error("Produto duplicado na venda.");
    seen.add(line.productId);
    const p = s.products.find((x) => x.id === line.productId && x.active);
    if (!p) throw new Error("Produto indispon\xEDvel.");
    integer(line.quantity, "Quantidade", 1);
    if (p.unit === "un" && line.quantity % 1e3 !== 0) throw new Error("Produtos por unidade exigem quantidade inteira.");
    if (line.quantity > p.stock) throw new Error(`Estoque insuficiente de ${p.name}.`);
    return { productId: p.id, name: p.name, code: p.code, unit: p.unit, quantity: line.quantity, price: p.price, cost: p.cost, total: Math.round(p.price * line.quantity / 1e3) };
  });
  const subtotal = items.reduce((a, x) => a + x.total, 0);
  integer(subtotal, "Total");
  if (discount > subtotal) throw new Error("O desconto n\xE3o pode superar o subtotal.");
  return { items, subtotal, discount, total: subtotal - discount };
}
function openRegister(s, opening, operator) {
  integer(opening, "Fundo de troco");
  requireText(operator, "o operador");
  if (activeSession(s)) throw new Error("J\xE1 existe um caixa aberto.");
  return next(s, (d) => {
    d.sessions.unshift({ id: id(), openedAt: now(), operator: operator.trim(), opening, status: "open" });
  });
}
function finalizeSale(s, lines, discount, payments, cashReceived, note = "") {
  const session = activeSession(s);
  if (!session) throw new Error("Abra o caixa antes de vender.");
  if (!lines.length) throw new Error("Adicione um produto \xE0 venda.");
  const t = cartTotals(s, lines, discount);
  if (t.total <= 0) throw new Error("A venda deve ter um total maior que zero.");
  const methods = /* @__PURE__ */ new Set();
  for (const p of payments) {
    if (!METHODS.includes(p.method) || methods.has(p.method)) throw new Error("Forma de pagamento inv\xE1lida ou repetida.");
    methods.add(p.method);
    integer(p.amount, "Pagamento", 1);
  }
  if (payments.reduce((a, x) => a + x.amount, 0) !== t.total) throw new Error("A soma dos pagamentos deve ser igual ao total.");
  integer(cashReceived, "Dinheiro recebido");
  const cash = payments.find((x) => x.method === "dinheiro")?.amount ?? 0;
  if (cashReceived < cash) throw new Error("O dinheiro recebido \xE9 insuficiente.");
  if (!cash && cashReceived) throw new Error("Dinheiro recebido requer pagamento em dinheiro.");
  const saleId = id();
  const at = now();
  return next(s, (d) => {
    d.sales.unshift({ id: saleId, number: d.nextSale++, sessionId: session.id, at, operator: session.operator, ...t, payments: structuredClone(payments), cashReceived, change: cashReceived - cash, note: note.trim(), status: "paid" });
    for (const item of t.items) {
      d.products.find((x) => x.id === item.productId).stock -= item.quantity;
      d.stockMoves.unshift({ id: id(), productId: item.productId, at, quantity: -item.quantity, reason: `Venda #${d.nextSale - 1}`, saleId });
    }
  });
}
function sessionTotals(s, sessionId) {
  const session = s.sessions.find((x) => x.id === sessionId);
  if (!session) throw new Error("Caixa n\xE3o encontrado.");
  const sales = s.sales.filter((x) => x.sessionId === sessionId && x.status === "paid");
  const payments = { dinheiro: 0, pix: 0, debito: 0, credito: 0 };
  for (const sale of sales) for (const p of sale.payments) payments[p.method] += p.amount;
  const moves = s.cashMoves.filter((x) => x.sessionId === sessionId);
  const supplied = moves.filter((x) => x.type === "supply").reduce((a, x) => a + x.amount, 0);
  const withdrawn = moves.filter((x) => x.type === "withdrawal").reduce((a, x) => a + x.amount, 0);
  return { sales, payments, supplied, withdrawn, drawer: session.opening + payments.dinheiro + supplied - withdrawn, revenue: sales.reduce((a, x) => a + x.total, 0) };
}
function moveCash(s, type, amount, reason) {
  const session = activeSession(s);
  if (!session) throw new Error("O caixa est\xE1 fechado.");
  if (type !== "supply" && type !== "withdrawal") throw new Error("Movimenta\xE7\xE3o inv\xE1lida.");
  integer(amount, "Valor", 1);
  requireText(reason, "o motivo");
  if (type === "withdrawal" && amount > sessionTotals(s, session.id).drawer) throw new Error("A sangria supera o dinheiro dispon\xEDvel no caixa.");
  return next(s, (d) => {
    d.cashMoves.unshift({ id: id(), sessionId: session.id, at: now(), type, amount, reason: reason.trim() });
  });
}
function closeRegister(s, counted) {
  const session = activeSession(s);
  if (!session) throw new Error("Nenhum caixa aberto.");
  for (const m of METHODS) integer(counted[m], METHOD_LABEL[m]);
  const totals = sessionTotals(s, session.id);
  const expected = { ...totals.payments, dinheiro: totals.drawer };
  return next(s, (d) => {
    const target = d.sessions.find((x) => x.id === session.id);
    Object.assign(target, { status: "closed", closedAt: now(), counted, expected, difference: counted.dinheiro - expected.dinheiro });
  });
}
function cancelSale(s, saleId, reason) {
  requireText(reason, "o motivo do cancelamento");
  const sale = s.sales.find((x) => x.id === saleId);
  if (!sale || sale.status !== "paid") throw new Error("Venda inexistente ou j\xE1 cancelada.");
  if (activeSession(s)?.id !== sale.sessionId) throw new Error("S\xF3 \xE9 poss\xEDvel cancelar vendas do caixa atualmente aberto.");
  const cash = sale.payments.find((x) => x.method === "dinheiro")?.amount ?? 0;
  if (sessionTotals(s, sale.sessionId).drawer < cash) throw new Error("Dinheiro insuficiente no caixa para devolver esta venda. Registre um suprimento.");
  return next(s, (d) => {
    const target = d.sales.find((x) => x.id === saleId);
    target.status = "cancelled";
    target.cancelledAt = now();
    target.cancelReason = reason.trim();
    for (const item of sale.items) {
      const product = d.products.find((x) => x.id === item.productId);
      if (!product) throw new Error("Produto da venda n\xE3o encontrado.");
      product.stock += item.quantity;
      d.stockMoves.unshift({ id: id(), productId: item.productId, at: now(), quantity: item.quantity, reason: `Cancelamento #${sale.number}`, saleId });
    }
  });
}
function saveProduct(s, product) {
  requireText(product.name, "o nome");
  requireText(product.code, "o c\xF3digo");
  requireText(product.category, "a categoria");
  if (!["un", "kg"].includes(product.unit)) throw new Error("Unidade inv\xE1lida.");
  integer(product.price, "Pre\xE7o", 1);
  integer(product.cost, "Custo");
  integer(product.minStock, "Estoque m\xEDnimo");
  if (s.products.some((x) => x.code.toLocaleLowerCase() === product.code.trim().toLocaleLowerCase() && x.id !== product.id)) throw new Error("J\xE1 existe um produto com esse c\xF3digo.");
  const existing = s.products.find((x) => x.id === product.id);
  if (existing && existing.unit !== product.unit) throw new Error("A unidade de um produto cadastrado n\xE3o pode ser alterada. Crie outro produto.");
  const stock = existing?.stock ?? product.stock ?? 0;
  integer(stock, "Estoque");
  if (product.unit === "un" && (stock % 1e3 || product.minStock % 1e3)) throw new Error("Estoque por unidade deve ser inteiro.");
  return next(s, (d) => {
    const clean = { ...product, id: existing?.id ?? id(), stock, name: product.name.trim(), code: product.code.trim(), category: product.category.trim() };
    if (existing) d.products[d.products.findIndex((x) => x.id === existing.id)] = clean;
    else {
      d.products.push(clean);
      if (stock) d.stockMoves.unshift({ id: id(), productId: clean.id, at: now(), quantity: stock, reason: "Estoque inicial" });
    }
  });
}
function adjustStock(s, productId, quantity, reason) {
  const p = s.products.find((x) => x.id === productId);
  if (!p) throw new Error("Produto n\xE3o encontrado.");
  if (!Number.isSafeInteger(quantity) || !quantity || Math.abs(quantity) > 1e9) throw new Error("Informe uma quantidade v\xE1lida.");
  if (p.unit === "un" && quantity % 1e3) throw new Error("Quantidade por unidade deve ser inteira.");
  if (p.stock + quantity < 0) throw new Error("A sa\xEDda supera o estoque dispon\xEDvel.");
  requireText(reason, "o motivo");
  return next(s, (d) => {
    d.products.find((x) => x.id === productId).stock += quantity;
    d.stockMoves.unshift({ id: id(), productId, at: now(), quantity, reason: reason.trim() });
  });
}
function saveSettings(s, settings) {
  requireText(settings.name, "o nome da padaria");
  requireText(settings.operator, "o operador");
  return next(s, (d) => {
    d.settings = { ...settings, name: settings.name.trim(), operator: settings.operator.trim() };
  });
}
function validateState(value) {
  if (!value || typeof value !== "object") throw new Error("Backup inv\xE1lido.");
  const s = value;
  if (s.schemaVersion !== 1 || !Array.isArray(s.products) || !Array.isArray(s.sales) || !Array.isArray(s.sessions) || !Array.isArray(s.cashMoves) || !Array.isArray(s.stockMoves)) throw new Error("Formato do backup n\xE3o reconhecido.");
  integer(s.revision, "Vers\xE3o");
  integer(s.nextSale, "Pr\xF3xima venda", 1);
  if (!s.settings || typeof s.settings.name !== "string" || typeof s.settings.operator !== "string" || typeof s.settings.footer !== "string") throw new Error("Configura\xE7\xF5es inv\xE1lidas.");
  const ids = /* @__PURE__ */ new Set();
  const codes = /* @__PURE__ */ new Set();
  for (const p of s.products) {
    if (typeof p.id !== "string" || !p.id || ids.has(p.id) || typeof p.code !== "string" || codes.has(p.code.toLowerCase()) || typeof p.name !== "string" || typeof p.category !== "string" || typeof p.icon !== "string" || typeof p.active !== "boolean" || !["un", "kg"].includes(p.unit)) throw new Error("Produtos inv\xE1lidos.");
    ids.add(p.id);
    codes.add(p.code.toLowerCase());
    for (const n of [p.price, p.cost, p.stock, p.minStock]) integer(n, "Produto");
    if (p.price === 0 || p.unit === "un" && (p.stock % 1e3 || p.minStock % 1e3)) throw new Error("Valores de produto inv\xE1lidos.");
  }
  const sessionIds = /* @__PURE__ */ new Set();
  let open = 0;
  const date = (x) => {
    if (typeof x !== "string" || !Number.isFinite(Date.parse(x))) throw new Error("Data inv\xE1lida no backup.");
  };
  for (const x of s.sessions) {
    if (!x.id || sessionIds.has(x.id) || typeof x.operator !== "string" || !["open", "closed"].includes(x.status)) throw new Error("Caixas inv\xE1lidos.");
    sessionIds.add(x.id);
    integer(x.opening, "Fundo");
    date(x.openedAt);
    if (x.status === "open") open++;
    else {
      date(x.closedAt);
      if (!x.counted || !x.expected) throw new Error("Fechamento inv\xE1lido.");
      for (const m of METHODS) {
        integer(x.counted[m], "Contagem");
        integer(x.expected[m], "Esperado");
      }
      if (x.difference !== x.counted.dinheiro - x.expected.dinheiro) throw new Error("Diferen\xE7a inv\xE1lida.");
    }
  }
  if (open > 1) throw new Error("H\xE1 mais de um caixa aberto.");
  const saleIds = /* @__PURE__ */ new Set();
  const numbers = /* @__PURE__ */ new Set();
  for (const x of s.sales) {
    if (!x.id || saleIds.has(x.id) || !sessionIds.has(x.sessionId) || !["paid", "cancelled"].includes(x.status) || !Array.isArray(x.items) || !x.items.length || !Array.isArray(x.payments) || typeof x.operator !== "string" || typeof x.note !== "string") throw new Error("Vendas inv\xE1lidas.");
    saleIds.add(x.id);
    integer(x.number, "Venda", 1);
    if (numbers.has(x.number) || x.number >= s.nextSale) throw new Error("Numera\xE7\xE3o inv\xE1lida.");
    numbers.add(x.number);
    date(x.at);
    for (const n of [x.subtotal, x.discount, x.total, x.cashReceived, x.change]) integer(n, "Venda");
    for (const i of x.items) {
      if (!ids.has(i.productId) || typeof i.name !== "string" || typeof i.code !== "string" || !["un", "kg"].includes(i.unit)) throw new Error("Item inv\xE1lido.");
      integer(i.quantity, "Quantidade", 1);
      integer(i.price, "Pre\xE7o", 1);
      integer(i.cost, "Custo");
      integer(i.total, "Total");
      if (i.unit === "un" && i.quantity % 1e3 || i.total !== Math.round(i.price * i.quantity / 1e3)) throw new Error("Total do item inv\xE1lido.");
    }
    const pm = /* @__PURE__ */ new Set();
    for (const p of x.payments) {
      if (!METHODS.includes(p.method) || pm.has(p.method)) throw new Error("Pagamento inv\xE1lido.");
      pm.add(p.method);
      integer(p.amount, "Pagamento", 1);
    }
    const cash = x.payments.find((p) => p.method === "dinheiro")?.amount ?? 0;
    if (x.subtotal !== x.items.reduce((a, i) => a + i.total, 0) || x.total !== x.subtotal - x.discount || x.total <= 0 || x.total !== x.payments.reduce((a, p) => a + p.amount, 0) || x.cashReceived < cash || !cash && x.cashReceived !== 0 || x.change !== x.cashReceived - cash) throw new Error("Totais inconsistentes no backup.");
    if (x.status === "cancelled") {
      date(x.cancelledAt);
      if (typeof x.cancelReason !== "string" || !x.cancelReason.trim()) throw new Error("Cancelamento inv\xE1lido.");
    }
  }
  for (const m of s.cashMoves) {
    if (!m.id || !sessionIds.has(m.sessionId) || !["supply", "withdrawal"].includes(m.type) || typeof m.reason !== "string") throw new Error("Movimenta\xE7\xE3o inv\xE1lida.");
    integer(m.amount, "Movimenta\xE7\xE3o", 1);
    date(m.at);
  }
  for (const m of s.stockMoves) {
    if (!m.id || !ids.has(m.productId) || !Number.isSafeInteger(m.quantity) || typeof m.reason !== "string" || m.saleId && !saleIds.has(m.saleId)) throw new Error("Estoque inv\xE1lido.");
    date(m.at);
  }
  for (const x of s.sessions) {
    const t = sessionTotals(s, x.id);
    if (t.drawer < 0) throw new Error("Saldo de caixa negativo no backup.");
    if (x.status === "closed" && METHODS.some((m) => x.expected[m] !== (m === "dinheiro" ? t.drawer : t.payments[m]))) throw new Error("Fechamento inconsistente no backup.");
  }
  return s;
}
export {
  METHODS,
  METHOD_LABEL,
  activeSession,
  adjustStock,
  cancelSale,
  cartTotals,
  closeRegister,
  createSeed,
  finalizeSale,
  localDay,
  money,
  moveCash,
  openRegister,
  parseDecimal,
  qty,
  saveProduct,
  saveSettings,
  sessionTotals,
  validateState
};
