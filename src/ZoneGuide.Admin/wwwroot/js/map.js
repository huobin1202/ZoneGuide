// POI Map Functions using Leaflet
var poiMap = null;
var markers = [];
var tempMarker = null;
var searchResultMarker = null;

// Picker map for dialog
var pickerMap = null;
var pickerMarker = null;
var dotNetRef = null;

window.initPOIMap = function (elementId, centerLat, centerLng, poiData, dotNetReference) {
    // Destroy existing map if any
    if (poiMap) {
        poiMap.remove();
        poiMap = null;
    }
    markers = [];
    searchResultMarker = null;
    dotNetRef = dotNetReference;

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

        var CustomIcon = L.Icon.extend({
            options: {
                shadowUrl: '/images/markers/marker-shadow.png',
                iconSize: [25, 41],
                iconAnchor: [12, 41],
                popupAnchor: [1, -34],
                shadowSize: [41, 41]
            }
        });

        // Use standard Leaflet colors from local offline files
        var foodIcon = new CustomIcon({iconUrl: '/images/markers/food-dish-svgrepo-com.svg'});
        var entertainmentIcon = new CustomIcon({iconUrl: '/images/markers/marker-icon-2x-yellow.png'});
        var travelIcon = new CustomIcon({iconUrl: '/images/markers/marker-icon-2x-green.png'});
        var servicesIcon = new CustomIcon({iconUrl: '/images/markers/marker-icon-2x-blue.png'});
        var shoppingIcon = new CustomIcon({iconUrl: '/images/markers/marker-icon-2x-orange.png'});
        var otherIcon = new CustomIcon({iconUrl: '/images/markers/marker-icon-2x-gray.png'});

        function getCustomIcon(category) {
            switch((category || '').toLowerCase()) {
                case 'food':
                case 'ăn uống':
                case 'an uong':
                    return foodIcon;
                case 'entertainment':
                case 'giải trí':
                case 'giai tri':
                    return entertainmentIcon;
                case 'travel':
                case 'du lịch':
                case 'du lich':
                    return travelIcon;
                case 'services':
                case 'dịch vụ':
                case 'dich vu':
                    return servicesIcon;
                case 'shopping':
                case 'mua sắm':
                case 'mua sam':
                    return shoppingIcon;
                case 'other':
                case 'khác':
                case 'khac':
                    return otherIcon;
                default:
                    return otherIcon; // Mặc định cho các loại khác hoặc không xác định
            }
        }

        poiData.forEach(function (poi) {
            var fallbackImageUrl = '/images/placeholder.png';
            var imageUrl = (poi.imageUrl && poi.imageUrl.trim()) ? poi.imageUrl : fallbackImageUrl;

            var marker = L.marker([poi.lat, poi.lng], { icon: getCustomIcon(poi.category) })
                .addTo(poiMap)
                .bindPopup(
                    '<div style="width: 250px; max-width: 250px; padding: 0; overflow: hidden;">' +
                    '<img src="' + imageUrl + '" onerror="this.onerror=null;this.src=\'' + fallbackImageUrl + '\';" style="width: 100%; height: 120px; object-fit: cover; border-top-left-radius: 8px; border-top-right-radius: 8px; margin-bottom: 8px; display: block;" />' +
                    '<div style="padding: 4px 8px 8px 8px;">' +
                    '<h4 style="margin: 0 0 6px 0; color: #333; font-size: 16px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">' + poi.name + '</h4>' +
                    '<div style="display: flex; align-items: center; margin-bottom: 8px;">' +
                        '<span style="background-color: #E3F2FD; color: #1976D2; padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500;">' +
                            '🏛 ' + poi.category + 
                        '</span>' +
                    '</div>' +
                    '<p style="margin: 0 0 8px 0; font-size: 13px; color: #555; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">' + 
                    (poi.description ? poi.description : 'Không có mô tả') + '</p>' +
                    '<p style="margin: 0; font-size: 12px; color: #888;">' +
                    '📍 ' + poi.lat.toFixed(6) + ', ' + poi.lng.toFixed(6) + '</p>' +
                    '</div></div>', {
                        className: 'custom-poi-popup',
                        minWidth: 250
                    }
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

    poiMap.on('click', function(e) {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnMapClicked', e.latlng.lat, e.latlng.lng);
        }
    });

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

window.addTempMarkerToPOIMap = function (lat, lng, dontChangeView) {
    if (poiMap) {
        if (tempMarker) {
            tempMarker.setLatLng([lat, lng]);
        } else {
            var redIcon = L.icon({
                iconUrl: '/images/markers/marker-icon-2x-red.png',
                shadowUrl: '/images/markers/marker-shadow.png',
                iconSize: [25, 41],
                iconAnchor: [12, 41],
                popupAnchor: [1, -34],
                shadowSize: [41, 41]
            });
            tempMarker = L.marker([lat, lng], {
                draggable: true,
                icon: redIcon
            }).addTo(poiMap);

            tempMarker.on('dragend', function (e) {
                var position = tempMarker.getLatLng();
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnMapClicked', position.lat, position.lng);
                }
            });
        }
        
        if (!dontChangeView) {
            poiMap.setView([lat, lng], 16);
        }
    }
};

window.removeTempMarkerFromPOIMap = function () {
    if (tempMarker && poiMap) {
        poiMap.removeLayer(tempMarker);
        tempMarker = null;
    }
};

function showSearchResultMarker(lat, lng, displayName) {
    if (!poiMap) {
        return;
    }

    if (!searchResultMarker) {
        var purpleIcon = L.icon({
            iconUrl: '/images/markers/marker-icon-2x-violet.png',
            shadowUrl: '/images/markers/marker-shadow.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34],
            shadowSize: [41, 41]
        });

        searchResultMarker = L.marker([lat, lng], { icon: purpleIcon }).addTo(poiMap);
    } else {
        searchResultMarker.setLatLng([lat, lng]);
    }

    searchResultMarker.bindPopup('<b>Tìm kiếm</b><br />' + displayName).openPopup();
    poiMap.setView([lat, lng], 16);
}

window.showSearchResultOnMainMap = function (lat, lng, displayName) {
    showSearchResultMarker(lat, lng, displayName || 'Vị trí đã chọn');
};

// ==========================================
// Search on main POI map (admin & contributor)
// ==========================================
window.searchAddressOnMainMap = function (address) {
    if (!poiMap) {
        return;
    }

    if (!address || !address.trim()) {
        alert('Vui lòng nhập địa chỉ hoặc tên địa danh.');
        return;
    }

    var url = 'https://nominatim.openstreetmap.org/search?format=json&limit=1&q=' + encodeURIComponent(address.trim());

    fetch(url, {
        headers: {
            'Accept-Language': 'vi',
            'User-Agent': 'ZoneGuide/1.0 (map search)'
        }
    })
        .then(function (response) { return response.json(); })
        .then(function (data) {
            if (!data || data.length === 0) {
                alert('Không tìm thấy địa điểm phù hợp.');
                return;
            }

            var lat = parseFloat(data[0].lat);
            var lng = parseFloat(data[0].lon);
            var displayName = data[0].display_name || address;
            showSearchResultMarker(lat, lng, displayName);
        })
        .catch(function (error) {
            console.error('Search error:', error);
            alert('Lỗi tìm kiếm địa chỉ.');
        });
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
        iconUrl: '/images/markers/marker-icon-2x-red.png',
        shadowUrl: '/images/markers/marker-shadow.png',
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
