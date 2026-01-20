@echo off
setlocal EnableDelayedExpansion

echo ============================================
echo  LiquidSDK-Cpp Build Script (x86/32-bit)
echo ============================================
echo.

:: --- Auto-detect Visual Studio ---
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%VSWHERE%" (
    echo [ERROR] Visual Studio not found. Please install Visual Studio 2022 with C++ tools.
    exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
    set "VS_PATH=%%i"
)

if not defined VS_PATH (
    echo [ERROR] Visual Studio with C++ tools not found.
    exit /b 1
)

echo [INFO] Found Visual Studio at: %VS_PATH%
call "%VS_PATH%\VC\Auxiliary\Build\vcvars32.bat" >nul

:: --- Configure ---
echo.
echo [1/3] Configuring CMake (x86/Win32 Release)...
cmake -S . -B build-x86 -A Win32
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] CMake configuration failed.
    exit /b 1
)

:: --- Build ---
echo.
echo [2/3] Building Release (x86)...
cmake --build build-x86 --config Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed.
    exit /b 1
)

:: --- Install ---
echo.
echo [3/3] Installing to dist-x86/...
cmake --install build-x86 --prefix dist-x86 --config Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Installation failed.
    exit /b 1
)

echo.
echo ============================================
echo  BUILD SUCCESSFUL! (x86/32-bit)
echo ============================================
echo Output: %CD%\dist-x86
echo.
echo NOTE: Use this for legacy 32-bit applications (Siemens CNC, etc.)
echo.

endlocal
