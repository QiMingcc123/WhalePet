using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace WhalePet.Game;

/// <summary>Transparent WebView2 host for the Live2D model. It only renders the
/// Live2D layer; all existing PNG/GIF rendering remains in MainWindow.</summary>
public partial class Live2DWindow : Window
{
    private const string VirtualHost = "live2d.local";

    private TaskCompletionSource<bool>? _readySource;
    private Task<bool>? _initialization;
    private bool _webViewInitialized;
    private bool _ready;
    private bool _wantedVisible;
    private bool _disposed;
    private bool _renderActive;
    private bool _suspended;
    private bool _suspendInFlight;
    private HwndSource? _source;

    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtTransparent = -1;

    /// <summary>Optional extra pass-through rule (for example the status bar
    /// that overlaps the lower edge of the character at some scales).</summary>
    public Func<Point, bool>? ShouldPassThrough { get; set; }

    /// <summary>Returns the character area in owner-client coordinates. The
    /// Live2D surface is sized to that area instead of the whole pet window so
    /// it never covers the speech bubble or the status panel.</summary>
    public Func<Rect>? CharacterBoundsProvider { get; set; }

    public event Action<string>? LoadFailed;
    public event Action? RightClickRequested;
    public event Action<double, double>? DragStarted;
    public event Action<double, double>? DragMoved;
    public event Action? DragEnded;
    public event Action<string>? PartClicked;

    public bool IsModelReady => _ready;

    /// <summary>Normalized model bounds inside the Web canvas, reported by the
    /// renderer. Used so transparent padding around the model is not treated
    /// as a draggable/clickable part.</summary>
    public Rect ModelBounds { get; private set; } = new(0, 0, 1, 1);

    public bool ContainsModelPoint(Point screenPoint)
    {
        return TryGetModelNormalized(screenPoint, out _, out _);
    }

    /// <summary>Returns 0..1 coordinates inside the reported model bounds.
    /// The model is narrower than the character host, so part regions must be
    /// relative to the model box, not to the whole canvas.</summary>
    public bool TryGetModelNormalized(Point screenPoint, out double nx, out double ny)
    {
        nx = 0;
        ny = 0;
        try
        {
            var local = PointFromScreen(screenPoint);
            var u = local.X / Math.Max(1, ActualWidth);
            var v = local.Y / Math.Max(1, ActualHeight);
            var bounds = ModelBounds;
            if (bounds.Width <= 0.001 || bounds.Height <= 0.001) return false;
            if (u < bounds.X || v < bounds.Y ||
                u > bounds.X + bounds.Width || v > bounds.Y + bounds.Height)
            {
                return false;
            }
            nx = (u - bounds.X) / bounds.Width;
            ny = (v - bounds.Y) / bounds.Height;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Live2DWindow()
    {
        InitializeComponent();
        Browser.Visibility = Visibility.Visible;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest) return IntPtr.Zero;

        // WPF layered windows report transparent areas as HTTRANSPARENT. Left
        // alone, the desktop would receive both left and right clicks made on
        // the model. Claim only the actual model bounds; everything else stays
        // a true click-through so desktop selection still works.
        var packed = lParam.ToInt64();
        var screenX = (short)(packed & 0xFFFF);
        var screenY = (short)((packed >> 16) & 0xFFFF);
        var screenPoint = new Point(screenX, screenY);
        var hitModel = ContainsModelPoint(screenPoint);
        var passThrough = ShouldPassThrough?.Invoke(screenPoint) == true;
        handled = true;
        return new IntPtr(hitModel && !passThrough ? HtClient : HtTransparent);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncToOwner();
    }

    public void AttachOwner(Window owner)
    {
        Owner = owner;
        SyncToOwner();
    }

    public void SyncToOwner()
    {
        if (Owner is null) return;
        var bounds = CharacterBoundsProvider?.Invoke()
                     ?? new Rect(0, 0, Math.Max(1, Owner.ActualWidth), Math.Max(1, Owner.ActualHeight));
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            bounds = new Rect(0, 0, Math.Max(1, Owner.ActualWidth), Math.Max(1, Owner.ActualHeight));
        }
        Left = Owner.Left + bounds.X;
        Top = Owner.Top + bounds.Y;
        Width = Math.Max(1, bounds.Width);
        Height = Math.Max(1, bounds.Height);
        if (!Owner.IsVisible) Hide();
    }

