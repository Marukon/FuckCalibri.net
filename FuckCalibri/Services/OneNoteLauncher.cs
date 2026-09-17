using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FuckCalibri.Services
{
    public static class OneNoteLauncher
    {
        private static readonly string[] CommonPaths =
        [
            @"C:\Program Files\Microsoft Office\root\Office16\ONENOTE.EXE",
            @"C:\Program Files (x86)\Microsoft Office\root\Office16\ONENOTE.EXE",
            @"C:\Program Files\Microsoft Office\Office16\ONENOTE.EXE",
            @"C:\Program Files (x86)\Microsoft Office\Office16\ONENOTE.EXE",
            @"C:\Program Files\Microsoft Office\Office15\ONENOTE.EXE",
            @"C:\Program Files (x86)\Microsoft Office\Office15\ONENOTE.EXE"
        ];

        public static string? FindOneNoteExecutablePath()
        {
            // 1. 尝试从注册表 App Paths 读取
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\onenote.exe")
                             ?? Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\onenote.exe");
                if (key != null)
                {
                    var val = key.GetValue(null)?.ToString();
                    if (!string.IsNullOrWhiteSpace(val) && File.Exists(val))
                    {
                        return val;
                    }
                }
            }
            catch { }

            // 2. 尝试从常见 Office 安装目录查找
            foreach (var path in CommonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        public static (bool Success, string Message) LaunchOrActivate()
        {
            try
            {
                // 1. 如果 OneNote 已经在运行，将其前置并唤醒窗口
                var processes = Process.GetProcessesByName(Constants.TargetProcessName);
                if (processes.Length > 0)
                {
                    var proc = processes[0];
                    var hWnd = proc.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        WinApi.ShowWindowAsync(hWnd, WinApi.SW_RESTORE);
                        WinApi.SetForegroundWindow(hWnd);
                        return (true, $"OneNote (PID: {proc.Id}) 已在运行，已唤醒至前台。");
                    }
                    return (true, $"OneNote (PID: {proc.Id}) 正在运行中。");
                }

                // 2. 如果未运行，尝试通过物理路径启动
                var exePath = FindOneNoteExecutablePath();
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                    return (true, "已成功启动 OneNote。");
                }

                // 3. Fallback：通过 onenote: 协议拉起
                Process.Start(new ProcessStartInfo("onenote:") { UseShellExecute = true });
                return (true, "已通过系统协议拉起 OneNote。");
            }
            catch (Exception ex)
            {
                return (false, $"启动 OneNote 失败: {ex.Message}");
            }
        }
    }
}
