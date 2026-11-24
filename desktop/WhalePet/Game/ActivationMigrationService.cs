using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WhalePet.Game;

/// <summary>
/// Offline one-time activation migration code. It only carries a hashed device
/// fingerprint, timestamp, random nonce and InstallId; no plain MAC is included.
/// </summary>
public static class ActivationMigrationService
{
    private static readonly string UsedPath =
        Path.Combine(PersistentStore.LocalDirectory, "activation-used.json");

    private sealed class CodePayload
    {
        public int V { get; set; } = 1;
        public string Mac { get; set; } = "";
        public DateTimeOffset At { get; set; }
        public string Nonce { get; set; } = "";
        public string Install { get; set; } = "";
        public string Sig { get; set; } = "";
    }

    public static string GenerateCode()
    {
        var lifecycle = GameRuntime.Store?.LoadLifecycle()
                        ?? throw new InvalidOperationException("本机激活信息不可用。");
        var payload = new CodePayload
        {
            V = 1,
            Mac = SHA256Hex(DeviceFingerprint.Value()),
            At = DateTimeOffset.Now,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)),
            Install = lifecycle.InstallId,
        };
        payload.Sig = Sign(payload);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        return Base64Url(json);
    }

    public static bool TryRedeem(string code, out string message)
    {
        message = "";
        try
        {
            var payload = JsonSerializer.Deserialize<CodePayload>(Base64UrlDecode(code.Trim()));
            if (payload is null || payload.V != 1) return Fail("迁移码格式无效。", out message);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(payload.Sig), Encoding.UTF8.GetBytes(Sign(payload))))
            {
                return Fail("迁移码签名校验失败。", out message);
            }
            var age = DateTimeOffset.Now - payload.At;
            if (age < TimeSpan.FromMinutes(-5) || age > TimeSpan.FromHours(24))
            {
                return Fail("迁移码已过期（有效期 24 小时）。", out message);
            }
            var used = LoadUsed();
            var nonceKey = SHA256Hex(payload.Nonce);
            if (used.Contains(nonceKey)) return Fail("该迁移码已经使用过，不能重复使用。", out message);

            var lifecycle = GameRuntime.Store?.LoadLifecycle();
            if (lifecycle is null) return Fail("本机 AppLifecycle 不可用。", out message);
            lifecycle.ActivatedAt = DateTimeOffset.Now;
            GameRuntime.Store!.SaveLifecycle(lifecycle);
            used.Add(nonceKey);
            SaveUsed(used);
            message = "迁移激活成功。本机已重新绑定激活时间，用户数据迁移包请另行导入。";
            return true;
        }
        catch (Exception error)
        {
            return Fail("迁移码无法解析：" + error.Message, out message);
        }
    }

    private static bool Fail(string text, out string message)
    {
        message = text;
        return false;
    }

    private static string Sign(CodePayload payload)
    {
        var key = ResourceSecrets.Derive("Activation.Migration");
        using var hmac = new HMACSHA256(key);
        var text = $"{payload.Mac}|{payload.At:O}|{payload.Nonce}|{payload.Install}|1";
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }

    private static HashSet<string> LoadUsed()
    {
        try
        {
            if (!File.Exists(UsedPath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(UsedPath));
            return new HashSet<string>(list ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveUsed(HashSet<string> used)
    {
        try
        {
            Directory.CreateDirectory(PersistentStore.LocalDirectory);
            File.WriteAllText(UsedPath, JsonSerializer.Serialize(used.ToList(),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // A failed used-nonce write is recovered by the 24-hour expiry.
        }
    }

    private static string SHA256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var value = text.Replace('-', '+').Replace('_', '/');
        switch (value.Length % 4)
        {
            case 2: value += "=="; break;
            case 3: value += "="; break;
        }
        return Convert.FromBase64String(value);
    }
}
