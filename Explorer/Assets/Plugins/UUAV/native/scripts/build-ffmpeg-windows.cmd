@echo off
rem FFmpeg's configure needs a POSIX shell and MSVC needs its environment
rem variables, so this enters the VS x64 developer environment and hands off to
rem build-ffmpeg-windows.sh under MSYS2, which inherits it.
rem
rem Prerequisites (see README): Visual Studio Build Tools 2022 with the C++
rem workload, LLVM for Windows (clang-cl), CMake, and MSYS2 with make, nasm,
rem pkgconf and diffutils. CMake and pkgconf are what the static libxml2 the
rem dash demuxer needs is built and found with; build-ffmpeg-windows.sh checks
rem for all of them up front. Override MSYS2_ROOT / LLVM_ROOT for non-default
rem locations.

setlocal

if not defined MSYS2_ROOT set "MSYS2_ROOT=C:\msys64"
if not defined LLVM_ROOT set "LLVM_ROOT=C:\Program Files\LLVM"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo error: vswhere.exe not found; install Visual Studio Build Tools 2022 1>&2
    exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"
if not defined VSPATH (
    echo error: no Visual Studio installation with the C++ x64 tools 1>&2
    exit /b 1
)

if not exist "%LLVM_ROOT%\bin\clang-cl.exe" (
    echo error: clang-cl.exe not found under "%LLVM_ROOT%"; install LLVM for Windows 1>&2
    exit /b 1
)

if not exist "%MSYS2_ROOT%\usr\bin\bash.exe" (
    echo error: MSYS2 not found under "%MSYS2_ROOT%" 1>&2
    exit /b 1
)

call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" || exit /b 1

set "PATH=%LLVM_ROOT%\bin;%PATH%"
rem hand the whole Windows PATH and the vcvars INCLUDE/LIB to the MSYS2 shell
set "MSYS2_PATH_TYPE=inherit"
set "CHERE_INVOKING=1"

"%MSYS2_ROOT%\usr\bin\bash.exe" -lc "cd \"$(cygpath -u '%~dp0')\" && ./build-ffmpeg-windows.sh"
exit /b %ERRORLEVEL%
