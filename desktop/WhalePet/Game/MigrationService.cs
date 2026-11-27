using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace WhalePet.Game;

public sealed class GifTransfer
{
    public string GifId { get; set; } = "";
    public string UnlockType { get; set; } = "";
    public string UnlockCondition { get; set; } = "";
    public DateTimeOffset? UnlockedAt { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? EnabledAt { get; set; }
    public bool EverEnabled { get; set; }
}

public sealed class DefaultAvatarTransfer
{
    public string Id { get; set; } = "";
    public string Data { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class CharacterImageTransfer
{
    public string Role { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string Data { get; set; } = "";
}

public sealed class MigrationPayload
{
    public int Version { get; set; } = 1;
    public string AppVersion { get; set; } = "1.1.0";
    public DateTimeOffset ExportedAt { get; set; }

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
    public int MaxLevelAffection { get; set; }

    public List<string> Inventory { get; set; } = new();
    public List<string> Staging { get; set; } = new();
    public List<DefaultAvatarTransfer> DefaultAvatars { get; set; } = new();
    public List<TodoItem> Todos { get; set; } = new();
    public List<MemoItem>? Memos { get; set; }
    public List<ClipboardEntry>? Clipboard { get; set; }
    public PetSettings? Settings { get; set; }
    public List<CharacterImageTransfer> CharacterImages { get; set; } = new();

    public string AvatarType { get; set; } = nameof(AvatarKind.DefaultStatic);
    public string? CurrentGifId { get; set; }
    public string? CurrentCustomImagePath { get; set; }
    public string? CurrentCustomImageData { get; set; }
    public string CurrentLive2DModelId { get; set; } = "seethrough_output";
    public DateTimeOffset? CurrentAvatarUpdatedAt { get; set; }

    public List<GifTransfer> Gifs { get; set; } = new();
    public Dictionary<string, DateTimeOffset> Achievements { get; set; } = new();
    public Dictionary<string, long> Counters { get; set; } = new();
    public List<int> SignInDates { get; set; } = new();
}

public sealed class MigrationEnvelope
{
    public int Version { get; set; } = 1;
    public int KdfIterations { get; set; } = 200_000;
    public string Salt { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Ciphertext { get; set; } = "";
    public string Tag { get; set; } = "";
    public DateTimeOffset ExportedAt { get; set; }
    public string AppVersion { get; set; } = "";
    public string PackageHash { get; set; } = "";
    public string Hmac { get; set; } = "";
}

/// <summary>
/// User-asset migration. Only whitelisted user assets are exported; device
/// binding, activation state, last-online time and daily counters never enter a
/// package. Passwords/keys live only in memory and are never logged.
/// </summary>
public sealed class MigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
    };

    private static readonly HashSet<string> PayloadKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "version", "appVersion", "exportedAt", "level", "affection", "affectionCarry", "mood", "satiety",
        "food", "ingredients", "fish", "totalCompanionSeconds", "continuousSignIn", "totalSignInDays",
        "lastSignDateIndex", "maxLevelAffection", "inventory", "staging", "defaultAvatars", "todos", "memos", "clipboard", "settings", "characterImages", "avatarType", "currentGifId", "currentCustomImagePath",
        "currentCustomImageData", "currentLive2DModelId",
        "currentAvatarUpdatedAt", "gifs", "achievements", "counters", "signInDates",
    };

    private static readonly HashSet<string> GifKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "gifId", "unlockType", "unlockCondition", "unlockedAt", "isEnabled", "enabledAt", "everEnabled",
    };

    private static readonly HashSet<string> AvatarKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "data", "enabled",
    };

