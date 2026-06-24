namespace ZoneGuide.Shared.Models;

/// <summary>
/// Request to generate TTS audio via Google Translate TTS (free, no API key).
/// </summary>
public class GenerateTtsRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; }
}

/// <summary>
/// Response from TTS generation endpoint.
/// </summary>
public class GenerateTtsResponse
{
    public bool Success { get; set; }
    public string? AudioUrl { get; set; }
    public string? AudioPath { get; set; }
    public string? FileName { get; set; }
    public int ContentLength { get; set; }
    public string? Error { get; set; }
}
