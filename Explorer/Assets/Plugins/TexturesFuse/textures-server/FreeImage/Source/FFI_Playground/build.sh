cmake . -DCMAKE_BUILD_TYPE=Release -DASTCENC_SHAREDLIB=ON -DASTCENC_UNIVERSAL_BUILD=ON -DASTCENC_DYNAMIC_LIBRARY=ON;
make -j10;


install_name_tool -change @rpath/astc-encoder-build/Source/libastcenc-neon-shared.dylib @loader_path/libastcenc-neon-shared.dylib ./playground
install_name_tool -change @rpath/astc-encoder-build/Source/libastcenc-sse4.1-shared.dylib @loader_path/libastcenc-sse4.1-shared.dylib ./playground
install_name_tool -change @rpath/astc-encoder-build/Source/libastcenc-avx2-shared.dylib @loader_path/libastcenc-avx2-shared.dylib ./playground