@echo off
setlocal EnableDelayedExpansion

:: Auto-detect Visual Studio
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%VSWHERE%" (
    echo [ERROR] Visual Studio installer vswhere not found.
    exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
    set "VS_PATH=%%i"
)

if defined VS_PATH (
    echo [INFO] Found Visual Studio at: !VS_PATH!
    call "!VS_PATH!\VC\Auxiliary\Build\vcvars64.bat" >nul
) else (
    echo [ERROR] Could not find Visual Studio with C++ tools.
    exit /b 1
)

:: Paths - Using relative paths from RailNPP folder
set NPP_SRC=%~dp0..
set RAIL_SDK=%~dp0..\..\..\RailSDK-Cpp

:: SDK Include
set INC_RAIL=/I"%RAIL_SDK%\dist-x64\include" /I"%RAIL_SDK%\dist-x64\include\nlohmann"

:: NPP Includes
set INC_NPP=/I"%NPP_SRC%\PowerEditor\src\MISC\PluginsManager" /I"%NPP_SRC%\scintilla\include" /I"%NPP_SRC%\PowerEditor\src"

:: Libs
set LIBS=kernel32.lib user32.lib shell32.lib shlwapi.lib "%RAIL_SDK%\dist-x64\lib\rail_sdk.lib"

if not exist build mkdir build

echo Compiling RailNPP Plugin...
cl /nologo /LD /EHsc /std:c++17 /O2 /DNOMINMAX /D_UNICODE /DUNICODE /DRail_NO_RTTR /DRail_STATIC %INC_RAIL% %INC_NPP% main.cpp /Fe"build\RailNPP.dll" /link %LIBS% > build.log 2>&1

if %errorlevel% neq 0 (
    echo Build Failed! Check build.log for details.
    type build.log
    exit /b 1
)

echo Build Success!
echo Output: build\RailNPP.dll

:: Copy to plugins folder
if not exist "%NPP_SRC%\PowerEditor\bin64\plugins\RailNPP" mkdir "%NPP_SRC%\PowerEditor\bin64\plugins\RailNPP"
copy /Y "build\RailNPP.dll" "%NPP_SRC%\PowerEditor\bin64\plugins\RailNPP\RailNPP.dll"
echo Deployed to: %NPP_SRC%\PowerEditor\bin64\plugins\RailNPP\RailNPP.dll

exit /b 0
