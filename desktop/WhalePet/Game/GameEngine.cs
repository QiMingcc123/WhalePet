using System.IO;

namespace WhalePet.Game;

/// <summary>
/// Gameplay engine for the character panel: time settlement, stats, tasks,
/// sign-in, lottery, fishing/cooking gates, achievements and GIF unlocks.
/// </summary>
public sealed class GameEngine
{
    public const int MinLevel = 1;
    public const int MaxLevel = 52;
    public const int MaxEnabledGifs = 5;
    public const int InteractionCooldownSeconds = 30;
    public const int DailyMoodBonusLimit = 5;
    public const int BaseInventorySlots = 10;
    public const int MaxStaging = 20;

    private readonly GameStore _store;
    private readonly GifCatalog _catalog;
    private readonly Dictionary<string, long> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _achievements = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unlockedGifs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _enabledGifs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _inventory = new();
    private readonly List<string> _staging = new();
    private readonly List<DefaultAvatar> _defaultAvatars = new();
    private int _stagingDayIndex;
    private GameState _state = new();
    private DateTimeOffset _lastHeartbeat = DateTimeOffset.Now;
    private DateTimeOffset _sessionStart = DateTimeOffset.Now;
    private DateTimeOffset _lastPersist = DateTimeOffset.Now;
    private bool _initialized;

    public GameEngine(GameStore store, GifCatalog catalog)
    {
        _store = store;
        _catalog = catalog;
    }

    public event Action? Changed;
    public event Action? AvatarChanged;
    public event Action<string>? Toast;
    public string? AvatarFallbackMessage { get; private set; }

    public GameState State => _state;
    public GameStore Database => _store;
    public IReadOnlyDictionary<string, long> Counters => _counters;
    public IReadOnlySet<string> UnlockedGifs => _unlockedGifs;
    public IReadOnlySet<string> EnabledGifs => _enabledGifs;
    public IReadOnlyList<string> Inventory => _inventory;
    public IReadOnlyList<string> Staging => _staging;
    public IReadOnlyList<DefaultAvatar> DefaultAvatars => _defaultAvatars;
    public DefaultAvatar? BuiltInDefaultAvatar =>
        _defaultAvatars.FirstOrDefault(avatar => avatar.IsBuiltIn);
    public int CustomDefaultAvatarCount =>
        _defaultAvatars.Count(avatar => !avatar.IsBuiltIn);
    public int InventoryCapacity => BaseInventorySlots + Math.Max(0, _state.Level - 1);
    public int FoodItemCount => _inventory.Count(ItemCatalog.IsFood);
    public int IngredientItemCount => _inventory.Count(item => string.Equals(item, ItemCatalog.Fish, StringComparison.OrdinalIgnoreCase));
    public bool IsInitialized => _initialized;
    public string InstallId => _store.LoadLifecycle().InstallId;

    public void Initialize()
    {
        _store.Initialize();
        _state = _store.LoadState();
        foreach (var pair in _store.LoadCounters()) _counters[pair.Key] = pair.Value;
        foreach (var id in _store.LoadAchievements()) _achievements.Add(id);
        foreach (var gif in _store.LoadGifUnlocks())
        {
            if (gif.IsUnlocked) _unlockedGifs.Add(gif.GifId);
            if (gif.IsEnabled && gif.IsUnlocked) _enabledGifs.Add(gif.GifId);
        }
        _inventory.Clear();
        _inventory.AddRange(_store.LoadInventory());
        _staging.Clear();
        _staging.AddRange(_store.LoadStaging());
        _stagingDayIndex = _store.LoadStagingDay();
        _defaultAvatars.Clear();
        _defaultAvatars.AddRange(_store.LoadDefaultAvatars());
        EnsureBuiltInDefaultAvatar();

        var now = DateTimeOffset.Now;
        _lastHeartbeat = now;
        _sessionStart = now;
        if (GetCounter("install_first_seen") == 0)
        {
            SetCounter("install_first_seen", 1);
        }

        if (_store.IsFirstRun)
        {
            _state.SignWeekStart = GameState.WeekStartIndex(now.LocalDateTime);
            _state.LastOnlineUtc = now;
            _state.LastDateIndex = GameState.DateIndex(now.LocalDateTime);
            _state.TaskDateIndex = _state.LastDateIndex;
            RefreshTask(now, force: true);
            SaveState();
        }
        else
        {
            EnsureSignWeek(now.LocalDateTime);
            SettleOffline(now, showNotice: true);
            EnsureTask(now);
        }

        ValidateAvatar();
        MigrateLegacyInventory(now);
        EnsureStagingDay(now);
        SyncItemMirrors();
        SaveState();
        _initialized = true;
        CheckAchievements();
        var lifecycle = _store.LoadLifecycle();
        lifecycle.DataSchemaVersion = GameStore.SchemaVersion;
        if (_store.IsFirstRun || string.IsNullOrWhiteSpace(lifecycle.UserDataKeyCheck))
        {
            lifecycle.UserDataKeyCheck = MigrationService.UserDataKeyCheck();
        }
        _store.SaveLifecycle(lifecycle);
        Changed?.Invoke();
    }

    // ----------------------------------------------------------- inventory

    private void MigrateLegacyInventory(DateTimeOffset now)
    {
        if (_store.LoadInventoryMigrated()) return;
        if (_inventory.Count == 0 && _staging.Count == 0)
        {
            // Old versions stored abstract counts. Preserve them as real items.
            for (var i = 0; i < Math.Max(0, _state.Food); i++) AddItemInternal(ItemCatalog.Sushi);
            for (var i = 0; i < Math.Max(0, _state.Ingredients); i++) AddItemInternal(ItemCatalog.Fish);
        }
        _stagingDayIndex = GameState.DateIndex(now.LocalDateTime);
        _store.SaveInventory(_inventory);
        _store.SaveStaging(_staging, _stagingDayIndex);
        _store.MarkInventoryMigrated();
        SyncItemMirrors();
    }

    private void SyncItemMirrors()
    {
        _state.Food = FoodItemCount;
        _state.Ingredients = IngredientItemCount;
    }

    private void PersistItems()
    {
        SyncItemMirrors();
        _store.SaveInventory(_inventory);
        _store.SaveStaging(_staging, _stagingDayIndex);
        SaveState();
    }

    private void EnsureStagingDay(DateTimeOffset now)
    {
        var today = GameState.DateIndex(now.LocalDateTime);
        if (_stagingDayIndex == today) return;
        _staging.Clear();
        _stagingDayIndex = today;
        _store.SaveStaging(_staging, _stagingDayIndex);
        if (_initialized) Changed?.Invoke();
    }

    private bool AddItemInternal(string itemId)
    {
        if (!ItemCatalog.IsKnown(itemId)) return false;
        if (_inventory.Count < InventoryCapacity)
        {
            _inventory.Add(itemId);
            return true;
        }
        if (_staging.Count < MaxStaging)
        {
            _staging.Add(itemId);
            return false;
        }
        return false;
    }

