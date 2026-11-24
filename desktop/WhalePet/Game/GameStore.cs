using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WhalePet.Game;

/// <summary>
/// Local SQLite persistence. The database lives beside the JSON settings and
/// keeps one .bak copy. Every mutation is committed in a transaction.
/// </summary>
public sealed class GameStore : IDisposable
{
    public const int SchemaVersion = 1;
    private const string FileName = "petgame.db";
    private readonly SqliteConnection _connection;
    private bool _disposed;

    static GameStore()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public GameStore(string? directory = null)
    {
        Directory = directory ?? PersistentStore.LocalDirectory;
        System.IO.Directory.CreateDirectory(Directory);
        DatabasePath = Path.Combine(Directory, FileName);
        BackupPath = DatabasePath + ".bak";
        _connection = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadWriteCreate;Cache=Private");
        _connection.Open();
        Execute("PRAGMA journal_mode=DELETE;");
        Execute("PRAGMA synchronous=FULL;");
        Execute("PRAGMA foreign_keys=ON;");
        CreateSchema();
    }

    public string Directory { get; }
    public string DatabasePath { get; }
    public string BackupPath { get; }

    public bool IsFirstRun { get; private set; }

    public void Initialize()
    {
        var count = Convert.ToInt64(Scalar("SELECT COUNT(*) FROM AppState"));
        IsFirstRun = count == 0;
        if (IsFirstRun)
        {
            SaveState(new GameState());
        }

        if (Convert.ToInt64(Scalar("SELECT COUNT(*) FROM AppLifecycle")) == 0)
        {
            var saved = ActivationStore.Load();
            InsertLifecycle(new LifecycleInfo
            {
                InstallId = saved?.InstallId ?? Guid.NewGuid().ToString("N"),
                ActivatedAt = saved?.ActivatedAt ?? DateTimeOffset.Now,
                LastUninstallClearAt = saved?.LastUninstallClearAt,
                KeepActivationOnUninstall = saved?.KeepActivationOnUninstall ?? true,
                DataSchemaVersion = SchemaVersion,
            });
        }

        EnsureSchemaVersion();
    }

    public GameState LoadState()
    {
        using var command = Command("SELECT * FROM AppState WHERE Id=1");
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return new GameState();

        return new GameState
        {
            Level = Int(reader, "Level", 1),
            Affection = Int(reader, "Affection"),
            AffectionCarry = Double(reader, "AffectionCarry"),
            Mood = Int(reader, "Mood", 100),
            Satiety = Int(reader, "Satiety", 100),
            Food = Int(reader, "Food"),
            Ingredients = Int(reader, "Ingredients"),
            Fish = Int(reader, "Fish"),
            TotalCompanionSeconds = Long(reader, "TotalCompanionSeconds"),
            ContinuousSignIn = Int(reader, "ContinuousSignIn"),
            TotalSignInDays = Int(reader, "TotalSignInDays"),
            LastSignDateIndex = Int(reader, "LastSignDateIndex"),
            LastOnlineUtc = Time(reader, "LastOnlineUtc") ?? DateTimeOffset.Now,
            LastDateIndex = Int(reader, "LastDateIndex", GameState.DateIndex(DateTime.Now)),
            OnlineCarrySeconds = Int(reader, "OnlineCarrySeconds"),
            TodayOnlineSeconds = Int(reader, "TodayOnlineSeconds"),
            TodayInteractionCount = Int(reader, "TodayInteractionCount"),
            TodayMoodBonusCount = Int(reader, "TodayMoodBonusCount"),
            TodayEffectiveInteractions = Int(reader, "TodayEffectiveInteractions"),
            TodayFeedCount = Int(reader, "TodayFeedCount"),
            LastInteractionUtc = Time(reader, "LastInteractionUtc") ?? DateTimeOffset.MinValue,
            LastMoodFullDate = Int(reader, "LastMoodFullDate"),
            AvatarType = Text(reader, "AvatarType") ?? nameof(AvatarKind.DefaultStatic),
            CurrentGifId = Text(reader, "CurrentGifId"),
            CurrentCustomImagePath = Text(reader, "CurrentCustomImagePath"),
            CurrentCustomImageData = Text(reader, "CurrentCustomImageData"),
            CurrentAvatarUpdatedAt = Time(reader, "CurrentAvatarUpdatedAt"),
            DrawDateIndex = Int(reader, "DrawDateIndex"),
            DrawResult = Text(reader, "DrawResult") ?? nameof(Fortune.None),
            DrawAtUtc = Time(reader, "DrawAtUtc"),
            LotMessageDateIndex = Int(reader, "LotMessageDateIndex"),
            LotMessage = Text(reader, "LotMessage") ?? "",
            FishingDateIndex = Int(reader, "FishingDateIndex"),
            FishingUsed = Int(reader, "FishingUsed"),
            CookingDateIndex = Int(reader, "CookingDateIndex"),
            CookingUsed = Int(reader, "CookingUsed"),
            TaskDateIndex = Int(reader, "TaskDateIndex"),
            TaskId = Text(reader, "TaskId"),
            TaskAcceptedUtc = Time(reader, "TaskAcceptedUtc"),
            TaskProgress = Int(reader, "TaskProgress"),
            TaskAux = Int(reader, "TaskAux"),
            TaskCompletedUtc = Time(reader, "TaskCompletedUtc"),
            TaskRewardClaimed = Bool(reader, "TaskRewardClaimed"),
            SignWeekStart = Int(reader, "SignWeekStart"),
            WeeklySignMask = Int(reader, "WeeklySignMask"),
            Milestone3Claimed = Bool(reader, "Milestone3Claimed"),
            Milestone7Claimed = Bool(reader, "Milestone7Claimed"),
            TimeRollbackDetected = Bool(reader, "TimeRollbackDetected"),
            TimeRollbackNotice = Text(reader, "TimeRollbackNotice") ?? "",
            MaxLevelAffection = Int(reader, "MaxLevelAffection"),
            DayOnline30Counted = Bool(reader, "DayOnline30Counted"),
            FishingSuccessToday = Long(reader, "FishingSuccessToday"),
            CookingSuccessToday = Long(reader, "CookingSuccessToday"),
            Fish5CountedToday = Bool(reader, "Fish5CountedToday"),
            Cook1CountedToday = Bool(reader, "Cook1CountedToday"),
        };
    }

