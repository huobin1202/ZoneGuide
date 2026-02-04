using HeriStepAI.Shared.Interfaces;

namespace HeriStepAI.Mobile.Services;

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

            var options = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = _volume
            };

            // Tìm locale phù hợp
            var locales = await TextToSpeech.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => 
                l.Language.Equals(language.Split('-')[0], StringComparison.OrdinalIgnoreCase) ||
                l.Name.Contains(language, StringComparison.OrdinalIgnoreCase));

            if (locale != null)
            {
                options.Locale = locale;
            }

            await TextToSpeech.SpeakAsync(text, options, _cancellationTokenSource.Token);

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
