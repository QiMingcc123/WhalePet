using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace WhalePet.Game;

public static class DeviceFingerprint
{
    /// <summary>Stable, non-reversible local device fingerprint (no plain MAC stored).</summary>
    public static string Value()
    {
        var mac = "";
        try
        {
            mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .Select(item => item.GetPhysicalAddress().ToString())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }
        catch
        {
            // A missing network adapter still leaves a usable machine identity.
        }

        var raw = $"{Environment.MachineName}|{Environment.OSVersion.VersionString}|{mac}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
