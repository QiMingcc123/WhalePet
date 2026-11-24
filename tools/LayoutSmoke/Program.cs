using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WhalePet;
using WhalePet.Game;

namespace LayoutSmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var temp = Path.Combine(Path.GetTempPath(), "whalepet-layout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            var catalog = new GifCatalog();
            var store = new GameStore(temp);
            var engine = new GameEngine(store, catalog);
            engine.Initialize();
            engine.AddAffection(250);
            foreach (var id in engine.UnlockedGifs.OrderBy(id => id).Take(2))
            {
                engine.EnableGif(id);
            }
            var gifs = new GifProvider(catalog, engine.IsGifUnlocked);
            SetStatic("Store", store);
            SetStatic("Catalog", catalog);
            SetStatic("Engine", engine);
            SetStatic("Gifs", gifs);
            SetStatic("Initialized", true);

            var settings = PetSettings.CreateDefault();
            settings.PanelScale = 1;
            settings.PanelOpacity = 1;
            settings.PanelLeft = 120;
            settings.PanelTop = 140;
            var saved = (0.0, 0.0);
            var panel = new CharacterPanelWindow(settings);
            panel.PositionChanged += (left, top) => saved = (left, top);
            panel.Show();
            Pump(panel);
            Dump(panel, "scale1");
            panel.Left = 200;
            panel.Top = 210;
            Invoke(panel, "SavePosition");
            Console.WriteLine($"position={panel.Left:0},{panel.Top:0} saved={saved.Item1:0},{saved.Item2:0}");
            Render(panel, "panel-current-1080.png");
            var tabs = (TabControl)panel.FindName("MainTabs");
            tabs.SelectedIndex = 3;
            Pump(panel);
            Render(panel, "panel-current-backpack.png");
            var innerScroll = FindScroll(tabs);
            if (innerScroll is not null)
            {
                innerScroll.ScrollToVerticalOffset(innerScroll.ScrollableHeight);
                Pump(panel);
                Render(panel, "panel-current-wall.png");
            }

            panel.Width = 540;
            panel.Height = 370;
            Pump(panel);
            Dump(panel, "width540");
            Render(panel, "panel-current-540.png");

            panel.Width = 400;
            panel.Height = 370;
            Pump(panel);
            Dump(panel, "width400");
            Render(panel, "panel-current-400.png");

            panel.Left = -500;
            panel.Top = -500;
            Invoke(panel, "ClampToWorkArea");
            Console.WriteLine($"clamped={panel.Left:0},{panel.Top:0}");

            var nameMethod = typeof(CharacterPanelWindow).GetMethod(
                "GifDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var id in new[] { "067_boss.gif", "067_boss_01.gif", "Fishing.gif", "  A_B_C.gif  " })
            {
                Console.WriteLine($"name[{id.Trim()}]={nameMethod?.Invoke(null, new object[] { id })}");
            }

            panel.Close();

            engine.Save();
            store.Dispose();
            try { Directory.Delete(temp, true); } catch { }
            Environment.Exit(0);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Environment.ExitCode = 1;
        }
    }

    private static void SetStatic(string name, object? value)
    {
        typeof(GameRuntime)
            .GetField($"<{name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, value);
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(target, null);
    }

    private static ScrollViewer? FindScroll(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer && viewer.ScrollableHeight > 0) return viewer;
            var nested = FindScroll(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();
    }

    private static void Dump(Window window, string label)
    {
        var grid = (Grid)window.FindName("ResponsiveGrid");
        var tabs = (TabControl)window.FindName("MainTabs");
        var left = (Border)window.FindName("LeftCard");
        var scroll = (ScrollViewer)window.FindName("BodyScroll");
        Console.WriteLine(
            $"{label}: window={window.ActualWidth:0}x{window.ActualHeight:0} " +
            $"grid={grid.ActualWidth:0}x{grid.ActualHeight:0} " +
            $"tabsRow={Grid.GetRow(tabs)} tabsSpan={Grid.GetColumnSpan(tabs)} " +
            $"leftSpan={Grid.GetColumnSpan(left)} scrollW={scroll.ScrollableWidth:0} " +
            $"scrollH={scroll.ScrollableHeight:0} hbar={scroll.ComputedHorizontalScrollBarVisibility} " +
            $"vbar={scroll.ComputedVerticalScrollBarVisibility}");
    }

    private static void Render(Window window, string name)
    {
        window.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "..", "verification")),
            name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
        Console.WriteLine("render=" + path);
    }
}
