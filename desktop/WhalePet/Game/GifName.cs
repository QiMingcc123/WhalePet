using System.IO;

namespace WhalePet.Game;

/// <summary>File-name based display label shared by the avatar switch and GIF wall.</summary>
public static class GifName
{
    public static string Display(string? gifId)
    {
        var file = Path.GetFileName((gifId ?? "").Trim());
        var stem = Path.GetFileNameWithoutExtension(file).Trim();
        var separator = stem.IndexOf('_');
        if (separator >= 0) stem = stem[(separator + 1)..].Trim();
        return stem.Length > 0 ? stem : Path.GetFileNameWithoutExtension(file).Trim();
    }
}
