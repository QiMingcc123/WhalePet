using System.Windows.Media;

namespace WhalePet;

public static class TodoPalette
{
    public static readonly (string Key, string Name, Color Color)[] Urgencies =
    {
        ("Red", "紧急", Color.FromRgb(0xFF, 0x5C, 0x5C)),
        ("Yellow", "加急", Color.FromRgb(0xFF, 0xC1, 0x4D)),
        ("Green", "正常", Color.FromRgb(0x58, 0xE0, 0xB4)),
        ("Blue", "不急", Color.FromRgb(0x74, 0x9B, 0xFF)),
    };

    public static readonly (string Key, string Name)[] Statuses =
    {
        ("NotStarted", "未开始"),
        ("InProgress", "进行中"),
        ("Completed", "已完成"),
    };

    public static Color ColorOf(string? key) =>
        Urgencies.FirstOrDefault(item => item.Key == key).Color is var color && color != default
            ? color
            : Urgencies[2].Color;

    public static string NameOf(string? key) =>
        Urgencies.FirstOrDefault(item => item.Key == key).Name ?? Urgencies[2].Name;

    public static string StatusName(string? key) =>
        Statuses.FirstOrDefault(item => item.Key == key).Name ?? Statuses[0].Name;
}