    public void SetAvatarVisible(bool visible)
    {
        if (_wantedVisible != visible || IsVisible != visible)
        {
            Live2DLog.Write($"avatar visible={visible} ready={_ready} active={_renderActive}");
        }
        _wantedVisible = visible;
        if (visible && _ready && Owner is { IsVisible: true })
        {
            ResumeRendering();
            SyncToOwner();
            Opacity = 1;
            Show();
            _renderActive = true;
            SendHostMessage(new { type = "visibility", visible = true });
            NotifyResize();
        }
        else
        {
            Opacity = 0;
            Hide();
            _renderActive = false;
            SendHostMessage(new { type = "visibility", visible = false });
            SuspendRenderingSoon();
        }
    }

    /// <summary>Hides for a temporary host state (edge collapse/minimize)
    /// without clearing the desired avatar visibility.</summary>
    public void SetTemporarilyHidden(bool hidden)
    {
        if (hidden)
        {
            Opacity = 0;
            Hide();
            _renderActive = false;
            SendHostMessage(new { type = "visibility", visible = false });
            SuspendRenderingSoon();
            return;
        }
        if (_wantedVisible && _ready && Owner is { IsVisible: true } &&
            Owner.WindowState != WindowState.Minimized)
        {
            ResumeRendering();
            SyncToOwner();
            Opacity = 1;
            if (!IsVisible) Show();
            _renderActive = true;
            SendHostMessage(new { type = "visibility", visible = true });
            NotifyResize();
        }
    }

    public void NotifyResize()
    {
        if (!_ready) return;
        SendHostMessage(new { type = "resize" });
    }

    public void NotifyLayout(double x, double y, double width, double height)
    {
        if (!_ready) return;
        SendHostMessage(new { type = "layout", x, y, width, height });
    }

    public void SyncAndResize()
    {
        if (!_ready || !_wantedVisible || Owner is not { IsVisible: true } ||
            Owner.WindowState == WindowState.Minimized)
        {
            return;
        }
        ResumeRendering();
        SyncToOwner();
        Opacity = 1;
        if (!IsVisible) Show();
        _renderActive = true;
        NotifyResize();
    }

    public void PrepareForInitialization()
    {
        SyncToOwner();
        Opacity = 0;
        if (Owner is { IsVisible: true } && !IsVisible) Show();
    }

    /// <summary>WebView2's documented power optimisation: a hidden rendering
    /// process is suspended (which also drops MemoryUsageTargetLevel to Low)
    /// and resumed when the avatar becomes visible again.</summary>
    private void SuspendRenderingSoon()
    {
        if (!_webViewInitialized || _disposed) return;
        _ = SuspendRenderingAsync();
    }

    private async Task SuspendRenderingAsync()
    {
        if (_suspended || _suspendInFlight || _renderActive || _disposed) return;
        _suspendInFlight = true;
        try
        {
            // Hide() is asynchronous at the HWND level; give it a beat before
            // asking WebView2 to suspend a still-visible surface.
            await Task.Delay(80);
            if (_renderActive || _disposed || Browser.CoreWebView2 is not { } core) return;
            var suspended = await core.TrySuspendAsync();
            _suspended = suspended;
            Live2DLog.Write("webview suspend=" + suspended);
        }
        catch (Exception error)
        {
            Live2DLog.Write("webview suspend failed: " + error.Message);
        }
        finally
        {
            _suspendInFlight = false;
        }
    }

    private void ResumeRendering()
    {
        if (!_suspended || !_webViewInitialized || _disposed) return;
        try
        {
            Browser.CoreWebView2.Resume();
            _suspended = false;
            Live2DLog.Write("webview resume");
        }
        catch (Exception error)
        {
            Live2DLog.Write("webview resume failed: " + error.Message);
        }
    }

