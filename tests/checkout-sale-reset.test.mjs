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

function extractFunction(name) {
  const match = siteScript.match(new RegExp(`  (?:async )?function ${name}\\(\\) \\{[\\s\\S]*?\\r?\\n  \\}\\r?\\n`));
  assert.ok(match, `Missing function ${name}`);
  return match[0];
}

test("a saved sale clears the buyer, payment, cart and sold items for the next buyer", () => {
  const reset = extractFunction("resetCompletedSale");

  for (const input of [
    "buyerNameInput",
    "buyerPostalCodeInput",
    "buyerCityInput",
    "buyerAddressInput",
    "chequeNumberInput",
    "chequeBankNameInput"
  ]) {
    assert.match(reset, new RegExp(`\\b${input}\\b`), `${input} is not cleared`);
  }

  assert.match(reset, /cart\.splice\(0, cart\.length\);/);
  assert.match(reset, /catalog\.splice\(/, "sold items must leave the catalog when the page does not reload");
  assert.match(reset, /input\.checked = input\.defaultChecked;/);
  assert.match(reset, /updateChequeDetailsVisibility\(\);/);
  assert.match(reset, /setBuyerMessage\(""\);/);
  assert.match(reset, /setPaymentMessage\(""\);/);
  assert.match(reset, /renderCart\(\);/);
});

test("the city list is emptied before the postal code change is replayed", () => {
  // showFreeTextCity copies the city list's value back into an empty city field, so the
  // list must be emptied first or the previous buyer's city would come back.
  const reset = extractFunction("resetCompletedSale");
  const clearCityList = reset.indexOf("buyerCitySelect?.replaceChildren();");
  const replayPostalCode = reset.indexOf('buyerPostalCodeInput?.dispatchEvent(new Event("input", { bubbles: true }));');
  assert.notEqual(clearCityList, -1);
  assert.notEqual(replayPostalCode, -1);
  assert.ok(clearCityList < replayPostalCode);
});

test("saving without printing resets the sale before reloading", () => {
  const handler = extractFunction("submitSaleWithoutInvoice");
  assert.match(
    handler,
    /if \(response\.status === 201 && payload\.saved\) \{\s*resetCompletedSale\(\);\s*setMessage\("Vente enregistrée\."\);\s*window\.location\.reload\(\);/);
});

test("saving with an invoice fills the invoice before resetting, then prints", () => {
  // The invoice lines are built from the cart, so the reset must come after renderInvoice.
  const handler = extractFunction("submitSaleWithInvoice");
  assert.match(
    handler,
    /if \(response\.status === 201 && payload\.saved\) \{\s*renderInvoice\(payload, saleDetails\);\s*resetCompletedSale\(\);\s*printInvoiceAndReload\(\);/);
});

test("a refused or failed save keeps what the operator typed", () => {
  for (const submit of ["submitSaleWithoutInvoice", "submitSaleWithInvoice"]) {
    const handler = extractFunction(submit);
    assert.equal(handler.match(/resetCompletedSale\(\)/g)?.length, 1, `${submit} resets outside the success branch`);
  }
});

test("the browser neither suggests nor restores a previous buyer or payment", () => {
  for (const id of ["checkout-buyer-name", "checkout-buyer-postal-code", "checkout-buyer-city", "checkout-buyer-address"]) {
    const field = checkoutPage.match(new RegExp(`<(?:input|textarea) id="${id}"[\\s\\S]*?>`));
    assert.ok(field, `Missing ${id}`);
    assert.match(field[0], /autocomplete="off"/, `${id} must not be autofilled`);
  }

  const paymentMethods = checkoutPage.match(/<input type="radio"\s+name="checkout-payment-method"[\s\S]*?\/>/g);
  assert.equal(paymentMethods?.length, 3);
  for (const radio of paymentMethods) {
    assert.match(radio, /autocomplete="off"/);
  }
});
