using System.IO;
using System.Reflection;

namespace WhalePet.Game;

/// <summary>
/// Decorative daily fortune lines from txt/LotMessage.txt. The file is embedded
/// at build time and never affects gameplay values.
/// </summary>
public static class LotMessages
{
    private const string ResourceName = "WhalePet.Resources.txt.LotMessage.txt";
    private static readonly string[] Lines = Load();

    public static IReadOnlyList<string> All => Lines;

    public static string Pick() =>
        Lines.Length == 0 ? "愿今日风顺，万事安宁。" : Lines[Random.Shared.Next(Lines.Length)];

    private static string[] Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null) return Array.Empty<string>();
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd().TrimStart('\uFEFF');
            return text.Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Take(2000)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
