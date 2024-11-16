cmake . -G "Visual Studio 17 2022" `
    -DCMAKE_BUILD_TYPE=Release `
    -DASTCENC_SHAREDLIB=ON `
    -DASTCENC_UNIVERSAL_BUILD=ON `
    -DASTCENC_DYNAMIC_LIBRARY=ON

cmake --build . --clean-first --config Release --parallel 10