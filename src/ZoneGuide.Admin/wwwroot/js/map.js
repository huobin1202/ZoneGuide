// POI Map Functions using Leaflet
var poiMap = null;
var markers = [];
var markerByPoiId = {};
var tempMarker = null;
var tempRadiusCircle = null;
var searchResultMarker = null;

// Picker map for dialog
var pickerMap = null;
var pickerMarker = null;
var dotNetRef = null;

window.initPOIMap = function (elementId, centerLat, centerLng, poiData, dotNetReference, readOnlyMode, focusPoiId) {
    // Destroy existing map if any
    if (poiMap) {
        poiMap.remove();
        poiMap = null;
    }
    markers = [];
    markerByPoiId = {};
    searchResultMarker = null;
    dotNetRef = dotNetReference || null;
    var isReadOnlyMode = readOnlyMode === true;

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
        var contributionIcon = new CustomIcon({iconUrl: '/images/markers/marker-icon-2x-red.png'});

        function getCustomIcon(category, isContribution) {
            if (isContribution) {
                return contributionIcon;
            }

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
            var escapedId = String(poi.id).replace(/'/g, "\\'");
            var editSvg = '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false"><path fill="currentColor" d="M3 17.25V21h3.75l11-11.03-3.75-3.75L3 17.25zm17.71-10.04a1.003 1.003 0 0 0 0-1.42L18.21 3.29a1.003 1.003 0 0 0-1.42 0l-1.96 1.96 3.75 3.75 2.13-1.79z"/></svg>';
            var deleteSvg = '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false"><path fill="currentColor" d="M6 19c0 1.1.9 2 2 2h8a2 2 0 0 0 2-2V7H6v12zm3.46-7.12 1.41-1.41L12 11.59l1.12-1.12 1.41 1.41L13.41 13l1.12 1.12-1.41 1.41L12 14.41l-1.12 1.12-1.41-1.41L10.59 13l-1.13-1.12zM15.5 4l-1-1h-5l-1 1H5v2h14V4z"/></svg>';
            var actionButtons = '';
            if (!isReadOnlyMode && !poi.isContribution) {
                actionButtons =
                    '<button type="button" title="Chỉnh sửa" onclick="window.handlePoiPopupEdit(event,\'' + escapedId + '\')" style="width:32px;height:32px;border:1px solid rgba(255,255,255,0.75);border-radius:999px;background:rgba(255,255,255,0.92);backdrop-filter:blur(6px);color:#37474f;display:inline-flex;align-items:center;justify-content:center;cursor:pointer;box-shadow:0 6px 16px rgba(15,23,42,0.24);">' + editSvg + '</button>' +
                    '<button type="button" title="Xóa" onclick="window.handlePoiPopupDelete(event,\'' + escapedId + '\')" style="width:32px;height:32px;border:1px solid rgba(255,255,255,0.75);border-radius:999px;background:rgba(255,255,255,0.92);backdrop-filter:blur(6px);color:#d32f2f;display:inline-flex;align-items:center;justify-content:center;cursor:pointer;box-shadow:0 6px 16px rgba(15,23,42,0.24);">' + deleteSvg + '</button>';
            }

            var actionBar = actionButtons
                ? '<div style="position:absolute;top:0;right:10px;display:flex;gap:8px;align-items:center;z-index:4;padding:6px 8px;border-radius:999px;background:rgba(255,255,255,0.98);box-shadow:0 8px 20px rgba(15,23,42,0.18);">' + actionButtons + '</div>'
                : '';

            var marker = L.marker([poi.lat, poi.lng], { icon: getCustomIcon(poi.category, poi.isContribution) })
                .addTo(poiMap)
                .bindPopup(
                    '<div style="width: 250px; max-width: 250px; padding: 0; overflow: hidden;">' +
                    '<div style="position:relative;padding-top:14px;margin-bottom:8px;">' +
                    actionBar +
                    '<img src="' + imageUrl + '" onerror="this.onerror=null;this.src=\'' + fallbackImageUrl + '\';" style="width: 100%; height: 120px; object-fit: cover; border-top-left-radius: 8px; border-top-right-radius: 8px; display: block;" />' +
                    '</div>' +
                    '<div style="padding: 4px 8px 8px 8px;">' +
                    '<h4 style="margin:0 0 6px 0;color:#333;font-size:16px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">' + poi.name + '</h4>' +
                    '<div style="display: flex; align-items: center; margin-bottom: 8px;">' +
                        '<span style="background-color: #E3F2FD; color: #1976D2; padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500;">' +
                            '🏛 ' + poi.category + 
                        '</span>' +
                    '</div>' +
                    '<p style="margin: 0 0 8px 0; font-size: 13px; color: #555; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">' + 
                    (poi.description ? poi.description : 'Chưa có nội dung thuyết minh (TTS)') + '</p>' +
                    '<p style="margin: 0; font-size: 12px; color: #888; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">' +
                    '📍 ' + (poi.address ? poi.address : (poi.lat.toFixed(6) + ', ' + poi.lng.toFixed(6))) + '</p>' +
                    '</div></div>', {
                        className: 'custom-poi-popup',
                        minWidth: 250,
                        closeButton: false
                    }
                );

            if (poi.triggerRadius && poi.triggerRadius > 0) {
                L.circle([poi.lat, poi.lng], {
                    radius: poi.triggerRadius,
                    color: '#4caf50',
                    weight: 1.5,
                    opacity: 0.65,
                    fillColor: '#81c784',
                    fillOpacity: 0.08
                }).addTo(poiMap);
            }

            marker.poiId = poi.id;
            marker.poiName = poi.name;
            marker.on('click', function () {
                if (dotNetRef && !isReadOnlyMode) {
                    dotNetRef.invokeMethodAsync('OnPoiMarkerSelected', String(poi.id));
                }
            });
            markers.push(marker);
            markerByPoiId[String(poi.id)] = marker;
            bounds.push([poi.lat, poi.lng]);
        });

        // Fit map to show all markers
        if (bounds.length > 1) {
            poiMap.fitBounds(bounds, { padding: [50, 50] });
        }
    }

    if (dotNetRef && !isReadOnlyMode) {
        poiMap.on('click', function(e) {
            dotNetRef.invokeMethodAsync('OnMapClicked', e.latlng.lat, e.latlng.lng);
        });
    }

    // Invalidate size after a small delay to ensure proper rendering
    setTimeout(function () {
        poiMap.invalidateSize();
        if (focusPoiId) {
            var focusMarker = markerByPoiId[String(focusPoiId)];
            if (focusMarker) {
                if (!poiData || poiData.length <= 1) {
                    poiMap.setView(focusMarker.getLatLng(), 17);
                }
                focusMarker.openPopup();
            }
        }
    }, 100);
};

