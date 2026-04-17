namespace ZoneGuide.Mobile.Services;

public static class AppLinkDispatcher
{
    private static readonly object SyncRoot = new();
    private static Uri? _pendingUri;

    public static event EventHandler<Uri>? UriReceived;

    public static void Publish(Uri uri)
    {
        lock (SyncRoot)
        {
            _pendingUri = uri;
        }

        UriReceived?.Invoke(null, uri);
    }

    public static Uri? ConsumePendingUri()
    {
        lock (SyncRoot)
        {
            var uri = _pendingUri;
            _pendingUri = null;
            return uri;
        }
    }
}
