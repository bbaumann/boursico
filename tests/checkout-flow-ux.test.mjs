import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const checkoutPage = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Checkout", "Index.cshtml"),
  "utf8");

const siteCss = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"),
  "utf8");

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

test("checkout presents a three-step sale workflow with a sticky total action", () => {
  assert.match(checkoutPage, /class="checkout-step-list"/);
  assert.match(checkoutPage, /Étape 1[\s\S]*Acheteur/);
  assert.match(checkoutPage, /Étape 2[\s\S]*Articles/);
  assert.match(checkoutPage, /Étape 3[\s\S]*Paiement/);
  assert.match(checkoutPage, /data-checkout-sticky-summary/);
  assert.match(checkoutPage, /data-checkout-sticky-count/);
  assert.match(checkoutPage, /data-checkout-sticky-total/);
  assert.match(siteCss, /\.checkout-grid\s*\{[\s\S]*grid-template-columns:\s*minmax\(18rem,\s*0\.85fr\)\s+minmax\(24rem,\s*1\.15fr\);/);
  assert.match(siteCss, /\.checkout-sticky-summary\s*\{[\s\S]*position:\s*sticky;/);
  assert.match(siteScript, /const stickyCountElement = root\.querySelector\("\[data-checkout-sticky-count\]"\);/);
  assert.match(siteScript, /stickyCountElement\.textContent = cart\.length === 1 \? "1 article" : `\$\{cart\.length\} articles`;/);
});

test("checkout search results expose a visible add action", () => {
  assert.match(checkoutPage, /<col class="checkout-result-action-column" \/>/);
  assert.match(checkoutPage, /<th>Ajouter<\/th>/);
  assert.match(siteScript, /function createCheckoutAddButton\(itemId\)/);
  assert.match(siteScript, /addButton\.dataset\.checkoutAdd = itemId;/);
  assert.match(siteScript, /createCheckoutActionCell\(item\.id\)\);/);
  assert.match(siteScript, /createCheckoutEmptyRow\("Aucun article ne correspond à cette recherche\.", 4\)/);
});

test("checkout marks already added search results as disabled instead of showing an error", () => {
  assert.match(siteScript, /const isAlreadyInCart = cart\.some\(cartItem => cartItem\.id === item\.id\);/);
  assert.match(siteScript, /row\.classList\.toggle\("checkout-result-row-disabled", isAlreadyInCart\);/);
  assert.match(siteScript, /row\.setAttribute\("aria-disabled", "true"\);/);
  assert.match(siteScript, /function createCheckoutAlreadyAddedCell\(\)/);
  assert.match(siteScript, /alreadyAdded\.textContent = "Déjà dans le panier";/);
  assert.match(siteCss, /\.checkout-result-row-disabled\s*\{[\s\S]*cursor:\s*not-allowed;/);
  assert.match(siteCss, /\.checkout-already-added-badge\s*\{[\s\S]*font-weight:\s*700;/);
  assert.doesNotMatch(siteScript, /Cet article est dÃ©jÃ  dans le panier\./);
});

test("checkout finalization actions name the consequence clearly", () => {
  assert.match(checkoutPage, /Valider sans imprimer/);
  assert.match(checkoutPage, /Valider et imprimer la facture/);
  assert.doesNotMatch(checkoutPage, />Enregistrer la vente<\/button>/);
  assert.doesNotMatch(checkoutPage, />Enregistrer et imprimer<\/button>/);
});
