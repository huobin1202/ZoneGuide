# Task Plan

## 1. Mobile App: Replace Google Maps → OpenStreetMap (Leaflet WebView)
- [x] Analyze current map implementation
- [ ] Remove Google Maps SDK dependencies (packages, manifest, MauiProgram)
- [ ] Create Leaflet map HTML (bundled as raw asset)
- [ ] Rewrite MapPage.xaml (WebView instead of `<maps:Map>`)
- [ ] Rewrite MapPage.xaml.cs (JS interop instead of Google Maps)
- [ ] Update MapViewModel (remove MAUI Maps types, use simple models)
- [ ] Handle polyline/route rendering via Leaflet
- [ ] Update LocationService if needed

## 2. Admin: Add Free TTS Audio Generation (replace ElevenLabs)
- [x] Analyze current audio/TTS implementation
- [ ] Create TTS generation service (gTTS - free Google Translate TTS)
- [ ] Add API endpoint POST /api/audio/generate-tts
- [ ] Add "Generate Audio" button in POI dialog
- [ ] Add "Generate Audio" button in translations
- [ ] Update ApiService to support audio generation

## 3. Build & Verify
- [ ] Build mobile app
- [ ] Build admin web app