    public void SaveState(GameState state)
    {
        using var transaction = _connection.BeginTransaction();
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Level"] = state.Level,
            ["Affection"] = state.Affection,
            ["AffectionCarry"] = state.AffectionCarry,
            ["Mood"] = state.Mood,
            ["Satiety"] = state.Satiety,
            ["Food"] = state.Food,
            ["Ingredients"] = state.Ingredients,
            ["Fish"] = state.Fish,
            ["TotalCompanionSeconds"] = state.TotalCompanionSeconds,
            ["ContinuousSignIn"] = state.ContinuousSignIn,
            ["TotalSignInDays"] = state.TotalSignInDays,
            ["LastSignDateIndex"] = state.LastSignDateIndex,
            ["LastOnlineUtc"] = Iso(state.LastOnlineUtc),
            ["LastDateIndex"] = state.LastDateIndex,
            ["OnlineCarrySeconds"] = state.OnlineCarrySeconds,
            ["TodayOnlineSeconds"] = state.TodayOnlineSeconds,
            ["TodayInteractionCount"] = state.TodayInteractionCount,
            ["TodayMoodBonusCount"] = state.TodayMoodBonusCount,
            ["TodayEffectiveInteractions"] = state.TodayEffectiveInteractions,
            ["TodayFeedCount"] = state.TodayFeedCount,
            ["LastInteractionUtc"] = state.LastInteractionUtc == DateTimeOffset.MinValue
                ? null
                : Iso(state.LastInteractionUtc),
            ["LastMoodFullDate"] = state.LastMoodFullDate,
            ["AvatarType"] = state.AvatarType,
            ["CurrentGifId"] = state.CurrentGifId,
            ["CurrentCustomImagePath"] = state.CurrentCustomImagePath,
            ["CurrentCustomImageData"] = state.CurrentCustomImageData,
            ["CurrentAvatarUpdatedAt"] = state.CurrentAvatarUpdatedAt is null ? null : Iso(state.CurrentAvatarUpdatedAt.Value),
            ["DrawDateIndex"] = state.DrawDateIndex,
            ["DrawResult"] = state.DrawResult,
            ["DrawAtUtc"] = state.DrawAtUtc is null ? null : Iso(state.DrawAtUtc.Value),
            ["LotMessageDateIndex"] = state.LotMessageDateIndex,
            ["LotMessage"] = state.LotMessage,
            ["FishingDateIndex"] = state.FishingDateIndex,
            ["FishingUsed"] = state.FishingUsed,
            ["CookingDateIndex"] = state.CookingDateIndex,
            ["CookingUsed"] = state.CookingUsed,
            ["TaskDateIndex"] = state.TaskDateIndex,
            ["TaskId"] = state.TaskId,
            ["TaskAcceptedUtc"] = state.TaskAcceptedUtc is null ? null : Iso(state.TaskAcceptedUtc.Value),
            ["TaskProgress"] = state.TaskProgress,
            ["TaskAux"] = state.TaskAux,
            ["TaskCompletedUtc"] = state.TaskCompletedUtc is null ? null : Iso(state.TaskCompletedUtc.Value),
            ["TaskRewardClaimed"] = state.TaskRewardClaimed ? 1 : 0,
            ["SignWeekStart"] = state.SignWeekStart,
            ["WeeklySignMask"] = state.WeeklySignMask,
            ["Milestone3Claimed"] = state.Milestone3Claimed ? 1 : 0,
            ["Milestone7Claimed"] = state.Milestone7Claimed ? 1 : 0,
            ["TimeRollbackDetected"] = state.TimeRollbackDetected ? 1 : 0,
            ["TimeRollbackNotice"] = state.TimeRollbackNotice,
            ["MaxLevelAffection"] = state.MaxLevelAffection,
            ["DayOnline30Counted"] = state.DayOnline30Counted ? 1 : 0,
            ["FishingSuccessToday"] = state.FishingSuccessToday,
            ["CookingSuccessToday"] = state.CookingSuccessToday,
            ["Fish5CountedToday"] = state.Fish5CountedToday ? 1 : 0,
            ["Cook1CountedToday"] = state.Cook1CountedToday ? 1 : 0,
            ["DataSchemaVersion"] = SchemaVersion,
        };

