using ZoneGuide.Shared.Models;

namespace ZoneGuide.Shared.Interfaces;

/// <summary>
/// Interface cho Location Service - Theo dõi GPS
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Sự kiện khi vị trí thay đổi
    /// </summary>
    event EventHandler<LocationData>? LocationChanged;
    
    /// <summary>
    /// Sự kiện khi có lỗi
    /// </summary>
    event EventHandler<string>? LocationError;
    
    /// <summary>
    /// Vị trí hiện tại
    /// </summary>
    LocationData? CurrentLocation { get; }
    
    /// <summary>
    /// Đang theo dõi vị trí
    /// </summary>
    bool IsTracking { get; }
    
    /// <summary>
    /// Bắt đầu theo dõi vị trí
    /// </summary>
    Task<bool> StartTrackingAsync(GPSAccuracyLevel accuracy = GPSAccuracyLevel.Medium);
    
    /// <summary>
    /// Dừng theo dõi vị trí
    /// </summary>
    Task StopTrackingAsync();
    
    /// <summary>
    /// Lấy vị trí hiện tại một lần
    /// </summary>
    Task<LocationData?> GetCurrentLocationAsync();
    
    /// <summary>
    /// Kiểm tra quyền truy cập vị trí
    /// </summary>
    Task<bool> CheckPermissionAsync();
    
    /// <summary>
    /// Yêu cầu quyền truy cập vị trí
    /// </summary>
    Task<bool> RequestPermissionAsync();
    
    /// <summary>
    /// Thiết lập mức độ chính xác
    /// </summary>
    void SetAccuracyLevel(GPSAccuracyLevel level);
}

/// <summary>
/// Interface cho Geofence Service - Quản lý vùng kích hoạt
/// </summary>
public interface IGeofenceService
{
    /// <summary>
    /// Sự kiện khi có trigger Geofence
    /// </summary>
    event EventHandler<GeofenceEvent>? GeofenceTriggered;
    
    /// <summary>
    /// Danh sách POI đang theo dõi
    /// </summary>
    IReadOnlyList<POI> MonitoredPOIs { get; }
    
    /// <summary>
    /// POI gần nhất
    /// </summary>
    POI? NearestPOI { get; }
    
    /// <summary>
    /// Khoảng cách đến POI gần nhất (mét)
    /// </summary>
    double? NearestPOIDistance { get; }
    
    /// <summary>
    /// Thêm POI để theo dõi
    /// </summary>
    void AddPOI(POI poi);
    
    /// <summary>
    /// Thêm nhiều POI để theo dõi
    /// </summary>
    void AddPOIs(IEnumerable<POI> pois);
    
    /// <summary>
    /// Xóa POI khỏi danh sách theo dõi
    /// </summary>
    void RemovePOI(int poiId);
    
    /// <summary>
    /// Xóa tất cả POI
    /// </summary>
    void ClearPOIs();
    
    /// <summary>
    /// Cập nhật vị trí và kiểm tra Geofence
    /// </summary>
    Task ProcessLocationUpdateAsync(LocationData location);
    
    /// <summary>
    /// Lấy danh sách POI trong vùng
    /// </summary>
    List<POI> GetPOIsInRange(double radius);
    
    /// <summary>
    /// Đặt cooldown cho POI
    /// </summary>
    void SetCooldown(int poiId, TimeSpan duration);
    
    /// <summary>
    /// Kiểm tra POI có đang cooldown không
    /// </summary>
    bool IsCooldownActive(int poiId);
    
    /// <summary>
    /// Reset cooldown cho POI
    /// </summary>
    void ResetCooldown(int poiId);
    
    /// <summary>
    /// Reset tất cả cooldown
    /// </summary>
    void ResetAllCooldowns();
}

/// <summary>
/// Interface cho Narration Service - Phát thuyết minh
/// </summary>
public interface INarrationService
{
    /// <summary>
    /// Sự kiện khi bắt đầu phát
    /// </summary>
    event EventHandler<NarrationQueueItem>? NarrationStarted;
    
