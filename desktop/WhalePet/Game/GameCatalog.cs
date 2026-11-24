namespace WhalePet.Game;

public sealed record AchievementDef(
    string Id,
    string Category,
    string Name,
    string Description,
    string Counter,
    long Target,
    bool Hidden = false);

public static class AchievementCatalog
{
    public static readonly string[] Categories = { "互动类", "陪伴类", "游戏类", "任务类", "签到成就" };

    public static readonly IReadOnlyList<AchievementDef> All = new List<AchievementDef>
    {
        // 互动类 15
        new("A01", "互动类", "初次心动", "累计有效互动 100 次", "interaction_total", 100),
        new("A02", "互动类", "亲密无间", "累计有效互动 500 次", "interaction_total", 500),
        new("A03", "互动类", "心灵相通", "累计有效互动 1000 次", "interaction_total", 1000),
        new("A04", "互动类", "投喂新手", "累计投喂 50 次", "feed_total", 50),
        new("A05", "互动类", "美食相伴", "累计投喂 200 次", "feed_total", 200),
        new("A06", "互动类", "满汉全席", "累计投喂 500 次", "feed_total", 500),
        new("A07", "互动类", "一日十次", "单日有效互动 10 次", "interaction_day_max", 10),
        new("A08", "互动类", "一日二十", "单日有效互动 20 次", "interaction_day_max", 20),
        new("A09", "互动类", "一日三十", "单日有效互动 30 次", "interaction_day_max", 30),
        new("A10", "互动类", "换装达人", "累计使用动态图 10 个", "gif_use_total", 10),
        new("A11", "互动类", "百变星君", "累计使用动态图 50 个", "gif_use_total", 50),
        new("A12", "互动类", "千面鲸娘", "累计使用动态图 100 个", "gif_use_total", 100),
        new("A13", "互动类", "心花怒放", "累计心情满值 10 次", "mood_full_total", 10),
        new("A14", "互动类", "心满意足", "累计心情满值 50 次", "mood_full_total", 50),
        new("A15", "互动类", "心潮澎湃", "累计心情满值 100 次", "mood_full_total", 100),

        // 陪伴类 16
        new("A16", "陪伴类", "初识相伴", "累计陪伴 10 小时", "companion_seconds", 10 * 3600L),
        new("A17", "陪伴类", "长夜相伴", "累计陪伴 100 小时", "companion_seconds", 100 * 3600L),
        new("A18", "陪伴类", "深海羁绊", "累计陪伴 500 小时", "companion_seconds", 500 * 3600L),
        new("A19", "陪伴类", "两小时", "连续在线 2 小时", "online_streak_minutes", 120),
        new("A20", "陪伴类", "六小时", "连续在线 6 小时", "online_streak_minutes", 360),
        new("A21", "陪伴类", "十二小时", "连续在线 12 小时", "online_streak_minutes", 720),
        new("A22", "陪伴类", "七日之约", "连续签到 7 天", "signin_streak_max", 7),
        new("A23", "陪伴类", "三十日之约", "连续签到 30 天", "signin_streak_max", 30),
        new("A24", "陪伴类", "百日守候", "连续签到 100 天", "signin_streak_max", 100),
        new("A25", "陪伴类", "成长之路", "等级达到 17 级", "level", 17),
        new("A26", "陪伴类", "深海领主", "等级达到 34 级", "level", 34),
        new("A27", "陪伴类", "满级鲸娘", "等级达到 51 级", "level", 51),
        new("A28", "陪伴类", "月度陪伴", "累计在线天数 30 天（每日 ≥30 分钟）", "online_days", 30),
        new("A29", "陪伴类", "百日陪伴", "累计在线天数 100 天（每日 ≥30 分钟）", "online_days", 100),
        new("A30", "陪伴类", "周年陪伴", "累计在线天数 365 天（每日 ≥30 分钟）", "online_days", 365),
        new("A31", "陪伴类", "满级之后的喜欢", "满级后累计好感达到 500", "max_level_affection", 500, true),

        // 游戏类 12
        new("A32", "游戏类", "抽签三十", "累计抽签 30 次", "draw_total", 30),
        new("A33", "游戏类", "上上签", "抽到上签 50 次", "draw_up_total", 50),
        new("A34", "游戏类", "下下签", "抽到下签 50 次", "draw_down_total", 50),
        new("A35", "游戏类", "钓鱼新手", "钓鱼（简单模式）成功 200 次", "fish_easy", 200),
        new("A36", "游戏类", "钓鱼达人", "钓鱼（普通模式）成功 100 次", "fish_normal", 100),
        new("A37", "游戏类", "钓鱼高手", "钓鱼（困难模式）成功 50 次", "fish_hard", 50),
        new("A38", "游戏类", "钓鱼大师", "钓鱼（噩梦模式）成功 20 次", "fish_nightmare", 20),
        new("A39", "游戏类", "厨房新手", "烹饪（简单模式）成功 200 次", "cook_easy", 200),
        new("A40", "游戏类", "厨艺精进", "烹饪（普通模式）成功 100 次", "cook_normal", 100),
        new("A41", "游戏类", "厨神", "烹饪（困难模式）成功 50 次", "cook_hard", 50),
        new("A42", "游戏类", "垂钓日", "单日完成 5 次钓鱼，累计 100 天", "fish5_days", 100, true),
        new("A43", "游戏类", "烹饪日", "单日完成 1 次烹饪，累计 100 天", "cook1_days", 100, true),

        // 任务类 8
        new("A44", "任务类", "任务新手", "累计完成任务 50 个", "task_completed", 50),
        new("A45", "任务类", "任务达人", "累计完成任务 200 个", "task_completed", 200),
        new("A46", "任务类", "接取新手", "累计接取任务 100 个", "task_accepted", 100),
        new("A47", "任务类", "接取达人", "累计接取任务 500 个", "task_accepted", 500),
        new("A48", "任务类", "食物储备", "累计任务获得食物 50 个", "task_food", 50),
        new("A49", "任务类", "食物满仓", "累计任务获得食物 200 个", "task_food", 200),
        new("A50", "任务类", "好感积累", "累计任务获得好感 50", "task_affection", 50),
        new("A51", "任务类", "好感满满", "累计任务获得好感 200", "task_affection", 200),

        // 签到成就 2
        new("A52", "签到成就", "百日之约", "累计签到 100 天", "signin_total", 100, true),
        new("A53", "签到成就", "一年之约", "累计签到 365 天", "signin_total", 365, true),
    };

