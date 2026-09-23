
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace FuckCalibri {
    public static class SingleInstanceHelper {
        
        private static Mutex? _mutex;
        private static uint _mutexMessage;

        public static bool IsSingleInstance { get; private set; }

        /// <summary>
        /// 响应通知：前启动的进程是否响应后启动进程的通知消息
        /// </summary>
        public static bool ReactToNotification { get; private set; }

        public static void Check(bool silent = false) {
            ReactToNotification = true;

            const string appGuid = "FuckCalibri_WPF_Net10_AppInstance_Guid_2026";
            _mutexMessage = WinApi.RegisterWindowMessage(appGuid);
            if (_mutexMessage > 0) {
                WinApi.ChangeWindowMessageFilter(_mutexMessage, WinApi.MSGFLT_ADD);
            }

            _mutex = new Mutex(true, $"Global\\{appGuid}", out bool createNew);
            IsSingleInstance = createNew;
        }

        public static bool IsMutexMessage(int msg) => _mutexMessage > 0 && msg == (int)_mutexMessage;

        public static void Notify() {
            if (_mutexMessage > 0) {
                WinApi.PostMessage(WinApi.HWND_BROADCAST, _mutexMessage, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static void Release() {
            if (_mutex != null) {
                try {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
                catch { }
                _mutex = null;
            }
            _mutexMessage = 0;
        }
    }
}
