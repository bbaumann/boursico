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

test("checkout station code is persisted locally and sent with sale requests", () => {
  assert.match(siteScript, /const checkoutCodeStorageKey = "association-sale-tool\.checkout-code";/);
  assert.match(siteScript, /localStorage\.getItem\(checkoutCodeStorageKey\)/);
  assert.match(siteScript, /localStorage\.setItem\(checkoutCodeStorageKey,\s*normalizedCode\)/);
  assert.match(siteScript, /body\.checkoutCode = options\.checkoutCode;/);
  assert.match(siteScript, /checkoutCode: getCheckoutCode\(\)/);
});

test("checkout page renders a French checkout code setup block", () => {
  assert.match(checkoutPage, /data-checkout-code-setup/);
  assert.match(checkoutPage, /data-checkout-code-input/);
  assert.match(checkoutPage, /Nom de la caisse/);
  assert.match(checkoutPage, /Enregistrer la caisse/);
});
