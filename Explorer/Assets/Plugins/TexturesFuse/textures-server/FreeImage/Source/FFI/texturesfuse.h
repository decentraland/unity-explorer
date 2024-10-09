#include <stdint.h>

#if defined(_WIN32) || defined(_WIN64)
#ifdef BUILD_DLL
#define FFI_API __declspec(dllexport)
#else
#define FFI_API __declspec(dllimport)
#endif
#elif defined(__GNUC__) && __GNUC__ >= 4
#define FFI_API __attribute__((visibility("default"))) // Ensure symbols are visible
#else
#define FFI_API
#endif

extern "C"
{
    typedef intptr_t FfiHandle;

    const FfiHandle INVALID_HANDLE = 0;

    FFI_API bool texturesfuse_initialize();

    FFI_API bool texturesfuse_dispose();

    FFI_API void texturesfuse_release(FfiHandle handle);

    FFI_API FfiHandle texturesfuse_processed_image_from_memory(
        const char *bytes,
        int length,
        char **outputBytes,
        int *outputLength);
}