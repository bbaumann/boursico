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