    private static readonly HashSet<string> TodoKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "title", "notes", "urgency", "status", "createdAt", "updatedAt",
    };

    private static readonly HashSet<string> MemoKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "text", "createdAt", "done",
    };

    private static readonly HashSet<string> ClipboardKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "text", "createdAt",
    };

    private static readonly HashSet<string> CharacterImageKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "role", "sourcePath", "data",
    };

    private static readonly string[] ForbiddenPayloadKeys =
    {
        "lastonline", "lastonlineutc", "device", "mac", "fingerprint", "activation", "installid",
        "daily", "today", "fishingused", "cookingused", "taskprogress", "taskid", "migrationlog",
        "userdatakey", "password", "key",
    };

    private static readonly HashSet<string> EnvelopeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "version", "kdfIterations", "salt", "nonce", "ciphertext", "tag", "exportedAt", "appVersion",
        "packageHash", "hmac",
    };

    private readonly GameEngine _engine;

    public MigrationService(GameEngine engine)
    {
        _engine = engine;
    }

    public static string UserDataKeyCheck()
    {
        var key = ResourceSecrets.DeriveUserDataKey(DeviceFingerprint.Value());
        return Convert.ToHexString(SHA256.HashData(key));
    }

    public void Export(Window owner)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出迁移包",
            Filter = "鲸鱼娘迁移包 (*.whalepet)|*.whalepet|所有文件 (*.*)|*.*",
            FileName = $"WhalePet-迁移包-{DateTime.Now:yyyyMMdd-HHmm}.whalepet",
            AddExtension = true,
            DefaultExt = ".whalepet",
        };
        if (dialog.ShowDialog(owner) != true) return;

        var prompt = new PasswordPromptWindow(
            "设置迁移密码",
            "迁移包只包含用户资产（等级、好感、签到、成就、解锁与启用的动态图等），不包含设备绑定、激活状态、最后在线时间和当日次数。",
            confirm: true,
            "密码长度至少 8 位。请妥善保管：丢失密码将无法恢复迁移包内容。") { Owner = owner };
        if (prompt.ShowDialog() != true) return;

        var password = prompt.Password;
        var exportedAt = DateTimeOffset.Now;
        string packageHash = "";
        try
        {
            var payload = BuildPayload(owner, exportedAt);
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            var salt = RandomNumberGenerator.GetBytes(16);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var key = DeriveKey(password, salt, 200_000);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }
            CryptographicOperations.ZeroMemory(plaintext);

            packageHash = HashPackage(salt, nonce, ciphertext, tag);
            var envelope = new MigrationEnvelope
            {
                Version = 1,
                KdfIterations = 200_000,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(ciphertext),
                Tag = Convert.ToBase64String(tag),
                ExportedAt = exportedAt,
                AppVersion = "1.1.0",
                PackageHash = packageHash,
                Hmac = ComputeHmac(key, packageHash, exportedAt, "1.1.0"),
            };

            var json = JsonSerializer.Serialize(envelope, JsonOptions);
            File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
            _engine.Database.AddMigrationLog("Export", "1.1.0", exportedAt, null, packageHash, "Success",
                "导出成功");
            MessageBox.Show($"迁移包已导出：\n{dialog.FileName}\n\n请同时记住迁移密码，丢失后无法恢复。",
                "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception error)
        {
            if (packageHash.Length > 0)
            {
                _engine.Database.AddMigrationLog("Export", "1.1.0", exportedAt, null, packageHash, "Failed",
                    SafeMessage(error));
            }
            throw;
        }
    }

    public void Import(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入迁移包",
            Filter = "鲸鱼娘迁移包 (*.whalepet)|*.whalepet|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(owner) != true) return;

        var prompt = new PasswordPromptWindow(
            "输入迁移密码",
            "将校验迁移包版本、HMAC 与 GCM 标签。导入会覆盖当前用户数据，并自动备份当前数据库。",
            confirm: false,
            "密码只在本机内存中使用，不会写入日志。") { Owner = owner };
        if (prompt.ShowDialog() != true) return;
        var password = prompt.Password;

        string packageHash = "";
        try
        {
            var json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            if (json.Length > 40 * 1024 * 1024) throw new InvalidOperationException("迁移包过大。");
            using var document = JsonDocument.Parse(json);
            ValidateKeys(document.RootElement, EnvelopeKeys, "迁移包");

            var envelope = JsonSerializer.Deserialize<MigrationEnvelope>(json, JsonOptions)
                           ?? throw new InvalidOperationException("迁移包格式无效。");
            if (envelope.Version != 1) throw new InvalidOperationException("不支持的迁移包版本。");
            if (envelope.KdfIterations is < 100_000 or > 1_000_000)
                throw new InvalidOperationException("迁移包 KDF 参数异常。");
            if (envelope.ExportedAt > DateTimeOffset.Now.AddMinutes(10))
                throw new InvalidOperationException("迁移包时间晚于本机时间，疑似系统时间回拨，已拒绝导入。");

            var salt = Convert.FromBase64String(envelope.Salt);
            var nonce = Convert.FromBase64String(envelope.Nonce);
            var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            var tag = Convert.FromBase64String(envelope.Tag);
            packageHash = envelope.PackageHash;
            if (!string.Equals(packageHash, HashPackage(salt, nonce, ciphertext, tag), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("迁移包完整性校验失败（PackageHash 不匹配）。");

            if (_engine.Database.IsPackageUsed(packageHash))
            {
                _engine.Database.AddMigrationLog("Import", "1.1.0", null, DateTimeOffset.Now, packageHash,
                    "Rejected", "迁移包已使用");
                throw new InvalidOperationException("该迁移包已经使用过，不能重复导入。");
            }

            var key = DeriveKey(password, salt, envelope.KdfIterations);
            var expectedHmac = ComputeHmac(key, packageHash, envelope.ExportedAt, envelope.AppVersion);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(expectedHmac), Convert.FromHexString(envelope.Hmac)))
            {
                throw new InvalidOperationException("密码错误或迁移包被篡改（HMAC 校验失败）。");
            }

            var plaintext = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("密码错误或迁移包被篡改（解密失败）。");
            }

            using var payloadDocument = JsonDocument.Parse(plaintext);
            ValidatePayload(payloadDocument.RootElement);
            var payload = JsonSerializer.Deserialize<MigrationPayload>(plaintext, JsonOptions)
                          ?? throw new InvalidOperationException("迁移包内容无效。");
            CryptographicOperations.ZeroMemory(plaintext);

            var snapshot = BuildSnapshot(payload);
            _engine.Save();
            _engine.Database.Backup();
            _engine.ApplyImportedSnapshot(snapshot);
            var todoData = TodoStore.Load();
            todoData.Items = (payload.Todos ?? new List<TodoItem>())
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .Take(1000)
                .Select(item => new TodoItem
                {
                    Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
                    Title = item.Title.Trim().Length > 120 ? item.Title.Trim()[..120] : item.Title.Trim(),
                    Notes = (item.Notes ?? "").Trim().Length > 1000
                        ? (item.Notes ?? "").Trim()[..1000]
                        : (item.Notes ?? "").Trim(),
                    Urgency = TodoPalette.Urgencies.Any(entry => entry.Key == item.Urgency)
                        ? item.Urgency
                        : "Green",
                    Status = TodoPalette.Statuses.Any(entry => entry.Key == item.Status)
                        ? item.Status
                        : "NotStarted",
                    CreatedAt = item.CreatedAt == default ? DateTimeOffset.Now : item.CreatedAt,
                    UpdatedAt = item.UpdatedAt == default ? DateTimeOffset.Now : item.UpdatedAt,
                })
                .ToList();
            TodoStore.Save(todoData);
            if (payload.Memos is { } memos)
            {
                MemoStore.Save(memos
                    .Where(memo => !string.IsNullOrWhiteSpace(memo.Text))
                    .Take(100)
                    .Select(memo => new MemoItem
                    {
                        Id = string.IsNullOrWhiteSpace(memo.Id) ? Guid.NewGuid().ToString("N") : memo.Id,
                        Text = memo.Text.Trim(),
                        CreatedAt = memo.CreatedAt == default ? DateTime.Now : memo.CreatedAt,
                        Done = memo.Done,
                    }));
            }
            if (payload.Clipboard is { } clipboard)
            {
                ClipboardStore.Save(clipboard
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
                    .Take(ClipboardStore.MaxEntries)
                    .Select(entry => new ClipboardEntry
                    {
                        Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                        Text = entry.Text,
                        CreatedAt = entry.CreatedAt == default ? DateTime.Now : entry.CreatedAt,
                    }));
                ClipboardStore.NotifyChanged();
            }
            if (payload.Settings is { } importedSettings)
            {
                ImportCharacterImages(payload.CharacterImages, importedSettings);
                importedSettings.Normalize();
                SettingsStore.Save(importedSettings);
            }
            _engine.Database.AddMigrationLog("Import", "1.1.0", null, DateTimeOffset.Now, packageHash,
                "Success", "导入成功");
        }
        catch (Exception error)
        {
            _engine.Database.AddMigrationLog("Import", "1.1.0", null, DateTimeOffset.Now,
                packageHash.Length > 0 ? packageHash : "unknown", "Failed", SafeMessage(error));
            throw;
        }
    }

    private MigrationPayload BuildPayload(Window owner, DateTimeOffset exportedAt)
    {
        var state = _engine.State;
        var snapshot = _engine.CreateSnapshot();
        var exportedSettings = SettingsStore.Load();

        string? customData = null;
        if (state.Avatar == AvatarKind.CustomImage && !string.IsNullOrWhiteSpace(state.CurrentCustomImagePath) &&
            File.Exists(state.CurrentCustomImagePath))
        {
            var info = new FileInfo(state.CurrentCustomImagePath);
            var include = true;
            if (info.Length > 2 * 1024 * 1024)
            {
                include = MessageBox.Show(
                    $"自定义主形象图片约 {info.Length / 1024.0 / 1024.0:0.0} MB，较大。\n是否将其加入迁移包？\n选择“否”则导入后回退内置默认静态图。",
                    "自定义形象", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            }
            if (include)
            {
                customData = Convert.ToBase64String(File.ReadAllBytes(state.CurrentCustomImagePath));
            }
        }

        return new MigrationPayload
        {
            Version = 1,
            AppVersion = "1.1.0",
            ExportedAt = exportedAt,
            Level = state.Level,
            Affection = state.Affection,
            AffectionCarry = state.AffectionCarry,
            Mood = state.Mood,
            Satiety = state.Satiety,
            Food = state.Food,
            Ingredients = state.Ingredients,
            Fish = state.Fish,
            TotalCompanionSeconds = state.TotalCompanionSeconds,
            ContinuousSignIn = state.ContinuousSignIn,
            TotalSignInDays = state.TotalSignInDays,
            LastSignDateIndex = state.LastSignDateIndex,
            MaxLevelAffection = state.MaxLevelAffection,
            Inventory = snapshot.Inventory,
            Staging = snapshot.Staging,
            DefaultAvatars = _engine.DefaultAvatars
                .Where(avatar => !avatar.IsBuiltIn)
                .Take(5)
                .Select(avatar => new DefaultAvatarTransfer
                {
                    Id = avatar.Id,
                    Data = avatar.Data,
                    Enabled = avatar.Enabled,
                })
                .ToList(),
            Todos = TodoStore.Load().Items
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .Take(1000)
                .ToList(),
            Memos = MemoStore.Load()
                .Where(memo => !string.IsNullOrWhiteSpace(memo.Text))
                .Take(100)
                .Select(memo => new MemoItem
                {
                    Id = memo.Id,
                    Text = memo.Text,
                    CreatedAt = memo.CreatedAt,
                    Done = memo.Done,
                })
                .ToList(),
            Clipboard = ClipboardStore.Load()
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
                .Take(ClipboardStore.MaxEntries)
                .Select(entry => new ClipboardEntry
                {
                    Id = entry.Id,
                    Text = entry.Text,
                    CreatedAt = entry.CreatedAt,
                })
                .ToList(),
            Settings = exportedSettings,
            CharacterImages = CollectCharacterImages(exportedSettings),
            AvatarType = customData is null && state.Avatar == AvatarKind.CustomImage
                ? nameof(AvatarKind.DefaultStatic)
                : state.AvatarType,
            CurrentGifId = state.CurrentGifId,
            CurrentCustomImagePath = null,
            CurrentCustomImageData = customData,
            CurrentLive2DModelId = state.CurrentLive2DModelId,
            CurrentAvatarUpdatedAt = state.CurrentAvatarUpdatedAt,
            Gifs = snapshot.Gifs.Where(gif => gif.IsUnlocked).Select(gif => new GifTransfer
            {
                GifId = gif.GifId,
                UnlockType = gif.UnlockType,
                UnlockCondition = gif.UnlockCondition,
                UnlockedAt = gif.UnlockedAt,
                IsEnabled = gif.IsEnabled,
                EnabledAt = gif.EnabledAt,
                EverEnabled = gif.EverEnabled,
            }).ToList(),
            Achievements = snapshot.Achievements,
            Counters = snapshot.Counters,
            SignInDates = snapshot.SignInDates,
        };
    }

    private static List<CharacterImageTransfer> CollectCharacterImages(PetSettings settings)
    {
        var result = new List<CharacterImageTransfer>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builtInRoot = Path.GetFullPath(AssetBootstrapper.DirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var totalBytes = 0L;

        void Add(string role, string raw)
        {
            if (result.Count >= 30 || totalBytes >= 20 * 1024 * 1024) return;
            try
            {
                var resolved = settings.ResolveImagePath(raw);
                if (string.IsNullOrWhiteSpace(resolved)) return;
                var full = Path.GetFullPath(resolved);
                if (!File.Exists(full) || full.StartsWith(builtInRoot, StringComparison.OrdinalIgnoreCase))
                    return;
                if (!seen.Add(full)) return;
                var bytes = File.ReadAllBytes(full);
                if (bytes.Length is 0 or > 8 * 1024 * 1024) return;
                totalBytes += bytes.Length;
                result.Add(new CharacterImageTransfer
                {
                    Role = role,
                    SourcePath = raw,
                    Data = Convert.ToBase64String(bytes),
                });
            }
            catch
            {
                // Skip unreadable custom images.
            }
        }

        foreach (var path in settings.MainImages) Add("main", path);
        foreach (var path in settings.IdleImages) Add("idle", path);
        foreach (var path in settings.PlayImages) Add("play", path);
        return result;
    }

    private static void ImportCharacterImages(IEnumerable<CharacterImageTransfer>? transfers,
        PetSettings settings)
    {
        if (transfers is null) return;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var directory = Path.Combine(PersistentStore.LocalDirectory, "migrated-images");
            Directory.CreateDirectory(directory);
            foreach (var transfer in transfers)
            {
                if (string.IsNullOrWhiteSpace(transfer.SourcePath) ||
                    string.IsNullOrWhiteSpace(transfer.Data)) continue;
                byte[] bytes;
                try { bytes = Convert.FromBase64String(transfer.Data); }
                catch { continue; }
                if (bytes.Length is 0 or > 8 * 1024 * 1024) continue;
                var extension = Path.GetExtension(transfer.SourcePath).ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp"))
                    extension = ".png";
                var name = "migrated-" + Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant() + extension;
                var target = Path.Combine(directory, name);
                if (!File.Exists(target)) File.WriteAllBytes(target, bytes);
                map[transfer.SourcePath] = target;
            }
        }
        catch
        {
            // Settings still import without the optional image bundle.
        }

        void Replace(List<string> paths)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                if (map.TryGetValue(paths[i], out var target)) paths[i] = target;
            }
        }

        if (map.Count == 0) return;
        Replace(settings.MainImages);
        Replace(settings.IdleImages);
        Replace(settings.PlayImages);
        Replace(settings.DisabledMainImages);
        Replace(settings.DisabledIdleImages);
        Replace(settings.DisabledPlayImages);
    }

    private static AppStateSnapshot BuildSnapshot(MigrationPayload payload)
    {
        var catalog = GameRuntime.Catalog ?? throw new InvalidOperationException("GIF 资源清单不可用。");
        var state = new GameState
        {
            Level = Math.Clamp(payload.Level, 1, 52),
            Affection = Math.Clamp(payload.Affection, 0, 99),
            AffectionCarry = Math.Clamp(payload.AffectionCarry, 0, 0.999),
            Mood = Math.Clamp(payload.Mood, 0, 100),
            Satiety = Math.Clamp(payload.Satiety, 0, 100),
            Food = Math.Max(0, payload.Food),
            Ingredients = Math.Max(0, payload.Ingredients),
            Fish = Math.Max(0, payload.Fish),
            TotalCompanionSeconds = Math.Max(0, payload.TotalCompanionSeconds),
            ContinuousSignIn = Math.Max(0, payload.ContinuousSignIn),
            TotalSignInDays = Math.Max(0, payload.TotalSignInDays),
            LastSignDateIndex = payload.LastSignDateIndex,
            MaxLevelAffection = Math.Max(0, payload.MaxLevelAffection),
            AvatarType = payload.AvatarType,
            CurrentGifId = payload.CurrentGifId,
            CurrentCustomImagePath = null,
            CurrentCustomImageData = payload.CurrentCustomImageData,
            CurrentLive2DModelId = string.IsNullOrWhiteSpace(payload.CurrentLive2DModelId)
                ? "seethrough_output"
                : payload.CurrentLive2DModelId,
            CurrentAvatarUpdatedAt = payload.CurrentAvatarUpdatedAt,
        };

        var gifs = new List<GifUnlockRow>();
        foreach (var transfer in payload.Gifs.Where(item => !string.IsNullOrWhiteSpace(item.GifId)).Take(200))
        {
            var entry = catalog.Find(transfer.GifId);
            if (entry is null) continue;
            gifs.Add(new GifUnlockRow
            {
                GifId = entry.GifId,
                SortIndex = entry.SortIndex,
                UnlockType = entry.UnlockType,
                UnlockCondition = entry.UnlockCondition,
                IsUnlocked = true,
                UnlockedAt = transfer.UnlockedAt ?? DateTimeOffset.Now,
                IsEnabled = transfer.IsEnabled,
                EnabledAt = transfer.EnabledAt,
                EverEnabled = transfer.EverEnabled,
            });
        }

        if (state.Avatar == AvatarKind.Gif &&
            (state.CurrentGifId is null ||
             !gifs.Any(gif => gif.IsEnabled && string.Equals(gif.GifId, state.CurrentGifId, StringComparison.OrdinalIgnoreCase))))
        {
            state.AvatarType = nameof(AvatarKind.DefaultStatic);
            state.CurrentGifId = null;
        }

        if (state.Avatar == AvatarKind.CustomImage && string.IsNullOrWhiteSpace(state.CurrentCustomImageData) &&
            string.IsNullOrWhiteSpace(state.CurrentCustomImagePath))
        {
            state.AvatarType = nameof(AvatarKind.DefaultStatic);
        }

        // Custom images travel as data, never as a device-specific path.
        if (!string.IsNullOrWhiteSpace(state.CurrentCustomImageData))
        {
            try
            {
                var bytes = Convert.FromBase64String(state.CurrentCustomImageData);
                if (bytes.Length is > 0 and <= 8 * 1024 * 1024)
                {
                    System.IO.Directory.CreateDirectory(PersistentStore.LocalDirectory);
                    var path = Path.Combine(PersistentStore.LocalDirectory, "custom-avatar.png");
                    File.WriteAllBytes(path, bytes);
                    state.CurrentCustomImagePath = path;
                    state.CurrentCustomImageData = null;
                }
            }
            catch
            {
                state.AvatarType = nameof(AvatarKind.DefaultStatic);
            }
        }

        if (state.Avatar == AvatarKind.Live2D)
        {
            var modelId = string.IsNullOrWhiteSpace(state.CurrentLive2DModelId)
                ? "seethrough_output"
                : state.CurrentLive2DModelId;
            var modelPath = Path.Combine(AppContext.BaseDirectory, "Live2DWeb", "models", modelId,
                modelId + ".model3.json");
            if (!File.Exists(modelPath))
            {
                state.AvatarType = nameof(AvatarKind.DefaultStatic);
                Live2DLog.Write("迁移目标缺少 Live2D 模型，已回退默认形象");
            }
        }

        var achievements = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in payload.Achievements)
        {
            if (AchievementCatalog.ById.ContainsKey(pair.Key)) achievements[pair.Key] = pair.Value;
        }

        var counters = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in payload.Counters)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 40) continue;
            if (pair.Key.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '_'))) continue;
            counters[pair.Key] = Math.Clamp(pair.Value, -1_000_000, 1_000_000_000);
        }

        var signInDates = payload.SignInDates
            .Where(day => day is > 0 and < 200000)
            .Distinct()
            .OrderBy(day => day)
            .ToList();

        var capacity = GameEngine.BaseInventorySlots + Math.Max(0, state.Level - 1);
        var inventory = (payload.Inventory ?? new List<string>())
            .Where(ItemCatalog.IsKnown)
            .Take(capacity)
            .ToList();
        var staging = (payload.Staging ?? new List<string>())
            .Where(ItemCatalog.IsKnown)
            .Take(GameEngine.MaxStaging)
            .ToList();
        if (inventory.Count == 0 && staging.Count == 0)
        {
            // Packages made before the item backpack existed carry only counts.
            var legacy = new List<string>();
            for (var i = 0; i < Math.Max(0, payload.Food); i++) legacy.Add(ItemCatalog.Sushi);
            for (var i = 0; i < Math.Max(0, payload.Ingredients); i++) legacy.Add(ItemCatalog.Fish);
            inventory = legacy.Take(capacity).ToList();
            staging = legacy.Skip(capacity).Take(GameEngine.MaxStaging).ToList();
        }

        var builtIn = GameRuntime.Engine?.BuiltInDefaultAvatar ?? new DefaultAvatar
        {
            Id = "builtin-default",
            Kind = "Builtin",
            Data = "",
            Enabled = true,
        };
        var defaultAvatars = new List<DefaultAvatar> { builtIn };
        var seenAvatarIds = new HashSet<string>(StringComparer.Ordinal) { builtIn.Id };
        foreach (var transfer in (payload.DefaultAvatars ?? new List<DefaultAvatarTransfer>()).Take(5))
        {
            if (string.IsNullOrWhiteSpace(transfer.Id) || !seenAvatarIds.Add(transfer.Id)) continue;
            if (string.IsNullOrWhiteSpace(transfer.Data)) continue;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(transfer.Data); }
            catch { continue; }
            if (bytes.Length is 0 or > 8 * 1024 * 1024) continue;
            defaultAvatars.Add(new DefaultAvatar
            {
                Id = transfer.Id.Length > 64 ? transfer.Id[..64] : transfer.Id,
                Kind = "Custom",
                Data = transfer.Data,
                Enabled = transfer.Enabled,
            });
        }

        return new AppStateSnapshot
        {
            State = state,
            Gifs = gifs,
            Achievements = achievements,
            Counters = counters,
            SignInDates = signInDates,
            Inventory = inventory,
            Staging = staging,
            StagingDayIndex = GameState.DateIndex(DateTime.Now),
            DefaultAvatars = defaultAvatars,
        };
    }

    private static void ValidatePayload(JsonElement root)
    {
        ValidateKeys(root, PayloadKeys, "迁移包内容");
        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name;
            if (ForbiddenPayloadKeys.Any(forbidden =>
                    name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("迁移包含有禁止字段：" + name + "，整个包已拒绝。");
            }
        }

        if (root.TryGetProperty("gifs", out var gifs) && gifs.ValueKind == JsonValueKind.Array)
        {
            foreach (var gif in gifs.EnumerateArray())
            {
                if (gif.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("迁移包动态图格式无效。");
                foreach (var property in gif.EnumerateObject())
                {
                    if (!GifKeys.Contains(property.Name))
                        throw new InvalidOperationException("迁移包动态图记录包含未知字段：" + property.Name);
                    if (property.Name.Contains("device", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("mac", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("迁移包动态图记录包含禁止字段。");
                    }
                }
            }
        }

        if (root.TryGetProperty("defaultAvatars", out var avatars) && avatars.ValueKind == JsonValueKind.Array)
        {
            if (avatars.GetArrayLength() > 5)
                throw new InvalidOperationException("迁移包默认主形象数量超过 5 张。");
            foreach (var avatar in avatars.EnumerateArray())
            {
                if (avatar.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("迁移包默认主形象格式无效。");
                foreach (var property in avatar.EnumerateObject())
                {
                    if (!AvatarKeys.Contains(property.Name))
                        throw new InvalidOperationException("迁移包默认主形象记录包含未知字段：" + property.Name);
                }
            }
        }

        if (root.TryGetProperty("todos", out var todos) && todos.ValueKind == JsonValueKind.Array)
        {
            if (todos.GetArrayLength() > 1000)
                throw new InvalidOperationException("迁移包待办数量超过 1000 条。");
            foreach (var todo in todos.EnumerateArray())
            {
                if (todo.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("迁移包待办格式无效。");
                ValidateKeys(todo, TodoKeys, "迁移包待办记录");
            }
        }

        if (root.TryGetProperty("memos", out var memos) && memos.ValueKind == JsonValueKind.Array)
        {
            if (memos.GetArrayLength() > 100)
                throw new InvalidOperationException("迁移包备忘录数量超过 100 条。");
            foreach (var memo in memos.EnumerateArray())
            {
                if (memo.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("迁移包备忘录格式无效。");
                ValidateKeys(memo, MemoKeys, "迁移包备忘录记录");
            }
        }

        if (root.TryGetProperty("clipboard", out var clipboard) && clipboard.ValueKind == JsonValueKind.Array)
        {
            if (clipboard.GetArrayLength() > ClipboardStore.MaxEntries)
                throw new InvalidOperationException("迁移包剪贴板记录超过上限。");
            foreach (var entry in clipboard.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("迁移包剪贴板格式无效。");
                ValidateKeys(entry, ClipboardKeys, "迁移包剪贴板记录");
            }
        }

        if (root.TryGetProperty("characterImages", out var characterImages) &&
            characterImages.ValueKind == JsonValueKind.Array)
        {
            if (characterImages.GetArrayLength() > 30)
                throw new InvalidOperationException("迁移包自定义角色图片数量超过上限。");
            foreach (var image in characterImages.EnumerateArray())
            {
                if (image.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("迁移包角色图片格式无效。");
                ValidateKeys(image, CharacterImageKeys, "迁移包角色图片记录");
            }
        }

        if (root.TryGetProperty("settings", out var settings) &&
            settings.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null)
        {
            throw new InvalidOperationException("迁移包设置配置格式无效。");
        }
    }

    private static void ValidateKeys(JsonElement root, HashSet<string> allowed, string label)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidOperationException(label + "格式无效。");
        foreach (var property in root.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                throw new InvalidOperationException($"{label}包含未知或禁止字段：{property.Name}");
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);

    private static string ComputeHmac(byte[] key, string packageHash, DateTimeOffset exportedAt, string version)
    {
        using var hmac = new HMACSHA256(key);
        var text = $"{packageHash}|{exportedAt:O}|{version}|Migration";
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }

    private static string HashPackage(byte[] salt, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(salt);
        hash.AppendData(nonce);
        hash.AppendData(ciphertext);
        hash.AppendData(tag);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string SafeMessage(Exception error)
    {
        var message = error.Message ?? "未知错误";
        return message.Length > 300 ? message[..300] : message;
    }
}