    public async Task<bool> EnsureInitializedAsync()
    {
        if (_disposed) return false;
        if (_ready) return true;
        if (_initialization is not null) return await _initialization;

        _initialization = InitializeCoreAsync();
        return await _initialization;
    }

    private async Task<bool> InitializeCoreAsync()
    {
        try
        {
            Live2DLog.Write("WebView2 init start");
            var dataFolder = Path.Combine(PersistentStore.LocalDirectory, "WebView2");
            Directory.CreateDirectory(dataFolder);
            var contentRoot = Path.Combine(AppContext.BaseDirectory, "Live2DWeb");
            Live2DLog.Write("content root=" + contentRoot + " exists=" + Directory.Exists(contentRoot));
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: dataFolder);
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            // app-region relies on the non-client-region support; disabling it
            // removes the Windows title-bar/system menu path for the canvas.
            Browser.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            Live2DLog.Write(
                $"webview settings nonClientRegion={Browser.CoreWebView2.Settings.IsNonClientRegionSupportEnabled} " +
                $"defaultContextMenu={Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled}");
            Browser.CoreWebView2.WebMessageReceived += OnWebMessage;
            Browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Browser.CoreWebView2.WebResourceResponseReceived += OnWebResourceResponseReceived;
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHost,
                contentRoot,
                CoreWebView2HostResourceAccessKind.DenyCors);

