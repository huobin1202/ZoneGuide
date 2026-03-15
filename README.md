# ZoneGuide - GPS-Based Tour Guide System

A comprehensive GPS-based tour guide application with real-time location tracking, automatic audio narration, and content management capabilities.

![QR code linking to the ZoneGuide repository or application download page](image.png)

## 🎯 Features

### Mobile Application (MAUI)
- **GPS Tracking**: Real-time location tracking in foreground and background with battery optimization
- **Geofence/POI Triggering**: Automatic detection when users enter POI areas with configurable radius, debounce, and cooldown
- **Auto-Narration**: Text-to-speech with multi-language support and audio queue management
- **Map View**: Interactive map displaying user location and POIs
- **Offline Support**: Local SQLite database for offline functionality
- **Data Sync**: Efficient synchronization with backend API

### Web API Backend
- **RESTful API**: Complete CRUD operations for POIs and Tours
- **Data Sync**: Incremental sync support for mobile clients
- **Analytics**: Anonymous location and narration tracking
- **Heatmap Data**: Aggregated visitor location data

### Admin Portal (Blazor)
- **Dashboard**: Key metrics and statistics overview
- **POI Management**: Create, edit, delete POIs with multi-language support
- **Tour Management**: Create tours by ordering POIs
- **Analytics View**: Top POIs, completion rates, daily stats
- **Heatmap Visualization**: Visitor distribution data

## 🏗️ Architecture

```
ZoneGuide/
├── src/
│   ├── ZoneGuide.Shared/          # Shared models, DTOs, interfaces
│   ├── ZoneGuide.Mobile/          # .NET MAUI mobile app
│   ├── ZoneGuide.API/             # ASP.NET Core Web API
│   └── ZoneGuide.Admin/           # Blazor Server admin portal
└── ZoneGuide.sln
```

## 🔧 Technology Stack

- **.NET 8.0** - Target framework
- **.NET MAUI** - Cross-platform mobile (Android/iOS)
- **ASP.NET Core** - Web API backend
- **Blazor Server** - Admin portal
- **Entity Framework Core** - ORM
- **SQLite** - Mobile offline storage
- **SQL Server** - Backend database
- **MudBlazor** - Admin UI components

## 📱 Mobile App Features

### Location Services
```csharp
// Configurable accuracy levels
public enum LocationAccuracy
{
    Low,      // 500m - battery saving
    Medium,   // 100m - balanced
    High      // 10m - navigation
}
```

### Geofence Monitoring
- Configurable trigger radius per POI
- Debounce mechanism to prevent rapid triggers
- Cooldown period between POI visits
- Priority-based narration queue

### Audio Narration
- Text-to-speech support
- Pre-recorded audio file support
- Queue management for multiple POIs
- Anti-duplicate mechanisms

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 with MAUI workload
- SQL Server (for API)
- Android SDK / Xcode (for mobile development)

### Setup

1. **Clone the repository**
```bash
git clone <repository-url>
cd ZoneGuide
```

2. **Restore packages**
```bash
dotnet restore
```

3. **Setup Database (API)**
```bash
cd src/ZoneGuide.API
dotnet ef database update
```

4. **Run API**
```bash
dotnet run --project src/ZoneGuide.API
```

5. **Run Admin Portal**
```bash
dotnet run --project src/ZoneGuide.Admin
```

6. **Run Mobile App**
```bash
dotnet build src/ZoneGuide.Mobile -t:Run -f net8.0-android
```
## Build APK
```bash
dotnet publish src/ZoneGuide.Mobile/ZoneGuide.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```
## 📡 API Endpoints

### POIs
- `GET /api/pois` - Get all POIs
- `GET /api/pois/{id}` - Get POI by ID
- `GET /api/pois/nearby?lat=&lon=&radius=` - Get nearby POIs
- `POST /api/pois` - Create POI
- `PUT /api/pois/{id}` - Update POI
- `DELETE /api/pois/{id}` - Delete POI

### Tours
- `GET /api/tours` - Get all tours
- `GET /api/tours/{id}` - Get tour by ID
- `GET /api/tours/{id}/details` - Get tour with POI details
- `POST /api/tours` - Create tour
- `PUT /api/tours/{id}` - Update tour
- `DELETE /api/tours/{id}` - Delete tour

### Sync
- `POST /api/sync` - Sync data (incremental)
- `GET /api/sync/version` - Get content version
- `GET /api/sync/full` - Full sync

### Analytics
- `POST /api/analytics/upload` - Upload analytics data
- `GET /api/analytics/dashboard` - Get dashboard metrics
- `GET /api/analytics/top-pois` - Get top POIs
- `GET /api/analytics/heatmap` - Get heatmap data

## 📊 Analytics Features

### Anonymous Tracking
- Device-specific anonymous IDs
- No personal data collection
- Location history with configurable retention

### Metrics
- Listen counts per POI
- Completion rates
- Average listen duration
- Daily/weekly/monthly trends
- Visitor heatmaps

## ⚙️ Configuration

### Mobile App Settings
```json
{
  "ApiBaseUrl": "https://your-api.com",
  "DefaultLanguage": "vi-VN",
  "LocationAccuracy": "High",
  "GeofenceCooldownSeconds": 300,
  "AutoSyncIntervalMinutes": 30
}
```

### API Configuration (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=ZoneGuide;..."
  }
}
```

## 🔒 Privacy

- Anonymous device IDs only
- No personal data stored
- Location data aggregated for analytics
- User consent required for location tracking

## 📝 License

MIT License

## 👥 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit changes
4. Push to the branch
5. Open a Pull Request

## 📞 Support

For issues and questions, please open a GitHub issue.
