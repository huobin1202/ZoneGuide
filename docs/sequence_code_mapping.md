# Mapping Sequence -> Code

Tai lieu nay map tung message trong 3 sequence diagram vao code hien tai de de doi chieu.
## 1. Quet QR dia diem va tu phat audio

- `Quet ma QR dia diem`
  - Code:
    ```csharp
    private void OnBarcodesDetected(object? sender, object e)
    ```
  - Vi tri: [QRScannerPage.xaml.cs](../src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs)
  - Y nghia: callback cua ZXing khi camera nhan duoc barcode.

- `Doc payload QR va tach poiId`
  - Code:
    ```csharp
    text = text.Trim();
    ApiService.TrySetPreferredBaseUrlFromQrPayload(text);

    if (!TryExtractPoiId(text, out var poiId))
        return;
    ```
  - Vi tri: [QRScannerPage.xaml.cs](../src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs)
  - Y nghia: payload duoc chuan hoa, co the cap nhat base URL backend, sau do tach `poiId`.

- `QR hop le`
  - Code:
    ```csharp
    _handled = true;
    _reader.IsDetecting = false;
    _ = NavigateToPoiAsync(poiId);
    ```
  - Vi tri: [QRScannerPage.xaml.cs](../src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs)
  - Y nghia: scanner dung quet tiep va chuyen sang man hinh map.

- `QR khong hop le`
  - Code:
    ```csharp
    if (!TryExtractPoiId(text, out var poiId))
        return;
    ```
  - Vi tri: [QRScannerPage.xaml.cs](../src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs)
  - Y nghia: QRPage bo qua payload khong tach duoc `poiId`.

- `Mo map voi poiId + autoplay=true`
  - Code:
    ```csharp
    await Shell.Current.GoToAsync($"//map?poiId={poiId}&autoplay=true");
    ```
  - Vi tri: [QRScannerPage.xaml.cs](../src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs)
  - Y nghia: QR chi dieu huong; viec focus POI va phat audio duoc xu ly tiep o `MapPage`/`MapViewModel`.

- `Map nhan query QR`
  - Code:
    ```csharp
    _focusPoiIdRequestedOnNavigation = TryGetIntQueryValue(query, "poiId");
    _autoPlayRequestedOnNavigation = TryGetBoolQueryValue(query, "autoplay");
    ```
  - Vi tri: [MapPage.xaml.cs](../src/ZoneGuide.Mobile/Views/MapPage.xaml.cs)
  - Y nghia: `MapPage` doc request focus POI va autoplay tu deep link/QR navigation.

- `Tim POI theo poiId`
  - Code:
    ```csharp
    var poi = _allPOIs.FirstOrDefault(p => p.Id == poiId)
              ?? POIs.FirstOrDefault(p => p.Id == poiId)
              ?? await _poiRepository.GetByIdAsync(poiId);
    ```
  - Vi tri: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs)
  - Y nghia: uu tien tim trong cache hien co, sau do moi doc local repository.

- `Neu thieu du lieu local thi sync server roi lay lai POI`
  - Code:
    ```csharp
    if (shouldRefreshFromServer)
    {
        await _syncService.SyncFromServerAsync();
        poi = await _poiRepository.GetByIdAsync(poiId);
    }
    ```
  - Vi tri: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs)
  - Y nghia: flow QR co quyen yeu cau refresh server khi POI/audio chua san sang o local.

- `Chon POI can mo`
  - Code:
    ```csharp
    SelectPoiCore(poi, revealOverlayInTourMode: false);
    ```
  - Vi tri: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs)
  - Y nghia: dat `SelectedPOI`, doi `MapSpan`, mo overlay cua POI.

- `PlayImmediatelyAsync(item)`
  - Code:
    ```csharp
    var item = BuildNarrationItem(SelectedPOI, GeofenceEventType.Enter, 0);
    item.IsManualPlayback = true;
    await _narrationService.PlayImmediatelyAsync(item);
    ```
  - Vi tri: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs)
  - Y nghia: autoplay QR duoc treat nhu manual playback de tranh geofence chen ngang.

- `Co audio`
  - Code:
    ```csharp
    await _audioService.PlayAsync(item.AudioPath)
    await _audioService.PlayFromUrlAsync(item.AudioUrl)
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)
  - Y nghia: neu co offline/online audio thi uu tien phat audio that.

- `Khong co audio`
  - Code:
    ```csharp
    if (!played && !string.IsNullOrWhiteSpace(item.TTSText))
    {
        await PlayTtsAndWaitAsync(item, _cancellationTokenSource.Token);
    }
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)
  - Y nghia: neu khong co audio hoac audio loi thi fallback sang TTS.

