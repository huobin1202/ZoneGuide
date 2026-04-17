using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace ZoneGuide.Mobile.Views;

internal static class QrScannerNavigationHelper
{
    public static async Task OpenScannerAsync(Page page)
    {
        if (page.Navigation.ModalStack.LastOrDefault() is QRScannerPage)
        {
            return;
        }

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

        var services = page.Handler?.MauiContext?.Services ?? Shell.Current?.Handler?.MauiContext?.Services;
        var scannerPage = services?.GetRequiredService<QRScannerPage>() ?? new QRScannerPage();
        await page.Navigation.PushModalAsync(scannerPage);
    }
}

