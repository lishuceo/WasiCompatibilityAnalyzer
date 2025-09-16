@echo off
setlocal enabledelayedexpansion

echo 🔧 WasiCompatibilityAnalyzer 元数据更新脚本

if "%1"=="" (
    echo.
    echo ❌ 错误: 请指定WasiCore源码路径
    echo.
    echo 📋 用法:
    echo   update-metadata.bat "WasiCore路径"
    echo.
    echo 📋 示例:
    echo   update-metadata.bat "C:\Dev\sce\wasicore"
    echo   update-metadata.bat "D:\Projects\wasicore"
    echo.
    pause
    exit /b 1
)

set "WASICORE_PATH=%~1"

if not exist "%WASICORE_PATH%" (
    echo ❌ 错误: WasiCore路径不存在: %WASICORE_PATH%
    echo ❌ 请检查路径是否正确
    pause
    exit /b 1
)

echo 🔍 验证WasiCore目录结构...
if not exist "%WASICORE_PATH%\GameCore" (
    echo ❌ 警告: 未找到GameCore子目录，请确认这是正确的WasiCore根目录
)

if not exist "%WASICORE_PATH%\GameUI" (
    echo ❌ 警告: 未找到GameUI子目录，请确认这是正确的WasiCore根目录
)

echo.
echo 📁 WasiCore路径: %WASICORE_PATH%
echo 🚀 开始生成元数据...
echo.

cd Tools

dotnet run --project MetadataGenerator.csproj "%WASICORE_PATH%" "../platform-api-metadata.json"

if !errorlevel! equ 0 (
    echo.
    echo 🔄 重新构建分析器...
    cd ..
    dotnet build
    
    if !errorlevel! equ 0 (
        echo.
        echo ✅ 元数据更新成功完成！
        echo 📄 生成的文件: platform-api-metadata.json
        echo 🔧 分析器已重新构建
    ) else (
        echo ❌ 分析器构建失败
    )
) else (
    echo ❌ 元数据生成失败
)

echo.
pause
