@echo off
for /f "tokens=*" %%i in ('"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -find VC\Auxiliary\Build\vcvarsall.bat') do set VCVARS=%%i
call "%VCVARS%" x64 >nul 2>&1
clang -shared -m64 -o WindowResizeConstraint.dll WindowResizeConstraint.c -luser32
