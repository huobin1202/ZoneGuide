using Microsoft.Maui.ApplicationModel;

namespace ZoneGuide.Mobile.Views;

internal static class QrScannerNavigationHelper
{
    public static async Task OpenScannerAsync(Page page)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
        {
            await page.DisplayAlert("Không có quyền camera", "Bạn cần cấp quyền camera để quét mã QR.", "OK");
            return;
        }

        await Shell.Current.GoToAsync(nameof(QRScannerPage));
    }
}

