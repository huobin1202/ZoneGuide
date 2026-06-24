# ZoneGuide

A **location-aware automatic audio tour guide system** — GPS-based museum/attraction audio guide that plays narrated content automatically when visitors approach Points of Interest (POIs) using geofencing. Supports multi-language content, offline mode, QR-coded POI pages, real-time admin monitoring via SignalR, and a contributor system.

---

## Architecture

### Solution Structure (4 projects)

```
ZoneGuide.sln
├── src/
│   ├── ZoneGuide.Shared/         # .NET 10 class library
│   ├── ZoneGuide.API/            # ASP.NET Core Web API
│   ├── ZoneGuide.Admin/          # Blazor Server Web App
│   └── ZoneGuide.Mobile/         # .NET MAUI (Android)
├── database/                     # SQL Server backup
├── docs/                         # PRD, sequence diagrams
├── scripts/                      # Utility scripts
└── .github/                      # CI/CD (not yet configured)
```

| Project | Framework | Description |
|---|---|---|
| **ZoneGuide.Shared** | .NET 10 | Models, DTOs, enums, interfaces shared across all projects |
| **ZoneGuide.API** | .NET 10 | REST API — CRUD, auth, analytics, TTS, QR codes, SignalR hubs |
| **ZoneGuide.Admin** | .NET 10 | Blazor dashboard — manage POIs/tours, users, contributions, live tracking |
| **ZoneGuide.Mobile** | .NET MAUI (Android 36) | End-user app — GPS, geofencing, auto-narration, offline, QR scanning |

---

## Features

### Geofenced Auto-Narration
- Dual-radius geofence: `TriggerRadius` (50m) for auto-play, `ApproachRadius` (100m) for proximity warnings
- POI scoring system intelligently selects best POI when visitor is within range of multiple locations — `FinalScore` computed from priority + distance
- 300-second cooldown between replays of the same POI
- Narration queue with deduplication prevents re-queuing already-playing POIs

### Tours
- Curated walking routes with ordered POIs
- Estimated duration, distance, and wheelchair accessibility info
- Offline audio pack download per tour

### Multi-Language
- 6 languages: Vietnamese, English, Chinese, Japanese, Korean, French
- POI and Tour content fully translatable (name, description, TTS script, audio file)
- Device language auto-detection with English fallback

### Offline Mode
- SQLite local database synced with server
- Downloaded audio packs for offline playback
- Analytics uploads batched and sent when connectivity resumes

### QR Code System
- Every POI gets a QR linking to: `zoneguide://poi/{id}` (mobile) or web PWA (browser)
- ZXing-based camera scanner on mobile
- QR presence tracking via SignalR for admin monitoring

### Real-Time Admin Monitoring
- Mobile app sends heartbeats every 5 seconds
- Admin dashboard displays live user locations on a Leaflet map with session cards
- SignalR hubs: `/hubs/mobile-monitor`, `/hubs/qr-monitor`, `/hubs/notifications`

### Analytics
- Dashboard KPIs (total listens, active users, completion rates)
- Heatmap visualization of visitor density
- Daily stats and per-POI statistics
- Location & narration history tracking

### Contributor System
- Users can submit new POI proposals with approval workflow
- Statuses: Draft → Pending → Approved / Rejected / NeedsRevision
- Admin review dialog with approval history

### Text-to-Speech
- Google Cloud Text-to-Speech integration (TTS)
- gTTS (free) fallback in development

### Deep Linking
- Custom URI scheme: `zoneguide://poi/{id}?autoplay=true|false`
- Shell navigation with query parameter support

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Runtime** | .NET 10 |
| **Backend** | ASP.NET Core Web API, Entity Framework Core 9, SQL Server |
| **Admin UI** | Blazor Server, MudBlazor 8, Blazored.LocalStorage |
| **Mobile** | .NET MAUI (Android), CommunityToolkit.Mvvm, Leaflet via WebView |
| **Real-time** | SignalR |
| **Auth** | JWT Bearer (HMAC-SHA512), ASP.NET Core Identity |
| **TTS** | Google.Cloud.TextToSpeech.V1, gTTS |
| **QR** | QRCoder |
| **Mobile DB** | SQLite (sqlite-net-pcl) |
| **Maps** | OpenStreetMap + Leaflet |
| **Scanning** | ZXing.Net.Maui |
| **Audio** | Plugin.Maui.Audio |
| **Dev** | Swagger/Swashbuckle, AutoMapper |

