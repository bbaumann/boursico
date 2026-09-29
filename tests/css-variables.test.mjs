import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const css = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"),
  "utf8");

test("every CSS variable used in site.css is defined", () => {
  // An undefined var() is silently ignored by the browser: --accent-strong was used for
  // the current deposit step and fell back to the plain text colour.
  const defined = new Set([...css.matchAll(/(--[a-z0-9-]+)\s*:/g)].map(match => match[1]));
  const used = new Set([...css.matchAll(/var\(\s*(--[a-z0-9-]+)/g)].map(match => match[1]));
  const undefinedVariables = [...used].filter(name => !defined.has(name));
  assert.deepEqual(undefinedVariables, []);
});
