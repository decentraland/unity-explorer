$ErrorActionPreference = "Stop"

# Change directory
Set-Location compressonator/cmp_compressonatorlib

# Run cmake command (line breaks added for readability)
cmake . -G "Visual Studio 17 2022" `
    -DOPTION_CMP_QT=OFF `
    -DOPTION_CMP_VULKAN=OFF `
    -DOPTION_CMP_DIRECTX=OFF `
    -DOPTION_CMP_OPENCV=OFF `
    -DOPTION_ENABLE_ALL_APPS=OFF `
    -DOPTION_BUILD_CMP_SDK=ON `
    -DOPTION_BUILD_APPS_CMP_CLI=OFF `
    -DOPTION_BUILD_KTX2=OFF `
    -DSIMDE_X86_AVX_ENABLE_NATIVE_ALIASES=ON `
    -DGLM_ENABLE_EXPERIMENTAL=OFF `
    -DGLM_BUILD_LIBRARY=ON
    
# Instead of make clean, you'll want to clean the build using cmake
cmake --build . --target clean

# Build using cmake instead of make
cmake --build . --config Release --parallel 10

# Copy the library file (using Windows path separator)
Copy-Item "Release\CMP_Compressonator.lib" -Destination "..\..\..\Compressator\lib\libCMP_Compressonator.lib"

Set-Location ..
Set-Location ..

# Change directory
Set-Location compressonator/cmp_core

# Run cmake command (line breaks added for readability)
cmake . -G "Visual Studio 17 2022" `
    -DOPTION_CMP_QT=OFF `
    -DOPTION_CMP_VULKAN=OFF `
    -DOPTION_CMP_DIRECTX=OFF `
    -DOPTION_CMP_OPENCV=OFF `
    -DOPTION_ENABLE_ALL_APPS=OFF `
    -DOPTION_BUILD_CMP_SDK=ON `
    -DOPTION_BUILD_APPS_CMP_CLI=OFF `
    -DOPTION_BUILD_KTX2=OFF `
    -DSIMDE_X86_AVX_ENABLE_NATIVE_ALIASES=ON `
    -DGLM_ENABLE_EXPERIMENTAL=OFF `
    -DGLM_BUILD_LIBRARY=ON
    
# Instead of make clean, you'll want to clean the build using cmake
cmake --build . --target clean

# Build using cmake instead of make
cmake --build . --config Release --parallel 10

# Copy the library files (using Windows path separator)
Copy-Item "Release\CMP_Core.lib" -Destination "..\..\..\Compressator\lib\libCMP_Core.lib"
Copy-Item "Release\CMP_Core_SSE.lib" -Destination "..\..\..\Compressator\lib\libCMP_Core_SSE.lib"
Copy-Item "Release\CMP_Core_AVX.lib" -Destination "..\..\..\Compressator\lib\libCMP_Core_AVX.lib"
Copy-Item "Release\CMP_Core_AVX512.lib" -Destination "..\..\..\Compressator\lib\libCMP_Core_AVX512.lib"



Set-Location ..
Set-Location ..

# Change directory
Set-Location compressonator/cmp_framework

# Run cmake command (line breaks added for readability)
cmake . -G "Visual Studio 17 2022" `
    -DOPTION_CMP_QT=OFF `
    -DOPTION_CMP_VULKAN=OFF `
    -DOPTION_CMP_DIRECTX=OFF `
    -DOPTION_CMP_OPENCV=OFF `
    -DOPTION_ENABLE_ALL_APPS=OFF `
    -DOPTION_BUILD_CMP_SDK=ON `
    -DOPTION_BUILD_APPS_CMP_CLI=OFF `
    -DOPTION_BUILD_KTX2=OFF `
    -DSIMDE_X86_AVX_ENABLE_NATIVE_ALIASES=ON `
    -DGLM_ENABLE_EXPERIMENTAL=OFF `
    -DGLM_BUILD_LIBRARY=ON
    
# Instead of make clean, you'll want to clean the build using cmake
cmake --build . --target clean

# Build using cmake instead of make
cmake --build . --config Release --parallel 10

# Copy the library files (using Windows path separator)
Copy-Item "Release\CMP_Framework.lib" -Destination "..\..\..\Compressator\lib\libCMP_Framework.lib"