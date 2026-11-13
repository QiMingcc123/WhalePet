using System.IO;
using System.Windows;

namespace WhalePet;

/// <summary>Extracts the embedded default artwork once so the published exe can be a single file.</summary>
public static class AssetBootstrapper
{
    private static readonly string[] Names = { "main.png", "sleep.png", "play1.png", "play2.png", "play3.png", "play4.png" };

    public static string DirectoryPath { get; } = Path.Combine(SettingsStore.DirectoryPath, "assets");

    public static string PathOf(string name) => Path.Combine(DirectoryPath, name);

    public static void Ensure()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            foreach (var name in Names)
            {
                var target = PathOf(name);
                if (File.Exists(target) && new FileInfo(target).Length > 0) continue;
                var resource = Application.GetResourceStream(new Uri("Assets/" + name, UriKind.Relative));
                if (resource?.Stream is null) continue;
                using var source = resource.Stream;
                using var destination = File.Create(target);
                source.CopyTo(destination);
            }
        }
        catch
        {
            // The pet stays usable without defaults; a configured absolute path can still load.
        }
    }
}
