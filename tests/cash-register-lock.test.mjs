import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";
import vm from "node:vm";

const siteScript = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "wwwroot", "js", "site.js"),
  "utf8");

const checkoutPage = readFileSync(
  join(process.cwd(), "src", "AssociationSaleTool.Web", "Pages", "Checkout", "Index.cshtml"),
  "utf8");

const storageKey = "association-sale-tool.cash-register-id";

function fakeStorage(initial = {}) {
  const values = new Map(Object.entries(initial));
  return {
    getItem: key => (values.has(key) ? values.get(key) : null),
    setItem: (key, value) => values.set(key, String(value)),
    removeItem: key => values.delete(key)
  };
}

// Runs the real site.js against a document with no matching elements, so only its
// function declarations are exercised.
function loadSiteScript(localStorage) {
  const document = { addEventListener() {}, querySelector: () => null, querySelectorAll: () => [] };
  const context = vm.createContext({ document, window: {}, Intl, localStorage });
  vm.runInContext(siteScript, context);
  return context;
}

function fakeDeviceSection(options) {
  const listeners = {};
  const select = {
    value: "",
    options: [{ value: "", textContent: "Choisir une caisse" }, ...options],
    focus() {}
  };
  const current = { textContent: "" };
  const message = { textContent: "" };
  const button = { addEventListener: (type, listener) => { listeners[type] = listener; } };
  const root = {
    querySelector: selector => ({
      "[data-device-cash-register-select]": select,
      "[data-device-cash-register-current]": current,
      "[data-device-cash-register-message]": message,
      "[data-device-cash-register-save]": button
    })[selector] ?? null
  };
  return { root, select, current, message, click: () => listeners.click() };
}

const cashRegisters = [{ value: "1", textContent: "Caisse 1" }, { value: "2", textContent: "Caisse 2" }];

test("this computer's cash register section shows the register currently stored", () => {
  const { initializeDeviceCashRegister } = loadSiteScript(fakeStorage({ [storageKey]: "2" }));
  const section = fakeDeviceSection(cashRegisters);

  initializeDeviceCashRegister(section.root);

  assert.equal(section.current.textContent, "Caisse actuelle de cet ordinateur : Caisse 2");
  assert.equal(section.select.value, "2");
});

test("this computer's cash register section says when none or an inactive one is stored", () => {
  for (const initial of [{}, { [storageKey]: "9" }]) {
    const { initializeDeviceCashRegister } = loadSiteScript(fakeStorage(initial));
    const section = fakeDeviceSection(cashRegisters);

    initializeDeviceCashRegister(section.root);

    assert.equal(section.current.textContent, "Aucune caisse active n'est choisie sur cet ordinateur.");
    assert.equal(section.select.value, "");
  }
});

test("changing this computer's cash register stores it and confirms the change", () => {
  const storage = fakeStorage({ [storageKey]: "1" });
  const { initializeDeviceCashRegister } = loadSiteScript(storage);
  const section = fakeDeviceSection(cashRegisters);
  initializeDeviceCashRegister(section.root);

  section.select.value = "2";
  section.click();

  assert.equal(storage.getItem(storageKey), "2");
  assert.equal(section.current.textContent, "Caisse actuelle de cet ordinateur : Caisse 2");
  assert.equal(section.message.textContent, "Cet ordinateur utilise maintenant Caisse 2.");
});

test("changing this computer's cash register requires a choice", () => {
  const storage = fakeStorage({ [storageKey]: "1" });
  const { initializeDeviceCashRegister } = loadSiteScript(storage);
  const section = fakeDeviceSection(cashRegisters);
  initializeDeviceCashRegister(section.root);

  section.select.value = "";
  section.click();

  assert.equal(storage.getItem(storageKey), "1");
  assert.equal(section.message.textContent, "Choisissez une caisse.");
});

test("the checkout hides the cash register choice once one is set during the sale phase", () => {
  assert.match(checkoutPage, /data-cash-register-locked="@\(Model\.CashRegisterChoiceLocked \? "true" : "false"\)"/);
  assert.match(checkoutPage, /<div data-checkout-cash-register-choice>/);
  assert.match(checkoutPage, /data-checkout-cash-register-locked-hint hidden>Pour changer de caisse, passez par l'administration sur cet ordinateur\.</);

  assert.match(siteScript, /const cashRegisterChoiceLocked = cashRegisterSetup\?\.dataset\.cashRegisterLocked === "true";/);
  assert.match(siteScript, /const isLocked = cashRegisterChoiceLocked && cashRegisterId\.length > 0;/);
  assert.match(siteScript, /cashRegisterChoice\.hidden = isLocked;/);
  assert.match(siteScript, /cashRegisterLockedHint\.hidden = !isLocked;/);
  assert.match(siteScript, /document\.querySelectorAll\("\[data-device-cash-register-root\]"\)\.forEach\(initializeDeviceCashRegister\);/);
});
