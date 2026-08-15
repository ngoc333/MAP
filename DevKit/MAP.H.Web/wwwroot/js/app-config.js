window.mapConfig = (() => {
  const storageKey = "map-app-config";

  if (typeof localStorage === "undefined") {
    console.error("[mapConfig] localStorage is not available in this context");
    return { get: () => null, set: () => {}, remove: () => {} };
  }

  function get() {
    return localStorage.getItem(storageKey);
  }

  function set(value) {
    localStorage.setItem(storageKey, value);
  }

  function remove() {
    localStorage.removeItem(storageKey);
  }

  return { get, set, remove };
})();
