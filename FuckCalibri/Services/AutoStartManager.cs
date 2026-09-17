using System;
using Microsoft.Win32;

namespace FuckCalibri.Services
{
    public static class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRunName = "FuckCalibri";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppRunName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return false;

                if (enable)
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        var command = $"\"{exePath}\" {Constants.LaunchArguments.Silent}";
                        key.SetValue(AppRunName, command);
                        return true;
                    }
                    return false;
                }
                else
                {
                    if (key.GetValue(AppRunName) != null)
                    {
                        key.DeleteValue(AppRunName, false);
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
