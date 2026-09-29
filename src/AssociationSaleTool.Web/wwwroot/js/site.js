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

function formatItemRegistrationCount(existingCount, newCount) {
  const total = existingCount + newCount;
  const label = `${total} ${total > 1 ? "articles" : "article"}`;
  if (existingCount === 0 || newCount === 0) {
    return label;
  }

  return `${label} (dont ${newCount} ${newCount > 1 ? "nouveaux" : "nouveau"})`;
}

// Counts the rows saving would try to create: a row with only a description or only a
// price still counts, since the operator means to add it; saving then asks for the rest.
function updateItemRegistrationCount(counter) {
  const tbody = counter.closest("form")?.querySelector("[data-item-registration-table] tbody");
  const rows = tbody ? Array.from(tbody.querySelectorAll("tr")) : [];
  const newCount = rows.filter(row =>
    Array.from(row.querySelectorAll("input")).some(input => input.value.trim() !== "")).length;
  const existingCount = Number.parseInt(counter.dataset.existingItemCount, 10) || 0;
  counter.textContent = formatItemRegistrationCount(existingCount, newCount);
}

function refreshItemRegistrationCount(element) {
  const counter = element.closest("form")?.querySelector("[data-item-registration-count]");
  if (counter) {
    updateItemRegistrationCount(counter);
  }
}

document.addEventListener("input", event => {
  if (event.target.closest("[data-item-registration-table]")) {
    refreshItemRegistrationCount(event.target);
  }
});

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
  refreshItemRegistrationCount(form);
  row.querySelector("input")?.focus();
});

document.addEventListener("change", event => {
  const input = event.target.closest("[data-auto-submit-file-input]");
  if (!input || !input.files || input.files.length === 0) {
    return;
  }

  const form = input.closest("form");
  const status = form?.querySelector("[data-auto-submit-file-status]");
  if (status) {
    status.textContent = `${input.files[0].name} - import en cours...`;
  }

  if (form?.requestSubmit) {
    form.requestSubmit();
  } else {
    form?.submit();
  }
});

function formatCheckoutPrice(cents) {
  return `${(cents / 100).toFixed(2)} €`;
}

function createCheckoutCell(text) {
  const cell = document.createElement("td");
  cell.textContent = text;
  return cell;
}

function createCheckoutAddButton(itemId) {
  const addButton = document.createElement("button");
  addButton.type = "button";
  addButton.className = "table-action-button table-action-button-primary";
  addButton.dataset.checkoutAdd = itemId;
  addButton.textContent = "Ajouter";
  return addButton;
}

function createCheckoutActionCell(itemId) {
  const cell = document.createElement("td");
  cell.className = "checkout-result-action-cell";
  cell.appendChild(createCheckoutAddButton(itemId));
  return cell;
}

