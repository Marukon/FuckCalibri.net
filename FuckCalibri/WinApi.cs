
using System;
using System.Runtime.InteropServices;

namespace FuckCalibri {
    internal static class WinApi {
        public const int PROCESS_WM_READ = 0x0010;
        public const int PROCESS_VM_WRITE = 0x0020;
        public const int PROCESS_VM_OPERATION = 0x0008;
        public const int PROCESS_QUERY_INFORMATION = 0x0400;

        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;

        public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterShellHookWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DeregisterShellHookWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const int HSHELL_WINDOWCREATED = 1;
        public const int HSHELL_WINDOWDESTROYED = 2;
        public const int HSHELL_ACTIVATESHELLWINDOW = 3;
        public const int HSHELL_WINDOWACTIVATED = 4;
        public const int HSHELL_RUDEAPPACTIVATED = 0x8004;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        /// <summary>
        /// 最小化至托盘时主动压缩工作集内存，释放未使用的物理内存占用
        /// </summary>
        public static void TrimWorkingSet() {
            try {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                SetProcessWorkingSetSize(proc.Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }
    }
}
