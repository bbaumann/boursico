import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";
import vm from "node:vm";

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

// Runs the real site.js against a document stub that has no matching
// elements, so only its function declarations are exercised.
// elementsBySelector lets a test hand back elements for selectors the functions query later.
function loadSiteScript(elementsBySelector = {}) {
  const document = {
    addEventListener() {},
    querySelector: () => null,
    querySelectorAll: selector => elementsBySelector[selector] ?? []
  };
  const context = vm.createContext({ document, window: {}, Intl });
  vm.runInContext(siteScript, context);
  return context;
}

// Values are visible inputs; { hidden: value } adds a hidden input, like the __Invariant
// field ASP.NET renders next to every type="number" input.
function fakeRow(...values) {
  const inputs = values.map(value =>
    typeof value === "object" ? { type: "hidden", value: value.hidden } : { type: "text", value });
  return {
    querySelectorAll: selector => {
      if (selector === "input") return inputs;
      if (selector === 'input:not([type="hidden"])') return inputs.filter(input => input.type !== "hidden");
      return [];
    }
  };
}

function fakeForm(existingCount, rows) {
  const counter = { dataset: { existingItemCount: String(existingCount) }, textContent: "" };
  const tbody = { querySelectorAll: selector => (selector === "tr" ? rows : []) };
  const form = {
    querySelector: selector => {
      if (selector === "[data-item-registration-count]") return counter;
      if (selector === "[data-item-registration-table] tbody") return tbody;
      return null;
    }
  };
  counter.closest = selector => (selector === "form" ? form : null);
  return { form, counter };
}

test("item count label uses singular and plural forms", () => {
  const { formatItemRegistrationCount } = loadSiteScript();
  assert.equal(formatItemRegistrationCount(0, 0), "0 article");
  assert.equal(formatItemRegistrationCount(0, 1), "1 article");
  assert.equal(formatItemRegistrationCount(0, 3), "3 articles");
  assert.equal(formatItemRegistrationCount(11, 1), "12 articles (dont 1 nouveau)");
  assert.equal(formatItemRegistrationCount(11, 3), "14 articles (dont 3 nouveaux)");
});

test("item count label omits the new-item detail when it adds nothing", () => {
  const { formatItemRegistrationCount } = loadSiteScript();
  assert.equal(formatItemRegistrationCount(14, 0), "14 articles");
  assert.equal(formatItemRegistrationCount(0, 5), "5 articles");
});

test("item counter counts rows with a description or a price, not empty ones", () => {
  const { updateItemRegistrationCount } = loadSiteScript();
  const { counter } = fakeForm(11, [
    fakeRow("Puzzle", "5"),
    fakeRow("Livre", ""),
    fakeRow("", "3"),
    fakeRow("  ", ""),
    fakeRow("", "")
  ]);

  updateItemRegistrationCount(counter);

  assert.equal(counter.textContent, "14 articles (dont 3 nouveaux)");
});

test("item counter ignores hidden inputs ASP.NET adds to number fields", () => {
  const { updateItemRegistrationCount } = loadSiteScript();
  const invariant = { hidden: "Input.Rows[0].SellerPrice" };
  const { counter } = fakeForm(0, [
    fakeRow("Puzzle", "5", invariant),
    fakeRow("", "", invariant),
    fakeRow("", "", invariant)
  ]);

  updateItemRegistrationCount(counter);

  assert.equal(counter.textContent, "1 article");
});

test("item counter updates the Articles heading badge and its new-item detail", () => {
  const total = { textContent: "11" };
  const detail = { textContent: "", hidden: true };
  const { updateItemRegistrationCount } = loadSiteScript({
    "[data-item-count-total]": [total],
    "[data-item-count-new]": [detail]
  });

  const typing = fakeForm(11, [fakeRow("Puzzle", "5"), fakeRow("Livre", "2"), fakeRow("", "")]);
  updateItemRegistrationCount(typing.counter);
  assert.equal(total.textContent, "13");
  assert.equal(detail.textContent, "dont 2 nouveaux");
  assert.equal(detail.hidden, false);

  const idle = fakeForm(11, [fakeRow("", "")]);
  updateItemRegistrationCount(idle.counter);
  assert.equal(total.textContent, "11");
  assert.equal(detail.textContent, "");
  assert.equal(detail.hidden, true);
});

test("seller page shows the item count in the Articles heading and the deposit dialog", () => {
  const content = readFileSync(
    join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Details.cshtml"),
    "utf8");

  const heading = content.match(/<h2 class="seller-items-heading">[\s\S]*?<\/h2>/);
  assert.ok(heading, "the Articles heading is missing its count badge");
  assert.match(heading[0], /data-item-count-total>@Model\.Seller\.Items\.Count</);
  assert.match(heading[0], /data-item-count-new hidden/);

  const dialog = content.match(/<dialog id="deposit-finalization-dialog"[\s\S]*?<\/dialog>/);
  assert.ok(dialog, "the deposit finalization dialog is missing");
  assert.match(dialog[0], /data-seller-deposit-item-count/);
  assert.match(dialog[0], /articles déposés/);
});

test("item counter is refreshed on typing and after adding a row", () => {
  assert.match(siteScript, /document\.addEventListener\("input",[\s\S]*?refreshItemRegistrationCount\(event\.target\)/);
  assert.match(siteScript, /tbody\.appendChild\(row\);\s*refreshItemRegistrationCount\(form\);/);
  assert.match(siteScript, /document\.querySelectorAll\("\[data-item-registration-count\]"\)\.forEach\(updateItemRegistrationCount\);/);
});

test("both item registration forms render the counter next to the add-row button", () => {
  const pages = {
    "Sellers/Details.cshtml": /data-existing-item-count="@Model\.Seller\.Items\.Count"/,
    "Sellers/Create.cshtml": /data-existing-item-count="0"/
  };

  for (const [page, existingCountPattern] of Object.entries(pages)) {
    const content = readFileSync(
      join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", ...page.split("/")),
      "utf8");
    const actions = content.match(/<div class="item-registration-actions">[\s\S]*?<\/div>/);
    assert.ok(actions, `${page} is missing the item registration actions`);
    assert.match(actions[0], /data-add-item-row/);
    assert.match(actions[0], /data-item-registration-count/);
    assert.match(actions[0], existingCountPattern);
    assert.match(actions[0], /aria-live="polite"/);
    assert.match(actions[0], /item-registration-count-pill/);
  }
});
