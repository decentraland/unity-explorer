@echo off
REM This script is used to compile the Rust code on Windows for x86_64

REM Compile for x86_64-pc-windows-msvc
cargo build --release

REM Copy the compiled DLL to the desired location
copy /Y target\release\rust_segment.dll ..\SegmentServerWrap\Libraries\Windows\segment-server.dll

REM Cleanup target folder to avoid confusion from Unity
rmdir /S /Q target