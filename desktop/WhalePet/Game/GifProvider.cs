using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace WhalePet.Game;

public interface IGifProvider
{
    Stream? GetDecryptedStream(string gifId);
    Stream? GetAssetStream(string gifId);
    IReadOnlyList<string> GetUnlockedGifIds();
    bool IsUnlocked(string gifId);
    string PackageVersion { get; }
}

public sealed class GifLockedException : Exception
{
    public GifLockedException(string gifId) : base("动态图尚未解锁：" + gifId) { }
}

public sealed class GifIntegrityException : Exception
{
    public GifIntegrityException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Runtime-only decryptor. Plain GIF bytes exist only inside a caller-owned
/// MemoryStream; nothing is written to disk or cached by this provider.
/// </summary>
public sealed class GifProvider : IGifProvider
{
    private static readonly byte[] Magic = "WPGIF001"u8.ToArray();
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    private readonly GifCatalog _catalog;
    private readonly Func<string, bool> _isUnlocked;
    private readonly byte[] _encryptionKey;
    private readonly byte[] _signatureKey;
    private readonly object _gate = new();
    private byte[] _manifestHash = Array.Empty<byte>();
    private long _packageLength;

    public string PackageSignature { get; private set; } = "";

    public GifProvider(GifCatalog catalog, Func<string, bool> isUnlocked)
    {
        _catalog = catalog;
        _isUnlocked = isUnlocked;
        _encryptionKey = ResourceSecrets.Derive("GifPackage.Encryption");
        _signatureKey = ResourceSecrets.Derive("GifPackage.Signature");
        VerifyPackage();
    }

    public string PackageVersion => _catalog.PackageVersion;

    public IReadOnlyList<string> GetUnlockedGifIds() =>
        _catalog.Entries
            .Where(entry => !_catalog.IsSpecial(entry) && _isUnlocked(entry.GifId))
            .Select(entry => entry.GifId)
            .ToList();

    public bool IsUnlocked(string gifId) => _isUnlocked(gifId);

    /// <summary>
    /// Always-available encrypted game assets (fishing/cooking). These entries
    /// are marked Special in the manifest and never appear in the unlock wall.
    /// </summary>
    public Stream? GetAssetStream(string gifId)
    {
        var entry = _catalog.Find(gifId);
        if (entry is null || !_catalog.IsSpecial(entry)) return null;
        return Decrypt(entry);
    }

    public Stream? GetDecryptedStream(string gifId)
    {
        var entry = _catalog.Find(gifId);
        if (entry is null || _catalog.IsSpecial(entry)) return null;
        if (!_isUnlocked(gifId)) return null;
        return Decrypt(entry);
    }

    private Stream? Decrypt(GifEntry entry)
    {
        lock (_gate)
        {
            using var package = OpenPackage();
            package.Seek(entry.Offset, SeekOrigin.Begin);
            var cipher = new byte[entry.Length];
            package.ReadExactly(cipher);
            var plain = new byte[cipher.Length];
            try
            {
                using var aes = new AesGcm(_encryptionKey, 16);
                aes.Decrypt(
                    Convert.FromBase64String(entry.Nonce),
                    cipher,
                    Convert.FromBase64String(entry.Tag),
                    plain);
            }
            catch (CryptographicException error)
            {
                throw new GifIntegrityException("动态图条目完整性校验失败。", error);
            }

            return new MemoryStream(plain, writable: false);
        }
    }

    private void VerifyPackage()
    {
        var manifestBytes = ReadResource(GifCatalog.ManifestResource);
        _manifestHash = SHA256.HashData(manifestBytes);

        using var package = OpenPackage();
        var header = new byte[Magic.Length + 32];
        package.ReadExactly(header);
        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new GifIntegrityException("GIF 资源包格式无效。");
        }
        if (!header.AsSpan(Magic.Length, 32).SequenceEqual(_manifestHash))
        {
            throw new GifIntegrityException("GIF 资源清单与加密包不匹配。");
        }

        _packageLength = package.Length;
        if (_packageLength <= Magic.Length + 32 + 32)
        {
            throw new GifIntegrityException("GIF 资源包不完整。");
        }

        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, _signatureKey);
        hmac.AppendData(header);
        var remaining = _packageLength - header.Length - 32;
        var buffer = new byte[81920];
        while (remaining > 0)
        {
            var read = package.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read <= 0) throw new GifIntegrityException("GIF 资源包读取中断。");
            hmac.AppendData(buffer, 0, read);
            remaining -= read;
        }

        var expected = new byte[32];
        package.ReadExactly(expected);
        var calculated = hmac.GetHashAndReset();
        if (!CryptographicOperations.FixedTimeEquals(expected, calculated))
        {
            throw new GifIntegrityException("资源完整性异常：GIF 加密包签名校验失败。");
        }
        PackageSignature = Convert.ToHexString(calculated);
    }

    private Stream OpenPackage()
    {
        return _assembly.GetManifestResourceStream(GifCatalog.PackageResource)
            ?? throw new GifIntegrityException("缺少内置 GIF 加密资源。");
    }

    private byte[] ReadResource(string name)
    {
        using var stream = _assembly.GetManifestResourceStream(name)
            ?? throw new GifIntegrityException("缺少内置资源：" + name);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
