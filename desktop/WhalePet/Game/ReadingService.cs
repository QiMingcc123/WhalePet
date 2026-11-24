using System.Globalization;
using System.IO;
using System.Text;

namespace WhalePet.Game;

/// <summary>Loads and paginates the reading text with BOM/encoding fallback.</summary>
public static class ReadingService
{
    private static readonly object Gate = new();
    private static List<string> _pages = new() { "" };
    private static string _rawText = "";
    private static string _lastError = "";
    private static bool _encodingSuspect;

    public static string RawText => _rawText;
    public static string LastError => _lastError;
    public static bool EncodingSuspect => _encodingSuspect;
    public static IReadOnlyList<string> Pages => _pages;
    public static int PageCount => Math.Max(1, _pages.Count);

    public static bool Reload(PetSettings settings)
    {
        lock (Gate)
        {
            _lastError = "";
            _encodingSuspect = false;
            _rawText = "";
            _pages = new List<string> { "" };
            var path = (settings.ReadingTextPath ?? "").Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                _lastError = "文本读取失败，请检查路径和编码";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch
            {
                _lastError = "文本读取失败，请检查路径和编码";
                return false;
            }
            if (bytes.Length == 0)
            {
                _lastError = "文本读取失败，请检查路径和编码";
                return false;
            }

            var text = Decode(bytes, settings.ReadingEncoding, out var error);
            if (text is null)
            {
                _lastError = error;
                return false;
            }
            _rawText = CleanText(text);
            _encodingSuspect = _rawText.Contains('\uFFFD');
            _pages = Paginate(_rawText, Math.Clamp(settings.ReadingCharsPerPage, 1, 1500));
            return true;
        }
    }

    /// <summary>Removes control characters/newlines/tabs and collapses whitespace runs to one space.</summary>
    private static string CleanText(string text)
    {
        var cleaned = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (cleaned.Length > 0) pendingSpace = true;
                continue;
            }
            if (pendingSpace)
            {
                cleaned.Append(' ');
                pendingSpace = false;
            }
            cleaned.Append(character);
        }
        return cleaned.ToString();
    }

    public static string PageAt(int page)
    {
        lock (Gate)
        {
            var index = Math.Clamp(page, 1, PageCount) - 1;
            return _pages[index];
        }
    }

    private static string? Decode(byte[] bytes, string choice, out string error)
    {
        error = "文本读取失败，请检查路径和编码";
        try
        {
            var encoding = choice switch
            {
                "UTF-8" => new UTF8Encoding(false, false),
                "GBK" => Encoding.GetEncoding(936),
                "GB2312" => Encoding.GetEncoding(936),
                "GB18030" => Encoding.GetEncoding(54936),
                _ => null,
            };
            if (encoding is not null) return encoding.GetString(bytes);

            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
                return new UTF32Encoding(false, true).GetString(bytes, 4, bytes.Length - 4);
            if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return new UTF32Encoding(true, true).GetString(bytes, 4, bytes.Length - 4);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return new UnicodeEncoding(false, true).GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return new UnicodeEncoding(true, true).GetString(bytes, 2, bytes.Length - 2);

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(936).GetString(bytes);
            }
        }
        catch (Exception exception)
        {
            error = "文本读取失败，请检查路径和编码";
            System.Diagnostics.Debug.WriteLine(exception);
            return null;
        }
    }

    private static List<string> Paginate(string text, int charsPerPage)
    {
        var pages = new List<string>();
        var page = new StringBuilder();
        var count = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            page.Append(element);
            count++;
            if (count >= charsPerPage)
            {
                pages.Add(page.ToString());
                page.Clear();
                count = 0;
            }
        }
        if (page.Length > 0 || pages.Count == 0) pages.Add(page.ToString());
        return pages;
    }
}
