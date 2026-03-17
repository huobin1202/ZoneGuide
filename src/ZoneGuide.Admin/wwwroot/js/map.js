// POI Map Functions using Leaflet
var poiMap = null;
var markers = [];

// Picker map for dialog
var pickerMap = null;
var pickerMarker = null;
var dotNetRef = null;

window.initPOIMap = function (elementId, centerLat, centerLng, poiData) {
    // Destroy existing map if any
    if (poiMap) {
        poiMap.remove();
        poiMap = null;
    }
    markers = [];

    // Initialize map
    poiMap = L.map(elementId, {

        // Giới hạn vùng kéo của bản đồ [[Nam, Tây], [Bắc, Đông]]
        maxBounds: [
            [-90, -180], // Góc Tây Nam (South-West)
            [90, 180]    // Góc Đông Bắc (North-East)
        ],

        // Độ cứng của viền (1.0 = cứng ngắc, kéo chạm viền sẽ không bị nảy ra ngoài)
        maxBoundsViscosity: 1.0
    }).setView([centerLat, centerLng], 14);
    // Add OpenStreetMap tiles
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors',
        maxZoom: 19,
        noWrap: true // Ngăn tile lặp lại theo chiều ngang
    }).addTo(poiMap);

    // Add markers for each POI
    if (poiData && poiData.length > 0) {
        var bounds = [];

        poiData.forEach(function (poi) {
            var marker = L.marker([poi.lat, poi.lng])
                .addTo(poiMap)
                .bindPopup(
                    '<div style="min-width: 200px;">' +
                    '<h4 style="margin: 0 0 8px 0; color: #1976D2;">' + poi.name + '</h4>' +
                    '<p style="margin: 0 0 4px 0;"><strong>Category:</strong> ' + poi.category + '</p>' +
                    '<p style="margin: 0 0 4px 0;"><strong>Lat:</strong> ' + poi.lat.toFixed(6) + '</p>' +
                    '<p style="margin: 0 0 4px 0;"><strong>Lng:</strong> ' + poi.lng.toFixed(6) + '</p>' +
                    (poi.description ? '<p style="margin: 8px 0 0 0;">' + poi.description + '</p>' : '') +
                    '</div>'
                );

            marker.poiId = poi.id;
            marker.poiName = poi.name;
            markers.push(marker);
            bounds.push([poi.lat, poi.lng]);
        });

        // Fit map to show all markers
        if (bounds.length > 1) {
            poiMap.fitBounds(bounds, { padding: [50, 50] });
        }
    }

    // Invalidate size after a small delay to ensure proper rendering
    setTimeout(function () {
        poiMap.invalidateSize();
    }, 100);
};

window.centerMapOnPOI = function (lat, lng, name) {
    if (poiMap) {
        poiMap.setView([lat, lng], 17);

        // Find and open the popup for this marker
        markers.forEach(function (marker) {
            if (marker.poiName === name) {
                marker.openPopup();
            }
        });
    }
};

window.addMarkerToMap = function (lat, lng, name, description) {
    if (poiMap) {
        var marker = L.marker([lat, lng])
            .addTo(poiMap)
            .bindPopup('<strong>' + name + '</strong><br>' + description);
        markers.push(marker);
        poiMap.setView([lat, lng], 16);
        marker.openPopup();
    }
};

// ==========================================
// POI Picker Map Functions (for dialog)
// ==========================================

window.initPickerMap = function (elementId, centerLat, centerLng, dotNetReference) {
    dotNetRef = dotNetReference;

    // Destroy existing picker map if any
    if (pickerMap) {
        pickerMap.remove();
        pickerMap = null;
        pickerMarker = null;
    }

    // Initialize picker map
    pickerMap = L.map(elementId, {

        // Giới hạn vùng kéo của bản đồ [[Nam, Tây], [Bắc, Đông]]
        maxBounds: [
            [-90, -180], // Góc Tây Nam (South-West)
            [90, 180]    // Góc Đông Bắc (North-East)
        ],

        // Độ cứng của viền (1.0 = cứng ngắc, kéo chạm viền sẽ không bị nảy ra ngoài)
        maxBoundsViscosity: 1.0
    }).setView([centerLat, centerLng], 15);
    // Add OpenStreetMap tiles
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap',
        maxZoom: 19,
        noWrap: true // Ngăn tile lặp lại theo chiều ngang
    }).addTo(pickerMap);



    // Create draggable marker
    var redIcon = L.icon({
        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png',
        shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
        iconSize: [25, 41],
        iconAnchor: [12, 41],
        popupAnchor: [1, -34],
        shadowSize: [41, 41]
    });

    pickerMarker = L.marker([centerLat, centerLng], {
        draggable: true,
        icon: redIcon
    }).addTo(pickerMap);

    // Update coordinates on marker drag
    pickerMarker.on('dragend', function (e) {
        var position = pickerMarker.getLatLng();
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnLocationPicked', position.lat, position.lng);
        }
    });

    // Click on map to move marker
    pickerMap.on('click', function (e) {
        pickerMarker.setLatLng(e.latlng);
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnLocationPicked', e.latlng.lat, e.latlng.lng);
        }
    });

    // Invalidate size after delay
    setTimeout(function () {
        pickerMap.invalidateSize();
    }, 200);
};

window.updatePickerMarker = function (lat, lng) {
    if (pickerMap && pickerMarker) {
        pickerMarker.setLatLng([lat, lng]);
        pickerMap.setView([lat, lng], 16);
    }
};

window.getCurrentLocation = function (dotNetReference) {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            function (position) {
                dotNetReference.invokeMethodAsync('OnCurrentLocationReceived',
                    position.coords.latitude,
                    position.coords.longitude
                );
            },
            function (error) {
                console.log('Geolocation error:', error);
                alert('Không thể lấy vị trí hiện tại. Vui lòng cho phép truy cập vị trí.');
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 0
            }
        );
    } else {
        alert('Trình duyệt không hỗ trợ Geolocation');
    }
};

window.searchAndMoveToAddress = function (address, dotNetReference) {
    // Use Nominatim (OpenStreetMap) for geocoding - free and no API key needed
    var url = 'https://nominatim.openstreetmap.org/search?format=json&q=' + encodeURIComponent(address);

    fetch(url)
        .then(function (response) {
            return response.json();
        })
        .then(function (data) {
            if (data && data.length > 0) {
                var lat = parseFloat(data[0].lat);
                var lng = parseFloat(data[0].lon);

                if (pickerMap && pickerMarker) {
                    pickerMarker.setLatLng([lat, lng]);
                    pickerMap.setView([lat, lng], 16);
                }

                dotNetReference.invokeMethodAsync('OnLocationPicked', lat, lng);
            } else {
                alert('Không tìm thấy địa chỉ: ' + address);
            }
        })
        .catch(function (error) {
            console.log('Search error:', error);
            alert('Lỗi tìm kiếm địa chỉ');
        });
};
