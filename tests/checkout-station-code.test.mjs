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

test("checkout cash register is persisted locally and sent with sale requests", () => {
  assert.match(siteScript, /const cashRegisterStorageKey = "association-sale-tool\.cash-register-id";/);
  assert.match(siteScript, /localStorage\.getItem\(cashRegisterStorageKey\)/);
  assert.match(siteScript, /localStorage\.removeItem\(cashRegisterStorageKey\)/);
  assert.match(siteScript, /localStorage\.setItem\(cashRegisterStorageKey,\s*selectedCashRegisterId\)/);
  assert.match(siteScript, /body\.cashRegisterId = options\.cashRegisterId;/);
  assert.match(siteScript, /cashRegisterId: getCashRegisterIdNumber\(\)/);
});

test("checkout page renders a French cash register setup block", () => {
  assert.match(checkoutPage, /data-checkout-cash-register-setup/);
  assert.match(checkoutPage, /data-checkout-cash-register-select/);
  assert.match(checkoutPage, /Choisir une caisse/);
  assert.match(checkoutPage, /Utiliser cette caisse/);
});
