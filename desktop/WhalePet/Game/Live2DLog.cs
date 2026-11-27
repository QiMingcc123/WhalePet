using System.IO;

namespace WhalePet.Game;

/// <summary>Small rolling log for the Live2D host. Keeps the latest three days.</summary>
public static class Live2DLog
{
    private static readonly object Gate = new();
    private static bool _cleaned;

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                CleanupCore();
                var directory = Path.Combine(PersistentStore.LocalDirectory, "logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, $"log-{DateTime.Now:yyyy-MM-dd}.txt"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never affect the pet.
        }
    }

    public static void Cleanup()
    {
        lock (Gate) CleanupCore();
    }

    private static void CleanupCore()
    {
        if (_cleaned) return;
        _cleaned = true;
        try
        {
            var directory = Path.Combine(PersistentStore.LocalDirectory, "logs");
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.GetFiles(directory, "log-*.txt"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-3))
                        File.Delete(file);
                }
                catch { }
            }
        }
        catch { }
    }
}
