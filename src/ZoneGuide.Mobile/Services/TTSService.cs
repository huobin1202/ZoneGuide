using System.Text.RegularExpressions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using ZoneGuide.Shared.Interfaces;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service Text-to-Speech đa ngôn ngữ
/// </summary>
public class TTSService : ITTSService
{
    public event EventHandler? SpeakStarted;
    public event EventHandler? SpeakCompleted;

    private CancellationTokenSource? _cancellationTokenSource;
    private string _currentVoice = string.Empty;
    private float _speed = 1.0f;
    private float _volume = 1.0f;

    public bool IsSpeaking { get; private set; }

    public async Task SpeakAsync(string text, string language = "vi-VN")
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        await StopAsync();

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsSpeaking = true;
            SpeakStarted?.Invoke(this, EventArgs.Empty);

            var prepared = PrepareTextForSpeech(text);
            var locales = (await TextToSpeech.GetLocalesAsync()).ToList();
            var locale = PickBestLocale(locales, language, _currentVoice);

            var rate = Math.Clamp(_speed, 0.1f, 2.0f);

            var options = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = _volume,
                Rate = rate
            };

            if (locale != null)
                options.Locale = locale;

            await TextToSpeech.SpeakAsync(prepared, options, _cancellationTokenSource.Token);

            IsSpeaking = false;
            SpeakCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            IsSpeaking = false;
        }
        catch (Exception ex)
        {
            IsSpeaking = false;
            System.Diagnostics.Debug.WriteLine($"TTS Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Chuẩn hóa văn bản để đọc trôi chảy hơn (khoảng trắng, dấu gạch nối từ).
    /// </summary>
    private static string PrepareTextForSpeech(string text)
    {
        var s = text.Trim();
        if (s.Length == 0)
            return s;

        s = Regex.Replace(s, @"\s+", " ");
        s = s.Replace("—", " — ").Replace("–", " – ");
        s = Regex.Replace(s, @"\s*([.,;:!?])\s*", "$1 ");
        s = Regex.Replace(s, @"\s*-\s*", " - ");
        return s.Trim();
    }

    /// <summary>
    /// Chọn locale theo mã BCP-47, ưu tiên giọng người dùng chọn và bản địa khớp vùng.
    /// </summary>
    private static Locale? PickBestLocale(
        IReadOnlyList<Locale> locales,
        string requestedTag,
        string? preferredVoiceLabel)
    {
        if (locales.Count == 0)
            return null;

        requestedTag = (requestedTag ?? "vi-VN").Trim().Replace('_', '-');
        if (string.IsNullOrEmpty(requestedTag))
            requestedTag = "vi-VN";

        var parts = requestedTag.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var primary = parts.Length > 0 ? parts[0].ToLowerInvariant() : "vi";
        string? region = null;
        if (parts.Length > 1)
        {
            var second = parts[1];
            region = second.Length == 2 || second.Length == 3
                ? second.ToUpperInvariant()
                : null;
        }

        bool LangMatches(Locale l)
        {
            var id = l.Id.Replace('_', '-');
            if (l.Language.Equals(primary, StringComparison.OrdinalIgnoreCase))
                return true;
            if (id.Equals(primary, StringComparison.OrdinalIgnoreCase))
                return true;
            if (id.StartsWith(primary + "-", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        var list = locales.Where(LangMatches).ToList();
        if (list.Count == 0)
        {
            list = locales
                .Where(l =>
                    l.Name.Contains(requestedTag, StringComparison.OrdinalIgnoreCase) ||
                    l.Id.Replace('_', '-').Contains(requestedTag, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (list.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(preferredVoiceLabel))
        {
            var exact = list.FirstOrDefault(l =>
                string.Equals($"{l.Name} ({l.Language})", preferredVoiceLabel, StringComparison.Ordinal));
            if (exact != null)
                return exact;

            var openParen = preferredVoiceLabel.IndexOf(" (", StringComparison.Ordinal);
            var namePart = (openParen > 0 ? preferredVoiceLabel[..openParen] : preferredVoiceLabel).Trim();
            if (namePart.Length > 0)
            {
                var byName = list.FirstOrDefault(l =>
                    l.Name.Equals(namePart, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                    return byName;
                byName = list.FirstOrDefault(l =>
                    l.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                    return byName;
            }
        }

        if (region != null)
        {
            var byRegion = list.FirstOrDefault(l =>
            {
                var id = l.Id.Replace('_', '-');
                return id.EndsWith("-" + region, StringComparison.OrdinalIgnoreCase)
                       || (!string.IsNullOrEmpty(l.Country)
                           && l.Country.Equals(region, StringComparison.OrdinalIgnoreCase));
            });
            if (byRegion != null)
                return byRegion;
        }

        var idExact = list.FirstOrDefault(l =>
            l.Id.Replace('_', '-').Equals(requestedTag, StringComparison.OrdinalIgnoreCase));
        if (idExact != null)
            return idExact;

        var neural = list.FirstOrDefault(l =>
            l.Id.Contains("network", StringComparison.OrdinalIgnoreCase)
            || l.Name.Contains("neural", StringComparison.OrdinalIgnoreCase)
            || l.Name.Contains("Natural", StringComparison.OrdinalIgnoreCase));
        if (neural != null)
            return neural;

        return list.OrderBy(l => l.Id, StringComparer.OrdinalIgnoreCase).First();
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        IsSpeaking = false;
    }

    public async Task<List<string>> GetSupportedLanguagesAsync()
    {
        var locales = await TextToSpeech.GetLocalesAsync();
        return locales.Select(l => l.Language).Distinct().ToList();
    }

    public async Task<List<string>> GetVoicesAsync(string language)
    {
        var locales = await TextToSpeech.GetLocalesAsync();
        return locales
            .Where(l => l.Language.StartsWith(language.Split('-')[0], StringComparison.OrdinalIgnoreCase))
            .Select(l => $"{l.Name} ({l.Language})")
            .ToList();
    }

    public void SetVoice(string voiceId)
    {
        _currentVoice = voiceId;
    }

    public void SetSpeed(float speed)
    {
        _speed = Math.Clamp(speed, 0.5f, 2.0f);
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0.0f, 1.0f);
    }
}
