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
function loadSiteScript() {
  const document = {
    addEventListener() {},
    querySelector: () => null,
    querySelectorAll: () => []
  };
  const context = vm.createContext({ document, window: {}, Intl });
  vm.runInContext(siteScript, context);
  return context;
}

function fakeRow(...values) {
  const inputs = values.map(value => ({ value }));
  return { querySelectorAll: selector => (selector === "input" ? inputs : []) };
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
  }
});
