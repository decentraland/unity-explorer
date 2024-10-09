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

    enum ImageResult : int
    {
        ErrorUnknown = 0,
        Success = 1,
        ErrorOpenMemoryStream = 2,
        ErrorUnknownImageFormat = 3,
        ErrorCannotLoadImage = 4,
        ErrorCannotGetBits = 5,
        ErrorCannotDownscale = 5
    };

    FFI_API bool texturesfuse_initialize();

    FFI_API bool texturesfuse_dispose();

    FFI_API void texturesfuse_release(FfiHandle handle);

    FFI_API ImageResult texturesfuse_processed_image_from_memory(
        BYTE *bytes,
        int bytesLength,
        int maxSideLength,

        BYTE **outputBytes,
        unsigned int *width,
        unsigned int *height,
        unsigned int *bitsPerPixel,
        int *colorType,
        FfiHandle *releaseHandle);
}