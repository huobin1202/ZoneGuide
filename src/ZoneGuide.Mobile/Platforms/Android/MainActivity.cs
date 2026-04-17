using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using ZoneGuide.Mobile.Services;

namespace ZoneGuide.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | 
    ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "zoneguide",
    DataHost = "poi")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    private static void HandleIntent(Intent? intent)
    {
        var uri = intent?.Data;
        if (uri == null || !string.Equals(uri.Scheme, "zoneguide", StringComparison.OrdinalIgnoreCase))
            return;

        var pathSegments = uri.PathSegments;
        if (pathSegments == null || pathSegments.Count == 0)
            return;

        var poiSegment = pathSegments[0]?.Trim('/');
        if (string.IsNullOrWhiteSpace(poiSegment))
            return;

        var autoplay = uri.GetQueryParameter("autoplay");
        AppLinkDispatcher.Publish(new Uri($"zoneguide://poi/{poiSegment}?autoplay={autoplay ?? "true"}"));
    }
}
