import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const css = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"),
  "utf8");

const pageModel = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Data", "Index.cshtml.cs"),
  "utf8");

function rule(selector) {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = css.match(new RegExp(`(?:^|\\n)${escaped}\\s*\\{([^}]*)\\}`));
  assert.ok(match, `Missing CSS rule for ${selector}`);
  return match[1];
}

test("data page tabs are styled as tabs, not as form buttons", () => {
  const tabClass = pageModel.match(/public string TabClass\(string tab\)\s*\{[\s\S]*?\n    \}/);
  assert.ok(tabClass, "Missing TabClass");
  assert.doesNotMatch(tabClass[0], /button-link/);
  assert.match(tabClass[0], /"data-list-tab data-list-tab-active"/);
  assert.match(tabClass[0], /"data-list-tab"/);
  assert.doesNotMatch(css, /\.data-list-tabs \.button-link/);
});

test("data page tabs stay on one line, scroll sideways only, never vertically", () => {
  const tabs = rule(".data-list-tabs");
  assert.doesNotMatch(tabs, /flex-wrap:\s*wrap/);
  assert.match(tabs, /overflow-x:\s*auto;/);
  assert.match(tabs, /overflow-y:\s*hidden;/);

  const tab = rule(".data-list-tab");
  assert.match(tab, /white-space:\s*nowrap;/);
  // A negative margin overflowing the row is what caused a vertical scrollbar.
  assert.doesNotMatch(tab, /margin[^;]*-\d/);
});

test("data page tabs sit in a framed row with a separator between tabs", () => {
  const tabs = rule(".data-list-tabs");
  assert.match(tabs, /border:\s*1px solid var\(--border\);/);
  assert.match(tabs, /width:\s*fit-content;/);
  assert.match(rule(".data-list-tab + .data-list-tab"), /border-left:\s*1px solid var\(--border\);/);
});

test("the active data page tab is filled with the accent colour", () => {
  const active = rule(".data-list-tab.data-list-tab-active");
  assert.match(active, /background:\s*var\(--accent\);/);
  assert.match(active, /color:\s*#fffdf8;/);
});
