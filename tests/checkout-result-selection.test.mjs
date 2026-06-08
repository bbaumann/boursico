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

test("checkout results table is hidden until a search is active", () => {
  assert.match(
    checkoutPage,
    /<div class="checkout-results-frame"[^>]*data-checkout-results-frame[^>]*hidden>/);
});

test("checkout item selection clears the search and hides results", () => {
  assert.match(
    siteScript,
    /const resultsFrame = root\.querySelector\("\[data-checkout-results-frame\]"\);/);
  assert.match(siteScript, /function clearCheckoutResults\(\) \{/);
  assert.match(siteScript, /searchInput\.value = "";/);
  assert.match(siteScript, /resultsFrame\.hidden = true;/);
  assert.match(siteScript, /clearCheckoutResults\(\);\s*renderCart\(\);/);
});
