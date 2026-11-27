using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using WhalePet;
using WhalePet.Game;

var failures = 0;
void Check(bool condition, string name)
{
    Console.WriteLine((condition ? "PASS  " : "FAIL  ") + name);
    if (!condition) failures++;
}

var root = Path.Combine(Path.GetTempPath(), "whalepet-game-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var catalog = new GifCatalog();
    Check(catalog.LevelGifCount == 51 && catalog.AchievementGifCount == 53,
        "资源映射：等级 51 + 成就 53 = 104 个解锁 GIF");
    Check(catalog.Find(GifCatalog.FishingGifId) is { UnlockType: "Special" } &&
          catalog.Find(GifCatalog.CookingGifId) is { UnlockType: "Special" },
        "钓鱼/烹饪动画作为特殊资源打包");

    var store = new GameStore(root);
    var engine = new GameEngine(store, catalog);
    engine.Initialize();

    Check(engine.State.Level == 1 && engine.State.Affection == 0, "初始等级 1 / 好感 0");
    Check(engine.State.Mood == 100 && engine.State.Satiety == 100, "初始心情/饱食 100");
    Check(engine.State.Food == 0 && engine.State.Ingredients == 0 && engine.State.Fish == 0, "初始食物/食材 0");
    Check(engine.Inventory.Count == 0 && engine.InventoryCapacity == 10, "初始背包 0 件 / 10 格");
    Check(engine.Staging.Count == 0, "初始暂存区为空");
    Check(engine.UnlockedGifs.Count == 0, "初始 GifUnlock 为空");
    Check(engine.EffectiveAvatar().Kind == AvatarKind.DefaultStatic, "初始主形象为内置默认静态图");

    // 冷却外有效互动 5 次，每日心情加成上限 5 次。
    engine.State.Mood = 40;
    for (var i = 0; i < 5; i++)
    {
        engine.State.LastInteractionUtc = DateTimeOffset.Now.AddSeconds(-31);
        engine.Interact();
    }
    Check(engine.State.Mood == 100, "互动每次 +20，封顶 100");
    Check(engine.GetCounter("interaction_total") == 5, "有效互动计数 5");
    Check(engine.GetCounter("mood_full_total") == 1, "心情从 <100 到 100 计 1 次");

    // 投喂：寿司 +40，鱼肉 +20；食物满时不消耗。
    engine.AddItem(ItemCatalog.Sushi);
    engine.AddItem(ItemCatalog.Sushi);
    engine.State.Satiety = 10;
    var feed = engine.Feed();
    Check(feed.Ok && engine.State.Satiety == 50 && engine.State.Food == 1, "寿司投喂 +40 饱食并扣 1 个");
    engine.State.Satiety = 100;
    var fullFeed = engine.Feed();
    Check(!fullFeed.Ok && engine.State.Food == 1, "饱食满不消耗食物并提示");

    engine.AddItem(ItemCatalog.FishMeat);
    engine.State.Satiety = 10;
    var meatIndex = engine.Inventory.ToList().FindIndex(id => id == ItemCatalog.FishMeat);
    var meatFeed = engine.UseItem(meatIndex);
    Check(meatFeed.Ok && engine.State.Satiety == 30, "鱼肉投喂 +20 饱食");

    // 食材点击提示先烹饪；背包容量 10 格，满后进暂存区。
    engine.AddItem(ItemCatalog.Fish);
    var ingredientUse = engine.UseItem(engine.Inventory.Count - 1);
    Check(!ingredientUse.Ok && ingredientUse.Message.Contains("烹饪"), "食材点击提示需要烹饪");
    while (engine.Inventory.Count < engine.InventoryCapacity) engine.AddItem(ItemCatalog.Fish);
    engine.AddItem(ItemCatalog.Fish);
    Check(engine.Inventory.Count == 10 && engine.Staging.Count == 1, "背包满后物品进入暂存区");

    // 好感升级与 gif 解锁：250 -> 升 2 级并保留 50。
    engine.AddAffection(250);
    Check(engine.State.Level == 3 && engine.State.Affection == 50, "好感 250 循环升 2 级，保留 50");
    Check(engine.InventoryCapacity == 12 && engine.Staging.Count == 0,
        "升级增加背包格并自动移入暂存区物品");
    Check(engine.UnlockedGifs.Count == 2, "等级 2-3 解锁 2 个 GIF");
    Check(engine.UnlockedGifs.Contains("001_hehe.gif") && engine.UnlockedGifs.Contains("002_haha.gif"),
        "按文件名顺序解锁 001/002");

    // 抽签每日一次。
    var draw1 = engine.DrawFortune();
    var draw2 = engine.DrawFortune();
    Check(draw1.Ok && !draw2.Ok, "抽签每日仅 1 次");
    Check(engine.TodayFortune == draw1.Result, "抽签结果当日生效");

    // 签到。
    var sign1 = engine.SignIn();
    var sign2 = engine.SignIn();
    Check(sign1.Ok && !sign2.Ok, "每日签到仅 1 次");
    Check(engine.WeeklySignedCount == 1 && engine.State.TotalSignInDays == 1, "周签到点亮 1 格，累计 1 天");

    // 任务接取。
    var accepted = engine.AcceptTask();
    Check(accepted.Ok, "每日任务可接取");
    Check(engine.GetCounter("task_accepted") == 1, "接取任务计数 1");

    // 动态图启用/切换。
    var firstGif = engine.UnlockedGifs.OrderBy(id => id).First();
    var enable = engine.EnableGif(firstGif);
    Check(enable.Ok && engine.EnabledGifs.Contains(firstGif), "启用动态图加入切换列表");
    var switchResult = engine.SwitchGif(firstGif);
    Check(switchResult.Ok && engine.State.Avatar == AvatarKind.Gif && engine.State.CurrentGifId == firstGif,
        "切换主形象为动态图");
    Check(engine.GetCounter("gif_switch_total") == 1, "切换动态图计数 1");
    var repeat = engine.SwitchGif(firstGif);
    Check(!repeat.Ok && engine.GetCounter("gif_switch_total") == 1, "重复切换到当前项不计数");

    // 动态图解密。
    var provider = new GifProvider(catalog, engine.IsGifUnlocked);
    var stream = provider.GetDecryptedStream(firstGif);
    using (stream)
    {
        var header = new byte[6];
        stream!.ReadExactly(header);
        Check(Encoding.ASCII.GetString(header) is "GIF89a" or "GIF87a", "已解锁 GIF 可在内存解密");
    }
    Check(provider.GetDecryptedStream("104_deepsleep.gif") is null, "未解锁 GIF 不进入解密分支");
    using (var special = provider.GetAssetStream(GifCatalog.FishingGifId))
    {
        var header = new byte[6];
        special!.ReadExactly(header);
        Check(Encoding.ASCII.GetString(header) is "GIF89a" or "GIF87a", "钓鱼动画可在内存解密");
    }
    Check(LotMessages.All.Count >= 100, "签语池已加载");
    Check(ItemCatalog.FishDef.Satiety == 0 && ItemCatalog.FishMeatDef.Satiety == 20 &&
          ItemCatalog.SushiDef.Satiety == 40, "食物数值：鱼肉 +20 / 寿司 +40");
    Check(ItemCatalog.LoadIcon(ItemCatalog.Fish) is not null, "食材图标可从程序集读取");

    engine.Save();
    store.Dispose();

    // 离线结算封顶 100 小时。
    var store2 = new GameStore(root);
    var state = store2.LoadState();
    state.Level = 3;
    state.Affection = 50;
    state.Mood = 100;
    state.Satiety = 100;
    state.LastOnlineUtc = DateTimeOffset.Now.AddHours(-150);
    state.LastDateIndex = GameState.DateIndex(DateTime.Now.AddHours(-150));
    store2.SaveState(state);
    store2.Dispose();

    var store3 = new GameStore(root);
    var engine3 = new GameEngine(store3, catalog);
    engine3.Initialize();
    Check(engine3.State.Mood == 0 && engine3.State.Satiety == 0, "离线 150 小时按 100 小时封顶扣减");
    Check(engine3.State.Level == 3, "离线扣好感不降级");
    Check(engine3.State.Affection == 0, "饱食=0 跨天最多扣 4 次，不扣成负数");
    engine3.Save();
    store3.Dispose();

    // 再次打开保持数据。
    var store4 = new GameStore(root);
    var engine4 = new GameEngine(store4, catalog);
    engine4.Initialize();
    Check(engine4.State.Level == 3 && engine4.UnlockedGifs.Count == 2, "SQLite 重启后等级与解锁记录持久化");
    Check(engine4.EnabledGifs.Contains(firstGif), "启用列表持久化");

    // 累计 100 次有效互动 -> 成就 A01 + 对应 GIF。
    for (var i = 0; i < 100; i++)
    {
        engine4.State.LastInteractionUtc = DateTimeOffset.Now.AddSeconds(-31);
        engine4.Interact();
    }
    Check(engine4.IsAchievementUnlocked("A01"), "累计 100 次有效互动解锁成就 A01");
    Check(engine4.UnlockedGifs.Contains("052_queue.gif"), "成就 A01 解锁对应动态图 052");

    // 满级后好感只累计、不升级、不扣减。
    engine4.State.Level = 52;
    engine4.State.Affection = 0;
    engine4.AddAffection(10);
    var maxBefore = engine4.State.MaxLevelAffection;
    engine4.LoseAffection(999);
    Check(engine4.State.Level == 52 && engine4.State.MaxLevelAffection == maxBefore,
        "满级后好感累计且不扣减");

    // 动态图最多同时启用 5 个。
    while (engine4.UnlockedGifs.Count < 8) engine4.UnlockNextLevelGif();
    foreach (var id in engine4.UnlockedGifs.OrderBy(id => id).ToList())
    {
        if (engine4.EnabledGifs.Count >= 5) break;
        engine4.EnableGif(id);
    }
    Check(engine4.EnabledGifs.Count == 5, "启用列表最多 5 个");
    var sixth = engine4.UnlockedGifs.OrderBy(id => id).First(id => !engine4.EnabledGifs.Contains(id));
    var sixthResult = engine4.EnableGif(sixth);
    Check(!sixthResult.Ok && engine4.EnabledGifs.Count == 5, "第 6 个动态图启用被拒绝");

    // 上签任务奖励翻倍。
    var today = engine4.State.LastDateIndex;
    engine4.State.TaskDateIndex = today;
    engine4.State.TaskId = "T01";
    engine4.State.TaskAcceptedUtc = DateTimeOffset.Now;
    engine4.State.TaskProgress = 0;
    engine4.State.TaskAux = 0;
    engine4.State.TaskCompletedUtc = null;
    engine4.State.TaskRewardClaimed = false;
    engine4.State.DrawDateIndex = today;
    engine4.State.DrawResult = nameof(Fortune.Up);
    engine4.State.DrawAtUtc = DateTimeOffset.Now.AddSeconds(-1);
    var foodBefore = engine4.State.Food;
    engine4.State.LastInteractionUtc = DateTimeOffset.Now.AddSeconds(-31);
    engine4.Interact();
    Check(engine4.State.Food == foodBefore + 2, "上签任务食物 ×2");

    // 下签任务只给食物。
    engine4.State.TaskId = "T01";
    engine4.State.TaskAcceptedUtc = DateTimeOffset.Now;
    engine4.State.TaskProgress = 0;
    engine4.State.TaskAux = 0;
    engine4.State.TaskCompletedUtc = null;
    engine4.State.TaskRewardClaimed = false;
    engine4.State.DrawResult = nameof(Fortune.Down);
    engine4.State.DrawAtUtc = DateTimeOffset.Now.AddSeconds(-1);
    foodBefore = engine4.State.Food;
    engine4.State.LastInteractionUtc = DateTimeOffset.Now.AddSeconds(-31);
    engine4.Interact();
    Check(engine4.State.Food == foodBefore + 1, "下签任务只给食物 ×1");

    // 任务未接取时事件不计数。
    engine4.State.TaskId = "T01";
    engine4.State.TaskAcceptedUtc = null;
    engine4.State.TaskProgress = 0;
    engine4.State.TaskAux = 0;
    engine4.State.TaskCompletedUtc = null;
    engine4.State.TaskRewardClaimed = false;
    engine4.State.LastInteractionUtc = DateTimeOffset.Now.AddSeconds(-31);
    engine4.Interact();
    Check(engine4.State.TaskProgress == 0, "任务未接取时点击不计数");

    // 周签到 3 天里程碑自动发放一次。
    var weekStart = GameState.WeekStartIndex(DateTime.Now);
    engine4.State.SignWeekStart = weekStart;
    var todayBit = 1 << Math.Clamp(engine4.State.LastDateIndex - weekStart, 0, 6);
    engine4.State.WeeklySignMask = 0x7F & ~todayBit;
    engine4.State.Milestone3Claimed = false;
    engine4.State.Milestone7Claimed = true;
    engine4.State.LastSignDateIndex = engine4.State.LastDateIndex - 1;
    engine4.State.ContinuousSignIn = 2;
    foodBefore = engine4.State.Food;
    var weekSign = engine4.SignIn();
    Check(weekSign.Ok && engine4.WeeklySignedCount >= 3 && engine4.State.Food == foodBefore + 2,
        "签到寿司 ×1，周签到 3 天再给寿司 ×1");

    // 系统时间回拨忽略当日抽签。
    engine4.State.LastDateIndex = GameState.DateIndex(DateTime.Now) + 1;
    engine4.State.TimeRollbackDetected = false;
    var rollbackDraw = engine4.DrawFortune();
    Check(!rollbackDraw.Ok && engine4.State.TimeRollbackDetected, "系统时间回拨时忽略当日抽签并提示");

    // 背包/暂存区容量与丢弃规则。
    while (engine4.Inventory.Count < engine4.InventoryCapacity) engine4.AddItem(ItemCatalog.Fish);
    while (engine4.Staging.Count < GameEngine.MaxStaging) engine4.AddItem(ItemCatalog.Sushi);
    var overflow = engine4.AddItem(ItemCatalog.Sushi);
    Check(!overflow.Stored && !overflow.Staged &&
          engine4.Inventory.Count == engine4.InventoryCapacity &&
          engine4.Staging.Count == GameEngine.MaxStaging, "背包与暂存区都满时丢弃物品");
    engine4.State.Satiety = 0;
    var foodIndex = engine4.Inventory.ToList().FindIndex(ItemCatalog.IsFood);
    if (foodIndex >= 0) engine4.UseItem(foodIndex);
    Check(engine4.Inventory.Count == engine4.InventoryCapacity && engine4.Staging.Count == 19,
        "使用物品后暂存区自动移入 1 件");

    // 默认主形象存储：内置 1 条固定，用户自定义最多 5 条，可禁用/删除。
    var builtInAvatar = engine4.BuiltInDefaultAvatar;
    Check(builtInAvatar is { IsBuiltIn: true, Enabled: true } && engine4.DefaultAvatars.Count == 1,
        "内置默认主形象固定存在");
    var builtInBytes = Convert.FromBase64String(builtInAvatar!.Data);
    for (var i = 0; i < 5; i++)
        Check(engine4.AddDefaultAvatar(builtInBytes), $"添加自定义主形象 {i + 1}");
    Check(engine4.DefaultAvatars.Count(avatar => !avatar.IsBuiltIn) == 5 &&
          !engine4.AddDefaultAvatar(builtInBytes), "自定义主形象最多 5 张");
    var customAvatar = engine4.DefaultAvatars.First(avatar => !avatar.IsBuiltIn);
    Check(engine4.SetDefaultAvatarEnabled(customAvatar.Id, false) && !customAvatar.Enabled &&
          engine4.SetDefaultAvatarEnabled(customAvatar.Id, true) && customAvatar.Enabled,
        "自定义主形象可禁用/启用");
    Check(engine4.DeleteDefaultAvatar(customAvatar.Id) && engine4.DefaultAvatars.Count == 5,
        "自定义主形象可删除");

    var migration = new MigrationService(engine4);
    var buildPayload = typeof(MigrationService).GetMethod("BuildPayload",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    var payload = buildPayload?.Invoke(migration, new object?[] { null, DateTimeOffset.Now })
        as MigrationPayload;
    Check(payload is not null && payload.Settings is not null &&
          payload.Memos is not null && payload.Clipboard is not null &&
          payload.CharacterImages is not null,
        "迁移包包含设置配置、备忘录、剪贴板历史与角色图片集合");
    var migrationJson = System.Text.Json.JsonSerializer.Serialize(payload,
        new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
    var migrationRoundTrip = System.Text.Json.JsonSerializer.Deserialize<MigrationPayload>(
        migrationJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
    Check(migrationRoundTrip?.Settings is not null && migrationRoundTrip.Memos is not null &&
          migrationRoundTrip.Clipboard is not null &&
          migrationRoundTrip.Settings.Alarms is not null,
        "迁移包设置/备忘录/剪贴板/闹钟可序列化往返");
    var imagePath = Path.Combine(Path.GetTempPath(), "whalepet-migration-" + Guid.NewGuid().ToString("N") + ".png");
    File.WriteAllBytes(imagePath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 });
    var imageSettings = new PetSettings();
    imageSettings.MainImages.Clear();
    imageSettings.MainImages.Add(imagePath);
    var collectImages = typeof(MigrationService).GetMethod("CollectCharacterImages",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    var imageTransfers = collectImages?.Invoke(null, new object[] { imageSettings })
        as List<CharacterImageTransfer>;
    Check(imageTransfers is { Count: 1 } && imageTransfers[0].Role == "main" &&
          imageTransfers[0].Data.Length > 0,
        "迁移包收集自定义主形象图片数据");
    File.Delete(imagePath);

    var mergeMissing = typeof(SettingsStore).GetMethod("MergeMissingFields",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    var mergedSettings = mergeMissing?.Invoke(null, new object[]
    {
        "{\"TodoEnabled\":true}",
        new[] { "{\"TodoWidth\":777,\"ReadingWidth\":333}" },
    }) as PetSettings;
    Check(mergedSettings is { TodoEnabled: true, TodoWidth: 777, ReadingWidth: 333 },
        "旧配置缺失设置项可从备份合并恢复");
    var defaultPanels = typeof(SettingsStore).GetMethod("IsEffectivelyDefault",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    var defaultSettings = new PetSettings();
    var customSettings = new PetSettings { TodoEnabled = true, TodoWidth = 777 };
    Check(defaultPanels?.Invoke(null, new object[] { defaultSettings }) is true &&
          defaultPanels?.Invoke(null, new object[] { customSettings }) is false,
        "设置恢复逻辑能识别默认配置");

    // 阅读区：UTF-8/GBK/BOM/分页/错误文本。
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var readingText = "第一页开头，中文标点算一个字符。\n第二行继续测试分页。";
    var readingPath = Path.Combine(root, "reading-utf8.txt");
    File.WriteAllText(readingPath, readingText, new UTF8Encoding(false));
    var readingSettings = new PetSettings
    {
        ReadingTextPath = readingPath,
        ReadingCharsPerPage = 10,
        ReadingEncoding = "自动检测",
    };
    Check(ReadingService.Reload(readingSettings) && ReadingService.PageCount == 3 &&
          ReadingService.PageAt(1).Length == 10,
        "阅读区 UTF-8 分页按字符数计算");
    var gbkPath = Path.Combine(root, "reading-gbk.txt");
    File.WriteAllBytes(gbkPath, Encoding.GetEncoding(936).GetBytes("中文测试"));
    readingSettings.ReadingTextPath = gbkPath;
    readingSettings.ReadingEncoding = "自动检测";
    Check(ReadingService.Reload(readingSettings) && ReadingService.RawText == "中文测试",
        "阅读区无 BOM 时 UTF-8 严格失败后回退 GBK");
    var utf16Path = Path.Combine(root, "reading-utf16.txt");
    var utf16 = new UnicodeEncoding(false, true).GetBytes("UTF16 文本");
    var preamble = new UnicodeEncoding(false, true).GetPreamble();
    File.WriteAllBytes(utf16Path, preamble.Concat(utf16).ToArray());
    readingSettings.ReadingTextPath = utf16Path;
    readingSettings.ReadingEncoding = "自动检测";
    Check(ReadingService.Reload(readingSettings) && ReadingService.RawText == "UTF16 文本",
        "阅读区识别 UTF-16 BOM");
    var emptyPath = Path.Combine(root, "reading-empty.txt");
    File.WriteAllBytes(emptyPath, Array.Empty<byte>());
    readingSettings.ReadingTextPath = emptyPath;
    Check(!ReadingService.Reload(readingSettings) &&
          ReadingService.LastError == "文本读取失败，请检查路径和编码",
        "阅读区空文件提示读取失败");
    readingSettings.ReadingTextPath = Path.Combine(root, "missing.txt");
    Check(!ReadingService.Reload(readingSettings) &&
          ReadingService.LastError == "文本读取失败，请检查路径和编码",
        "阅读区路径不存在提示读取失败");
    var cleaningPath = Path.Combine(root, "reading-cleaning.txt");
    File.WriteAllText(cleaningPath, "  A\tB\r\n\r\nC  ", new UTF8Encoding(false));
    readingSettings.ReadingTextPath = cleaningPath;
    readingSettings.ReadingCharsPerPage = 2;
    Check(ReadingService.Reload(readingSettings) &&
          ReadingService.RawText == "A B C" &&
          !ReadingService.RawText.Any(char.IsControl) &&
          ReadingService.PageCount == 3,
        "阅读区先清洗控制字符/空白再分页");
    Check(Enum.TryParse<AvatarKind>("Live2D", true, out var live2dKind) &&
          live2dKind == AvatarKind.Live2D &&
          new GameState().CurrentLive2DModelId == "seethrough_output",
        "Live2D 形象类型与默认模型 ID");

    // 多显示器吸附：使用副屏工作区（1920..3840）验证算法不会退回主屏边界。
    var secondary = new MonitorPlacement.RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1040 };
    var nearSecondaryLeft = new MonitorPlacement.RECT { Left = 1925, Top = 300, Right = 2075, Bottom = 447 };
    var snappedSecondary = MonitorPlacement.ConstrainToWorkArea(nearSecondaryLeft, secondary, 15, true);
    Check(snappedSecondary.Left == 1920 && snappedSecondary.Top == 300,
        "副屏左边缘 15px 内吸附到副屏工作区 1920");
    var nearSecondaryRight = new MonitorPlacement.RECT { Left = 3680, Top = 300, Right = 3830, Bottom = 447 };
    var snappedSecondaryRight = MonitorPlacement.ConstrainToWorkArea(nearSecondaryRight, secondary, 15, true);
    Check(snappedSecondaryRight.Left == 3690,
        "副屏右边缘 15px 内吸附到副屏工作区 3840");
    var outsideSecondary = new MonitorPlacement.RECT { Left = 3800, Top = 1000, Right = 3950, Bottom = 1147 };
    var clampedSecondary = MonitorPlacement.ConstrainToWorkArea(outsideSecondary, secondary, 15, true);
    Check(clampedSecondary.Left == 3690 && clampedSecondary.Top == 893,
        "拖出副屏工作区时坐标被夹回副屏范围");
    var primary = new MonitorPlacement.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
    var primaryLeft = new MonitorPlacement.RECT { Left = 10, Top = 100, Right = 160, Bottom = 247 };
    Check(MonitorPlacement.ConstrainToWorkArea(primaryLeft, primary, 15, true).Left == 0,
        "主屏左边缘吸附保持原有行为");

    var singleKey = HotkeyGesture.TryParse("A", out var singleGesture) &&
                    singleGesture.Matches(Key.A, ModifierKeys.None);
    var comboKey = HotkeyGesture.TryParse("Ctrl+Shift+F", out var comboGesture) &&
                   comboGesture.Matches(Key.F, ModifierKeys.Control | ModifierKeys.Shift);
    Check(singleKey && comboKey, "快捷键录制可解析单键与组合键");
    var migratedHotkeys = new PetSettings
    {
        ReadingPrevHotkey = "PageUp",
        ReadingNextHotkey = "PageDown",
    };
    migratedHotkeys.Normalize();
    var clearedHotkey = new PetSettings { ReadingPrevHotkey = "" };
    clearedHotkey.Normalize();
    Check(migratedHotkeys.ReadingPrevHotkey == "Ctrl+," &&
          migratedHotkeys.ReadingNextHotkey == "Ctrl+." &&
          clearedHotkey.ReadingPrevHotkey == "",
        "旧版翻页快捷键迁移到新默认值，且允许清除为未绑定");

    store4.Dispose();
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;
