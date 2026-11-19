import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

const checkoutPage = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Checkout", "Index.cshtml"),
  "utf8");

function extractFunction(name, parameters = "") {
  const match = siteScript.match(new RegExp(`  (?:async )?function ${name}\\(${parameters}\\) \\{[\\s\\S]*?\\r?\\n  \\}\\r?\\n`));
  assert.ok(match, `Missing function ${name}`);
  return match[0];
}

function loadApplyCurrentItems(cart, catalog) {
  const source = extractFunction("applyCurrentItems", "currentItems");
  return new Function("cart", "catalog", `${source}\nreturn applyCurrentItems;`)(cart, catalog);
}

test("current items replace outdated cart and catalog entries and report what changed", () => {
  const puzzle = { id: 1, reference: "BB-001", title: "Puzzle", sellerPriceCents: 500 };
  const truck = { id: 2, reference: "BB-002", title: "Camion", sellerPriceCents: 1000 };
  const cart = [puzzle, truck];
  const catalog = [puzzle, truck, { id: 3, reference: "BB-003", title: "Ballon", sellerPriceCents: 200 }];
  const applyCurrentItems = loadApplyCurrentItems(cart, catalog);

  const repricedPuzzle = { id: 1, reference: "BB-001", title: "Puzzle", sellerPriceCents: 300 };
  const changes = applyCurrentItems([repricedPuzzle, { ...truck }]);

  assert.deepEqual(changes, [{ before: puzzle, after: repricedPuzzle }]);
  assert.equal(cart[0], repricedPuzzle);
  assert.equal(catalog[0], repricedPuzzle);
  assert.equal(cart.length, 2);
});

test("a renamed item is reported even when its price did not change", () => {
  const puzzle = { id: 1, reference: "BB-001", title: "Puzzle", sellerPriceCents: 500 };
  const applyCurrentItems = loadApplyCurrentItems([puzzle], [puzzle]);

  const changes = applyCurrentItems([{ ...puzzle, title: "Puzzle 100 pièces" }]);

  assert.equal(changes.length, 1);
});

test("cart items missing from the answer are left for the conflict handling", () => {
  const puzzle = { id: 1, reference: "BB-001", title: "Puzzle", sellerPriceCents: 500 };
  const cart = [puzzle];
  const applyCurrentItems = loadApplyCurrentItems(cart, [puzzle]);

  assert.deepEqual(applyCurrentItems([]), []);
  assert.equal(cart[0], puzzle);
});

test("refreshing the catalog keeps the sale being typed and flags unavailable cart items", () => {
  const refresh = extractFunction("refreshCatalog");
  assert.match(refresh, /fetch\("\/api\/caisse\/articles"/);
  assert.match(refresh, /applyCurrentItems\(currentItems\)/);
  assert.match(refresh, /setConflictItems\(unavailableItemIds\)/);
  assert.match(refresh, /showItemChanges\(changes\)/);
  assert.doesNotMatch(refresh, /location\.reload|resetCompletedSale|cart\.splice/);
});

test("finalizing uses the server's current items and shows the changes before the payment", () => {
  assert.match(siteScript, /const changes = applyCurrentItems\(payload\.items \|\| \[\]\);/);
  assert.match(
    siteScript,
    /showItemChanges\(changes, \(\) => showModal\(payload\.totals, payload\.itemCount\)\);/);
});

test("the checkout page offers the refresh button and the changed items dialog in French", () => {
  assert.match(checkoutPage, /data-checkout-refresh-catalog>Actualiser les articles</);
  assert.match(checkoutPage, /data-checkout-item-changes hidden role="dialog" aria-modal="true"/);
  assert.match(checkoutPage, /Articles modifiés/);
  assert.match(checkoutPage, /data-checkout-item-changes-close>J'ai compris</);
});