---

## API Overview

### Endpoints
- HTTP: `http://localhost:56042`
- HTTPS: `https://localhost:56040`

### Controllers (16)

| Controller | Route | Purpose |
|---|---|---|
| `AuthController` | `/api/auth` | Login, register, refresh tokens |
| `POIsController` | `/api/pois` | POI CRUD, search, translations |
| `ToursController` | `/api/tours` | Tour CRUD |
| `AnalyticsController` | `/api/analytics` | Dashboard KPIs, heatmap, daily stats |
| `ContributionsController` | `/api/contributions` | POI contribution workflow |
| `AudioController` | `/api/audio` | Audio file management & TTS generation |
| `QRCodesController` | `/api/qrcodes` | QR code generation |
| `SyncController` | `/api/sync` | Mobile data synchronization |
| `UsersController` | `/api/users` | User management |
| `NotificationsController` | `/api/notifications` | Notification CRUD |
| `MobileMonitoringController` | `/api/mobile-monitoring` | Heartbeat & session snapshots |
| `QrMonitoringController` | `/api/qr-monitoring` | QR presence heartbeat |
| `TtsController` | `/api/tts` | gTTS fallback generation |
| `ActivityLogController` | `/api/activity-log` | Audit trail |
| `PublicPoiController` | `/api/public-poi` | Public POI endpoints |
| `PublicWebController` | `/api/public-web` | Web app data serving |

### Middleware Pipeline
1. Security headers (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, CSP)
2. Static files (`wwwroot/`)
3. CORS (AllowAll — any origin/method/header)
4. Rate limiting: Auth (10/min, queue 2), General (200/min)
5. JWT Authentication + Authorization
6. MVC Controllers + SignalR Hubs

---

## Admin Dashboard

- **URL:** `http://localhost:56043` (HTTP) or `https://localhost:56041` (HTTPS)
- **UI Framework:** MudBlazor
- **Key pages:** POI management, tour management, user management, contributions, analytics/heatmap, QR code management, notifications, activity log, live mobile tracking
- **Public pages:** Home, map, POI list/detail, tour list/detail, history, settings

### Services
| Service | Purpose |
|---|---|
| `ApiService` | HTTP client for all API calls |
| `AuthTokenHandler` | Delegating handler for automatic JWT Bearer injection |
| `WebTtsService` | Web-based TTS generation |
| `AudioPlayerInterop` | JS interop for browser audio playback |

---

## Mobile App

- **App ID:** `com.ZoneGuide.app`
- **Min SDK:** 21, **Target SDK:** 34
- **Architecture:** MVVM (CommunityToolkit.Mvvm)

### Views (31 XAML pages)
| Page | Purpose |
|---|---|
| `HomePage` | Nearby places, featured tours, continue listening |
| `MapPage` | Leaflet map with POI markers, route rendering |
| `MainPage` | Core tracking controls + status display |
| `POIListPage` | Searchable POI list with category filters |
| `POIDetailPage` | POI detail with narration, map link, navigation |
| `TourListPage` | Curated tours listing |
| `TourDetailPage` | Tour detail with POI list, offline download |
| `QRScannerPage` | ZXing camera scanner |
| `HistoryPage` | Listening history with replay |
| `OfflinePage` | Downloaded content management |
| `SettingsPage` | Audio, GPS, sync, language, voice settings |
| `LanguageSelectionPage` | Initial language wizard |
| `MiniPlayerView` | Compact now-playing overlay |

### Services (15)
| Service | Purpose |
|---|---|
| `ApiService` | HTTP API client |
| `AudioService` | Playback via Plugin.Maui.Audio |
| `DatabaseService` | SQLite init & management |
| `GeofenceService` | Geofence event detection, scoring, deduplication |
| `LocationService` | GPS tracking with configurable accuracy |
| `MobilePresenceService` | Heartbeat for admin real-time monitoring |
| `NarrationService` | Narration queue, playback orchestration |
| `PoiScoringService` | Intelligent POI selection on overlap |
| `SyncService` | Server sync + analytics upload |
| `SettingsService` | Persisted app settings |
| `TTSService` | Device TTS engine |
| `UserSessionService` | Session management |
| `DistanceUnitService` | m/km formatting |
| `Repositories` | SQLite CRUD for POIs, tours, translations, analytics |
| `AppLinkDispatcher` | Deep link handling |

