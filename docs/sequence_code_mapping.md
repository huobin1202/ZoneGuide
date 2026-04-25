# Mapping Sequence -> Code

Tai lieu nay map tung message trong 5 sequence diagram vao code hien tai.

Nguyen tac doc:
- `ViewModel/UI` trong sequence la participant tong hop. Thuc te co nhieu call site cung goi `INarrationService`.
- Mot message co the map vao 1 method hoac 1 block code ngan, neu sequence dang tom gon nhieu dong code thanh 1 y.
- Cac line number ben duoi duoc doi chieu theo code hien tai trong repo.

## 1. QR scan -> mo map -> autoplay narration

Sequence: `docs/plantuml/qr_scan_sequence.puml`

| Message trong sequence | Code khop | Vi tri code | Ghi chu |
| --- | --- | --- | --- |
| `User -> QRScannerPage: OnBarcodesDetected(results)` | `OnBarcodesDetected(object? sender, object e)` | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:121-155` | Day la callback scanner khi camera nhan duoc barcode. |
| `QRScannerPage -> QRScannerPage: lay text QR dau tien khong rong` | vong `foreach (var r in results)` + `valueProp` + `if (!string.IsNullOrWhiteSpace(text)) break;` | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:126-139` | Sequence gom phan doc barcode dau tien hop le. |
| `QRScannerPage -> ApiService: TrySetPreferredBaseUrlFromQrPayload(text)` | `ApiService.TrySetPreferredBaseUrlFromQrPayload(text);` | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:141-143`, `src/ZoneGuide.Mobile/Services/ApiService.cs:315-338` | Co the doi preferred API base URL theo payload QR. |
| `QRScannerPage -> QRScannerPage: bo qua payload / reader tiep tuc detecting` | `if (!TryExtractPoiId(text, out var poiId)) return;` | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:145-146` | Code khong tat reader trong nhanh QR khong hop le. |
| `QRScannerPage -> QRScannerPage: _handled = true / _reader.IsDetecting = false` | `_handled = true; if (_reader != null) _reader.IsDetecting = false;` | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:148-150` | Danh dau QR da duoc xu ly va dung detect tiep. |
| `QRScannerPage -> Shell: GoToAsync("//map?...") / fallback "map?..."` | `NavigateToPoiAsync(int poiId)` voi `Shell.Current.GoToAsync(...)` 2 lan | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:205-227` | Code thu route tuyet doi truoc, fail thi fallback sang route tuong doi. |
| `QRScannerPage -> User: DisplayAlert("Khong mo duoc trang POI", ...)` | `await DisplayAlert(...)` | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:221-227` | Chi chay khi ca 2 route deu that bai. |
| `QRScannerPage -> QRScannerPage: _handled = false / _reader.IsDetecting = true` | `_handled = false; if (_reader != null) _reader.IsDetecting = true;` | `src/ZoneGuide.Mobile/Views/QRScannerPage.xaml.cs:223-226` | Bat scan lai sau khi dieu huong that bai. |
| `Shell -> MapPage: ApplyQueryAttributes(query)` | `public void ApplyQueryAttributes(IDictionary<string, object> query)` | `src/ZoneGuide.Mobile/Views/MapPage.xaml.cs:316-408` | `Shell` goi callback cua `MapPage` khi route/query duoc apply. |
| `MapPage -> MapViewModel: FocusPOIByIdAsync(poiId, allowServerSync: true)` | `focused = await _viewModel.FocusPOIByIdAsync(..., allowServerSync: _autoPlayRequestedOnNavigation);` | `src/ZoneGuide.Mobile/Views/MapPage.xaml.cs:250-253`, `350-353`, `384-387`; `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:845-891` | `allowServerSync` se la `true` khi query co `autoplay=true`. |
| `MapViewModel -> MapViewModel: SyncFromServerAsync() / poi = GetByIdAsync(poiId)` | `await _syncService.SyncFromServerAsync(); poi = await _poiRepository.GetByIdAsync(poiId);` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:854-870` | Chay khi POI chua co local hoac audio source can refresh. |
| `MapViewModel --> MapPage: focused = false` | `if (poi == null) return false;` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:872-873` | `MapPage` dua vao bool tra ve de quyet dinh autoplay hay khong. |
| `MapViewModel -> MapViewModel: SelectPoiCore(poi, revealOverlayInTourMode: false)` | `SelectPoiCore(poi, revealOverlayInTourMode: false);` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:811-840`, `889-891` | Day la doan focus POI that su len map/overlay. |
| `MapViewModel --> MapPage: focused = true` | `return true;` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:891` | Tra ve cho `MapPage` biet da focus thanh cong. |
| `MapPage -> MapViewModel: PlaySelectedPOICommand.ExecuteAsync(null)` | `await _viewModel.PlaySelectedPOICommand.ExecuteAsync(null);` | `src/ZoneGuide.Mobile/Views/MapPage.xaml.cs:355-357`, `389-391`, `255-257` | QR autoplay duoc kick tu `MapPage` sau khi focus xong. |
| `MapViewModel -> NarrationService: PlayImmediatelyAsync(item)` | `await _narrationService.PlayImmediatelyAsync(item);` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1051-1055` | `item.IsManualPlayback = true` de tranh geofence chen ngang. |
| `NarrationService --> MapViewModel: skip duplicate` | `if (_currentItem?.POI.Id == item.POI.Id || _queue.Any(...)) return;` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:86-93` | Service tu chan duplicate ngay dau vao. |
| `NarrationService -> NarrationService: StopAsync() / ClearQueue() / _queue.Enqueue(item) / EnsureQueueProcessingStarted()` | block `PlayImmediatelyAsync(item)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:95-109` | Sequence gom cac buoc uu tien phat ngay vao 1 message. |
| `NarrationService -> AudioService: PlayAsync(item.AudioPath)` | `await PlayAudioAndWaitAsync(() => _audioService.PlayAsync(item.AudioPath), ...)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:301-315` | Chi chay khi co `AudioPath` local va file ton tai. |
| `NarrationService -> AudioService: PlayFromUrlAsync(item.AudioUrl)` | `await PlayAudioAndWaitAsync(() => _audioService.PlayFromUrlAsync(item.AudioUrl), ...)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:317-331` | Nhanh online audio. |
| `NarrationService -> TTSService: SpeakAsync(item.TTSText, item.Language)` | `await PlayTtsAndWaitAsync(item, ...)` -> `await _ttsService.SpeakAsync(item.TTSText, item.Language);` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:334-337`, `397-407` | Chay khi khong co audio hoac fallback sang TTS. |
| `NarrationService -> TTSService: SpeakAsync(item.TTSText, item.Language)` trong nhanh fallback | `catch (...) when (... && !string.IsNullOrWhiteSpace(item.TTSText)) { await PlayTtsAndWaitAsync(item, ...); }` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:311-314`, `327-330` | Day la nhanh audio loi nhung con TTSText. |
| `NarrationService --> MapViewModel: NarrationCompleted(item)` | `NarrationCompleted?.Invoke(this, item);` + `MapViewModel` subscribe bang `OnNarrationStateChanged` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:339-354`; `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:214-219`, `2005-2008` | Mapping cu truoc day tro nham vao `OnNarrationCompleted`; subscription that su hien tai la `OnNarrationStateChanged`. |

