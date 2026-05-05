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
  row.querySelectorAll("[data-whole-euro-input]").forEach(initializeWholeEuroInput);
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

let checkoutCitiesByPostalCode = null;

async function loadCheckoutCitiesByPostalCode() {
  if (checkoutCitiesByPostalCode !== null) {
    return checkoutCitiesByPostalCode;
  }

  try {
    const response = await fetch("/data/isere-postal-cities.json");
    checkoutCitiesByPostalCode = response.ok ? await response.json() : {};
  } catch {
    checkoutCitiesByPostalCode = {};
  }

  return checkoutCitiesByPostalCode;
}

function initializePostalCityRoot(root) {
  const postalCodeInput = root.querySelector("[data-postal-code-input]");
  const cityInput = root.querySelector("[data-city-input]");
  const cityOptions = root.querySelector("[data-city-options]");

  if (!postalCodeInput || !cityInput || !cityOptions) {
    return;
  }

  let lastPrefilledCity = "";

  async function updateCityOptions() {
    const postalCode = postalCodeInput.value.trim();
    cityOptions.replaceChildren();

    if (!/^38\d{3}$/.test(postalCode)) {
      if (cityInput.value === lastPrefilledCity) {
        cityInput.value = "";
      }

      lastPrefilledCity = "";
      return;
    }

    const cityMap = await loadCheckoutCitiesByPostalCode();
    if (postalCodeInput.value.trim() !== postalCode) {
      return;
    }

    const cities = cityMap[postalCode] || [];
    for (const city of cities) {
      const option = document.createElement("option");
      option.value = city;
      cityOptions.appendChild(option);
    }

    if (cities.length === 1 && (!cityInput.value.trim() || cityInput.value === lastPrefilledCity)) {
      cityInput.value = cities[0];
      lastPrefilledCity = cities[0];
      cityInput.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }

    if (cityInput.value !== lastPrefilledCity) {
      lastPrefilledCity = "";
    }
  }

  postalCodeInput.addEventListener("input", updateCityOptions);
  updateCityOptions();
}

function initializeWholeEuroInput(input) {
  const message = input.dataset.wholeEuroMessage || "Le montant doit être un nombre entier en euros.";

  function updateValidityMessage() {
    if (input.validity.patternMismatch) {
      input.setCustomValidity(message);
      return;
    }

    input.setCustomValidity("");
  }

  input.addEventListener("input", updateValidityMessage);
  input.addEventListener("invalid", updateValidityMessage);
  updateValidityMessage();
}

