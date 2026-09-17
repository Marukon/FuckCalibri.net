using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FuckCalibri.Models;
using FuckCalibri.Services;
using Wpf.Ui.Controls;

namespace FuckCalibri
{
    public partial class MainWindow : FluentWindow
    {
        private readonly OnenoteMemoryPatcher _patcher;
        private readonly CancellationTokenSource _cts = new();
        private bool _isExplicitExit = false;

        private uint _shellHookMsg = 0;
        private IntPtr _hwnd = IntPtr.Zero;

        public MainWindow()
        {
            InitializeComponent();

            _patcher = new OnenoteMemoryPatcher();
            _patcher.StateChanged += OnPatcherStateChanged;

            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            StateChanged += OnWindowStateChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (_hwnd == IntPtr.Zero)
            {
                _hwnd = new WindowInteropHelper(this).Handle;

                // 挂载 Win32 消息钩子
                var source = PresentationSource.FromVisual(this) as HwndSource;
                source?.AddHook(WndProc);

                // 注册 Windows Shell 窗口消息钩子 (方案 A)
                WinApi.RegisterShellHookWindow(_hwnd);
                _shellHookMsg = WinApi.RegisterWindowMessage("SHELLHOOK");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (SingleInstanceHelper.ReactToNotification && SingleInstanceHelper.IsMutexMessage(msg))
            {
                handled = true;
                Dispatcher.InvokeAsync(BringToFront);
                return IntPtr.Zero;
            }

            // 处理 Windows Shell 窗口消息 (方案 A)
            if (_shellHookMsg > 0 && msg == (int)_shellHookMsg)
            {
                if (_patcher.Mode == MonitoringMode.EventDriven)
                {
                    _patcher.NotifyWindowChanged((int)wParam, lParam);
                }
            }
            return IntPtr.Zero;
        }

        private bool _isCoreInitialized = false;

        public void EnsureInitialized()
        {
            if (_isCoreInitialized) return;
            _isCoreInitialized = true;

            // 确保底层 Win32 HWND 已创建，保证消息循环与钩子挂载
            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero)
            {
                helper.EnsureHandle();
            }

            // 初始化自启动设置
            var autoStartEnabled = AutoStartManager.IsAutoStartEnabled();
            UpdateAutoStartVisual(autoStartEnabled);

            // 初始化最小化到托盘设置
            var minimizeEnabled = AppSettings.MinimizeToTray;
            UpdateMinimizeToTrayVisual(minimizeEnabled);

            // 初始化方案选择 (默认方案 A)
            var currentMode = AppSettings.MonitoringMode;
            UpdateSchemeVisual(currentMode);

            // 更新当前内存占用显示
            UpdateMemoryUsageDisplay();

            // 启动后台持续监听任务
            Task.Run(() => _patcher.StartAsync(_cts.Token));

            // 初始立即扫描一次
            _patcher.ScanAndPatchNow();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            EnsureInitialized();
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && ChkMinimizeToTray.IsChecked == true)
            {
                Hide();
                WinApi.TrimWorkingSet();
                UpdateMemoryUsageDisplay();
            }
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (!_isExplicitExit && ChkMinimizeToTray.IsChecked == true)
            {
                e.Cancel = true;
                Hide();
                // 窗口隐藏后主动修剪工作集内存
                WinApi.TrimWorkingSet();
                UpdateMemoryUsageDisplay();
            }
            else
            {
                _cts.Cancel();
                if (_hwnd != IntPtr.Zero)
                {
                    WinApi.DeregisterShellHookWindow(_hwnd);
                }
                TrayIcon.Dispose();
            }
        }

        private void OnPatcherStateChanged(object? sender, PatchStateInfo state)
        {
            Dispatcher.InvokeAsync(() => UpdateUiState(state));
        }

        private void UpdateUiState(PatchStateInfo state)
        {
            UpdateMemoryUsageDisplay();

            switch (state.Status)
            {
                case PatchStatus.Patched:
                    TxtStatusBadgeIcon.Text = "✅";
                    TxtStatusTitle.Text = "已生效";
                    TxtStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                    BadgePillPatched.Visibility = Visibility.Visible;
                    BadgePillPatched.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7));
                    TxtBadgeCount.Foreground = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
                    TxtBadgeCount.Text = $"有效保护 {state.PatchedCount} 处特征码";

