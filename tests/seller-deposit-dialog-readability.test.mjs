import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const css = readFileSync(join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"), "utf8");

function rule(selector) {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = css.match(new RegExp(`(?:^|\\n)${escaped}\\s*\\{([^}]*)\\}`));
  assert.ok(match, `Missing CSS rule for ${selector}`);
  return match[1];
}

test("deposit finalization dialog is wide and uses larger text than the base size", () => {
  const dialog = rule(".seller-deposit-dialog");
  assert.match(dialog, /width:\s*min\(48rem,\s*calc\(100vw - 2rem\)\);/);
  assert.match(dialog, /font-size:\s*1\.15rem;/);
});

test("deposit finalization dialog headings are clearly larger than its text", () => {
  assert.match(rule(".seller-deposit-dialog h3"), /font-size:\s*1\.6rem;/);
  assert.match(rule(".seller-deposit-dialog h4"), /font-size:\s*1\.35rem;/);
});

test("button-styled buttons inherit the surrounding text size everywhere", () => {
  // Buttons do not inherit the page font by default and render at the browser's small
  // control size (about 13px) while links with the same class render at 18px. The
  // shorthand resets the weight, so the weight must be declared after it.
  const buttons = rule(".button-link");
  assert.match(buttons, /font:\s*inherit;\s*font-weight:\s*700;/);
});

test("deposit finalization dialog buttons are taller", () => {
  assert.match(rule(".seller-deposit-dialog .button-link"), /min-height:\s*3\.25rem;/);
});

test("deposit signature checkbox is large enough to hit easily", () => {
  const checkbox = rule(".seller-deposit-dialog .checkbox-label input");
  assert.match(checkbox, /width:\s*1\.5rem;/);
  assert.match(checkbox, /height:\s*1\.5rem;/);
  assert.match(checkbox, /flex-shrink:\s*0;/);
});

test("deposit finalization dialog width is declared after the generic admin dialog width", () => {
  // Both selectors match the dialog with equal specificity, so the later width wins.
  const adminDialogIndex = css.search(/\n\.admin-dialog\s*\{/);
  const depositDialogIndex = css.search(/\n\.seller-deposit-dialog\s*\{/);
  assert.notEqual(adminDialogIndex, -1);
  assert.notEqual(depositDialogIndex, -1);
  assert.ok(adminDialogIndex < depositDialogIndex, ".seller-deposit-dialog must come after .admin-dialog");
});
