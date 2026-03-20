# ZoneGuide — Hệ Thống Thuyết Minh Du Lịch Dựa Trên GPS

 ### Yêu cầu
- .NET 8.0 SDK
- Visual Studio 2022 
- SQL Server 

---

## 1. Thành Viên

| # | Họ và tên | 
|---|---|
| 1 | Hồ Phạm Hữu Bình | 
| 2 | Nguyễn Văn Phát | 

---

## 2. Tổng Quan

Hệ thống thuyết minh du lịch tự động dựa trên GPS gồm 3 thành phần:

| Thành phần | Công nghệ | Mô tả |
|---|---|---|
| **Mobile App** | .NET MAUI | Du khách nghe audio tự động qua GPS + Geofencing, bản đồ tương tác, offline-first |
| **Admin Web** | Blazor Server + MudBlazor | Quản lý POI, tour, người dùng, analytics, dashboard |
| **Backend API** | ASP.NET Core | REST API, TTS, RBAC, đồng bộ offline |

---

## 3. Mục Tiêu 

**Mục tiêu chính:**
- POI có mô tả + ảnh + audio thuyết minh tự động khi du khách đi dạo trong khu vực POI.
- Hoạt động **100% offline** sau lần đồng bộ đầu tiên:  SQLite + audio cache local
- GPS + Geofencing tự động kích hoạt audio
- Hỗ trợ **đa ngôn ngữ** với TTS và file audio ghi sẵn.
- Chủ quán tự quản lý nội dung quán mình
- Dùng Edge-TTS và Google Translate miễn phí

---

## 4. Đối Tượng Người Dùng

| Persona | Nhu cầu chính |
|---|---|
| Du khách nội địa | Xem bản đồ POI, nghe thuyết minh tự động, khám phá tour theo lịch trình |
| Du khách quốc tế | Nghe audio bằng ngôn ngữ mẹ đẻ, xem ảnh minh họa, hiểu đặc trưng địa điểm |
| Quản trị viên (Admin) | CRUD POI & Tour, xem analytics, quản lý heatmap du khách |
| Chủ cửa hàng | CRUD POI, sửa thông tin cửa hàng |

---

## 5. Phạm Vi Tính Năng 

| Tính năng | Platform |
|---|---|
| Bản đồ tương tác hiển thị POI & vị trí người dùng | Mobile |
| GPS + Geofencing tự động kích hoạt audio narration | Mobile |
| Phát audio đa ngôn ngữ (TTS + pre-recorded) | Mobile |
| Hàng đợi audio (queue) với cơ chế chống trùng lặp | Mobile |
| Tải dữ liệu offline (SQLite + audio cache) | Mobile |
| Đồng bộ tăng dần (incremental sync) với backend | Mobile |
| Màn hình chi tiết POI (mô tả, ảnh, audio) | Mobile |
| Dashboard thống kê tổng quan | Web Admin |
| CRUD POI đa ngôn ngữ + hình ảnh | Web Admin |
| Quản lý Tour (sắp xếp POI theo thứ tự) | Web Admin |
| Analytics: Top POI, completion rate, daily stats | Web Admin |
| Heatmap mật độ du khách | Web Admin |
| REST API đồng bộ offline (incremental + full sync) | Backend |
| Anonymous analytics tracking | Backend |

**Ngoài phạm vi:** Push notification, thanh toán, chatbot.

### 5.1 Logic Geofencing & Audio Queue

Khi người dùng di chuyển vào vùng POI, app kích hoạt audio theo cơ chế ưu tiên:

```
GPS cập nhật tọa độ (mỗi 1–5 giây)
        ↓
┌────────────────────────────────────┐
│  Khoảng cách đến POI ≤ radius?     │
└────────────────────────────────────┘
        │ YES                  │ NO
        ▼                       ▼
  Debounce (3s)           Tiếp tục theo dõi
        ↓
  POI trong Cooldown?
        │ YES                  │ NO
        ▼                       ▼
  Bỏ qua               Thêm vào Audio Queue
                               ↓
                    Cache local có MP3?
                    │ HIT              │ MISS
                    ▼                   ▼
              Phát ngay (0ms)     TTS on-demand
                                  → Cache + Phát
```

