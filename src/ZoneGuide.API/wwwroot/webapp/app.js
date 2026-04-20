(function () {
    const VISIT_STATE_KEY = "zoneguide-web-poi-visit";
    const SETTINGS_KEY = "zoneguide-web-settings";
    const QR_PRESENCE_SESSION_KEY = "zoneguide-web-qr-session-id";
    const QR_PRESENCE_INTERVAL_MS = 5000;
    const params = new URLSearchParams(window.location.search);
    const initialPoiId = params.get("poiId");
    const settings = loadSettings();
    const autoplayRequested = params.get("autoplay") !== "false" && settings.autoplay;

    let currentPoi = null;
    let currentPoiId = initialPoiId;
    let currentTour = null;
    let geoWatchId = null;
    let currentUserCoords = null;
    let currentMapZoom = 17;
    let currentFilter = "all";
    let allPois = [];
    let filteredPois = [];
    let allTours = [];
    let qrPresenceTimerId = null;
    let qrPresenceActive = false;
    let isListSheetExpanded = false;
    let sheetDragState = null;
    const qrPresenceSessionId = getQrPresenceSessionId();
    const categoryOrder = ["tourism", "service", "food", "entertainment", "drinks", "shopping"];

    const elements = {
        shell: document.querySelector(".app-shell"),
        homePanel: document.getElementById("home-panel"),
        tourPanel: document.getElementById("tour-panel"),
        morePanel: document.getElementById("more-panel"),
        homeSubtitle: document.getElementById("home-subtitle"),
        homeNearbyList: document.getElementById("home-nearby-list"),
        homeTourList: document.getElementById("home-tour-list"),
        homeOpenMap: document.getElementById("home-open-map"),
        homeOpenTours: document.getElementById("home-open-tours"),
        overlay: document.getElementById("poi-overlay"),
        overlayToggle: document.getElementById("overlay-toggle"),
        sheet: document.getElementById("poi-sheet"),
        name: document.getElementById("poi-name"),
        subtitle: document.getElementById("poi-subtitle"),
        address: document.getElementById("poi-address"),
        category: document.getElementById("poi-category"),
        distance: document.getElementById("poi-distance"),
        language: document.getElementById("poi-language"),
        coordinates: document.getElementById("poi-coordinates"),
        triggerRadius: document.getElementById("poi-trigger-radius"),
        approachRadius: document.getElementById("poi-approach-radius"),
        script: document.getElementById("poi-script"),
        image: document.getElementById("poi-image"),
        map: document.getElementById("poi-map"),
        mapsLink: document.getElementById("maps-link"),
        openAppLink: document.getElementById("open-app-link"),
        audio: document.getElementById("poi-audio"),
        playButton: document.getElementById("play-audio-button"),
        autoplayStatus: document.getElementById("autoplay-status"),
        playIndicator: document.getElementById("play-indicator"),
        searchInput: document.getElementById("poi-search-input"),
        chipRow: document.getElementById("chip-row"),
        poiResults: document.getElementById("poi-results"),
        poiListSheet: document.getElementById("poi-list-sheet"),
        poiListToggle: document.getElementById("poi-list-toggle"),
        poiListItems: document.getElementById("poi-list-items"),
        poiListCount: document.getElementById("poi-list-count"),
        poiListTitle: document.getElementById("poi-list-title"),
        tourList: document.getElementById("tour-list"),
        tourDetail: document.getElementById("tour-detail"),
        tourDetailName: document.getElementById("tour-detail-name"),
        tourDetailSummary: document.getElementById("tour-detail-summary"),
        tourDetailPois: document.getElementById("tour-detail-pois"),
        tourBackButton: document.getElementById("tour-back-button"),
        settingAutoplayWeb: document.getElementById("setting-autoplay-web"),
        settingVolumeWeb: document.getElementById("setting-volume-web"),
        moreOpenCurrentPoi: document.getElementById("more-open-current-poi"),
        moreOpenApp: document.getElementById("more-open-app"),
        moreOpenGoogleMaps: document.getElementById("more-open-google-maps"),
        navHome: document.getElementById("nav-home"),
        navMap: document.getElementById("nav-map"),
        navTour: document.getElementById("nav-tour"),
        navMore: document.getElementById("nav-more"),
        mapRecenter: document.getElementById("map-recenter"),
        mapZoomIn: document.getElementById("map-zoom-in"),
        mapZoomOut: document.getElementById("map-zoom-out")
    };

    if (!initialPoiId) {
        renderError("Khong tim thay ma dia diem trong lien ket QR.");
        return;
    }

    bindUi();
    Promise.all([loadPoiList(), loadTourList(), loadPoi(initialPoiId)]).catch(function (error) {
        renderError(error instanceof Error ? error.message : "Khong the tai dia diem.");
    });

    function bindUi() {
        elements.homeOpenMap.addEventListener("click", function () {
            showTab("map");
        });
        elements.homeOpenTours.addEventListener("click", function () {
            showTab("tour");
        });
        elements.overlayToggle.addEventListener("click", toggleSheet);
        elements.playButton.addEventListener("click", function () {
            safePlayAudio(true);
        });
        elements.searchInput.addEventListener("input", renderSearchResults);
        elements.searchInput.addEventListener("focus", function () {
            setListSheetExpanded(true);
            renderSearchResults();
        });
        elements.poiListToggle.addEventListener("click", toggleListSheet);
        elements.poiListSheet.addEventListener("pointerdown", onSheetPointerDown);
        elements.poiListSheet.addEventListener("pointermove", onSheetPointerMove);
        elements.poiListSheet.addEventListener("pointerup", onSheetPointerUp);
        elements.poiListSheet.addEventListener("pointercancel", onSheetPointerUp);

        document.addEventListener("click", function (event) {
            const target = event.target;
            if (!(target instanceof Node)) {
                return;
            }

            if (!elements.poiResults.hidden &&
                !elements.poiResults.contains(target) &&
                !elements.searchInput.contains(target)) {
                hideSearchResults();
            }
        });

        elements.chipRow.addEventListener("click", function (event) {
            const chip = event.target instanceof HTMLElement
                ? event.target.closest("[data-filter]")
                : null;

            if (!chip) {
                return;
            }

            currentFilter = chip.getAttribute("data-filter") || "all";
            updateChipState();
            setListSheetExpanded(true);
            elements.poiListTitle.textContent = getPoiListTitle();
            renderSearchResults();
        });

        elements.mapRecenter.addEventListener("click", function () {
            recenterMap();
            setStatus("Da dua ban do ve lai diem dang chon.");
        });

        elements.mapZoomIn.addEventListener("click", function () {
            currentMapZoom = Math.min(currentMapZoom + 1, 20);
            updateMap();
        });

        elements.mapZoomOut.addEventListener("click", function () {
            currentMapZoom = Math.max(currentMapZoom - 1, 12);
            updateMap();
        });

        elements.navHome.addEventListener("click", function () { showTab("home"); });
        elements.navMap.addEventListener("click", function () { showTab("map"); });
        elements.navTour.addEventListener("click", function () { showTab("tour"); });
        elements.navMore.addEventListener("click", function () { showTab("more"); });
        elements.tourBackButton.addEventListener("click", function () {
            elements.tourDetail.hidden = true;
        });
        elements.settingAutoplayWeb.checked = settings.autoplay;
        elements.settingVolumeWeb.value = settings.volume;
        elements.settingAutoplayWeb.addEventListener("change", function () {
            settings.autoplay = elements.settingAutoplayWeb.checked;
            persistSettings("Da cap nhat tu dong phat.");
        });
        elements.settingVolumeWeb.addEventListener("input", function () {
            settings.volume = Number(elements.settingVolumeWeb.value);
            applyAudioVolume();
            persistSettings();
        });
        elements.moreOpenCurrentPoi.addEventListener("click", function () {
            showTab("map");
        });
        elements.moreOpenApp.addEventListener("click", function () {
            if (currentPoiId) {
                window.location.href = buildAppLink(currentPoiId);
            }
        });
        elements.moreOpenGoogleMaps.addEventListener("click", function () {
            if (elements.mapsLink.href) {
                window.open(elements.mapsLink.href, "_blank", "noopener");
            }
        });
        document.addEventListener("visibilitychange", handleVisibilityChange);
        window.addEventListener("pagehide", stopQrPresence);
        window.addEventListener("beforeunload", stopQrPresence);

        elements.audio.addEventListener("play", function () {
            elements.playIndicator.textContent = "Dang nghe";
            setStatus("Dang phat thuyet minh.");
        });

        elements.audio.addEventListener("pause", function () {
            if (!elements.audio.ended) {
                elements.playIndicator.textContent = "Tiep tuc";
                setStatus("Audio tam dung.");
            }
        });

        elements.audio.addEventListener("ended", function () {
            elements.playIndicator.textContent = "Nghe lai";
            setStatus("Da phat xong thuyet minh.");
        });

        showTab("map");
    }

    async function loadPoiList() {
        const response = await fetch("/api/pois", {
            headers: { Accept: "application/json" }
        });

        if (!response.ok) {
            throw new Error("Khong the tai danh sach dia diem.");
        }

        allPois = await response.json();
        buildCategoryChips(allPois);
        renderSearchResults();
        renderHomeNearby();
    }

    async function loadTourList() {
        const response = await fetch("/api/tours", {
            headers: { Accept: "application/json" }
        });

        if (!response.ok) {
            throw new Error("Khong the tai danh sach tour.");
        }

        allTours = await response.json();
        renderHomeTours();
        renderTours();
    }

    async function loadPoi(id) {
        const response = await fetch(`/api/pois/${encodeURIComponent(id)}`, {
            headers: { Accept: "application/json" }
        });

        if (!response.ok) {
            throw new Error("Khong the tai du lieu dia diem.");
        }

        const poi = await response.json();
        currentPoi = poi;
        currentPoiId = poi.id;
        syncUrl(poi.id);
        renderPoi(poi);
        applyPoiSettings(poi, { resetVisit: false });
        restartAutoplayLifecycle();
        refreshQrPresence();
    }

    function buildCategoryChips(pois) {
        const categorySet = new Set(
            pois
                .map(function (poi) { return normalizeCategoryKey(poi.category); })
                .filter(Boolean)
        );

        const categories = categoryOrder.filter(function (key) {
            return categorySet.has(key);
        });

        elements.chipRow.innerHTML = "";
        elements.chipRow.appendChild(buildChip("Tat ca", "all"));

        categories.forEach(function (categoryKey) {
            elements.chipRow.appendChild(buildChip(getCategoryDisplay(categoryKey), categoryKey));
        });

        updateChipState();
    }

    function buildChip(label, value) {
        const button = document.createElement("button");
        button.className = "filter-chip";
        button.type = "button";
        button.setAttribute("data-filter", value);
        button.innerHTML = `${buildIconMarkup(getCategoryIconId(value))}<span>${escapeHtml(label)}</span>`;
        return button;
    }

    function updateChipState() {
        const chips = elements.chipRow.querySelectorAll("[data-filter]");
        chips.forEach(function (chip) {
            const isActive = (chip.getAttribute("data-filter") || "all") === currentFilter;
            chip.classList.toggle("filter-chip-active", isActive);
        });
    }

    function renderSearchResults() {
        const keyword = normalizeText(elements.searchInput.value);
        elements.poiListTitle.textContent = getPoiListTitle();
        filteredPois = allPois.filter(function (poi) {
            const inCategory = currentFilter === "all" || normalizeCategoryKey(poi.category) === currentFilter;
            const inKeyword = !keyword ||
                normalizeText(poi.name).includes(keyword) ||
                normalizeText(getCategoryDisplay(poi.category)).includes(keyword);
            return inCategory && inKeyword;
        });
        elements.poiResults.hidden = true;
        elements.poiResults.innerHTML = "";
        renderPoiList();
    }

    function hideSearchResults() {
        elements.poiResults.hidden = true;
    }

    function renderPoiList() {
        elements.poiListItems.innerHTML = "";
        elements.poiListCount.textContent = String(filteredPois.length);

        if (!filteredPois.length) {
            const empty = document.createElement("div");
            empty.className = "poi-list-empty";
            empty.textContent = "Khong tim thay dia diem phu hop.";
            elements.poiListItems.appendChild(empty);
            return;
        }

        filteredPois.forEach(function (poi) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = `poi-list-item${String(poi.id) === String(currentPoiId) ? " is-active" : ""}`;
            button.innerHTML = buildPoiListItemMarkup(poi);
            button.addEventListener("click", function () {
                loadPoi(poi.id)
                    .then(function () {
                        setListSheetExpanded(false);
                        setActiveNav("map");
                    })
                    .catch(function (error) {
                        renderError(error instanceof Error ? error.message : "Khong the mo dia diem.");
                    });
            });
            elements.poiListItems.appendChild(button);
        });
    }

    function renderHomeNearby() {
        elements.homeNearbyList.innerHTML = "";
        const items = getNearbyPois(allPois).slice(0, 6);
        elements.homeSubtitle.textContent = items.length
            ? "Bat dau voi cac diem gan ban, sau do chuyen sang ban do de nghe thuyet minh."
            : "Kham pha dia diem va tour ngay tren web, giong flow chinh trong app.";

        items.forEach(function (poi) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "home-poi-card";
            button.innerHTML = `<div class="home-poi-thumb${poi.imageUrl ? "" : " placeholder"}"${poi.imageUrl ? ` style="background-image:url('${escapeAttribute(normalizeMediaUrl(poi.imageUrl))}')"` : ""}>${poi.imageUrl ? "" : "<span>Z</span>"}</div>
                <div class="home-poi-body">
                    <h3>${escapeHtml(poi.name || "Dia diem")}</h3>
                    <p class="eyebrow-line">${buildIconMarkup(getCategoryIconId(poi.category))}<span>${escapeHtml(getCategoryDisplay(poi.category))}</span></p>
                </div>`;
            button.addEventListener("click", function () {
                loadPoi(poi.id).then(function () {
                    showTab("map");
                });
            });
            elements.homeNearbyList.appendChild(button);
        });
    }

    function renderHomeTours() {
        elements.homeTourList.innerHTML = "";
        allTours.slice(0, 4).forEach(function (tour) {
            const card = document.createElement("button");
            card.type = "button";
            card.className = "tour-card-web";
            card.innerHTML = buildTourCardMarkup(tour);
            card.addEventListener("click", function () {
                openTourDetail(tour.id);
                showTab("tour");
            });
            elements.homeTourList.appendChild(card);
        });
    }

    function renderTours() {
        elements.tourList.innerHTML = "";
        allTours.forEach(function (tour) {
            const card = document.createElement("button");
            card.type = "button";
            card.className = "tour-card-web";
            card.innerHTML = buildTourCardMarkup(tour);
            card.addEventListener("click", function () {
                openTourDetail(tour.id);
            });
            elements.tourList.appendChild(card);
        });
    }

    function buildTourCardMarkup(tour) {
        const thumb = tour.thumbnailUrl || tour.imageUrl;
        return `<div class="tour-card-thumb${thumb ? "" : " placeholder"}"${thumb ? ` style="background-image:url('${escapeAttribute(normalizeMediaUrl(thumb))}')"` : ""}>${thumb ? "" : "<span>T</span>"}</div>
            <div class="tour-card-body">
                <h3>${escapeHtml(tour.name || "Tour")}</h3>
                <p class="meta-line">${escapeHtml(tour.description || "Tour gom cac POI noi bat, co the mo tung diem tren ban do web.")}</p>
                <div class="tour-stats">
                    <div class="tour-stat">${escapeHtml(String(tour.pOICount ?? tour.poiCount ?? 0))} diem</div>
                    <div class="tour-stat">${escapeHtml(String(tour.estimatedDurationMinutes || 0))}m</div>
                    <div class="tour-stat">${formatTourDistance(tour)}</div>
                </div>
            </div>`;
    }

    async function openTourDetail(tourId) {
        const response = await fetch(`/api/tours/${encodeURIComponent(tourId)}/details`, {
            headers: { Accept: "application/json" }
        });

        if (!response.ok) {
            setStatus("Khong the tai chi tiet tour.");
            return;
        }

        currentTour = await response.json();
        elements.tourDetail.hidden = false;
        elements.tourDetailName.textContent = currentTour.name || "Chi tiet tour";
        elements.tourDetailSummary.textContent = currentTour.description || "Chon mot POI de mo tren ban do web.";
        elements.tourDetailPois.innerHTML = "";

        (currentTour.pOIs || currentTour.pois || []).forEach(function (poi) {
            const card = document.createElement("button");
            card.type = "button";
            card.className = "tour-poi-card";
            card.innerHTML = `<div class="tour-poi-thumb${poi.imageUrl ? "" : " placeholder"}"${poi.imageUrl ? ` style="background-image:url('${escapeAttribute(normalizeMediaUrl(poi.imageUrl))}')"` : ""}>${poi.imageUrl ? "" : "<span>P</span>"}</div>
                <div class="tour-poi-body">
                    <h3>${escapeHtml(poi.name || "POI")}</h3>
                    <p class="meta-line">${buildIconMarkup(getCategoryIconId(poi.category))}<span>${escapeHtml(getCategoryDisplay(poi.category))}</span></p>
                </div>
                <span class="poi-list-arrow icon-wrap">${buildIconMarkup("icon-chevron-right")}</span>`;
            card.addEventListener("click", function () {
                loadPoi(poi.id).then(function () {
                    showTab("map");
                });
            });
            elements.tourDetailPois.appendChild(card);
        });
    }

    function buildPoiListItemMarkup(poi) {
        const thumbStyle = poi.imageUrl
            ? ` style="background-image:url('${escapeAttribute(normalizeMediaUrl(poi.imageUrl))}')"`
            : "";
        const thumbClass = `poi-list-thumb${poi.imageUrl ? "" : " placeholder"}`;
        const categoryDisplay = getCategoryDisplay(poi.category);
        const distanceText = currentPoiId && String(poi.id) === String(currentPoiId)
            ? "Dang xem"
            : categoryDisplay;

        return `<div class="${thumbClass}"${thumbStyle}>${poi.imageUrl ? "" : "<span>Z</span>"}</div>
            <div class="poi-list-body">
                <p class="poi-list-name">${escapeHtml(poi.name || "Dia diem")}</p>
                <p class="poi-list-meta poi-list-item-icon">${buildIconMarkup(getCategoryIconId(poi.category))}<span>${escapeHtml(distanceText)}</span></p>
            </div>
            <span class="poi-list-arrow icon-wrap" aria-hidden="true">${buildIconMarkup("icon-chevron-right")}</span>`;
    }

    function getPoiListTitle() {
        const hasKeyword = normalizeText(elements.searchInput.value).length > 0;
        if (hasKeyword) {
            return "Ket qua tim kiem";
        }

        if (currentFilter !== "all") {
            return getCategoryDisplay(currentFilter);
        }

        return "Tat ca dia diem";
    }

    function renderPoi(poi) {
        document.title = `${poi.name || "ZoneGuide"} | ZoneGuide`;
        elements.name.textContent = poi.name || "Dia diem khong ro ten";
        elements.subtitle.textContent = "Giao dien web duoc canh chinh lai theo man map cua app.";
        if (elements.address) {
            elements.address.textContent = poi.address || "Dang cap nhat";
        }
        elements.category.textContent = getCategoryDisplay(poi.category);
        elements.poiListTitle.textContent = getPoiListTitle();
        elements.language.textContent = translateLanguageName(poi.language || "vi-VN");
        elements.coordinates.textContent = formatCoordinates(poi.latitude, poi.longitude);
        elements.script.textContent = poi.ttsScript || "Noi dung thuyet minh dang duoc cap nhat.";
        elements.distance.textContent = "Cho cap quyen vi tri de tinh khoang cach";
        elements.mapsLink.href = buildMapsUrl(poi);

        resetImage();
        if (poi.imageUrl) {
            elements.image.classList.remove("placeholder");
            elements.image.style.backgroundImage = `linear-gradient(rgba(15,23,42,.08), rgba(15,23,42,.08)), url("${escapeCssUrl(normalizeMediaUrl(poi.imageUrl))}")`;
            elements.image.textContent = "";
        }

        const audioUrl = normalizeMediaUrl(poi.audioUrl);
        if (audioUrl) {
            elements.audio.src = audioUrl;
            elements.audio.hidden = false;
            elements.playButton.hidden = false;
            elements.playIndicator.textContent = "Nghe";
            setStatus("Audio da san sang.");
        } else {
            elements.audio.removeAttribute("src");
            elements.audio.hidden = true;
            elements.playButton.hidden = true;
            elements.playIndicator.textContent = "Chi tiet";
            setStatus("Dia diem nay hien chua co file audio online.");
        }

        updateMap();
        renderPoiList();
    }

    function resetImage() {
        elements.image.classList.add("placeholder");
        elements.image.style.backgroundImage = "";
        elements.image.textContent = "ZoneGuide";
    }

    function updateMap() {
        if (!currentPoi) {
            return;
        }

        elements.map.src = buildMapEmbedUrl(currentPoi.latitude, currentPoi.longitude, currentMapZoom);
    }

    function recenterMap() {
        currentMapZoom = 17;
        updateMap();
    }

    async function safePlayAudio(fromUserGesture) {
        if (!elements.audio.src) {
            setStatus("Dia diem nay hien chua co file audio online.");
            return;
        }

        try {
            applyAudioVolume();
            await elements.audio.play();
        } catch (error) {
            elements.playIndicator.textContent = "Phat";
            setStatus(fromUserGesture
                ? "Khong the phat audio tren trinh duyet nay."
                : "Trinh duyet chan tu dong phat. Bam Phat audio de nghe.");
        }
    }

    function expandSheet() {
        elements.sheet.hidden = false;
        elements.overlay.classList.add("expanded");
        elements.overlayToggle.setAttribute("aria-expanded", "true");
    }

    function collapseSheet() {
        elements.sheet.hidden = true;
        elements.overlay.classList.remove("expanded");
        elements.overlayToggle.setAttribute("aria-expanded", "false");
    }

    function toggleSheet() {
        if (elements.sheet.hidden) {
            expandSheet();
            return;
        }

        collapseSheet();
    }

    function toggleListSheet() {
        setListSheetExpanded(!isListSheetExpanded);
    }

    function setListSheetExpanded(nextState) {
        isListSheetExpanded = !!nextState;
        elements.shell.classList.toggle("sheet-expanded", isListSheetExpanded);
        elements.poiListToggle.setAttribute("aria-expanded", isListSheetExpanded ? "true" : "false");
        elements.poiListSheet.style.transform = "";
    }

    function onSheetPointerDown(event) {
        if (event.pointerType === "mouse" && event.button !== 0) {
            return;
        }

        const dragZone = event.target instanceof Element
            ? event.target.closest(".poi-list-handle, .poi-list-header")
            : null;

        if (!dragZone) {
            return;
        }

        sheetDragState = {
            pointerId: event.pointerId,
            startY: event.clientY,
            wasExpanded: isListSheetExpanded
        };
        elements.poiListSheet.setPointerCapture(event.pointerId);
        elements.poiListSheet.classList.add("is-dragging");
    }

    function onSheetPointerMove(event) {
        if (!sheetDragState || sheetDragState.pointerId !== event.pointerId) {
            return;
        }

        const deltaY = event.clientY - sheetDragState.startY;
        const maxTranslate = getSheetCollapsedOffset();
        const baseTranslate = sheetDragState.wasExpanded ? 0 : maxTranslate;
        const nextTranslate = clamp(baseTranslate + deltaY, 0, maxTranslate);
        elements.poiListSheet.style.transform = `translateY(${nextTranslate}px)`;
    }

    function onSheetPointerUp(event) {
        if (!sheetDragState || sheetDragState.pointerId !== event.pointerId) {
            return;
        }

        if (elements.poiListSheet.hasPointerCapture(event.pointerId)) {
            elements.poiListSheet.releasePointerCapture(event.pointerId);
        }

        const deltaY = event.clientY - sheetDragState.startY;
        const shouldExpand = sheetDragState.wasExpanded
            ? deltaY < 72
            : deltaY < -72;

        elements.poiListSheet.classList.remove("is-dragging");
        elements.poiListSheet.style.transform = "";
        setListSheetExpanded(shouldExpand);
        sheetDragState = null;
    }

    function getSheetCollapsedOffset() {
        const styles = window.getComputedStyle(elements.poiListSheet);
        const height = parseFloat(styles.height) || 0;
        const peek = parseFloat(getComputedStyle(elements.shell).getPropertyValue("--poi-sheet-peek")) || 206;
        return Math.max(0, height - peek);
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function getNearbyPois(pois) {
        const items = Array.isArray(pois) ? [...pois] : [];
        if (currentUserCoords) {
            return items.sort(function (a, b) {
                return calculateDistanceMeters(currentUserCoords.latitude, currentUserCoords.longitude, a.latitude, a.longitude) -
                    calculateDistanceMeters(currentUserCoords.latitude, currentUserCoords.longitude, b.latitude, b.longitude);
            });
        }

        return items.sort(function (a, b) {
            return String(a.name || "").localeCompare(String(b.name || ""), "vi");
        });
    }

    function formatTourDistance(tour) {
        const km = Number(tour.distanceKm);
        if (Number.isFinite(km) && km > 0) {
            return `${km.toFixed(1)} km`;
        }

        return "Route";
    }

    function setActiveNav(section) {
        elements.navHome.classList.toggle("nav-item-active", section === "home");
        elements.navMap.classList.toggle("nav-item-active", section === "map");
        elements.navTour.classList.toggle("nav-item-active", section === "tour");
        elements.navMore.classList.toggle("nav-item-active", section === "more");
    }

    function showTab(tab) {
        elements.homePanel.hidden = tab !== "home";
        elements.tourPanel.hidden = tab !== "tour";
        elements.morePanel.hidden = tab !== "more";
        if (tab === "map") {
            collapseSheet();
            setListSheetExpanded(false);
        }
        setActiveNav(tab);
    }

    function syncUrl(id) {
        const nextUrl = new URL(window.location.href);
        nextUrl.searchParams.set("poiId", id);
        nextUrl.searchParams.set("autoplay", autoplayRequested ? "true" : "false");
        window.history.replaceState({}, "", nextUrl);
        elements.openAppLink.href = buildAppLink(id);
    }

    function buildAppLink(id) {
        const appLink = `zoneguide://poi/${encodeURIComponent(id)}?autoplay=true`;
        return isAndroid() ? buildIntentLink(id) : appLink;
    }

    function renderError(message) {
        document.title = "ZoneGuide | Loi mo QR";
        elements.name.textContent = "Khong mo duoc dia diem";
        elements.category.textContent = "Loi tai du lieu";
        if (elements.address) {
            elements.address.textContent = message;
        }
        elements.subtitle.textContent = "Vui long kiem tra lai ma QR hoac tinh trang ket noi.";
        elements.language.textContent = "Khong co du lieu";
        elements.coordinates.textContent = "Khong co du lieu";
        elements.script.textContent = "Khong the tai noi dung dia diem.";
        elements.playIndicator.textContent = "Loi";
        elements.audio.hidden = true;
        elements.playButton.hidden = true;
        expandSheet();
        setStatus("Khong the phat audio.");
    }

    function applyPoiSettings(poi, options) {
        const trigger = getEffectiveTriggerRadius(poi);
        const approach = getEffectiveApproachRadius(poi, trigger);
        settings.triggerRadius = Math.round(trigger);
        settings.approachRadius = Math.round(approach);
        persistSettings();
        elements.triggerRadius.textContent = `${Math.round(trigger)}m`;
        elements.approachRadius.textContent = `${Math.round(approach)}m`;

        if (options && options.resetVisit) {
            clearVisitState();
        }
    }

    function restartAutoplayLifecycle() {
        if (geoWatchId !== null && navigator.geolocation) {
            navigator.geolocation.clearWatch(geoWatchId);
            geoWatchId = null;
        }

        if (!currentPoi) {
            return;
        }

        initAutoplayLifecycle(currentPoi);
    }

    function handleVisibilityChange() {
        if (document.visibilityState === "visible") {
            refreshQrPresence();
            return;
        }

        stopQrPresence();
    }

    function refreshQrPresence() {
        if (!currentPoiId || document.visibilityState !== "visible") {
            stopQrPresence();
            return;
        }

        sendQrPresenceHeartbeat();

        if (qrPresenceTimerId !== null) {
            window.clearInterval(qrPresenceTimerId);
        }

        qrPresenceTimerId = window.setInterval(function () {
            if (document.visibilityState === "visible") {
                sendQrPresenceHeartbeat();
                return;
            }

            stopQrPresence();
        }, QR_PRESENCE_INTERVAL_MS);
    }

    function sendQrPresenceHeartbeat() {
        if (!currentPoiId) {
            return;
        }

        qrPresenceActive = true;
        fetch("/api/qr-monitoring/presence", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                Accept: "application/json"
            },
            credentials: "same-origin",
            keepalive: true,
            body: JSON.stringify({
                sessionId: qrPresenceSessionId,
                poiId: Number(currentPoiId)
            })
        }).catch(function () {
            // Ignore transient QR presence errors on the public web page.
        });
    }

    function stopQrPresence() {
        if (qrPresenceTimerId !== null) {
            window.clearInterval(qrPresenceTimerId);
            qrPresenceTimerId = null;
        }

        if (!qrPresenceActive) {
            return;
        }

        qrPresenceActive = false;
        const payload = JSON.stringify({
            sessionId: qrPresenceSessionId,
            poiId: Number(currentPoiId || 0)
        });

        if (navigator.sendBeacon) {
            const blob = new Blob([payload], { type: "application/json" });
            navigator.sendBeacon("/api/qr-monitoring/presence/stop", blob);
            return;
        }

        fetch("/api/qr-monitoring/presence/stop", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                Accept: "application/json"
            },
            credentials: "same-origin",
            keepalive: true,
            body: payload
        }).catch(function () {
            // Ignore transient QR stop errors on unload/background transitions.
        });
    }

    function initAutoplayLifecycle(poi) {
        if (!navigator.geolocation) {
            if (autoplayRequested && settings.autoplay && !hasPlayedCurrentVisit(currentPoiId)) {
                markVisitPlayed(currentPoiId);
                expandSheet();
                safePlayAudio(false);
            }

            setStatus("Trinh duyet khong ho tro vi tri. Web se khong theo doi viec ra vao vung.");
            return;
        }

        const geoOptions = {
            enableHighAccuracy: true,
            maximumAge: 5000,
            timeout: 12000
        };

        navigator.geolocation.getCurrentPosition(function (position) {
            handleLocationUpdate(position, poi);
        }, function () {
            setStatus("Chua lay duoc vi tri. Ban van co the bam Phat audio thu cong.");
        }, geoOptions);

        geoWatchId = navigator.geolocation.watchPosition(function (position) {
            handleLocationUpdate(position, poi);
        }, function () {
            setStatus("Khong theo doi duoc vi tri lien tuc. Web se khong tu phat lai khi ban quay lai.");
        }, geoOptions);
    }

    function handleLocationUpdate(position, poi) {
        const latitude = position.coords.latitude;
        const longitude = position.coords.longitude;
        currentUserCoords = { latitude: latitude, longitude: longitude };
        renderHomeNearby();
        const distanceMeters = calculateDistanceMeters(latitude, longitude, poi.latitude, poi.longitude);
        const trigger = getEffectiveTriggerRadius(poi);
        const approach = getEffectiveApproachRadius(poi, trigger);
        const visitState = loadVisitState();

        elements.distance.textContent = `${formatDistance(distanceMeters)} tu vi tri hien tai`;

        if (distanceMeters > approach) {
            if (visitState && String(visitState.poiId) === String(currentPoiId)) {
                const outsideSince = visitState.outsideSince || Date.now();
                saveVisitState({
                    poiId: currentPoiId,
                    played: true,
                    outsideSince: outsideSince,
                    updatedAt: Date.now()
                });

                if (Date.now() - outsideSince >= 3000) {
                    clearVisitState();
                    setStatus("Ban da ra khoi vung. Quay lai diem nay de web tu phat audio mot lan nua.");
                } else {
                    setStatus("Dang roi khoi vung, doi mot chut de reset tu dong phat.");
                }
            } else {
                setStatus("Ban dang o ngoai vung kich hoat.");
            }

            return;
        }

        if (visitState && String(visitState.poiId) === String(currentPoiId) && visitState.outsideSince) {
            saveVisitState({
                poiId: currentPoiId,
                played: visitState.played === true,
                outsideSince: null,
                updatedAt: Date.now()
            });
        }

        if (distanceMeters <= trigger) {
            if (autoplayRequested && settings.autoplay && !hasPlayedCurrentVisit(currentPoiId)) {
                markVisitPlayed(currentPoiId);
                expandSheet();
                safePlayAudio(false);
            } else if (hasPlayedCurrentVisit(currentPoiId)) {
                setStatus("Diem nay da tu dong phat 1 lan trong lan vao vung hien tai.");
            } else {
                setStatus("Ban dang o trong vung kich hoat. Bam Phat audio neu muon nghe ngay.");
            }
            return;
        }

        if (hasPlayedCurrentVisit(currentPoiId)) {
            setStatus("Ban van dang trong vung, web se khong tu phat lai cho den khi ban ra khoi diem nay.");
        } else {
            setStatus("Ban dang o vung canh bao. Di gan hon de web tu phat audio.");
        }
    }

    function buildMapsUrl(poi) {
        if (poi.mapLink) {
            return poi.mapLink;
        }

        if (typeof poi.latitude === "number" && typeof poi.longitude === "number") {
            return `https://www.google.com/maps/search/?api=1&query=${poi.latitude},${poi.longitude}`;
        }

        return "#";
    }

    function buildMapEmbedUrl(latitude, longitude, zoom) {
        if (typeof latitude !== "number" || typeof longitude !== "number") {
            return "";
        }

        const effectiveZoom = Math.max(12, Math.min(20, Number(zoom) || 17));
        const delta = 0.14 / Math.pow(2, effectiveZoom - 12);
        const left = longitude - delta;
        const right = longitude + delta;
        const top = latitude + delta;
        const bottom = latitude - delta;

        return `https://www.openstreetmap.org/export/embed.html?bbox=${left},${bottom},${right},${top}&layer=mapnik&marker=${latitude},${longitude}`;
    }

    function normalizeMediaUrl(value) {
        if (!value || typeof value !== "string") {
            return "";
        }

        if (/^https?:\/\//i.test(value)) {
            return value;
        }

        if (value.startsWith("/")) {
            return value;
        }

        return `/${value.replace(/^\/+/, "")}`;
    }

    function formatCoordinates(latitude, longitude) {
        if (typeof latitude !== "number" || typeof longitude !== "number") {
            return "Dang cap nhat";
        }

        return `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
    }

    function escapeCssUrl(value) {
        return String(value).replace(/"/g, '\\"');
    }

    function buildIntentLink(id) {
        const fallbackUrl = new URL(window.location.href);
        return `intent://poi/${encodeURIComponent(id)}?autoplay=true#Intent;scheme=zoneguide;package=com.ZoneGuide.app;S.browser_fallback_url=${encodeURIComponent(fallbackUrl.toString())};end`;
    }

    function getQrPresenceSessionId() {
        try {
            let sessionId = window.sessionStorage.getItem(QR_PRESENCE_SESSION_KEY);
            if (!sessionId) {
                sessionId = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
                window.sessionStorage.setItem(QR_PRESENCE_SESSION_KEY, sessionId);
            }

            return sessionId;
        } catch (error) {
            return crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
        }
    }

    function isAndroid() {
        return /android/i.test(navigator.userAgent || "");
    }

    function loadSettings() {
        try {
            const raw = window.localStorage.getItem(SETTINGS_KEY);
            if (!raw) {
                return buildDefaultSettings();
            }

            const parsed = JSON.parse(raw);
            return {
                autoplay: parsed.autoplay !== false,
                volume: normalizeNumber(parsed.volume, 100),
                triggerRadius: normalizeNumber(parsed.triggerRadius, 50),
                approachRadius: normalizeNumber(parsed.approachRadius, 100)
            };
        } catch (error) {
            return buildDefaultSettings();
        }
    }

    function buildDefaultSettings() {
        return {
            autoplay: true,
            volume: 100,
            triggerRadius: 50,
            approachRadius: 100
        };
    }

    function persistSettings(message) {
        window.localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
        if (message) {
            setStatus(message);
        }
    }

    function applyAudioVolume() {
        elements.audio.volume = Math.max(0, Math.min(1, settings.volume / 100));
    }

    function translateLanguageName(languageCode) {
        const normalized = String(languageCode || "vi-VN").toLowerCase();
        if (normalized.startsWith("vi")) return "Tieng Viet";
        if (normalized.startsWith("en")) return "English";
        if (normalized.startsWith("ja")) return "Nhat Ban";
        if (normalized.startsWith("ko")) return "Han Quoc";
        if (normalized.startsWith("zh")) return "Trung Quoc";
        if (normalized.startsWith("fr")) return "Francais";
        return languageCode || "vi-VN";
    }

    function getEffectiveTriggerRadius(poi) {
        const fromPoi = normalizeNumber(poi.triggerRadius || poi.triggerRadiusMeters, settings.triggerRadius);
        return Math.max(20, fromPoi || settings.triggerRadius);
    }

    function getEffectiveApproachRadius(poi, triggerRadius) {
        const fromPoi = normalizeNumber(poi.approachRadius, settings.approachRadius);
        return Math.max(triggerRadius, fromPoi || Math.max(triggerRadius * 2, settings.approachRadius));
    }

    function setStatus(message) {
        elements.autoplayStatus.textContent = message;
    }

    function loadVisitState() {
        try {
            const raw = window.localStorage.getItem(VISIT_STATE_KEY);
            return raw ? JSON.parse(raw) : null;
        } catch (error) {
            return null;
        }
    }

    function saveVisitState(state) {
        window.localStorage.setItem(VISIT_STATE_KEY, JSON.stringify(state));
    }

    function clearVisitState() {
        window.localStorage.removeItem(VISIT_STATE_KEY);
    }

    function hasPlayedCurrentVisit(targetPoiId) {
        const state = loadVisitState();
        return !!state && String(state.poiId) === String(targetPoiId) && state.played === true;
    }

    function markVisitPlayed(targetPoiId) {
        saveVisitState({
            poiId: targetPoiId,
            played: true,
            outsideSince: null,
            updatedAt: Date.now()
        });
    }

    function calculateDistanceMeters(lat1, lon1, lat2, lon2) {
        if (typeof lat1 !== "number" || typeof lon1 !== "number" || typeof lat2 !== "number" || typeof lon2 !== "number") {
            return Number.POSITIVE_INFINITY;
        }

        const earthRadius = 6371000;
        const toRad = Math.PI / 180;
        const dLat = (lat2 - lat1) * toRad;
        const dLon = (lon2 - lon1) * toRad;
        const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
            Math.cos(lat1 * toRad) * Math.cos(lat2 * toRad) *
            Math.sin(dLon / 2) * Math.sin(dLon / 2);
        const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
        return earthRadius * c;
    }

    function formatDistance(distanceMeters) {
        if (!Number.isFinite(distanceMeters)) {
            return "Khong ro";
        }

        if (distanceMeters < 1000) {
            return `${Math.round(distanceMeters)}m`;
        }

        return `${(distanceMeters / 1000).toFixed(1)}km`;
    }

    function normalizeNumber(value, fallback) {
        const numeric = Number(value);
        return Number.isFinite(numeric) && numeric > 0 ? numeric : fallback;
    }

    function normalizeCategoryKey(category) {
        const normalized = normalizeText(category);
        switch (normalized) {
            case "hai san & oc":
            case "seafood & snails":
            case "seafood":
            case "du lich":
            case "tourism":
                return "tourism";
            case "an vat":
            case "snacks":
            case "snack":
            case "dich vu":
            case "service":
            case "services":
                return "service";
            case "lau & nuong":
            case "hotpot & grill":
            case "hotpot":
            case "grill":
            case "an uong":
            case "food":
            case "food & drink":
                return "food";
            case "nhau":
            case "drinking":
            case "pub":
            case "giai tri":
            case "entertainment":
                return "entertainment";
            case "giai khat":
            case "beverage":
            case "beverages":
            case "drinks":
                return "drinks";
            case "an no":
            case "hearty meals":
            case "main meal":
            case "mua sam":
            case "shopping":
            case "khac":
            case "other":
            case "":
                return "shopping";
            default:
                return normalized || "shopping";
        }
    }

    function getCategoryDisplay(category) {
        switch (normalizeCategoryKey(category)) {
            case "tourism":
                return "Hai san & oc";
            case "service":
                return "An vat";
            case "food":
                return "Lau & nuong";
            case "entertainment":
                return "Nhau";
            case "drinks":
                return "Giai khat";
            case "shopping":
                return "An no";
            default:
                return category || "An no";
        }
    }

    function getCategoryIconId(category) {
        switch (normalizeCategoryKey(category)) {
            case "all":
                return "icon-all";
            case "tourism":
                return "icon-tourism";
            case "service":
                return "icon-service";
            case "food":
                return "icon-food";
            case "entertainment":
                return "icon-entertainment";
            case "drinks":
                return "icon-drinks";
            case "shopping":
                return "icon-shopping";
            default:
                return "icon-shopping";
        }
    }

    function buildIconMarkup(iconId) {
        return `<svg class="icon"><use href="#${escapeAttribute(String(iconId || "").replace(/^#/, ""))}"/></svg>`;
    }

    function normalizeText(value) {
        return String(value || "")
            .trim()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "");
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function escapeAttribute(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/'/g, "&#39;");
    }
})();