---

## Database

### SQL Server (Server-side via EF Core)
Tables: `POIs`, `POITranslations`, `Tours`, `TourTranslations`, `TourPOIs`, `LocationHistories`, `NarrationHistories`, `POIStatistics`, `DeletedRecords`, `Users`, `POIContributions`, `POIApprovalHistories`, `ActivityLogs`, `Notifications`

Key indexes: `UniqueCode` on POI/Tour, `(POIId, LanguageCode)` on translations, `Email` on Users.

13 EF Core migrations as of June 2026.

### SQLite (Mobile-side via sqlite-net-pcl)
Local cache with sync to server.

---

## Getting Started

### Prerequisites
- Visual Studio 2022+ or JetBrains Rider
- .NET 10 SDK
- SQL Server Express (or full SQL Server)
- Android SDK 34+ (for mobile build)

### Setup

```bash
git clone <repo-url>
cd ZoneGuide
```

1. Open `ZoneGuide.sln`
2. Restore NuGet packages
3. Configure `src/ZoneGuide.API/appsettings.json`:
   - Set `ConnectionStrings.DefaultConnection` to your SQL Server
   - Set `PublicWebApp.BaseUrl` and `AdminBaseUrl` to your IP
   - (Dev) Copy or create `appsettings.Development.json` with seed passwords:
     ```json
     { "SeedAccounts": { "AdminPassword": "Admin@123", "UserPassword": "User@123" } }
     ```
4. Set multiple startup projects: `ZoneGuide.API` + `ZoneGuide.Admin`
5. Build and run — API auto-migrates and seeds database on first launch

### Seed Accounts (Dev)
| Role | Email | Password |
|---|---|---|
| Admin | admin@ZoneGuide.com | Admin@123 |
| User | user@ZoneGuide.com | User@123 |

### Scripts

```powershell
# Restart API on ports 56042/56040 with optional ngrok tunnel
.\scripts\restart-api.ps1 [-SkipBuild] [-TunnelUrl "https://xxx.ngrok-free.dev"]
```

---

## Project Status

### What's Working
- Full POI/Tour CRUD with multi-language translations
- JWT authentication with role-based authorization
- SignalR real-time monitoring (mobile + QR)
- Analytics dashboard with heatmap
- QR code generation and scanning
- Mobile geofencing with auto-narration
- Offline SQLite sync
- Contributor workflow
- Admin dashboard (MudBlazor)
- Public web PWA for QR landing pages

### In Progress
- **Google Maps → OpenStreetMap (Leaflet)** migration on mobile
- **gTTS free TTS fallback** integration (replacing ElevenLabs)
- Build & verification pipeline

### Not Yet Implemented
- Docker support
- CI/CD pipeline (GitHub Actions)
- iOS support
- License file

---

## Configuration Reference

### API (`src/ZoneGuide.API/appsettings.json`)

| Key | Description |
|---|---|
| `ConnectionStrings.DefaultConnection` | SQL Server connection string |
| `GoogleCloud.ApiKey` | Google Cloud API key for TTS |
| `PublicWebApp.BaseUrl` | Public-facing API URL |
| `PublicWebApp.AdminBaseUrl` | Public-facing Admin URL |
| `PublicWebApp.TunnelBaseUrl` | ngrok tunnel URL override |
| `PublicWebApp.PreferTunnel` | Use tunnel URL instead of direct |
| `SeedAccounts.AdminEmail` | Default admin email |
| `SeedAccounts.UserEmail` | Default user email |

### Admin (`src/ZoneGuide.Admin/appsettings.json`)

| Key | Description |
|---|---|
| `ApiBaseUrl` | Backend API URL (`http://localhost:56042`) |

### Mobile (`src/ZoneGuide.Mobile`)

| Setting | Location |
|---|---|
| API base URL | Configured in `ApiService.cs` |
| Deep link scheme | `zoneguide://` in `AppLinkDispatcher.cs` |
| App ID | `com.ZoneGuide.app` in `.csproj` |
