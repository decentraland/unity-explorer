# Run from /Build dir
# Windows version of the build script

# Stop script on first error
$ErrorActionPreference = "Stop"

# Clear the console
Clear-Host

# Configure main project
cmake . -G "Visual Studio 17 2022" `
    -DCMAKE_BUILD_TYPE=Release `
    -DASTCENC_SHAREDLIB=ON `
    -DASTCENC_UNIVERSAL_BUILD=ON `
    -DASTCENC_DYNAMIC_LIBRARY=ON

# Build ASTC encoder
Set-Location astc-encoder-build
cmake --build . --clean-first --config Release --parallel 20
Set-Location ..

# Build main project
cmake --build . --clean-first --config Release --parallel 20

# Build configuration
$RELATIVE_PATH = "..\..\..\..\TexturesServerWrap\Libraries\Windows"
$ASTC_PATH = "astc-encoder-build\Source\Release"

# Moving files to the dedicated directory
# Note: In Windows, .dylib becomes .dll
Move-Item -Force "Release\texturesfuse.dll" "$RELATIVE_PATH\texturesfuse.dll"
Move-Item -Force "$ASTC_PATH\astcenc-native-shared.dll" "$RELATIVE_PATH\astcenc-shared.dll"
# Move-Item -Force "$ASTC_PATH\astcenc-avx2-shared.dll" "$RELATIVE_PATH\astcenc-avx2-shared.dll"
# Move-Item -Force "$ASTC_PATH\astcenc-sse4.1-shared.dll" "$RELATIVE_PATH\astcenc-sse4.1-shared.dll"
# Note: NEON is ARM-specific and typically not built on Windows x64
# Move-Item -Force "$ASTC_PATH\Release\astcenc-neon-shared.dll" "$RELATIVE_PATH\astcenc-neon-shared.dll"

# Windows doesn't need install_name_tool as DLL linking is handled differently
# DLL dependencies are resolved via the PATH or the same directory at runtime

# Cleaning up temp build directory
Remove-Item -Force -Recurse -ErrorAction SilentlyContinue @(
    "astc-encoder-build",
    "Build",
    "CMakeFiles",
    "cmake_install.cmake",
    "CMakeCache.txt",
    "compile_commands.json",
    "*.vcxproj*",
    "*.sln",
    "Release",
    "Debug",
    "x64"
)
