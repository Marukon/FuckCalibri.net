@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================================
echo   一键发布 (智能解析编译输出路径 + 安全防误删版)
echo ========================================================

set "ROOT_DIR=%~dp0"
set "LOG_FILE=%TEMP%\dotnet_pub_%RANDOM%.log"

cd /d "%ROOT_DIR%"

echo [1/3] 正在编译单文件实体...
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false -p:DebugType=none -p:DebugSymbols=false > "!LOG_FILE!" 2>&1

:: 回显编译过程
type "!LOG_FILE!"

if errorlevel 1 (
    del "!LOG_FILE!" 2>nul
    goto :BUILD_ERROR
)

echo.
echo [2/3] 正在提取生成路径...
set "PUB_PATH="

:: 1. 抓取包含 publish 的输出行
for /f "delims=" %%L in ('findstr /i "publish\\" "!LOG_FILE!"') do (
    set "RAW_LINE=%%L"
)
del "!LOG_FILE!" 2>nul

if not defined RAW_LINE goto :NOT_FOUND

:: 2. 安全截取箭头 "→" 或 "->" 后面的真实路径（规避括号引起的语法错误）
set "PUB_PATH=!RAW_LINE:*→=!"
if "!PUB_PATH!"=="!RAW_LINE!" set "PUB_PATH=!RAW_LINE:*->=!"

:: 去除路径开头的多余空格
for /f "tokens=* delims= " %%A in ("!PUB_PATH!") do set "PUB_PATH=%%A"

echo 解析到的发布目录: !PUB_PATH!

:: 3. 智能判断是绝对路径还是相对路径
set "SRC_DIR="
if exist "!PUB_PATH!*.exe" (
    set "SRC_DIR=!PUB_PATH!"
) else if exist "%ROOT_DIR%!PUB_PATH!*.exe" (
    set "SRC_DIR=%ROOT_DIR%!PUB_PATH!"
)

if not defined SRC_DIR (
    echo.
    echo [错误] 在目标路径未检测到生成好的 exe 文件！
    echo 检查路径 1: !PUB_PATH!
    echo 检查路径 2: %ROOT_DIR%!PUB_PATH!
    pause
    exit /b 1
)

:: 4. 执行移动并强制覆盖到当前根目录（不屏蔽错误，清晰可见）
echo 正在移动程序到根目录...
move /y "!SRC_DIR!*.exe" "%ROOT_DIR%"
if errorlevel 1 (
    echo.
    echo [错误] 移动 EXE 文件失败，已中止清理垃圾以防丢失文件！
    pause
    exit /b 1
)

echo.
echo [3/3] 移动成功，正在清理所有工程下的 bin / obj 垃圾...
for /f "delims=" %%d in ('dir /s /b /ad "%ROOT_DIR%" 2^>nul ^| findstr /i "\\bin$ \\obj$"') do (
    rd /s /q "%%d" 2>nul
)

echo.
echo ========================================================
echo   发布完成！独立单文件已成功就绪在当前根目录，垃圾已清空。
echo ========================================================

exit /b 0

:BUILD_ERROR
echo.
echo [错误] 编译未通过，请检查上方代码报错。

exit /b 1

:NOT_FOUND
echo.
echo [错误] 未能从日志中找到包含 publish 的生成信息。
exit /b 1