using System.Windows;
using System.Windows.Media;

namespace WhalePet.Game;

public partial class ActivationWindow : Window
{
    public ActivationWindow()
    {
        InitializeComponent();
    }

    private void OnGenerateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CodeBox.Text = ActivationMigrationService.GenerateCode();
            CopyButton.IsEnabled = CodeBox.Text.Length > 0;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xE0, 0xC0));
            StatusText.Text = "迁移码已生成，有效期 24 小时且只能使用一次。请勿泄露给他人。";
        }
        catch (Exception error)
        {
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x9A));
            StatusText.Text = "生成失败：" + error.Message;
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(CodeBox.Text);
            StatusText.Text = "迁移码已复制到剪贴板。";
        }
        catch (Exception error)
        {
            StatusText.Text = "复制失败：" + error.Message;
        }
    }

    private void OnRedeemClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RedeemBox.Text))
        {
            StatusText.Text = "请先粘贴迁移码。";
            return;
        }
        var ok = ActivationMigrationService.TryRedeem(RedeemBox.Text, out var message);
        StatusText.Foreground = new SolidColorBrush(ok
            ? Color.FromRgb(0x8F, 0xE0, 0xC0)
            : Color.FromRgb(0xFF, 0x9A, 0x9A));
        StatusText.Text = message;
        if (ok) RedeemBox.Text = "";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
