# Main Navigation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a laptop-focused global top menu with large `VENDEURS` and `CAISSE` navigation buttons.

**Architecture:** Render the menu once in the shared Razor layout. Compute active state from `Context.Request.Path` and style the links with existing visual tokens in `site.css`.

**Tech Stack:** ASP.NET Core Razor Pages, CSS, xUnit smoke tests.

---

### Task 1: Navigation Smoke Tests

**Files:**
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Steps:**
1. Add a smoke test that loads `/vendeurs` and asserts the decoded HTML contains `main-navigation`, `/vendeurs`, `/caisse`, `VENDEURS`, `CAISSE`, and `aria-current="page"` on the sellers link.
2. Add a smoke test that loads `/caisse` and asserts the decoded HTML contains the same menu links and `aria-current="page"` on the checkout link.
3. Add CSS smoke coverage for `.main-navigation`, `.main-navigation-link`, and `.main-navigation-link-active`.
4. Run `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter WebApplicationSmokeTests -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\main-navigation-red`.
5. Expected: fail because the shared layout has no navigation menu yet.

### Task 2: Shared Layout And Styles

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Shared/_Layout.cshtml`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`

**Steps:**
1. Add active-state path checks in `_Layout.cshtml`.
2. Add a `nav` above `main.page-shell` with two large links to `/vendeurs` and `/caisse`.
3. Add CSS classes for the top menu, button links, and active state.
4. Run the smoke tests again with isolated output.
5. Expected: pass.

### Task 3: Verification And Commit

**Files:**
- No additional code changes expected.

**Steps:**
1. Run the full suite with `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\main-navigation-full`.
2. Run `rtk git diff --check`.
3. Commit the navigation docs, layout, CSS, and tests.
