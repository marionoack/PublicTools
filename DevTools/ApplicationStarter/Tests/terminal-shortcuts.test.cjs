const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

function createTerminal() {
  const messages = [];
  let handler;
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "Terminal", "terminal.js"), "utf8"), {
    Terminal: class {
      loadAddon() {}
      open() {}
      attachCustomKeyEventHandler(callback) { handler = callback; }
      onData() {}
      onResize() {}
      focus() {}
    },
    FitAddon: { FitAddon: class { fit() {} } },
    ResizeObserver: class { observe() {} },
    document: { getElementById() {} },
    window: { chrome: { webview: { postMessage(message) { messages.push(message); }, addEventListener() {} } } }
  });
  messages.length = 0;
  return { messages, key: event => handler({ type: "keydown", ctrlKey: true, code: "Digit1", ...event }) };
}

test("Ctrl+digits launch numbered shortcuts without sending them to the terminal", () => {
  const { messages, key } = createTerminal();
  assert.equal(key({}), false);
  assert.equal(key({ code: "Digit9" }), false);
  assert.equal(key({ code: "Numpad2" }), false);
  assert.deepEqual(messages.map(message => message.number), [1, 9, 2]);
  assert.ok(messages.every(message => message.type === "shortcut"));
  assert.equal(key({ type: "keyup" }), false);
  assert.equal(key({ repeat: true }), false);
  assert.equal(messages.length, 3);
});

test("Other terminal keystrokes retain their original behavior", () => {
  const { messages, key } = createTerminal();
  for (const event of [{ ctrlKey: false }, { altKey: true }, { shiftKey: true }, { metaKey: true },
    { code: "Digit0" }, { code: "KeyC" }]) {
    assert.equal(key(event), true);
  }
  assert.equal(messages.length, 0);
});

test("Ctrl+Tab switches application tabs instead of being consumed by the terminal", () => {
  const { messages, key } = createTerminal();
  assert.equal(key({ code: "Tab" }), false);
  assert.equal(key({ code: "Tab", shiftKey: true }), false);
  assert.deepEqual(messages.map(message => message.reverse), [false, true]);
  assert.ok(messages.every(message => message.type === "switchTab"));
  assert.equal(key({ code: "Tab", ctrlKey: false }), true);
  assert.equal(key({ code: "Tab", type: "keyup" }), false);
  assert.equal(messages.length, 2);
});
