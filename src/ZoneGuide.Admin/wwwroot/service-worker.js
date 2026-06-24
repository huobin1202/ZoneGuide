self.addEventListener('install', function (event) {
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(clients.claim());
});

self.addEventListener('push', function (event) {
    try {
        var data = event.data.json();
        self.registration.showNotification(data.title || 'ZoneGuide', {
            body: data.message || '',
            icon: '/images/icon.svg',
            badge: '/images/icon.svg',
            vibrate: [200, 100, 200],
            tag: data.tag || 'notification',
            data: { url: data.url || '/' }
        });
    } catch (e) {
        self.registration.showNotification('ZoneGuide', {
            body: 'Có thông báo mới',
            icon: '/images/icon.svg'
        });
    }
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    var url = event.notification.data && event.notification.data.url ? event.notification.data.url : '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            for (var i = 0; i < clientList.length; i++) {
                var client = clientList[i];
                if (client.url.includes(self.location.host) && 'focus' in client) {
                    return client.focus().then(function (focused) {
                        if (focused) focused.navigate(url);
                    });
                }
            }
            if (clients.openWindow) return clients.openWindow(url);
        })
    );
});
