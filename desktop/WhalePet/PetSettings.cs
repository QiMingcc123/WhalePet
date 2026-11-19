using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhalePet;

/// <summary>Persisted desktop-pet preferences. Image lists always contain at least one entry.</summary>
public sealed class PetSettings
{
    public bool ShowStatusBar { get; set; } = true;
    public bool GreetingEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool ShowSettingsButton { get; set; } = false;
    public double StatusBarScale { get; set; } = 1.0;
    public int IdleMinutes { get; set; } = 5;
    public int PlaySeconds { get; set; } = 30;
    public int TransitionMs { get; set; } = 420;
    public double Scale { get; set; } = 1.0;
    public double CharacterScale { get; set; } = 1.0;
    public double StatusFontSize { get; set; } = 10;
    public double SettingsFontSize { get; set; } = 11;
    public double MemoFontSize { get; set; } = 12;
    public string ThemeColor { get; set; } = "#4D6BFE";
    public string StatusTextColor { get; set; } = "#EAF6FF";
    public string SettingsTextColor { get; set; } = "#DFF2FF";
    public string MemoTextColor { get; set; } = "#3A2A18";
    public double WeatherFontSize { get; set; } = 10.5;
    public double SettingsButtonScale { get; set; } = 1.0;
    public double MemoButtonScale { get; set; } = 1.0;
    public double SpeechBubbleScale { get; set; } = 1.0;
    public double SpeechBubbleFontSize { get; set; } = 12;
    public double InputBadgeScale { get; set; } = 1.0;

    public bool WeatherEnabled { get; set; } = true;
    public string WeatherCity { get; set; } = "";
    public bool ShowDeviceInfo { get; set; } = true;
    public bool ShowRemovableDevices { get; set; } = true;
    public bool AutoHideAtEdge { get; set; } = true;
    public int EdgeCollapseDistance { get; set; } = 26;
    public double EdgeHoverSeconds { get; set; } = 3.0;
    public double MemoUiScale { get; set; } = 1.0;
    public bool ShowWaterReminder { get; set; } = true;
    public bool WaterPopupEnabled { get; set; } = true;
    public int WaterReminderMinutes { get; set; } = 30;

    public bool SedentaryReminderEnabled { get; set; } = false;
    public bool SedentaryPopupEnabled { get; set; } = true;
    public int SedentaryMinutes { get; set; } = 45;
    public bool AlarmsEnabled { get; set; } = false;
    public List<AlarmItem> Alarms { get; set; } = new();
    public bool ClipboardHistoryEnabled { get; set; } = true;
    public bool LockScreenEnabled { get; set; } = true;

    public bool BackgroundImageEnabled { get; set; } = false;
    public string BackgroundImagePath { get; set; } = "";
    public double BackgroundImageOpacity { get; set; } = 0.35;

    public bool ScreenshotEnabled { get; set; } = false;
    public bool ScreenshotCtrl { get; set; } = true;
    public bool ScreenshotAlt { get; set; } = true;
    public bool ScreenshotShift { get; set; } = false;
    public bool ScreenshotWin { get; set; } = false;
    public string ScreenshotKey { get; set; } = "A";

    public List<int> WorkDays { get; set; } = new() { 1, 2, 3, 4, 5 };
    public string WorkStart { get; set; } = "09:00";
    public string WorkEnd { get; set; } = "18:00";
    public bool ShowWorkBadge { get; set; } = true;
    public string WorkBadgeText { get; set; } = "搬砖中...";
    public double WorkBadgeScale { get; set; } = 1.0;
    public double WorkBadgeFontSize { get; set; } = 10;
    public bool ShowBeforeWorkReminder { get; set; } = true;
    public int BeforeWorkReminderMinutes { get; set; } = 5;
    public bool ShowEndWorkReminder { get; set; } = true;
    public string WorkReminderBeforeText { get; set; } = "快下班了，去倒个水吧~";
    public string WorkReminderEndText { get; set; } = "下班了，下班了，关机，打卡，50到手！";

    public int ChatIdleMinutes { get; set; } = 3;
    public List<string> ChatPhrases { get; set; } = new();

    public List<string> MainImages { get; set; } = new();
    public List<string> IdleImages { get; set; } = new();
    public List<string> PlayImages { get; set; } = new();
    public List<string> DisabledMainImages { get; set; } = new();
    public List<string> DisabledIdleImages { get; set; } = new();
    public List<string> DisabledPlayImages { get; set; } = new();
    public List<string> InteractionPhrases { get; set; } = new();

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    [JsonIgnore]
    public string ConfigPath => SettingsStore.ConfigPath;

