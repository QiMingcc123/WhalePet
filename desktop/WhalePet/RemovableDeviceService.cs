using System.Diagnostics;
using System.IO;

namespace WhalePet;

/// <summary>Finds removable USB disks, including external hard drives reported as fixed volumes.</summary>
public static class RemovableDeviceService
{
    public static HashSet<string> FindDriveLetters()
    {
        var letters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Removable)
                    {
                        letters.Add(drive.Name.TrimEnd('\\'));
                    }
                }
                catch
                {
                    // Ignore drives that disappear.
                }
            }

            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -WindowStyle Hidden -Command \"Get-Disk | Where-Object { $_.BusType -eq 'USB' } | Get-Partition | Where-Object { $_.DriveLetter } | ForEach-Object { $_.DriveLetter }\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start);
            if (process is not null && process.WaitForExit(5000))
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = process.StandardOutput.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(line) && line.Length == 1 && char.IsLetter(line[0]))
                    {
                        letters.Add(line.ToUpperInvariant() + ":");
                    }
                }
            }
            else
            {
                try { process?.Kill(entireProcessTree: true); } catch { }
            }
        }
        catch
        {
            // PowerShell/Get-Disk may be unavailable; removable volumes above still work.
        }
        return letters;
    }
}
