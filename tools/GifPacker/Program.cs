using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GifPacker;

/// <summary>
/// Build-time only GIF packer. It never ships with the desktop app and never
/// contains the master secret in source; the caller supplies it on the command
/// line (or through WHALEPET_MASTER_SECRET).
/// </summary>
internal static class Program
{
    private const uint LevelGifCount = 51;
    private const int AchievementCount = 53;
    private static readonly byte[] Magic = "WPGIF001"u8.ToArray();

    private static int Main(string[] args)
    {
        try
        {
            var options = Parse(args);
            if (options is null)
            {
                Console.Error.WriteLine(
                    "usage: GifPacker --input <gifs-dir> --output <gifs.enc> --manifest <gifs.manifest.json> --master <secret> [--version 1.1.0]");
                return 2;
            }

            Pack(options);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("GifPacker failed: " + error.Message);
            return 1;
        }
    }

    private static Options? Parse(string[] args)
    {
        string? input = null, output = null, manifest = null, master = null, version = "1.1.0";
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException("missing value for " + args[i]);
            switch (args[i])
            {
                case "--input": input = Next(); break;
                case "--output": output = Next(); break;
                case "--manifest": manifest = Next(); break;
                case "--master": master = Next(); break;
                case "--version": version = Next(); break;
                default: throw new ArgumentException("unknown argument " + args[i]);
            }
        }

        master ??= Environment.GetEnvironmentVariable("WHALEPET_MASTER_SECRET");
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output) ||
            string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(master))
        {
            return null;
        }

        return new Options(Path.GetFullPath(input), Path.GetFullPath(output), Path.GetFullPath(manifest), master, version);
    }

    private static void Pack(Options options)
    {
        if (!Directory.Exists(options.Input)) throw new DirectoryNotFoundException(options.Input);

        // Unlockable GIFs live in gifs/unlocked. Older layouts kept them at the
        // input root; support both so the tool stays usable during migration.
        var unlockedDir = Path.Combine(options.Input, "unlocked");
        var files = (Directory.Exists(unlockedDir)
                ? Directory.GetFiles(unlockedDir, "*.gif", SearchOption.TopDirectoryOnly)
                : Directory.GetFiles(options.Input, "*.gif", SearchOption.TopDirectoryOnly))
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0) throw new InvalidOperationException("no .gif files in " + options.Input);

        var levelCount = Math.Min(LevelGifCount, (uint)files.Length);
        var achievementCount = Math.Min(AchievementCount, files.Length - (int)levelCount);
        if (files.Length != levelCount + achievementCount)
        {
            Console.WriteLine($"warning: expected {LevelGifCount + AchievementCount} gifs, found {files.Length}");
        }

        // Always-available game assets (fishing/cooking animations) are packed
        // as special entries. They are never part of the unlock catalog.
        var specialFiles = new[] { "fishing", "cooking" }
            .Select(folder => Path.Combine(options.Input, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*.gif", SearchOption.TopDirectoryOnly))
            .OrderBy(path => Path.GetRelativePath(options.Input, path), StringComparer.Ordinal)
            .ToArray();

        var encKey = Derive(options.Master, "GifPackage.Encryption");
        var hmacKey = Derive(options.Master, "GifPackage.Signature");

        var entries = new List<ManifestEntry>(files.Length + specialFiles.Length);
        var payload = new MemoryStream();
        long payloadOffset = Magic.Length + 32;

        void Append(string file, string gifId, string unlockType, string unlockCondition)
        {
            var index = entries.Count;
            var plain = File.ReadAllBytes(file);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[plain.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(encKey, 16))
            {
                aes.Encrypt(nonce, plain, cipher, tag);
            }

            entries.Add(new ManifestEntry
            {
                GifId = gifId,
                SortIndex = index,
                Offset = payloadOffset,
                Length = cipher.Length,
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                UnlockType = unlockType,
                UnlockCondition = unlockCondition,
            });
            payload.Write(cipher);
            payloadOffset += cipher.Length;
        }

        for (var index = 0; index < files.Length; index++)
        {
            Append(
                files[index],
                Path.GetFileName(files[index]),
                index < levelCount ? "Level" : "Achievement",
                index < levelCount
                    ? (index + 2).ToString()
                    : "A" + (index - levelCount + 1).ToString("00"));
        }

        foreach (var file in specialFiles)
        {
            var relative = Path.GetRelativePath(options.Input, file).Replace('\\', '/');
            Append(file, relative, "Special", "Always");
        }

        var manifest = new Manifest
        {
            PackageVersion = options.Version,
            CreatedAt = DateTimeOffset.UtcNow,
            EntryCount = entries.Count,
            Entries = entries,
        };
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        var manifestHash = SHA256.HashData(manifestBytes);

        using var output = new FileStream(options.Output, FileMode.Create, FileAccess.Write, FileShare.None);
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, hmacKey);
        output.Write(Magic);
        output.Write(manifestHash);
        hmac.AppendData(Magic);
        hmac.AppendData(manifestHash);

        payload.Position = 0;
        var buffer = new byte[81920];
        int read;
        while ((read = payload.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            hmac.AppendData(buffer, 0, read);
        }

        output.Write(hmac.GetHashAndReset());
        output.Flush(true);

        File.WriteAllBytes(options.Manifest, manifestBytes);
        Console.WriteLine(
            $"packed {entries.Count} gifs -> {options.Output} ({output.Length:N0} bytes), manifest {options.Manifest}");
    }

    private static byte[] Derive(string master, string purpose)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(master));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(purpose));
    }

    private sealed record Options(string Input, string Output, string Manifest, string Master, string Version);

    private sealed class Manifest
    {
        public string PackageVersion { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public int EntryCount { get; set; }
        public List<ManifestEntry> Entries { get; set; } = new();
    }

    private sealed class ManifestEntry
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
}