                    TxtStatusSubtitle.Text = "OneNote 内部强制 Calibri 字体回退逻辑已被精准拦截。";

                    TxtProcessPidTag.Text = $"PID: {state.ProcessId} (64位)";
                    TxtMetricProcess.Text = $"ONENOTE.EXE (PID: {state.ProcessId})";
                    TxtMetricModule.Text = state.ModuleName ?? "onmain.dll";
                    TxtMetricPatched.Text = $"{state.PatchedCount} 处特征已修补拦截 (0x300 / 0x101)";
                    TxtMetricPatched.Foreground = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));

                    TagRealtimeProtect.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7));
                    TxtRealtimeProtect.Foreground = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
                    TxtRealtimeProtect.Text = "✔ 实时拦截已就绪";

                    DotStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    TxtBottomStatus.Text = $"守护就绪  OneNote 桌面版已受保护 (PID: {state.ProcessId})";

                    UpdateTrayVisual("green", $"FuckCalibri.net - 已生效 (保护 {state.PatchedCount} 处特征)");
                    break;

                case PatchStatus.NotRunning:
                    TxtStatusBadgeIcon.Text = "⚪";
                    TxtStatusTitle.Text = "OneNote 未运行";
                    TxtStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                    BadgePillPatched.Visibility = Visibility.Collapsed;

                    TxtStatusSubtitle.Text = "OneNote 尚未启动。本程序在后台实时守候，将在启动瞬间毫秒级自动拦截。";

                    TxtProcessPidTag.Text = "未运行";
                    TxtMetricProcess.Text = "未运行 (等待 OneNote 启动)";
                    TxtMetricModule.Text = "尚未加载";
                    TxtMetricPatched.Text = "待机中 (0 处)";
                    TxtMetricPatched.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

                    TagRealtimeProtect.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                    TxtRealtimeProtect.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                    TxtRealtimeProtect.Text = "⚪ 等待 OneNote 启动";

                    DotStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    TxtBottomStatus.Text = "待机中  等待 OneNote 启动";

                    UpdateTrayVisual("gray", "FuckCalibri.net - OneNote 未运行 (守候中)");
                    break;

                case PatchStatus.WaitingModule:
                    TxtStatusBadgeIcon.Text = "⏳";
                    TxtStatusTitle.Text = "正在初始化";
                    TxtStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                    BadgePillPatched.Visibility = Visibility.Collapsed;

                    TxtStatusSubtitle.Text = state.Message;
                    TxtProcessPidTag.Text = $"PID: {state.ProcessId}";
                    TxtMetricProcess.Text = $"ONENOTE.EXE (PID: {state.ProcessId})";
                    TxtMetricModule.Text = "模块加载中...";
                    TxtMetricPatched.Text = "等待核心模块";
                    TxtMetricPatched.Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));

                    DotStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    TxtBottomStatus.Text = $"正在检测  OneNote 正在加载 onmain.dll...";
                    UpdateTrayVisual("gray", "FuckCalibri.net - 正在等待 OneNote 核心模块加载...");
                    break;

                case PatchStatus.Unsupported:
                    TxtStatusBadgeIcon.Text = "⚠️";
                    TxtStatusTitle.Text = "未匹配到特征码 (未生效)";
                    TxtStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09));
                    BadgePillPatched.Visibility = Visibility.Visible;
                    BadgePillPatched.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
                    TxtBadgeCount.Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09));
                    TxtBadgeCount.Text = "特征不匹配";

                    TxtStatusSubtitle.Text = $"在模块 {state.ModuleName} 中未找到已知特征码，版本可能不受支持或特征码发生变更。";

                    TxtProcessPidTag.Text = $"PID: {state.ProcessId}";
                    TxtMetricProcess.Text = $"ONENOTE.EXE (PID: {state.ProcessId})";
                    TxtMetricModule.Text = state.ModuleName ?? "onmain.dll";
                    TxtMetricPatched.Text = "0 处匹配 (未生效)";
                    TxtMetricPatched.Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09));

                    TagRealtimeProtect.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
                    TxtRealtimeProtect.Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09));
                    TxtRealtimeProtect.Text = "⚠️ 特征不匹配";

                    DotStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    TxtBottomStatus.Text = "未生效  未匹配到 OneNote 字体回退特征码";
                    UpdateTrayVisual("blue", "FuckCalibri.net - 未匹配到特征码 (未生效)");
                    break;

                case PatchStatus.AccessDenied:
                case PatchStatus.Error:
                default:
                    TxtStatusBadgeIcon.Text = "❌";
                    TxtStatusTitle.Text = "异常状态";
                    TxtStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    BadgePillPatched.Visibility = Visibility.Collapsed;

                    TxtStatusSubtitle.Text = state.Message;
                    TxtMetricProcess.Text = $"ONENOTE.EXE (PID: {state.ProcessId})";
                    TxtMetricModule.Text = state.ModuleName ?? "onmain.dll";
                    TxtMetricPatched.Text = "错误";
                    TxtMetricPatched.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));

                    DotStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    TxtBottomStatus.Text = $"错误  {state.Message}";
                    UpdateTrayVisual("blue", "FuckCalibri.net - 检测遇到错误");
                    break;
            }

            // 若注入拦截失败，自动弹窗警示用户
            CheckAndAlertFailure(state);
        }

        private int _lastAlertedFailurePid = 0;
        private PatchStatus _lastAlertedFailureStatus = PatchStatus.NotRunning;

        private void CheckAndAlertFailure(PatchStateInfo state)
        {
            if (state.Status == PatchStatus.Patched)
            {
                // 成功注入生效，重置失败记录
                _lastAlertedFailurePid = 0;
                _lastAlertedFailureStatus = PatchStatus.NotRunning;
                return;
            }

            // 正常待机或初始化过渡状态不予弹窗
            if (state.Status == PatchStatus.NotRunning || state.Status == PatchStatus.WaitingModule || state.Status == PatchStatus.Patching)
            {
                return;
            }

            // 同一 OneNote 实例的同一失败状态只弹窗一次，避免周期轮询时循环弹窗打扰
            if (state.ProcessId > 0 && (state.ProcessId != _lastAlertedFailurePid || state.Status != _lastAlertedFailureStatus))
            {
                _lastAlertedFailurePid = state.ProcessId;
                _lastAlertedFailureStatus = state.Status;

                string alertTitle = "FuckCalibri.net - 注入拦截失败提醒";
                string alertContent = "";

                switch (state.Status)
                {
                    case PatchStatus.Unsupported:
                        alertContent = $"【OneNote 字体拦截失败】\n\n" +
                                       $"已检测到 OneNote 正在运行 (PID: {state.ProcessId})，但在其模块 ({state.ModuleName ?? "onmain.dll"}) 中未匹配到已知的 Calibri 回退特征码。\n\n" +
                                       $"可能原因：\n" +
                                       $"1. 当前使用的 OneNote 版本较新或为未适配的分支版本；\n" +
                                       $"2. 内部排版代码段发生变动，特征码发生偏移。\n\n" +
                                       $"当前未能成功注入拦截，输入时可能仍会出现字体跳回 Calibri 的现象。";
                        break;

                    case PatchStatus.AccessDenied:
                        alertContent = $"【OneNote 内存访问权限不足】\n\n" +
                                       $"已检测到 OneNote 进程 (PID: {state.ProcessId})，但本程序权限不足，无法访问其进程内存 (Access Denied)。\n\n" +
                                       $"建议解决方案：\n" +
                                       $"请尝试以“管理员身份运行” FuckCalibri.net。";
                        break;

                    case PatchStatus.Error:
                    default:
                        alertContent = $"【OneNote 注入拦截异常】\n\n" +
                                       $"目标进程：ONENOTE.EXE (PID: {state.ProcessId})\n" +
                                       $"异常信息：{state.Message}\n\n" +
                                       $"请检查 OneNote 是否卡死或尝试重新检测。";
                        break;
                }

                // 弹出模态警示弹窗
                System.Windows.MessageBox.Show(
                    alertContent,
                    alertTitle,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );

                // 唤醒主界面至前台以便用户直观核对参数
                BringToFront();
            }
        }

        private void UpdateTrayVisual(string iconColor, string tooltip)
        {
            try
            {
                TrayIcon.IconSource = new BitmapImage(new Uri($"pack://application:,,,/Resources/fuckcalibri-{iconColor}.ico"));
            }
            catch
            {
                try
                {
                    var streamInfo = Application.GetResourceStream(new Uri($"pack://application:,,,/Resources/fuckcalibri-{iconColor}.ico"));
                    if (streamInfo != null)
                    {
                        TrayIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
                    }
                }
                catch { }
            }

            TrayIcon.ToolTipText = tooltip;
        }

        private void UpdateMemoryUsageDisplay()
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                var mb = proc.WorkingSet64 / (1024.0 * 1024.0);
                TxtMemoryUsage.Text = $"内存: {mb:F1} MB";
            }
            catch { }
        }

        #region 方案选择与视觉联动

        private void OnSelectSchemeAClicked(object sender, MouseButtonEventArgs e)
        {
            ApplyScheme(MonitoringMode.EventDriven);
        }

        private void OnRadioSchemeAClicked(object sender, RoutedEventArgs e)
        {
            ApplyScheme(MonitoringMode.EventDriven);
        }

        private void OnSelectSchemeBClicked(object sender, MouseButtonEventArgs e)
        {
            ApplyScheme(MonitoringMode.PeriodicPolling);
        }

        private void OnRadioSchemeBClicked(object sender, RoutedEventArgs e)
        {
            ApplyScheme(MonitoringMode.PeriodicPolling);
        }

        private void ApplyScheme(MonitoringMode mode)
        {
            AppSettings.MonitoringMode = mode;
            _patcher.Mode = mode;
            UpdateSchemeVisual(mode);
        }

        private void UpdateSchemeVisual(MonitoringMode mode)
        {
            if (mode == MonitoringMode.EventDriven)
            {
                RadioSchemeA.IsChecked = true;
                RadioSchemeB.IsChecked = false;
                CardSchemeA.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
                CardSchemeA.BorderThickness = new Thickness(1.5);
                CardSchemeB.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                CardSchemeB.BorderThickness = new Thickness(1);

                TxtTopActiveMode.Text = "⚡ 方案 A (事件监听)";
                TxtMetricMode.Text = "方案 A：窗口事件监听模式 (即时触发)";
            }
            else
            {
                RadioSchemeA.IsChecked = false;
                RadioSchemeB.IsChecked = true;
                CardSchemeB.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
                CardSchemeB.BorderThickness = new Thickness(1.5);
                CardSchemeA.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                CardSchemeA.BorderThickness = new Thickness(1);

                TxtTopActiveMode.Text = "⏱ 方案 B (定时轮询)";
                TxtMetricMode.Text = "方案 B：后台周期定时轮询 (1.5秒)";
            }
        }

        #endregion

        #region 设置选项与状态同步 (解决“不知道选中没选中”)

        private void OnToggleAutoStartClicked(object sender, MouseButtonEventArgs e)
        {
            var newState = !(ChkAutoStart.IsChecked == true);
            UpdateAutoStartVisual(newState);
            AutoStartManager.SetAutoStart(newState);
        }

        private void OnChkAutoStartClicked(object sender, RoutedEventArgs e)
        {
            var isChecked = ChkAutoStart.IsChecked == true;
            UpdateAutoStartVisual(isChecked);
            AutoStartManager.SetAutoStart(isChecked);
        }

        private void UpdateAutoStartVisual(bool isChecked)
        {
            ChkAutoStart.IsChecked = isChecked;
            MenuAutoStart.IsChecked = isChecked;

            BadgeAutoStartStatus.Background = new SolidColorBrush(isChecked ? Color.FromRgb(0xDC, 0xFC, 0xE7) : Color.FromRgb(0xF1, 0xF5, 0xF9));
            TxtAutoStartStatus.Foreground = new SolidColorBrush(isChecked ? Color.FromRgb(0x15, 0x80, 0x3D) : Color.FromRgb(0x64, 0x74, 0x8B));
            TxtAutoStartStatus.Text = isChecked ? "✔ 已开启" : "⚪ 已关闭";
        }

        private void OnToggleMinimizeToTrayClicked(object sender, MouseButtonEventArgs e)
        {
            var newState = !(ChkMinimizeToTray.IsChecked == true);
            UpdateMinimizeToTrayVisual(newState);
            AppSettings.MinimizeToTray = newState;
        }

        private void OnChkMinimizeToTrayClicked(object sender, RoutedEventArgs e)
        {
            var isChecked = ChkMinimizeToTray.IsChecked == true;
            UpdateMinimizeToTrayVisual(isChecked);
            AppSettings.MinimizeToTray = isChecked;
        }

        private void UpdateMinimizeToTrayVisual(bool isChecked)
        {
            ChkMinimizeToTray.IsChecked = isChecked;

            BadgeMinimizeStatus.Background = new SolidColorBrush(isChecked ? Color.FromRgb(0xDC, 0xFC, 0xE7) : Color.FromRgb(0xF1, 0xF5, 0xF9));
            TxtMinimizeStatus.Foreground = new SolidColorBrush(isChecked ? Color.FromRgb(0x15, 0x80, 0x3D) : Color.FromRgb(0x64, 0x74, 0x8B));
            TxtMinimizeStatus.Text = isChecked ? "✔ 已开启" : "⚪ 已关闭";
        }

        private void OnToggleAutoStartMenuClicked(object sender, RoutedEventArgs e)
        {
            var isChecked = MenuAutoStart.IsChecked;
            UpdateAutoStartVisual(isChecked);
            AutoStartManager.SetAutoStart(isChecked);
        }

        #endregion

        #region 操作控制与托盘交互

        public void BringToFront()
        {
            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            UpdateMemoryUsageDisplay();
        }

        private void OnLaunchOneNoteClicked(object sender, RoutedEventArgs e)
        {
            OneNoteLauncher.LaunchOrActivate();
            _patcher.ScanAndPatchNow();
        }

        private void OnRescanClicked(object sender, RoutedEventArgs e)
        {
            var state = _patcher.ScanAndPatchNow();
            UpdateUiState(state);
        }

        private void OnCopyStatusClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var summary = $"【FuckCalibri.net 状态信息】\n状态：{TxtStatusTitle.Text}\n进程：{TxtMetricProcess.Text}\n模块：{TxtMetricModule.Text}\n特征：{TxtMetricPatched.Text}\n方案：{TxtMetricMode.Text}\n时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                Clipboard.SetText(summary);
                System.Windows.MessageBox.Show("已成功复制当前 OneNote 状态信息至剪贴板！", "复制成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch { }
        }

        private void OnShowHelpClicked(object sender, RoutedEventArgs e)
        {
            var helpText = "【关于 FuckCalibri.net v2.0】\n\n" +
                           "1. 痛点：OneNote 硬编码了字体回退逻辑，输入中西文混排或数字标点时总会强制切回 Calibri 字体。\n" +
                           "2. 原理：本工具对 onmain.dll 内存中的特征码进行动态掩码覆写，关闭强制回退分支，还原您自选设定的全局中西文字体。\n" +
                           "3. 方案 A：通过 Windows 窗口事件毫秒级捕获 OneNote，零后台轮询开销 (推荐)。\n" +
                           "4. 方案 B：后台 1.5 秒低开销定时轮询进程快照。\n" +
                           "5. 极致轻量：最小化至托盘时自动修剪工作集内存至 ~7MB。\n\n" +
                           "开源协议：MIT License";
            System.Windows.MessageBox.Show(helpText, "原理解析与使用帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void OnTrayContextMenuOpened(object sender, RoutedEventArgs e)
        {
            TrayContextMenu.Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
            TrayContextMenu.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            TrayContextMenu.Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
        }

        private void OnShowMainWindowClicked(object sender, RoutedEventArgs e)
        {
            BringToFront();
        }

        private void OnTrayDoubleClicked(object sender, RoutedEventArgs e)
        {
            BringToFront();
        }

        private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            if (IsVisible)
            {
                Hide();
                WinApi.TrimWorkingSet();
                UpdateMemoryUsageDisplay();
            }
            else
            {
                BringToFront();
            }
        }

        private void OnExitAppClicked(object sender, RoutedEventArgs e)
        {
            _isExplicitExit = true;
            _cts.Cancel();
            TrayIcon.Dispose();
            Close();
            Application.Current.Shutdown();
        }

        #endregion
    }
}