    /// <summary>
    /// Sự kiện khi hoàn thành phát
    /// </summary>
    event EventHandler<NarrationQueueItem>? NarrationCompleted;
    
    /// <summary>
    /// Sự kiện khi dừng phát
    /// </summary>
    event EventHandler<NarrationQueueItem>? NarrationStopped;
    
    /// <summary>
    /// Sự kiện khi có lỗi
    /// </summary>
    event EventHandler<string>? NarrationError;
    
    /// <summary>
    /// Sự kiện cập nhật tiến trình
    /// </summary>
    event EventHandler<double>? ProgressUpdated;
    
    /// <summary>
    /// Đang phát
    /// </summary>
    bool IsPlaying { get; }
    
    /// <summary>
    /// Đang tạm dừng
    /// </summary>
    bool IsPaused { get; }
    
    /// <summary>
    /// Item đang phát
    /// </summary>
    NarrationQueueItem? CurrentItem { get; }
    
    /// <summary>
    /// Hàng đợi audio
    /// </summary>
    IReadOnlyList<NarrationQueueItem> Queue { get; }
    
    /// <summary>
    /// Tiến trình hiện tại (0.0 - 1.0)
    /// </summary>
    double CurrentProgress { get; }
    
    /// <summary>
    /// Thêm vào hàng đợi
    /// </summary>
    Task EnqueueAsync(NarrationQueueItem item);
    
    /// <summary>
    /// Phát ngay (dừng cái hiện tại nếu có)
    /// </summary>
    Task PlayImmediatelyAsync(NarrationQueueItem item);
    
    /// <summary>
    /// Phát tiếp
    /// </summary>
    Task ResumeAsync();
    
    /// <summary>
    /// Tạm dừng
    /// </summary>
    Task PauseAsync();
    
    /// <summary>
    /// Dừng hẳn
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Bỏ qua và phát cái tiếp theo
    /// </summary>
    Task SkipAsync();
    
    /// <summary>
    /// Xóa hàng đợi
    /// </summary>
    void ClearQueue();
    
    /// <summary>
    /// Thiết lập âm lượng (0.0 - 1.0)
    /// </summary>
    void SetVolume(float volume);
    
    /// <summary>
    /// Thiết lập tốc độ TTS (0.5 - 2.0)
    /// </summary>
    void SetTTSSpeed(float speed);
    
    /// <summary>
    /// Lấy danh sách giọng TTS có sẵn
    /// </summary>
    Task<List<string>> GetAvailableVoicesAsync();
    
    /// <summary>
    /// Thiết lập giọng TTS
    /// </summary>
    Task SetVoiceAsync(string voiceId);
}

/// <summary>
/// Interface cho Text-to-Speech Service
/// </summary>
public interface ITTSService
{
    /// <summary>
    /// Sự kiện khi bắt đầu nói
    /// </summary>
    event EventHandler? SpeakStarted;
    
    /// <summary>
    /// Sự kiện khi nói xong
    /// </summary>
    event EventHandler? SpeakCompleted;
    
    /// <summary>
    /// Đang nói
    /// </summary>
    bool IsSpeaking { get; }
    
    /// <summary>
    /// Nói văn bản
    /// </summary>
    Task SpeakAsync(string text, string language = "vi-VN");
    
    /// <summary>
    /// Dừng nói
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Lấy danh sách ngôn ngữ hỗ trợ
    /// </summary>
    Task<List<string>> GetSupportedLanguagesAsync();
    
    /// <summary>
    /// Lấy danh sách giọng nói
    /// </summary>
    Task<List<string>> GetVoicesAsync(string language);
    
    /// <summary>
    /// Thiết lập giọng nói
    /// </summary>
    void SetVoice(string voiceId);
    
    /// <summary>
    /// Thiết lập tốc độ (0.5 - 2.0)
    /// </summary>
    void SetSpeed(float speed);
    
    /// <summary>
    /// Thiết lập âm lượng (0.0 - 1.0)
    /// </summary>
    void SetVolume(float volume);
}
