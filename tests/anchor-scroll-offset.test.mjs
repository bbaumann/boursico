import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const css = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"),
  "utf8");

test("anchor jumps clear the sticky main navigation", () => {
  // After labelling, the seller page redirects to #articles: without an offset the
  // sticky navigation covers the result message at the top of that section.
  assert.match(css, /html\s*\{[^}]*scroll-padding-top:\s*7rem;/);
});

test("the offset is dropped where the navigation stops being sticky", () => {
  const narrow = css.slice(css.indexOf("@media (max-width: 980px)"));
  assert.match(narrow, /html\s*\{\s*scroll-padding-top:\s*0;/);
  assert.match(narrow, /\.main-navigation\s*\{\s*position:\s*static;/);
});
