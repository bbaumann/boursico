import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const sellerDetailsPage = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Details.cshtml"),
  "utf8");

test("seller deposit cancellation is separated into a lower administration section", () => {
  const depositPanelIndex = sellerDetailsPage.indexOf("seller-deposit-panel");
  const contactListIndex = sellerDetailsPage.indexOf("seller-contact-list");
  const adminSectionIndex = sellerDetailsPage.indexOf("seller-deposit-admin-section");
  const cancelButtonIndex = sellerDetailsPage.indexOf("Annuler la validation du dépôt");

  assert.notEqual(depositPanelIndex, -1);
  assert.notEqual(contactListIndex, -1);
  assert.notEqual(adminSectionIndex, -1);
  assert.notEqual(cancelButtonIndex, -1);
  assert.ok(adminSectionIndex > contactListIndex, "administration section should sit below normal seller details");
  assert.ok(cancelButtonIndex > adminSectionIndex, "cancellation action should live inside the administration section");
  assert.match(sellerDetailsPage, /<h2>Administration<\/h2>/);
  assert.match(sellerDetailsPage, /class="subsection seller-deposit-admin-section"/);
  assert.doesNotMatch(
    sellerDetailsPage.slice(depositPanelIndex, contactListIndex),
    /Annuler la validation du dépôt/);
});
