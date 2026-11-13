using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WhalePet;

public readonly record struct EjectResult(bool Success, string Message);

/// <summary>
/// Ejects a USB disk the same way Windows' "safely remove hardware" does:
/// every mounted volume on the disk is locked and dismounted, then the USB
/// device node is asked to eject through the PnP configuration manager.
/// </summary>
public static class DeviceEjector
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    private const uint IoctlStorageGetDeviceNumber = 0x002D1080;
    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlDismountVolume = 0x00090020;

    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;

    private static readonly Guid DiskInterfaceClass = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");

    public static EjectResult Eject(string driveRoot, bool dryRun = false)
    {
        var letter = NormalizeLetter(driveRoot);
        if (letter == '\0') return new EjectResult(false, "盘符无效，无法弹出。");
        var root = letter + ":";

        uint deviceNumber;
        using (var volume = OpenVolume(@"\\.\" + root, out _))
        {
            if (volume.IsInvalid) return new EjectResult(false, $"{root} 已不可访问，可能已经弹出。");
            if (!TryGetDeviceNumber(volume, out deviceNumber)) return new EjectResult(false, $"无法读取 {root} 的设备编号。");
        }

        var diskInst = FindDiskDevNode(deviceNumber);
        if (diskInst == 0) return new EjectResult(false, $"没有找到 {root} 对应的磁盘设备。");

        var targetInst = FindUsbAncestor(diskInst);
        if (targetInst == 0) targetInst = diskInst;

        if (dryRun)
        {
            var lockState = "未测试";
            using (var probe = OpenVolume(@"\\.\" + root, out var writable))
            {
                if (!probe.IsInvalid)
                {
                    var locked = writable && DeviceIoControl(probe, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                    lockState = locked ? "可锁定" : writable ? "被占用" : "无法写入";
                }
            }
            return new EjectResult(true,
                $"{root} 设备定位成功：磁盘节点 {GetDeviceId(diskInst)}，弹出节点 {GetDeviceId(targetInst)}，卷状态 {lockState}。");
        }

        // Lock and dismount every mounted volume that lives on this disk, the
        // same preparation "Safely Remove Hardware" performs.
        var handles = new List<SafeFileHandle>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    if (drive.DriveType is DriveType.Network or DriveType.CDRom or DriveType.NoRootDirectory) continue;

                    var handle = OpenVolume(@"\\.\" + drive.Name.TrimEnd('\\'), out var writable);
                    if (handle.IsInvalid)
                    {
                        handle.Dispose();
                        continue;
                    }
                    if (!TryGetDeviceNumber(handle, out var number) || number != deviceNumber)
                    {
                        handle.Dispose();
                        continue;
                    }

                    if (writable)
                    {
                        // Flush cached writes before the volume is locked.
                        FlushFileBuffers(handle);
                    }

                    if (writable
                        && !DeviceIoControl(handle, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                    {
                        handle.Dispose();
                        return new EjectResult(false,
                            $"{drive.Name.TrimEnd('\\')} 正被其他程序使用，请关闭相关文件或窗口后再弹出。");
                    }

                    DeviceIoControl(handle, FsctlDismountVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                    handles.Add(handle);
                }
                catch
                {
                    // A volume can vanish while it is being enumerated.
                }
            }

            // Close every volume handle before asking PnP to remove the device.
            // An outstanding open handle makes the volume devnode veto the
            // removal (CR_REMOVE_VETOED), even after it was dismounted.
            foreach (var handle in handles)
            {
                try { handle.Dispose(); } catch { }
            }
            handles.Clear();

            EjectResult failure = new(false, $"无法弹出 {root}。");
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var vetoName = new StringBuilder(260);
                var result = CM_Request_Device_Eject(targetInst, out var vetoType, vetoName, vetoName.Capacity, 0);
                if (result == 0)
                {
                    return new EjectResult(true, $"已安全弹出 {root}，现在可以拔下移动硬盘了。");
                }

                failure = new EjectResult(false, DescribeFailure(root, result, vetoType, vetoName.ToString()));
                if (attempt < 3) Thread.Sleep(600);
            }

            return failure;
        }
        catch (Exception error)
        {
            return new EjectResult(false, $"弹出 {root} 时出错：{error.Message}");
        }
        finally
        {
            foreach (var handle in handles)
            {
                try { handle.Dispose(); } catch { }
            }
        }
    }

    private static char NormalizeLetter(string? driveRoot)
    {
        var text = (driveRoot ?? "").Trim();
        if (text.Length == 0) return '\0';
        var colon = text.IndexOf(':');
        var candidate = colon == 1 ? text[0] : text.Length == 1 ? text[0] : '\0';
        return char.IsLetter(candidate) ? char.ToUpperInvariant(candidate) : '\0';
    }

    private static SafeFileHandle OpenVolume(string path, out bool writable)
    {
        var handle = CreateFile(path, GenericRead | GenericWrite,
            FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        writable = !handle.IsInvalid;
        if (handle.IsInvalid)
        {
            handle = CreateFile(path, GenericRead,
                FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        }
        if (handle.IsInvalid)
        {
            handle = CreateFile(path, 0,
                FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        }
        return handle;
    }

    private static bool TryGetDeviceNumber(SafeFileHandle handle, out uint deviceNumber)
    {
        deviceNumber = 0;
        var size = (uint)Marshal.SizeOf<StorageDeviceNumber>();
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (!DeviceIoControl(handle, IoctlStorageGetDeviceNumber, IntPtr.Zero, 0, buffer, size, out _, IntPtr.Zero))
            {
                return false;
            }
            var data = Marshal.PtrToStructure<StorageDeviceNumber>(buffer);
            deviceNumber = data.DeviceNumber;
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static uint FindDiskDevNode(uint deviceNumber)
    {
        var diskClass = DiskInterfaceClass;
        var set = SetupDiGetClassDevs(ref diskClass, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return 0;
        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    Size = (uint)Marshal.SizeOf<SpDeviceInterfaceData>(),
                };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref diskClass, index, ref interfaceData)) break;

                SetupDiGetDeviceInterfaceDetail(set, ref interfaceData, IntPtr.Zero, 0, out var required, IntPtr.Zero);
                if (required == 0) continue;

                var buffer = Marshal.AllocHGlobal((int)required);
                var infoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<SpDevInfoData>());
                try
                {
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    Marshal.StructureToPtr(new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() }, infoPointer, false);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref interfaceData, buffer, required, out _, infoPointer)) continue;

                    var path = Marshal.PtrToStringUni(IntPtr.Add(buffer, 4));
                    if (string.IsNullOrEmpty(path)) continue;

                    using var disk = CreateFile(path, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                    if (disk.IsInvalid) continue;
                    if (!TryGetDeviceNumber(disk, out var number) || number != deviceNumber) continue;

                    return Marshal.PtrToStructure<SpDevInfoData>(infoPointer).DevInst;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                    Marshal.FreeHGlobal(infoPointer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
        return 0;
    }

    private static uint FindUsbAncestor(uint devInst)
    {
        var current = devInst;
        for (var depth = 0; depth < 8; depth++)
        {
            if (CM_Get_Parent(out var parent, current, 0) != 0) break;
            current = parent;
            var id = GetDeviceId(current);
            if (id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)
                && !id.StartsWith("USB\\ROOT_HUB", StringComparison.OrdinalIgnoreCase)
                && (id.Contains("VID_", StringComparison.OrdinalIgnoreCase) || id.Contains("PID_", StringComparison.OrdinalIgnoreCase)))
            {
                return current;
            }
        }
        return 0;
    }

    private static string GetDeviceId(uint devInst)
    {
        var buffer = new StringBuilder(512);
        return CM_Get_Device_ID(devInst, buffer, buffer.Capacity, 0) == 0 ? buffer.ToString() : "";
    }

    private static string DescribeFailure(string root, uint result, int vetoType, string vetoName)
    {
        var name = (vetoName ?? "").Trim('\0', ' ', '\r', '\n', '\t');
        if (name.StartsWith(@"STORAGE\Volume\", StringComparison.OrdinalIgnoreCase))
        {
            name = "移动硬盘上的一个分区";
        }
        var detail = string.IsNullOrWhiteSpace(name) ? "" : $"（{name}）";
        var reason = vetoType switch
        {
            2 or 3 or 4 => $"有程序仍在使用 {root}{detail}",
            5 => $"{root} 还有未关闭的文件{detail}",
            9 => "设备电源不足，暂时无法弹出",
            12 => "权限不足，请用管理员身份运行后再试",
            _ => $"系统拒绝了弹出请求{detail}",
        };
        return $"{reason}。请关闭相关程序后重试（错误码 0x{result:X}）。";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StorageDeviceNumber
    {
        public uint DeviceType;
        public uint DeviceNumber;
        public uint PartitionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(SafeFileHandle file);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        IntPtr inBuffer,
        uint inBufferSize,
        IntPtr outBuffer,
        uint outBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr parentWindow,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Request_Device_Eject(
        uint devInst,
        out int vetoType,
        StringBuilder vetoName,
        int nameLength,
        int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Parent(out uint parent, uint devInst, int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID(uint devInst, StringBuilder buffer, int bufferLength, int flags);
}
