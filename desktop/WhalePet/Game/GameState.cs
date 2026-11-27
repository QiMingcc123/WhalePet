namespace WhalePet.Game;

public enum AvatarKind
{
    DefaultStatic,
    Gif,
    CustomImage,
    Live2D,
}

public enum Fortune
{
    None,
    Up,
    Middle,
    Down,
}

public sealed class GameState
{
    public int Level { get; set; } = 1;
    public int Affection { get; set; }
    public double AffectionCarry { get; set; }
    public int Mood { get; set; } = 100;
    public int Satiety { get; set; } = 100;
    public int Food { get; set; }
    public int Ingredients { get; set; }
    public int Fish { get; set; }
    public long TotalCompanionSeconds { get; set; }
    public int ContinuousSignIn { get; set; }
    public int TotalSignInDays { get; set; }
    public int LastSignDateIndex { get; set; }

    public DateTimeOffset LastOnlineUtc { get; set; } = DateTimeOffset.Now;
    public int LastDateIndex { get; set; } = DateIndex(DateTime.Now);
    public int OnlineCarrySeconds { get; set; }
    public int TodayOnlineSeconds { get; set; }
    public int TodayInteractionCount { get; set; }
    public int TodayMoodBonusCount { get; set; }
    public int TodayEffectiveInteractions { get; set; }
    public int TodayFeedCount { get; set; }
    public DateTimeOffset LastInteractionUtc { get; set; } = DateTimeOffset.MinValue;
    public int LastMoodFullDate { get; set; }

    public string AvatarType { get; set; } = nameof(AvatarKind.DefaultStatic);
    public string? CurrentGifId { get; set; }
    public string? CurrentCustomImagePath { get; set; }
    public string? CurrentCustomImageData { get; set; }
    public string CurrentLive2DModelId { get; set; } = "seethrough_output";
    public DateTimeOffset? CurrentAvatarUpdatedAt { get; set; }

    public int DrawDateIndex { get; set; }
    public string DrawResult { get; set; } = nameof(Fortune.None);
    public DateTimeOffset? DrawAtUtc { get; set; }
    public int LotMessageDateIndex { get; set; }
    public string LotMessage { get; set; } = "";

    public int FishingDateIndex { get; set; }
    public int FishingUsed { get; set; }
    public int CookingDateIndex { get; set; }
    public int CookingUsed { get; set; }

    public int TaskDateIndex { get; set; }
    public string? TaskId { get; set; }
    public DateTimeOffset? TaskAcceptedUtc { get; set; }
    public int TaskProgress { get; set; }
    public int TaskAux { get; set; }
    public DateTimeOffset? TaskCompletedUtc { get; set; }
    public bool TaskRewardClaimed { get; set; }

    public int SignWeekStart { get; set; }
    public int WeeklySignMask { get; set; }
    public bool Milestone3Claimed { get; set; }
    public bool Milestone7Claimed { get; set; }

    public bool TimeRollbackDetected { get; set; }
    public string TimeRollbackNotice { get; set; } = "";
    public int MaxLevelAffection { get; set; }
    public bool DayOnline30Counted { get; set; }
    public long FishingSuccessToday { get; set; }
    public long CookingSuccessToday { get; set; }
    public bool Fish5CountedToday { get; set; }
    public bool Cook1CountedToday { get; set; }

    public AvatarKind Avatar => Enum.TryParse<AvatarKind>(AvatarType, true, out var value) ? value : AvatarKind.DefaultStatic;
    public Fortune FortuneToday => Enum.TryParse<Fortune>(DrawResult, true, out var value) ? value : Fortune.None;

    public static int DateIndex(DateTime date) => DateOnly.FromDateTime(date).DayNumber;

    public static int WeekStartIndex(DateTime date)
    {
        var today = DateOnly.FromDateTime(date);
        var offset = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-offset).DayNumber;
    }

    public GameState Clone()
    {
        return (GameState)MemberwiseClone();
    }
}

public sealed class GifUnlockRow
{
    public string GifId { get; set; } = "";
    public int SortIndex { get; set; }
    public string UnlockType { get; set; } = "";
    public string UnlockCondition { get; set; } = "";
    public bool IsUnlocked { get; set; }
    public DateTimeOffset? UnlockedAt { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? EnabledAt { get; set; }
    public bool EverEnabled { get; set; }
}

/// <summary>A selectable default main avatar. Image bytes live in Data as base64.</summary>
public sealed class DefaultAvatar
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "Custom";
    public string Data { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int SortIndex { get; set; }

    public bool IsBuiltIn => string.Equals(Kind, "Builtin", StringComparison.OrdinalIgnoreCase);
}

public sealed class MigrationLogRow
{
    public long Id { get; set; }
    public string Direction { get; set; } = "";
    public string PackageVersion { get; set; } = "";
    public DateTimeOffset? ExportedAt { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public string PackageHash { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class LifecycleInfo
{
    public string InstallId { get; set; } = "";
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastUninstallClearAt { get; set; }
    public bool KeepActivationOnUninstall { get; set; } = true;
    public int DataSchemaVersion { get; set; } = 1;
    public DateTimeOffset? LastBackupAt { get; set; }
    public string UserDataKeyCheck { get; set; } = "";
}
