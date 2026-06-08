import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

test("seller deposit finalization flow opens the guided dialog", () => {
  assert.match(
    siteScript,
    /document\.querySelectorAll\("\[data-seller-deposit-flow\]"\)\.forEach\(initializeSellerDepositFlow\);/);
  assert.match(
    siteScript,
    /const openButton = root\.querySelector\("\[data-seller-deposit-open\]"\);/);
  assert.match(siteScript, /dialog\.showModal\(\);/);
});

test("seller deposit finalization advances after print and requires signed contract confirmation", () => {
  assert.match(
    siteScript,
    /printLink\?\.addEventListener\("click",\s*\(\) => showStep\("validate"\)\);/);
  assert.match(
    siteScript,
    /submitButton\.disabled = !signatureConfirmation\.checked;/);
  assert.match(
    siteScript,
    /signatureConfirmation\?\.addEventListener\("change", updateSubmitState\);/);
});
