# FuckCalibri.net 2.0

> **Microsoft OneNote 强制 Calibri 字体拦截工具**  
> 基于 **.NET 10** 与 **WPF-UI (Fluent 2)** 现代化重构升级，采用独立单文件绿色发布。

---

## 📖 痛点背景

在使用 Microsoft OneNote 记录笔记时，无论您在“文件 - 选项”中将默认字体设定为何种中文字体，只要输入某些非西文字符、数字、特定标点符号或在中西文混排移动光标时，OneNote 核心排版引擎便会顽固且自动切回并锁定为 **Calibri** 英文字体。这不仅破坏了整体版面字体的协调性，也造成了长期困扰用户的排版体验缺陷。

由于该逻辑被硬编码在 OneNote 内部排版引擎中，微软官方至今未提供关闭选项。

---

## 💡 解决方案与原理

本项目通过 Windows 内存特征匹配与动态掩码修补技术，在 OneNote 运行时对其核心排版模块（`onmain.dll` / `onmainw32.dll`）中的 Calibri 强制 Fallback 回退逻辑进行即时掩码覆写，关闭该强制替换分支，彻底还原用户自选设定的默认字体。

---

## 🚀 v2.0 全新升级特性

在原项目基础上，v2.0 开展了全方位的架构重写与功能升级：

### 1. 全新升级至 .NET 10 平台
- 采用微软最新 **.NET 10 (`net10.0-windows`)** 与 C# 13，全面支持原生 64 位寻址与现代运行时优化；
- **独立单文件编译 (Single-File)**：所有依赖库、图标与 WPF-UI 资源均打包集成于单一 `FuckCalibri.exe` 可执行文件中，彻底告别散乱 DLL，即开即用。

### 2. 现代 Fluent 2 视觉界面（保持经典紧凑结构）
- 界面严格遵循原软件经典、紧凑的**单列纵向主体结构**，不堆砌复杂宽屏，操作逻辑自然顺畅；
- 引入 **Fluent 2 / WPF-UI** 现代设计语言，底色与卡片呈现细腻分明的空间层次感；
- 状态展示全面升级为彩色动态徽章（`✅ 已生效` / `⚪ 未运行` / `⚠️ 未匹配`），彻底替换老旧复选框。

### 3. 彻底修复“无法检测是否已生效”的已知历史缺陷
- **原版痛点**：原版仅能单向扫描未修补的特征字节序列，当因版本不兼容或模块差异找不到特征码时，也会误报为“已生效”。
- **双状态精准扫描算法**：同时扫描原版未修补序列（`0x302` / `0x103`）与已修补序列（`0x300` / `0x101`）。精准识别“已生效（显示保护点数，如 9 处）”、“未修补（自动写入补丁）”与“特征不匹配（提示版本未适配，杜绝误报）”。

### 4. 灵活双拦截方案（默认方案 A，零后台开销）
- **方案 A：窗口事件监听模式 (推荐 · 默认)**
  - 基于 Win32 `RegisterShellHookWindow` 挂载系统 Shell 窗口消息钩子；
  - 毫秒级感知 OneNote 窗口的诞生与激活并即刻执行修补，**平时零后台进程轮询循环，CPU 占用与能耗严格为 0%**。
- **方案 B：后台周期定时轮询模式**
  - 每隔 1.5 秒主动轮询进程快照，兼容后台无窗口静默同步等特殊环境。

### 5. 极清晰的偏好设置与状态反馈
- 提供“开机自动启动”与“关闭窗口时最小化到系统托盘”选项；
- 每个设置项配备醒目的大字胶囊徽章（开启显示高对比度 **`✔ 已开启`**，关闭显示 **`⚪ 已关闭`**），彻底消除传统复选框辨识不清的困扰；
- 托盘右键菜单针对任务栏背景进行实色与对比度优化，清晰沉稳。

### 6. 应用内一键启动 / 唤醒 OneNote 与启动时序解耦
- 支持任意启动时序：先开 OneNote、后开本程序、或使用过程中重启 OneNote 均可无缝自动捕获；
- 主界面配备「🚀 启动 / 切换至 OneNote」按钮，可一键拉起或置顶激活 OneNote。

### 7. 极致轻量常驻
- 最小化/隐藏至托盘时，主动调用 `WinApi.TrimWorkingSet` 释放 DirectX 与图形渲染缓冲，后台内存占用仅约 **~7 MB**。

### 8. 专精 OneNote 桌面版
- 彻底移除已被微软官方废弃停更的 OneNote for Windows 10 (UWP) 支持，专注服务 Microsoft 365 / Office 桌面版 OneNote (`ONENOTE.EXE`)。

---

## 🛠 命令行参数

- `/silent` : 静默模式，启动后直接最小化常驻系统托盘，不主动弹出主窗口（适合开机自启）。
- `/show`   : 显示模式，启动时强制唤醒并置顶主界面。

---

## 📦 编译构建

如需从源码自行编译单文件版本：

```bash
# 确保已安装 .NET 10 SDK
dotnet publish "FuckCalibri/FuckCalibri.csproj" -c Release -r win-x64 --self-contained false
```

编译输出位于：`FuckCalibri/bin/Release/net10.0-windows/win-x64/publish/FuckCalibri.exe`。

---

## 🙏 致谢与开源传承

本项目是在多位优秀开源作者的工作基础上进化重构而来，特此致以由衷的敬意与谢意：

- **原 C++ 原型与核心逆向算法**：[@zhmjx](https://github.com/zhmjx) - [zhmjx/FuckCalibri](https://github.com/zhmjx/FuckCalibri)  
  *奠定了定位并修补 OneNote `onmain.dll` 字体回退特征码的核心逆向成果。*
- **原 .NET Framework 移植与托盘实现**：[@he1a2s0](https://github.com/he1a2s0) / [@Marukon](https://github.com/Marukon) - [FuckCalibri.net](https://github.com/Marukon/FuckCalibri.net)  
  *首创了基于 .NET Framework 的 Windows 窗体托盘守候版本。*

---

## 📄 开源协议

本项目遵循 **MIT License** 开放源代码。
