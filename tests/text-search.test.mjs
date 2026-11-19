import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";
import vm from "node:vm";

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

function loadSiteScript() {
  const document = {
    addEventListener() {},
    querySelector: () => null,
    querySelectorAll: () => []
  };
  const context = vm.createContext({ document, window: {}, Intl });
  vm.runInContext(siteScript, context);
  return context;
}

const { parseSearchQuery, matchesSearchWords } = loadSiteScript();

function matches(query, text) {
  return matchesSearchWords(text, parseSearchQuery(query));
}

test("words match in any order, ignoring case, accents, plurals and filler words", () => {
  assert.ok(matches("pompier camion", "Camion de pompiers"));
  assert.ok(matches("camions de pompiers", "Camion pompier"));
  assert.ok(matches("elephant peluche", "Peluche Éléphant"));
  assert.ok(matches("coeur", "Cœur en tissu"));
  assert.ok(matches("jeux societe", "Jeu de société"));
  assert.ok(matches("chevaux", "Cheval à bascule"));
  assert.ok(matches("pomp", "Camion de pompiers"));
  assert.ok(matches("b-157", "b157 puzzle"));
});

test("every meaningful word must be found", () => {
  assert.ok(!matches("camion rouge", "Camion de pompiers"));
});

test("a query made only of short words is kept so typing 'le' still finds 'Lego'", () => {
  assert.ok(matches("le", "Lego Duplo"));
});

test("a blank query matches nothing to filter on", () => {
  assert.equal(parseSearchQuery("  - ' ").length, 0);
});

test("every search field uses the shared matcher instead of a raw substring", () => {
  assert.doesNotMatch(siteScript, /\.includes\(query\)/);
  assert.equal((siteScript.match(/parseSearchQuery\(searchInput\.value\)/g) || []).length, 6);
});
