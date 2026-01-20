@echo off
set "DEPS_DIR=%cd%\deps"

set "SDL2_DIR=%DEPS_DIR%\SDL2-2.28.5"
set "SDL2_MIXER_DIR=%DEPS_DIR%\SDL2_mixer-2.6.3"
set "SDL2_NET_DIR=%DEPS_DIR%\SDL2_net-2.2.0"

echo Configuring Chocolate Doom with deps from %DEPS_DIR%...

cmake -S . -B build ^
    -A x64 ^
    -DSDL2_PATH="%SDL2_DIR%" ^
    -DSDL2_MIXER_PATH="%SDL2_MIXER_DIR%" ^
    -DSDL2_NET_PATH="%SDL2_NET_DIR%" ^
    -DSDL2_LIBRARY="%SDL2_DIR%\lib\x64\SDL2.lib" ^
    -DSDL2_INCLUDE_DIR="%SDL2_DIR%\include" ^
    -DSDL2_MIXER_LIBRARY="%SDL2_MIXER_DIR%\lib\x64\SDL2_mixer.lib" ^
    -DSDL2_MIXER_INCLUDE_DIR="%SDL2_MIXER_DIR%\include" ^
    -DSDL2_NET_LIBRARY="%SDL2_NET_DIR%\lib\x64\SDL2_net.lib" ^
    -DSDL2_NET_INCLUDE_DIR="%SDL2_NET_DIR%\include" ^
    -DENABLE_PYTHON=OFF


