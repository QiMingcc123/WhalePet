using System.Windows;
using System.Windows.Media;

namespace WhalePet;

public partial class TodoEditWindow : Window
{
    private sealed class Choice
    {
        public string Key { get; init; } = "";
        public string Name { get; init; } = "";
        public Brush Brush { get; init; } = Brushes.Transparent;
        public override string ToString() => Name;
    }

    public TodoItem Result { get; }

    public TodoEditWindow(TodoItem? existing = null)
    {
        InitializeComponent();
        Result = existing is null
            ? new TodoItem()
            : new TodoItem
            {
                Id = existing.Id,
                Title = existing.Title,
                Notes = existing.Notes,
                Urgency = existing.Urgency,
                Status = existing.Status,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = existing.UpdatedAt,
            };
        TitleText.Text = existing is null ? "新增待办" : "编辑待办";
        UrgencyBox.ItemsSource = TodoPalette.Urgencies
            .Select(item => new Choice
            {
                Key = item.Key,
                Name = item.Name,
                Brush = new SolidColorBrush(item.Color),
            })
            .ToList();
        UrgencyBox.SelectedIndex = Math.Max(0, Array.FindIndex(TodoPalette.Urgencies,
            item => item.Key == Result.Urgency));
        StatusBox.ItemsSource = TodoPalette.Statuses
            .Select(item => new Choice { Key = item.Key, Name = item.Name })
            .ToList();
        StatusBox.SelectedIndex = Math.Max(0, Array.FindIndex(TodoPalette.Statuses,
            item => item.Key == Result.Status));
        TodoTitleBox.Text = Result.Title;
        TodoNotesBox.Text = Result.Notes;
        Loaded += (_, _) =>
        {
            TodoTitleBox.Focus();
            TodoTitleBox.SelectAll();
        };
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var title = TodoTitleBox.Text.Trim();
        if (title.Length == 0)
        {
            ErrorText.Text = "请输入待办标题。";
            return;
        }
        if (title.Length > 120) title = title[..120];
        Result.Title = title;
        var notes = TodoNotesBox.Text.Trim();
        Result.Notes = notes.Length > 1000 ? notes[..1000] : notes;
        Result.Urgency = (UrgencyBox.SelectedItem as Choice)?.Key ?? "Green";
        Result.Status = (StatusBox.SelectedItem as Choice)?.Key ?? "NotStarted";
        Result.UpdatedAt = DateTimeOffset.Now;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
