using System;
using Microsoft.Win32;

namespace FuckCalibri.Services
{
    public enum MonitoringMode
    {
        EventDriven = 0,      // 方案 A：窗口事件即时响应 (Shell Hook, 推荐 · 默认)
        PeriodicPolling = 1   // 方案 B：后台周期定时轮询 (1.5秒扫描)
    }

    public static class AppSettings
    {
        private const string RegSubKey = @"Software\FuckCalibri";
        private const string KeyMonitoringMode = "MonitoringMode";
        private const string KeyMinimizeToTray = "MinimizeToTray";

        public static MonitoringMode MonitoringMode
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegSubKey, false);
                    if (key != null)
                    {
                        var val = key.GetValue(KeyMonitoringMode);
                        if (val is int intVal && Enum.IsDefined(typeof(MonitoringMode), intVal))
                        {
                            return (MonitoringMode)intVal;
                        }
                    }
                }
                catch { }
                return MonitoringMode.EventDriven; // 默认方案 A (事件驱动)
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(RegSubKey, true);
                    key?.SetValue(KeyMonitoringMode, (int)value, RegistryValueKind.DWord);
                }
                catch { }
            }
        }

        public static bool MinimizeToTray
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegSubKey, false);
                    if (key != null)
                    {
                        var val = key.GetValue(KeyMinimizeToTray);
                        if (val is int intVal)
                        {
                            return intVal != 0;
                        }
                    }
                }
                catch { }
                return true; // 默认开启最小化到托盘
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(RegSubKey, true);
                    key?.SetValue(KeyMinimizeToTray, value ? 1 : 0, RegistryValueKind.DWord);
                }
                catch { }
            }
        }
    }
}
