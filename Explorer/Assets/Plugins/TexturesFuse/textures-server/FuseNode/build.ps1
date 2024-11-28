cmake . -G "Visual Studio 17 2022" `
    -DCMAKE_BUILD_TYPE=Debug `
    -DASTCENC_SHAREDLIB=ON `
    -DASTCENC_UNIVERSAL_BUILD=ON `
    -DASTCENC_DYNAMIC_LIBRARY=ON

cmake --build . --config Debug --parallel 10