**Cấu hình geofence:**
- `geofence_radius`: Cấu hình riêng cho từng POI
- `debounce`: 3 giây (tránh trigger khi di chuyển qua nhanh)
- `cooldown`: 300 giây (5 phút) — không phát lại cùng 1 POI quá sớm
- `priority queue`: POI gần nhất và có độ ưu tiên cao được phát trước

### 5.2 Logic Độ Chính Xác GPS Theo Pin

App tự động điều chỉnh độ chính xác GPS để tiết kiệm pin:

| Mức | Độ chính xác | Tiêu hao pin | Dùng khi |
|---|---|---|---|
| **Low** | ~500m | Rất thấp | Nền, tiết kiệm pin |
| **Medium** | ~100m | Trung bình | Duyệt bản đồ |
| **High** | ~10m | Cao | Navigation, geofencing |

### 5.3 Đồng Bộ Offline — Incremental Sync

```
Mobile App khởi động
        ↓
Load từ SQLite local → Hiển thị ngay (0ms)
        ↓
GET /api/sync?since={last_sync_time}
        ↓
┌────────────────────────────────────┐
│  Có dữ liệu mới?                   │
└────────────────────────────────────┘
        │ YES                  │ NO
        ▼                       ▼
  Upsert SQLite           Không thay đổi
  Tải audio mới           (dùng cache cũ)
  Cập nhật UI
```


---

## 6. Kiến Trúc Hệ Thống

### 6.1 System Architecture Overview

```mermaid
graph TB
    subgraph CLIENT["CLIENT SIDE"]
        MAUI["ZoneGuide.Mobile\n(.NET MAUI)\nMVVM + Services\nAndroid / iOS"]
        BLAZOR["ZoneGuide.Admin\n(Blazor Server + MudBlazor)\nAdmin Portal"]
    end

    subgraph BACKEND["BACKEND — ZoneGuide.API (ASP.NET Core)"]
        direction LR
        POI["/api/pois\nCRUD POI"]
        TOUR["/api/tours\nCRUD Tour"]
        SYNC["/api/sync\nIncremental Sync"]
        ANALYTICS["/api/analytics\nAnonymous Tracking"]
    end

    subgraph DATA["DATA LAYER"]
        DB[(SQL Server\nBackend)]
        SQLITE[(SQLite\nMobile Offline)]
    end

    SHARED["ZoneGuide.Shared\nModels / DTOs / Interfaces"]

    MAUI -- REST API --> BACKEND
    BLAZOR -- REST API --> BACKEND
    BACKEND --> DATA
    SHARED -.-> MAUI
    SHARED -.-> BLAZOR
    SHARED -.-> BACKEND
```

### 6.2 Cấu Trúc Solution

```
ZoneGuide/
├── src/
│   ├── ZoneGuide.Shared/          # Models, DTOs, Interfaces dùng chung
│   ├── ZoneGuide.Mobile/          # .NET MAUI — Android/iOS
│   ├── ZoneGuide.API/             # ASP.NET Core Web API
│   └── ZoneGuide.Admin/           # Blazor Server Admin Portal
├── scripts/                       # Build & deploy scripts (PowerShell)
├── .github/workflows/             # CI/CD GitHub Actions
├── ZoneGuide.sln
└── ZoneGuide.keystore             # Android signing key
```

### 6.3 MVVM Pattern (Mobile)

```mermaid
graph LR
    V["View\n(XAML / ContentPage)"]
    VM["ViewModel\n(C# + INotifyPropertyChanged)"]
    S["Service / Repository\n(ILocationService\nIGeofenceService\nINarrationService\nIApiService)"]

    V -- "Data Binding / Commands" --> VM
    VM -- "Calls" --> S
    S -- "Updates (async)" --> VM
    VM -- "ObservableProperty" --> V
```

---

## 7. Luồng Nghiệp Vụ Chính

### 7.1 Geofencing → Audio Narration Flow

