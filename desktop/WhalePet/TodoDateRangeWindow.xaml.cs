using System.Windows;

namespace WhalePet;

public partial class TodoDateRangeWindow : Window
{
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool Cleared { get; private set; }

    public TodoDateRangeWindow(DateTime? start, DateTime? end)
    {
        InitializeComponent();
        StartDatePicker.SelectedDate = start;
        EndDatePicker.SelectedDate = end;
        StartDate = start?.Date;
        EndDate = end?.Date;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (StartDatePicker.SelectedDate is not { } start || EndDatePicker.SelectedDate is not { } end)
        {
            ErrorText.Text = "请选择开始和结束日期。";
            return;
        }
        if (end.Date < start.Date)
        {
            ErrorText.Text = "结束日期不能早于开始日期。";
            return;
        }
        if ((end.Date - start.Date).TotalDays > 180)
        {
            ErrorText.Text = "自定义日期跨度不能超过 180 天。";
            return;
        }
        StartDate = start.Date;
        EndDate = end.Date;
        DialogResult = true;
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Cleared = true;
        StartDate = null;
        EndDate = null;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
