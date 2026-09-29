import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

test("checkout cheque details are validated when focus leaves the fields", () => {
  assert.match(
    siteScript,
    /chequeNumberInput\?\.addEventListener\("blur",\s*\(\) => validatePaymentDetailField\("chequeNumber"\)\);/);
  assert.match(
    siteScript,
    /chequeBankNameInput\?\.addEventListener\("blur",\s*\(\) => validatePaymentDetailField\("chequeBankName"\)\);/);

  assert.doesNotMatch(
    siteScript,
    /chequeNumberInput\?\.addEventListener\("input",\s*validatePaymentDetails\);/);
  assert.doesNotMatch(
    siteScript,
    /chequeBankNameInput\?\.addEventListener\("input",\s*validatePaymentDetails\);/);
  assert.doesNotMatch(
    siteScript,
    /chequeNumberInput\?\.addEventListener\("blur",\s*validatePaymentDetails\);/);
  assert.doesNotMatch(
    siteScript,
    /chequeBankNameInput\?\.addEventListener\("blur",\s*validatePaymentDetails\);/);
});

test("cheque bank name reads as a text field with suggestions, not a drop-down list", () => {
  const checkoutPage = readFileSync(
    join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Checkout", "Index.cshtml"),
    "utf8");
  const css = readFileSync(
    join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"),
    "utf8");

  const bankInput = checkoutPage.match(/<input id="checkout-cheque-bank-name"[\s\S]*?\/>/);
  assert.ok(bankInput, "Missing the cheque bank name input");
  assert.match(bankInput[0], /list="checkout-cheque-bank-options"/);
  assert.match(bankInput[0], /placeholder="Tapez le nom de la banque"/);

  // Chromium draws a drop-down arrow on inputs bound to a datalist, which made testers
  // believe they could only pick from the list.
  assert.match(
    css,
    /\[data-checkout-cheque-bank-name\]::-webkit-calendar-picker-indicator\s*\{\s*display:\s*none\s*!important;\s*\}/);
});