        var columns = string.Join(", ", fields.Keys);
        var parameters = string.Join(", ", fields.Keys.Select(key => "@" + key));
        var updates = string.Join(", ", fields.Keys.Where(key => key != "Id").Select(key => $"{key}=@{key}"));
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO AppState(Id, {columns}) VALUES(1, {parameters}) ON CONFLICT(Id) DO UPDATE SET {updates}";
        foreach (var pair in fields) Add(command, pair.Key, pair.Value);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public long GetCounter(string key)
    {
        var value = Scalar("SELECT Value FROM Counters WHERE Key=@key", ("@key", key));
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    public void SetCounter(string key, long value)
    {
        Execute("INSERT INTO Counters(Key,Value) VALUES(@key,@value) ON CONFLICT(Key) DO UPDATE SET Value=@value",
            ("@key", key), ("@value", value));
    }

    public void AddCounter(string key, long delta)
    {
        Execute("INSERT INTO Counters(Key,Value) VALUES(@key,@delta) ON CONFLICT(Key) DO UPDATE SET Value=Value+@delta",
            ("@key", key), ("@delta", delta));
    }

    public Dictionary<string, long> LoadCounters()
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var command = Command("SELECT Key,Value FROM Counters");
        using var reader = command.ExecuteReader();
        while (reader.Read()) result[reader.GetString(0)] = reader.GetInt64(1);
        return result;
    }

