using System.IO;
using System.Text;

namespace WhalePet;

/// <summary>
/// Stores small JSON documents (settings, memos) in two independent profile
/// folders and keeps rolling backups. Replacing the executable never touches
/// these folders, and a torn or corrupt write can be recovered from the newest
/// valid backup instead of silently falling back to defaults.
/// </summary>
public static class PersistentStore
{
    private const int MaxBackups = 12;

    public static string LocalDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DSHWhalePet");

    public static string RoamingDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DSHWhalePet");

    /// <summary>Extra copy in the user profile, untouched by application updates or AppData cleanup.</summary>
    public static string HomeDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".whalepet");

    public static string BackupDirectory { get; } = Path.Combine(LocalDirectory, "backup");

    public static string PrimaryPath(string fileName) => Path.Combine(LocalDirectory, fileName);

    public static string RoamingPath(string fileName) => Path.Combine(RoamingDirectory, fileName);

    public static string HomePath(string fileName) => Path.Combine(HomeDirectory, fileName);

    /// <summary>
    /// Returns the contents of every known copy, newest first. Callers try to
    /// deserialize each one until a valid document is found.
    /// </summary>
    public static IEnumerable<string> ReadCandidates(string fileName)
    {
        var paths = new List<string>();
        var stem = Path.GetFileNameWithoutExtension(fileName);

        void Add(string path)
        {
            try
            {
                if (File.Exists(path)) paths.Add(path);
            }
            catch
            {
                // An unreadable path is simply skipped.
            }
        }

        Add(PrimaryPath(fileName));
        Add(RoamingPath(fileName));
        Add(HomePath(fileName));
        Add(Path.Combine(BackupDirectory, stem + ".lastgood.json"));

        try
        {
            if (Directory.Exists(BackupDirectory))
            {
                paths.AddRange(Directory.GetFiles(BackupDirectory, stem + "-*.json"));
            }
        }
        catch
        {
            // Backups are optional.
        }

        foreach (var legacy in LegacyPaths(fileName)) Add(legacy);

        foreach (var path in paths
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(SafeWriteTime))
        {
            var text = SafeRead(path);
            if (!string.IsNullOrWhiteSpace(text)) yield return text;
        }
    }

    /// <summary>Writes the primary copy, the roaming copy and a rolling backup.</summary>
    public static bool Save(string fileName, string json)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var saved = WriteAtomic(PrimaryPath(fileName), json);
        saved |= WriteAtomic(RoamingPath(fileName), json);
        saved |= WriteAtomic(HomePath(fileName), json);

        var backup = Path.Combine(BackupDirectory, stem + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        WriteAtomic(backup, json);
        WriteAtomic(Path.Combine(BackupDirectory, stem + ".lastgood.json"), json);
        PruneBackups(stem);
        return saved;
    }

    private static IEnumerable<string> LegacyPaths(string fileName)
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, fileName);
        if (fileName.Equals("settings.json", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(baseDirectory, "config.json");
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(local, "WhalePet", fileName);
        yield return Path.Combine(roaming, "WhalePet", fileName);
        yield return Path.Combine(local, "DSHWhalePet", "config.json");
    }

    private static bool WriteAtomic(string path, string content)
    {
        string? temp = null;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) return false;
            Directory.CreateDirectory(directory);

            temp = path + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(temp, path, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, path);
            }
            return true;
        }
        catch
        {
            try
            {
                if (temp is not null && File.Exists(temp))
                {
                    File.Copy(temp, path, overwrite: true);
                    File.Delete(temp);
                    return true;
                }
            }
            catch
            {
                // Leave the previous copy in place.
            }
            return false;
        }
    }

    private static void PruneBackups(string stem)
    {
        try
        {
            if (!Directory.Exists(BackupDirectory)) return;
            var files = Directory.GetFiles(BackupDirectory, stem + "-*.json")
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Skip(MaxBackups)
                .ToList();
            foreach (var file in files)
            {
                try { file.Delete(); } catch { }
            }
        }
        catch
        {
            // Cleanup is best effort.
        }
    }

    private static DateTime SafeWriteTime(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static string? SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }
}
