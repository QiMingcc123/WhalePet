using System.Windows;

namespace WhalePet;

public partial class MemoEditWindow : Window
{
    public string EditedText { get; private set; }

    public MemoEditWindow(string text)
    {
        InitializeComponent();
        EditedText = text;
        Editor.Text = text;
        Loaded += (_, _) =>
        {
            Editor.Focus();
            Editor.CaretIndex = Editor.Text.Length;
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var text = Editor.Text.Trim();
        if (text.Length == 0)
        {
            ErrorText.Text = "内容不能为空。";
            return;
        }
        if (text.Length > 2000)
        {
            ErrorText.Text = "最多 2000 字。";
            return;
        }
        EditedText = text;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
