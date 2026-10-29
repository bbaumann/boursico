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

const siteCss = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"),
  "utf8");

function extractDepositStep(attributeName) {
  const match = sellerDetailsPage.match(new RegExp(
    `<section class="seller-deposit-step-panel" ${attributeName}[\\s\\S]*?</section>`));
  assert.ok(match, `Missing ${attributeName} section`);
  return match[0];
}

function assertAppearsBefore(content, left, right) {
  const leftIndex = content.indexOf(left);
  const rightIndex = content.indexOf(right);
  assert.notEqual(leftIndex, -1, `Missing ${left}`);
  assert.notEqual(rightIndex, -1, `Missing ${right}`);
  assert.ok(leftIndex < rightIndex, `${left} should appear before ${right}`);
}

test("seller deposit blockers are grouped by action area", () => {
  assert.match(sellerDetailsPage, /class="seller-deposit-blocker-summary"/);
  assert.match(sellerDetailsPage, /Fiche vendeur/);
  assert.match(sellerDetailsPage, /Articles/);
  assert.match(sellerDetailsPage, /Compléter la fiche vendeur/);
  assert.match(sellerDetailsPage, /Corriger les articles incomplets/);
  assert.match(sellerDetailsPage, /class="seller-deposit-blocker-detail"/);
});

test("seller deposit validation shows a clear success confirmation after redirect", () => {
  assert.match(sellerDetailsModel, /\[TempData\]\s*public string\? DepositSuccessMessage/);
  assert.match(sellerDetailsModel, /Dépôt validé :/);
  assert.match(sellerDetailsModel, /articles? (sont|est) maintenant en stock/);
  assert.match(sellerDetailsPage, /data-seller-deposit-success/);
});

test("seller deposit finalization starts with a visible non-blocking fee step", () => {
  assert.match(sellerDetailsPage, /data-seller-deposit-initial-step/);
  assert.match(sellerDetailsPage, /data-seller-deposit-fee-step/);
  assert.match(sellerDetailsPage, /asp-page-handler="UpdateDepositFee"/);
  assert.match(sellerDetailsPage, /DepositFeeInput\.FeePaid/);
  assert.match(sellerDetailsPage, /Déduit au rapprochement/);
  assert.match(sellerDetailsModel, /OnPostUpdateDepositFeeAsync/);
  assert.match(sellerDetailsModel, /UpdateSellerFeePaidAsync/);
});

test("seller deposit stepper keeps each step compact and primary actions rightmost", () => {
  const feeStep = extractDepositStep("data-seller-deposit-fee-step");
  const printStep = extractDepositStep("data-seller-deposit-print-step hidden");
  const validateStep = extractDepositStep("data-seller-deposit-validate-step hidden");

  assert.match(feeStep, /<h4>Frais<\/h4>/);
  assert.match(printStep, /<h4>Contrat<\/h4>/);
  assert.match(validateStep, /<h4>Signature<\/h4>/);
  assert.doesNotMatch(sellerDetailsPage, /Cliquez pour lancer l'impression du contrat/);
  assert.doesNotMatch(sellerDetailsPage, /Les frais restants ne bloquent pas la validation du dépôt/);

  assertAppearsBefore(feeStep, "data-seller-deposit-close", "Continuer");
  assertAppearsBefore(printStep, "data-seller-deposit-back-to-fees", "Imprimer le contrat");
  assertAppearsBefore(validateStep, "data-seller-deposit-back", "data-seller-deposit-submit");
});

test("seller deposit hidden steps do not render their actions", () => {
  assert.match(
    siteCss,
    /\.seller-deposit-step-panel\[hidden\]\s*{\s*display:\s*none;\s*}/);
});
