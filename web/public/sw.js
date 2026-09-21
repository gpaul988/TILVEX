const CACHE = "greygas-pos-shell-v1";
const APP_SHELL = ["/", "/dashboard", "/manifest.webmanifest"];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(APP_SHELL)));
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(self.clients.claim());
});

self.addEventListener("fetch", (event) => {
  if (event.request.method !== "GET" || new URL(event.request.url).origin !== self.location.origin) return;
  event.respondWith(
    fetch(event.request).catch(() => caches.match(event.request).then((response) => response || caches.match("/dashboard")))
  );
});

self.addEventListener("sync", (event) => {
  if (event.tag !== "greygas-sync") return;
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((clients) => {
      clients.forEach((client) => client.postMessage({ type: "SYNC_QUEUE" }));
    })
  );
});
