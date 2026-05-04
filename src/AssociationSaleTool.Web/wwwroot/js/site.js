const rowIndexPatterns = [
  /Input\.Rows\[\d+\]/g,
  /Input_Rows_\d+__/g
];

function replaceRowIndex(value, nextIndex) {
  return value
    .replace(rowIndexPatterns[0], `Input.Rows[${nextIndex}]`)
    .replace(rowIndexPatterns[1], `Input_Rows_${nextIndex}__`);
}

function clearRow(row) {
  row.querySelectorAll("input").forEach(input => {
    input.value = "";
  });

  row.querySelectorAll(".validation").forEach(message => {
    message.textContent = "";
  });
}

function reindexRow(row, nextIndex) {
  row.querySelectorAll("[name], [id], [data-valmsg-for], [for], [aria-describedby]").forEach(element => {
    for (const attribute of ["name", "id", "data-valmsg-for", "for", "aria-describedby"]) {
      const value = element.getAttribute(attribute);
      if (value) {
        element.setAttribute(attribute, replaceRowIndex(value, nextIndex));
      }
    }
  });
}

document.addEventListener("click", event => {
  const button = event.target.closest("[data-add-item-row]");
  if (!button) {
    return;
  }

  const form = button.closest("form");
  const table = form?.querySelector("[data-item-registration-table]");
  const tbody = table?.querySelector("tbody");
  const lastRow = tbody?.querySelector("tr:last-child");

  if (!tbody || !lastRow) {
    return;
  }

  const nextIndex = tbody.querySelectorAll("tr").length;
  const row = lastRow.cloneNode(true);
  reindexRow(row, nextIndex);
  clearRow(row);
  tbody.appendChild(row);
  row.querySelector("input")?.focus();
});

function formatCheckoutPrice(cents) {
  return `${(cents / 100).toFixed(2)} EUR`;
}

function createCheckoutCell(text) {
  const cell = document.createElement("td");
  cell.textContent = text;
  return cell;
}

function createCheckoutEmptyRow(message, columnCount) {
  const row = document.createElement("tr");
  const cell = document.createElement("td");
  cell.colSpan = columnCount;
  cell.className = "empty-state";
  cell.textContent = message;
  row.appendChild(cell);
  return row;
}

async function postCheckoutCart(url, cart, receiptDetails) {
  const body = {
    itemIds: cart.map(item => item.id)
  };

  if (receiptDetails !== undefined) {
    body.receiptDetails = receiptDetails;
  }

  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body)
  });
  const text = await response.text();
  const payload = text ? JSON.parse(text) : {};
  return { response, payload };
}

