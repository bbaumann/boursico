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