    public static PetSettings CreateDefault() => new()
    {
        MainImages = new() { AssetBootstrapper.PathOf("main.png") },
        IdleImages = new() { AssetBootstrapper.PathOf("sleep.png") },
        PlayImages = new()
        {
            AssetBootstrapper.PathOf("play1.png"),
            AssetBootstrapper.PathOf("play2.png"),
            AssetBootstrapper.PathOf("play3.png"),
            AssetBootstrapper.PathOf("play4.png"),
        },
        InteractionPhrases = new()
        {
            "今天也要好好写代码呀。",
            "我在看着你哦。",
            "要不要先整理一下思路？",
            "摸摸头，继续加油。",
            "深海里也有星光。",
            "又写下一个漂亮的函数！",
            "累了就看看海吧。",
            "休息一下，鲸鱼娘在这里。",
        },
        ChatPhrases = new()
        {
            "还在忙吗？要不要休息一下。",
            "鲸鱼娘在这里，随时可以叫我。",
            "记得喝口水，活动一下肩膀。",
            "需要我陪你理一下思路吗？",
        },
    };

    public PetSettings Clone()
    {
        return new PetSettings
        {
            ShowStatusBar = ShowStatusBar,
            GreetingEnabled = GreetingEnabled,
            StartWithWindows = StartWithWindows,
            ShowSettingsButton = ShowSettingsButton,
            StatusBarScale = StatusBarScale,
            IdleMinutes = IdleMinutes,
            PlaySeconds = PlaySeconds,
            TransitionMs = TransitionMs,
            Scale = Scale,
            CharacterScale = CharacterScale,
            StatusFontSize = StatusFontSize,
            SettingsFontSize = SettingsFontSize,
            MemoFontSize = MemoFontSize,
            ThemeColor = ThemeColor,
            StatusTextColor = StatusTextColor,
            SettingsTextColor = SettingsTextColor,
            MemoTextColor = MemoTextColor,
            WeatherFontSize = WeatherFontSize,
            SettingsButtonScale = SettingsButtonScale,
            MemoButtonScale = MemoButtonScale,
            SpeechBubbleScale = SpeechBubbleScale,
            SpeechBubbleFontSize = SpeechBubbleFontSize,
            InputBadgeScale = InputBadgeScale,
            WeatherEnabled = WeatherEnabled,
            WeatherCity = WeatherCity,
            ShowDeviceInfo = ShowDeviceInfo,
            ShowRemovableDevices = ShowRemovableDevices,
            AutoHideAtEdge = AutoHideAtEdge,
            EdgeCollapseDistance = EdgeCollapseDistance,
            EdgeHoverSeconds = EdgeHoverSeconds,
            MemoUiScale = MemoUiScale,
            ShowWaterReminder = ShowWaterReminder,
            WaterPopupEnabled = WaterPopupEnabled,
            WaterReminderMinutes = WaterReminderMinutes,
            SedentaryReminderEnabled = SedentaryReminderEnabled,
            SedentaryPopupEnabled = SedentaryPopupEnabled,
            SedentaryMinutes = SedentaryMinutes,
            AlarmsEnabled = AlarmsEnabled,
            Alarms = Alarms.Select(alarm => alarm.Clone()).ToList(),
            ClipboardHistoryEnabled = ClipboardHistoryEnabled,
            LockScreenEnabled = LockScreenEnabled,
            BackgroundImageEnabled = BackgroundImageEnabled,
            BackgroundImagePath = BackgroundImagePath,
            BackgroundImageOpacity = BackgroundImageOpacity,
            ScreenshotEnabled = ScreenshotEnabled,
            ScreenshotCtrl = ScreenshotCtrl,
            ScreenshotAlt = ScreenshotAlt,
            ScreenshotShift = ScreenshotShift,
            ScreenshotWin = ScreenshotWin,
            ScreenshotKey = ScreenshotKey,
            WorkDays = WorkDays.ToList(),
            WorkStart = WorkStart,
            WorkEnd = WorkEnd,
            ShowWorkBadge = ShowWorkBadge,
            WorkBadgeText = WorkBadgeText,
            WorkBadgeScale = WorkBadgeScale,
            WorkBadgeFontSize = WorkBadgeFontSize,
            ShowBeforeWorkReminder = ShowBeforeWorkReminder,
            BeforeWorkReminderMinutes = BeforeWorkReminderMinutes,
            ShowEndWorkReminder = ShowEndWorkReminder,
            WorkReminderBeforeText = WorkReminderBeforeText,
            WorkReminderEndText = WorkReminderEndText,
            ChatIdleMinutes = ChatIdleMinutes,
            ChatPhrases = ChatPhrases.ToList(),
            MainImages = MainImages.ToList(),
            IdleImages = IdleImages.ToList(),
            PlayImages = PlayImages.ToList(),
            DisabledMainImages = DisabledMainImages.ToList(),
            DisabledIdleImages = DisabledIdleImages.ToList(),
            DisabledPlayImages = DisabledPlayImages.ToList(),
            InteractionPhrases = InteractionPhrases.ToList(),
            WindowLeft = WindowLeft,
            WindowTop = WindowTop,
        };
    }

