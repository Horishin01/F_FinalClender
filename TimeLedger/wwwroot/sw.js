// sw.js
// PWA のサービスワーカー。静的アセットをプリキャッシュし、ナビゲーションはネット優先（フォールバックはルートキャッシュ）、GET のみを対象にする。

const CACHE_NAME = 'timeledger-cache-v4';
const SCOPE_PATH = new URL(self.registration.scope).pathname.replace(/\/$/, '');
const toAppPath = (path) => {
  const raw = (path || '').toString();
  if (!raw || raw === '/') {
    return SCOPE_PATH ? `${SCOPE_PATH}/` : '/';
  }
  const normalized = raw.startsWith('/') ? raw : `/${raw}`;
  return SCOPE_PATH ? `${SCOPE_PATH}${normalized}` : normalized;
};

const APP_ROOT = toAppPath('/');
const EVENTS_API_PATH = toAppPath('/Events/GetEvents');
const ASSETS = [
  APP_ROOT,
  toAppPath('/css/site.css'),
  toAppPath('/js/site.js'),
  toAppPath('/manifest.webmanifest'),
  toAppPath('/icons/icon-192.png'),
  toAppPath('/icons/icon-512.png'),
  toAppPath('/lib/bootstrap/dist/css/bootstrap.min.css'),
  toAppPath('/lib/bootstrap/dist/js/bootstrap.bundle.min.js'),
  toAppPath('/lib/fontawesome/css/all.min.css')
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(ASSETS))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') {
    return;
  }

  const url = new URL(event.request.url);

  // ナビゲーションはネット優先、失敗時はキャッシュのルートへ
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request)
        .then((response) => {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(APP_ROOT, clone));
          return response;
        })
        .catch(() => caches.match(APP_ROOT) || Response.error())
    );
    return;
  }

  // 動的API（イベント取得など）はキャッシュしない
  if (url.pathname.startsWith(EVENTS_API_PATH)) {
    event.respondWith(fetch(event.request).catch(() => caches.match(event.request)));
    return;
  }

  // 静的リソースはキャッシュ優先
  event.respondWith(
    caches.match(event.request).then((cached) => {
      if (cached) {
        return cached;
      }
      return fetch(event.request).then((response) => {
        if (response && response.status === 200 && response.type === 'basic') {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clone));
        }
        return response;
      });
    })
  );
});