- `NarrationStarted`
  - Code:
    ```csharp
    NarrationStarted?.Invoke(this, item);
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)
  - Y nghia: ViewModel/UI nhan event de hien thi dang phat.

- `Hien thi POI va tu phat audio`
  - Code:
    ```csharp
    if (_autoPlayRequestedOnNavigation && focused && _viewModel.PlaySelectedPOICommand.CanExecute(null))
    {
        await _viewModel.PlaySelectedPOICommand.ExecuteAsync(null);
    }
    ```
  - Vi tri: [MapPage.xaml.cs](../src/ZoneGuide.Mobile/Views/MapPage.xaml.cs)
  - Y nghia: sau khi focus dung POI, `MapPage` kich autoplay de nguoi dung thay POI va nghe audio ngay.

- `NarrationCompleted`
  - Code:
    ```csharp
    NarrationCompleted?.Invoke(this, item);
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)
  - Y nghia: UI co the cap nhat trang thai da phat xong.

- `Cap nhat trang thai hoan tat`
  - Code:
    ```csharp
    private void OnNarrationCompleted(object? sender, NarrationQueueItem item)
    ```
  - Vi tri: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs)
  - Y nghia: `MapViewModel` clear lock va cap nhat lai state cho overlay/miniplayer.
           

















## 2. Monitoring app mobile

- `Mo app`
  - Code:
    ```csharp
    window.Created += async (...) => await _mobilePresenceService.StartAsync();
    ```
  - Vi tri: [App.xaml.cs](../src/ZoneGuide.Mobile/App.xaml.cs)

- `Bao tin hieu bat dau cho api`
  - Code:
    ```csharp
    await SendHeartbeatAsync();
    ```
  - Vi tri: [MobilePresenceService.cs](../src/ZoneGuide.Mobile/Services/MobilePresenceService.cs)
  - Y nghia: ngay khi bat dau session, app gui heartbeat dau tien len API.

- `Gui tin hieu dinh ky (moi 5 giay)`
  - Code:
    ```csharp
    _heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(), null, HeartbeatIntervalMs, HeartbeatIntervalMs);
    ```
  - Vi tri: [MobilePresenceService.cs](../src/ZoneGuide.Mobile/Services/MobilePresenceService.cs)

- `Dang ky hoac cap nhat phien`
  - Code:
    ```csharp
    _sessions.AddOrUpdate(...)
    ```
  - Vi tri: [MobileLiveMonitoringService.cs](../src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs)
  - Y nghia: session moi duoc tao, session cu duoc cap nhat state.

- `Day trang thai hien tai / moi neu thay doi`
  - Code:
    ```csharp
    await _hubContext.Clients.All.SendAsync("MobileMonitorUpdated", snapshot);
    ```
  - Vi tri: [MobileLiveMonitoringService.cs](../src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs)

- `Bang dieu khien admin nhan trang thai`
  - Code:
    ```csharp
    _mobileHubConnection.On<MobileLiveMonitoringSnapshotDto>("MobileMonitorUpdated", snapshot => ...)
    ```
  - Vi tri: [Index.razor](../src/ZoneGuide.Admin/Pages/Index.razor)

- `Gui offline`
  - Code:
    ```csharp
    await _apiService.UploadMobileOfflineAsync(sessionIdToClose);
    ```
  - Vi tri: [MobilePresenceService.cs](../src/ZoneGuide.Mobile/Services/MobilePresenceService.cs)

- `Huy dang ky phien`
  - Code:
    ```csharp
    _sessions.TryRemove(sessionId.Trim(), out _);
    ```
  - Vi tri: [MobileLiveMonitoringService.cs](../src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs)


















## 3. Nhieu du khach cung nghe audio - Hang doi

- `Moi thiet bi co hang doi rieng`
  - Code:
    ```csharp
    builder.Services.AddSingleton<INarrationService, NarrationService>();
    ```
  - Vi tri: [MauiProgram.cs](../src/ZoneGuide.Mobile/MauiProgram.cs)
  - Y nghia: moi app instance tren moi thiet bi co 1 `NarrationService` rieng.

