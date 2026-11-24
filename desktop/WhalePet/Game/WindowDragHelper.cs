using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WhalePet.Game;

internal static class WindowDragHelper
{
    /// <summary>Attached marker for item slots, GIF cards and other clickable content.</summary>
    public static readonly DependencyProperty NoDragProperty =
        DependencyProperty.RegisterAttached(
            "NoDrag",
            typeof(bool),
            typeof(WindowDragHelper),
            new FrameworkPropertyMetadata(false));

    public static void SetNoDrag(DependencyObject element, bool value) =>
        element.SetValue(NoDragProperty, value);

    public static bool GetNoDrag(DependencyObject element) =>
        (bool)element.GetValue(NoDragProperty);

    /// <summary>True when the pointer is over an interactive control, so panel dragging must not start.</summary>
    public static bool IsInteractive(object? source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (GetNoDrag(current)) return true;
            if (current is ButtonBase or ComboBox or TextBox or PasswordBox or TabItem or
                ScrollBar or Thumb or Slider or ListBoxItem)
            {
                return true;
            }
            if (current is Window) return false;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