function initializeCheckout(root) {
  const dataElement = document.getElementById("checkout-catalog-data");
  const searchInput = root.querySelector("[data-checkout-search]");
  const resultsBody = root.querySelector("[data-checkout-results]");
  const cartBody = root.querySelector("[data-checkout-cart]");
  const emptyCart = root.querySelector("[data-checkout-empty-cart]");
  const message = root.querySelector("[data-checkout-message]");
  const subtotalElement = root.querySelector("[data-checkout-subtotal]");
  const feeElement = root.querySelector("[data-checkout-fee]");
  const totalElement = root.querySelector("[data-checkout-total]");
  const receiptDetailsInput = root.querySelector("[data-checkout-receipt-details]");
  const finalizeButton = root.querySelector("[data-checkout-finalize]");
  const modal = root.querySelector("[data-checkout-modal]");
  const modalBackButton = root.querySelector("[data-checkout-modal-back]");
  const saveSaleButton = root.querySelector("[data-checkout-save-sale]");
  const savePrintButton = root.querySelector("[data-checkout-save-print]");
  const modalCount = root.querySelector("[data-checkout-modal-count]");
  const modalSubtotal = root.querySelector("[data-checkout-modal-subtotal]");
  const modalFee = root.querySelector("[data-checkout-modal-fee]");
  const modalTotal = root.querySelector("[data-checkout-modal-total]");
  const receipt = document.querySelector("[data-checkout-receipt]");
  const receiptSaleId = document.querySelector("[data-checkout-receipt-sale-id]");
  const receiptDate = document.querySelector("[data-checkout-receipt-date]");
  const receiptDetailsText = document.querySelector("[data-checkout-receipt-details-text]");
  const receiptLines = document.querySelector("[data-checkout-receipt-lines]");
  const receiptSubtotal = document.querySelector("[data-checkout-receipt-subtotal]");
  const receiptFee = document.querySelector("[data-checkout-receipt-fee]");
  const receiptTotal = document.querySelector("[data-checkout-receipt-total]");

  if (!dataElement || !searchInput || !resultsBody || !cartBody) {
    return;
  }

  const catalog = JSON.parse(dataElement.textContent || "[]");
  const cart = [];
  const conflictItemIds = new Set();

  function findCatalogItem(itemId) {
    return catalog.find(item => String(item.id) === String(itemId));
  }

  function setMessage(text) {
    if (message) {
      message.textContent = text;
    }
  }

  function setConflictItems(itemIds) {
    conflictItemIds.clear();
    itemIds.forEach(itemId => conflictItemIds.add(Number(itemId)));
  }

  function getReceiptDetails() {
    return receiptDetailsInput?.value.trim() || "";
  }

  function updateSavePrintButtonState() {
    if (savePrintButton) {
      savePrintButton.disabled = getReceiptDetails().length === 0;
    }
  }

  function hideModal() {
    if (modal) {
      modal.hidden = true;
    }
  }

  function showModal(totals, itemCount) {
    if (!modal) {
      return;
    }

    if (modalCount) {
      modalCount.textContent = itemCount.toString();
    }

    if (modalSubtotal) {
      modalSubtotal.textContent = formatCheckoutPrice(totals.subtotalSellerPriceCents);
    }

    if (modalFee) {
      modalFee.textContent = formatCheckoutPrice(totals.checkoutFeeCents);
    }

    if (modalTotal) {
      modalTotal.textContent = formatCheckoutPrice(totals.totalPaidCents);
    }

    modal.hidden = false;
    updateSavePrintButtonState();
  }

  function renderReceipt(payload, receiptDetails) {
    if (!receipt || !receiptLines) {
      return;
    }

    if (receiptSaleId) {
      receiptSaleId.textContent = payload.saleId ? `#${payload.saleId}` : "";
    }

    if (receiptDate) {
      receiptDate.textContent = new Date().toLocaleString("fr-FR");
    }

    if (receiptDetailsText) {
      receiptDetailsText.textContent = receiptDetails;
    }

    receiptLines.replaceChildren();
    for (const item of cart) {
      const row = document.createElement("tr");
      row.appendChild(createCheckoutCell(item.reference));
      row.appendChild(createCheckoutCell(item.title));
      row.appendChild(createCheckoutCell(formatCheckoutPrice(item.sellerPriceCents)));
      receiptLines.appendChild(row);
    }

    if (payload.totals) {
      if (receiptSubtotal) {
        receiptSubtotal.textContent = formatCheckoutPrice(payload.totals.subtotalSellerPriceCents);
      }

      if (receiptFee) {
        receiptFee.textContent = formatCheckoutPrice(payload.totals.checkoutFeeCents);
      }

      if (receiptTotal) {
        receiptTotal.textContent = formatCheckoutPrice(payload.totals.totalPaidCents);
      }
    }
  }

  function printReceiptAndReload() {
    document.body.classList.add("checkout-printing");
    window.onafterprint = () => {
      window.location.reload();
    };
    window.print();
  }

  function renderResults() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    resultsBody.replaceChildren();

    if (!query) {
      resultsBody.appendChild(createCheckoutEmptyRow("Saisissez une recherche pour filtrer les articles.", 3));
      return;
    }

    const matches = catalog.filter(item =>
      `${item.reference} ${item.title}`.toLocaleLowerCase("fr-FR").includes(query));

    if (matches.length === 0) {
      resultsBody.appendChild(createCheckoutEmptyRow("Aucun article ne correspond à cette recherche.", 3));
      return;
    }

    for (const item of matches) {
      const row = document.createElement("tr");
      row.dataset.checkoutItemId = item.id;
      row.className = "checkout-result-row";
      row.appendChild(createCheckoutCell(item.reference));
      row.appendChild(createCheckoutCell(item.title));
      row.appendChild(createCheckoutCell(formatCheckoutPrice(item.sellerPriceCents)));
      resultsBody.appendChild(row);
    }
  }

  function renderCart() {
    cartBody.replaceChildren();

    if (emptyCart) {
      emptyCart.hidden = cart.length > 0;
    }

    for (const item of cart) {
      const row = document.createElement("tr");
      if (conflictItemIds.has(Number(item.id))) {
        row.className = "checkout-conflict-row";
      }

      row.appendChild(createCheckoutCell(item.reference));
      row.appendChild(createCheckoutCell(item.title));
      row.appendChild(createCheckoutCell(formatCheckoutPrice(item.sellerPriceCents)));

      const actionCell = document.createElement("td");
      const removeButton = document.createElement("button");
      removeButton.type = "button";
      removeButton.className = "table-action-button";
      removeButton.dataset.checkoutRemove = item.id;
      removeButton.textContent = "Retirer";
      actionCell.appendChild(removeButton);
      row.appendChild(actionCell);
      cartBody.appendChild(row);
    }

    const subtotal = cart.reduce((sum, item) => sum + item.sellerPriceCents, 0);
    const fee = Math.round(subtotal / 10);
    const total = subtotal + fee;

    if (subtotalElement) {
      subtotalElement.textContent = formatCheckoutPrice(subtotal);
    }

    if (feeElement) {
      feeElement.textContent = formatCheckoutPrice(fee);
    }

    if (totalElement) {
      totalElement.textContent = formatCheckoutPrice(total);
    }

    if (finalizeButton) {
      finalizeButton.disabled = cart.length === 0;
    }
  }

  searchInput.addEventListener("input", renderResults);
  receiptDetailsInput?.addEventListener("input", updateSavePrintButtonState);

  resultsBody.addEventListener("click", event => {
    const row = event.target.closest("[data-checkout-item-id]");
    if (!row) {
      return;
    }

    const item = findCatalogItem(row.dataset.checkoutItemId);
    if (!item) {
      return;
    }

    if (cart.some(cartItem => cartItem.id === item.id)) {
      setMessage("Cet article est déjà dans le panier.");
      return;
    }

    cart.push(item);
    setMessage("");
    conflictItemIds.delete(Number(item.id));
    renderCart();
  });

  cartBody.addEventListener("click", event => {
    const button = event.target.closest("[data-checkout-remove]");
    if (!button) {
      return;
    }

    const index = cart.findIndex(item => String(item.id) === button.dataset.checkoutRemove);
    if (index >= 0) {
      conflictItemIds.delete(Number(cart[index].id));
      cart.splice(index, 1);
      if (conflictItemIds.size === 0) {
        setMessage("");
      }
      renderCart();
    }
  });

  finalizeButton?.addEventListener("click", async () => {
    if (cart.length === 0) {
      setMessage("Ajoutez au moins un article au panier.");
      return;
    }

    setMessage("");
    setConflictItems([]);
    renderCart();

    const { response, payload } = await postCheckoutCart("/api/caisse/validation", cart);
    if (response.ok && payload.isValid) {
      showModal(payload.totals, payload.itemCount);
      return;
    }

    if (response.status === 409) {
      setConflictItems(payload.conflictItemIds || []);
      setMessage(payload.message || "Un article du panier n'est plus disponible. Retirez la ligne en rouge avant de continuer.");
      hideModal();
      renderCart();
      return;
    }

    setMessage("La validation du panier a échoué. Réessayez.");
  });

  modalBackButton?.addEventListener("click", hideModal);

  saveSaleButton?.addEventListener("click", async () => {
    const { response, payload } = await postCheckoutCart("/api/ventes", cart, getReceiptDetails());
    if (response.status === 201 && payload.saved) {
      cart.splice(0, cart.length);
      setConflictItems([]);
      setMessage("Vente enregistrée.");
      renderCart();
      window.location.reload();
      return;
    }

    if (response.status === 409) {
      setConflictItems(payload.conflictItemIds || []);
      setMessage(payload.message || "La vente n'a pas été enregistrée : un article n'est plus disponible. Retirez la ligne en rouge avant de continuer.");
      hideModal();
      renderCart();
      return;
    }

    setMessage("La vente n'a pas pu être enregistrée. Réessayez.");
    hideModal();
  });

  savePrintButton?.addEventListener("click", async () => {
    const receiptDetails = getReceiptDetails();
    if (receiptDetails.length === 0) {
      return;
    }

    const { response, payload } = await postCheckoutCart("/api/ventes", cart, receiptDetails);
    if (response.status === 201 && payload.saved) {
      renderReceipt(payload, receiptDetails);
      printReceiptAndReload();
      return;
    }

    if (response.status === 409) {
      setConflictItems(payload.conflictItemIds || []);
      setMessage(payload.message || "La vente n'a pas été enregistrée : un article n'est plus disponible. Retirez la ligne en rouge avant de continuer.");
      hideModal();
      renderCart();
      return;
    }

    setMessage("La vente n'a pas pu être enregistrée. Réessayez.");
    hideModal();
  });

  renderResults();
  renderCart();
  updateSavePrintButtonState();
}

