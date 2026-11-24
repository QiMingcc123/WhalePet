using System.Windows;
using System.Windows.Input;

namespace WhalePet.Game;

public partial class PasswordPromptWindow : Window
{
    private readonly bool _confirm;

    public PasswordPromptWindow(string title, string description, bool confirm, string warning)
    {
        InitializeComponent();
        _confirm = confirm;
        Title = title;
        TitleText.Text = title;
        DescriptionText.Text = description;
        WarningText.Text = warning;
        ConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => PasswordBox1.Focus();
    }

    public string Password { get; private set; } = "";

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox1.Password;
        if (password.Length < 8)
        {
            WarningText.Text = "密码长度必须至少 8 位。";
            return;
        }
        if (_confirm && password != PasswordBox2.Password)
        {
            WarningText.Text = "两次输入的密码不一致。";
            return;
        }
        Password = password;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnConfirmClick(sender, e);
        else if (e.Key == Key.Escape) DialogResult = false;
    }
}
