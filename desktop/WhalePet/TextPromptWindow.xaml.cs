using System.Windows;
using System.Windows.Controls;

namespace WhalePet;

public partial class TextPromptWindow : Window
{
    private readonly int _maxLength;
    private readonly Func<string, string?>? _validate;

    public string Value => ValueBox.Text.Trim();

    public TextPromptWindow(string prompt, string? initial = null, int maxLength = 30,
        Func<string, string?>? validate = null)
    {
        InitializeComponent();
        _maxLength = Math.Clamp(maxLength, 1, 200);
        _validate = validate;
        PromptText.Text = prompt;
        ValueBox.Text = initial ?? "";
        ValueBox.MaxLength = 0;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) OnOkClick(this, new RoutedEventArgs());
        };
    }

    private void OnValueChanged(object sender, TextChangedEventArgs e)
    {
        if (ErrorText is not null) ErrorText.Text = "";
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var value = Value;
        if (value.Length == 0)
        {
            ErrorText.Text = "名称不能为空。";
            return;
        }
        if (value.Length > _maxLength)
        {
            ErrorText.Text = $"名称不能超过 {_maxLength} 个字符。";
            return;
        }
        var error = _validate?.Invoke(value);
        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorText.Text = error;
            return;
        }
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