```mermaid
sequenceDiagram
    participant U as Tourist (App)
    participant GPS as LocationService
    participant GF as GeofenceService
    participant AU as NarrationService
    participant API as Backend API

    U->>GPS: Mở app, cấp quyền Location
    GPS-->>GF: Cập nhật tọa độ (mỗi 1-5s)
    loop Heartbeat
        GF->>GF: Tính khoảng cách đến từng POI
        alt Khoảng cách ≤ POI radius
            GF->>GF: Debounce 3s → Xác nhận ENTER
            alt Không trong Cooldown
                GF->>AU: Trigger POI (id + language)
                AU->>AU: Kiểm tra cache local MP3
                alt Cache HIT
                    AU-->>U: Phát audio ngay (0ms)
                else Cache MISS
                    AU->>API: Request TTS on-demand
                    API-->>AU: MP3 stream
                    AU-->>U: Phát audio
                    AU->>AU: Lưu cache local
                end
                GF->>GF: Set Cooldown 5 phút
            end
        end
    end
```

### 7.2 Offline Sync Flow

```mermaid
sequenceDiagram
    participant APP as Mobile App
    participant LOCAL as SQLite (Local)
    participant API as Backend API

    APP->>LOCAL: Load POI & Tour từ SQLite (0ms)
    APP-->>APP: Hiển thị dữ liệu offline ngay
    APP->>API: GET /api/sync?since={last_sync}
    API-->>APP: Delta data (POI/Tour thay đổi)
    APP->>LOCAL: Upsert POI & Tour mới
    APP->>API: GET /api/sync/version
    API-->>APP: Content version hash
    APP->>APP: Tải audio mới nếu cần
```

### 7.3 Analytics Upload Flow

```mermaid
sequenceDiagram
    participant U as Du khách
    participant APP as Mobile App
    participant LOCAL as Local Buffer
    participant API as Backend API

    U->>APP: Vào vùng POI → Nghe audio
    APP->>LOCAL: Lưu analytics event (ẩn danh)
    Note over LOCAL: Không có UserID / DeviceID thật
    loop Batch upload (30 phút)
        APP->>API: POST /api/analytics/upload (batch)
        API-->>APP: OK
        APP->>LOCAL: Xóa events đã upload
    end
```

---

## 8. Mô Hình Dữ Liệu

```mermaid
classDiagram
    class POI {
        +Guid Id
        +string Name
        +string Description
        +double Latitude
        +double Longitude
        +double TriggerRadius
        +int Priority
        +bool IsActive
        +DateTime UpdatedAt
    }

    class LocalizedContent {
        +Guid Id
        +Guid POIId
        +string Language
        +string Name
        +string Description
        +string AudioUrl
    }

    class Tour {
        +Guid Id
        +string Name
        +string Description
        +string Language
        +bool IsActive
        +DateTime CreatedAt
    }

    class TourStop {
        +Guid Id
        +Guid TourId
        +Guid POIId
        +int OrderIndex
        +string NarrationText
    }

    class AnalyticsEvent {
        +string PoiId
        +string EventType
        +int DurationMs
        +string Language
        +double LatRounded
        +double LngRounded
        +DateTime Timestamp
    }

    class SyncRecord {
        +Guid Id
        +string ContentVersion
        +DateTime LastSyncAt
        +string EntityType
    }

    POI "1" --> "many" LocalizedContent
    Tour "1" --> "many" TourStop
    TourStop "many" --> "1" POI
    POI "1" --> "many" AnalyticsEvent
```

---

## 9. Phân Quyền & Bảo Mật

```mermaid
flowchart LR
    subgraph ROLES["Vai trò hệ thống"]
        AD[Admin]
        GU[Guest / Tourist]
    end

    subgraph PERMISSIONS["Quyền hạn"]
        P1[CRUD tất cả POI]
        P2[CRUD Tour & TourStop]
        P3[Xem Analytics & Heatmap]
        P4[Xem Dashboard tổng quan]
        P5[Xem bản đồ POI]
        P6[Nghe audio tự động]
        P7[Đồng bộ dữ liệu offline]
        P8[Upload analytics ẩn danh]
    end

    AD --> P1
    AD --> P2
    AD --> P3
    AD --> P4
    GU --> P5
    GU --> P6
    GU --> P7
    GU --> P8
```

**Bảo mật dữ liệu:**
- Analytics **không lưu UserID / DeviceID** thật — chỉ anonymous device ID tạm thời.
- Tọa độ GPS được **làm tròn** (3 chữ số thập phân) trước khi gửi lên server.
- **Không thu thập thông tin cá nhân** — tuân thủ quy định quyền riêng tư.
- Yêu cầu **sự đồng ý của người dùng** trước khi theo dõi vị trí.

