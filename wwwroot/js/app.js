(() => {
  const state = { items: [], boms: [], stock: [] };

  const pick = (obj, keys, fallback = null) => {
    for (const k of keys) {
      if (obj && obj[k] !== undefined && obj[k] !== null) return obj[k];
    }
    return fallback;
  };

  const normalizeItem = (x) => ({
    id: Number(pick(x, ["itemId", "itemID", "ItemID", "itemid"], 0)),
    code: String(pick(x, ["itemCode", "ItemCode"], "") ?? ""),
    name: String(pick(x, ["itemName", "ItemName"], "") ?? ""),
    type: String(pick(x, ["itemType", "ItemType"], "") ?? ""),
    unit: pick(x, ["unit", "Unit"], ""),
    unitCost: pick(x, ["unitCost", "UnitCost"], null),
    leadTimeDays: pick(x, ["leadTimeDays", "LeadTimeDays"], null),
  });

  const normalizeBom = (x) => ({
    id: Number(pick(x, ["bomId", "bomid", "BOMID"], 0)),
    parentItemId: Number(pick(x, ["parentItemId", "ParentItemID"], 0)),
    childItemId: Number(pick(x, ["childItemId", "ChildItemID"], 0)),
    quantity: Number(pick(x, ["quantity", "Quantity"], 0)),
  });

  const normalizeStock = (x) => ({
    id: Number(pick(x, ["stockOperationId", "stockoperationId", "StockOperationID"], 0)),
    specificationId: Number(pick(x, ["specificationId", "SpecificationId"], 0)),
    date: pick(x, ["date", "Date"], null),
    quantity: Number(pick(x, ["quantity", "Quantity"], 0)),
    operationType: String(pick(x, ["operationType", "OperationType"], "") ?? ""),
  });

  function itemLabel(id) {
    const it = state.items.find((x) => x.id === id);
    if (!it) return `ID ${id}`;
    return `${it.id} - ${it.name}${it.code ? ` (${it.code})` : ""}`;
  }

  async function api(url, options = {}) {
    const headers = { Accept: "application/json", ...(options.headers || {}) };
    if (options.body && !headers["Content-Type"]) headers["Content-Type"] = "application/json";

    const res = await fetch(url, { ...options, headers });
    if (res.status === 204) return null;

    const text = await res.text();
    const isJson = (res.headers.get("content-type") || "").includes("application/json");
    const body = text ? (isJson ? JSON.parse(text) : text) : null;

    if (!res.ok) {
      const msg = typeof body === "string" ? body : body?.title || body?.detail || res.statusText;
      throw new Error(msg || "Ошибка запроса");
    }
    return body;
  }

  function toast(message, ok = false) {
    const host = document.getElementById("toastHost");
    const el = document.createElement("div");
    el.className = `toast ${ok ? "toast--ok" : "toast--err"}`;
    el.textContent = message;
    host.appendChild(el);
    setTimeout(() => el.remove(), 4000);
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
    return Number(v).toLocaleString("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  async function loadAll() {
    const [itemsRaw, bomsRaw, stockRaw] = await Promise.all([
      api("/api/Items"),
      api("/api/Boms"),
      api("/api/StockOperations").catch(() => []),
    ]);

    state.items = (itemsRaw || []).map(normalizeItem);
    state.boms = (bomsRaw || []).map(normalizeBom);
    state.stock = (stockRaw || []).map(normalizeStock);

    renderItems();
    renderBoms();
    renderStock();
  }

  function renderItems() {
    const tb = document.querySelector("#tableItems tbody");
    tb.innerHTML = "";
    if (!state.items.length) {
      tb.innerHTML = '<tr><td colspan="8"><div class="empty">Нет данных</div></td></tr>';
      return;
    }

    for (const x of state.items) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${x.id}</td>
        <td class="mono">${esc(x.code || "-")}</td>
        <td>${esc(x.name)}</td>
        <td>${esc(x.type)}</td>
        <td>${esc(x.unit || "-")}</td>
        <td>${toMoney(x.unitCost)}</td>
        <td>${x.leadTimeDays ?? "-"}</td>
        <td>
          <button class="btn btn--small" data-edit-item="${x.id}" type="button">Изм.</button>
          <button class="btn btn--small btn--danger" data-del-item="${x.id}" type="button">Удал.</button>
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
      tb.innerHTML = '<tr><td colspan="5"><div class="empty">Нет данных</div></td></tr>';
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
          <button class="btn btn--small" data-edit-bom="${x.id}" type="button">Изм.</button>
          <button class="btn btn--small btn--danger" data-del-bom="${x.id}" type="button">Удал.</button>
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
      tb.innerHTML = '<tr><td colspan="6"><div class="empty">Нет данных</div></td></tr>';
      return;
    }

    for (const x of state.stock) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${x.id}</td>
        <td>${x.specificationId}</td>
        <td>${x.date ? new Date(x.date).toLocaleString("ru-RU") : "-"}</td>
        <td>${x.quantity}</td>
        <td>${esc(x.operationType)}</td>
        <td>
          <button class="btn btn--small" data-edit-stock="${x.id}" type="button">Изм.</button>
          <button class="btn btn--small btn--danger" data-del-stock="${x.id}" type="button">Удал.</button>
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

  function itemOptions(selected) {
    return state.items
      .map((x) => `<option value="${x.id}" ${x.id === selected ? "selected" : ""}>${esc(itemLabel(x.id))}</option>`)
      .join("");
  }

  function openItemModal(id) {
    const row = id ? state.items.find((x) => x.id === id) : null;
    const edit = !!row;

    openModal(
      edit ? `Редактировать #${id}` : "Новая позиция",
      `<form id="formItem" class="form-grid">
        <div class="form-row"><label>Код</label><input name="itemCode" value="${esc(row?.code || "")}" /></div>
        <div class="form-row"><label>Название</label><input name="itemName" required value="${esc(row?.name || "")}" /></div>
        <div class="form-row"><label>Тип</label><select name="itemType"><option>Product</option><option>Assembly</option><option selected>Component</option><option>Material</option></select></div>
        <div class="form-row"><label>Ед.</label><input name="unit" value="${esc(row?.unit || "")}" /></div>
        <div class="form-row"><label>Цена</label><input name="unitCost" type="number" step="0.01" value="${row?.unitCost ?? ""}" /></div>
        <div class="form-row"><label>Срок, дн.</label><input name="leadTimeDays" type="number" value="${row?.leadTimeDays ?? ""}" /></div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">${edit ? "Сохранить" : "Создать"}</button>
        </div>
      </form>`,
    );

    if (row) modalBody.querySelector('[name="itemType"]').value = row.type || "Component";

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
        leadTimeDays: fd.get("leadTimeDays") === "" ? null : Number(fd.get("leadTimeDays")),
      };

      try {
        if (edit) await api(`/api/Items/${id}`, { method: "PUT", body: JSON.stringify(payload) });
        else await api("/api/Items", { method: "POST", body: JSON.stringify(payload) });
        closeModal();
        toast(edit ? "Изменено" : "Создано", true);
        await loadAll();
      } catch (err) {
        toast(err.message);
      }
    });
  }

  function openBomModal(id) {
    if (!state.items.length) {
      toast("Сначала добавьте номенклатуру");
      return;
    }
    const row = id ? state.boms.find((x) => x.id === id) : null;
    const edit = !!row;

    openModal(
      edit ? `Редактировать BOM #${id}` : "Новый BOM",
      `<form id="formBom" class="form-grid">
        <div class="form-row"><label>Родитель</label><select name="parentItemId">${itemOptions(row?.parentItemId || state.items[0].id)}</select></div>
        <div class="form-row"><label>Компонент</label><select name="childItemId">${itemOptions(row?.childItemId || state.items[0].id)}</select></div>
        <div class="form-row"><label>Количество</label><input name="quantity" type="number" step="0.01" min="0.01" value="${row?.quantity ?? 1}" /></div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">${edit ? "Сохранить" : "Создать"}</button>
        </div>
      </form>`,
    );

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
        toast(edit ? "Изменено" : "Создано", true);
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
      toast("Сначала добавьте BOM");
      return;
    }
    const row = id ? state.stock.find((x) => x.id === id) : null;
    const edit = !!row;

    const bomOptions = state.boms
      .map((b) => {
        const text = `#${b.id} ${itemLabel(b.parentItemId)} -> ${itemLabel(b.childItemId)}`;
        return `<option value="${b.id}" ${b.id === (row?.specificationId || state.boms[0].id) ? "selected" : ""}>${esc(text)}</option>`;
      })
      .join("");

    openModal(
      edit ? `Редактировать операцию #${id}` : "Новая операция",
      `<form id="formStock" class="form-grid">
        <div class="form-row"><label>BOM</label><select name="specificationId">${bomOptions}</select></div>
        <div class="form-row"><label>Дата</label><input name="date" type="datetime-local" value="${dateToLocalInput(row?.date)}" required /></div>
        <div class="form-row"><label>Количество</label><input name="quantity" type="number" step="0.0001" min="0.0001" value="${row?.quantity ?? 1}" required /></div>
        <div class="form-row"><label>Тип</label><select name="operationType"><option>Receipt</option><option>Issue</option><option>Adjustment</option></select></div>
        <div class="form-actions">
          <button class="btn" type="button" data-close-modal>Отмена</button>
          <button class="btn btn--primary" type="submit">${edit ? "Сохранить" : "Создать"}</button>
        </div>
      </form>`,
    );

    if (row?.operationType) modalBody.querySelector('[name="operationType"]').value = row.operationType;

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
        toast(edit ? "Изменено" : "Создано", true);
        await loadAll();
      } catch (err) {
        toast(err.message);
      }
    });
  }

  async function deleteItem(id) {
    if (!confirm(`Удалить позицию #${id}?`)) return;
    try {
      await api(`/api/Items/${id}`, { method: "DELETE" });
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  async function deleteBom(id) {
    if (!confirm(`Удалить BOM #${id}?`)) return;
    try {
      await api(`/api/Boms/${id}`, { method: "DELETE" });
      toast("Удалено", true);
      await loadAll();
    } catch (err) {
      toast(err.message);
    }
  }

  async function deleteStock(id) {
    if (!confirm(`Удалить операцию #${id}?`)) return;
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
      toast("Обновлено", true);
    } catch (err) {
      toast(err.message);
    }
  });

  loadAll().catch((err) => toast(err.message));
})();
