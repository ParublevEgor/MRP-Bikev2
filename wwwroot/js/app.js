(() => {
  const state = { items: [], boms: [], stock: [], balances: [] };

  const ITEM_TYPE_RU = {
    Product: "Готовая продукция",
    Assembly: "Сборочная единица",
    Component: "Компонент",
    Material: "Материал",
  };

  const OP_TYPE_RU = {
    Receipt: "Приход",
    Issue: "Расход",
    Adjustment: "Корректировка",
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
    adjustmentQty: Number(pick(x, ["adjustmentQty", "AdjustmentQty"], 0)),
    currentStock: Number(pick(x, ["currentStock", "CurrentStock"], 0)),
  });

  /** Защита от «тихого» 0, если ключ JSON не совпал с ожидаемым. */
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
    return `${it.id} — ${it.name}${it.code ? ` (${it.code})` : ""}`;
  }

  function typeRu(en) {
    return ITEM_TYPE_RU[en] || en || "—";
  }

  function opRu(en) {
    return OP_TYPE_RU[en] || en || "—";
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
    const keys = ["Receipt", "Issue", "Adjustment"];
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
    if (v == null || Number.isNaN(Number(v))) return "—";
    return `${Number(v).toLocaleString("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₽`;
  }

  function toQty(v) {
    if (v == null || Number.isNaN(Number(v))) return "—";
    return Number(v).toLocaleString("ru-RU", { maximumFractionDigits: 4 });
  }

  /** Краткая подпись строки BOM для таблицы склада. */
  function bomLineLabel(bomId) {
    const b = state.boms.find((x) => x.id === bomId);
    if (!b) return `#${bomId}`;
    return `#${b.id}: ${itemLabel(b.parentItemId)} → ${itemLabel(b.childItemId)}`;
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

    if (!Array.isArray(itemsRaw)) throw new Error("Ответ /api/Items не массив — проверьте консоль сервера и /swagger/v1/swagger.json");
    if (!Array.isArray(bomsRaw)) throw new Error("Ответ /api/Boms не массив.");
    if (!Array.isArray(stockRaw)) throw new Error("Ответ /api/StockOperations не массив.");
    if (!Array.isArray(balancesRaw)) throw new Error("Ответ /api/Items/stock-balance не массив.");

    state.items = itemsRaw.map(normalizeItem);
    state.boms = bomsRaw.map(normalizeBom);
    state.stock = stockRaw.map(normalizeStock);
    state.balances = balancesRaw.map(normalizeBalance);

    renderItems();
    renderBoms();
    renderStock();
    renderBalances();
  }

  function renderItems() {
    const tb = document.querySelector("#tableItems tbody");
    tb.innerHTML = "";
    if (!state.items.length) {
      tb.innerHTML =
        '<tr><td colspan="7"><div class="empty">Нет позиций — добавьте первую или проверьте SQL Server.</div></td></tr>';
      return;
    }

    for (const x of state.items) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${x.id}</td>
        <td class="mono">${esc(x.code || "—")}</td>
        <td>${esc(x.name)}</td>
        <td>${esc(typeRu(x.type))}</td>
        <td>${esc(x.unit || "—")}</td>
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

    for (const x of state.boms) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${x.id}</td>
        <td>${esc(itemLabel(x.parentItemId))}</td>
        <td>${esc(itemLabel(x.childItemId))}</td>
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
        '<tr><td colspan="6"><div class="empty">Нет операций — нужна хотя бы одна строка BOM.</div></td></tr>';
      return;
    }

    for (const x of state.stock) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${x.id}</td>
        <td>${esc(bomLineLabel(x.specificationId))}</td>
        <td>${x.date ? new Date(x.date).toLocaleString("ru-RU") : "—"}</td>
        <td>${x.quantity}</td>
        <td>${esc(opRu(x.operationType))}</td>
        <td>
          <button class="btn btn--small" data-edit-stock="${x.id}" type="button">Изменить</button>
          <button class="btn btn--small btn--danger" data-del-stock="${x.id}" type="button">Удалить</button>
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
      tb.innerHTML = '<tr><td colspan="8"><div class="empty">Нет данных об остатках.</div></td></tr>';
      return;
    }

    for (const x of state.balances) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${x.itemId}</td>
        <td class="mono">${esc(x.itemCode || "—")}</td>
        <td>${esc(x.itemName)}</td>
        <td>${esc(x.unit || "—")}</td>
        <td class="num">${toQty(x.receiptQty)}</td>
        <td class="num">${toQty(x.issueQty)}</td>
        <td class="num">${toQty(x.adjustmentQty)}</td>
        <td class="num num--total">${toQty(x.currentStock)}</td>`;
      tb.appendChild(tr);
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
      };

      try {
        if (edit) await api(`/api/Items/${id}`, { method: "PUT", body: JSON.stringify(payload) });
        else await api("/api/Items", { method: "POST", body: JSON.stringify(payload) });
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

      try {
        if (edit) await api(`/api/Boms/${id}`, { method: "PUT", body: JSON.stringify(payload) });
        else await api("/api/Boms", { method: "POST", body: JSON.stringify(payload) });
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
        const t = `#${b.id}: ${itemLabel(b.parentItemId)} → ${itemLabel(b.childItemId)}, кол-во ${b.quantity}`;
        return `<option value="${b.id}" ${b.id === spec ? "selected" : ""}>${esc(t)}</option>`;
      })
      .join("");

    openModal(
      edit ? `Операция, ID ${id}` : "Новая операция",
      `<form id="formStock" class="form-grid">
        <div class="form-row"><label>Строка BOM</label><select name="specificationId">${bomOptions}</select></div>
        <div class="form-row"><label>Дата и время</label><input name="date" type="datetime-local" value="${dateToLocalInput(row?.date)}" required /></div>
        <div class="form-row"><label>Количество</label><input name="quantity" type="number" step="1" min="1" value="${row?.quantity ?? 1}" required /></div>
        <div class="form-row"><label>Тип операции</label><select name="operationType">${opTypeOptionsHtml(row?.operationType || "Receipt")}</select></div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">${edit ? "Сохранить" : "Создать"}</button>
        </div>
      </form>`,
    );

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

      try {
        if (edit) await api(`/api/StockOperations/${id}`, { method: "PUT", body: JSON.stringify(payload) });
        else await api("/api/StockOperations", { method: "POST", body: JSON.stringify(payload) });
        closeModal();
        toast(edit ? "Сохранено" : "Создано", true);
        await loadAll();
      } catch (err) {
        toast(err.message);
      }
    });
  }

  async function deleteItem(id) {
    if (!confirm(`Удалить позицию с ID ${id}? Будут удалены связанные строки спецификации и складские движения по ним.`)) return;
    try {
      await api(`/api/Items/${id}`, { method: "DELETE" });
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  async function deleteBom(id) {
    if (!confirm(`Удалить строку спецификации с ID ${id}? Складские операции по этой строке будут удалены.`)) return;
    try {
      await api(`/api/Boms/${id}`, { method: "DELETE" });
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  async function deleteStock(id) {
    if (!confirm(`Удалить операцию с ID ${id}?`)) return;
    try {
      await api(`/api/StockOperations/${id}`, { method: "DELETE" });
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  function setView(name) {
    document.querySelectorAll(".tab").forEach((x) => x.classList.toggle("tab--active", x.dataset.view === name));
    document.querySelectorAll(".view").forEach((x) => x.classList.toggle("view--active", x.id === `view-${name}`));
  }

  document.querySelectorAll(".tab").forEach((b) => b.addEventListener("click", () => setView(b.dataset.view)));
  document.getElementById("btnAddItem").addEventListener("click", () => openItemModal(null));
  document.getElementById("btnAddBom").addEventListener("click", () => openBomModal(null));
  document.getElementById("btnAddStock").addEventListener("click", () => openStockModal(null));
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
