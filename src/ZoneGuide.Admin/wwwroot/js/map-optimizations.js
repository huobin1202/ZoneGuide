// ==========================================
// Map Optimization Utilities
// ==========================================
// Performance enhancements for Leaflet maps

/**
 * Deferred Map Initialization
 * Only initialize maps when they become visible in viewport
 * Reduces initial page load time
 */
var deferredMapInitializers = [];

window.registerDeferredMapInit = function(elementId, initFunction) {
    if (!elementId || !initFunction) return;
    
    var element = document.getElementById(elementId);
    if (!element) return;
    
    // Check if element is already visible
    if (isElementInViewport(element)) {
        initFunction();
        return;
    }
    
    // Use Intersection Observer to defer initialization
    if ('IntersectionObserver' in window) {
        var observer = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    initFunction();
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.1 });
        
        observer.observe(element);
    } else {
        // Fallback: initialize after delay
        setTimeout(initFunction, 500);
    }
};

function isElementInViewport(el) {
    if (!el) return false;
    var rect = el.getBoundingClientRect();
    return (
        rect.top <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.left <= (window.innerWidth || document.documentElement.clientWidth) &&
        rect.bottom >= 0 &&
        rect.right >= 0
    );
}

/**
 * Tile Layer Caching Enhancement
 * Add better cache control for tile requests
 */
window.createOptimizedTileLayer = function(url, options) {
    options = options || {};
    options.maxZoom = options.maxZoom || 19;
    options.attribution = options.attribution || '&copy; OpenStreetMap contributors';
    options.noWrap = options.noWrap !== false;
    
    // Add cache-control headers for better browser caching
    options.crossOrigin = 'anonymous';
    
    return L.tileLayer(url, options);
};

/**
 * Lazy Load Popup Content
 * Load detailed popup content only when popup is opened
 */
window.createLazyPopup = function(marker, summaryHtml, detailedContent) {
    marker.bindPopup(summaryHtml);
    
    marker.on('popupopen', function() {
        if (detailedContent && typeof detailedContent === 'function') {
            var content = detailedContent();
            var popup = marker.getPopup();
            if (popup) {
                popup.setContent(summaryHtml + content);
            }
        }
    });
    
    return marker;
};

/**
 * Batch Marker Rendering
 * Add markers in batches to avoid blocking UI
 */
window.addMarkersInBatches = function(markers, clusterGroup, batchSize, onProgress) {
    batchSize = batchSize || 50;
    var index = 0;
    
    function addBatch() {
        var endIndex = Math.min(index + batchSize, markers.length);
        
        for (var i = index; i < endIndex; i++) {
            clusterGroup.addLayer(markers[i]);
        }
        
        index = endIndex;
        
        if (onProgress) {
            onProgress(index, markers.length);
        }
        
        if (index < markers.length) {
            requestAnimationFrame(addBatch);
        }
    }
    
    addBatch();
};

/**
 * Image Lazy Loading in Popups
 * Optimize popup images with lazy loading
 */
window.optimizePopupImages = function(popupContent) {
    if (!popupContent) return '';
    
    // Replace img tags with lazy loading
    return popupContent.replace(
        /<img\s+src="([^"]*)"\s+alt="([^"]*)"/g,
        function(match, src, alt) {
            return '<img loading="lazy" src="' + src + '" alt="' + alt + '" decoding="async"';
        }
    );
};

/**
 * Progressive Map Enhancement
 * Load map features progressively as user interacts
 */
window.createProgressiveMap = function(mapElement, initialZoom, center) {
    var map = L.map(mapElement, {
        maxBounds: [[-90, -180], [90, 180]],
        maxBoundsViscosity: 1.0,
        preferCanvas: true // Use canvas rendering for better performance
    }).setView(center, initialZoom);
    
    // Add tile layer with optimization
    window.createOptimizedTileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19
    }).addTo(map);
    
    return map;
};

/**
 * Debounce map pan/zoom handlers
 * Prevent excessive re-renders during user interaction
 */
window.debounceMapEvent = function(map, eventName, handler, delay) {
    delay = delay || 300;
    var timeout;
    
    map.on(eventName, function(e) {
        clearTimeout(timeout);
        timeout = setTimeout(function() {
            handler(e);
        }, delay);
    });
};

/**
 * Marker Density Reduction
 * For maps with too many markers, show only relevant ones based on zoom level
 */
window.createAdaptiveMarkerLayer = function(markers, clusterGroup) {
    var markersByZoom = {};
    
    // Group markers by relevance levels
    markers.forEach(function(marker) {
        var relevance = marker.relevance || 5;
        if (!markersByZoom[relevance]) {
            markersByZoom[relevance] = [];
        }
        markersByZoom[relevance].push(marker);
    });
    
    return {
        updateVisibility: function(currentZoom) {
            Object.keys(markersByZoom).forEach(function(relevance) {
                var shouldShow = currentZoom >= relevance;
                markersByZoom[relevance].forEach(function(marker) {
                    if (shouldShow && !clusterGroup.hasLayer(marker)) {
                        clusterGroup.addLayer(marker);
                    } else if (!shouldShow && clusterGroup.hasLayer(marker)) {
                        clusterGroup.removeLayer(marker);
                    }
                });
            });
        }
    };
};

console.log('Map optimization utilities loaded');

// Global error handlers to help debug client-side issues (captures errors during search/heatmap)
window.addEventListener('error', function (event) {
    try {
        console.error('Global error captured:', event.message, event.error || event.filename + ':' + event.lineno);
    } catch (e) {
        console.error('Error while logging global error', e);
    }
});

window.addEventListener('unhandledrejection', function (event) {
    try {
        console.error('Unhandled promise rejection:', event.reason);
    } catch (e) {
        console.error('Error while logging rejection', e);
    }
});
