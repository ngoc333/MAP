window.mapLog = (() => {
	const databaseName = "map-diagnostics";
	const storeName = "logs";
	const retentionMs = 7 * 24 * 60 * 60 * 1000;

	if (typeof indexedDB === "undefined") {
		console.error("[mapLog] IndexedDB is not available in this context");
		const noop = () => Promise.resolve();
		return {
			write: noop,
			get: () => Promise.resolve([]),
			days: () => Promise.resolve([]),
			clear: noop,
		};
	}

	function open() {
		return new Promise((resolve, reject) => {
			const request = indexedDB.open(databaseName, 1);
			request.onupgradeneeded = () => {
				const store = request.result.createObjectStore(storeName, {
					keyPath: "id",
					autoIncrement: true,
				});
				store.createIndex("day", "day");
				store.createIndex("timestamp", "timestamp");
			};
			request.onsuccess = () => resolve(request.result);
			request.onerror = () => reject(request.error);
		});
	}

	async function write(entry) {
		const db = await open();
		try {
			const timestamp = entry.timestamp || new Date().toISOString();
			const transaction = db.transaction(storeName, "readwrite");
			const store = transaction.objectStore(storeName);
			store.add({ ...entry, timestamp, day: timestamp.slice(0, 10) });
			const expired = IDBKeyRange.upperBound(
				new Date(Date.now() - retentionMs).toISOString(),
				true,
			);
			store.index("timestamp").openCursor(expired).onsuccess = (event) => {
				const cursor = event.target.result;
				if (cursor) {
					cursor.delete();
					cursor.continue();
				}
			};
			await complete(transaction);
		} finally {
			db.close();
		}
	}

	async function get(day) {
		const db = await open();
		try {
			const transaction = db.transaction(storeName, "readonly");
			const store = transaction.objectStore(storeName);
			const request = store.index("day").getAll(day);
			const result = await requestResult(request);
			return result.sort((a, b) => b.timestamp.localeCompare(a.timestamp));
		} finally {
			db.close();
		}
	}

	async function days() {
		const db = await open();

		try {
			const transaction = db.transaction(storeName, "readonly");
			const entries = await requestResult(
				transaction.objectStore(storeName).getAll(),
			);

			return [...new Set(entries.map((x) => x.day))]
				.filter(Boolean)
				.sort()
				.reverse();
		} finally {
			db.close();
		}
	}

	async function clear(day) {
		const db = await open();
		try {
			const transaction = db.transaction(storeName, "readwrite");
			const store = transaction.objectStore(storeName);
			store.index("day").openCursor(IDBKeyRange.only(day)).onsuccess = (
				event,
			) => {
				const cursor = event.target.result;
				if (cursor) {
					cursor.delete();
					cursor.continue();
				}
			};
			await complete(transaction);
		} finally {
			db.close();
		}
	}

	function complete(transaction) {
		return new Promise((resolve, reject) => {
			transaction.oncomplete = resolve;
			transaction.onerror = () => reject(transaction.error);
			transaction.onabort = () => reject(transaction.error);
		});
	}

	function requestResult(request) {
		return new Promise((resolve, reject) => {
			request.onsuccess = () => resolve(request.result);
			request.onerror = () => reject(request.error);
		});
	}

	return { write, get, days, clear };
})();