    public void Normalize()
    {
        IdleMinutes = Math.Clamp(IdleMinutes, 1, 240);
        StatusBarScale = Math.Clamp(Math.Round(StatusBarScale, 2), 0.5, 2);
        PlaySeconds = Math.Clamp(PlaySeconds, 5, 600);
        TransitionMs = Math.Clamp(TransitionMs, 120, 2000);
        if (!double.IsFinite(Scale)) Scale = 1.0;
        Scale = Math.Clamp(Math.Round(Scale, 2), 0.1, 1.5);
        if (!double.IsFinite(CharacterScale)) CharacterScale = 1.0;
        CharacterScale = Math.Clamp(Math.Round(CharacterScale, 2), 0.1, 1.5);
        StatusFontSize = Math.Clamp(Math.Round(StatusFontSize, 1), 8, 32);
        SettingsFontSize = Math.Clamp(Math.Round(SettingsFontSize, 1), 8, 32);
        MemoFontSize = Math.Clamp(Math.Round(MemoFontSize, 1), 8, 32);
        ThemeColor = ColorOr(ThemeColor, "#4D6BFE");
        StatusTextColor = ColorOr(StatusTextColor, "#EAF6FF");
        SettingsTextColor = ColorOr(SettingsTextColor, "#DFF2FF");
        MemoTextColor = ColorOr(MemoTextColor, "#3A2A18");
        WeatherFontSize = Math.Clamp(Math.Round(WeatherFontSize, 1), 8, 32);
        SettingsButtonScale = Math.Clamp(Math.Round(SettingsButtonScale, 2), 0.5, 2);
        MemoButtonScale = Math.Clamp(Math.Round(MemoButtonScale, 2), 0.5, 2);
        SpeechBubbleScale = Math.Clamp(Math.Round(SpeechBubbleScale, 2), 0.5, 2);
        SpeechBubbleFontSize = Math.Clamp(Math.Round(SpeechBubbleFontSize, 1), 8, 32);
        InputBadgeScale = Math.Clamp(Math.Round(InputBadgeScale, 2), 0.5, 2);
        WeatherCity = NormalizeCity(WeatherCity);
        ChatIdleMinutes = Math.Clamp(ChatIdleMinutes, 0, 240);
        EdgeCollapseDistance = Math.Clamp(EdgeCollapseDistance, 0, 50);
        EdgeHoverSeconds = Math.Clamp(Math.Round(EdgeHoverSeconds, 1), 1, 10);
        MemoUiScale = Math.Clamp(Math.Round(MemoUiScale, 2), 0.5, 2);
        WaterReminderMinutes = Math.Clamp(WaterReminderMinutes, 5, 240);
        SedentaryMinutes = Math.Clamp(SedentaryMinutes, 0, 240);
        Alarms = NormalizeAlarms(Alarms);
        BackgroundImagePath = (BackgroundImagePath ?? "").Trim();
        if (!double.IsFinite(BackgroundImageOpacity)) BackgroundImageOpacity = 0.35;
        BackgroundImageOpacity = Math.Clamp(Math.Round(BackgroundImageOpacity, 2), 0, 1);
        ScreenshotKey = NormalizeScreenshotKey(ScreenshotKey);
        WorkBadgeScale = Math.Clamp(Math.Round(WorkBadgeScale, 2), 0.5, 2);
        WorkBadgeFontSize = Math.Clamp(Math.Round(WorkBadgeFontSize, 1), 8, 32);

        WorkDays = (WorkDays ?? new List<int>())
            .Where(day => day is >= 0 and <= 6)
            .Distinct()
            .OrderBy(day => day)
            .ToList();
        WorkStart = NormalizeTime(WorkStart, "09:00");
        WorkEnd = NormalizeTime(WorkEnd, "18:00");
        WorkBadgeText = NormalizeText(WorkBadgeText, "搬砖中...", 40);
        WorkReminderBeforeText = NormalizeText(WorkReminderBeforeText, "快下班了，去倒个水吧~", 100);
        BeforeWorkReminderMinutes = Math.Clamp(BeforeWorkReminderMinutes, 1, 120);
        WorkReminderEndText = NormalizeText(WorkReminderEndText, "下班了，下班了，关机，打卡，50到手！", 140);

        var defaults = CreateDefault();
        MainImages = CleanList(MainImages, defaults.MainImages);
        IdleImages = CleanList(IdleImages, defaults.IdleImages);
        PlayImages = CleanList(PlayImages, defaults.PlayImages);
        DisabledMainImages = CleanDisabled(DisabledMainImages, MainImages);
        DisabledIdleImages = CleanDisabled(DisabledIdleImages, IdleImages);
        DisabledPlayImages = CleanDisabled(DisabledPlayImages, PlayImages);
        InteractionPhrases = CleanList(InteractionPhrases, defaults.InteractionPhrases);
        ChatPhrases = CleanList(ChatPhrases, defaults.ChatPhrases);
    }

