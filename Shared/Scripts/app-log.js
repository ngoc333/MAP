window.mapLog = (() => {
  const databaseName = "map-diagnostics";
  const storeName = "logs";
  const retentionMs = 30 * 24 * 60 * 60 * 1000;
  let shortcutHandler;

  if (typeof indexedDB === "undefined") {
    console.error("[mapLog] IndexedDB is not available in this context");
    const noop = () => Promise.resolve();
    return { write: noop, get: () => Promise.resolve([]), days: () => Promise.resolve([]), clear: noop, registerShortcut: () => {} };
  }

  function open() {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(databaseName, 1);
      request.onupgradeneeded = () => {
        const store = request.result.createObjectStore(storeName, { keyPath: "id", autoIncrement: true });
        store.createIndex("day", "day");
        store.createIndex("timestamp", "timestamp");
      };
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => {
        console.error("[mapLog] Failed to open database:", request.error);
        reject(request.error);
      };
    });
  }

  async function write(entry) {
    const db = await open();
    try {
      const timestamp = entry.timestamp || new Date().toISOString();
      const transaction = db.transaction(storeName, "readwrite");
      const store = transaction.objectStore(storeName);
      store.add({ ...entry, timestamp, day: timestamp.slice(0, 10) });
      const expired = IDBKeyRange.upperBound(new Date(Date.now() - retentionMs).toISOString(), true);
      store.index("timestamp").openCursor(expired).onsuccess = event => {
        const cursor = event.target.result;
        if (cursor) { cursor.delete(); cursor.continue(); }
      };
      await complete(transaction);
    } catch (e) {
      console.error("[mapLog] write failed:", e);
      throw e;
    } finally {
      db.close();
    }
  }

  async function get(day) {
    const db = await open();
    const transaction = db.transaction(storeName, "readonly");
    const store = transaction.objectStore(storeName);
    const request = day ? store.index("day").getAll(day) : store.getAll();
    const result = await requestResult(request);
    db.close();
    return result.sort((a, b) => b.timestamp.localeCompare(a.timestamp));
  }

  async function days() {
    const entries = await get();
    return [...new Set(entries.map(x => x.day))].sort().reverse();
  }

  async function clear(day) {
    const db = await open();
    const transaction = db.transaction(storeName, "readwrite");
    const store = transaction.objectStore(storeName);
    if (!day) store.clear();
    else store.index("day").openCursor(IDBKeyRange.only(day)).onsuccess = event => {
      const cursor = event.target.result;
      if (cursor) { cursor.delete(); cursor.continue(); }
    };
    await complete(transaction);
    db.close();
  }

  function registerShortcut(dotNetReference) {
    if (shortcutHandler) window.removeEventListener("keydown", shortcutHandler);
    shortcutHandler = event => {
      if (event.ctrlKey && event.shiftKey && event.key.toLowerCase() === "l") {
        event.preventDefault();
        dotNetReference.invokeMethodAsync("OpenSystemLogsAsync");
      }
    };
    window.addEventListener("keydown", shortcutHandler);
  }

  function earlyError(message, exception) {
    write({ timestamp: new Date().toISOString(), level: "Error", category: "JavaScript", eventName: "UnhandledError", message: String(message), exception: exception ? String(exception) : null }).catch(() => {});
  }

  function complete(transaction) {
    return new Promise((resolve, reject) => { transaction.oncomplete = resolve; transaction.onerror = () => reject(transaction.error); transaction.onabort = () => reject(transaction.error); });
  }

  function requestResult(request) {
    return new Promise((resolve, reject) => { request.onsuccess = () => resolve(request.result); request.onerror = () => reject(request.error); });
  }

  window.addEventListener("error", event => earlyError(event.message, event.error));
  window.addEventListener("unhandledrejection", event => earlyError("Unhandled promise rejection", event.reason));
  write({ timestamp: new Date().toISOString(), level: "Information", category: "JavaScript", eventName: "HostPageLoaded", message: "Host page loaded" })
    .then(() => console.log("[mapLog] HostPageLoaded entry written"))
    .catch(e => console.error("[mapLog] initial write failed:", e));
  return { write, get, days, clear, registerShortcut };
})();