            _webViewInitialized = true;
            _readySource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Browser.CoreWebView2.Navigate($"https://{VirtualHost}/index.html");
            var completed = await Task.WhenAny(_readySource.Task, Task.Delay(TimeSpan.FromSeconds(45)));
            if (completed != _readySource.Task || !await _readySource.Task)
            {
                LoadFailed?.Invoke("Live2D 加载超时，已回退到默认形象");
                return false;
            }
            return true;
        }
        catch (Exception error)
        {
            Live2DLog.Write("WebView2 init failed: " + error);
            var message = error.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase)
                ? "Live2D 需要 WebView2 Runtime，请安装后重试"
                : "Live2D 初始化失败：" + error.Message;
            LoadFailed?.Invoke(message);
            return false;
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            Live2DLog.Write("navigation failed: " + e.WebErrorStatus);
            LoadFailed?.Invoke("Live2D 页面加载失败，已回退到默认形象");
            _readySource?.TrySetResult(false);
        }
    }

    private void OnWebResourceResponseReceived(object? sender,
        CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        try
        {
            var status = e.Response.StatusCode;
            var uri = e.Request.Uri;
            if (status != 200 || uri.Contains("model3.json", StringComparison.OrdinalIgnoreCase))
                Live2DLog.Write($"resource {status} {uri}");
        }
        catch { }
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement)) return;
            var type = typeElement.GetString();
            if (type == "modelReady")
            {
                _ready = true;
                Live2DLog.Write("model ready");
                if (root.TryGetProperty("hitAreas", out var hitAreas) &&
                    hitAreas.ValueKind == JsonValueKind.Array && hitAreas.GetArrayLength() == 0)
                {
                    Live2DLog.Write("model has no HitAreas; normalized part fallback is active");
                }
                _readySource?.TrySetResult(true);
                if (_wantedVisible && Owner is { IsVisible: true })
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ResumeRendering();
                        SyncToOwner();
                        Show();
                        _renderActive = true;
                        SendHostMessage(new { type = "visibility", visible = true });
                        NotifyResize();
                    }));
                }
            }
            else if (type == "modelError")
            {
                var message = root.TryGetProperty("message", out var value)
                    ? value.GetString()
                    : "Live2D 模型加载失败";
                Live2DLog.Write("model error: " + message);
                LoadFailed?.Invoke(message ?? "Live2D 模型加载失败");
                _readySource?.TrySetResult(false);
            }
            else if (type == "probe")
            {
                Live2DLog.Write("probe " + e.WebMessageAsJson);
            }
            else if (type == "pageError")
            {
                var message = root.TryGetProperty("message", out var value) ? value.GetString() : "";
                var stack = root.TryGetProperty("stack", out var stackValue) ? stackValue.GetString() : "";
                Live2DLog.Write("page error: " + message + " | " + stack);
                LoadFailed?.Invoke("Live2D 渲染异常，已回退到默认形象");
                _readySource?.TrySetResult(false);
            }
            else if (type == "rightClick")
            {
                RightClickRequested?.Invoke();
            }
            else if (type == "dragStart")
            {
                DragStarted?.Invoke(Number(root, "screenX"), Number(root, "screenY"));
            }
            else if (type == "dragMove")
            {
                DragMoved?.Invoke(Number(root, "screenX"), Number(root, "screenY"));
            }
            else if (type == "dragEnd")
            {
                DragEnded?.Invoke();
            }
            else if (type == "partClick")
            {
                var part = root.TryGetProperty("part", out var partValue) ? partValue.GetString() : "";
                if (!string.IsNullOrWhiteSpace(part)) PartClicked?.Invoke(part);
            }
            else if (type == "noHitAreas")
            {
                var message = root.TryGetProperty("message", out var value) ? value.GetString() : "";
                Live2DLog.Write("hit areas: " + message);
            }
            else if (type == "expressionError" || type == "randomMotionError")
            {
                Live2DLog.Write("web message: " + e.WebMessageAsJson);
            }
            else if (type == "expression")
            {
                var name = root.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : "";
                var action = root.TryGetProperty("action", out var actionValue) ? actionValue.GetString() : "";
                Live2DLog.Write($"expression {action} {name}");
            }
            else if (type == "fps")
            {
                var fps = root.TryGetProperty("fps", out var fpsValue) && fpsValue.TryGetDouble(out var number)
                    ? number
                    : 0;
                var target = root.TryGetProperty("target", out var targetValue) && targetValue.TryGetDouble(out var targetNumber)
                    ? targetNumber
                    : 0;
                Live2DLog.Write($"fps {fps:0.0} target={target:0}");
            }
            else if (type == "geometry")
            {
                if (root.TryGetProperty("bounds", out var boundsElement) &&
                    boundsElement.ValueKind == JsonValueKind.Object)
                {
                    var x = Number(boundsElement, "x");
                    var y = Number(boundsElement, "y");
                    var w = Number(boundsElement, "width");
                    var h = Number(boundsElement, "height");
                    if (w > 0 && h > 0)
                    {
                        ModelBounds = new Rect(
                            Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1),
                            Math.Clamp(w, 0, 1), Math.Clamp(h, 0, 1));
                    }
                }
                Live2DLog.Write("web geometry " + e.WebMessageAsJson);
            }
            else if (type == "randomMotion")
            {
                var group = root.TryGetProperty("group", out var groupValue) ? groupValue.GetString() : "";
                Live2DLog.Write("random motion: " + group);
            }
        }
        catch (Exception error)
        {
            Live2DLog.Write("message error: " + error.Message);
        }
    }

    private static double Number(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.TryGetDouble(out var number)
            ? number
            : 0;
    }

    private void SendHostMessage(object message)
    {
        if (!_ready || !_webViewInitialized) return;
        try
        {
            var json = JsonSerializer.Serialize(message);
            Browser.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch
        {
            // Input forwarding is best effort.
        }
    }

    public void SendInput(object message) => SendHostMessage(message);

    public void SendExpression(string name) => SendHostMessage(new { type = "expression", name });

    public void DisposeAndClose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (Browser.CoreWebView2 is { } core)
            {
                core.WebMessageReceived -= OnWebMessage;
                core.NavigationCompleted -= OnNavigationCompleted;
                core.WebResourceResponseReceived -= OnWebResourceResponseReceived;
                try { core.Stop(); } catch { }
            }
            Browser.Dispose();
        }
        catch
        {
            // Shutdown must not be blocked by a failed WebView2 teardown.
        }
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        try { _source?.RemoveHook(WndProc); } catch { }
        _source = null;
        try
        {
            if (!_disposed && Browser.CoreWebView2 is { } core)
            {
                core.WebMessageReceived -= OnWebMessage;
                core.NavigationCompleted -= OnNavigationCompleted;
                core.WebResourceResponseReceived -= OnWebResourceResponseReceived;
                try { core.Stop(); } catch { }
            }
        }
        catch { }
        try { Browser.Dispose(); } catch { }
    }
}
