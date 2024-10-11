# run from /Build dir 
# MAC ONLY for windows use a different script

set -e  # Exit immediately if any command exits with a non-zero status

# Build

clear
cmake .. -DCMAKE_BUILD_TYPE=Release -DASTCENC_SHAREDLIB=ON -DASTCENC_UNIVERSAL_BUILD=ON -DASTCENC_DYNAMIC_LIBRARY=ON
cd astc-encoder-build
make clean
make -j10;
cd ..

make clean
make -j10;

# Moving files to the dedicated directory

RELATIVE_PATH=../../../../../TexturesServerWrap/Libraries/Mac
ASTC_PATH=astc-encoder-build/Source

mv libtexturesfuse.dylib $RELATIVE_PATH/libtexturesfuse.dylib
mv $ASTC_PATH/libastcenc-shared.dylib $RELATIVE_PATH/libastcenc-shared.dylib
mv $ASTC_PATH/libastcenc-avx2-shared.dylib $RELATIVE_PATH/libastcenc-avx2-shared.dylib
mv $ASTC_PATH/libastcenc-sse4.1-shared.dylib $RELATIVE_PATH/libastcenc-sse4.1-shared.dylib
mv $ASTC_PATH/libastcenc-neon-shared.dylib $RELATIVE_PATH/libastcenc-neon-shared.dylib

# Updating linking paths

install_name_tool -change @rpath/libastcenc-neon-shared.dylib @loader_path/libastcenc-neon-shared.dylib $RELATIVE_PATH/libtexturesfuse.dylib
install_name_tool -change @rpath/libastcenc-sse4.1-shared.dylib @loader_path/libastcenc-sse4.1-shared.dylib $RELATIVE_PATH/libtexturesfuse.dylib
install_name_tool -change @rpath/libastcenc-avx2-shared.dylib @loader_path/libastcenc-avx2-shared.dylib $RELATIVE_PATH/libtexturesfuse.dylib

# Cleaning up temp build derectory
# rm -rf . is too dangerous

rm -rf ./astc-encoder-build
rm -rf ./Build
rm -rf ./CMakeFiles
rm -rf ./cmake_install.cmake
rm -rf ./CMakeCache.txt
rm -rf ./compile_commands.json
rm -rf ./Makefile
rm -rf ./Makefile
rm -rf ./texturesfuse