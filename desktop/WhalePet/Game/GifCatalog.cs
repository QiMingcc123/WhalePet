using System.IO;
using System.Reflection;
using System.Text.Json;

namespace WhalePet.Game;

public sealed class GifManifest
{
    public string PackageVersion { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public int EntryCount { get; set; }
    public List<GifEntry> Entries { get; set; } = new();
}

public sealed class GifEntry
{
    public string GifId { get; set; } = "";
    public int SortIndex { get; set; }
    public long Offset { get; set; }
    public int Length { get; set; }
    public string Nonce { get; set; } = "";
    public string Tag { get; set; } = "";
    public string UnlockType { get; set; } = "";
    public string UnlockCondition { get; set; } = "";
}

/// <summary>
/// Embedded resource mapping. This is the single source of truth for GIF
/// unlock order, level conditions and achievement conditions.
/// </summary>
public sealed class GifCatalog
{
    public const string ManifestResource = "WhalePet.Resources.gifs.manifest.json";
    public const string PackageResource = "WhalePet.Resources.gifs.enc";
    public const string FishingGifId = "fishing/Fishing.gif";
    public const string CookingGifId = "cooking/Cooking.gif";

    private readonly Dictionary<string, GifEntry> _byId;
    private readonly List<GifEntry> _ordered;

    public GifCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ManifestResource)
            ?? throw new InvalidOperationException("缺少内置 GIF 资源清单。");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        Manifest = JsonSerializer.Deserialize<GifManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("GIF 资源清单损坏。");

        _ordered = Manifest.Entries.OrderBy(entry => entry.SortIndex).ToList();
        _byId = _ordered.ToDictionary(entry => entry.GifId, StringComparer.OrdinalIgnoreCase);
    }

    public GifManifest Manifest { get; }
    public IReadOnlyList<GifEntry> Entries => _ordered;
    public string PackageVersion => Manifest.PackageVersion;

    public GifEntry? Find(string? gifId) =>
        !string.IsNullOrWhiteSpace(gifId) && _byId.TryGetValue(gifId, out var entry) ? entry : null;

    public bool IsSpecial(GifEntry entry) =>
        string.Equals(entry.UnlockType, "Special", StringComparison.OrdinalIgnoreCase);


    public string? NextLevelGif(IReadOnlySet<string> unlocked)
    {
        return _ordered
            .Where(entry => entry.UnlockType == "Level"
                            && int.TryParse(entry.UnlockCondition, out _)
                            && !unlocked.Contains(entry.GifId))
            .OrderBy(entry => entry.SortIndex)
            .Select(entry => entry.GifId)
            .FirstOrDefault();
    }

    public string? AchievementGif(string achievementId)
    {
        return _ordered
            .FirstOrDefault(entry => entry.UnlockType == "Achievement"
                                     && string.Equals(entry.UnlockCondition, achievementId, StringComparison.OrdinalIgnoreCase))
            ?.GifId;
    }

    public int LevelGifCount => _ordered.Count(entry => entry.UnlockType == "Level");
    public int AchievementGifCount => _ordered.Count(entry => entry.UnlockType == "Achievement");
}
