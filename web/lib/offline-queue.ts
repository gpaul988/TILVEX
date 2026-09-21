export type QueueOperation = "CREATE" | "UPDATE" | "DELETE";

export type QueueItem = {
  id: string;
  entity: string;
  operation: QueueOperation;
  payload: Record<string, unknown>;
  createdAt: string;
  attempts: number;
};

const DB_NAME = "greygas-pos";
const STORE_NAME = "sync-queue";
const DB_VERSION = 1;

function openQueue(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE_NAME, { keyPath: "id" });
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function enqueue(item: Omit<QueueItem, "id" | "createdAt" | "attempts">) {
  if (typeof indexedDB === "undefined") return null;
  const queued: QueueItem = {
    ...item,
    id: crypto.randomUUID(),
    createdAt: new Date().toISOString(),
    attempts: 0
  };
  const db = await openQueue();
  await new Promise<void>((resolve, reject) => {
    const request = db.transaction(STORE_NAME, "readwrite").objectStore(STORE_NAME).add(queued);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });
  return queued;
}

export async function readQueue(): Promise<QueueItem[]> {
  if (typeof indexedDB === "undefined") return [];
  const db = await openQueue();
  return new Promise((resolve, reject) => {
    const request = db.transaction(STORE_NAME, "readonly").objectStore(STORE_NAME).getAll();
    request.onsuccess = () => resolve(request.result as QueueItem[]);
    request.onerror = () => reject(request.error);
  });
}

export async function removeQueued(id: string) {
  if (typeof indexedDB === "undefined") return;
  const db = await openQueue();
  await new Promise<void>((resolve, reject) => {
    const request = db.transaction(STORE_NAME, "readwrite").objectStore(STORE_NAME).delete(id);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });
}

export async function flushQueue() {
  const queued = await readQueue();
  for (const item of queued) {
    const response = await fetch("/api/sync", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(item)
    });
    if (!response.ok) break;
    await removeQueued(item.id);
  }
}

export async function requestBackgroundSync() {
  if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return;
  const registration = await navigator.serviceWorker.ready;
  const syncManager = (registration as ServiceWorkerRegistration & {
    sync?: { register: (tag: string) => Promise<void> };
  }).sync;
  if (syncManager) await syncManager.register("greygas-sync");
}
