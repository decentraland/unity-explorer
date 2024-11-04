set -e  # Exit immediately if any command exits with a non-zero status

git submodule update --init --recursive

cd astc-encoder
cmake . -DASTCENC_SHAREDLIB=ON -DASTCENC_UNIVERSAL_BUILD=ON -DASTCENC_DYNAMIC_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release
cd ..

cd CompressonatorWorkaround/
python3.12 ./compressonator/build/fetch_dependencies.py
python3.12 ./compressonator/build/fetch_dependencies.py # intended
python3.12 integrate_simde.py
./builds_mac.sh
cd ..

cd FreeImage/Source/FFI/
./build.sh