async function postCheckoutCart(url, cart, options = {}) {
  const body = {
    itemIds: cart.map(item => item.id)
  };

  if (options.buyerAddress !== undefined) {
    body.buyerAddress = options.buyerAddress;
  }

  if (options.buyerName !== undefined) {
    body.buyerName = options.buyerName;
  }

  if (options.buyerPostalCode !== undefined) {
    body.buyerPostalCode = options.buyerPostalCode;
  }

  if (options.buyerCity !== undefined) {
    body.buyerCity = options.buyerCity;
  }

  if (options.paymentMethod !== undefined) {
    body.paymentMethod = options.paymentMethod;
  }

  if (options.chequeNumber !== undefined) {
    body.chequeNumber = options.chequeNumber;
  }

  if (options.chequeBankName !== undefined) {
    body.chequeBankName = options.chequeBankName;
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
  const buyerAddressInput = root.querySelector("[data-checkout-buyer-address]");
  const buyerNameInput = root.querySelector("[data-checkout-buyer-name]");
  const buyerPostalCodeInput = root.querySelector("[data-checkout-buyer-postal-code]");
  const buyerCityInput = root.querySelector("[data-checkout-buyer-city]");
  const buyerMessage = root.querySelector("[data-checkout-buyer-message]");
  const finalizeButton = root.querySelector("[data-checkout-finalize]");
  const modal = root.querySelector("[data-checkout-modal]");
  const modalBackButton = root.querySelector("[data-checkout-modal-back]");
  const saveSaleButton = root.querySelector("[data-checkout-save-sale]");
  const savePrintButton = root.querySelector("[data-checkout-save-print]");
  const paymentMethodInputs = root.querySelectorAll("[data-checkout-payment-method]");
  const chequeDetails = root.querySelector("[data-checkout-cheque-details]");
  const chequeNumberInput = root.querySelector("[data-checkout-cheque-number]");
  const chequeBankNameInput = root.querySelector("[data-checkout-cheque-bank-name]");
  const paymentMessage = root.querySelector("[data-checkout-payment-message]");
  const modalCount = root.querySelector("[data-checkout-modal-count]");
  const modalSubtotal = root.querySelector("[data-checkout-modal-subtotal]");
  const modalFee = root.querySelector("[data-checkout-modal-fee]");
  const modalTotal = root.querySelector("[data-checkout-modal-total]");
  const receipt = document.querySelector("[data-checkout-receipt]");
  const receiptSaleId = document.querySelector("[data-checkout-receipt-sale-id]");
  const receiptDate = document.querySelector("[data-checkout-receipt-date]");
  const receiptBuyerSection = document.querySelector("[data-checkout-receipt-buyer-section]");
  const receiptBuyerText = document.querySelector("[data-checkout-receipt-buyer-text]");
  const receiptAddressText = document.querySelector("[data-checkout-receipt-address-text]");
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

  function getBuyerAddress() {
    return buyerAddressInput?.value.trim() || "";
  }

  function getBuyerDetails() {
    return {
      buyerName: buyerNameInput?.value.trim() || "",
      buyerPostalCode: buyerPostalCodeInput?.value.trim() || "",
      buyerCity: buyerCityInput?.value.trim() || ""
    };
  }

  function setBuyerMessage(text) {
    if (buyerMessage) {
      buyerMessage.textContent = text;
    }
  }

  function validateBuyerDetails() {
    const buyerDetails = getBuyerDetails();
    const missingFields = [
      [buyerDetails.buyerName, buyerNameInput],
      [buyerDetails.buyerPostalCode, buyerPostalCodeInput],
      [buyerDetails.buyerCity, buyerCityInput]
    ].filter(([value]) => value.length === 0);

    if (missingFields.length === 0) {
      setBuyerMessage("");
      return true;
    }

    setBuyerMessage("Le nom, le code postal et la ville de l'acheteur sont obligatoires.");
    missingFields[0][1]?.focus();
    return false;
  }

  function getPaymentDetails() {
    const selectedMethod = root.querySelector("[data-checkout-payment-method]:checked")?.value || "card";
    return {
      paymentMethod: selectedMethod,
      chequeNumber: chequeNumberInput?.value.trim() || "",
      chequeBankName: chequeBankNameInput?.value.trim() || ""
    };
  }

  function setPaymentMessage(text) {
    if (paymentMessage) {
      paymentMessage.textContent = text;
    }
  }

  function updateChequeDetailsVisibility() {
    const paymentDetails = getPaymentDetails();
    const isCheque = paymentDetails.paymentMethod === "cheque";

    if (chequeDetails) {
      chequeDetails.hidden = !isCheque;
    }

    if (chequeNumberInput) {
      chequeNumberInput.required = isCheque;
    }

    if (chequeBankNameInput) {
      chequeBankNameInput.required = isCheque;
    }

    if (!isCheque) {
      setPaymentMessage("");
    }
  }

  function validatePaymentDetails() {
    const paymentDetails = getPaymentDetails();
    if (paymentDetails.paymentMethod !== "cheque") {
      setPaymentMessage("");
      return true;
    }

    if (paymentDetails.chequeNumber.length > 0 && paymentDetails.chequeBankName.length > 0) {
      setPaymentMessage("");
      return true;
    }

    setPaymentMessage("Le numéro de chèque et le nom de la banque sont obligatoires pour un paiement par chèque.");
    if (paymentDetails.chequeNumber.length === 0) {
      chequeNumberInput?.focus();
    } else {
      chequeBankNameInput?.focus();
    }
    return false;
  }

  function getSaleDetails() {
    return {
      buyerAddress: getBuyerAddress(),
      ...getBuyerDetails(),
      ...getPaymentDetails()
    };
  }

  function formatBuyerDetails(saleDetails) {
    const location = [saleDetails.buyerPostalCode, saleDetails.buyerCity]
      .filter(value => value.length > 0)
      .join(" ");

    return [saleDetails.buyerName, location]
      .filter(value => value.length > 0)
      .join(" - ");
  }

  function updateSavePrintButtonState() {
    if (savePrintButton) {
      savePrintButton.disabled = getBuyerAddress().length === 0;
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

  function renderReceipt(payload, saleDetails) {
    if (!receipt || !receiptLines) {
      return;
    }

    if (receiptSaleId) {
      receiptSaleId.textContent = payload.saleId ? `#${payload.saleId}` : "";
    }

    if (receiptDate) {
      receiptDate.textContent = new Date().toLocaleString("fr-FR");
    }

    const buyerDetails = formatBuyerDetails(saleDetails);
    if (receiptBuyerSection) {
      receiptBuyerSection.hidden = buyerDetails.length === 0;
    }

    if (receiptBuyerText) {
      receiptBuyerText.textContent = buyerDetails;
    }

    if (receiptAddressText) {
      receiptAddressText.textContent = saleDetails.buyerAddress;
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
  buyerAddressInput?.addEventListener("input", updateSavePrintButtonState);
  [buyerNameInput, buyerPostalCodeInput, buyerCityInput].forEach(input => {
    input?.addEventListener("input", () => {
      if (getBuyerDetails().buyerName && getBuyerDetails().buyerPostalCode && getBuyerDetails().buyerCity) {
        setBuyerMessage("");
      }
    });
  });
  paymentMethodInputs.forEach(input => input.addEventListener("change", updateChequeDetailsVisibility));
  chequeNumberInput?.addEventListener("input", validatePaymentDetails);
  chequeBankNameInput?.addEventListener("input", validatePaymentDetails);
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

    if (!validateBuyerDetails()) {
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
    if (!validateBuyerDetails()) {
      return;
    }

    if (!validatePaymentDetails()) {
      return;
    }

    const { response, payload } = await postCheckoutCart("/api/ventes", cart, getSaleDetails());
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

    if (response.status === 400) {
      setPaymentMessage(payload.message || "Les informations de paiement sont incomplètes.");
      return;
    }

    setMessage("La vente n'a pas pu être enregistrée. Réessayez.");
    hideModal();
  });

  savePrintButton?.addEventListener("click", async () => {
    const saleDetails = getSaleDetails();
    if (saleDetails.buyerAddress.length === 0) {
      return;
    }

    if (!validateBuyerDetails()) {
      return;
    }

    if (!validatePaymentDetails()) {
      return;
    }

    const { response, payload } = await postCheckoutCart("/api/ventes", cart, saleDetails);
    if (response.status === 201 && payload.saved) {
      renderReceipt(payload, saleDetails);
      printReceiptAndReload();
      return;
    }

    if (response.status === 400) {
      setPaymentMessage(payload.message || "Les informations de paiement sont incomplètes.");
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
  updateChequeDetailsVisibility();
  updateSavePrintButtonState();
}

document.querySelectorAll("[data-postal-city-root]").forEach(initializePostalCityRoot);
document.querySelectorAll("[data-whole-euro-input]").forEach(initializeWholeEuroInput);
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

function initializeItemCorrection(root) {
  const searchInput = root.querySelector("[data-item-correction-search]");
  const rows = Array.from(root.querySelectorAll("[data-item-correction-row]"));
  const emptyMessage = root.querySelector("[data-item-correction-empty-message]");

  if (!searchInput || rows.length === 0) {
    return;
  }

  function renderRows() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    let visibleRows = 0;

    for (const row of rows) {
      const filterText = row.dataset.itemCorrectionFilterText || "";
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

document.querySelectorAll("[data-item-correction-root]").forEach(initializeItemCorrection);

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
