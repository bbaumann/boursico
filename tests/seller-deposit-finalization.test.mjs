import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

test("seller deposit finalization flow opens only when server validation passed", () => {
  assert.match(
    siteScript,
    /document\.querySelectorAll\("\[data-seller-deposit-flow\]"\)\.forEach\(initializeSellerDepositFlow\);/);
  assert.match(
    siteScript,
    /const autoOpen = root\.hasAttribute\("data-seller-deposit-auto-open"\);/);
  assert.match(
    siteScript,
    /const initialStep = root\.dataset\.sellerDepositInitialStep \|\| "fees";/);
  assert.match(
    siteScript,
    /if \(autoOpen\) \{\s*dialog\.showModal\(\);\s*showStep\(initialStep\);\s*\}/);
});

test("seller deposit finalization advances after print and requires signed contract confirmation", () => {
  assert.match(
    siteScript,
    /const printButton = root\.querySelector\("\[data-seller-deposit-print\]"\);/);
  assert.match(
    siteScript,
    /const feeStep = root\.querySelector\("\[data-seller-deposit-fee-step\]"\);/);
  assert.match(
    siteScript,
    /window\.open\(printButton\.dataset\.sellerDepositPrintUrl,\s*"_blank",\s*"noopener"\);/);
  assert.match(
    siteScript,
    /printButton\?\.addEventListener\("click",\s*\(\) => \{/);
  assert.match(
    siteScript,
    /submitButton\.disabled = !signatureConfirmation\.checked;/);
  assert.match(
    siteScript,
    /signatureConfirmation\?\.addEventListener\("change", updateSubmitState\);/);
  assert.match(
    siteScript,
    /backToFeesButton\?\.addEventListener\("click", \(\) => showStep\("fees"\)\);/);
});

test("deposit contract auto-print mode prints and closes its generated tab", () => {
  assert.match(
    siteScript,
    /document\.querySelectorAll\("\[data-deposit-contract-auto-print\]"\)\.forEach\(initializeDepositContractAutoPrint\);/);
  assert.match(siteScript, /window\.onafterprint = \(\) => \{/);
  assert.match(siteScript, /window\.close\(\);/);
  assert.match(siteScript, /window\.print\(\);/);
});
