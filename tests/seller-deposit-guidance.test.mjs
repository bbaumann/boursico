import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const sellerDetailsPage = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Details.cshtml"),
  "utf8");

const sellerDetailsModel = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Details.cshtml.cs"),
  "utf8");

test("seller deposit blockers are grouped by action area", () => {
  assert.match(sellerDetailsPage, /class="seller-deposit-blocker-summary"/);
  assert.match(sellerDetailsPage, /Fiche vendeur/);
  assert.match(sellerDetailsPage, /Articles/);
  assert.match(sellerDetailsPage, /Corriger la fiche vendeur/);
  assert.match(sellerDetailsPage, /Corriger les articles incomplets/);
  assert.match(sellerDetailsPage, /class="seller-deposit-blocker-detail"/);
});

test("seller deposit validation shows a clear success confirmation after redirect", () => {
  assert.match(sellerDetailsModel, /\[TempData\]\s*public string\? DepositSuccessMessage/);
  assert.match(sellerDetailsModel, /Dépôt validé :/);
  assert.match(sellerDetailsModel, /articles? (sont|est) maintenant en stock/);
  assert.match(sellerDetailsPage, /data-seller-deposit-success/);
});
