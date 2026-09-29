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

function extract(pattern, description) {
  const match = siteScript.match(pattern);
  assert.ok(match, `Missing ${description}`);
  return match[0];
}

test("the invoice confirmation is a hidden dialog with explicit choices, printing last", () => {
  const dialog = checkoutPage.match(/<div class="checkout-modal checkout-invoice-confirmation"[\s\S]*?<\/div>\s*<\/div>\s*<\/div>/);
  assert.ok(dialog, "Missing the invoice confirmation dialog");
  assert.match(dialog[0], /data-checkout-invoice-confirmation hidden role="dialog" aria-modal="true"/);
  assert.match(dialog[0], /Une adresse est saisie\. Le client souhaite-t-il une facture \?/);

  const buttons = [...dialog[0].matchAll(/<button[^>]*(data-checkout-invoice-confirmation-[a-z-]+)[^>]*>\s*([^<]+?)\s*<\/button>/g)]
    .map(match => [match[1], match[2]]);
  assert.deepEqual(buttons, [
    ["data-checkout-invoice-confirmation-back", "Retour"],
    ["data-checkout-invoice-confirmation-without-invoice", "Valider sans facture"],
    ["data-checkout-invoice-confirmation-print", "Valider et imprimer la facture"]
  ]);
  assert.match(dialog[0], /class="button-link button-link-solid" data-checkout-invoice-confirmation-print/);
  assert.doesNotMatch(dialog[0], /class="button-link button-link-solid" data-checkout-invoice-confirmation-without-invoice/);
});

test("saving without printing asks for confirmation only when an address is typed and the sale is ready", () => {
  const handler = extract(
    /  saveSaleButton\?\.addEventListener\("click", \(\) => \{[\s\S]*?\r?\n  \}\);\r?\n/,
    "the save without printing click handler");
  assert.match(
    handler,
    /if \(!isSaleReadyToSave\(\)\) \{\s*return;\s*\}\s*if \(getBuyerAddress\(\)\.length > 0\) \{\s*showInvoiceConfirmation\(\);\s*return;\s*\}\s*submitSaleWithoutInvoice\(\);/);
});

test("both save paths run the same checks before saving", () => {
  const readiness = extract(/  function isSaleReadyToSave\(\) \{[\s\S]*?\r?\n  \}\r?\n/, "isSaleReadyToSave");
  assert.match(readiness, /validateBuyerDetails\(\)/);
  assert.match(readiness, /validatePaymentDetails\(\)/);
  assert.match(readiness, /getCashRegisterId\(\)\.length === 0/);

  for (const submit of ["submitSaleWithoutInvoice", "submitSaleWithInvoice"]) {
    const body = extract(new RegExp(`  async function ${submit}\\(\\) \\{[\\s\\S]*?\\r?\\n  \\}\\r?\\n`), submit);
    assert.match(body, /if \(!isSaleReadyToSave\(\)\) \{\s*return;\s*\}/, `${submit} must check the sale first`);
  }
});

test("the confirmation choices save, print or go back to the payment step", () => {
  assert.match(
    siteScript,
    /invoiceConfirmationPrintButton\?\.addEventListener\("click", \(\) => \{\s*hideInvoiceConfirmation\(\);\s*submitSaleWithInvoice\(\);\s*\}\);/);
  assert.match(
    siteScript,
    /invoiceConfirmationWithoutInvoiceButton\?\.addEventListener\("click", \(\) => \{\s*hideInvoiceConfirmation\(\);\s*submitSaleWithoutInvoice\(\);\s*\}\);/);
  assert.match(
    siteScript,
    /invoiceConfirmationBackButton\?\.addEventListener\("click", \(\) => \{\s*hideInvoiceConfirmation\(\);\s*saveSaleButton\?\.focus\(\);\s*\}\);/);
  assert.match(siteScript, /savePrintButton\?\.addEventListener\("click", submitSaleWithInvoice\);/);
});

test("the confirmation focuses printing, so a reflex Enter prints the invoice", () => {
  const show = extract(/  function showInvoiceConfirmation\(\) \{[\s\S]*?\r?\n  \}\r?\n/, "showInvoiceConfirmation");
  assert.match(show, /invoiceConfirmation\.hidden = false;/);
  assert.match(show, /invoiceConfirmationPrintButton\?\.focus\(\);/);
});
