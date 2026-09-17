using System;
using System.Collections.Generic;

namespace FuckCalibri
{
    internal static class Constants
    {
        public const string AppName = "FuckCalibri.net";
        public const string AppVersion = "2.0.0";
        public const string ProjectUrl = "https://github.com/Marukon/FuckCalibri.net";
        public const string UpstreamUrl = "https://github.com/zhmjx/FuckCalibri";
        public const string DotnetRepoUrl = "https://github.com/he1a2s0/FuckCalibri.net";

        public static class LaunchArguments
        {
            public const string Silent = "/silent";
            public const string Show = "/show";
        }

        // 仅支持桌面版 OneNote，放弃已经废弃的 OneNote for Windows 10 (onenoteim)
        public const string TargetProcessName = "ONENOTE";
        public const string DisplayTargetName = "Microsoft OneNote (桌面版)";

        public static readonly string[] TargetModuleNames = ["onmain.dll", "onmainw32.dll"];

        public static class Signatures
        {
            public const byte OpcodeMovEcx = 0xb9; // mov ecx, imm32
            public const byte OpcodePush = 0x68;   // push imm32
            public const byte OpcodeJmpRel = 0xe9; // jmp rel32

            // 未打补丁时的特征码值
            public const int UnpatchedVal1 = 0x302;
            public const int UnpatchedVal2 = 0x103;

            // 打补丁后（生效状态）的特征码值：& 0xfd 之后的特征
            public const int PatchedVal1 = 0x300;
            public const int PatchedVal2 = 0x101;
        }
    }
}