    public List<GifUnlockRow> LoadGifUnlocks()
    {
        var list = new List<GifUnlockRow>();
        using var command = Command("SELECT * FROM GifUnlock ORDER BY SortIndex");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new GifUnlockRow
            {
                GifId = Text(reader, "GifId") ?? "",
                SortIndex = Int(reader, "SortIndex"),
                UnlockType = Text(reader, "UnlockType") ?? "",
                UnlockCondition = Text(reader, "UnlockCondition") ?? "",
                IsUnlocked = Bool(reader, "IsUnlocked"),
                UnlockedAt = Time(reader, "UnlockedAtUtc"),
                IsEnabled = Bool(reader, "IsEnabled"),
                EnabledAt = Time(reader, "EnabledAtUtc"),
                EverEnabled = Bool(reader, "EverEnabled"),
            });
        }
        return list;
    }

    public List<DefaultAvatar> LoadDefaultAvatars()
    {
        var list = new List<DefaultAvatar>();
        using var command = Command("SELECT * FROM DefaultAvatar ORDER BY SortIndex");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new DefaultAvatar
            {
                Id = Text(reader, "Id") ?? "",
                Kind = Text(reader, "Kind") ?? "Custom",
                Data = Text(reader, "Data") ?? "",
                Enabled = Bool(reader, "Enabled", true),
                SortIndex = Int(reader, "SortIndex"),
            });
        }
        return list;
    }

    public void SaveDefaultAvatars(IEnumerable<DefaultAvatar> avatars)
    {
        using var transaction = _connection.BeginTransaction();
        using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM DefaultAvatar;";
            command.ExecuteNonQuery();
        }
        var index = 0;
        foreach (var avatar in avatars)
        {
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO DefaultAvatar(Id,Kind,Data,Enabled,SortIndex) VALUES(@id,@kind,@data,@enabled,@sort)";
            Add(command, "id", avatar.Id);
            Add(command, "kind", avatar.Kind);
            Add(command, "data", avatar.Data);
            Add(command, "enabled", avatar.Enabled ? 1 : 0);
            Add(command, "sort", index++);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void UnlockGif(string gifId, string unlockType, string unlockCondition, int sortIndex)
    {
        var now = Iso(DateTimeOffset.Now);
        Execute("""
                INSERT INTO GifUnlock(GifId,SortIndex,UnlockType,UnlockCondition,IsUnlocked,UnlockedAtUtc,IsEnabled,EnabledAtUtc,EverEnabled)
                VALUES(@id,@sort,@type,@condition,1,@now,0,NULL,0)
                ON CONFLICT(GifId) DO UPDATE SET
                    IsUnlocked=1,
                    UnlockedAtUtc=COALESCE(GifUnlock.UnlockedAtUtc,@now),
                    UnlockType=@type,
                    UnlockCondition=@condition
                """,
            ("@id", gifId), ("@sort", sortIndex), ("@type", unlockType),
            ("@condition", unlockCondition), ("@now", now));
    }

    public void SetGifEnabled(string gifId, bool enabled)
    {
        Execute(enabled
                ? "UPDATE GifUnlock SET IsEnabled=1, EnabledAtUtc=@now WHERE GifId=@id"
                : "UPDATE GifUnlock SET IsEnabled=0, EnabledAtUtc=NULL WHERE GifId=@id",
            ("@id", gifId), ("@now", Iso(DateTimeOffset.Now)));
    }

    public void MarkGifEverEnabled(string gifId)
    {
        Execute("UPDATE GifUnlock SET EverEnabled=1 WHERE GifId=@id", ("@id", gifId));
    }

    public HashSet<string> LoadAchievements()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = Command("SELECT AchievementId FROM Achievements");
        using var reader = command.ExecuteReader();
        while (reader.Read()) set.Add(reader.GetString(0));
        return set;
    }

    public Dictionary<string, DateTimeOffset> LoadAchievementTimes()
    {
        var result = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        using var command = Command("SELECT AchievementId,UnlockedAtUtc FROM Achievements");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(reader.GetString(1), out var at)) result[reader.GetString(0)] = at;
        }
        return result;
    }

    public bool UnlockAchievement(string id)
    {
        using var command = Command(
            "INSERT INTO Achievements(AchievementId,UnlockedAtUtc) VALUES(@id,@now) ON CONFLICT(AchievementId) DO NOTHING",
            ("@id", id), ("@now", Iso(DateTimeOffset.Now)));
        return command.ExecuteNonQuery() > 0;
    }

    public void AddSignIn(int dateIndex)
    {
        Execute("INSERT INTO SignInLog(DateIndex,SignedAtUtc) VALUES(@date,@now) ON CONFLICT(DateIndex) DO NOTHING",
            ("@date", dateIndex), ("@now", Iso(DateTimeOffset.Now)));
    }

    public HashSet<int> LoadSignInDates()
    {
        var set = new HashSet<int>();
        using var command = Command("SELECT DateIndex FROM SignInLog ORDER BY DateIndex");
        using var reader = command.ExecuteReader();
        while (reader.Read()) set.Add(reader.GetInt32(0));
        return set;
    }

    // ------------------------------------------------------------ backpack

    public List<string> LoadInventory()
    {
        var list = new List<string>();
        using var command = Command("SELECT ItemId FROM Inventory ORDER BY Slot");
        using var reader = command.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    public void SaveInventory(IEnumerable<string> items)
    {
        using var transaction = _connection.BeginTransaction();
        using (var delete = _connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Inventory;";
            delete.ExecuteNonQuery();
        }
        var slot = 0;
        foreach (var item in items)
        {
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Inventory(Slot,ItemId) VALUES(@slot,@item)";
            Add(insert, "slot", slot++);
            Add(insert, "item", item);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public List<string> LoadStaging()
    {
        var list = new List<string>();
        using var command = Command("SELECT ItemId FROM Staging ORDER BY Ordinal");
        using var reader = command.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    public void SaveStaging(IEnumerable<string> items, int dayIndex)
    {
        using var transaction = _connection.BeginTransaction();
        using (var delete = _connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Staging;";
            delete.ExecuteNonQuery();
        }
        var ordinal = 0;
        foreach (var item in items)
        {
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Staging(Ordinal,ItemId) VALUES(@ordinal,@item)";
            Add(insert, "ordinal", ordinal++);
            Add(insert, "item", item);
            insert.ExecuteNonQuery();
        }
        using (var meta = _connection.CreateCommand())
        {
            meta.Transaction = transaction;
            meta.CommandText =
                "INSERT INTO InventoryMeta(Key,Value) VALUES('StagingDay',@day) " +
                "ON CONFLICT(Key) DO UPDATE SET Value=@day";
            Add(meta, "day", dayIndex.ToString(CultureInfo.InvariantCulture));
            meta.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public int LoadStagingDay()
    {
        var value = Scalar("SELECT Value FROM InventoryMeta WHERE Key='StagingDay'");
        return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public string? GetMeta(string key)
    {
        var value = Scalar("SELECT Value FROM InventoryMeta WHERE Key=@key", ("@key", key));
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public void SetMeta(string key, string value)
    {
        Execute("INSERT INTO InventoryMeta(Key,Value) VALUES(@key,@value) " +
                "ON CONFLICT(Key) DO UPDATE SET Value=@value", ("@key", key), ("@value", value));
    }

    public bool LoadInventoryMigrated()
    {
        var value = Scalar("SELECT Value FROM InventoryMeta WHERE Key='LegacyMigrated'");
        return value is not null and not DBNull && Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
    }

    public void MarkInventoryMigrated()
    {
        Execute("INSERT INTO InventoryMeta(Key,Value) VALUES('LegacyMigrated','1') " +
                "ON CONFLICT(Key) DO UPDATE SET Value='1'");
    }

    public LifecycleInfo LoadLifecycle()
    {
        using var command = Command("SELECT * FROM AppLifecycle WHERE Id=1");
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new LifecycleInfo { InstallId = Guid.NewGuid().ToString("N"), ActivatedAt = DateTimeOffset.Now };
        }
        return new LifecycleInfo
        {
            InstallId = Text(reader, "InstallId") ?? Guid.NewGuid().ToString("N"),
            ActivatedAt = Time(reader, "ActivatedAtUtc") ?? DateTimeOffset.Now,
            LastUninstallClearAt = Time(reader, "LastUninstallClearAtUtc"),
            KeepActivationOnUninstall = Bool(reader, "KeepActivationOnUninstall", true),
            DataSchemaVersion = Int(reader, "DataSchemaVersion", SchemaVersion),
            LastBackupAt = Time(reader, "LastBackupAtUtc"),
            UserDataKeyCheck = Text(reader, "UserDataKeyCheck") ?? "",
        };
    }

    public void SaveLifecycle(LifecycleInfo info)
    {
        Execute("""
                UPDATE AppLifecycle SET InstallId=@install,ActivatedAtUtc=@activated,LastUninstallClearAtUtc=@cleared,
                    KeepActivationOnUninstall=@keep,DataSchemaVersion=@version,LastBackupAtUtc=@backup,UserDataKeyCheck=@check
                WHERE Id=1
                """,
            ("@install", info.InstallId), ("@activated", Iso(info.ActivatedAt)),
            ("@cleared", info.LastUninstallClearAt is null ? null : Iso(info.LastUninstallClearAt.Value)),
            ("@keep", info.KeepActivationOnUninstall ? 1 : 0), ("@version", info.DataSchemaVersion),
            ("@backup", info.LastBackupAt is null ? null : Iso(info.LastBackupAt.Value)),
            ("@check", info.UserDataKeyCheck));
    }

    public void SavePackageMeta(string version, string signature, int entryCount)
    {
        Execute("""
                INSERT INTO GifPackageMeta(PackageVersion,HmacSignature,CreatedAtUtc,EntryCount)
                VALUES(@version,@signature,@now,@count)
                ON CONFLICT(PackageVersion) DO UPDATE SET HmacSignature=@signature,EntryCount=@count
                """,
            ("@version", version), ("@signature", signature), ("@now", Iso(DateTimeOffset.Now)), ("@count", entryCount));
    }

    public void AddMigrationLog(string direction, string packageVersion, DateTimeOffset? exportedAt,
        DateTimeOffset? importedAt, string packageHash, string status, string message)
    {
        Execute("""
                INSERT INTO MigrationLog(Direction,PackageVersion,ExportedAtUtc,ImportedAtUtc,PackageHash,Status,Message)
                VALUES(@direction,@version,@exported,@imported,@hash,@status,@message)
                """,
            ("@direction", direction), ("@version", packageVersion),
            ("@exported", exportedAt is null ? null : Iso(exportedAt.Value)),
            ("@imported", importedAt is null ? null : Iso(importedAt.Value)),
            ("@hash", packageHash), ("@status", status), ("@message", message));
    }

    public List<MigrationLogRow> LoadMigrationLogs()
    {
        var list = new List<MigrationLogRow>();
        using var command = Command("SELECT * FROM MigrationLog ORDER BY Id DESC LIMIT 50");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new MigrationLogRow
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Direction = Text(reader, "Direction") ?? "",
                PackageVersion = Text(reader, "PackageVersion") ?? "",
                ExportedAt = Time(reader, "ExportedAtUtc"),
                ImportedAt = Time(reader, "ImportedAtUtc"),
                PackageHash = Text(reader, "PackageHash") ?? "",
                Status = Text(reader, "Status") ?? "",
                Message = Text(reader, "Message") ?? "",
            });
        }
        return list;
    }

    public bool IsPackageUsed(string hash) =>
        Convert.ToInt64(Scalar(
            "SELECT COUNT(*) FROM MigrationLog WHERE PackageHash=@hash AND Status='Success'",
            ("@hash", hash))) > 0;

    public void Backup()
    {
        try
        {
            _connection.Close();
            File.Copy(DatabasePath, BackupPath, true);
        }
        finally
        {
            _connection.Open();
        }
        Execute("UPDATE AppLifecycle SET LastBackupAtUtc=@now WHERE Id=1", ("@now", Iso(DateTimeOffset.Now)));
    }

    public void ReplaceAll(AppStateSnapshot snapshot)
    {
        using var transaction = _connection.BeginTransaction();
        using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                "DELETE FROM GifUnlock; DELETE FROM Achievements; DELETE FROM Counters; DELETE FROM SignInLog; " +
                "DELETE FROM Inventory; DELETE FROM Staging; DELETE FROM InventoryMeta; DELETE FROM DefaultAvatar;";
            command.ExecuteNonQuery();
        }
        transaction.Commit();

        SaveState(snapshot.State);
        SaveInventory(snapshot.Inventory);
        SaveStaging(snapshot.Staging, snapshot.StagingDayIndex);
        SaveDefaultAvatars(snapshot.DefaultAvatars);
        MarkInventoryMigrated();
        foreach (var gif in snapshot.Gifs)
        {
            Execute("""
                    INSERT INTO GifUnlock(GifId,SortIndex,UnlockType,UnlockCondition,IsUnlocked,UnlockedAtUtc,IsEnabled,EnabledAtUtc,EverEnabled)
                    VALUES(@id,@sort,@type,@condition,1,@at,@enabled,@enabledAt,@ever)
                    """,
                ("@id", gif.GifId), ("@sort", gif.SortIndex), ("@type", gif.UnlockType),
                ("@condition", gif.UnlockCondition), ("@at", gif.UnlockedAt is null ? null : Iso(gif.UnlockedAt.Value)),
                ("@enabled", gif.IsEnabled ? 1 : 0),
                ("@enabledAt", gif.EnabledAt is null ? null : Iso(gif.EnabledAt.Value)),
                ("@ever", gif.EverEnabled ? 1 : 0));
        }
        foreach (var achievement in snapshot.Achievements)
        {
            Execute("INSERT INTO Achievements(AchievementId,UnlockedAtUtc) VALUES(@id,@at)",
                ("@id", achievement.Key), ("@at", Iso(achievement.Value)));
        }
        foreach (var counter in snapshot.Counters)
        {
            Execute("INSERT INTO Counters(Key,Value) VALUES(@key,@value)", ("@key", counter.Key), ("@value", counter.Value));
        }
        foreach (var date in snapshot.SignInDates)
        {
            Execute("INSERT INTO SignInLog(DateIndex,SignedAtUtc) VALUES(@date,@now)",
                ("@date", date), ("@now", Iso(DateTimeOffset.Now)));
        }
    }

    public void ClearAllData(bool thorough)
    {
        Execute("DELETE FROM AppState; DELETE FROM GifUnlock; DELETE FROM Achievements; DELETE FROM Counters; DELETE FROM SignInLog; DELETE FROM MigrationLog; DELETE FROM Inventory; DELETE FROM Staging; DELETE FROM InventoryMeta; DELETE FROM DefaultAvatar;");
        try { File.Delete(Path.Combine(Directory, "custom-avatar.png")); } catch { }
        try { File.Delete(Path.Combine(Directory, "activation-used.json")); } catch { }
        if (thorough)
        {
            Execute("DELETE FROM AppLifecycle; DELETE FROM GifPackageMeta;");
            ActivationStore.Delete();
        }
        else
        {
            var lifecycle = LoadLifecycle();
            lifecycle.LastUninstallClearAt = DateTimeOffset.Now;
            lifecycle.KeepActivationOnUninstall = true;
            SaveLifecycle(lifecycle);
            ActivationStore.Save(lifecycle);
        }
    }

    private void CreateSchema()
    {
        Execute("""
                CREATE TABLE IF NOT EXISTS AppState(
                    Id INTEGER PRIMARY KEY CHECK(Id=1),
                    Level INTEGER NOT NULL DEFAULT 1,
                    Affection INTEGER NOT NULL DEFAULT 0,
                    AffectionCarry REAL NOT NULL DEFAULT 0,
                    Mood INTEGER NOT NULL DEFAULT 100,
                    Satiety INTEGER NOT NULL DEFAULT 100,
                    Food INTEGER NOT NULL DEFAULT 0,
                    Ingredients INTEGER NOT NULL DEFAULT 0,
                    Fish INTEGER NOT NULL DEFAULT 0,
                    TotalCompanionSeconds INTEGER NOT NULL DEFAULT 0,
                    ContinuousSignIn INTEGER NOT NULL DEFAULT 0,
                    TotalSignInDays INTEGER NOT NULL DEFAULT 0,
                    LastSignDateIndex INTEGER NOT NULL DEFAULT 0,
                    LastOnlineUtc TEXT,
                    LastDateIndex INTEGER NOT NULL DEFAULT 0,
                    OnlineCarrySeconds INTEGER NOT NULL DEFAULT 0,
                    TodayOnlineSeconds INTEGER NOT NULL DEFAULT 0,
                    TodayInteractionCount INTEGER NOT NULL DEFAULT 0,
                    TodayMoodBonusCount INTEGER NOT NULL DEFAULT 0,
                    TodayEffectiveInteractions INTEGER NOT NULL DEFAULT 0,
                    TodayFeedCount INTEGER NOT NULL DEFAULT 0,
                    LastInteractionUtc TEXT,
                    LastMoodFullDate INTEGER NOT NULL DEFAULT 0,
                    AvatarType TEXT NOT NULL DEFAULT 'DefaultStatic',
                    CurrentGifId TEXT,
                    CurrentCustomImagePath TEXT,
                    CurrentCustomImageData TEXT,
                    CurrentAvatarUpdatedAt TEXT,
                    DrawDateIndex INTEGER NOT NULL DEFAULT 0,
                    DrawResult TEXT NOT NULL DEFAULT 'None',
                    DrawAtUtc TEXT,
                    LotMessageDateIndex INTEGER NOT NULL DEFAULT 0,
                    LotMessage TEXT,
                    FishingDateIndex INTEGER NOT NULL DEFAULT 0,
                    FishingUsed INTEGER NOT NULL DEFAULT 0,
                    CookingDateIndex INTEGER NOT NULL DEFAULT 0,
                    CookingUsed INTEGER NOT NULL DEFAULT 0,
                    TaskDateIndex INTEGER NOT NULL DEFAULT 0,
                    TaskId TEXT,
                    TaskAcceptedUtc TEXT,
                    TaskProgress INTEGER NOT NULL DEFAULT 0,
                    TaskAux INTEGER NOT NULL DEFAULT 0,
                    TaskCompletedUtc TEXT,
                    TaskRewardClaimed INTEGER NOT NULL DEFAULT 0,
                    SignWeekStart INTEGER NOT NULL DEFAULT 0,
                    WeeklySignMask INTEGER NOT NULL DEFAULT 0,
                    Milestone3Claimed INTEGER NOT NULL DEFAULT 0,
                    Milestone7Claimed INTEGER NOT NULL DEFAULT 0,
                    TimeRollbackDetected INTEGER NOT NULL DEFAULT 0,
                    TimeRollbackNotice TEXT,
                    MaxLevelAffection INTEGER NOT NULL DEFAULT 0,
                    DayOnline30Counted INTEGER NOT NULL DEFAULT 0,
                    FishingSuccessToday INTEGER NOT NULL DEFAULT 0,
                    CookingSuccessToday INTEGER NOT NULL DEFAULT 0,
                    Fish5CountedToday INTEGER NOT NULL DEFAULT 0,
                    Cook1CountedToday INTEGER NOT NULL DEFAULT 0,
                    DataSchemaVersion INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS GifUnlock(
                    GifId TEXT PRIMARY KEY,
                    SortIndex INTEGER NOT NULL DEFAULT 0,
                    UnlockType TEXT NOT NULL,
                    UnlockCondition TEXT NOT NULL,
                    IsUnlocked INTEGER NOT NULL DEFAULT 0,
                    UnlockedAtUtc TEXT,
                    IsEnabled INTEGER NOT NULL DEFAULT 0,
                    EnabledAtUtc TEXT,
                    EverEnabled INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS DefaultAvatar(
                    Id TEXT PRIMARY KEY,
                    Kind TEXT NOT NULL,
                    Data TEXT NOT NULL,
                    Enabled INTEGER NOT NULL DEFAULT 1,
                    SortIndex INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS GifPackageMeta(
                    PackageVersion TEXT PRIMARY KEY,
                    HmacSignature TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    EntryCount INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Achievements(
                    AchievementId TEXT PRIMARY KEY,
                    UnlockedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Counters(
                    Key TEXT PRIMARY KEY,
                    Value INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS SignInLog(
                    DateIndex INTEGER PRIMARY KEY,
                    SignedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Inventory(
                    Slot INTEGER PRIMARY KEY,
                    ItemId TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Staging(
                    Ordinal INTEGER PRIMARY KEY,
                    ItemId TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS InventoryMeta(
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS MigrationLog(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Direction TEXT NOT NULL,
                    PackageVersion TEXT NOT NULL,
                    ExportedAtUtc TEXT,
                    ImportedAtUtc TEXT,
                    PackageHash TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    Message TEXT
                );

                CREATE TABLE IF NOT EXISTS AppLifecycle(
                    Id INTEGER PRIMARY KEY CHECK(Id=1),
                    InstallId TEXT NOT NULL,
                    ActivatedAtUtc TEXT NOT NULL,
                    LastUninstallClearAtUtc TEXT,
                    KeepActivationOnUninstall INTEGER NOT NULL DEFAULT 1,
                    DataSchemaVersion INTEGER NOT NULL DEFAULT 1,
                    LastBackupAtUtc TEXT,
                    UserDataKeyCheck TEXT
                );
                """);
        EnsureAppStateColumns();
    }

    /// <summary>Adds columns introduced after the first release to existing databases.</summary>
    private void EnsureAppStateColumns()
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = Command("PRAGMA table_info(AppState)"))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read()) columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("LotMessageDateIndex"))
        {
            Execute("ALTER TABLE AppState ADD COLUMN LotMessageDateIndex INTEGER NOT NULL DEFAULT 0");
        }
        if (!columns.Contains("LotMessage"))
        {
            Execute("ALTER TABLE AppState ADD COLUMN LotMessage TEXT");
        }
    }

    private void EnsureSchemaVersion()
    {
        var version = Convert.ToInt32(Scalar("SELECT DataSchemaVersion FROM AppLifecycle WHERE Id=1") ?? 1);
        if (version < SchemaVersion)
        {
            Execute("UPDATE AppLifecycle SET DataSchemaVersion=@version WHERE Id=1", ("@version", SchemaVersion));
        }
    }

    private void InsertLifecycle(LifecycleInfo info)
    {
        Execute("""
                INSERT INTO AppLifecycle(Id,InstallId,ActivatedAtUtc,LastUninstallClearAtUtc,KeepActivationOnUninstall,DataSchemaVersion,LastBackupAtUtc,UserDataKeyCheck)
                VALUES(1,@install,@activated,@cleared,@keep,@version,@backup,@check)
                """,
            ("@install", info.InstallId), ("@activated", Iso(info.ActivatedAt)),
            ("@cleared", info.LastUninstallClearAt is null ? null : Iso(info.LastUninstallClearAt.Value)),
            ("@keep", info.KeepActivationOnUninstall ? 1 : 0), ("@version", info.DataSchemaVersion),
            ("@backup", info.LastBackupAt is null ? null : Iso(info.LastBackupAt.Value)),
            ("@check", info.UserDataKeyCheck));
    }

    private SqliteCommand Command(string sql, params (string Name, object? Value)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) Add(command, name.TrimStart('@'), value);
        return command;
    }

    private void Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(sql, parameters);
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(sql, parameters);
        return command.ExecuteScalar();
    }

    private static void Add(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue("@" + name, value ?? DBNull.Value);
    }

    private static string Iso(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static string? Text(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int Int(SqliteDataReader reader, string column, int fallback = 0)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? fallback : reader.GetInt32(ordinal);
    }

    private static long Long(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt64(ordinal);
    }

    private static double Double(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetDouble(ordinal);
    }

    private static bool Bool(SqliteDataReader reader, string column, bool fallback = false)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? fallback : reader.GetInt64(ordinal) != 0;
    }

    private static DateTimeOffset? Time(SqliteDataReader reader, string column)
    {
        var text = Text(reader, column);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            Backup();
        }
        catch
        {
            // The data is already committed; a failed backup must not crash exit.
        }
        _connection.Dispose();
    }
}

