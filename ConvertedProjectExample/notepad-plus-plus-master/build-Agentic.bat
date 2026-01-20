@echo off
setlocal EnableDelayedExpansion
REM ============================================
REM Build Script for Notepad++ with RAIL
REM ============================================

echo [RAIL] Building Notepad++ with RAIL integration...

REM Auto-detect Visual Studio
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%VSWHERE%" (
    echo [ERROR] Visual Studio installer vswhere not found.
    goto :SKIP_VS_SETUP
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
    set "VS_PATH=%%i"
)

if defined VS_PATH (
    echo [INFO] Found Visual Studio at: !VS_PATH!
    call "!VS_PATH!\VC\Auxiliary\Build\vcvars64.bat" >nul
)

:SKIP_VS_SETUP

REM Build Dependencies
echo [RAIL] Building Scintilla...
cd scintilla\win32
msbuild Scintilla.vcxproj /p:Configuration=Release /p:Platform=x64 /m
if %errorlevel% neq 0 (
    echo [ERROR] Scintilla build failed.
    pause
    exit /b 1
)
cd ..\..\

echo [RAIL] Building Lexilla...
cd lexilla\src
msbuild Lexilla.vcxproj /p:Configuration=Release /p:Platform=x64 /m
if %errorlevel% neq 0 (
    echo [ERROR] Lexilla build failed.
    pause
    exit /b 1
)
cd ..\..\

REM Prepare Dependencies for Linking
if not exist scintilla\bin mkdir scintilla\bin
copy /Y scintilla\win32\x64\Release\libScintilla.lib scintilla\bin\libscintilla.lib
copy /Y lexilla\src\x64\Release\libLexilla.lib scintilla\bin\liblexilla.lib

REM Build Notepad++
echo [RAIL] Building Notepad++ (x64 Release)...
cd PowerEditor\visual.net
msbuild notepadPlus.vcxproj /p:Configuration=Release /p:Platform=x64 /m
if %errorlevel% neq 0 (
    echo [ERROR] MSBuild failed.
    pause
    exit /b 1
)

REM Check if build succeeded (Output is in PowerEditor\bin64)
if not exist "..\bin64\notepad++.exe" (
    echo [ERROR] Build failed - notepad++.exe not found in PowerEditor\bin64
    cd ..\..
    exit /b 1
)

echo [RAIL] Build successful!
cd ..\..

REM Copy N++ Resources (Themes, Localization, etc.)
echo [RAIL] Copying Resources...
set "SRC_INST=PowerEditor\installer"
set "DST_BIN=PowerEditor\bin64"

xcopy /E /I /Y "%SRC_INST%\nativeLang" "%DST_BIN%\localization" >nul
xcopy /E /I /Y "%SRC_INST%\themes" "%DST_BIN%\themes" >nul
xcopy /E /I /Y "%SRC_INST%\functionList" "%DST_BIN%\functionList" >nul
xcopy /E /I /Y "%SRC_INST%\APIs" "%DST_BIN%\autoCompletion" >nul

powershell -ExecutionPolicy Bypass -File "patch_config.ps1" "%DST_BIN%"

echo Copying Toolbar Icons...
copy /Y "%DEPLOY_SRC%\toolbarIcons.xml" "%DST_BIN%\toolbarIcons.xml" >nul
if not exist "PowerEditor\bin64\toolbarIcons\default" mkdir "PowerEditor\bin64\toolbarIcons\default"
copy "PowerEditor\src\icons\light\toolbar\regular\*.ico" "PowerEditor\bin64\toolbarIcons\default\" /Y
copy /Y "%DEPLOY_SRC%\stylers.model.xml" "%DST_BIN%\stylers.model.xml" >nul
copy /Y "%DEPLOY_SRC%\langs.model.xml" "%DST_BIN%\langs.model.xml" >nul

REM Copy Missing DLLs found in Deploy
copy /Y "%DEPLOY_SRC%\libcurl.dll" "%DST_BIN%\" >nul
copy /Y "%DEPLOY_SRC%\zlib1.dll" "%DST_BIN%\" >nul

REM Force Local Config (Portable Mode)
echo. > "%DST_BIN%\doLocalConf.xml"

REM Copy RAIL DLLs to output folder
echo [RAIL] Copying RAIL DLLs...

set "OUT_DIR=PowerEditor\bin64"

REM RailBridge.dll
copy /Y "..\..\RailBridge.Native\bin\Release\net9.0\win-x64\native\RailBridge.dll" "%OUT_DIR%\" 2>nul
if not exist "%OUT_DIR%\RailBridge.dll" (
    echo [WARNING] RailBridge.dll not found in ..\..\RailBridge.Native\bin\Release\net9.0\win-x64\native
)

REM rail_sdk.dll
copy /Y "..\..\RailSDK-Cpp\dist-x64\bin\rail_sdk.dll" "%OUT_DIR%\" 2>nul
if not exist "%OUT_DIR%\rail_sdk.dll" (
    echo [WARNING] rail_sdk.dll not found in ..\..\RailSDK-Cpp\dist-x64\bin
)

REM Build RailNPP Plugin
echo [RAIL] Building RailNPP Plugin...
cd RailNPP
call build.bat
if %errorlevel% neq 0 (
    echo [WARNING] RailNPP plugin build failed - continuing anyway
)
cd ..

echo.
echo ============================================
echo Build complete!
echo Executable: %OUT_DIR%\notepad++.exe
echo ============================================
echo.
echo To run: start %OUT_DIR%\notepad++.exe

pause