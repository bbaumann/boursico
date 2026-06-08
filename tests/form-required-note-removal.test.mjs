import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const formPages = [
  ["création vendeur", "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Create.cshtml"],
  ["modification vendeur", "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Edit.cshtml"],
  ["modification article", "src", "AssociationSaleTool.Web", "Pages", "Items", "Edit.cshtml"],
  ["caisse", "src", "AssociationSaleTool.Web", "Pages", "Checkout", "Index.cshtml"]
];

test("forms do not render the required fields helper sentence", () => {
  for (const [name, ...segments] of formPages) {
    const page = readFileSync(join(process.cwd(), ...segments), "utf8");
    assert.doesNotMatch(
      page,
      /Les champs marqués[\s\S]*Obligatoire[\s\S]*doivent être complétés/,
      `${name} should not render the required fields helper sentence`);
  }
});
