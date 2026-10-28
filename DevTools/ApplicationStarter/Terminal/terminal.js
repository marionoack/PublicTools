const terminal = new Terminal({
  cursorBlink: true, fontFamily: "Cascadia Mono, Consolas, monospace",
  fontSize: 14, scrollback: 10000, allowProposedApi: false,
  theme: { background: "#10151d" }
});
const fit = new FitAddon.FitAddon();
terminal.loadAddon(fit);
terminal.open(document.getElementById("terminal"));
const send = message => window.chrome.webview.postMessage(message);
terminal.attachCustomKeyEventHandler(event => {
  if (event.ctrlKey && !event.altKey && !event.metaKey && event.code === "Tab") {
    if (event.type === "keydown" && !event.repeat) send({ type: "switchTab", reverse: !!event.shiftKey });
    return false;
  }
  const digit = /^(?:Digit|Numpad)([1-9])$/.exec(event.code);
  if (!event.ctrlKey || event.altKey || event.shiftKey || event.metaKey || !digit) return true;
  if (event.type === "keydown" && !event.repeat) send({ type: "shortcut", number: Number(digit[1]) });
  return false;
});
terminal.onData(data => send({ type: "input", data }));
terminal.onResize(size => send({ type: "resize", cols: size.cols, rows: size.rows }));
window.chrome.webview.addEventListener("message", event => {
  if (event.data.type === "focus") terminal.focus();
  if (event.data.type === "output") {
    terminal.write(Uint8Array.from(atob(event.data.data), c => c.charCodeAt(0)));
  }
  if (event.data.type === "exit") terminal.write("\r\n[Prozess beendet]\r\n");
});
new ResizeObserver(() => fit.fit()).observe(document.getElementById("terminal"));
fit.fit();
terminal.focus();
send({ type: "ready", cols: terminal.cols, rows: terminal.rows });