document.querySelectorAll("[data-checkout-root]").forEach(initializeCheckout);

function initializeReconciliation(root) {
  const searchInput = root.querySelector("[data-reconciliation-search]");
  const rows = Array.from(root.querySelectorAll("[data-reconciliation-item-row]"));
  const emptyMessage = root.querySelector("[data-reconciliation-empty-message]");

  if (!searchInput || rows.length === 0) {
    return;
  }

  function renderRows() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    let visibleRows = 0;

    for (const row of rows) {
      const filterText = row.dataset.reconciliationFilterText || "";
      const isVisible = !query || filterText.includes(query);
      row.hidden = !isVisible;

      if (isVisible) {
        visibleRows++;
      }
    }

    if (emptyMessage) {
      emptyMessage.hidden = visibleRows > 0 || !query;
    }
  }

  searchInput.addEventListener("input", renderRows);
  renderRows();
}

document.querySelectorAll("[data-reconciliation-root]").forEach(initializeReconciliation);

function initializeSaleCorrection(root) {
  const searchInput = root.querySelector("[data-sale-correction-search]");
  const rows = Array.from(root.querySelectorAll("[data-sale-correction-item-row]"));
  const emptyMessage = root.querySelector("[data-sale-correction-empty-message]");

  if (!searchInput || rows.length === 0) {
    return;
  }

  function renderRows() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    let visibleRows = 0;

    for (const row of rows) {
      const filterText = row.dataset.saleCorrectionFilterText || "";
      const isVisible = !query || filterText.includes(query);
      row.hidden = !isVisible;

      if (isVisible) {
        visibleRows++;
      }
    }

    if (emptyMessage) {
      emptyMessage.hidden = visibleRows > 0 || !query;
    }
  }

  searchInput.addEventListener("input", renderRows);
  renderRows();
}

