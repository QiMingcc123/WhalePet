using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WhalePet;

/// <summary>A small click-to-record hotkey field used by the settings window.</summary>
public sealed class HotkeyRecorderBox : Border
{
    private readonly TextBlock _display;
    private string _text = "";
    private string _beforeRecording = "";
    private bool _recording;

    public event EventHandler? BindingChanged;

    public HotkeyRecorderBox()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        MinHeight = 28;
        Padding = new Thickness(8, 4, 8, 4);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x22, 0x38));
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x8F, 0xD3, 0xFF));
        ToolTip = "单击录制快捷键；按 Esc 取消，按 Backspace 清除设置";
        KeyboardNavigation.SetIsTabStop(this, true);
        _display = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEA, 0xF6, 0xFF)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Child = _display;
        UpdateDisplay();
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = (value ?? "").Trim();
            if (!_recording) UpdateDisplay();
        }
    }

    public bool IsRecording => _recording;

    public void StartRecording()
    {
        if (_recording) return;
        _beforeRecording = _text;
        _recording = true;
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x58, 0xE0, 0xB4));
        UpdateDisplay();
        Focus();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        StartRecording();
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_recording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            _text = _beforeRecording;
            StopRecording();
            return;
        }
        if (e.Key == Key.Back)
        {
            _text = "";
            StopRecording();
            BindingChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var gesture = HotkeyGesture.FromKey(key, Keyboard.Modifiers);
        if (gesture is null) return;
        _text = gesture.Display;
        StopRecording();
        BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        if (_recording)
        {
            _text = _beforeRecording;
            StopRecording();
        }
        base.OnLostKeyboardFocus(e);
    }

    private void StopRecording()
    {
        _recording = false;
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x8F, 0xD3, 0xFF));
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_recording)
        {
            _display.Text = "请按下组合键";
            _display.Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xE0, 0xB4));
        }
        else if (string.IsNullOrWhiteSpace(_text))
        {
            _display.Text = "未设置";
            _display.Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xA5, 0xC5));
        }
        else
        {
            _display.Text = _text;
            _display.Foreground = new SolidColorBrush(Color.FromRgb(0xEA, 0xF6, 0xFF));
        }
    }
}
