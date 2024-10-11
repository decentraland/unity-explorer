#include "astcenc.h"
#include <stdint.h>
#include <unordered_set>

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
    const unsigned int THREADS_PER_CONTEXT = 1;

    struct InitOptions
    {
#pragma region ASTC_options

        int ASTCProfile;
        unsigned int blockX;
        unsigned int blockY;
        unsigned int blockZ;
        float quality;
        unsigned int flags;

#pragma endregion ASTC_options
    };

    enum ImageResult : int {
        ErrorNotImplemented = -1,
        ErrorUnknown = 0,
        Success = 1,
        ErrorOpenMemoryStream = 2,
        ErrorUnknownImageFormat = 3,
        ErrorCannotLoadImage = 4,
        ErrorCannotGetBits = 5,
        ErrorCannotDownscale = 6,
        ErrorCannotConvertTo32Bits = 7,

        ErrorInvalidPointer = 10,
        ErrorASTCOnInit = 11,
        ErrorASTCOnAlloc = 12,
        ErrorASTCOnCompress = 13,

        ErrorDisposeAlreadyDisposed = 20,
        ErrorDisposeNotAllTexturesReleased = 21,

        ErrorReleaseNoHandleFound = 30,
    };

    /**
     * Context provides synchronization. Library is stateless and all multithreading/threadsafety should be resolved on client's side.
     * Context shouldn't be mutated by client's side by design.
     */
    struct context
    {
        std::unordered_set<FfiHandle> handles;
        astcenc_config config;
        astcenc_context* astcContext;
        bool disposed;
    };

    FFI_API ImageResult texturesfuse_initialize(InitOptions initOptions, context **contextOutput);

    FFI_API ImageResult texturesfuse_dispose(context *context);

    FFI_API ImageResult texturesfuse_release(context *context, FfiHandle handle);

    FFI_API ImageResult texturesfuse_processed_image_from_memory(
        context *context,
        BYTE *bytes,
        int bytesLength,
        int maxSideLength,

        BYTE **outputBytes,
        unsigned int *width,
        unsigned int *height,
        unsigned int *bitsPerPixel,
        int *colorType,
        FfiHandle *releaseHandle);

    FFI_API ImageResult texturesfuse_astc_image_from_memory(
        context *context,
        BYTE *bytes,
        int bytesLength,
        int maxSideLength,

        BYTE **outputBytes,
        int *outputLength,
        unsigned int *width,
        unsigned int *height,
        FfiHandle *releaseHandle);
}