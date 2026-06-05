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

test("checkout buyer city uses the seller-style postal city dropdown", () => {
  assert.match(checkoutPage, /data-city-select/);
  assert.match(checkoutPage, /data-checkout-buyer-city-select/);
  assert.doesNotMatch(checkoutPage, /data-checkout-buyer-city-options/);
});

test("checkout buyer validation focuses the visible city dropdown when it is active", () => {
  assert.match(
    siteScript,
    /const buyerCitySelect = root\.querySelector\("\[data-checkout-buyer-city-select\]"\);/);
  assert.match(
    siteScript,
    /input === buyerCityInput && buyerCitySelect && !buyerCitySelect\.disabled && !buyerCitySelect\.hidden/);
  assert.match(siteScript, /buyerCitySelect\.focus\(\);/);
});
