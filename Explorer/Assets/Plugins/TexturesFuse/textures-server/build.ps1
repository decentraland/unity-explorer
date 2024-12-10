# Exit immediately if any command fails
$ErrorActionPreference = "Stop"

# Initialize and update submodules
git submodule update --init --recursive

# Navigate to astc-encoder directory and build with CMake
Push-Location "astc-encoder"
cmake -G "Visual Studio 17 2022" . -DASTCENC_SHAREDLIB=ON -DASTCENC_UNIVERSAL_BUILD=ON -DASTCENC_DYNAMIC_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release
Pop-Location

# Navigate to CompressonatorWorkaround and run the required Python scripts
Push-Location "CompressonatorWorkaround"
python ./compressonator/build/fetch_dependencies.py
python ./compressonator/build/fetch_dependencies.py # repeated intentionally
python integrate_simde.py
./builds_win.ps1
Pop-Location

# Navigate to FreeImage/Source/FFI and run the build script
Push-Location "FreeImage/Source/FFI"
./build.ps1
Pop-Location