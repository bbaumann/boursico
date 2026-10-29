import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const css = readFileSync(join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "css", "site.css"), "utf8");
const sellersIndex = readFileSync(join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Index.cshtml"), "utf8");
const sellerDetails = readFileSync(join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Sellers", "Details.cshtml"), "utf8");

test("base typography and navigation are readable on common elderly-user displays", () => {
  assert.match(css, /html\s*\{\s*font-size:\s*18px;/);
  assert.match(css, /font-family:\s*"Segoe UI",\s*Arial,\s*sans-serif;/);
  assert.match(css, /@media\s*\(max-width:\s*980px\)\s*\{[\s\S]*\.main-navigation\s*\{[\s\S]*grid-template-columns:\s*repeat\(3,\s*minmax\(0,\s*1fr\)\);/);
  assert.match(css, /@media\s*\(max-width:\s*980px\)[\s\S]*\.main-navigation-link\s*\{[\s\S]*min-height:\s*3rem;/);
});

test("seller index hides secondary contact columns at narrower widths", () => {
  assert.match(sellersIndex, /<col class="seller-index-email-column seller-index-secondary-column" \/>/);
  assert.match(sellersIndex, /<th class="seller-index-secondary-column">E-mail<\/th>/);
  assert.match(sellersIndex, /<td class="seller-index-secondary-column">@\(seller\.Email/);
  assert.match(css, /@media\s*\(max-width:\s*980px\)[\s\S]*\.seller-index-secondary-column\s*\{[\s\S]*display:\s*none;/);
});

test("seller detail presents deposit next action before contact details on compact screens", () => {
  const depositPanelIndex = sellerDetails.indexOf("seller-deposit-panel");
  const depositSummaryIndex = sellerDetails.indexOf("seller-deposit-summary");
  const readOnlyMessageIndex = sellerDetails.indexOf("Model.ReadOnlyMessage");
  const contactListIndex = sellerDetails.indexOf("seller-contact-list");

  assert.notEqual(depositPanelIndex, -1);
  assert.notEqual(depositSummaryIndex, -1);
  assert.notEqual(readOnlyMessageIndex, -1);
  assert.notEqual(contactListIndex, -1);
  assert.ok(depositPanelIndex < readOnlyMessageIndex, "deposit panel should appear before the read-only message");
  assert.ok(depositPanelIndex < contactListIndex, "deposit panel should appear before the contact list");
  assert.ok(depositSummaryIndex < contactListIndex, "deposit summary should appear before the contact list");
  assert.match(sellerDetails, /VisibleDepositBlockers\.Count/);
  assert.match(css, /@media\s*\(max-width:\s*980px\)[\s\S]*\.seller-contact-list\s*\{[\s\S]*grid-template-columns:\s*minmax\(0,\s*1fr\);/);
});

test("seller detail header keeps its actions on a single line", () => {
  assert.match(sellerDetails, /asp-page="\/Sellers\/Edit" asp-route-id="@Model\.Seller\.Id">Modifier<\/a>/);
  assert.match(css, /\.seller-detail-card \.header-actions \{\s*flex-shrink:\s*0;/);
});