function createCheckoutAlreadyAddedCell() {
  const cell = document.createElement("td");
  cell.className = "checkout-result-action-cell";
  const alreadyAdded = document.createElement("span");
  alreadyAdded.className = "checkout-already-added-badge";
  alreadyAdded.textContent = "Déjà dans le panier";
  cell.appendChild(alreadyAdded);
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
const cashRegisterStorageKey = "association-sale-tool.cash-register-id";

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
  const citySelect = root.querySelector("[data-city-select]");

  if (!postalCodeInput || !cityInput || (!cityOptions && !citySelect)) {
    return;
  }

  const canEditCity = !cityInput.disabled;
  let lastPrefilledCity = "";

  function normalizeCityForMatching(value) {
    return (value || "")
      .trim()
      .toUpperCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/[^A-Z0-9]+/g, " ")
      .split(/\s+/)
      .filter(Boolean)
      .map((token) => {
        if (token === "ST") {
          return "SAINT";
        }

        if (token === "STE") {
          return "SAINTE";
        }

        return token;
      })
      .join(" ");
  }

  function findCanonicalCity(cities, value) {
    const trimmed = (value || "").trim();
    const exactMatch = cities.find((city) => city === trimmed);
    if (exactMatch) {
      return exactMatch;
    }

    const normalized = normalizeCityForMatching(trimmed);
    if (!normalized) {
      return "";
    }

    return cities.find((city) => normalizeCityForMatching(city) === normalized) || "";
  }

  function updateCitySelect(cities) {
    if (!citySelect || !canEditCity) {
      return;
    }

    const currentCity = cityInput.value || citySelect.value;
    citySelect.replaceChildren();

    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = "Sélectionnez une ville";
    citySelect.appendChild(placeholder);

    for (const city of cities) {
      const option = document.createElement("option");
      option.value = city;
      option.textContent = city;
      citySelect.appendChild(option);
    }

    const selectedCity = cities.length === 1
      ? cities[0]
      : findCanonicalCity(cities, currentCity);
    citySelect.value = selectedCity;
    cityInput.value = selectedCity;
    cityInput.hidden = true;
    cityInput.disabled = true;
    citySelect.hidden = false;
    citySelect.disabled = false;
    lastPrefilledCity = selectedCity;
  }

  function showFreeTextCity() {
    if (citySelect) {
      if (!cityInput.value && citySelect.value) {
        cityInput.value = citySelect.value;
      }

      citySelect.hidden = true;
      citySelect.disabled = true;
    }

    cityInput.hidden = false;
    cityInput.disabled = !canEditCity;
  }

  if (citySelect) {
    citySelect.addEventListener("change", () => {
      cityInput.value = citySelect.value;
      lastPrefilledCity = citySelect.value;
      cityInput.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }

  async function updateCityOptions() {
    const postalCode = postalCodeInput.value.trim();
    cityOptions?.replaceChildren();

    if (!/^38\d{3}$/.test(postalCode)) {
      if (cityInput.value === lastPrefilledCity) {
        cityInput.value = "";
      }

      lastPrefilledCity = "";
      showFreeTextCity();
      return;
    }

    const cityMap = await loadCheckoutCitiesByPostalCode();
    if (postalCodeInput.value.trim() !== postalCode) {
      return;
    }

    const cities = cityMap[postalCode] || [];
    if (citySelect && cities.length > 0) {
      updateCitySelect(cities);
      return;
    }

    showFreeTextCity();

    if (!cityOptions) {
      return;
    }

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

  if (options.cashRegisterId !== undefined) {
    body.cashRegisterId = options.cashRegisterId;
  }

  if (options.createInvoice !== undefined) {
    body.createInvoice = options.createInvoice;
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
  const resultsFrame = root.querySelector("[data-checkout-results-frame]");
  const resultsBody = root.querySelector("[data-checkout-results]");
  const cartBody = root.querySelector("[data-checkout-cart]");
  const emptyCart = root.querySelector("[data-checkout-empty-cart]");
  const message = root.querySelector("[data-checkout-message]");
  const subtotalElement = root.querySelector("[data-checkout-subtotal]");
  const feeElement = root.querySelector("[data-checkout-fee]");
  const totalElement = root.querySelector("[data-checkout-total]");
  const stickyCountElement = root.querySelector("[data-checkout-sticky-count]");
  const stickyTotalElement = root.querySelector("[data-checkout-sticky-total]");
  const buyerAddressInput = root.querySelector("[data-checkout-buyer-address]");
  const buyerNameInput = root.querySelector("[data-checkout-buyer-name]");
  const buyerPostalCodeInput = root.querySelector("[data-checkout-buyer-postal-code]");
  const buyerCityInput = root.querySelector("[data-checkout-buyer-city]");
  const buyerCitySelect = root.querySelector("[data-checkout-buyer-city-select]");
  const buyerMessage = root.querySelector("[data-checkout-buyer-message]");
  const finalizeButton = root.querySelector("[data-checkout-finalize]");
  const modal = root.querySelector("[data-checkout-modal]");
  const modalBackButton = root.querySelector("[data-checkout-modal-back]");
  const saveSaleButton = root.querySelector("[data-checkout-save-sale]");
  const savePrintButton = root.querySelector("[data-checkout-save-print]");
  const checkoutWorkflow = root.querySelector("[data-checkout-workflow]");
  const cashRegisterSelect = root.querySelector("[data-checkout-cash-register-select]");
  const cashRegisterSaveButton = root.querySelector("[data-checkout-cash-register-save]");
  const cashRegisterMessage = root.querySelector("[data-checkout-cash-register-message]");
  const paymentMethodInputs = root.querySelectorAll("[data-checkout-payment-method]");
  const chequeDetails = root.querySelector("[data-checkout-cheque-details]");
  const chequeNumberInput = root.querySelector("[data-checkout-cheque-number]");
  const chequeBankNameInput = root.querySelector("[data-checkout-cheque-bank-name]");
  const paymentMessage = root.querySelector("[data-checkout-payment-message]");
  const modalCount = root.querySelector("[data-checkout-modal-count]");
  const modalSubtotal = root.querySelector("[data-checkout-modal-subtotal]");
  const modalFee = root.querySelector("[data-checkout-modal-fee]");
  const modalTotal = root.querySelector("[data-checkout-modal-total]");
  const invoice = document.querySelector("[data-checkout-invoice]");
  const invoiceNumber = document.querySelector("[data-checkout-invoice-number]");
  const invoiceIssuedAt = document.querySelector("[data-checkout-invoice-issued-at]");
  const invoiceCustomerSection = document.querySelector("[data-checkout-invoice-customer-section]");
  const invoiceCustomerText = document.querySelector("[data-checkout-invoice-customer-text]");
  const invoiceLines = document.querySelector("[data-checkout-invoice-lines]");
  const invoiceSubtotal = document.querySelector("[data-checkout-invoice-subtotal]");
  const invoiceFee = document.querySelector("[data-checkout-invoice-fee]");
  const invoiceTotalExcludingVat = document.querySelector("[data-checkout-invoice-total-excluding-vat]");
  const invoiceVat = document.querySelector("[data-checkout-invoice-vat]");
  const invoiceTotalIncludingVat = document.querySelector("[data-checkout-invoice-total-including-vat]");

  if (!dataElement || !searchInput || !resultsBody || !cartBody) {
    return;
  }

  const catalog = JSON.parse(dataElement.textContent || "[]");
  const cart = [];
  const conflictItemIds = new Set();
  const touchedPaymentFields = {
    chequeNumber: false,
    chequeBankName: false
  };
  let paymentSubmitAttempted = false;

  function setCashRegisterMessage(text) {
    if (cashRegisterMessage) {
      cashRegisterMessage.textContent = text;
    }
  }

  function getCashRegisterId() {
    return localStorage.getItem(cashRegisterStorageKey)?.trim() || "";
  }

  function getCashRegisterIdNumber() {
    const cashRegisterId = Number.parseInt(getCashRegisterId(), 10);
    return Number.isFinite(cashRegisterId) && cashRegisterId > 0 ? cashRegisterId : null;
  }

  function refreshCashRegisterState() {
    let cashRegisterId = getCashRegisterId();
    if (cashRegisterSelect) {
      cashRegisterSelect.value = cashRegisterId;
      if (cashRegisterId.length > 0 && cashRegisterSelect.value !== cashRegisterId) {
        localStorage.removeItem(cashRegisterStorageKey);
        cashRegisterId = "";
      }
    }

    if (checkoutWorkflow) {
      checkoutWorkflow.hidden = cashRegisterId.length === 0;
    }

    if (cashRegisterId.length === 0) {
      setCashRegisterMessage("Choisissez une caisse avant d'enregistrer une vente.");
      cashRegisterSelect?.focus();
      return;
    }

    const selectedOption = cashRegisterSelect?.selectedOptions?.[0];
    setCashRegisterMessage(`Caisse active : ${selectedOption?.textContent || cashRegisterId}`);
  }

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

  function focusBuyerField(input) {
    if (input === buyerCityInput && buyerCitySelect && !buyerCitySelect.disabled && !buyerCitySelect.hidden) {
      buyerCitySelect.focus();
      return;
    }

    input?.focus();
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
    focusBuyerField(missingFields[0][1]);
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
      touchedPaymentFields.chequeNumber = false;
      touchedPaymentFields.chequeBankName = false;
      paymentSubmitAttempted = false;
    }
  }

  function getMissingPaymentFields() {
    const paymentDetails = getPaymentDetails();
    if (paymentDetails.paymentMethod !== "cheque") {
      return [];
    }

    return [
      {
        name: "chequeNumber",
        value: paymentDetails.chequeNumber,
        input: chequeNumberInput,
        message: "Le numéro de chèque est obligatoire."
      },
      {
        name: "chequeBankName",
        value: paymentDetails.chequeBankName,
        input: chequeBankNameInput,
        message: "Le nom de la banque est obligatoire."
      }
    ].filter(field => field.value.length === 0);
  }

  function validatePaymentDetailField(fieldName) {
    touchedPaymentFields[fieldName] = true;
    const paymentDetails = getPaymentDetails();
    if (paymentDetails.paymentMethod !== "cheque") {
      setPaymentMessage("");
      return true;
    }

    const missingFields = getMissingPaymentFields();
    const currentField = missingFields.find(field => field.name === fieldName);
    if (currentField) {
      setPaymentMessage(currentField.message);
      return false;
    }

    const visibleField = missingFields.find(field =>
      touchedPaymentFields[field.name] || paymentSubmitAttempted);
    setPaymentMessage(visibleField?.message || "");
    return missingFields.length === 0;
  }

  function updatePaymentDetailField(fieldName) {
    if (touchedPaymentFields[fieldName] || paymentSubmitAttempted) {
      validatePaymentDetailField(fieldName);
    }
  }

  function validatePaymentDetails() {
    const paymentDetails = getPaymentDetails();
    if (paymentDetails.paymentMethod !== "cheque") {
      setPaymentMessage("");
      return true;
    }

    paymentSubmitAttempted = true;
    const missingFields = getMissingPaymentFields();
    if (missingFields.length === 0) {
      setPaymentMessage("");
      return true;
    }

    setPaymentMessage("Le numéro de chèque et le nom de la banque sont obligatoires pour un paiement par chèque.");
    missingFields[0].input?.focus();
    return false;
  }

  function getSaleDetails() {
    return {
      cashRegisterId: getCashRegisterIdNumber(),
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

  function formatInvoiceCustomerDetails(saleDetails) {
    const location = [saleDetails.buyerPostalCode, saleDetails.buyerCity]
      .filter(value => value.length > 0)
      .join(" ");

    return [saleDetails.buyerName, saleDetails.buyerAddress, location]
      .filter(value => value.length > 0)
      .join("\n");
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

  function formatInvoiceIssueDate(value) {
    return value ? new Date(value).toLocaleDateString("fr-FR") : "";
  }

  function renderInvoice(payload, saleDetails) {
    if (!invoice || !invoiceLines) {
      return;
    }

    if (invoiceNumber) {
      invoiceNumber.textContent = payload.invoiceNumber || "";
    }

    if (invoiceIssuedAt) {
      invoiceIssuedAt.textContent = formatInvoiceIssueDate(payload.invoiceIssuedAt);
    }

    const customerDetails = formatInvoiceCustomerDetails(saleDetails);
    if (invoiceCustomerSection) {
      invoiceCustomerSection.hidden = customerDetails.length === 0;
    }

    if (invoiceCustomerText) {
      invoiceCustomerText.textContent = customerDetails;
    }

    invoiceLines.replaceChildren();
    for (const item of cart) {
      const row = document.createElement("tr");
      row.appendChild(createCheckoutCell("1"));
      row.appendChild(createCheckoutCell(`${item.reference} - ${item.title}`));
      row.appendChild(createCheckoutCell(formatCheckoutPrice(item.sellerPriceCents)));
      row.appendChild(createCheckoutCell(formatCheckoutPrice(item.sellerPriceCents)));
      invoiceLines.appendChild(row);
    }

    if (payload.totals) {
      if (invoiceSubtotal) {
        invoiceSubtotal.textContent = formatCheckoutPrice(payload.totals.subtotalSellerPriceCents);
      }

      if (invoiceFee) {
        invoiceFee.textContent = formatCheckoutPrice(payload.totals.checkoutFeeCents);
      }

      if (invoiceTotalExcludingVat) {
        invoiceTotalExcludingVat.textContent = formatCheckoutPrice(payload.totals.totalPaidCents);
      }

      if (invoiceVat) {
        invoiceVat.textContent = formatCheckoutPrice(0);
      }

      if (invoiceTotalIncludingVat) {
        invoiceTotalIncludingVat.textContent = formatCheckoutPrice(payload.totals.totalPaidCents);
      }
    }
  }

  function printInvoiceAndReload() {
    document.body.classList.add("checkout-printing");
    window.onafterprint = () => {
      window.location.reload();
    };
    window.print();
  }

  function clearCheckoutResults() {
    searchInput.value = "";
    resultsBody.replaceChildren();

    if (resultsFrame) {
      resultsFrame.hidden = true;
    }
  }

  function renderResults() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    resultsBody.replaceChildren();

    if (!query) {
      clearCheckoutResults();
      return;
    }

    if (resultsFrame) {
      resultsFrame.hidden = false;
    }

    const matches = catalog.filter(item =>
      `${item.reference} ${item.title}`.toLocaleLowerCase("fr-FR").includes(query));

    if (matches.length === 0) {
      resultsBody.appendChild(createCheckoutEmptyRow("Aucun article ne correspond à cette recherche.", 4));
      return;
    }

    for (const item of matches) {
      const row = document.createElement("tr");
      const isAlreadyInCart = cart.some(cartItem => cartItem.id === item.id);
      row.className = "checkout-result-row";
      row.classList.toggle("checkout-result-row-disabled", isAlreadyInCart);
      if (isAlreadyInCart) {
        row.setAttribute("aria-disabled", "true");
      } else {
        row.dataset.checkoutItemId = item.id;
      }

      row.appendChild(createCheckoutCell(item.reference));
      row.appendChild(createCheckoutCell(item.title));
      row.appendChild(createCheckoutCell(formatCheckoutPrice(item.sellerPriceCents)));
      row.appendChild(isAlreadyInCart
        ? createCheckoutAlreadyAddedCell()
        : createCheckoutActionCell(item.id));
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

    if (stickyCountElement) {
      stickyCountElement.textContent = cart.length === 1 ? "1 article" : `${cart.length} articles`;
    }

    if (stickyTotalElement) {
      stickyTotalElement.textContent = formatCheckoutPrice(total);
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
  chequeNumberInput?.addEventListener("blur", () => validatePaymentDetailField("chequeNumber"));
  chequeNumberInput?.addEventListener("input", () => updatePaymentDetailField("chequeNumber"));
  chequeBankNameInput?.addEventListener("blur", () => validatePaymentDetailField("chequeBankName"));
  chequeBankNameInput?.addEventListener("input", () => updatePaymentDetailField("chequeBankName"));
  resultsBody.addEventListener("click", event => {
    const row = event.target.closest("[data-checkout-item-id]");
    if (!row) {
      return;
    }

    const item = findCatalogItem(row.dataset.checkoutItemId);
    if (!item) {
      return;
    }

    cart.push(item);
    setMessage("");
    conflictItemIds.delete(Number(item.id));
    clearCheckoutResults();
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

    if (getCashRegisterId().length === 0) {
      refreshCashRegisterState();
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

    if (getCashRegisterId().length === 0) {
      refreshCashRegisterState();
      return;
    }

    const { response, payload } = await postCheckoutCart("/api/ventes", cart, {
      ...saleDetails,
      createInvoice: true
    });
    if (response.status === 201 && payload.saved) {
      renderInvoice(payload, saleDetails);
      printInvoiceAndReload();
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
  cashRegisterSaveButton?.addEventListener("click", () => {
    const selectedCashRegisterId = cashRegisterSelect?.value.trim() || "";
    if (selectedCashRegisterId.length === 0) {
      setCashRegisterMessage("Choisissez une caisse.");
      cashRegisterSelect?.focus();
      return;
    }

    localStorage.setItem(cashRegisterStorageKey, selectedCashRegisterId);
    refreshCashRegisterState();
  });
  refreshCashRegisterState();
}

function initializeSellerDepositFlow(root) {
  const autoOpen = root.hasAttribute("data-seller-deposit-auto-open");
  const initialStep = root.dataset.sellerDepositInitialStep || "fees";
  const dialog = root.querySelector("[data-seller-deposit-dialog]");
  const feeStep = root.querySelector("[data-seller-deposit-fee-step]");
  const printStep = root.querySelector("[data-seller-deposit-print-step]");
  const validateStep = root.querySelector("[data-seller-deposit-validate-step]");
  const printButton = root.querySelector("[data-seller-deposit-print]");
  const backToFeesButton = root.querySelector("[data-seller-deposit-back-to-fees]");
  const backButton = root.querySelector("[data-seller-deposit-back]");
  const closeButtons = root.querySelectorAll("[data-seller-deposit-close]");
  const signatureConfirmation = root.querySelector("[data-seller-deposit-signature-confirmation]");
  const submitButton = root.querySelector("[data-seller-deposit-submit]");

  if (!dialog || !feeStep || !printStep || !validateStep) {
    return;
  }

  function updateSubmitState() {
    if (!submitButton || !signatureConfirmation) {
      return;
    }

    submitButton.disabled = !signatureConfirmation.checked;
  }

  function showStep(step) {
    const normalizedStep = ["fees", "print", "validate"].includes(step) ? step : "fees";
    const isFeeStep = normalizedStep === "fees";
    const isPrintStep = normalizedStep === "print";
    const isValidateStep = normalizedStep === "validate";
    feeStep.hidden = !isFeeStep;
    printStep.hidden = !isPrintStep;
    validateStep.hidden = !isValidateStep;

    root.querySelectorAll("[data-seller-deposit-step-indicator]").forEach(indicator => {
      const isCurrent = indicator.dataset.sellerDepositStepIndicator === normalizedStep;
      if (isCurrent) {
        indicator.setAttribute("aria-current", "step");
      } else {
        indicator.removeAttribute("aria-current");
      }
    });

    updateSubmitState();
    const focusTarget = isFeeStep
      ? feeStep.querySelector("[name='DepositFeeInput.FeePaid']") || closeButtons[0]
      : isValidateStep
      ? signatureConfirmation || backButton || submitButton
      : printButton || closeButtons[0];
    focusTarget?.focus();
  }

  printButton?.addEventListener("click", () => {
    if (printButton.dataset.sellerDepositPrintUrl) {
      window.open(printButton.dataset.sellerDepositPrintUrl, "_blank", "noopener");
    }

    showStep("validate");
  });
  backToFeesButton?.addEventListener("click", () => showStep("fees"));
  backButton?.addEventListener("click", () => showStep("print"));
  signatureConfirmation?.addEventListener("change", updateSubmitState);
  closeButtons.forEach(button => button.addEventListener("click", () => dialog.close()));
  dialog.addEventListener("close", () => {
    if (signatureConfirmation) {
      signatureConfirmation.checked = false;
    }

    updateSubmitState();
  });
  updateSubmitState();

  if (autoOpen) {
    dialog.showModal();
    showStep(initialStep);
  }
}

function initializeDepositContractAutoPrint() {
  const closeButton = document.querySelector("[data-deposit-contract-close]");
  closeButton?.addEventListener("click", () => window.close());

  window.onafterprint = () => {
    window.close();
  };

  requestAnimationFrame(() => {
    window.print();
  });
}

document.querySelectorAll("[data-postal-city-root]").forEach(initializePostalCityRoot);
document.querySelectorAll("[data-whole-euro-input]").forEach(initializeWholeEuroInput);
document.querySelectorAll("[data-item-registration-count]").forEach(updateItemRegistrationCount);
document.querySelectorAll("[data-checkout-root]").forEach(initializeCheckout);
document.querySelectorAll("[data-seller-deposit-flow]").forEach(initializeSellerDepositFlow);
document.querySelectorAll("[data-deposit-contract-auto-print]").forEach(initializeDepositContractAutoPrint);

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
  const promptMessage = root.querySelector("[data-item-correction-prompt-message]");
  const emptyMessage = root.querySelector("[data-item-correction-empty-message]");

  if (!searchInput || rows.length === 0) {
    return;
  }

  function renderRows() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    let visibleRows = 0;

    if (!query) {
      for (const row of rows) {
        row.hidden = true;
      }

      if (promptMessage) {
        promptMessage.hidden = false;
      }

      if (emptyMessage) {
        emptyMessage.hidden = true;
      }

      return;
    }

    if (promptMessage) {
      promptMessage.hidden = true;
    }

    for (const row of rows) {
      const filterText = row.dataset.itemCorrectionFilterText || "";
      const isVisible = filterText.includes(query);
      row.hidden = !isVisible;

      if (isVisible) {
        visibleRows++;
      }
    }

    if (emptyMessage) {
      emptyMessage.hidden = visibleRows > 0;
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

function initializeSalesViewInvoicePrint() {
  const printMarker = document.querySelector("[data-sales-view-print-on-load]");
  if (!printMarker) {
    return;
  }

  document.body.classList.add("checkout-printing");
  window.onafterprint = () => {
    document.body.classList.remove("checkout-printing");
    window.history.replaceState(null, "", window.location.pathname);
  };
  window.print();
}

initializeSalesViewInvoicePrint();

const dataListTextCollator = new Intl.Collator("fr-FR", {
  numeric: true,
  sensitivity: "base"
});

function getDataListSortValue(row, columnIndex, sortType) {
  const cell = row.cells[columnIndex];
  const rawValue = cell?.dataset.dataListSortValue ?? cell?.textContent ?? "";
  const value = rawValue.trim();

  if (sortType === "number") {
    const parsed = Number(value.replace(",", "."));
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return value.toLocaleLowerCase("fr-FR");
}

function compareDataListSortValues(left, right, sortType) {
  if (sortType === "number") {
    return left - right;
  }

  return dataListTextCollator.compare(left, right);
}

function initializeDataListSorting(root, renderRows) {
  root.querySelectorAll("[data-data-list-sort]").forEach(button => {
    button.addEventListener("click", () => {
      const header = button.closest("th");
      const table = button.closest("[data-data-list-table]");
      const tbody = table?.tBodies?.[0];

      if (!header || !table || !tbody) {
        return;
      }

      const columnIndex = header.cellIndex;
      const sortType = button.dataset.dataListSortType || "text";
      const direction = header.getAttribute("aria-sort") === "ascending"
        ? "descending"
        : "ascending";
      const directionMultiplier = direction === "ascending" ? 1 : -1;
      const rows = Array.from(tbody.querySelectorAll("[data-data-list-row]"))
        .map((row, index) => ({ row, index }));

      table.querySelectorAll("th[aria-sort]").forEach(sortHeader => {
        sortHeader.setAttribute("aria-sort", sortHeader === header ? direction : "none");
      });

      rows
        .sort((leftRow, rightRow) => {
          const left = getDataListSortValue(leftRow.row, columnIndex, sortType);
          const right = getDataListSortValue(rightRow.row, columnIndex, sortType);
          const comparison = compareDataListSortValues(left, right, sortType);

          if (comparison !== 0) {
            return comparison * directionMultiplier;
          }

          return leftRow.index - rightRow.index;
        })
        .forEach(({ row }) => tbody.appendChild(row));

      renderRows();
    });
  });
}

function initializeDataList(root) {
  const searchInput = root.querySelector("[data-data-list-search]");
  const statusFilter = root.querySelector("[data-data-list-status-filter]");
  const rows = Array.from(root.querySelectorAll("[data-data-list-row]"));
  const emptyMessage = root.querySelector("[data-data-list-empty-message]");

  if (!searchInput || rows.length === 0) {
    return;
  }

  function renderRows() {
    const query = searchInput.value.trim().toLocaleLowerCase("fr-FR");
    const selectedStatus = statusFilter?.value || "";
    let visibleRows = 0;

    for (const row of rows) {
      const filterText = row.dataset.dataListFilterText || "";
      const rowStatus = row.dataset.dataListStatus || "";
      const matchesText = !query || filterText.includes(query);
      const matchesStatus = !selectedStatus || rowStatus === selectedStatus;
      const isVisible = matchesText && matchesStatus;
      row.hidden = !isVisible;

      if (isVisible) {
        visibleRows++;
      }
    }

    if (emptyMessage) {
      emptyMessage.hidden = visibleRows > 0 || (!query && !selectedStatus);
    }
  }

  searchInput.addEventListener("input", renderRows);
  statusFilter?.addEventListener("change", renderRows);
  initializeDataListSorting(root, renderRows);
  renderRows();
}

document.querySelectorAll("[data-data-list-root]").forEach(initializeDataList);

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