- `Them vao hang doi hoac phat ngay`
  - Code:
    ```csharp
    public Task EnqueueAsync(...)
    public async Task PlayImmediatelyAsync(...)
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)

- `Kiem tra trung lap POI.Id`
  - Code:
    ```csharp
    if (_currentItem?.POI.Id == item.POI.Id || _queue.Any(q => q.POI.Id == item.POI.Id))
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)

- `Khong trung lap -> dua vao queue rieng cua thiet bi`
  - Code:
    ```csharp
    _queue.Enqueue(item);
    EnsureQueueProcessingStarted();
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)

- `Trung lap -> bo qua yeu cau`
  - Code:
    ```csharp
    return Task.CompletedTask;
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)

- `Phat AudioPath hoac AudioUrl`
  - Code:
    ```csharp
    await _audioService.PlayAsync(item.AudioPath)
    await _audioService.PlayFromUrlAsync(item.AudioUrl)
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)

- `Loi audio -> chuyen sang doc bang TTS`
  - Code:
    ```csharp
    catch (...) when (... && !string.IsNullOrWhiteSpace(item.TTSText))
    {
        await PlayTtsAndWaitAsync(item, _cancellationTokenSource.Token);
    }
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)



























## 4. Xu ly trung khi dung giua 2 POI

- `Di chuyen vao vung giao nhau`
  - Nguon su kien la cap nhat vi tri, sau do `MapViewModel`/`GeofenceService` xu ly.
  - Tham chieu: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs), [GeofenceService.cs](../src/ZoneGuide.Mobile/Services/GeofenceService.cs)

- `Xu ly cap nhat vi tri`
  - Code: `await _geofenceService.ProcessLocationUpdateAsync(location);`
  - Vi tri: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs)
  - Y nghia: `MapViewModel` day vi tri moi vao `GeofenceService`.

- `Tinh khoang cach den tung POI`
  - Code:
    ```csharp
    var distance = location.DistanceTo(poi.Latitude, poi.Longitude);
    ```
  - Vi tri: [GeofenceService.cs](../src/ZoneGuide.Mobile/Services/GeofenceService.cs)
  - Y nghia: moi POI dang monitor deu duoc tinh khoang cach tu vi tri hien tai.

- `Tao danh sach su kien Enter hop le`
  - Code:
    ```csharp
    if (newState.HasValue)
    {
        events.Add(new GeofenceEvent { ... });
    }
    ```
  - Vi tri: [GeofenceService.cs](../src/ZoneGuide.Mobile/Services/GeofenceService.cs)
  - Y nghia: chi cac POI vuot qua dieu kien state/cooldown/debounce moi duoc dua vao danh sach event.

- `Co nhieu Enter cung luc`
  - Code:
    ```csharp
    var enterEvents = events.Where(e => e.EventType == GeofenceEventType.Enter).ToList();
    ```
  - Vi tri: [GeofenceService.cs](../src/ZoneGuide.Mobile/Services/GeofenceService.cs)

- `Sap xep theo Priority ... / neu cung Priority thi chon Distance nho hon`
  - Code:
    ```csharp
    .OrderByDescending(x => x.FinalScore)
    .ThenBy(x => x.Event.Distance)
    .First();
    ```
  - Vi tri: [GeofenceService.cs](../src/ZoneGuide.Mobile/Services/GeofenceService.cs)
  - Y nghia: code hien tai dung `FinalScore` thong qua `PoiScoringService`, trong do `Priority` va `Distance` la 2 thanh phan quan trong.

- `Phat POI duoc chon`
  - Code:
    ```csharp
    GeofenceTriggered?.Invoke(this, bestEnter.Event);
    ```
  - Vi tri: [GeofenceService.cs](../src/ZoneGuide.Mobile/Services/GeofenceService.cs)
  - Y nghia: service chi fire 1 event Enter tot nhat.

- `Phat ngay voi POI duoc chon`
  - Code:
    ```csharp
    await _narrationService.PlayImmediatelyAsync(BuildNarrationItem(
        evt.POI,
        evt.EventType,
        evt.Distance));
    ```
  - Vi tri: [MapViewModel.cs](../src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs)
  - Y nghia: `MapViewModel` nhan event geofence roi goi `NarrationService`.

- `Neu trung POI dang phat/da trong queue thi bo qua`
  - Code:
    ```csharp
    if (_currentItem?.POI.Id == item.POI.Id || _queue.Any(q => q.POI.Id == item.POI.Id))
    {
        return;
    }
    ```
  - Vi tri: [NarrationService.cs](../src/ZoneGuide.Mobile/Services/NarrationService.cs)
