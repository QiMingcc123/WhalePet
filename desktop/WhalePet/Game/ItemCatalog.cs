using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhalePet.Game;

public sealed record ItemDef(
    string Id,
    string Name,
    string Category,
    int Satiety,
    string Description,
    string ResourceName);

/// <summary>
/// Backpack item definitions and their embedded icons. One item occupies one
/// slot; icons are 25x25 inside a 32x32 slot and keep their aspect ratio.
/// </summary>
public static class ItemCatalog
{
    public const string Fish = "fish";
    public const string FishMeat = "fish_meat";
    public const string Sushi = "sushi";

    public const string FoodCategory = "食物";
    public const string IngredientCategory = "食材";

    public static readonly ItemDef FishDef = new(
        Fish,
        "小鱼",
        IngredientCategory,
        0,
        "钓上来的鲜活小鱼,烹饪食材",
        "WhalePet.Resources.img.ingredients.fish.png");

    public static readonly ItemDef FishMeatDef = new(
        FishMeat,
        "鱼肉",
        FoodCategory,
        20,
        "烹饪后的美味鱼肉，投喂鲸鱼娘可增加20点饱食度",
        "WhalePet.Resources.img.food.fish_meat.png");

    public static readonly ItemDef SushiDef = new(
        Sushi,
        "寿司",
        FoodCategory,
        40,
        "米饭与鱼肉的组合，投喂鲸鱼娘可增加40点饱食度",
        "WhalePet.Resources.img.food.sushi.png");

    public static readonly IReadOnlyList<ItemDef> All = new[] { FishDef, FishMeatDef, SushiDef };

    private static readonly IReadOnlyDictionary<string, ItemDef> ById =
        All.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, BitmapSource> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    public static ItemDef? Find(string? id) =>
        !string.IsNullOrWhiteSpace(id) && ById.TryGetValue(id, out var item) ? item : null;

    public static bool IsKnown(string? id) => Find(id) is not null;

    public static bool IsFood(string? id) => Find(id) is { Satiety: > 0 };

    public static bool IsIngredient(string? id) =>
        string.Equals(Find(id)?.Category, IngredientCategory, StringComparison.Ordinal);

    public static string NameOf(string? id) => Find(id)?.Name ?? "未知物品";

    public static BitmapSource? LoadIcon(string? id)
    {
        var def = Find(id);
        if (def is null) return null;
        lock (ImageCache)
        {
            if (ImageCache.TryGetValue(def.Id, out var cached)) return cached;
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(def.ResourceName);
                if (stream is null) return null;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                ImageCache[def.Id] = bitmap;
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
