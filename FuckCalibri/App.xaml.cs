using System;
using System.Linq;
using System.Windows;
using Wpf.Ui.Appearance;

namespace FuckCalibri
{
    public partial class App : Application
    {
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                try { System.IO.File.WriteAllText("D:\\Develope\\Win32 app develope\\FuckCalibri.net\\crash.log", ev.ExceptionObject.ToString()); } catch { }
            };
            DispatcherUnhandledException += (s, ev) =>
            {
                try { System.IO.File.WriteAllText("D:\\Develope\\Win32 app develope\\FuckCalibri.net\\crash.log", ev.Exception.ToString()); } catch { }
            };

            base.OnStartup(e);

            var cmdArgs = Environment.GetCommandLineArgs();
            bool silent = cmdArgs.Any(a => string.Equals(a, Constants.LaunchArguments.Silent, StringComparison.OrdinalIgnoreCase));
            bool show = cmdArgs.Any(a => string.Equals(a, Constants.LaunchArguments.Show, StringComparison.OrdinalIgnoreCase));

            SingleInstanceHelper.Check(silent);
            if (!SingleInstanceHelper.IsSingleInstance)
            {
                if (!silent)
                {
                    SingleInstanceHelper.Notify();
                }
                Shutdown();
                return;
            }

            // 初始化 WPF-UI 主题跟随系统
            try
            {
                ApplicationThemeManager.ApplySystemTheme();
            }
            catch { }

            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;

            // 无论是否静默，都确保主窗口 HWND 与后台监测逻辑完整初始化
            _mainWindow.EnsureInitialized();

            // 若带有 /silent 且未显式指定 /show，则直接托盘后台运行，不弹出窗口
            if (!silent || show)
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
            else
            {
                // 开机自启动静默模式：直接在托盘常驻，主动修剪工作集内存至 ~7MB
                WinApi.TrimWorkingSet();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SingleInstanceHelper.Release();
            base.OnExit(e);
        }
    }
}