    /// <summary>
    /// Adds one item. When the backpack is full it goes to the 20-slot staging
    /// area; when that is also full the item is discarded with a notice.
    /// </summary>
    public (bool Stored, bool Staged) AddItem(string itemId)
    {
        var now = DateTimeOffset.Now;
        EnsureStagingDay(now);
        if (!ItemCatalog.IsKnown(itemId)) return (false, false);

        if (_inventory.Count < InventoryCapacity)
        {
            _inventory.Add(itemId);
            PersistItems();
            Changed?.Invoke();
            return (true, false);
        }

        if (_staging.Count < MaxStaging)
        {
            _staging.Add(itemId);
            PersistItems();
            Toast?.Invoke("背包已满，物品已放入暂存区。");
            Changed?.Invoke();
            return (false, true);
        }

        PersistItems();
        Toast?.Invoke("背包和暂存区都已满，物品已丢弃。");
        Changed?.Invoke();
        return (false, false);
    }

    private void MoveStagingToInventory()
    {
        if (_staging.Count == 0 || _inventory.Count >= InventoryCapacity) return;
        var moved = false;
        while (_staging.Count > 0 && _inventory.Count < InventoryCapacity)
        {
            _inventory.Add(_staging[0]);
            _staging.RemoveAt(0);
            moved = true;
        }
        if (moved) PersistItems();
    }

    public (bool Ok, string Message) UseItem(int slotIndex)
    {
        var now = DateTimeOffset.Now;
        EnsureDay(now, applyStarve: true);
        EnsureStagingDay(now);
        if (slotIndex < 0 || slotIndex >= _inventory.Count) return (false, "这个格子是空的。");
        var def = ItemCatalog.Find(_inventory[slotIndex]);
        if (def is null) return (false, "物品数据异常。");
        if (def.Category == ItemCatalog.IngredientCategory)
        {
            return (false, "这是食材，需要烹饪后才能使用。");
        }
        if (_state.Satiety >= 100) return (false, "已经吃饱了，食物没有消耗。");

        var before = _state.Satiety;
        _inventory.RemoveAt(slotIndex);
        _state.TodayFeedCount++;
        _state.Satiety = Math.Min(100, _state.Satiety + def.Satiety);
        AddCounter("feed_total", 1);
        AdvanceTask("feed", _state.TodayFeedCount);
        AdvanceThresholdTasks();
        if (before < 50 && _state.Satiety >= 50) AdvanceTask("satiety_recovered", 1);
        MoveStagingToInventory();
        PersistItems();
        CheckAchievements();
        Changed?.Invoke();
        return (true, $"投喂成功，饱食度 +{def.Satiety}（{_state.Satiety}/100）。");
    }

    public bool HasIngredients => IngredientItemCount > 0;

