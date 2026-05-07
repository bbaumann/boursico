# Excel Postal City Compatibility Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make seller Excel prefill use postal code and city values that are compatible with the application's authoritative Isere city list.

**Architecture:** Add a server-side lookup that reads the same `wwwroot/data/isere-postal-cities.json` file used by the browser. Extend Excel parsing to read the workbook `CP VILLE` value, accept only canonical authoritative postal-code/city pairs, and prefill the normal seller form with the canonical values.

**Tech Stack:** ASP.NET Core Razor Pages, C# XML-based `.xlsx` parsing, xUnit, static JSON city data.

---

### Task 1: Add postal-code/city lookup

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/IserePostalCityLookup.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/IserePostalCityLookupTests.cs`

**Steps:**
1. Write failing tests for loading the JSON and city validation.
2. Run the lookup tests and verify they fail because the lookup does not exist.
3. Implement the lookup and register it in DI from `wwwroot/data/isere-postal-cities.json`.
4. Run the lookup tests and verify they pass.

### Task 2: Parse Excel `CP VILLE`

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/ExcelSellerItemParser.cs`
- Modify: `src/AssociationSaleTool.Web/Services/Models/ExcelSellerItemParseResult.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/ExcelSellerItemParserTests.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Steps:**
1. Write failing tests showing that `38120 Saint-Égrève` style canonical workbook values prefill postal code and city.
2. Write a failing test showing that a non-authoritative city for the same postal code is rejected instead of guessed.
3. Implement the parser/result/page-model wiring.
4. Run the parser and smoke tests and verify they pass.

### Task 3: Update documentation

**Files:**
- Modify: `docs/decisions.md`
- Modify: `docs/features.md`
- Modify: `docs/vnext-backlog.md`
- Modify: `docs/plans/2026-05-04-excel-seller-item-prefill-design.md`

**Steps:**
1. Update the Excel prefill contract to say city/postal code are accepted only when they match the authoritative Isere list.
2. Keep postal address as `UNKNOWN` until address handling is designed.
3. Run the full test project before completion.
