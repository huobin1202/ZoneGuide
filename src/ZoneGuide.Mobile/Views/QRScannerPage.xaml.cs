using System.Text.RegularExpressions;
using System.Linq;
using ZXing;
using Microsoft.Maui.ApplicationModel;
using System.Collections;
using ZoneGuide.Mobile.Services;
using System.Globalization;

namespace ZoneGuide.Mobile.Views;

public partial class QRScannerPage : ContentPage
{
    private bool _handled;
    private readonly Regex _poiRegex = new(@"^POI:(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _poiUrlRegex = new(@"(?:^https?://.+/poi/|^zoneguide://poi/)(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public QRScannerPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = SafeInitializeScannerAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_reader != null)
        {
            _reader.IsDetecting = false;
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await CloseScannerAsync();
    }

    private async void OnScanFromPhotoClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Chưa hỗ trợ", "Tính năng scan từ ảnh đang được phát triển.", "OK");
    }

    private async Task CloseScannerAsync()
    {
        if (_reader != null)
        {
            _reader.IsDetecting = false;
        }

        if (Navigation?.ModalStack?.LastOrDefault() == this)
        {
            await Navigation.PopModalAsync();
        }
    }

    private async Task InitializeScannerAsync()
    {
        _handled = false;

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Không có quyền camera", "Bạn cần cấp quyền camera để quét mã QR.", "OK");
            return;
        }

        if (_reader != null)
        {
            _reader.IsDetecting = true;
        }
    }

    private async Task SafeInitializeScannerAsync()
    {
        try
        {
            await InitializeScannerAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrScanner] Initialize failed: {ex}");

            if (Dispatcher.IsDispatchRequired)
            {
                await Dispatcher.DispatchAsync(async () =>
                {
                    await DisplayAlert("Không khởi tạo được camera", "Máy quét QR không thể khởi động trên thiết bị này.", "OK");
                });
                return;
            }

            await DisplayAlert("Không khởi tạo được camera", "Máy quét QR không thể khởi động trên thiết bị này.", "OK");
        }
    }

    // ZXing.Net.MAUI.Controls: BarcodesDetected callback.
    // We intentionally accept `object` for the event args to avoid relying on the event args type
    // (it may be internal/not referenced directly). We then read `Results` via reflection.
    private void OnBarcodesDetected(object? sender, object e)
    {
        if (_handled)
            return;

        try
        {
            var resultsProp = e.GetType().GetProperty("Results");
            var resultsObj = resultsProp?.GetValue(e);
            if (resultsObj is not IEnumerable results)
                return;

            string? text = null;
            foreach (var r in results)
            {
                if (r == null)
                    continue;

                var valueProp = r.GetType().GetProperty("Value");
                text = valueProp?.GetValue(r) as string;
                if (!string.IsNullOrWhiteSpace(text))
                    break;
            }

            if (string.IsNullOrWhiteSpace(text))
                return;

            text = text.Trim();
            ApiService.TrySetPreferredBaseUrlFromQrPayload(text);

            if (!TryExtractPoiId(text, out var poiId))
                return;

            _handled = true;

            if (_reader != null)
                _reader.IsDetecting = false;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _ = NavigateToPoiAsync(poiId);
            });
        }
        catch
        {
            // Ignore unexpected barcode payloads to avoid crashing scanner page.
        }
    }

    private bool TryExtractPoiId(string payload, out int poiId)
    {
        poiId = 0;

        var match = _poiRegex.Match(payload);
        if (!match.Success)
        {
            match = _poiUrlRegex.Match(payload);
        }

        if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out poiId))
            return true;

        if (!Uri.TryCreate(payload, UriKind.Absolute, out var uri))
            return false;

        // Support links like /webapp/?poiId=12 or /webapp/?id=12
        var query = uri.Query?.TrimStart('?') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return false;

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = pair.Split('=', 2);
            if (tokens.Length != 2)
                continue;

            var key = Uri.UnescapeDataString(tokens[0]);
            if (!string.Equals(key, "poiId", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(tokens[1]);
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out poiId))
                return true;
        }

        return false;
    }

    private async Task NavigateToPoiAsync(int poiId)
    {
        try
        {
            await Shell.Current.GoToAsync($"//map?poiId={poiId}&autoplay=true");
            return;
        }
        catch
        {
            // Fallback route navigation in case absolute route parsing fails on specific Shell state.
        }

        try
        {
            await Shell.Current.GoToAsync($"map?poiId={poiId}&autoplay=true");
        }
        catch
        {
            await DisplayAlert("Khong mo duoc trang POI", "Da quet ma QR nhung khong the dieu huong den trang ban do.", "OK");
            _handled = false;
            if (_reader != null)
                _reader.IsDetecting = true;
        }
    }
}

