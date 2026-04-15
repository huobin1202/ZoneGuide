using System.Text.RegularExpressions;
using System.Linq;
using ZXing;
using Microsoft.Maui.ApplicationModel;
using System.Collections;

namespace ZoneGuide.Mobile.Views;

public partial class QRScannerPage : ContentPage
{
    private bool _handled;
    private readonly Regex _poiRegex = new(@"^POI:(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public QRScannerPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = InitializeScannerAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_reader != null)
        {
            _reader.IsDetecting = false;
        }
    }

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        // We open this page via Shell navigation; go back by one level.
        _ = Shell.Current.GoToAsync("..");
    }

    private async void OnScanFromPhotoClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Chưa hỗ trợ", "Tính năng scan từ ảnh đang được phát triển.", "OK");
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
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (_reader != null)
        {
            _reader.IsDetecting = true;
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

            var match = _poiRegex.Match(text);
            if (!match.Success)
                return;

            if (!int.TryParse(match.Groups[1].Value, out var poiId))
                return;

            _handled = true;

            if (_reader != null)
                _reader.IsDetecting = false;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync($"//map?poiId={poiId}&autoplay=true");
            });
        }
        catch
        {
            // Ignore unexpected barcode payloads to avoid crashing scanner page.
        }
    }
}