document.querySelectorAll("[data-sale-correction-root]").forEach(initializeSaleCorrection);

function initializeSalesView(root) {
  const searchInput = root.querySelector("[data-sales-view-search]");
  const rows = Array.from(root.querySelectorAll("[data-sales-view-row]"));
  const emptyMessage = root.querySelector("[data-sales-view-empty-message]");

  if (!searchInput || rows.length === 0) {
    return;
  }

  function renderRows() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    let visibleRows = 0;

    for (const row of rows) {
      const filterText = row.dataset.salesViewFilterText || "";
      const isVisible = !query || filterText.includes(query);
      row.hidden = !isVisible;

      if (isVisible) {
        visibleRows++;
      }
    }

    if (emptyMessage) {
      emptyMessage.hidden = visibleRows > 0 || !query;
    }
  }

  searchInput.addEventListener("input", renderRows);
  renderRows();
}

document.querySelectorAll("[data-sales-view-root]").forEach(initializeSalesView);

function initializeSellerSearch(root) {
  const searchInput = root.querySelector("[data-seller-search]");
  const rows = Array.from(root.querySelectorAll("[data-seller-row]"));
  const emptyMessage = root.querySelector("[data-seller-empty-message]");

  if (!searchInput || rows.length === 0) {
    return;
  }

  function renderRows() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    let visibleRows = 0;

    for (const row of rows) {
      const filterText = row.dataset.sellerFilterText || "";
      const isVisible = !query || filterText.includes(query);
      row.hidden = !isVisible;

      if (isVisible) {
        visibleRows++;
      }
    }

    if (emptyMessage) {
      emptyMessage.hidden = visibleRows > 0 || !query;
    }
  }

  searchInput.addEventListener("input", renderRows);
  renderRows();
}

document.querySelectorAll("[data-seller-search-root]").forEach(initializeSellerSearch);
