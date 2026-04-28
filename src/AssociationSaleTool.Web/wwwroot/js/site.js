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

  if (!dataElement || !searchInput || !resultsBody || !cartBody) {
    return;
  }

  const catalog = JSON.parse(dataElement.textContent || "[]");
  const cart = [];

  function findCatalogItem(itemId) {
    return catalog.find(item => String(item.id) === String(itemId));
  }

  function setMessage(text) {
    if (message) {
      message.textContent = text;
    }
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
  }

  searchInput.addEventListener("input", renderResults);

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
    renderCart();
  });

  cartBody.addEventListener("click", event => {
    const button = event.target.closest("[data-checkout-remove]");
    if (!button) {
      return;
    }

    const index = cart.findIndex(item => String(item.id) === button.dataset.checkoutRemove);
    if (index >= 0) {
      cart.splice(index, 1);
      setMessage("");
      renderCart();
    }
  });

  renderResults();
  renderCart();
}

document.querySelectorAll("[data-checkout-root]").forEach(initializeCheckout);
