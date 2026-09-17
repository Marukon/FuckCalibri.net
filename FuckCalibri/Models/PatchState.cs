using System;

namespace FuckCalibri.Models
{
    public enum PatchStatus
    {
        NotRunning,       // OneNote 未运行
        WaitingModule,    // OneNote 已启动，等待核心模块载入
        Patching,         // 发现未修补特征码，正在写入补丁
        Patched,          // 已生效（已成功修补或检测到特征码已处于修补状态）
        Unsupported,      // 未匹配到特征码（版本不受支持或特征码变动）
        AccessDenied,     // 无法打开进程或读写内存（权限不足）
        Error             // 其他未知错误
    }

    public class PatchStateInfo
    {
        public PatchStatus Status { get; set; } = PatchStatus.NotRunning;

        public int ProcessId { get; set; }

        public string ProcessName { get; set; } = Constants.TargetProcessName;

        public string? ModuleName { get; set; }

        public IntPtr ModuleBaseAddress { get; set; }

        public int ModuleMemorySize { get; set; }

        // 检测到的已生效特征码处数
        public int PatchedCount { get; set; }

        // 检测到的未修补特征码处数
        public int UnpatchedCount { get; set; }

        // 本次执行新修补的处数
        public int NewlyPatchedCount { get; set; }

        public string Message { get; set; } = "等待 OneNote 启动...";

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsEffective => Status == PatchStatus.Patched;
    }
}