    private bool ConsumeIngredient()
    {
        var index = _inventory.FindIndex(item =>
            string.Equals(item, ItemCatalog.Fish, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        _inventory.RemoveAt(index);
        MoveStagingToInventory();
        return true;
    }

    // ------------------------------------------------------- default avatars

    private void EnsureBuiltInDefaultAvatar()
    {
        var builtIn = _defaultAvatars.FirstOrDefault(avatar => avatar.IsBuiltIn);
        if (builtIn is null)
        {
            builtIn = new DefaultAvatar
            {
                Id = "builtin-default",
                Kind = "Builtin",
                Enabled = true,
                SortIndex = 0,
                Data = ReadBuiltInDefaultAvatarData(),
            };
            _defaultAvatars.Insert(0, builtIn);
            SaveDefaultAvatars();
        }
        else
        {
            builtIn.Enabled = true;
            if (string.IsNullOrWhiteSpace(builtIn.Data)) builtIn.Data = ReadBuiltInDefaultAvatarData();
        }
    }

    private static string ReadBuiltInDefaultAvatarData()
    {
        try
        {
            var path = AssetBootstrapper.PathOf("main.png");
            return File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : "";
        }
        catch
        {
            return "";
        }
    }

    private void SaveDefaultAvatars()
    {
        for (var i = 0; i < _defaultAvatars.Count; i++) _defaultAvatars[i].SortIndex = i;
        _store.SaveDefaultAvatars(_defaultAvatars);
    }

    /// <summary>Migrates the legacy path-based main image list once.</summary>
    public void MigrateLegacyDefaultAvatars(PetSettings settings)
    {
        if (_store.GetMeta("DefaultAvatarsMigrated") == "1") return;
        _store.SetMeta("DefaultAvatarsMigrated", "1");
        if (CustomDefaultAvatarCount > 0) return;

        var builtInPath = Path.GetFullPath(AssetBootstrapper.PathOf("main.png"));
        var disabled = new HashSet<string>(settings.DisabledMainImages ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var raw in settings.MainImages ?? new List<string>())
        {
            if (CustomDefaultAvatarCount >= 5) break;
            var path = settings.ResolveImagePath(raw);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
            if (string.Equals(Path.GetFullPath(path), builtInPath, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length is 0 or > 8 * 1024 * 1024) continue;
                _defaultAvatars.Add(new DefaultAvatar
                {
                    Id = "custom-" + Guid.NewGuid().ToString("N"),
                    Kind = "Custom",
                    Data = Convert.ToBase64String(bytes),
                    Enabled = !disabled.Contains(raw),
                });
            }
            catch
            {
                // Skip unreadable legacy images.
            }
        }
        SaveDefaultAvatars();
    }

    public bool AddDefaultAvatar(byte[] bytes, bool enabled = true)
    {
        if (bytes.Length is 0 or > 8 * 1024 * 1024) return false;
        if (CustomDefaultAvatarCount >= 5) return false;
        _defaultAvatars.Add(new DefaultAvatar
        {
            Id = "custom-" + Guid.NewGuid().ToString("N"),
            Kind = "Custom",
            Data = Convert.ToBase64String(bytes),
            Enabled = enabled,
        });
        SaveDefaultAvatars();
        Changed?.Invoke();
        return true;
    }

    public void ReplaceDefaultAvatars(IEnumerable<DefaultAvatar> avatars)
    {
        var builtIn = _defaultAvatars.FirstOrDefault(avatar => avatar.IsBuiltIn) ?? new DefaultAvatar
        {
            Id = "builtin-default",
            Kind = "Builtin",
            Enabled = true,
            Data = ReadBuiltInDefaultAvatarData(),
        };
        builtIn.Enabled = true;
        _defaultAvatars.Clear();
        _defaultAvatars.Add(builtIn);
        foreach (var avatar in avatars
                     .Where(avatar => !avatar.IsBuiltIn && avatar.Kind == "Custom")
                     .Take(5))
        {
            avatar.Id = string.IsNullOrWhiteSpace(avatar.Id) ? "custom-" + Guid.NewGuid().ToString("N") : avatar.Id;
            avatar.Kind = "Custom";
            avatar.Data = avatar.Data ?? "";
            _defaultAvatars.Add(avatar);
        }
        SaveDefaultAvatars();
        Changed?.Invoke();
    }

    public bool SetDefaultAvatarEnabled(string id, bool enabled)
    {
        var avatar = _defaultAvatars.FirstOrDefault(item => item.Id == id);
        if (avatar is null || avatar.IsBuiltIn) return false;
        avatar.Enabled = enabled;
        SaveDefaultAvatars();
        Changed?.Invoke();
        return true;
    }

    public bool DeleteDefaultAvatar(string id)
    {
        var avatar = _defaultAvatars.FirstOrDefault(item => item.Id == id);
        if (avatar is null || avatar.IsBuiltIn) return false;
        _defaultAvatars.Remove(avatar);
        SaveDefaultAvatars();
        Changed?.Invoke();
        return true;
    }

    public (bool Ok, string Message) SelectBuiltInDefault()
    {
        var builtIn = BuiltInDefaultAvatar;
        if (builtIn is null) return (false, "缺少内置默认主形象。");
        _state.AvatarType = nameof(AvatarKind.DefaultStatic);
        _state.CurrentGifId = null;
        _state.CurrentCustomImagePath = null;
        _state.CurrentCustomImageData = null;
        _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
        SaveState();
        AvatarChanged?.Invoke();
        Changed?.Invoke();
        return (true, "已切换到内置默认主形象。");
    }

    public (bool Ok, string Message) SelectCustomDefault(DefaultAvatar avatar)
    {
        if (avatar.IsBuiltIn) return SelectBuiltInDefault();
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(avatar.Data ?? "");
        }
        catch
        {
            return (false, "自定义主形象数据损坏。");
        }
        if (bytes.Length is 0 or > 8 * 1024 * 1024) return (false, "自定义主形象数据无效。");
        try
        {
            Directory.CreateDirectory(PersistentStore.LocalDirectory);
            var path = Path.Combine(PersistentStore.LocalDirectory, "custom-avatar.png");
            File.WriteAllBytes(path, bytes);
            _state.AvatarType = nameof(AvatarKind.CustomImage);
            _state.CurrentGifId = null;
            _state.CurrentCustomImagePath = path;
            _state.CurrentCustomImageData = avatar.Data;
            _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
            SaveState();
            AvatarChanged?.Invoke();
            Changed?.Invoke();
            return (true, "已切换自定义主形象。");
        }
        catch (Exception error)
        {
            return (false, "自定义主形象保存失败：" + error.Message);
        }
    }

    public string? CurrentCustomAvatarData =>
        _state.Avatar == AvatarKind.CustomImage ? _state.CurrentCustomImageData : null;

    // ---------------------------------------------------------------- time

    public void Tick()
    {
        if (!_initialized) return;
        var now = DateTimeOffset.Now;

        if (now < _lastHeartbeat.AddSeconds(-5))
        {
            MarkRollback(now);
            _lastHeartbeat = now;
            return;
        }

        var gap = now - _lastHeartbeat;
        _lastHeartbeat = now;
        if (gap.TotalMinutes >= 2)
        {
            SettleOffline(now, showNotice: true);
            _sessionStart = now;
            Changed?.Invoke();
            return;
        }

        EnsureDay(now, applyStarve: true);
        var seconds = Math.Clamp((int)Math.Round(gap.TotalSeconds), 0, 10);
        if (seconds > 0) AddOnlineSeconds(seconds, now);

        if ((now - _lastPersist).TotalSeconds >= 60)
        {
            SaveState();
            _lastPersist = now;
        }
    }

    private void AddOnlineSeconds(int seconds, DateTimeOffset now)
    {
        _state.TotalCompanionSeconds += seconds;
        _state.TodayOnlineSeconds += seconds;
        _state.OnlineCarrySeconds += seconds;
        SetCounter("companion_seconds", _state.TotalCompanionSeconds);

        var sessionMinutes = (long)Math.Max(0, (now - _sessionStart).TotalMinutes);
        if (sessionMinutes > GetCounter("online_streak_minutes"))
        {
            SetCounter("online_streak_minutes", sessionMinutes);
        }

        while (_state.OnlineCarrySeconds >= 3600)
        {
            _state.OnlineCarrySeconds -= 3600;
            _state.Mood = Math.Max(0, _state.Mood - 1);
            _state.Satiety = Math.Max(0, _state.Satiety - 2);
        }

        if (!_state.DayOnline30Counted && _state.TodayOnlineSeconds >= 1800)
        {
            _state.DayOnline30Counted = true;
            AddCounter("online_days", 1);
        }

        ProgressOnlineTask(now);
        AdvanceThresholdTasks();
        CheckAchievements();
    }

    private void ProgressOnlineTask(DateTimeOffset now)
    {
        if (!HasActiveTask(out var task) || task.Counter != "online_minutes") return;
        AdvanceTask("online_minutes", _state.TodayOnlineSeconds / 60);
    }

    private void SettleOffline(DateTimeOffset now, bool showNotice)
    {
        var last = _state.LastOnlineUtc;
        if (now < last.AddMinutes(-2))
        {
            MarkRollback(now);
            return;
        }

        var elapsed = now - last;
        var hours = Math.Clamp((int)Math.Floor(elapsed.TotalHours), 0, 100);
        if (hours <= 0)
        {
            EnsureDay(now, applyStarve: true);
            _state.LastOnlineUtc = now;
            SaveState();
            return;
        }

        var oldSatiety = _state.Satiety;
        _state.Mood = Math.Max(0, _state.Mood - hours);
        _state.Satiety = Math.Max(0, _state.Satiety - hours * 2);

        var today = GameState.DateIndex(now.LocalDateTime);
        var dayDiff = Math.Max(0, today - _state.LastDateIndex);
        var starveDays = 0;
        if (oldSatiety == 0) starveDays = Math.Min(4, dayDiff);
        else if (_state.Satiety == 0) starveDays = Math.Min(4, Math.Max(0, dayDiff - 1));
        if (starveDays > 0) LoseAffection(starveDays * 25);

        _state.LastOnlineUtc = now;
        EnsureDay(now, applyStarve: false);
        SaveState();
        if (showNotice && hours >= 1)
        {
            Toast?.Invoke($"离线 {hours} 小时，已按规则结算。");
        }
        Changed?.Invoke();
    }

    private void EnsureDay(DateTimeOffset now, bool applyStarve)
    {
        var today = GameState.DateIndex(now.LocalDateTime);
        if (today < _state.LastDateIndex)
        {
            MarkRollback(now);
            return;
        }
        if (today == _state.LastDateIndex)
        {
            EnsureTask(now);
            EnsureStagingDay(now);
            return;
        }

        if (_state.TodayOnlineSeconds >= 1800 && !_state.DayOnline30Counted)
        {
            _state.DayOnline30Counted = true;
            AddCounter("online_days", 1);
        }

        if (applyStarve && _state.Satiety == 0)
        {
            LoseAffection(25);
        }

        _state.TimeRollbackDetected = false;
        _state.TimeRollbackNotice = "";
        _state.LastDateIndex = today;
        _state.TodayOnlineSeconds = 0;
        _state.TodayInteractionCount = 0;
        _state.TodayMoodBonusCount = 0;
        _state.TodayEffectiveInteractions = 0;
        _state.TodayFeedCount = 0;
        _state.DayOnline30Counted = false;
        _state.DrawDateIndex = 0;
        _state.DrawResult = nameof(Fortune.None);
        _state.DrawAtUtc = null;
        _state.LotMessageDateIndex = 0;
        _state.LotMessage = "";
        _state.FishingDateIndex = today;
        _state.FishingUsed = 0;
        _state.CookingDateIndex = today;
        _state.CookingUsed = 0;
        _state.FishingSuccessToday = 0;
        _state.CookingSuccessToday = 0;
        _state.Fish5CountedToday = false;
        _state.Cook1CountedToday = false;
        EnsureStagingDay(now);
        EnsureSignWeek(now.LocalDateTime);
        RefreshTask(now, force: true);
        SaveState();
        Changed?.Invoke();
    }

    private void MarkRollback(DateTimeOffset now)
    {
        if (!_state.TimeRollbackDetected)
        {
            _state.TimeRollbackDetected = true;
            _state.TimeRollbackNotice = "检测到系统时间回拨：今日签到、抽签与任务刷新已忽略，请校准系统时间。";
            _state.LastOnlineUtc = now;
            SaveState();
            Toast?.Invoke(_state.TimeRollbackNotice);
            Changed?.Invoke();
        }
    }

    public bool CanUseDailyActions => !_state.TimeRollbackDetected;

    // ------------------------------------------------------------- stats

    public void Interact()
    {
        if (!_initialized) return;
        var now = DateTimeOffset.Now;
        EnsureDay(now, applyStarve: true);
        _state.TodayInteractionCount++;

        if (HasActiveTask(out var task) && task.Counter == "interaction_any")
        {
            AdvanceTask("interaction_any", _state.TodayInteractionCount);
        }

        var effective = (now - _state.LastInteractionUtc).TotalSeconds >= InteractionCooldownSeconds;
        if (effective)
        {
            _state.LastInteractionUtc = now;
            _state.TodayEffectiveInteractions++;
            AddCounter("interaction_total", 1);
            SetCounter("interaction_day_max", Math.Max(
                GetCounter("interaction_day_max"), _state.TodayEffectiveInteractions));

            if (_state.TodayMoodBonusCount < DailyMoodBonusLimit)
            {
                var before = _state.Mood;
                _state.Mood = Math.Min(100, _state.Mood + 20);
                _state.TodayMoodBonusCount++;
                if (before < 100 && _state.Mood == 100)
                {
                    _state.LastMoodFullDate = _state.LastDateIndex;
                    AddCounter("mood_full_total", 1);
                }
            }
            AdvanceTask("interaction_effective", _state.TodayEffectiveInteractions);
        }

        AdvanceThresholdTasks();
        SaveState();
        CheckAchievements();
        Changed?.Invoke();
    }

    public (bool Ok, string Message) Feed()
    {
        var now = DateTimeOffset.Now;
        EnsureDay(now, applyStarve: true);
        if (_state.Satiety >= 100) return (false, "已经吃饱了，食物没有消耗。");
        var index = _inventory.FindIndex(ItemCatalog.IsFood);
        if (index < 0) return (false, "背包里还没有食物。");
        return UseItem(index);
    }

    private void AdvanceThresholdTasks()
    {
        if (HasActiveTask(out var task))
        {
            if (task.Counter == "mood_value" && _state.Mood >= task.Target) AdvanceTask("mood_value", _state.Mood);
            else if (task.Counter == "satiety_value" && _state.Satiety >= task.Target) AdvanceTask("satiety_value", _state.Satiety);
        }
    }

    // --------------------------------------------------------- affection

    public void AddAffection(double amount)
    {
        if (amount <= 0) return;
        if (_state.Level >= MaxLevel)
        {
            _state.MaxLevelAffection += (int)Math.Floor(amount);
            CheckAchievements();
            return;
        }

        _state.AffectionCarry += amount;
        var whole = (int)Math.Floor(_state.AffectionCarry);
        _state.AffectionCarry -= whole;
        _state.Affection += whole;

        while (_state.Affection >= 100 && _state.Level < MaxLevel)
        {
            _state.Affection -= 100;
            _state.Level++;
            AddCounter("level", 1);
            UnlockNextLevelGif();
        }
        if (_state.Level > MinLevel) MoveStagingToInventory();

        if (_state.Level >= MaxLevel)
        {
            _state.MaxLevelAffection += _state.Affection;
            _state.Affection = 0;
            _state.AffectionCarry = 0;
        }
        CheckAchievements();
    }

    public void LoseAffection(int amount)
    {
        if (amount <= 0 || _state.Level >= MaxLevel) return;
        _state.Affection = Math.Max(0, _state.Affection - amount);
    }

    public void UnlockNextLevelGif()
    {
        var gifId = _catalog.NextLevelGif(_unlockedGifs);
        if (gifId is null) return;
        var entry = _catalog.Find(gifId);
        if (entry is null) return;
        _store.UnlockGif(entry.GifId, entry.UnlockType, entry.UnlockCondition, entry.SortIndex);
        _unlockedGifs.Add(entry.GifId);
        Toast?.Invoke($"解锁动态图：{entry.GifId}");
        CheckAchievements();
    }

    // --------------------------------------------------------------- gifs

    public bool IsGifUnlocked(string gifId) => _unlockedGifs.Contains(gifId);

    public IReadOnlyList<GifUnlockRow> LoadGifRows()
    {
        var rows = _store.LoadGifUnlocks();
        foreach (var row in rows)
        {
            if (_catalog.Find(row.GifId) is { } entry)
            {
                row.SortIndex = entry.SortIndex;
                row.UnlockType = entry.UnlockType;
                row.UnlockCondition = entry.UnlockCondition;
            }
        }
        return rows.OrderBy(row => row.SortIndex).ToList();
    }

    public (bool Ok, string Message) EnableGif(string gifId)
    {
        if (!_unlockedGifs.Contains(gifId)) return (false, "该动态图尚未解锁。");
        if (_enabledGifs.Contains(gifId)) return (false, "该动态图已经启用。");
        if (_enabledGifs.Count >= MaxEnabledGifs) return (false, $"最多同时启用 {MaxEnabledGifs} 个动态图");

        _store.SetGifEnabled(gifId, true);
        _enabledGifs.Add(gifId);
        if (_state.Avatar == AvatarKind.Gif &&
            !string.Equals(_state.CurrentGifId, gifId, StringComparison.OrdinalIgnoreCase))
        {
            SwitchGifInternal(gifId, count: false);
        }

        var row = _store.LoadGifUnlocks().FirstOrDefault(item => item.GifId == gifId);
        if (row is not null && !row.EverEnabled)
        {
            _store.MarkGifEverEnabled(gifId);
            AddCounter("gif_use_total", 1);
        }
        AdvanceTask("gif_enable", 1);
        SaveState();
        CheckAchievements();
        Changed?.Invoke();
        return (true, "已加入主形象切换列表。");
    }

    public (bool Ok, string Message) DisableGif(string gifId)
    {
        if (!_enabledGifs.Contains(gifId)) return (false, "该动态图未启用。");
        _store.SetGifEnabled(gifId, false);
        _enabledGifs.Remove(gifId);

        if (string.Equals(_state.CurrentGifId, gifId, StringComparison.OrdinalIgnoreCase) &&
            _state.Avatar == AvatarKind.Gif)
        {
            var next = _enabledGifs.OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (next is not null)
            {
                _state.CurrentGifId = next;
                _state.AvatarType = nameof(AvatarKind.Gif);
                _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
            }
            else
            {
                _state.AvatarType = nameof(AvatarKind.DefaultStatic);
                _state.CurrentGifId = null;
                _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
            }
            AvatarChanged?.Invoke();
        }

        SaveState();
        Changed?.Invoke();
        return (true, "已从主形象切换列表移除。");
    }

    public (bool Ok, string Message) SwitchGif(string gifId)
    {
        if (!_enabledGifs.Contains(gifId)) return (false, "请先在背包中启用该动态图。");
        if (_state.Avatar == AvatarKind.Gif &&
            string.Equals(_state.CurrentGifId, gifId, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "当前已经是这个形象。");
        }
        SwitchGifInternal(gifId, count: true);
        AdvanceTask("gif_switch", 1);
        SaveState();
        CheckAchievements();
        Changed?.Invoke();
        return (true, "主形象已切换。");
    }

    private void SwitchGifInternal(string gifId, bool count)
    {
        if (count) AddCounter("gif_switch_total", 1);
        _state.AvatarType = nameof(AvatarKind.Gif);
        _state.CurrentGifId = gifId;
        _state.CurrentCustomImagePath = null;
        _state.CurrentCustomImageData = null;
        _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
        AvatarChanged?.Invoke();
    }

    public void SetCustomAvatar(string path)
    {
        _state.AvatarType = nameof(AvatarKind.CustomImage);
        _state.CurrentCustomImagePath = path;
        _state.CurrentCustomImageData = null;
        _state.CurrentGifId = null;
        _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
        SaveState();
        AvatarChanged?.Invoke();
        Changed?.Invoke();
    }

    public void ResetAvatarToDefault()
    {
        _state.AvatarType = nameof(AvatarKind.DefaultStatic);
        _state.CurrentGifId = null;
        _state.CurrentCustomImagePath = null;
        _state.CurrentCustomImageData = null;
        _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
        SaveState();
        AvatarChanged?.Invoke();
        Changed?.Invoke();
    }

    public (AvatarKind Kind, string? GifId, string? CustomPath) EffectiveAvatar()
    {
        if (_state.Avatar == AvatarKind.Live2D)
        {
            var modelId = string.IsNullOrWhiteSpace(_state.CurrentLive2DModelId)
                ? "seethrough_output"
                : _state.CurrentLive2DModelId;
            var modelPath = Path.Combine(AppContext.BaseDirectory, "Live2DWeb", "models", modelId,
                modelId + ".model3.json");
            if (File.Exists(modelPath)) return (AvatarKind.Live2D, null, null);
            return (AvatarKind.DefaultStatic, null, null);
        }

        if (_state.Avatar == AvatarKind.Gif &&
            !string.IsNullOrWhiteSpace(_state.CurrentGifId) &&
            _enabledGifs.Contains(_state.CurrentGifId) &&
            _unlockedGifs.Contains(_state.CurrentGifId))
        {
            return (AvatarKind.Gif, _state.CurrentGifId, null);
        }

        if (_state.Avatar == AvatarKind.CustomImage)
        {
            if (!string.IsNullOrWhiteSpace(_state.CurrentCustomImagePath) &&
                File.Exists(_state.CurrentCustomImagePath))
            {
                return (AvatarKind.CustomImage, null, _state.CurrentCustomImagePath);
            }
        }

        return (AvatarKind.DefaultStatic, null, null);
    }

    public (bool Ok, string Message) SelectLive2D(string modelId)
    {
        var id = string.IsNullOrWhiteSpace(modelId) ? "seethrough_output" : modelId.Trim();
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Live2DWeb", "models", id, id + ".model3.json");
        if (!File.Exists(modelPath))
        {
            FallbackToDefault("Live2D 模型缺失，已回退到默认形象");
            return (false, "Live2D 模型缺失，已回退到默认形象");
        }
        _state.AvatarType = nameof(AvatarKind.Live2D);
        _state.CurrentLive2DModelId = id;
        _state.CurrentGifId = null;
        _state.CurrentCustomImagePath = null;
        _state.CurrentCustomImageData = null;
        _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
        SaveState();
        AvatarChanged?.Invoke();
        Changed?.Invoke();
        return (true, "已切换 Live2D 形象。");
    }

    public void FallbackToDefault(string message)
    {
        AvatarFallbackMessage = message;
        _state.AvatarType = nameof(AvatarKind.DefaultStatic);
        _state.CurrentGifId = null;
        _state.CurrentCustomImagePath = null;
        _state.CurrentCustomImageData = null;
        _state.CurrentAvatarUpdatedAt = DateTimeOffset.Now;
        SaveState();
        Toast?.Invoke(message);
        AvatarChanged?.Invoke();
        Changed?.Invoke();
    }

    private void ValidateAvatar()
    {
        if (_state.Avatar == AvatarKind.Live2D)
        {
            var id = string.IsNullOrWhiteSpace(_state.CurrentLive2DModelId)
                ? "seethrough_output"
                : _state.CurrentLive2DModelId;
            var path = Path.Combine(AppContext.BaseDirectory, "Live2DWeb", "models", id, id + ".model3.json");
            if (!File.Exists(path)) FallbackToDefault("Live2D 模型缺失，已回退到默认形象");
        }
        if (_state.Avatar == AvatarKind.Gif &&
            (string.IsNullOrWhiteSpace(_state.CurrentGifId) ||
             !_enabledGifs.Contains(_state.CurrentGifId) ||
             !_unlockedGifs.Contains(_state.CurrentGifId)))
        {
            _state.AvatarType = nameof(AvatarKind.DefaultStatic);
            _state.CurrentGifId = null;
        }
    }

    // --------------------------------------------------------------- draw

    public Fortune TodayFortune =>
        _state.DrawDateIndex == _state.LastDateIndex && _state.FortuneToday != Fortune.None
            ? _state.FortuneToday
            : Fortune.None;

    public int DrawsRemaining => TodayFortune == Fortune.None ? 1 : 0;

    public string TodayLotMessage =>
        _state.LotMessageDateIndex == _state.LastDateIndex ? _state.LotMessage : "";

    /// <summary>Picks one decorative line per day; it never affects game values.</summary>
    public string EnsureDailyLotMessage()
    {
        EnsureDay(DateTimeOffset.Now, applyStarve: true);
        if (_state.LotMessageDateIndex == _state.LastDateIndex &&
            !string.IsNullOrWhiteSpace(_state.LotMessage))
        {
            return _state.LotMessage;
        }
        _state.LotMessageDateIndex = _state.LastDateIndex;
        _state.LotMessage = LotMessages.Pick();
        SaveState();
        Changed?.Invoke();
        return _state.LotMessage;
    }

    public (bool Ok, Fortune Result, string Message) DrawFortune()
    {
        EnsureDay(DateTimeOffset.Now, applyStarve: true);
        if (!CanUseDailyActions) return (false, Fortune.None, _state.TimeRollbackNotice);
        if (TodayFortune != Fortune.None) return (false, TodayFortune, "今天已经抽过签了。");
        EnsureDailyLotMessage();

        var roll = Random.Shared.Next(100);
        var result = roll < 10 ? Fortune.Up : roll < 90 ? Fortune.Middle : Fortune.Down;
        _state.DrawDateIndex = _state.LastDateIndex;
        _state.DrawResult = result.ToString();
        _state.DrawAtUtc = DateTimeOffset.Now;
        AddCounter("draw_total", 1);
        if (result == Fortune.Up) AddCounter("draw_up_total", 1);
        if (result == Fortune.Down) AddCounter("draw_down_total", 1);
        AdvanceTask("draw", 1);
        SaveState();
        CheckAchievements();
        Changed?.Invoke();

        var message = result switch
        {
            Fortune.Up => "上签：今日任务奖励翻倍（随机食物 ×2、好感 +2）。",
            Fortune.Middle => "中签：今日任务奖励保持原样。",
            _ => "下签：今日任务只给食物，不再增加好感。",
        };
        return (true, result, message);
    }

    public void MarkFortuneViewed()
    {
        AdvanceTask("draw_view", 1);
        SaveState();
        Changed?.Invoke();
    }

    public static string FortuneName(Fortune fortune) => fortune switch
    {
        Fortune.Up => "上签",
        Fortune.Middle => "中签",
        Fortune.Down => "下签",
        _ => "未抽签",
    };

    // ------------------------------------------------------------- sign-in

    public bool IsSignedToday => _state.LastSignDateIndex == _state.LastDateIndex;

    public int WeeklySignedCount => System.Numerics.BitOperations.PopCount((uint)_state.WeeklySignMask);

    public IReadOnlySet<int> SignInDates() => _store.LoadSignInDates();

    public void EnsureSignWeek(DateTime localNow)
    {
        var week = GameState.WeekStartIndex(localNow);
        if (_state.SignWeekStart == week) return;
        _state.SignWeekStart = week;
        _state.WeeklySignMask = 0;
        _state.Milestone3Claimed = false;
        _state.Milestone7Claimed = false;
    }

    public (bool Ok, string Message) SignIn()
    {
        var now = DateTimeOffset.Now;
        EnsureDay(now, applyStarve: true);
        if (!CanUseDailyActions) return (false, _state.TimeRollbackNotice);
        EnsureSignWeek(now.LocalDateTime);
        if (IsSignedToday) return (false, "今天已经签到过了。");

        var today = _state.LastDateIndex;
        var monday = _state.SignWeekStart;
        var dayIndex = Math.Clamp(today - monday, 0, 6);
        var bit = 1 << dayIndex;
        if ((_state.WeeklySignMask & bit) != 0) return (false, "本周这一格已经点亮。");

        _state.WeeklySignMask |= bit;
        var previousSignDate = _state.LastSignDateIndex;
        _state.LastSignDateIndex = today;
        _state.TotalSignInDays++;
        _state.ContinuousSignIn = previousSignDate == today - 1
            ? _state.ContinuousSignIn + 1
            : 1;
        _store.AddSignIn(today);
        SetCounter("signin_total", _state.TotalSignInDays);
        SetCounter("signin_streak_max", Math.Max(GetCounter("signin_streak_max"), _state.ContinuousSignIn));
        AdvanceTask("signin", 1);

        var message = "签到成功，今日奖励寿司 ×1。";
        AddItem(ItemCatalog.Sushi);
        if (WeeklySignedCount >= 3 && !_state.Milestone3Claimed)
        {
            _state.Milestone3Claimed = true;
            AddItem(ItemCatalog.Sushi);
            message += " 周签到 3 天奖励：寿司 ×1。";
        }
        if (WeeklySignedCount >= 7 && !_state.Milestone7Claimed)
        {
            _state.Milestone7Claimed = true;
            AddAffection(1);
            message += " 周签到 7 天奖励：好感 +1。";
        }

        SaveState();
        CheckAchievements();
        Changed?.Invoke();
        return (true, message);
    }

    public void MarkWeeklyViewed()
    {
        AdvanceTask("week_view", 1);
        SaveState();
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- tasks

    public TaskDef? CurrentTask => TaskCatalog.Find(_state.TaskId);

    public bool TaskAccepted => _state.TaskAcceptedUtc is not null;

    public bool TaskCompleted => _state.TaskCompletedUtc is not null;

    public string TaskStatusText
    {
        get
        {
            if (_state.TaskId is null) return "今日任务准备中";
            if (TaskCompleted) return "已完成";
            return TaskAccepted ? "进行中" : "待接取";
        }
    }

    public void EnsureTask(DateTimeOffset now)
    {
        if (_state.TaskDateIndex != _state.LastDateIndex || _state.TaskId is null)
        {
            RefreshTask(now, force: true);
        }
    }

    public void RefreshTask(DateTimeOffset now, bool force)
    {
        if (!force && _state.TaskDateIndex == _state.LastDateIndex && _state.TaskId is not null) return;
        var candidates = TaskCatalog.All.Where(task => IsTaskEligible(task.Id)).ToList();
        if (candidates.Count == 0) candidates.Add(TaskCatalog.All[0]);
        var picked = candidates[Random.Shared.Next(candidates.Count)];

        _state.TaskDateIndex = _state.LastDateIndex;
        _state.TaskId = picked.Id;
        _state.TaskAcceptedUtc = null;
        _state.TaskProgress = 0;
        _state.TaskAux = 0;
        _state.TaskCompletedUtc = null;
        _state.TaskRewardClaimed = false;
        SaveState();
    }

    private bool IsTaskEligible(string id)
    {
        return id switch
        {
            "T06" => _state.Food >= 1,
            "T07" => _state.Food >= 2,
            "T08" => _state.Food >= 1,
            "T09" => _state.Satiety < 50 && _state.Food >= 1,
            "T25" or "T26" => _state.Ingredients > 0,
            "T27" => _unlockedGifs.Count >= 1,
            "T28" => _unlockedGifs.Count >= 2,
            _ => true,
        };
    }

    public (bool Ok, string Message) AcceptTask()
    {
        var now = DateTimeOffset.Now;
        EnsureDay(now, applyStarve: true);
        if (!CanUseDailyActions) return (false, _state.TimeRollbackNotice);
        EnsureTask(now);
        if (_state.TaskId is null) return (false, "今天还没有可接取的任务。");
        if (TaskAccepted) return (false, "任务已经接取了。");
        var task = CurrentTask;
        if (task is null) return (false, "任务数据异常。");

        _state.TaskAcceptedUtc = now;
        _state.TaskProgress = task.Counter switch
        {
            "mood_value" => Math.Max(0, stateValueFor(task)),
            "satiety_value" => Math.Max(0, stateValueFor(task)),
            _ => 0,
        };
        AddCounter("task_accepted", 1);
        var message = "任务已接取：" + task.Name;

        if (_state.TaskProgress >= task.Target)
        {
            CompleteTask(task, now, out var reward);
            message = "任务已接取并立即完成：" + reward;
        }
        else
        {
            SaveState();
        }
        CheckAchievements();
        Changed?.Invoke();
        return (true, message);
    }

    private int stateValueFor(TaskDef task) => task.Counter switch
    {
        "mood_value" => _state.Mood,
        "satiety_value" => _state.Satiety,
        _ => 0,
    };

    private bool HasActiveTask(out TaskDef task)
    {
        task = CurrentTask!;
        return task is not null && TaskAccepted && !TaskCompleted && _state.TaskDateIndex == _state.LastDateIndex;
    }

    public void AdvanceTask(string eventKey, int value)
    {
        if (!HasActiveTask(out var task)) return;
        if (task.Id == "T29")
        {
            if (eventKey == "draw") _state.TaskAux |= 1;
            else if (eventKey == "draw_view") _state.TaskAux |= 2;
            else return;
            _state.TaskProgress = _state.TaskAux == 3 ? 1 : 0;
            if (_state.TaskProgress >= task.Target) CompleteTask(task, DateTimeOffset.Now, out _);
            else { SaveState(); Changed?.Invoke(); }
            return;
        }
        if (task.Id == "T30")
        {
            if (eventKey == "signin") _state.TaskAux |= 1;
            else if (eventKey == "week_view") _state.TaskAux |= 2;
            else return;
            _state.TaskProgress = _state.TaskAux == 3 ? 1 : 0;
            if (_state.TaskProgress >= task.Target) CompleteTask(task, DateTimeOffset.Now, out _);
            else { SaveState(); Changed?.Invoke(); }
            return;
        }

        if (!string.Equals(task.Counter, eventKey, StringComparison.OrdinalIgnoreCase)) return;

        switch (task.Counter)
        {
            case "mood_value":
            case "satiety_value":
                _state.TaskProgress = Math.Max(_state.TaskProgress, value);
                break;
            case "online_minutes":
                _state.TaskProgress = task.Id == "T13"
                    ? (int)Math.Max(0, (DateTimeOffset.Now - _sessionStart).TotalMinutes)
                    : value;
                break;
            case "interaction_any":
            case "interaction_effective":
            case "feed":
            case "fishing_success":
                _state.TaskProgress++;
                break;
            default:
                _state.TaskProgress++;
                break;
        }

        if (_state.TaskProgress >= task.Target)
        {
            CompleteTask(task, DateTimeOffset.Now, out var reward);
            Toast?.Invoke("任务完成：" + task.Name + "，" + reward);
        }
        else
        {
            SaveState();
            Changed?.Invoke();
        }
    }

    private void CompleteTask(TaskDef task, DateTimeOffset now, out string rewardText)
    {
        if (_state.TaskCompletedUtc is not null)
        {
            rewardText = "奖励已领取";
            return;
        }
        if (_state.TaskRewardClaimed)
        {
            rewardText = "奖励已领取";
            return;
        }

        _state.TaskCompletedUtc = now;
        _state.TaskRewardClaimed = true;
        var fortune = TaskFortuneAt(now);
        var factor = _state.Mood == 0 ? 0.0 : _state.Mood < 50 ? 0.5 : 1.0;
        var baseAffection = fortune switch
        {
            Fortune.Up => 2,
            Fortune.Down => 0,
            _ => 1,
        };
        var affection = baseAffection * factor;
        var foodCount = fortune == Fortune.Up ? 2 : 1;
        var foodId = Random.Shared.Next(2) == 0 ? ItemCatalog.Sushi : ItemCatalog.FishMeat;
        var foodName = ItemCatalog.NameOf(foodId);

        if (affection > 0) AddAffection(affection);
        for (var i = 0; i < foodCount; i++) AddItem(foodId);
        AddCounter("task_completed", 1);
        AddCounter("task_food", foodCount);
        AddCounter("task_affection", (long)Math.Floor(affection));

        rewardText = $"好感 +{affection:0.#}、{foodName} ×{foodCount}";
        if (fortune == Fortune.Down) rewardText = $"{foodName} ×{foodCount}（下签不加好感）";
        if (fortune == Fortune.Up) rewardText = $"上签翻倍：好感 +{affection:0.#}、{foodName} ×{foodCount}";
        SaveState();
        CheckAchievements();
        Changed?.Invoke();
    }

    private Fortune TaskFortuneAt(DateTimeOffset completedAt)
    {
        if (_state.DrawDateIndex != _state.LastDateIndex || _state.DrawAtUtc is null) return Fortune.None;
        if (completedAt <= _state.DrawAtUtc.Value) return Fortune.None;
        return _state.FortuneToday;
    }

    public void MarkPanelViewed()
    {
        AdvanceTask("panel_view", 1);
    }

    public void MarkInventoryViewed()
    {
        AdvanceTask("inventory_view", 1);
    }

    public void MarkAchievementsViewed()
    {
        AdvanceTask("achievement_view", 1);
    }

    // ------------------------------------------------------------ minigames

    public int FishingRemaining
    {
        get
        {
            if (_state.FishingDateIndex != _state.LastDateIndex) return 5;
            return Math.Max(0, 5 - _state.FishingUsed);
        }
    }

    public int CookingRemaining
    {
        get
        {
            if (_state.CookingDateIndex != _state.LastDateIndex) return 3;
            return Math.Max(0, 3 - _state.CookingUsed);
        }
    }

    public void MarkFishingEntered() => AdvanceTask("fishing_enter", 1);

    public bool TrySpendFishing()
    {
        EnsureDay(DateTimeOffset.Now, applyStarve: true);
        if (!CanUseDailyActions) return false;
        if (FishingRemaining <= 0) return false;
        _state.FishingDateIndex = _state.LastDateIndex;
        _state.FishingUsed++;
        AdvanceTask("fishing_cast", 1);
        SaveState();
        Changed?.Invoke();
        return true;
    }

    public void FinishFishing(bool success, string difficulty)
    {
        if (!success) return;
        _state.Fish++;
        AddItem(ItemCatalog.Fish);
        _state.FishingSuccessToday++;
        AddCounter("fish_" + difficulty, 1);
        AdvanceTask("fishing_success", 1);
        if (_state.FishingSuccessToday >= 5 && !_state.Fish5CountedToday)
        {
            _state.Fish5CountedToday = true;
            AddCounter("fish5_days", 1);
        }
        SaveState();
        CheckAchievements();
        Changed?.Invoke();
    }

    public void MarkCookingEntered() => AdvanceTask("cooking_enter", 1);

    public bool TryStartCooking()
    {
        EnsureDay(DateTimeOffset.Now, applyStarve: true);
        if (!CanUseDailyActions) return false;
        if (CookingRemaining <= 0) return false;
        if (!ConsumeIngredient()) return false;
        _state.CookingDateIndex = _state.LastDateIndex;
        _state.CookingUsed++;
        PersistItems();
        Changed?.Invoke();
        return true;
    }

    public void FinishCooking(bool success, string difficulty)
    {
        if (!success) return;
        AddItem(ItemCatalog.FishMeat);
        _state.CookingSuccessToday++;
        AddCounter("cook_" + difficulty, 1);
        AdvanceTask("cooking_success", 1);
        if (_state.CookingSuccessToday >= 1 && !_state.Cook1CountedToday)
        {
            _state.Cook1CountedToday = true;
            AddCounter("cook1_days", 1);
        }
        SaveState();
        CheckAchievements();
        Changed?.Invoke();
    }

    // --------------------------------------------------------- achievements

    public bool IsAchievementUnlocked(string id) => _achievements.Contains(id);

    public IReadOnlyList<(AchievementDef Def, bool Unlocked, DateTimeOffset? At, bool Visible)> AchievementWall()
    {
        var result = new List<(AchievementDef, bool, DateTimeOffset?, bool)>();
        var unlockedAt = _store.LoadAchievementTimes();

        foreach (var def in AchievementCatalog.All)
        {
            var unlocked = _achievements.Contains(def.Id);
            unlockedAt.TryGetValue(def.Id, out var at);
            result.Add((def, unlocked, at, !def.Hidden || unlocked));
        }
        return result;
    }

    public void CheckAchievements()
    {
        foreach (var def in AchievementCatalog.All)
        {
            if (_achievements.Contains(def.Id)) continue;
            long value = def.Counter switch
            {
                "level" => _state.Level,
                "companion_seconds" => _state.TotalCompanionSeconds,
                "max_level_affection" => _state.MaxLevelAffection,
                _ => GetCounter(def.Counter),
            };
            if (value < def.Target) continue;

            if (!_store.UnlockAchievement(def.Id)) continue;
            _achievements.Add(def.Id);
            var gifId = _catalog.AchievementGif(def.Id);
            if (gifId is not null)
            {
                var entry = _catalog.Find(gifId);
                if (entry is not null)
                {
                    _store.UnlockGif(entry.GifId, entry.UnlockType, entry.UnlockCondition, entry.SortIndex);
                    _unlockedGifs.Add(entry.GifId);
                }
            }
            Toast?.Invoke($"达成成就「{def.Name}」，解锁动态图 {gifId}。");
        }
    }

    public long GetCounter(string key) => _counters.TryGetValue(key, out var value) ? value : 0;

    private void AddCounter(string key, long delta)
    {
        _counters[key] = GetCounter(key) + delta;
        _store.AddCounter(key, delta);
        if (key == "companion_seconds") return;
        CheckAchievements();
    }

    private void SetCounter(string key, long value)
    {
        _counters[key] = value;
        _store.SetCounter(key, value);
    }

    // -------------------------------------------------------------- misc

    public AppStateSnapshot CreateSnapshot()
    {
        return new AppStateSnapshot
        {
            State = _state.Clone(),
            Gifs = _store.LoadGifUnlocks(),
            Achievements = _store.LoadAchievementTimes(),
            Counters = _counters.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            SignInDates = _store.LoadSignInDates().OrderBy(value => value).ToList(),
            Inventory = _inventory.ToList(),
            Staging = _staging.ToList(),
            StagingDayIndex = _stagingDayIndex,
            DefaultAvatars = _defaultAvatars.Select(avatar => new DefaultAvatar
            {
                Id = avatar.Id,
                Kind = avatar.Kind,
                Data = avatar.Data,
                Enabled = avatar.Enabled,
                SortIndex = avatar.SortIndex,
            }).ToList(),
        };
    }

    /// <summary>Applies a migration import, resetting all time/daily state.</summary>
    public void ApplyImportedSnapshot(AppStateSnapshot snapshot)
    {
        var now = DateTimeOffset.Now;
        var today = GameState.DateIndex(now.LocalDateTime);
        var state = snapshot.State;
        state.LastOnlineUtc = now;
        state.LastDateIndex = today;
        state.SignWeekStart = GameState.WeekStartIndex(now.LocalDateTime);
        state.WeeklySignMask = 0;
        state.Milestone3Claimed = false;
        state.Milestone7Claimed = false;
        var signs = snapshot.SignInDates.ToHashSet();
        state.LastSignDateIndex = signs.Contains(today - 1) ? today - 1 : 0;
        state.TodayOnlineSeconds = 0;
        state.OnlineCarrySeconds = 0;
        state.TodayInteractionCount = 0;
        state.TodayMoodBonusCount = 0;
        state.TodayEffectiveInteractions = 0;
        state.TodayFeedCount = 0;
        state.DayOnline30Counted = false;
        state.DrawDateIndex = 0;
        state.DrawResult = nameof(Fortune.None);
        state.DrawAtUtc = null;
        state.LotMessageDateIndex = 0;
        state.LotMessage = "";
        state.FishingDateIndex = today;
        state.FishingUsed = 0;
        state.CookingDateIndex = today;
        state.CookingUsed = 0;
        state.FishingSuccessToday = 0;
        state.CookingSuccessToday = 0;
        state.Fish5CountedToday = false;
        state.Cook1CountedToday = false;
        state.TaskDateIndex = today;
        state.TaskId = null;
        state.TaskAcceptedUtc = null;
        state.TaskProgress = 0;
        state.TaskAux = 0;
        state.TaskCompletedUtc = null;
        state.TaskRewardClaimed = false;
        state.TimeRollbackDetected = false;
        state.TimeRollbackNotice = "";

        var capacity = BaseInventorySlots + Math.Max(0, state.Level - 1);
        snapshot.Inventory = snapshot.Inventory
            .Where(ItemCatalog.IsKnown)
            .Take(capacity)
            .ToList();
        snapshot.Staging = snapshot.Staging
            .Where(ItemCatalog.IsKnown)
            .Take(MaxStaging)
            .ToList();
        snapshot.StagingDayIndex = today;
        _inventory.Clear();
        _inventory.AddRange(snapshot.Inventory);
        _staging.Clear();
        _staging.AddRange(snapshot.Staging);
        _stagingDayIndex = today;
        SyncItemMirrors();

        var builtIn = _defaultAvatars.FirstOrDefault(avatar => avatar.IsBuiltIn) ?? new DefaultAvatar
        {
            Id = "builtin-default",
            Kind = "Builtin",
            Enabled = true,
            Data = ReadBuiltInDefaultAvatarData(),
        };
        builtIn.Enabled = true;
        snapshot.DefaultAvatars = new[] { builtIn }
            .Concat((snapshot.DefaultAvatars ?? new List<DefaultAvatar>())
                .Where(avatar => !avatar.IsBuiltIn && avatar.Kind == "Custom")
                .Take(5))
            .ToList();
        _defaultAvatars.Clear();
        _defaultAvatars.AddRange(snapshot.DefaultAvatars);

        _store.ReplaceAll(snapshot);
        Reload();
        var lifecycle = _store.LoadLifecycle();
        lifecycle.UserDataKeyCheck = MigrationService.UserDataKeyCheck();
        _store.SaveLifecycle(lifecycle);
        EnsureTask(now);
        SaveState();
        Changed?.Invoke();
    }

    public void Reload()
    {
        _state = _store.LoadState();
        _counters.Clear();
        foreach (var pair in _store.LoadCounters()) _counters[pair.Key] = pair.Value;
        _achievements.Clear();
        foreach (var id in _store.LoadAchievements()) _achievements.Add(id);
        _unlockedGifs.Clear();
        _enabledGifs.Clear();
        foreach (var gif in _store.LoadGifUnlocks())
        {
            if (gif.IsUnlocked) _unlockedGifs.Add(gif.GifId);
            if (gif.IsEnabled && gif.IsUnlocked) _enabledGifs.Add(gif.GifId);
        }
        _inventory.Clear();
        _inventory.AddRange(_store.LoadInventory());
        _staging.Clear();
        _staging.AddRange(_store.LoadStaging());
        _stagingDayIndex = _store.LoadStagingDay();
        EnsureStagingDay(DateTimeOffset.Now);
        _defaultAvatars.Clear();
        _defaultAvatars.AddRange(_store.LoadDefaultAvatars());
        EnsureBuiltInDefaultAvatar();
        SyncItemMirrors();
        ValidateAvatar();
        SaveState();
        CheckAchievements();
        Changed?.Invoke();
        AvatarChanged?.Invoke();
    }

    public void Save()
    {
        _state.LastOnlineUtc = DateTimeOffset.Now;
        _store.SaveState(_state);
        _store.Backup();
    }

    private void SaveState()
    {
        _state.LastOnlineUtc = DateTimeOffset.Now;
        _store.SaveState(_state);
        _lastPersist = DateTimeOffset.Now;
    }

    public string CompanionText
    {
        get
        {
            var total = TimeSpan.FromSeconds(_state.TotalCompanionSeconds);
            return total.TotalHours >= 1
                ? $"{(int)total.TotalHours} 小时 {total.Minutes} 分钟"
                : $"{total.Minutes} 分钟";
        }
    }

    public string AffectionDisplay => _state.Level >= MaxLevel
        ? $"MAX + {_state.MaxLevelAffection}"
        : $"{_state.Affection}/100";

    public double AffectionProgress => _state.Level >= MaxLevel ? 1 : _state.Affection / 100.0;

    public int OfflineHoursLimit => 100;
}