window.centerMapOnPOI = function (poiId, lat, lng) {
    if (poiMap) {
        poiMap.setView([lat, lng], 17);
        var marker = markerByPoiId[String(poiId)];
        if (marker) {
            marker.openPopup();
        }
    }
};

window.handlePoiPopupEdit = function (evt, poiId) {
    if (evt) {
        evt.preventDefault();
        evt.stopPropagation();
    }
    if (poiMap) {
        poiMap.closePopup();
    }
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnPoiMarkerEditRequested', String(poiId));
    }
};

window.handlePoiPopupDelete = function (evt, poiId) {
    if (evt) {
        evt.preventDefault();
        evt.stopPropagation();
    }
    if (poiMap) {
        poiMap.closePopup();
    }
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnPoiMarkerDeleteRequested', String(poiId));
    }
};

window.scrollPoiCardIntoView = function (poiId) {
    var card = document.getElementById('poi-card-' + poiId);
    if (card) {
        card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
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

function updateTempRadiusCircle(lat, lng, radiusMeters) {
    if (!poiMap) {
        return;
    }

    if (!radiusMeters || radiusMeters <= 0) {
        if (tempRadiusCircle) {
            poiMap.removeLayer(tempRadiusCircle);
            tempRadiusCircle = null;
        }
        return;
    }

    if (!tempRadiusCircle) {
        tempRadiusCircle = L.circle([lat, lng], {
            radius: radiusMeters,
            color: '#1976d2',
            weight: 2,
            opacity: 0.9,
            fillColor: '#42a5f5',
            fillOpacity: 0.18
        }).addTo(poiMap);
    } else {
        tempRadiusCircle.setLatLng([lat, lng]);
        tempRadiusCircle.setRadius(radiusMeters);
    }
}

window.addTempMarkerToPOIMap = function (lat, lng, dontChangeView, radiusMeters) {
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
                if (tempRadiusCircle) {
                    tempRadiusCircle.setLatLng(position);
                }
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnMapClicked', position.lat, position.lng);
                }
            });
        }

        updateTempRadiusCircle(lat, lng, radiusMeters);
        
        if (!dontChangeView) {
            poiMap.setView([lat, lng], 16);
        }
    }
};