public sealed class AppStateSnapshot
{
    public GameState State { get; set; } = new();
    public List<GifUnlockRow> Gifs { get; set; } = new();
    public Dictionary<string, DateTimeOffset> Achievements { get; set; } = new();
    public Dictionary<string, long> Counters { get; set; } = new();
    public List<int> SignInDates { get; set; } = new();
    public List<string> Inventory { get; set; } = new();
    public List<string> Staging { get; set; } = new();
    public int StagingDayIndex { get; set; }
    public List<DefaultAvatar> DefaultAvatars { get; set; } = new();
}

/// <summary>Activation/InstallId survives a default data clear.</summary>
public sealed class ActivationRecord
{
    public string InstallId { get; set; } = "";
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset? LastUninstallClearAt { get; set; }
    public bool KeepActivationOnUninstall { get; set; } = true;
}

public static class ActivationStore
{
    private static string PathName => Path.Combine(PersistentStore.LocalDirectory, "activation.json");

    public static ActivationRecord? Load()
    {
        try
        {
            if (!File.Exists(PathName)) return null;
            return System.Text.Json.JsonSerializer.Deserialize<ActivationRecord>(File.ReadAllText(PathName));
        }
        catch
        {
            return null;
        }
    }

    public static void Save(LifecycleInfo info)
    {
        try
        {
            Directory.CreateDirectory(PersistentStore.LocalDirectory);
            File.WriteAllText(PathName, System.Text.Json.JsonSerializer.Serialize(new ActivationRecord
            {
                InstallId = info.InstallId,
                ActivatedAt = info.ActivatedAt,
                LastUninstallClearAt = info.LastUninstallClearAt,
                KeepActivationOnUninstall = info.KeepActivationOnUninstall,
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Activation preservation is best effort.
        }
    }

    public static void Delete()
    {
        try { File.Delete(PathName); } catch { }
    }
}