Note theo sequence moi:
- Sequence nay da duoc sua de phan anh dung nhanh fallback route trong `NavigateToPoiAsync`.
- Message `Shell -> MapPage: ApplyQueryAttributes(query)` la callback cua framework, nhung van co method code ro rang trong `MapPage.xaml.cs`.

## 2. Mobile monitoring realtime

Sequence: `docs/plantuml/mobile_monitoring_sequence.puml`

| Message trong sequence | Code khop | Vi tri code | Ghi chu |
| --- | --- | --- | --- |
| `User -> App: mo app / quay lai foreground` | `window.Created`, `window.Activated`, `window.Resumed` | `src/ZoneGuide.Mobile/App.xaml.cs:42-57` | Cac event vong doi nay deu goi `StartAsync()`. |
| `App -> MobilePresenceService: StartAsync()` | `await _mobilePresenceService.StartAsync();` | `src/ZoneGuide.Mobile/App.xaml.cs:46-55` | App bat mobile monitoring khi tao window va khi foreground lai. |
| `MobilePresenceService -> MobilePresenceService: LoadAsync() / GetAnonymousDeviceIdAsync() / sessionId = ... / start Timer(5000ms)` | block khoi tao trong `StartAsync()` | `src/ZoneGuide.Mobile/Services/MobilePresenceService.cs:43-69`, `160-169` | Sequence gom nhom buoc lifecycle thanh 1 message. |
| `MobilePresenceService -> MobileApiService: UploadMobileHeartbeatAsync(dto)` | `await _apiService.UploadMobileHeartbeatAsync(new MobileLiveHeartbeatDto { ... })` | `src/ZoneGuide.Mobile/Services/MobilePresenceService.cs:116-148`; `src/ZoneGuide.Mobile/Services/ApiService.cs:599-635` | Chay 1 lan ngay sau khi start va lap lai moi 5 giay. |
| `MobileApiService -> MobileMonitoringController: POST /api/mobile-monitoring/heartbeat` | `var heartbeatUri = new Uri(new Uri(baseUrl), "mobile-monitoring/heartbeat");` + `HttpMethod.Post` | `src/ZoneGuide.Mobile/Services/ApiService.cs:605-617`; `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:19-33` | Client goi endpoint heartbeat de dang ky/cap nhat session. |
| `MobileMonitoringController -> MobileLiveMonitoringService: RegisterHeartbeatAsync(...)` | `var snapshot = await _monitoringService.RegisterHeartbeatAsync(...);` | `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:27-32`; `src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs:49-78` | Controller doc user info tu claims roi day vao service. |
| `MobileLiveMonitoringService -> MobileMonitoringHub: SendAsync("MobileMonitorUpdated", snapshot)` | `await _hubContext.Clients.All.SendAsync("MobileMonitorUpdated", snapshot);` | `src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs:147-170` | Chi phat khi signature cua snapshot thay doi. |
| `AdminDashboard -> AdminApiService: GetMobileMonitoringSnapshotAsync()` | `await ApiService.GetMobileMonitoringSnapshotAsync();` | `src/ZoneGuide.Admin/Pages/Index.razor:280-282`; `src/ZoneGuide.Admin/Services/ApiService.cs:446-456` | Dashboard tai snapshot ban dau truoc khi mo SignalR. |
| `AdminApiService -> MobileMonitoringController: GET /api/mobile-monitoring/snapshot` | `_httpClient.GetFromJsonAsync<MobileLiveMonitoringSnapshotDto>("api/mobile-monitoring/snapshot")` | `src/ZoneGuide.Admin/Services/ApiService.cs:446-450`; `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:35-39` | Day la nhanh REST de lay anh chup hien tai. |
| `MobileMonitoringController -> MobileLiveMonitoringService: GetSnapshot()` | `return Ok(_monitoringService.GetSnapshot());` | `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:35-39`; `src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs:80-83` | Snapshot duoc build tu session state trong memory. |
| `AdminDashboard -> MobileMonitoringHub: HubConnection.On("MobileMonitorUpdated") + StartAsync()` | `_mobileHubConnection.On<MobileLiveMonitoringSnapshotDto>(...)` + `await _mobileHubConnection.StartAsync();` | `src/ZoneGuide.Admin/Pages/Index.razor:287-319` | Sequence gom phan dang ky callback va start ket noi. |
| `loop moi 5 giay` | `_heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(), ..., 5000, 5000);` | `src/ZoneGuide.Mobile/Services/MobilePresenceService.cs:59-61` | Nguon heartbeat dinh ky phia mobile. |
| `MobileMonitoringHub --> AdminDashboard: MobileMonitorUpdated(snapshot)` | `_mobileHubConnection.On<MobileLiveMonitoringSnapshotDto>("MobileMonitorUpdated", snapshot => { ... })` | `src/ZoneGuide.Admin/Pages/Index.razor:310-314` | Dashboard nhan realtime update va re-render. |

