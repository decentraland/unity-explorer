ASTC encoder https://github.com/ARM-software/astc-encoder is included as a copy and not as a git submodule due its API does not gurantee that it won't be changed in the future

Quote:
 * While the aim is that we keep this interface mostly stable, it should be viewed as a mutable
 * interface tied to a specific source version. We are not trying to maintain backwards
 * compatibility across codec versions.

 Build ASTC: 
 1. Go to directory `astc-encoder`
 2. `cmake . -DASTCENC_SHAREDLIB=ON -DASTCENC_UNIVERSAL_BUILD=ON -DASTCENC_DYNAMIC_LIBRARY=ON`

 Build Compressonator:
 1. Follow README.md in ./СompressonatorWorkaround

 Build Lib
 1. Run FreeImage/Source/FFI/build.sh