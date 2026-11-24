using System.Security.Cryptography;
using System.Text;

namespace WhalePet.Game;

/// <summary>
/// Global resource-key material. The same passphrase is supplied to the
/// build-time GifPacker (never stored inside the tool itself). It is unrelated
/// to any device fingerprint, so the encrypted GIF package works on every PC.
/// </summary>
public static class ResourceSecrets
{
    private const string MasterPassphrase = "WhalePet|2026|DeepSea|GifPackage|v1|9f3C7aQ2";

    public static byte[] Derive(string purpose)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(MasterPassphrase));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(purpose));
    }

    public static string DeriveText(string purpose) => Convert.ToHexString(Derive(purpose));

    /// <summary>Per-device key used to protect local sensitive field checks.</summary>
    public static byte[] DeriveUserDataKey(string fingerprint)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(MasterPassphrase));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes("UserData|" + (fingerprint ?? "")));
    }
}
