using System.IO;
using System.Security;
using Microsoft.Win32;

namespace WhalePet;

/// <summary>Registers the single-file executable under the current user's Run key.</summary>
public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DSHWhalePet";

    public static string ExecutablePath
    {
        get
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath)
                && !string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return processPath;
            }
            return Path.Combine(AppContext.BaseDirectory, "WhalePet.exe");
        }
    }

    public static bool SetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "无法打开当前用户的开机启动注册表项。";
                return false;
            }

            if (enabled)
            {
                var executable = ExecutablePath;
                if (!File.Exists(executable))
                {
                    error = "找不到 WhalePet.exe，无法设置开机自启动。";
                    return false;
                }
                key.SetValue(ValueName, "\"" + executable + "\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            error = "当前用户没有修改开机启动项的权限。";
            return false;
        }
        catch (SecurityException)
        {
            error = "当前用户没有修改开机启动项的权限。";
            return false;
        }
        catch (Exception)
        {
            error = "写入开机启动项失败，请检查系统安全策略。";
            return false;
        }
    }

    public static void SyncSilently(bool enabled)
    {
        if (!enabled) return;
        _ = SetEnabled(true, out _);
    }
}