    public static readonly IReadOnlyDictionary<string, AchievementDef> ById =
        All.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
}

public sealed record TaskDef(
    string Id,
    string Name,
    string Category,
    int Target,
    string Description,
    string Counter);

public static class TaskCatalog
{
    public static readonly IReadOnlyList<TaskDef> All = new List<TaskDef>
    {
        new("T01", "初次问候", "互动", 1, "点击角色互动 1 次", "interaction_any"),
        new("T02", "亲密互动", "互动", 3, "冷却外有效互动 3 次", "interaction_effective"),
        new("T03", "今日活跃", "互动", 5, "冷却外有效互动 5 次", "interaction_effective"),
        new("T04", "心情回暖", "心情", 60, "心情值接取后首次达到 60", "mood_value"),
        new("T05", "元气满满", "心情", 80, "心情值接取后首次达到 80", "mood_value"),
        new("T06", "首次投喂", "投喂", 1, "成功投喂食物 1 次", "feed"),
        new("T07", "加餐时间", "投喂", 2, "成功投喂食物 2 次", "feed"),
        new("T08", "吃饱喝足", "饱食", 80, "饱食度接取后首次达到 80", "satiety_value"),
        new("T09", "告别饥饿", "饱食", 1, "饱食度从低于 50 恢复到 50 以上", "satiety_recovered"),
        new("T10", "小憩片刻", "陪伴", 15, "当日在线陪伴累计 15 分钟", "online_minutes"),
        new("T11", "相伴半小时", "陪伴", 30, "当日在线陪伴累计 30 分钟", "online_minutes"),
        new("T12", "长情陪伴", "陪伴", 45, "当日在线陪伴累计 45 分钟", "online_minutes"),
        new("T13", "不离不弃", "陪伴", 10, "连续在线 10 分钟不退出", "online_minutes"),
        new("T14", "今日运势", "抽签", 1, "完成 1 次抽签", "draw"),
        new("T15", "签文解读", "抽签", 1, "打开抽签 Tab 查看今日签文 1 次", "draw_view"),
        new("T16", "每日打卡", "签到", 1, "完成 1 次每日签到", "signin"),
        new("T17", "周签检查", "签到", 1, "打开周签到板 1 次", "week_view"),
        new("T18", "背包整理", "面板", 1, "打开背包 Tab 1 次", "inventory_view"),
        new("T19", "荣誉回顾", "面板", 1, "打开成就墙 1 次", "achievement_view"),
        new("T20", "面板巡视", "面板", 1, "打开角色面板 1 次", "panel_view"),
        new("T21", "抛竿练习", "钓鱼", 1, "进入钓鱼小游戏 1 次", "fishing_enter"),
        new("T22", "初次垂钓", "钓鱼", 1, "完成 1 次抛竿", "fishing_cast"),
        new("T23", "小有渔获", "钓鱼", 1, "成功钓鱼 1 次（任意难度）", "fishing_success"),
        new("T24", "双喜临门", "钓鱼", 2, "成功钓鱼 2 次（任意难度）", "fishing_success"),
        new("T25", "厨房新手", "烹饪", 1, "进入烹饪小游戏 1 次", "cooking_enter"),
        new("T26", "料理初成", "烹饪", 1, "烹饪成功 1 次（任意难度）", "cooking_success"),
        new("T27", "换装时刻", "动态图", 1, "启用动态图 1 次", "gif_enable"),
        new("T28", "百变形象", "动态图", 1, "更换动态图 1 次", "gif_switch"),
        new("T29", "运势确认", "抽签", 1, "接取后完成抽签并查看今日签文", "draw_then_view"),
        new("T30", "打卡确认", "签到", 1, "接取后完成签到并查看周签到板", "signin_then_view"),
    };

    public static TaskDef? Find(string? id) =>
        All.FirstOrDefault(task => string.Equals(task.Id, id, StringComparison.OrdinalIgnoreCase));
}
