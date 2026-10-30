// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js?{{VERSION}}');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('updatefound', () => {
    let newWorker = self.installing;
    newWorker.addEventListener('statechange', () => {
        switch (newWorker.state) {
            case 'installed':
                if (navigator.serviceWorker.controller) {
                    // Inform any open pages to reload to start using the new service worker.
                    self.clients.matchAll({type: 'window'}).then(clients => {
                        clients.forEach(client => client.postMessage('reload-window'));
                    });
                }
                break;
        }
    });
});
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/];
const offlineAssetsExclude = [
    /^service-worker\.js$/,
    /^appsettings\.json$/, // <-- Exclude appsettings files from caching
    /^customization\//  // so customization will not be cached by service worker
];
const skipIntegrityCheck = [
    /appsettings\.json$/,
    /manifest\.json$/,
    /_content\/Radzen\.Blazor\// //ignore Radzen.Blazor, SRI failes here, I dont know why TODO get SRI working with Radzen
];

async function onInstall(event) {
    console.info('Service worker Install ' + cacheName);
    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => {
            // Conditionally set the integrity and cache control based on URL patterns
            const options = skipIntegrityCheck.some(pattern => pattern.test(asset.url))
                ? {cache: 'no-cache'} // Skip integrity check
                : {integrity: asset.hash, cache: 'no-cache'};
            return new Request(asset.url, options);
        });
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate';

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}
