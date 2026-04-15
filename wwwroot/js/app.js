(() => {
  const state = { items: [], boms: [], stock: [], balances: [], undo: null, stockOpSortAsc: true, orders: [] };

  const ITEM_TYPE_RU = {
    Product: "Готовая продукция",
    Assembly: "Сборочная единица",
    Component: "Компонент",
    Material: "Материал",
  };

  const OP_TYPE_RU = {
    Receipt: "Приход",
    Issue: "Расход",
  };

  const pick = (obj, keys, fallback = null) => {
    for (const k of keys) {
      if (obj && obj[k] !== undefined && obj[k] !== null) return obj[k];
    }
    return fallback;
  };

  const normalizeItem = (x) => ({
    id: Number(pick(x, ["itemId", "itemID", "ItemID"], 0)),
    code: String(pick(x, ["itemCode", "ItemCode"], "") ?? ""),
    name: String(pick(x, ["itemName", "ItemName"], "") ?? ""),
    type: String(pick(x, ["itemType", "ItemType"], "") ?? ""),
    unit: pick(x, ["unit", "Unit"], ""),
    unitCost: pick(x, ["unitCost", "UnitCost"], null),
    sellingPrice: pick(x, ["sellingPrice", "SellingPrice"], null),
  });

  const normalizeBom = (x) => ({
    id: NonZeroId(
      pick(x, ["bomId", "bomID", "BOMID", "BomId"], null),
      "строка спецификации",
    ),
    parentItemId: Number(pick(x, ["parentItemId", "parentItemID", "ParentItemID"], 0)),
    childItemId: Number(pick(x, ["childItemId", "childItemID", "ChildItemID"], 0)),
    quantity: Number(pick(x, ["quantity", "Quantity"], 0)),
  });

  const normalizeStock = (x) => ({
    id: NonZeroId(
      pick(x, ["stockOperationId", "stockOperationID", "StockOperationID"], null),
      "операция",
    ),
    specificationId: Number(pick(x, ["specificationId", "SpecificationId"], 0)),
    date: pick(x, ["date", "Date"], null),
    quantity: Number(pick(x, ["quantity", "Quantity"], 0)),
    operationType: String(pick(x, ["operationType", "OperationType"], "") ?? ""),
  });

  const normalizeBalance = (x) => ({
    itemId: Number(pick(x, ["itemId", "itemID", "ItemID"], 0)),
    itemCode: String(pick(x, ["itemCode", "ItemCode"], "") ?? ""),
    itemName: String(pick(x, ["itemName", "ItemName"], "") ?? ""),
    unit: pick(x, ["unit", "Unit"], ""),
    receiptQty: Number(pick(x, ["receiptQty", "ReceiptQty"], 0)),
    issueQty: Number(pick(x, ["issueQty", "IssueQty"], 0)),
    currentStock: Number(pick(x, ["currentStock", "CurrentStock"], 0)),
  });

  const isIntegerLike = (v) => Number.isInteger(Number(v));

  function NonZeroId(raw, what) {
    const n = Number(raw);
    if (!Number.isFinite(n) || n <= 0) {
      console.warn("Пропущен или неверный ID для:", what, raw);
    }
    return Number.isFinite(n) ? n : 0;
  }

  function itemLabel(id) {
    const it = state.items.find((x) => x.id === id);
    if (!it) return `ID ${id}`;
    return `${it.id} - ${it.name}${it.code ? ` (${it.code})` : ""}`;
  }

  function itemNameById(id) {
    return state.items.find((x) => x.id === id)?.name || `ID ${id}`;
  }

  function typeRu(en) {
    return ITEM_TYPE_RU[en] || en || "-";
  }

  function opRu(en) {
    return OP_TYPE_RU[en] || en || "-";
  }

  function itemTypeOptionsHtml(selected) {
    const keys = ["Product", "Assembly", "Component", "Material"];
    return keys
      .map(
        (k) =>
          `<option value="${k}" ${String(selected) === k ? "selected" : ""}>${ITEM_TYPE_RU[k]}</option>`,
      )
      .join("");
  }

  function opTypeOptionsHtml(selected) {
    const keys = ["Receipt", "Issue"];
    return keys
      .map(
        (k) =>
          `<option value="${k}" ${String(selected) === k ? "selected" : ""}>${OP_TYPE_RU[k]}</option>`,
      )
      .join("");
  }

  async function api(url, options = {}) {
    const headers = { Accept: "application/json", ...(options.headers || {}) };
    if (options.body && !headers["Content-Type"]) headers["Content-Type"] = "application/json";

    const res = await fetch(url, { ...options, headers });
    if (res.status === 204) return null;

    const text = await res.text();
    const ct = res.headers.get("content-type") || "";
    const isJson = ct.includes("application/json");
    let body = null;
    if (text) {
      if (isJson) {
        try {
          body = JSON.parse(text);
        } catch {
          throw new Error(
            `Сервер вернул не JSON (возможно, HTML страницы). Проверь URL API и что приложение запущено. (${res.status})`,
          );
        }
      } else body = text;
    }

    if (!res.ok) {
      const msg =
        typeof body === "string" ? body : body?.detail || body?.title || body?.message || res.statusText;
      throw new Error(msg || `Ошибка запроса (${res.status})`);
    }
    return body;
  }

  function toast(message, ok = false) {
    const host = document.getElementById("toastHost");
    const el = document.createElement("div");
    el.className = `toast ${ok ? "toast--ok" : "toast--err"}`;
    el.textContent = message;
    host.appendChild(el);
    setTimeout(() => el.remove(), 4500);
  }

  function esc(s) {
    return String(s ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;");
  }

  function toMoney(v) {
    if (v == null || Number.isNaN(Number(v))) return "-";
    return `${Number(v).toLocaleString("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₽`;
  }

  function toQty(v) {
    if (v == null || Number.isNaN(Number(v))) return "-";
    return Number(v).toLocaleString("ru-RU", { maximumFractionDigits: 4 });
  }

  function bomLineLabel(bomId) {
    const b = state.boms.find((x) => x.id === bomId);
    if (!b) return "-";
    return itemNameById(b.childItemId);
  }

  function bomOptionLabel(bomId) {
    const b = state.boms.find((x) => x.id === bomId);
    if (!b) return `BOM ${bomId}`;
    return `BOM ${b.id}: ${itemNameById(b.parentItemId)} / ${itemNameById(b.childItemId)}`;
  }

  function applyIntegerValidation(host = document) {
    host.querySelectorAll('input[type="number"][step="1"]').forEach((input) => {
      input.addEventListener("input", () => input.setCustomValidity(""));
      input.addEventListener("invalid", () => input.setCustomValidity("Введите целое число"));
    });
  }

  function setUndoAction(label, run) {
    state.undo = { label, run };
    const btn = document.getElementById("btnUndo");
    if (!btn) return;
    btn.disabled = false;
    btn.title = label;
  }

  function clearUndoAction() {
    state.undo = null;
    const btn = document.getElementById("btnUndo");
    if (!btn) return;
    btn.disabled = true;
    btn.title = "";
  }

  function loadOrders() {
    try {
      const raw = localStorage.getItem("mrpOrdersV1");
      const parsed = raw ? JSON.parse(raw) : [];
      state.orders = Array.isArray(parsed)
        ? parsed.map((x) => {
            if (Array.isArray(x.items)) return x;
            if (x?.itemId) {
              return {
                ...x,
                items: [{ itemId: Number(x.itemId), quantity: Number(x.quantity) || 1 }],
              };
            }
            return x;
          })
        : [];
      if (!Array.isArray(state.orders)) state.orders = [];
    } catch {
      state.orders = [];
    }
  }

  function saveOrders() {
    localStorage.setItem("mrpOrdersV1", JSON.stringify(state.orders));
  }

  function cloneOrders() {
    return JSON.parse(JSON.stringify(state.orders));
  }

  function setOrdersUndo(label, previousOrders) {
    setUndoAction(label, async () => {
      state.orders = JSON.parse(JSON.stringify(previousOrders));
      saveOrders();
      rebuildDisplayBalances();
      renderBalances();
      renderOrders();
    });
  }

  function stockDateRu(value) {
    if (!value) return "-";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "-";
    return d.toLocaleString("ru-RU", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  function isStockLocked(row) {
    if (!row || row.operationType !== "Receipt") return false;
    const thisBom = state.boms.find((b) => b.id === row.specificationId);
    if (!thisBom) return false;
    const receiptDate = new Date(row.date).getTime();
    if (!Number.isFinite(receiptDate)) return false;

    return state.stock.some((s) => {
      if (s.id === row.id || s.operationType !== "Issue") return false;
      const issueBom = state.boms.find((b) => b.id === s.specificationId);
      if (!issueBom || issueBom.childItemId !== thisBom.childItemId) return false;
      const issueDate = new Date(s.date).getTime();
      return Number.isFinite(issueDate) && issueDate >= receiptDate;
    });
  }

  function setApiError(msg) {
    const el = document.getElementById("apiError");
    if (!el) return;
    if (msg) {
      el.hidden = false;
      el.textContent = msg;
    } else {
      el.hidden = true;
      el.textContent = "";
    }
  }

  async function loadAll() {
    setApiError("");
    const [itemsRaw, bomsRaw, stockRaw, balancesRaw] = await Promise.all([
      api("/api/Items"),
      api("/api/Boms"),
      api("/api/StockOperations").catch(() => []),
      api("/api/Items/stock-balance").catch(() => []),
    ]);

    if (!Array.isArray(itemsRaw)) throw new Error("Ответ /api/Items не массив - проверьте консоль сервера и /swagger/v1/swagger.json");
    if (!Array.isArray(bomsRaw)) throw new Error("Ответ /api/Boms не массив.");
    if (!Array.isArray(stockRaw)) throw new Error("Ответ /api/StockOperations не массив.");
    if (!Array.isArray(balancesRaw)) throw new Error("Ответ /api/Items/stock-balance не массив.");

    state.items = itemsRaw.map(normalizeItem).filter((x) => x.code !== "SYS-GP");
    state.boms = bomsRaw.map(normalizeBom).filter((b) => {
      const parentRaw = itemsRaw.find((i) => Number(pick(i, ["itemId", "itemID", "ItemID"], 0)) === b.parentItemId);
      return String(pick(parentRaw, ["itemCode", "ItemCode"], "")) !== "SYS-GP";
    });
    const visibleBomIds = new Set(state.boms.map((b) => b.id));
    state.stock = stockRaw
      .map(normalizeStock)
      .filter((s) => visibleBomIds.has(s.specificationId))
      .sort((a, b) => {
      const ap = a.operationType === "Receipt" ? 0 : 1;
      const bp = b.operationType === "Receipt" ? 0 : 1;
      if (ap !== bp) return ap - bp;
      return new Date(b.date).getTime() - new Date(a.date).getTime();
    });
    state.balances = balancesRaw.map(normalizeBalance).filter((x) => x.itemCode !== "SYS-GP");
    loadOrders();
    rebuildDisplayBalances();

    renderItems();
    renderBoms();
    renderStock();
    renderBalances();
    renderOrders();
    populateProductSelect();
    if (document.getElementById("view-production")?.classList.contains("view--active")) {
      fetchCapacity();
      fetchProductionStats();
    }
  }

  function populateProductSelect() {
    const sel = document.getElementById("selectProduct");
    if (!sel) return;
    const products = state.items.filter((x) => x.type === "Product");
    const prev = sel.value;
    if (!products.length) {
      sel.innerHTML = '<option value="">- нет готовой продукции -</option>';
      return;
    }
    sel.innerHTML = products.map((p) => `<option value="${p.id}">${esc(itemLabel(p.id))}</option>`).join("");
    const still = products.some((p) => String(p.id) === prev);
    const bike = state.items.find((x) => x.code === "BIKE" && x.type === "Product");
    if (still) sel.value = prev;
    else if (bike && products.some((p) => p.id === bike.id)) sel.value = String(bike.id);
    else sel.value = String(products[0].id);
  }

  function rebuildDisplayBalances() {
    const baseByItem = new Map(state.balances.map((x) => [x.itemId, x]));
    const childrenByParent = new Map();
    for (const b of state.boms) {
      if (!childrenByParent.has(b.parentItemId)) childrenByParent.set(b.parentItemId, []);
      childrenByParent.get(b.parentItemId).push(b);
    }

    const memo = new Map();
    const stack = new Set();

    function available(itemId) {
      if (memo.has(itemId)) return memo.get(itemId);
      if (stack.has(itemId)) return 0;
      stack.add(itemId);
      const rawRow = baseByItem.get(itemId);
      const direct = Number(rawRow?.receiptQty ?? 0) - Number(rawRow?.issueQty ?? 0);
      const children = childrenByParent.get(itemId) || [];
      if (!children.length) {
        memo.set(itemId, direct);
        stack.delete(itemId);
        return direct;
      }
      let derived = Number.POSITIVE_INFINITY;
      for (const line of children) {
        const childAvail = Math.max(0, Number(available(line.childItemId)));
        const fromLine = line.quantity > 0 ? Math.floor(childAvail / Number(line.quantity)) : 0;
        derived = Math.min(derived, fromLine);
      }
      const calculated = Number.isFinite(derived) ? derived : 0;
      const total = Math.max(direct, calculated);
      memo.set(itemId, total);
      stack.delete(itemId);
      return total;
    }

    const closedByItem = new Map();
    for (const order of state.orders) {
      if (order?.orderType !== "closed" || !Array.isArray(order.items)) continue;
      for (const line of order.items) {
        const itemId = Number(line.itemId);
        const qty = Number(line.quantity) || 0;
        closedByItem.set(itemId, (closedByItem.get(itemId) || 0) + qty);
      }
    }

    state.balances = state.items.map((it) => {
      const raw = baseByItem.get(it.id);
      const closedQty = closedByItem.get(it.id) || 0;
      const currentStock = Math.max(0, Number(available(it.id)) - closedQty);
      return {
        itemId: it.id,
        itemCode: it.code || "",
        itemName: it.name,
        unit: it.unit || "",
        receiptQty: Number(raw?.receiptQty ?? 0),
        issueQty: Number(raw?.issueQty ?? 0),
        currentStock,
      };
    });
  }

  function availableQtyByItemId(itemId) {
    return Number(state.balances.find((x) => x.itemId === Number(itemId))?.currentStock ?? 0);
  }

  function renderCapacityHtml(c) {
    const maxQty = Number(pick(c, ["maxQty", "MaxQty"], 0));
    const limId = pick(c, ["limitingItemId", "LimitingItemId"], null);
    const lines = pick(c, ["lines", "Lines"], []) || [];
    const limNum = limId != null ? Number(limId) : null;
    const limName =
      limNum != null && Number.isFinite(limNum)
        ? state.items.find((x) => x.id === limNum)?.name || `ID ${limNum}`
        : null;
    const limText = limName ? ` Ограничивает: ${esc(limName)}.` : "";
    const rows = lines.length
      ? lines
          .map((l) => {
            const name = pick(l, ["childName", "ChildName"], "-");
            const per = pick(l, ["qtyPerUnit", "QtyPerUnit"], 0);
            const stock = pick(l, ["currentStock", "CurrentStock"], 0);
            const m = pick(l, ["maxFromThisLine", "MaxFromThisLine"], 0);
            return `<tr><td>${esc(name)}</td><td>${toQty(per)}</td><td>${toQty(stock)}</td><td>${m}</td></tr>`;
          })
          .join("")
      : '<tr><td colspan="4"><div class="empty">Нет строк BOM</div></td></tr>';
    return `
      <p class="capacity-max"><strong>Можно произвести:</strong> ${maxQty} шт.${limText}</p>
      <div class="table-wrap">
        <table class="table table--compact">
          <thead>
            <tr><th>Компонент</th><th>На 1 шт.</th><th>Остаток</th><th>Макс. по строке</th></tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>`;
  }

  async function fetchCapacity() {
    const sel = document.getElementById("selectProduct");
    const box = document.getElementById("capacityInfo");
    if (!sel || !box) return;
    const id = Number(sel.value);
    if (!id) {
      box.innerHTML = '<p class="empty">Добавьте готовую продукцию или нажмите «Приход материалов…».</p>';
      return;
    }
    try {
      const c = await api(`/api/Production/capacity?productItemId=${id}`);
      box.innerHTML = renderCapacityHtml(c);
    } catch (err) {
      box.innerHTML = `<p class="empty capacity-err">${esc(err.message || String(err))}</p>`;
    }
  }

  function renderProductionStatsHtml(s) {
    const pq = Number(pick(s, ["producedQty", "ProducedQty"], 0));
    const cpb = pick(s, ["costPerBike", "CostPerBike"], 0);
    const tmc = pick(s, ["totalMaterialCost", "TotalMaterialCost"], 0);
    const tr = pick(s, ["totalRevenue", "TotalRevenue"], null);
    const tp = pick(s, ["totalProfit", "TotalProfit"], null);
    return `<div class="production-stats">
      <p><strong>Выпущено, шт.:</strong> ${toQty(pq)}</p>
      <p><strong>Себестоимость материалов на 1 шт.:</strong> ${toMoney(cpb)}</p>
      <p><strong>Себестоимость выпуска (оценка):</strong> ${toMoney(tmc)}</p>
      <p><strong>Выручка:</strong> ${tr != null && tr !== "" ? toMoney(tr) : "-"}</p>
      <p><strong>Прибыль:</strong> ${tp != null && tp !== "" ? toMoney(tp) : "-"}</p>
    </div>`;
  }

  async function fetchProductionStats() {
    const sel = document.getElementById("selectProduct");
    const box = document.getElementById("productionStats");
    if (!sel || !box) return;
    const id = Number(sel.value);
    if (!id) {
      box.innerHTML = "";
      return;
    }
    try {
      const s = await api(`/api/Production/stats?productItemId=${id}`);
      box.innerHTML = renderProductionStatsHtml(s);
    } catch (err) {
      box.innerHTML = `<p class="empty capacity-err">${esc(err.message || String(err))}</p>`;
    }
  }

  function renderItems() {
    const tb = document.querySelector("#tableItems tbody");
    tb.innerHTML = "";
    if (!state.items.length) {
      tb.innerHTML =
        '<tr><td colspan="7"><div class="empty">Нет позиций - добавьте первую или проверьте SQL Server.</div></td></tr>';
      return;
    }

    for (const [idx, x] of state.items.entries()) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${idx + 1}</td>
        <td class="mono">${esc(x.code || "-")}</td>
        <td>${esc(x.name)}</td>
        <td>${esc(typeRu(x.type))}</td>
        <td>${esc(x.unit || "-")}</td>
        <td>${toMoney(x.unitCost)}</td>
        <td>
          <button class="btn btn--small" data-edit-item="${x.id}" type="button">Изменить</button>
          <button class="btn btn--small btn--danger" data-del-item="${x.id}" type="button">Удалить</button>
        </td>`;
      tb.appendChild(tr);
    }

    tb.querySelectorAll("[data-edit-item]").forEach((b) =>
      b.addEventListener("click", () => openItemModal(Number(b.dataset.editItem))),
    );
    tb.querySelectorAll("[data-del-item]").forEach((b) =>
      b.addEventListener("click", () => deleteItem(Number(b.dataset.delItem))),
    );
  }

  function renderBoms() {
    const tb = document.querySelector("#tableBoms tbody");
    tb.innerHTML = "";
    if (!state.boms.length) {
      tb.innerHTML = '<tr><td colspan="5"><div class="empty">Сначала заведите номенклатуру, затем добавьте строки BOM.</div></td></tr>';
      return;
    }

    for (const [idx, x] of state.boms.entries()) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${idx + 1}</td>
        <td>${esc(itemNameById(x.parentItemId))}</td>
        <td>${esc(itemNameById(x.childItemId))}</td>
        <td>${x.quantity}</td>
        <td>
          <button class="btn btn--small" data-edit-bom="${x.id}" type="button">Изменить</button>
          <button class="btn btn--small btn--danger" data-del-bom="${x.id}" type="button">Удалить</button>
        </td>`;
      tb.appendChild(tr);
    }

    tb.querySelectorAll("[data-edit-bom]").forEach((b) =>
      b.addEventListener("click", () => openBomModal(Number(b.dataset.editBom))),
    );
    tb.querySelectorAll("[data-del-bom]").forEach((b) =>
      b.addEventListener("click", () => deleteBom(Number(b.dataset.delBom))),
    );
  }

  function renderStock() {
    const tb = document.querySelector("#tableStock tbody");
    tb.innerHTML = "";
    if (!state.stock.length) {
      tb.innerHTML =
        '<tr><td colspan="6"><div class="empty">Нет операций - нужна хотя бы одна строка BOM.</div></td></tr>';
      return;
    }

    const sorted = [...state.stock].sort((a, b) => {
      const aw = a.operationType === "Receipt" ? 0 : 1;
      const bw = b.operationType === "Receipt" ? 0 : 1;
      if (aw !== bw) return state.stockOpSortAsc ? aw - bw : bw - aw;
      return new Date(b.date).getTime() - new Date(a.date).getTime();
    });

    for (const x of sorted) {
      const locked = isStockLocked(x);
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${x.id}</td>
        <td>${esc(bomLineLabel(x.specificationId))}</td>
        <td>${stockDateRu(x.date)}</td>
        <td>${x.quantity}</td>
        <td>${esc(opRu(x.operationType))}</td>
        <td>
          <button class="btn btn--small" data-edit-stock="${x.id}" type="button" ${locked ? "disabled title='Зафиксировано расходом'" : ""}>Изменить</button>
          <button class="btn btn--small btn--danger" data-del-stock="${x.id}" type="button" ${locked ? "disabled title='Зафиксировано расходом'" : ""}>Удалить</button>
        </td>`;
      tb.appendChild(tr);
    }

    tb.querySelectorAll("[data-edit-stock]").forEach((b) =>
      b.addEventListener("click", () => openStockModal(Number(b.dataset.editStock))),
    );
    tb.querySelectorAll("[data-del-stock]").forEach((b) =>
      b.addEventListener("click", () => deleteStock(Number(b.dataset.delStock))),
    );
  }

  function renderBalances() {
    const tb = document.querySelector("#tableBalances tbody");
    if (!tb) return;
    tb.innerHTML = "";
    if (!state.balances.length) {
      tb.innerHTML = '<tr><td colspan="7"><div class="empty">Нет данных об остатках.</div></td></tr>';
      return;
    }

    for (const [idx, x] of state.balances.entries()) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${idx + 1}</td>
        <td class="mono">${esc(x.itemCode || "-")}</td>
        <td>${esc(x.itemName)}</td>
        <td>${esc(x.unit || "-")}</td>
        <td class="num">${toQty(x.receiptQty)}</td>
        <td class="num">${toQty(x.issueQty)}</td>
        <td class="num num--total">${toQty(x.currentStock)}</td>`;
      tb.appendChild(tr);
    }
  }

  function orderTypeRu(v) {
    return v === "closed" ? "Закрытый" : "Открытый";
  }

  function orderItemsNames(order) {
    const items = Array.isArray(order.items) ? order.items : [];
    if (!items.length) return "-";
    return items.map((x) => itemNameById(Number(x.itemId))).join(", ");
  }

  function orderItemsQty(order) {
    const items = Array.isArray(order.items) ? order.items : [];
    if (!items.length) return "-";
    return items.map((x) => String(Number(x.quantity) || 0)).join(", ");
  }

  function renderOrders() {
    const tb = document.querySelector("#tableOrders tbody");
    if (!tb) return;
    tb.innerHTML = "";
    const allowedNodeIds = new Set(
      state.items.filter((x) => x.type !== "Material").map((x) => x.id),
    );
    const rows = state.orders.filter((o) => (o.items || []).every((x) => allowedNodeIds.has(Number(x.itemId))));
    if (!rows.length) {
      tb.innerHTML = '<tr><td colspan="7"><div class="empty">Нет заказов.</div></td></tr>';
    } else {
      rows.forEach((o, idx) => {
        const isClosed = o.orderType === "closed";
        const tr = document.createElement("tr");
        tr.innerHTML = `
          <td>${idx + 1}</td>
          <td>${esc(orderItemsNames(o))}</td>
          <td>${esc(orderItemsQty(o))}</td>
          <td>${esc(stockDateRu(o.orderDate))}</td>
          <td>${esc(stockDateRu(o.dueDate))}</td>
          <td>${esc(orderTypeRu(o.orderType))}</td>
          <td>
            <button class="btn btn--small" data-edit-order="${idx}" type="button" ${isClosed ? "disabled" : ""}>Изменить</button>
            <button class="btn btn--small" data-close-order="${idx}" type="button" ${isClosed ? "disabled" : ""}>Закрыть</button>
            <button class="btn btn--small btn--danger" data-del-order="${idx}" type="button">Удалить</button>
          </td>`;
        tb.appendChild(tr);
      });
      tb.querySelectorAll("[data-edit-order]").forEach((b) =>
        b.addEventListener("click", () => {
          const idx = Number(b.dataset.editOrder);
          if (state.orders[idx]?.orderType === "closed") return toast("Закрытый заказ изменять нельзя");
          openOrderModal(idx);
        }),
      );
      tb.querySelectorAll("[data-close-order]").forEach((b) =>
        b.addEventListener("click", () => {
          const idx = Number(b.dataset.closeOrder);
          if (!state.orders[idx] || state.orders[idx].orderType === "closed") return;
          const order = state.orders[idx];
          for (const line of order.items || []) {
            const avail = availableQtyByItemId(line.itemId);
            if (Number(line.quantity) > avail) {
              return toast(`Недостаточно остатка: в наличии ${avail} ${itemNameById(line.itemId).toLowerCase()}`);
            }
          }
          const prevOrders = cloneOrders();
          state.orders[idx].orderType = "closed";
          saveOrders();
          rebuildDisplayBalances();
          renderBalances();
          renderOrders();
          setOrdersUndo("Отмена закрытия заказа", prevOrders);
          toast("Заказ закрыт", true);
        }),
      );
      tb.querySelectorAll("[data-del-order]").forEach((b) =>
        b.addEventListener("click", () => {
          const idx = Number(b.dataset.delOrder);
          const prevOrders = cloneOrders();
          state.orders.splice(idx, 1);
          saveOrders();
          rebuildDisplayBalances();
          renderBalances();
          renderOrders();
          setOrdersUndo("Отмена удаления заказа", prevOrders);
          toast("Заказ удален", true);
        }),
      );
    }
  }

  const modal = document.getElementById("modal");
  const modalTitle = document.getElementById("modalTitle");
  const modalBody = document.getElementById("modalBody");

  function openModal(title, html) {
    modalTitle.textContent = title;
    modalBody.innerHTML = html;
    modal.hidden = false;
  }

  function closeModal() {
    modal.hidden = true;
    modalBody.innerHTML = "";
  }

  modal.addEventListener("click", (e) => {
    if (e.target.closest("[data-close-modal]")) closeModal();
  });

  function itemSelectOptionsHtml() {
    return state.items
      .map((x) => {
        const label = esc(itemLabel(x.id));
        return `<option value="${x.id}">${label}</option>`;
      })
      .join("");
  }

  function openItemModal(id) {
    const row = id ? state.items.find((x) => x.id === id) : null;
    const edit = !!row;

    openModal(
      edit ? `Номенклатура, ID ${id}` : "Новая позиция",
      `<form id="formItem" class="form-grid">
        <div class="form-row"><label>Код (необязательно, уникальный)</label><input name="itemCode" value="${esc(row?.code || "")}" /></div>
        <div class="form-row"><label>Наименование</label><input name="itemName" required value="${esc(row?.name || "")}" /></div>
        <div class="form-row"><label>Тип</label><select name="itemType">${itemTypeOptionsHtml(row?.type || "Component")}</select></div>
        <div class="form-row"><label>Единица измерения</label><input name="unit" value="${esc(row?.unit || "")}" placeholder="шт., кг…" /></div>
        <div class="form-row">
          <label>Себестоимость за единицу</label>
          <div class="input-with-suffix">
            <input name="unitCost" type="number" step="1" min="0" value="${row?.unitCost ?? ""}" />
            <span class="input-suffix">₽</span>
          </div>
        </div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">${edit ? "Сохранить" : "Создать"}</button>
        </div>
      </form>`,
    );
    applyIntegerValidation(modalBody);

    document.getElementById("formItem").addEventListener("submit", async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const payload = {
        itemId: edit ? id : 0,
        itemCode: fd.get("itemCode") || null,
        itemName: String(fd.get("itemName") || "").trim(),
        itemType: String(fd.get("itemType") || "Component"),
        unit: fd.get("unit") || null,
        unitCost: fd.get("unitCost") === "" ? null : Number(fd.get("unitCost")),
        sellingPrice: null,
      };
      if (payload.unitCost != null && !isIntegerLike(payload.unitCost)) return toast("Себестоимость должна быть целым числом.");
      if (payload.unit && /\d/.test(String(payload.unit))) return toast("Единица измерения должна быть строкой, без цифр.");

      try {
        const prev = row
          ? {
              itemId: row.id,
              itemCode: row.code || null,
              itemName: row.name,
              itemType: row.type,
              unit: row.unit || null,
              unitCost: row.unitCost,
              sellingPrice: row.sellingPrice,
            }
          : null;
        if (edit) {
          await api(`/api/Items/${id}`, { method: "PUT", body: JSON.stringify(payload) });
          setUndoAction("Отмена изменения номенклатуры", () =>
            api(`/api/Items/${id}`, { method: "PUT", body: JSON.stringify(prev) }),
          );
        } else {
          const created = await api("/api/Items", { method: "POST", body: JSON.stringify(payload) });
          const createdId = Number(pick(created, ["itemId", "itemID", "ItemID"], 0));
          if (createdId > 0)
            setUndoAction("Отмена создания номенклатуры", () => api(`/api/Items/${createdId}`, { method: "DELETE" }));
        }
        closeModal();
        toast(edit ? "Сохранено" : "Создано", true);
        await loadAll();
      } catch (err) {
        toast(err.message);
      }
    });
  }

  function openBomModal(id) {
    if (!state.items.length) {
      toast("Сначала добавьте номенклатуру.");
      return;
    }
    const row = id ? state.boms.find((x) => x.id === id) : null;
    const edit = !!row;
    const p = row?.parentItemId ?? state.items[0].id;
    const c = row?.childItemId ?? state.items[0].id;

    openModal(
      edit ? `Строка BOM, ID ${id}` : "Новая строка BOM",
      `<form id="formBom" class="form-grid">
        <div class="form-row"><label>Родитель (сборка / изделие)</label><select name="parentItemId">${itemSelectOptionsHtml()}</select></div>
        <div class="form-row"><label>Компонент (что списывается)</label><select name="childItemId">${itemSelectOptionsHtml()}</select></div>
        <div class="form-row"><label>На 1 ед. родителя, шт.</label><input name="quantity" type="number" step="1" min="1" value="${row?.quantity ?? 1}" required /></div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">${edit ? "Сохранить" : "Создать"}</button>
        </div>
      </form>`,
    );
    applyIntegerValidation(modalBody);

    modalBody.querySelector('[name="parentItemId"]').value = String(p);
    modalBody.querySelector('[name="childItemId"]').value = String(c);

    document.getElementById("formBom").addEventListener("submit", async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const payload = {
        bomId: edit ? id : 0,
        parentItemId: Number(fd.get("parentItemId")),
        childItemId: Number(fd.get("childItemId")),
        quantity: Number(fd.get("quantity")),
      };
      if (!isIntegerLike(payload.quantity)) return toast("Количество в BOM должно быть целым числом.");

      try {
        const prev = row
          ? {
              bomId: row.id,
              parentItemId: row.parentItemId,
              childItemId: row.childItemId,
              quantity: row.quantity,
            }
          : null;
        if (edit) {
          await api(`/api/Boms/${id}`, { method: "PUT", body: JSON.stringify(payload) });
          setUndoAction("Отмена изменения BOM", () => api(`/api/Boms/${id}`, { method: "PUT", body: JSON.stringify(prev) }));
        } else {
          const created = await api("/api/Boms", { method: "POST", body: JSON.stringify(payload) });
          const createdId = Number(pick(created, ["bomId", "bomID", "BOMID", "BomId"], 0));
          if (createdId > 0)
            setUndoAction("Отмена создания BOM", () => api(`/api/Boms/${createdId}`, { method: "DELETE" }));
        }
        closeModal();
        toast(edit ? "Сохранено" : "Создано", true);
        await loadAll();
      } catch (err) {
        toast(err.message);
      }
    });
  }

  function dateToLocalInput(value) {
    if (!value) return "";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "";
    const p = (n) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  function openStockModal(id) {
    if (!state.boms.length) {
      toast("Сначала добавьте строки спецификации.");
      return;
    }
    const row = id ? state.stock.find((x) => x.id === id) : null;
    const edit = !!row;
    const spec = row?.specificationId ?? state.boms[0].id;

    const bomOptions = state.boms
      .map((b) => {
        const t = bomOptionLabel(b.id);
        return `<option value="${b.id}" ${b.id === spec ? "selected" : ""}>${esc(t)}</option>`;
      })
      .join("");

    openModal(
      edit ? `Операция, ID ${id}` : "Новая операция",
      `<form id="formStock" class="form-grid">
        <div class="form-row"><label>BOM</label><select name="specificationId">${bomOptions}</select></div>
        <div class="form-row"><label>Дата и время</label><input name="date" type="datetime-local" value="${dateToLocalInput(row?.date)}" required /></div>
        <div class="form-row"><label>Количество</label><input name="quantity" type="number" step="1" min="1" value="${row?.quantity ?? 1}" required /></div>
        <div class="form-row"><label>Тип операции</label><select name="operationType">${opTypeOptionsHtml(row?.operationType || "Receipt")}</select></div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">${edit ? "Сохранить" : "Создать"}</button>
        </div>
      </form>`,
    );
    applyIntegerValidation(modalBody);

    document.getElementById("formStock").addEventListener("submit", async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const payload = {
        stockOperationId: edit ? id : 0,
        specificationId: Number(fd.get("specificationId")),
        date: new Date(String(fd.get("date"))).toISOString(),
        quantity: Number(fd.get("quantity")),
        operationType: String(fd.get("operationType")),
      };
      if (!isIntegerLike(payload.quantity)) return toast("Количество операции должно быть целым числом.");

      try {
        const prev = row
          ? {
              stockOperationId: row.id,
              specificationId: row.specificationId,
              date: row.date,
              quantity: row.quantity,
              operationType: row.operationType,
            }
          : null;
        if (edit) {
          await api(`/api/StockOperations/${id}`, { method: "PUT", body: JSON.stringify(payload) });
          setUndoAction("Отмена изменения операции", () =>
            api(`/api/StockOperations/${id}`, { method: "PUT", body: JSON.stringify(prev) }),
          );
        } else {
          const created = await api("/api/StockOperations", { method: "POST", body: JSON.stringify(payload) });
          const createdId = Number(pick(created, ["stockOperationId", "stockOperationID", "StockOperationID"], 0));
          if (createdId > 0)
            setUndoAction("Отмена создания операции", () => api(`/api/StockOperations/${createdId}`, { method: "DELETE" }));
        }
        closeModal();
        toast(edit ? "Сохранено" : "Создано", true);
        await loadAll();
      } catch (err) {
        toast(err.message);
      }
    });
  }

  function openOrderModal(editIndex = null) {
    const nodes = state.items.filter((x) => x.type !== "Material");
    if (!nodes.length) {
      toast("Нет узлов для заказа.");
      return;
    }
    const editing = editIndex != null;
    const current = editing ? state.orders[editIndex] : null;
    if (editing && current?.orderType === "closed") {
      toast("Закрытый заказ изменять нельзя");
      return;
    }
    const now = dateToLocalInput(new Date().toISOString());
    const options = nodes.map((n) => `<option value="${n.id}">${esc(n.name)}</option>`).join("");
    openModal(
      editing ? "Изменить заказ" : "Новый заказ",
      `<form id="formOrder" class="form-grid">
        <div id="orderItems"></div>
        <div class="form-row"><button class="btn btn--small" type="button" id="btnAddOrderItem">Добавить изделие</button></div>
        <div class="form-row"><label>Дата оформления</label><input name="orderDate" type="datetime-local" value="${editing ? dateToLocalInput(current?.orderDate) : now}" required /></div>
        <div class="form-row"><label>Дата готовности</label><input name="dueDate" type="datetime-local" value="${editing ? dateToLocalInput(current?.dueDate) : now}" required /></div>
        <div class="form-row"><label>Тип заказа</label>
          <select name="orderType" ${editing && current?.orderType === "closed" ? "disabled" : ""}>
            <option value="open" ${(current?.orderType || "open") === "open" ? "selected" : ""}>Открытый</option>
            <option value="closed" ${current?.orderType === "closed" ? "selected" : ""}>Закрытый</option>
          </select>
        </div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">Сохранить</button>
        </div>
      </form>`,
    );
    applyIntegerValidation(modalBody);
    const itemsHost = document.getElementById("orderItems");
    const initialItems = editing && Array.isArray(current?.items) && current.items.length
      ? current.items
      : [{ itemId: nodes[0].id, quantity: 1 }];
    const drawOrderItems = () => {
           itemsHost.innerHTML = initialItems
        .map(
          (x, idx) => `<div class="order-line" data-order-item="${idx}">
            <select data-item-id="${idx}">${options}</select>
            <input data-item-qty="${idx}" type="number" min="1" step="1" value="${Number(x.quantity) || 1}" />
            <input data-item-available="${idx}" type="text" readonly value="" title="В наличии" />
            <button class="btn btn--small btn--danger" type="button" data-remove-item="${idx}" ${initialItems.length === 1 ? "disabled" : ""}>Удалить</button>
          </div>`,
        )
        .join("");
      initialItems.forEach((x, idx) => {
        const sel = itemsHost.querySelector(`[data-item-id="${idx}"]`);
        if (sel) sel.value = String(x.itemId);
        const av = itemsHost.querySelector(`[data-item-available="${idx}"]`);
        if (av) av.value = `В наличии: ${availableQtyByItemId(x.itemId)}`;
      });
      const selectedIds = initialItems.map((x) => String(x.itemId));
      initialItems.forEach((x, idx) => {
        const sel = itemsHost.querySelector(`[data-item-id="${idx}"]`);
        if (!sel) return;
        [...sel.options].forEach((opt) => {
          opt.disabled = opt.value !== String(x.itemId) && selectedIds.includes(opt.value);
        });
      });
      applyIntegerValidation(itemsHost);
      itemsHost.querySelectorAll("[data-remove-item]").forEach((b) =>
        b.addEventListener("click", () => {
          const idx = Number(b.dataset.removeItem);
          initialItems.splice(idx, 1);
          drawOrderItems();
        }),
      );
      itemsHost.querySelectorAll("select[data-item-id]").forEach((s) =>
        s.addEventListener("change", () => {
          const idx = Number(s.dataset.itemId);
          initialItems[idx].itemId = Number(s.value);
          drawOrderItems();
        }),
      );
      itemsHost.querySelectorAll("[data-item-qty]").forEach((inp) =>
        inp.addEventListener("input", () => {
          const idx = Number(inp.dataset.itemQty);
          initialItems[idx].quantity = Number(inp.value);
          const av = itemsHost.querySelector(`[data-item-available="${idx}"]`);
          if (av) av.value = `В наличии: ${availableQtyByItemId(initialItems[idx].itemId)}`;
        }),
      );
    };
    drawOrderItems();
    document.getElementById("btnAddOrderItem").addEventListener("click", () => {
      const used = new Set(initialItems.map((x) => Number(x.itemId)));
      const next = nodes.find((n) => !used.has(n.id));
      if (!next) {
        toast("В заказе уже выбраны все доступные изделия");
        return;
      }
      initialItems.push({ itemId: next.id, quantity: 1 });
      drawOrderItems();
    });

    document.getElementById("formOrder").addEventListener("submit", (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const items = initialItems.map((_, idx) => ({
        itemId: Number(itemsHost.querySelector(`[data-item-id="${idx}"]`)?.value || 0),
        quantity: Number(itemsHost.querySelector(`[data-item-qty="${idx}"]`)?.value || 0),
      }));
      const uniqueIds = new Set(items.map((x) => x.itemId));
      if (!items.length || items.some((x) => !x.itemId || !isIntegerLike(x.quantity) || x.quantity < 1))
        return toast("Введите целое число");
      if (uniqueIds.size !== items.length)
        return toast("В одном заказе каждое изделие должно быть уникальным");
      for (const line of items) {
        const avail = availableQtyByItemId(line.itemId);
        if (line.quantity > avail) {
          return toast(`Недостаточно остатка: в наличии ${avail} ${itemNameById(line.itemId).toLowerCase()}`);
        }
      }

      const orderDateRaw = String(fd.get("orderDate"));
      const dueDateRaw = String(fd.get("dueDate"));
      const orderDate = new Date(orderDateRaw).getTime();
      const dueDate = new Date(dueDateRaw).getTime();
      if (!Number.isFinite(orderDate) || !Number.isFinite(dueDate))
        return toast("Проверьте даты заказа");
      if (dueDate - orderDate < 60 * 60 * 1000)
        return toast("Дата готовности должна быть минимум на 1 час позже даты оформления");

      const prevOrders = cloneOrders();
      const payload = {
        items,
        orderDate: new Date(orderDateRaw).toISOString(),
        dueDate: new Date(dueDateRaw).toISOString(),
        orderType: String(fd.get("orderType")) === "closed" ? "closed" : "open",
      };
      if (editing) state.orders[editIndex] = payload;
      else state.orders.push(payload);
      saveOrders();
      rebuildDisplayBalances();
      renderBalances();
      setOrdersUndo(editing ? "Отмена изменения заказа" : "Отмена создания заказа", prevOrders);
      closeModal();
      renderOrders();
      toast(editing ? "Заказ изменен" : "Заказ добавлен", true);
    });
  }

  async function deleteItem(id) {
    if (!confirm(`Удалить позицию с ID ${id}? Будут удалены связанные строки спецификации и складские движения по ним.`)) return;
    try {
      const row = state.items.find((x) => x.id === id);
      await api(`/api/Items/${id}`, { method: "DELETE" });
      if (row) {
        const payload = {
          itemId: 0,
          itemCode: row.code || null,
          itemName: row.name,
          itemType: row.type,
          unit: row.unit || null,
          unitCost: row.unitCost,
          sellingPrice: row.sellingPrice,
        };
        setUndoAction("Отмена удаления номенклатуры", () => api("/api/Items", { method: "POST", body: JSON.stringify(payload) }));
      }
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  async function deleteBom(id) {
    if (!confirm(`Удалить строку спецификации с ID ${id}? Складские операции по этой строке будут удалены.`)) return;
    try {
      const row = state.boms.find((x) => x.id === id);
      await api(`/api/Boms/${id}`, { method: "DELETE" });
      if (row) {
        const payload = {
          bomId: 0,
          parentItemId: row.parentItemId,
          childItemId: row.childItemId,
          quantity: row.quantity,
        };
        setUndoAction("Отмена удаления BOM", () => api("/api/Boms", { method: "POST", body: JSON.stringify(payload) }));
      }
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  async function deleteStock(id) {
    if (!confirm(`Удалить операцию с ID ${id}?`)) return;
    try {
      const row = state.stock.find((x) => x.id === id);
      await api(`/api/StockOperations/${id}`, { method: "DELETE" });
      if (row) {
        const payload = {
          stockOperationId: 0,
          specificationId: row.specificationId,
          date: row.date,
          quantity: row.quantity,
          operationType: row.operationType,
        };
        setUndoAction("Отмена удаления операции", () =>
          api("/api/StockOperations", { method: "POST", body: JSON.stringify(payload) }),
        );
      }
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  function setView(name) {
    document.querySelectorAll(".tab").forEach((x) => x.classList.toggle("tab--active", x.dataset.view === name));
    document.querySelectorAll(".view").forEach((x) => x.classList.toggle("view--active", x.id === `view-${name}`));
    if (name === "production") {
      populateProductSelect();
      fetchCapacity();
      fetchProductionStats();
    }
  }

  document.querySelectorAll(".tab").forEach((b) => b.addEventListener("click", () => setView(b.dataset.view)));
  document.getElementById("btnAddItem").addEventListener("click", () => openItemModal(null));
  document.getElementById("btnAddBom").addEventListener("click", () => openBomModal(null));
  document.getElementById("btnAddStock").addEventListener("click", () => openStockModal(null));
  document.getElementById("btnAddOrder")?.addEventListener("click", () => openOrderModal());
  document.getElementById("btnSortOperation")?.addEventListener("click", () => {
    state.stockOpSortAsc = !state.stockOpSortAsc;
    renderStock();
  });
  document.getElementById("btnUndo")?.addEventListener("click", async () => {
    if (!state.undo) return;
    try {
      await state.undo.run();
      clearUndoAction();
      toast("Последнее действие отменено", true);
      await loadAll();
    } catch (err) {
      toast(err.message || "Не удалось отменить последнее действие");
    }
  });
  document.getElementById("selectProduct")?.addEventListener("change", () => {
    fetchCapacity();
    fetchProductionStats();
  });

  document.getElementById("btnReceiptMaterials")?.addEventListener("click", async () => {
    try {
      const sel = document.getElementById("selectProduct");
      const pid = Number(sel?.value);
      const body = { bikeCount: 10 };
      if (pid > 0) body.productItemId = pid;
      await api("/api/Production/receipt-materials", {
        method: "POST",
        body: JSON.stringify(body),
      });
      toast("Материалы оприходованы на 10 велосипедов", true);
      await loadAll();
      const bike = state.items.find((x) => x.code === "BIKE");
      if (bike && sel) sel.value = String(bike.id);
      await fetchCapacity();
      await fetchProductionStats();
    } catch (err) {
      toast(err.message);
    }
  });

  document.getElementById("btnProduce")?.addEventListener("click", async () => {
    const sel = document.getElementById("selectProduct");
    const inp = document.getElementById("inputProduceQty");
    const id = Number(sel?.value);
    const qty = Number(inp?.value);
    if (!id) {
      toast("Выберите изделие.");
      return;
    }
    if (!Number.isFinite(qty) || qty < 1 || !isIntegerLike(qty)) {
      toast("Укажите целое количество ≥ 1.");
      return;
    }
    try {
      await api("/api/Production/produce", {
        method: "POST",
        body: JSON.stringify({ productItemId: id, quantity: qty }),
      });
      toast(`Выпущено ${qty} шт.`, true);
      await loadAll();
      await fetchCapacity();
      await fetchProductionStats();
    } catch (err) {
      toast(err.message);
    }
  });

  document.getElementById("btnRefresh").addEventListener("click", async () => {
    try {
      await loadAll();
      setApiError("");
      toast("Данные обновлены", true);
    } catch (err) {
      const m = err?.message || String(err);
      toast(m);
      setApiError("Ошибка обновления: " + m);
    }
  });

  applyIntegerValidation(document);

  loadAll().catch((err) => {
    const m = err?.message || String(err);
    toast(m);
    setApiError(
      "Не удалось загрузить данные с сервера. " +
        m +
        " Проверьте, что приложение запущено, в браузере открывается /api/health и страница Swagger.",
    );
  });
})();
