using System.IO;

namespace WhalePet;

/// <summary>Removes every user-data location used by the pet for a thorough uninstall.</summary>
public static class UserDataCleaner
{
    public static void DeleteAll()
    {
        DeleteDirectory(PersistentStore.LocalDirectory);
        DeleteDirectory(PersistentStore.RoamingDirectory);
        DeleteDirectory(PersistentStore.HomeDirectory);
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                    // Best effort: a locked cache file must not abort the uninstall.
                }
            }
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