    public string ResolveImagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (Path.IsPathRooted(expanded)) return expanded;

        var fromBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
        if (File.Exists(fromBase)) return fromBase;

        // Migrate old relative defaults from the folder distribution to the
        // embedded assets extracted beside the configuration file.
        var name = Path.GetFileName(expanded.Replace('\\', '/'));
        var extracted = AssetBootstrapper.PathOf(name);
        return File.Exists(extracted) ? extracted : fromBase;
    }

    private static readonly char[] CitySeparators = { ',', '，', '、', ';', '；', '/', '|', '\n', '\r', '\t' };

    /// <summary>True when the text names exactly one place (no comma/slash separated list).</summary>
    public static bool IsSingleCity(string? value)
    {
        var text = (value ?? "").Trim();
        return text.IndexOfAny(CitySeparators) < 0;
    }

    public static string NormalizeCity(string? value)
    {
        var text = (value ?? "").Trim();
        var separator = text.IndexOfAny(CitySeparators);
        if (separator >= 0) text = text[..separator].Trim();
        text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return text.Length > 30 ? text[..30] : text;
    }

    public static readonly string[] ScreenshotKeys =
        Enumerable.Range('A', 26).Select(code => ((char)code).ToString())
            .Concat(Enumerable.Range(0, 10).Select(number => number.ToString()))
            .Concat(Enumerable.Range(1, 12).Select(number => "F" + number))
            .ToArray();

    public static string NormalizeScreenshotKey(string? value)
    {
        var text = (value ?? "").Trim().ToUpperInvariant();
        return ScreenshotKeys.Contains(text) ? text : "A";
    }

    private static List<AlarmItem> NormalizeAlarms(List<AlarmItem>? alarms)
    {
        var result = new List<AlarmItem>();
        foreach (var alarm in alarms ?? new List<AlarmItem>())
        {
            if (alarm is null) continue;
            var start = AlarmItem.NormalizeClock(alarm.Start) ?? "09:00";
            var end = string.IsNullOrWhiteSpace(alarm.End) ? "" : AlarmItem.NormalizeClock(alarm.End) ?? "";
            if (end.Length > 0 && string.CompareOrdinal(end, start) < 0) end = "";
            var text = (alarm.Text ?? "").Trim();
            if (text.Length == 0) continue;
            if (text.Length > 60) text = text[..60];
            result.Add(new AlarmItem
            {
                Enabled = alarm.Enabled,
                Date = alarm.Date,
                Start = start,
                End = end,
                Text = text,
                Popup = alarm.Popup,
            });
            if (result.Count >= 10) break;
        }
        return result;
    }

    private static string NormalizeTime(string? value, string fallback)
    {
        return TimeSpan.TryParseExact((value ?? "").Trim(), @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(@"hh\:mm")
            : fallback;
    }

    private static string NormalizeText(string? value, string fallback, int maxLength)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0) text = fallback;
        return text.Length > maxLength ? text[..maxLength] : text;
    }

    private static string ColorOr(string? value, string fallback)
    {
        var text = (value ?? "").Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$") ? text : fallback;
    }

    private static List<string> CleanList(List<string>? values, List<string> fallback)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values ?? new List<string>())
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0 || !seen.Add(text)) continue;
            result.Add(text);
            if (result.Count >= 24) break;
        }
        if (result.Count == 0) result.AddRange(fallback);
        return result;
    }

    private static List<string> CleanDisabled(List<string>? values, List<string> images)
    {
        var valid = new HashSet<string>(images, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values ?? new List<string>())
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0 || !valid.Contains(text) || !seen.Add(text)) continue;
            result.Add(text);
        }
        return result;
    }
}

public static class SettingsStore
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string DirectoryPath => PersistentStore.LocalDirectory;

    public static string ConfigPath => PersistentStore.PrimaryPath(FileName);

    /// <summary>Roaming copy survives replacing the executable or clearing LocalAppData.</summary>
    public static string BackupPath => PersistentStore.RoamingPath(FileName);

    public static PetSettings Load()
    {
        foreach (var json in PersistentStore.ReadCandidates(FileName))
        {
            try
            {
                var settings = JsonSerializer.Deserialize<PetSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    settings.Normalize();
                    return settings;
                }
            }
            catch
            {
                // Try the next copy.
            }
        }

        var fallback = PetSettings.CreateDefault();
        fallback.Normalize();
        return fallback;
    }

    public static void Save(PetSettings settings)
    {
        settings.Normalize();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        PersistentStore.Save(FileName, json);
    }
}
