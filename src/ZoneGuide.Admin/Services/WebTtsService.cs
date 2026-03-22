using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace ZoneGuide.Admin.Services;

/// <summary>
/// Gọi Web Speech API trong trình duyệt (nghe thử TTS trên admin).
/// </summary>
public sealed class WebTtsService(IJSRuntime js)
{
    public async Task<WebTtsSpeakResult> SpeakAsync(
        string? text,
        string? language,
        double rate = 1.0,
        double pitch = 1.0,
        double volume = 1.0,
        string? voiceUri = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new WebTtsSpeakResult(false, "empty-text");

        try
        {
            var lang = string.IsNullOrWhiteSpace(language) ? "vi-VN" : language!;
            var dto = await js.InvokeAsync<WebTtsSpeakJsDto?>(
                "zoneGuideTts.speak",
                text,
                lang,
                new WebTtsSpeakOptions(rate, pitch, volume, string.IsNullOrEmpty(voiceUri) ? null : voiceUri));
            return new WebTtsSpeakResult(dto?.Ok == true, dto?.Error);
        }
        catch (JSDisconnectedException)
        {
            return new WebTtsSpeakResult(false, "disconnected");
        }
        catch (JSException ex)
        {
            return new WebTtsSpeakResult(false, ex.Message);
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await js.InvokeVoidAsync("zoneGuideTts.stop");
        }
        catch (JSDisconnectedException)
        {
            // ignore
        }
    }

    public async Task<IReadOnlyList<WebTtsVoiceInfo>> GetVoicesAsync()
    {
        try
        {
            var list = await js.InvokeAsync<List<WebTtsVoiceInfo>?>("zoneGuideTts.listVoices");
            return list ?? (IReadOnlyList<WebTtsVoiceInfo>)Array.Empty<WebTtsVoiceInfo>();
        }
        catch (JSDisconnectedException)
        {
            return Array.Empty<WebTtsVoiceInfo>();
        }
        catch (JSException)
        {
            return Array.Empty<WebTtsVoiceInfo>();
        }
    }

    private sealed record WebTtsSpeakOptions(
        [property: JsonPropertyName("rate")] double Rate,
        [property: JsonPropertyName("pitch")] double Pitch,
        [property: JsonPropertyName("volume")] double Volume,
        [property: JsonPropertyName("voiceUri")] string? VoiceUri);

    private sealed class WebTtsSpeakJsDto
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}

public readonly record struct WebTtsSpeakResult(bool Ok, string? Error);

public sealed class WebTtsVoiceInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "";

    [JsonPropertyName("voiceURI")]
    public string VoiceUri { get; set; } = "";

    [JsonPropertyName("localService")]
    public bool LocalService { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}