Note theo sequence moi:
- Sequence nay da doi message sang dung ten method/endpoint dang co trong code.
- `RunSnapshotLoopAsync()` trong `MobileLiveMonitoringService` van ton tai, nhung duoc dua thanh note de giu sequence gon. Vi tri: `src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs:131-145`.

## 3. Hang doi narration theo tung thiet bi

Sequence: `docs/plantuml/narration_queue_sequence.puml`

| Message trong sequence | Code khop | Vi tri code | Ghi chu |
| --- | --- | --- | --- |
| `Guest1 -> Caller1: yeu cau nghe POI A` | cac UI/VM command goi narration | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1017-1055`; `src/ZoneGuide.Mobile/ViewModels/HomeViewModel.cs:123,166`; `src/ZoneGuide.Mobile/ViewModels/POIViewModels.cs:225,631`; `src/ZoneGuide.Mobile/ViewModels/HistoryViewModel.cs:347`; `src/ZoneGuide.Mobile/ViewModels/MainViewModel.cs:163,224` | `Caller1` la abstraction cho UI/ViewModel tren 1 thiet bi, khong phai 1 class duy nhat. |
| `Caller1 -> NarrationService1: PlayImmediatelyAsync(item) (hoac EnqueueAsync(item))` | `PlayImmediatelyAsync(NarrationQueueItem item)` / `EnqueueAsync(NarrationQueueItem item)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:67-109` | Code hien tai da co ca 2 API; app dang goi `PlayImmediatelyAsync` la chinh. |
| `NarrationService1 --> Caller1: bo qua yeu cau` | `if (_currentItem?.POI.Id == item.POI.Id || _queue.Any(...)) return;` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:69-73`, `88-93` | Co ca o `EnqueueAsync` va `PlayImmediatelyAsync`. |
| `NarrationService1 -> NarrationService1: StopAsync() / ClearQueue()` | `await StopAsync(); ClearQueue();` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:95-99` | Chi xay ra trong `PlayImmediatelyAsync`. |
| `NarrationService1 -> NarrationService1: _queue.Enqueue(item) / EnsureQueueProcessingStarted()` | `_queue.Enqueue(item); EnsureQueueProcessingStarted();` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:76-83`, `101-109`, `381-395` | Day la queue rieng cua singleton `NarrationService` tren tung app instance. |
| `NarrationService1 --> Caller1: NarrationStarted(item)` | `NarrationStarted?.Invoke(this, item);` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:285-294` | UI/ViewModel co the subscribe de doi icon/trang thai. |
| `NarrationService1 -> AudioService1: PlayAsync(item.AudioPath)` | `await PlayAudioAndWaitAsync(() => _audioService.PlayAsync(item.AudioPath), ...)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:301-315` | Nhanh audio local. |
| `NarrationService1 -> AudioService1: PlayFromUrlAsync(item.AudioUrl)` | `await PlayAudioAndWaitAsync(() => _audioService.PlayFromUrlAsync(item.AudioUrl), ...)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:317-331` | Nhanh audio online. |
| `NarrationService1 -> TTSService1: SpeakAsync(item.TTSText, item.Language)` | `await PlayTtsAndWaitAsync(item, ...)` -> `_ttsService.SpeakAsync(...)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:334-337`, `397-407` | Nhanh TTS khi khong co audio. |
| `NarrationService1 -> TTSService1: SpeakAsync(item.TTSText, item.Language)` trong nhanh fallback | `catch (...) when (... && !string.IsNullOrWhiteSpace(item.TTSText)) { await PlayTtsAndWaitAsync(item, ...); }` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:311-314`, `327-330` | Fallback khi audio phat loi. |
| `NarrationService1 --> Caller1: NarrationCompleted(item)` | `NarrationCompleted?.Invoke(this, item);` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:339-354` | Event hoan tat sau khi phat xong tu nhien. |
| `Guest2 -> Caller2: yeu cau nghe POI B` | cung kieu UI/VM command nhu thiet bi 1 | `tham chieu cung nhom call site o tren` | Participant `Caller2` chi la app instance khac. |
| `Caller2 -> NarrationService2: PlayImmediatelyAsync(item) (hoac EnqueueAsync(item))` | cung API `INarrationService` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:67-109` | Moi thiet bi giu queue rieng. |
| `NarrationService2 -> NarrationService2: xu ly bang queue rieng cua thiet bi 2` | `builder.Services.AddSingleton<INarrationService, NarrationService>();` | `src/ZoneGuide.Mobile/MauiProgram.cs:35-44` | Singleton nay nam trong tung app process, khong dung chung giua thiet bi. |
| `Guest3 -> Caller3: yeu cau nghe POI C` | cung kieu UI/VM command nhu thiet bi 1 | `tham chieu cung nhom call site o tren` | Participant `Caller3` la app instance khac. |
| `Caller3 -> NarrationService3: PlayImmediatelyAsync(item) (hoac EnqueueAsync(item))` | cung API `INarrationService` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:67-109` | Giong thiet bi 2. |
| `NarrationService3 -> NarrationService3: xu ly bang queue rieng cua thiet bi 3` | singleton theo tung app instance | `src/ZoneGuide.Mobile/MauiProgram.cs:35-44` | Giong thiet bi 2. |

Note theo sequence moi:
- Participant `App1/App2/App3` duoc doi thanh `ViewModel/UI 1/2/3` vi code khong co 1 class `App` truc tiep goi narration; caller that su la nhieu ViewModel/UI khac nhau.
- Sequence van giu note "moi thiet bi co queue rieng" va nay khop voi dang ky DI hien tai.

## 4. Live tracking tren Heatmap

Sequence: `docs/plantuml/heatmap_sequence.puml`

| Message trong sequence | Code khop | Vi tri code | Ghi chu |
| --- | --- | --- | --- |
| `Admin -> HeatmapPage: mo trang Heatmap` | `OnInitializedAsync()` + `OnAfterRenderAsync(firstRender)` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:134-166` | Trang vua load du lieu vua khoi tao map JS. |
| `HeatmapPage -> AdminApiService: GetHeatmapDataAsync(from, to)` | `_heatmapData = await ApiService.GetHeatmapDataAsync(_dateRange.Start, _dateRange.End);` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:168-177`; `src/ZoneGuide.Admin/Services/ApiService.cs:412-430` | Day la du lieu heatmap lich su. |
| `AdminApiService -> AnalyticsController: GET /api/analytics/heatmap` | `_httpClient.GetFromJsonAsync<List<HeatmapPointDto>>("api/analytics/heatmap{query}")` | `src/ZoneGuide.Admin/Services/ApiService.cs:416-424`; `src/ZoneGuide.API/Controllers/AnalyticsController.cs:87-100` | Sequence moi them participant nay de khop voi code. |
| `AnalyticsController --> AdminApiService: List<HeatmapPointDto>` | `return Ok(heatmapData);` | `src/ZoneGuide.API/Controllers/AnalyticsController.cs:92-95` | Response du lieu heatmap lich su. |
| `AdminApiService --> HeatmapPage: _heatmapData` | assignment vao `_heatmapData` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:168-177` | Sau do `UpdateHeatmapLayer()` se day sang JS. |
| `HeatmapPage -> AdminApiService: GetMobileMonitoringSnapshotAsync()` | `_mobileSnapshot = await ApiService.GetMobileMonitoringSnapshotAsync();` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:180-188`; `src/ZoneGuide.Admin/Services/ApiService.cs:446-456` | Day la nhanh live tracking mobile. |
| `AdminApiService -> MobileMonitoringController: GET /api/mobile-monitoring/snapshot` | `_httpClient.GetFromJsonAsync<MobileLiveMonitoringSnapshotDto>("api/mobile-monitoring/snapshot")` | `src/ZoneGuide.Admin/Services/ApiService.cs:446-450`; `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:35-39` | REST snapshot de ve marker tracking. |
| `MobileMonitoringController -> MobileLiveMonitoringService: GetSnapshot()` | `return Ok(_monitoringService.GetSnapshot());` | `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:35-39`; `src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs:80-83` | Lay session state hien tai. |
| `AdminApiService --> HeatmapPage: _mobileSnapshot` | assignment vao `_mobileSnapshot` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:180-188` | Sau do `UpdateTrackingLayer()` se push sang JS. |
| `HeatmapPage -> MapJs: initHeatmapMap("heatmap-map")` | `await JS.InvokeVoidAsync("initHeatmapMap", "heatmap-map");` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:147-149`; `src/ZoneGuide.Admin/wwwroot/js/map.js:806-832` | Khoi tao Leaflet map. |
| `HeatmapPage -> MapJs: updateHeatmapData(_heatmapData)` | `await JS.InvokeVoidAsync("updateHeatmapData", _heatmapData);` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:195-200`; `src/ZoneGuide.Admin/wwwroot/js/map.js:834-853` | Day layer heatmap lich su. |
| `HeatmapPage -> MapJs: updateTrackingUsers(_mobileSnapshot.Sessions)` | `await JS.InvokeVoidAsync("updateTrackingUsers", _mobileSnapshot.Sessions);` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:203-208`; `src/ZoneGuide.Admin/wwwroot/js/map.js:855-886` | Day marker live tracking. |
| `MapJs -> MapJs: bo marker cu / loc session co hasLocationFix / ve marker theo isTracking` | block `updateTrackingUsers(trackingSessions)` | `src/ZoneGuide.Admin/wwwroot/js/map.js:855-886` | Marker xanh neu dang tracking, xam neu tam dung. |
| `loop moi 10 giay` | `_refreshTimer = new System.Timers.Timer(10000);` + `RefreshTrackingData()` | `src/ZoneGuide.Admin/Pages/Heatmap.razor:156-159`, `190-193` | Heatmap page polling snapshot live tracking moi 10 giay. |
| `App -> MobilePresenceService: StartAsync()` | `await _mobilePresenceService.StartAsync();` | `src/ZoneGuide.Mobile/App.xaml.cs:46-55` | Nhanh mobile app cap nhat du lieu nen. |
| `MobilePresenceService -> MobileApiService: UploadMobileHeartbeatAsync(dto)` | `await _apiService.UploadMobileHeartbeatAsync(new MobileLiveHeartbeatDto { ... })` | `src/ZoneGuide.Mobile/Services/MobilePresenceService.cs:116-148`; `src/ZoneGuide.Mobile/Services/ApiService.cs:599-635` | Nguon du lieu cho marker tracking. |
| `MobileApiService -> MobileMonitoringController: POST /api/mobile-monitoring/heartbeat` | POST `mobile-monitoring/heartbeat` | `src/ZoneGuide.Mobile/Services/ApiService.cs:605-617`; `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:19-33` | Day session state len server. |
| `MobileMonitoringController -> MobileLiveMonitoringService: RegisterHeartbeatAsync(...)` | `await _monitoringService.RegisterHeartbeatAsync(...);` | `src/ZoneGuide.API/Controllers/MobileMonitoringController.cs:27-32`; `src/ZoneGuide.API/Services/MobileLiveMonitoringService.cs:49-78` | Service cap nhat state de trang Heatmap lay lai o lan poll ke tiep. |

Note theo sequence moi:
- Sequence nay da duoc bo sung nhanh `GetHeatmapDataAsync()` va `AnalyticsController`, vi code thuc te tai ca heatmap lich su lan live tracking.
- Live tracking tren Heatmap dang dung polling 10 giay, khong dung SignalR truc tiep o trang nay.

## 5. Xu ly trung khi dung giua 2 POI gan nhau

Sequence: `docs/plantuml/duplicate_poi_resolution_sequence.puml`

| Message trong sequence | Code khop | Vi tri code | Ghi chu |
| --- | --- | --- | --- |
| `User -> LocationService: di chuyen gan 2 POI` | tac nhan ngoai he thong; code nhan vao o event `LocationChanged` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:214-215`, `1222-1259` | Sequence moi doi nguon su kien thanh `LocationService` de sat code hon. |
| `LocationService -> MapViewModel: OnLocationChanged(location)` | `private void OnLocationChanged(object? sender, LocationData location)` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1222-1259` | `MapViewModel` subscribe `_locationService.LocationChanged += OnLocationChanged;`. |
| `MapViewModel -> GeofenceService: ProcessLocationUpdateAsync(location)` | `_ = _geofenceService.ProcessLocationUpdateAsync(location);` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1255` | Moi cap nhat vi tri deu day vao geofence. |
| `GeofenceService -> GeofenceService: tinh distance / newState / cooldown / debounce cho tung POI dang monitor` | block tinh `distance`, `newState`, `CanTrigger`, `IsCooldownActive`, `events.Add(...)` | `src/ZoneGuide.Mobile/Services/GeofenceService.cs:180-249` | Sequence gom phan noi bo thanh 1 message tong hop. |
| `GeofenceService -> PoiScoringService: CalculateFinalPriority(context)` | `PoiScoringService.CalculateFinalPriority(new PoiScoreContext { ... })` | `src/ZoneGuide.Mobile/Services/GeofenceService.cs:277-293`; `src/ZoneGuide.Mobile/Services/PoiScoringService.cs:10-19` | Chi ap dung khi co nhieu `Enter` events can tranh nhau. |
| `PoiScoringService --> GeofenceService: finalPriority` | gia tri tra ve tu `CalculateFinalPriority(...)` | `src/ZoneGuide.Mobile/Services/PoiScoringService.cs:10-19` | Sau do `GeofenceService` sort giam dan. |
| `GeofenceService --> MapViewModel: tra ve Enter event uu tien cao nhat` | `GeofenceTriggered?.Invoke(this, bestEnter.Event);` | `src/ZoneGuide.Mobile/Services/GeofenceService.cs:274-300` | Sequence nay giu dung logic chi fire 1 `Enter` tot nhat. |
| `GeofenceService --> MapViewModel: tra ve event do` | `foreach (var evt in nonEnterEvents) { GeofenceTriggered?.Invoke(this, evt); }` | `src/ZoneGuide.Mobile/Services/GeofenceService.cs:265-272` | Nhanh `Approach`, `Dwell`, `Exit` duoc fire truc tiep. |
| `MapViewModel -> MapViewModel: clear _playedPoiIdsInCurrentVisit / _replayBlockedPoiId / _lockedPoiId / _lastInRangePoiId` | nhanh `if (evt.EventType == GeofenceEventType.Exit)` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1336-1361` | Day la logic clear state khi ra khoi POI. |
| `MapViewModel -> MapViewModel: TryAutoOpenPoiDetailAsync(evt.POI)` | `await TryAutoOpenPoiDetailAsync(evt.POI);` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1364-1371`; `1724-1749` | Chay truoc khi check autoplay, neu event la `Approach`/`Enter`. |
| `MapViewModel -> MapViewModel: chi auto-open POI detail` | nhanh `if (!autoPlayEnabled) { ... return; }` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1374-1382` | Khi tat `AutoPlayOnEnter`, code van co the auto-open detail. |
| `MapViewModel -> MapViewModel: bo qua auto narration` | cac nhanh `replay block`, `locked POI`, `same active POI`, `manual playback`, `ShouldSkipAutoNarration` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1384-1427` | Sequence gop cac dieu kien chan autoplay thanh 1 message. |
| `MapViewModel -> NarrationService: StopAsync()` | `await _narrationService.StopAsync();` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1428-1431` | Chi xay ra khi dang co narration khac va du dieu kien chuyen sang POI moi. |
| `MapViewModel -> NarrationService: PlayImmediatelyAsync(BuildNarrationItem(...))` | `await _narrationService.PlayImmediatelyAsync(BuildNarrationItem(...));` | `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1433-1436` | Day la message autoplay cho POI duoc chon. |
| `NarrationService --> MapViewModel: skip duplicate` | `if (_currentItem?.POI.Id == item.POI.Id || _queue.Any(...)) return;` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:86-93` | Van co duplicate guard o service du `MapViewModel` da chan tu truoc. |
| `NarrationService -> NarrationService: StopAsync() / ClearQueue() / _queue.Enqueue(item) / EnsureQueueProcessingStarted()` | block `PlayImmediatelyAsync(item)` | `src/ZoneGuide.Mobile/Services/NarrationService.cs:95-109` | Message nay khop voi logic uu tien item moi trong service. |

Note theo sequence moi:
- Participant dau vao da duoc doi tu `User` sang `LocationService -> MapViewModel` de dung voi event flow trong code.
- Sequence nay tap trung vao nhanh `GeofenceTriggered` khi nhieu POI gan nhau. `EnsureNarrationByCurrentLocationAsync()` van ton tai nhu 1 nhanh bo tro khac trong `MapViewModel` tai `src/ZoneGuide.Mobile/ViewModels/MapViewModel.cs:1573-1721`.