---

## 10. Technology Stack

| Layer | Công nghệ | Lý do |
|---|---|---|
| Mobile | .NET MAUI (.NET 8/10) | Cross-platform Android/iOS/Windows |
| Mobile UI | XAML + MAUI Controls | Native UI, MVVM data binding |
| Mobile DB | SQLite (sqlite-net-pcl) | Offline-first, nhẹ, hiệu năng cao |
| Admin Web | Blazor Server (.NET 8) | C# fullstack, real-time SSE |
| Admin UI | MudBlazor | Component library chuyên nghiệp |
| Backend | ASP.NET Core Web API | REST, Middleware, CORS |
| ORM | Entity Framework Core | Code-first, Migrations |
| Database | SQL Server (dev: SQLite) | Relational, ACID, Audit Log |
| Auth | JWT / Cookie | Bảo mật API và admin portal |

---

## 11. API Design

### POIs

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/pois` | GET | Danh sách tất cả POI |
| `/api/pois/{id}` | GET | Chi tiết 1 POI |
| `/api/pois/nearby` | GET | POI theo `lat`, `lon`, `radius` |
| `/api/pois` | POST | Tạo POI mới |
| `/api/pois/{id}` | PUT | Cập nhật POI |
| `/api/pois/{id}` | DELETE | Xóa POI |

### Tours

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/tours` | GET | Danh sách tour |
| `/api/tours/{id}` | GET | Chi tiết tour |
| `/api/tours/{id}/details` | GET | Tour kèm chi tiết POI |
| `/api/tours` | POST | Tạo tour |
| `/api/tours/{id}` | PUT | Cập nhật tour |
| `/api/tours/{id}` | DELETE | Xóa tour |

### Analytics

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/analytics/upload` | POST | Upload batch events ẩn danh |
| `/api/analytics/dashboard` | GET | Dashboard tổng hợp |
| `/api/analytics/top-pois` | GET | Top POI theo lượt nghe |
| `/api/analytics/heatmap` | GET | Dữ liệu heatmap (lat/lng clusters) |

---

## 13. Analytics & Dashboard

### 13.1 Thu Thập Dữ Liệu Ẩn Danh

App ghi lại các event **không chứa thông tin cá nhân** để phân tích hành vi du khách:

```csharp
// Event schema — không có user ID thật, không có device ID cố định
public class AnalyticsEvent
{
    public string   PoiId       { get; set; }  // "poi-guid-xxx"
    public string   EventType   { get; set; }  // "enter" | "play" | "skip" | "complete"
    public int      DurationMs  { get; set; }  // Thời gian nghe (ms)
    public string   Language    { get; set; }  // "vi-VN", "en-US"...
    public double   LatRounded  { get; set; }  // Làm tròn 3 chữ số (ẩn danh hóa)
    public double   LngRounded  { get; set; }
    public DateTime Timestamp   { get; set; }
}
```

### 13.2 Dashboard Admin (Blazor + MudBlazor)

```mermaid
graph LR
    APP["Mobile App\n(events ẩn danh)"] -- batch POST --> API["Backend API\n/api/analytics"]
    API --> DB[(Analytics DB)]
    DB --> D1["Top POI\nNghe nhiều nhất"]
    DB --> D2["Completion Rate\nSkip rate mỗi POI"]
    DB --> D3["Daily Stats\nLượt truy cập theo ngày"]
    DB --> D4["Heatmap\nMật độ du khách"]
```

| Metric | Mô tả | Dùng để |
|---|---|---|
| **Top POIs** | Điểm được nghe nhiều nhất 7/30 ngày | Ưu tiên cập nhật nội dung |
| **Completion rate** | Tỉ lệ nghe hết / bỏ giữa chừng | Đánh giá chất lượng audio |
| **Avg listen duration** | Thời gian nghe trung bình / POI | Tối ưu độ dài nội dung |
| **Daily trends** | Số lượt truy cập theo ngày/tuần | Lên kế hoạch vận hành |
| **Heatmap** | Mật độ du khách theo khu vực | Quyết định mở rộng POI |

---








