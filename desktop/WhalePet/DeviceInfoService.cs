using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WhalePet;

public sealed record DeviceSnapshot(string Memory, string Cpu, string Gpu, string Disk, string Network);

/// <summary>Best-effort local device information. Missing sensors are simply omitted.</summary>
public static class DeviceInfoService
{
    private static ulong _lastIdle;
    private static ulong _lastTotal;
    private static bool _hasCpuBaseline;

    private static long _lastBytesReceived;
    private static long _lastBytesSent;
    private static DateTime _lastNetworkSample;
    private static bool _hasNetworkBaseline;

    public static DeviceSnapshot Capture()
    {
        return new DeviceSnapshot(
            ReadMemory(),
            ReadCpu(),
            ReadGpu(),
            ReadDisk(),
            ReadNetwork());
    }

    private static string ReadMemory()
    {
        try
        {
            var status = new MemoryStatusEx();
            status.dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
            if (!GlobalMemoryStatusEx(ref status)) return "";
            var total = status.ullTotalPhys / 1024d / 1024d / 1024d;
            var available = status.ullAvailPhys / 1024d / 1024d / 1024d;
            var used = Math.Max(0, total - available);
            var percent = total > 0 ? used / total * 100 : 0;
            return $"内存 {used:0.0} / {total:0.0} GB · 已用 {percent:0}%";
        }
        catch
        {
            return "";
        }
    }

    private static string ReadCpu()
    {
        var name = "";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            name = (key?.GetValue("ProcessorNameString") as string ?? "").Trim();
        }
        catch
        {
            // Registry access is best effort.
        }

        var usage = ReadCpuUsage();
        if (string.IsNullOrWhiteSpace(name)) name = $"CPU · {Environment.ProcessorCount} 核";
        if (usage < 0) return $"{name} · {Environment.ProcessorCount} 核";
        return $"{name} · {usage:0}% · {Environment.ProcessorCount} 核";
    }

    private static double ReadCpuUsage()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user)) return -1;
            var idleTime = ToUInt64(idle);
            var total = ToUInt64(kernel) + ToUInt64(user);
            if (!_hasCpuBaseline)
            {
                _lastIdle = idleTime;
                _lastTotal = total;
                _hasCpuBaseline = true;
                return -1;
            }

            var idleDelta = idleTime - _lastIdle;
            var totalDelta = total - _lastTotal;
            _lastIdle = idleTime;
            _lastTotal = total;
            if (totalDelta == 0) return -1;
            return Math.Clamp((1d - idleDelta / (double)totalDelta) * 100d, 0, 100);
        }
        catch
        {
            return -1;
        }
    }

    private static string ReadGpu()
    {
        var smi = ReadNvidiaSmi();
        if (smi is not null) return smi;

        var name = ReadGpuName();
        return string.IsNullOrWhiteSpace(name) ? "" : name;
    }

    private static string? ReadNvidiaSmi()
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,utilization.gpu,temperature.gpu --format=csv,noheader,nounits",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start);
            if (process is null) return null;
            if (!process.WaitForExit(1500))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }
            var line = process.StandardOutput.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return null;
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3) return null;
            var name = parts[0];
            var usage = int.TryParse(parts[1], out var u) ? u : -1;
            var temp = int.TryParse(parts[2], out var t) ? t : -1;
            var detail = usage >= 0 ? $" · 使用 {usage}%" : "";
            if (temp >= 0) detail += $" · {temp}°C";
            return $"显卡 {name}{detail}";
        }
        catch
        {
            return null;
        }
    }

    private static string ReadGpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (key is null) return "";
            foreach (var childName in key.GetSubKeyNames())
            {
                if (!childName.All(char.IsDigit)) continue;
                using var child = key.OpenSubKey(childName);
                var desc = (child?.GetValue("DriverDesc") as string ?? "").Trim();
                if (desc.Length > 0 && !desc.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                {
                    return "显卡 " + desc;
                }
            }
        }
        catch
        {
            // Best effort.
        }
        return "";
    }

    private static string ReadDisk()
    {
        try
        {
            var parts = new List<string>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    var total = drive.TotalSize / 1024d / 1024d / 1024d;
                    var free = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                    var used = Math.Max(0, total - free);
                    var percent = total > 0 ? used / total * 100 : 0;
                    parts.Add($"{drive.Name.TrimEnd('\\')} 已用 {used:0.0} / {total:0.0} GB · {percent:0}%");
                }
                catch
                {
                    // A drive can disappear while enumerating.
                }
            }
            return parts.Count == 0 ? "" : "硬盘 " + string.Join("  |  ", parts);
        }
        catch
        {
            return "";
        }
    }

    private static string ReadNetwork()
    {
        try
        {
            NetworkInterface? best = null;
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                try
                {
                    var hasGateway = adapter.GetIPProperties().GatewayAddresses
                        .Any(gateway => gateway.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                        && !gateway.Address.Equals(System.Net.IPAddress.Any));
                    if (!hasGateway) continue;
                }
                catch
                {
                    continue;
                }

                if (best is null || NetworkRank(adapter) < NetworkRank(best)) best = adapter;
            }

            if (best is null)
            {
                _hasNetworkBaseline = false;
                return "网络 未连接";
            }

            long received;
            long sent;
            try
            {
                var stats = best.GetIPv4Statistics();
                received = stats.BytesReceived;
                sent = stats.BytesSent;
            }
            catch
            {
                return "网络 已连接";
            }

            var typeName = best.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet => "以太网",
                _ => "网络",
            };

            var now = DateTime.Now;
            var status = $"网络 {typeName} 已连接";
            if (!_hasNetworkBaseline || (now - _lastNetworkSample).TotalSeconds <= 0)
            {
                _hasNetworkBaseline = true;
                _lastBytesReceived = received;
                _lastBytesSent = sent;
                _lastNetworkSample = now;
                return status + " · ↓ — ↑ —";
            }

            var seconds = Math.Max(0.5, (now - _lastNetworkSample).TotalSeconds);
            var down = Math.Max(0, received - _lastBytesReceived) / seconds;
            var up = Math.Max(0, sent - _lastBytesSent) / seconds;
            _lastBytesReceived = received;
            _lastBytesSent = sent;
            _lastNetworkSample = now;
            return status + $" · ↓ {FormatSpeed(down)} ↑ {FormatSpeed(up)}";
        }
        catch
        {
            return "";
        }
    }

    private static int NetworkRank(NetworkInterface adapter) => adapter.NetworkInterfaceType switch
    {
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet => 0,
        NetworkInterfaceType.Wireless80211 => 1,
        _ => 2,
    };

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024d * 1024d) return $"{bytesPerSecond / 1024d / 1024d:0.00} MB/s";
        if (bytesPerSecond >= 1024d) return $"{bytesPerSecond / 1024d:0} KB/s";
        return $"{bytesPerSecond:0} B/s";
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.dwHighDateTime << 32) | value.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
}
