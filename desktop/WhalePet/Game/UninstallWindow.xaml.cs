using System.Windows;

namespace WhalePet.Game;

public partial class UninstallWindow : Window
{
    public UninstallWindow()
    {
        InitializeComponent();
        OnDataChoiceChanged(this, new RoutedEventArgs());
    }

    public bool ClearUserData => ClearDataRadio.IsChecked == true;

    private void OnDataChoiceChanged(object sender, RoutedEventArgs e)
    {
        if (ClearHint is null || UninstallButton is null) return;
        var clear = ClearDataRadio.IsChecked == true;
        ClearHint.Visibility = clear ? Visibility.Visible : Visibility.Collapsed;
        UninstallButton.Content = clear ? "清空数据并卸载" : "卸载";
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (ClearUserData)
        {
            var color = SettingsStore.Load().ThemeColor;
            if (!ConfirmDialog.Show(this, "此操作不可逆，确认清空所有数据吗？",
                    "清空所有数据", "确认清空", color))
            {
                return;
            }
        }
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
