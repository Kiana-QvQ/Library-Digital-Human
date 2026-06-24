@echo off
chcp 65001 >nul
cd /d "%~dp0"

set "PYSIDE=%~dp0.."
for /f "delims=" %%i in ('python -c "import PySide6, os; print(os.path.dirname(PySide6.__file__))" 2^>nul') do set "PYSIDE=%%i"

if not exist "%PYSIDE%\Qt6Core.dll" (
    echo [错误] 未找到 PySide6，请先执行: pip install PySide6==6.7.3
    pause
    exit /b 1
)

set "PATH=%PYSIDE%;%PATH%"
set "QT_PLUGIN_PATH=%PYSIDE%\plugins"
set "QT_QPA_PLATFORM_PLUGIN_PATH=%PYSIDE%\plugins\platforms"
set "QT_QPA_PLATFORM=windows"

python "%~dp0run_qt_admin.py"
if errorlevel 1 pause
