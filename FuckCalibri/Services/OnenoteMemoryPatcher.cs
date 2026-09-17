using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FuckCalibri.Models;

namespace FuckCalibri.Services
{
    public class OnenoteMemoryPatcher
    {
        public event EventHandler<PatchStateInfo>? StateChanged;

        private PatchStateInfo _currentState = new();
        public PatchStateInfo CurrentState => _currentState;

        private int _lastPatchedProcessId = 0;

        private void SetState(PatchStateInfo state)
        {
            _currentState = state;
            StateChanged?.Invoke(this, state);
        }

        public MonitoringMode Mode { get; set; } = AppSettings.MonitoringMode;

        public void NotifyWindowChanged(int eventType, IntPtr hWnd)
        {
            if (eventType == WinApi.HSHELL_WINDOWCREATED || eventType == WinApi.HSHELL_WINDOWACTIVATED || eventType == WinApi.HSHELL_RUDEAPPACTIVATED)
            {
                if (hWnd != IntPtr.Zero)
                {
                    WinApi.GetWindowThreadProcessId(hWnd, out uint pid);
                    if (pid > 0)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            if (string.Equals(proc.ProcessName, Constants.TargetProcessName, StringComparison.OrdinalIgnoreCase))
                            {
                                ScanAndPatchNow();
                                return;
                            }
                        }
                        catch { }
                    }
                }
            }
            else if (eventType == WinApi.HSHELL_WINDOWDESTROYED)
            {
                if (_currentState.Status == PatchStatus.Patched)
                {
                    var procs = Process.GetProcessesByName(Constants.TargetProcessName);
                    if (procs.Length == 0)
                    {
                        ScanAndPatchNow();
                    }
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ScanAndPatch();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[FuckCalibri] Exception during ScanAndPatch: {ex.Message}");
                }

                try
                {
                    // 方案 B (轮询模式): 每 1.5 秒主动轮询进程快照
                    // 方案 A (事件驱动模式): 依靠 Windows 窗口创建事件毫秒级触发，后台仅维持 15 秒低频安全兜底
                    int delayMs = Mode == MonitoringMode.PeriodicPolling ? 1500 : 15000;
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public PatchStateInfo ScanAndPatchNow()
        {
            return ScanAndPatch(forceRescan: true);
        }

        private PatchStateInfo ScanAndPatch(bool forceRescan = false)
        {
            var processes = Process.GetProcessesByName(Constants.TargetProcessName);
            if (processes.Length == 0)
            {
                _lastPatchedProcessId = 0;
                var notRunningState = new PatchStateInfo
                {
                    Status = PatchStatus.NotRunning,
                    ProcessName = Constants.TargetProcessName,
                    Message = "OneNote 未运行"
                };
                SetState(notRunningState);
                return notRunningState;
            }

            var process = processes[0];
            var pid = process.Id;

            // 如果当前进程已成功打过补丁且未指定强制重扫，且进程 PID 未变，保持 Patched 状态
            if (!forceRescan && _lastPatchedProcessId == pid && _currentState.Status == PatchStatus.Patched)
            {
                return _currentState;
            }

            ProcessModule? targetModule = null;
            try
            {
                targetModule = process.Modules.Cast<ProcessModule>()
                    .FirstOrDefault(pm => Constants.TargetModuleNames.Contains(pm.ModuleName, StringComparer.OrdinalIgnoreCase));
            }
            catch
            {
                // OneNote 刚启动时，枚举模块可能抛出异常（模块未完成初始化）
                var waitingState = new PatchStateInfo
                {
                    Status = PatchStatus.WaitingModule,
                    ProcessId = pid,
                    Message = "OneNote 正在启动，等待核心模块载入..."
                };
                SetState(waitingState);
                return waitingState;
            }

            if (targetModule == null)
            {
                var waitingState = new PatchStateInfo
                {
                    Status = PatchStatus.WaitingModule,
                    ProcessId = pid,
                    Message = "已找到 OneNote 进程，正在等待 onmain.dll 加载..."
                };
                SetState(waitingState);
                return waitingState;
            }

            var moduleBaseAddress = targetModule.BaseAddress;
            var size = targetModule.ModuleMemorySize;

            var processHandle = WinApi.OpenProcess(
                WinApi.PROCESS_QUERY_INFORMATION | WinApi.PROCESS_WM_READ | WinApi.PROCESS_VM_WRITE | WinApi.PROCESS_VM_OPERATION,
                false,
                pid
            );

            if (processHandle == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                var accessDenied = err == 5;
                var errorState = new PatchStateInfo
                {
                    Status = accessDenied ? PatchStatus.AccessDenied : PatchStatus.Error,
                    ProcessId = pid,
                    ModuleName = targetModule.ModuleName,
                    Message = accessDenied ? "权限不足：无法访问 OneNote 进程内存，请尝试以管理员身份运行本程序。" : $"打开 OneNote 进程失败 (Win32Error: {err})。"
                };
                SetState(errorState);
                return errorState;
            }

            try
            {
                var buffer = new byte[size];
                int numOfBytesRead = 0;
                if (!WinApi.ReadProcessMemory(processHandle, moduleBaseAddress, buffer, size, ref numOfBytesRead))
                {
                    var err = Marshal.GetLastWin32Error();
                    var readFailState = new PatchStateInfo
                    {
                        Status = PatchStatus.Error,
                        ProcessId = pid,
                        ModuleName = targetModule.ModuleName,
                        Message = $"读取 OneNote 内存失败 (Win32Error: {err})。"
                    };
                    SetState(readFailState);
                    return readFailState;
                }

                var unpatchedOffsets = new List<int>();
                int alreadyPatchedCount = 0;

                // 双向扫描：同时统计未修补与已修补特征码
                for (int i = 0; i < numOfBytesRead - 5; i++)
                {
                    byte b = buffer[i];
                    if (b == Constants.Signatures.OpcodeMovEcx || b == Constants.Signatures.OpcodePush)
                    {
                        int val32 = BitConverter.ToInt32(buffer, i + 1);

                        // 1. 检查未打补丁时的特征序列
                        if (val32 == Constants.Signatures.UnpatchedVal1 ||
                            (val32 == Constants.Signatures.UnpatchedVal2 && buffer[i + 5] == Constants.Signatures.OpcodeJmpRel))
                        {
                            unpatchedOffsets.Add(i);
                        }
                        // 2. 检查已打补丁（已生效）时的特征序列
                        else if (val32 == Constants.Signatures.PatchedVal1 ||
                                 (val32 == Constants.Signatures.PatchedVal2 && buffer[i + 5] == Constants.Signatures.OpcodeJmpRel))
                        {
                            alreadyPatchedCount++;
                        }
                    }
                }

                // 如果检测到未修补特征码，立即应用写入
                if (unpatchedOffsets.Count > 0)
                {
                    int newlyPatched = 0;
                    foreach (var offset in unpatchedOffsets)
                    {
                        // 将 0x0302/0x0103 修改为 0x0300/0x0101 (& 0xfd)
                        buffer[offset + 1] = (byte)(buffer[offset + 1] & 0xfd);

                        var patchChunk = new byte[6];
                        Array.Copy(buffer, offset, patchChunk, 0, 6);
                        int bytesWritten = 0;

                        if (WinApi.WriteProcessMemory(processHandle, moduleBaseAddress + offset, patchChunk, 6, ref bytesWritten))
                        {
                            newlyPatched++;
                        }
                    }

                    int totalEffective = alreadyPatchedCount + newlyPatched;
                    _lastPatchedProcessId = pid;

                    var successState = new PatchStateInfo
                    {
                        Status = PatchStatus.Patched,
                        ProcessId = pid,
                        ModuleName = targetModule.ModuleName,
                        ModuleBaseAddress = moduleBaseAddress,
                        ModuleMemorySize = size,
                        PatchedCount = totalEffective,
                        NewlyPatchedCount = newlyPatched,
                        UnpatchedCount = unpatchedOffsets.Count - newlyPatched,
                        Message = $"已覆盖内存区域，成功保护 {totalEffective} 处特征点。"
                    };
                    SetState(successState);
                    return successState;
                }
                else
                {
                    // 未检测到原版特征码，检查是否已处于修补状态
                    if (alreadyPatchedCount > 0)
                    {
                        _lastPatchedProcessId = pid;
                        var alreadyPatchedState = new PatchStateInfo
                        {
                            Status = PatchStatus.Patched,
                            ProcessId = pid,
                            ModuleName = targetModule.ModuleName,
                            ModuleBaseAddress = moduleBaseAddress,
                            ModuleMemorySize = size,
                            PatchedCount = alreadyPatchedCount,
                            Message = $"已生效（检测到 {alreadyPatchedCount} 处特征码已处于保护状态，已成功拦截 Calibri）。"
                        };
                        SetState(alreadyPatchedState);
                        return alreadyPatchedState;
                    }
                    else
                    {
                        // 既没有未修补特征，也没有已修补特征：版本不支持或特征码已变动！
                        var unsupportedState = new PatchStateInfo
                        {
                            Status = PatchStatus.Unsupported,
                            ProcessId = pid,
                            ModuleName = targetModule.ModuleName,
                            ModuleBaseAddress = moduleBaseAddress,
                            ModuleMemorySize = size,
                            Message = "未匹配到特征码（未生效，当前 OneNote 版本可能不受支持或特征码已变更）。"
                        };
                        SetState(unsupportedState);
                        return unsupportedState;
                    }
                }
            }
            finally
            {
                WinApi.CloseHandle(processHandle);
            }
        }
    }
}
