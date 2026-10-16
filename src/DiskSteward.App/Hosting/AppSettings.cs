using System.Text.Json.Serialization;

namespace DiskSteward.App.Hosting;

// Saved as settings.json. Changing Language or digits takes effect after a restart.
public sealed record AppSettings
{
    // Empty = follow the system locale. Otherwise a specific culture, e.g. "ar-SA" or "en-US".
    public string Language { get; init; } = "";

    public bool ArabicIndicDigits { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
    public AppTheme Theme { get; init; } = AppTheme.System;

    // Null = follow the Windows accent; otherwise one of AccentPresets (hex, e.g. "#0F6CBD").
    public string? Accent { get; init; }
}