window.updateTempRadiusPreviewOnPOIMap = function (radiusMeters) {
    if (tempMarker) {
        var position = tempMarker.getLatLng();
        updateTempRadiusCircle(position.lat, position.lng, radiusMeters);
    }
};

window.removeTempMarkerFromPOIMap = function () {
    if (tempMarker && poiMap) {
        poiMap.removeLayer(tempMarker);
        tempMarker = null;
    }
    if (tempRadiusCircle && poiMap) {
        poiMap.removeLayer(tempRadiusCircle);
        tempRadiusCircle = null;
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

window.reverseGeocodeAddress = async function (lat, lng) {
    try {
        var url = 'https://nominatim.openstreetmap.org/reverse?format=json&lat=' +
            encodeURIComponent(String(lat)) +
            '&lon=' +
            encodeURIComponent(String(lng));

        var response = await fetch(url, {
            headers: {
                'Accept-Language': 'vi'
            }
        });

        if (!response.ok) {
            return '';
        }

        var data = await response.json();
        return data && data.display_name ? data.display_name : '';
    } catch (error) {
        console.warn('Reverse geocode failed', error);
        return '';
    }
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

// ==========================================
// Tour planner routing map
// ==========================================
var tourPlannerMap = null;
var tourPlannerMarkers = [];
var tourPlannerRouteLayer = null;
var tourPlannerFallbackLayer = null;
var tourPlannerTileLayer = null;
var tourPlannerElementId = null;

function ensureTourPlannerMap(elementId, points) {
    var fallbackCenter = [16.047079, 108.20623];

    if (points && points.length > 0) {
        fallbackCenter = [points[0].lat, points[0].lng];
    }

    if (tourPlannerMap && tourPlannerElementId !== elementId) {
        tourPlannerMap.remove();
        tourPlannerMap = null;
        tourPlannerMarkers = [];
        tourPlannerRouteLayer = null;
        tourPlannerFallbackLayer = null;
        tourPlannerTileLayer = null;
    }

    if (!tourPlannerMap) {
        tourPlannerMap = L.map(elementId, {
            zoomControl: true,
            maxBounds: [[-90, -180], [90, 180]],
            maxBoundsViscosity: 1.0
        }).setView(fallbackCenter, points && points.length > 1 ? 6 : 12);

        tourPlannerTileLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors',
            maxZoom: 19,
            noWrap: true
        }).addTo(tourPlannerMap);

        tourPlannerElementId = elementId;
    }

    setTimeout(function () {
        if (tourPlannerMap) {
            tourPlannerMap.invalidateSize();
        }
    }, 120);
}

function clearTourPlannerLayers() {
    tourPlannerMarkers.forEach(function (marker) {
        if (tourPlannerMap) {
            tourPlannerMap.removeLayer(marker);
        }
    });
    tourPlannerMarkers = [];

    if (tourPlannerRouteLayer && tourPlannerMap) {
        tourPlannerMap.removeLayer(tourPlannerRouteLayer);
    }
    tourPlannerRouteLayer = null;

    if (tourPlannerFallbackLayer && tourPlannerMap) {
        tourPlannerMap.removeLayer(tourPlannerFallbackLayer);
    }
    tourPlannerFallbackLayer = null;
}

function getTourMarkerColor(index) {
    var palette = ['#2563eb', '#7c3aed', '#db2777', '#ea580c', '#16a34a', '#0891b2', '#4f46e5'];
    return palette[index % palette.length];
}

function createTourMarkerIcon(order, color) {
    return L.divIcon({
        className: 'tour-planner-pin-wrapper',
        html:
            '<div style="' +
            'width:44px;height:44px;border-radius:999px;background:' + color + ';' +
            'border:4px solid #ffffff;color:#ffffff;font-weight:800;font-size:22px;' +
            'display:flex;align-items:center;justify-content:center;' +
            'box-shadow:0 8px 18px rgba(15,23,42,.28);">' + order + '</div>',
        iconSize: [44, 44],
        iconAnchor: [22, 22],
        popupAnchor: [0, -18]
    });
}

function haversineDistanceKm(lat1, lng1, lat2, lng2) {
    var toRadians = Math.PI / 180;
    var dLat = (lat2 - lat1) * toRadians;
    var dLng = (lng2 - lng1) * toRadians;
    var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
        Math.cos(lat1 * toRadians) * Math.cos(lat2 * toRadians) *
        Math.sin(dLng / 2) * Math.sin(dLng / 2);
    var c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return 6371 * c;
}

function calculateStraightDistanceKm(points) {
    var total = 0;
    for (var i = 1; i < points.length; i++) {
        total += haversineDistanceKm(points[i - 1].lat, points[i - 1].lng, points[i].lat, points[i].lng);
    }
    return total;
}

function renderFallbackRoute(points) {
    var latLngs = points.map(function (point) { return [point.lat, point.lng]; });
    if (latLngs.length >= 2) {
        tourPlannerFallbackLayer = L.polyline(latLngs, {
            color: '#2563eb',
            weight: 4,
            opacity: 0.75,
            dashArray: '10 8',
            lineJoin: 'round'
        }).addTo(tourPlannerMap);
        tourPlannerMap.fitBounds(tourPlannerFallbackLayer.getBounds(), { padding: [40, 40] });
    } else if (latLngs.length === 1) {
        tourPlannerMap.setView(latLngs[0], 12);
    }
}

async function fetchDrivingRoute(points) {
    if (!points || points.length < 2) {
        return null;
    }

    var coordinates = points
        .map(function (point) { return point.lng + ',' + point.lat; })
        .join(';');
    var url = 'https://router.project-osrm.org/route/v1/driving/' + coordinates + '?overview=full&geometries=geojson&steps=false';

    try {
        var response = await fetch(url);
        if (!response.ok) {
            return null;
        }

        var data = await response.json();
        if (!data || !data.routes || data.routes.length === 0) {
            return null;
        }

        return data.routes[0];
    } catch (error) {
        console.warn('Tour routing fetch failed', error);
        return null;
    }
}

window.renderTourPlannerMap = async function (elementId, points) {
    ensureTourPlannerMap(elementId, points || []);
    clearTourPlannerLayers();

    points = points || [];

    var straightDistanceKm = calculateStraightDistanceKm(points);
    var defaultResult = {
        distanceKm: straightDistanceKm,
        durationMinutes: points.length >= 2 ? Math.round((straightDistanceKm / 35) * 60) : 0,
        straightDistanceKm: straightDistanceKm,
        usedRouting: false
    };

    points.forEach(function (point, index) {
        var marker = L.marker([point.lat, point.lng], {
            icon: createTourMarkerIcon(index + 1, getTourMarkerColor(index))
        })
            .addTo(tourPlannerMap)
            .bindPopup('<strong>' + point.name + '</strong><br/>Điểm dừng #' + (index + 1));

        tourPlannerMarkers.push(marker);
    });

    if (points.length === 0) {
        tourPlannerMap.setView([16.047079, 108.20623], 6);
        return defaultResult;
    }

    if (points.length === 1) {
        tourPlannerMap.setView([points[0].lat, points[0].lng], 12);
        return defaultResult;
    }

    var route = await fetchDrivingRoute(points);
    if (route && route.geometry && route.geometry.coordinates) {
        var latLngs = route.geometry.coordinates.map(function (coordinate) {
            return [coordinate[1], coordinate[0]];
        });

        tourPlannerRouteLayer = L.polyline(latLngs, {
            color: '#2563eb',
            weight: 5,
            opacity: 0.9,
            lineJoin: 'round'
        }).addTo(tourPlannerMap);

        tourPlannerMap.fitBounds(tourPlannerRouteLayer.getBounds(), { padding: [40, 40] });

        return {
            distanceKm: route.distance / 1000,
            durationMinutes: Math.round(route.duration / 60),
            straightDistanceKm: straightDistanceKm,
            usedRouting: true
        };
    }

    renderFallbackRoute(points);
    return defaultResult;
};
