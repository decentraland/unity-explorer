#include "texture.h"
#include "compressonator.h"
#include "bitmaps.h"
#include <string>
#include <fstream>
#include <iostream>
#include "common.h"
#include "anylog.h"

#include <stdio.h>
#include <stdlib.h>

ImageResult ErrorFromASTC(astcenc_error error)
{
    switch (error)
    {
    case ASTCENC_ERR_OUT_OF_MEM:
        return ErrorASTC_OUT_OF_MEM;
    case ASTCENC_ERR_BAD_CPU_FLOAT:
        return ErrorASTC_BAD_CPU_FLOAT;
    case ASTCENC_ERR_BAD_PARAM:
        return ErrorASTC_BAD_PARAM;
    case ASTCENC_ERR_BAD_BLOCK_SIZE:
        return ErrorASTC_BAD_BLOCK_SIZE;
    case ASTCENC_ERR_BAD_PROFILE:
        return ErrorASTC_BAD_PROFILE;
    case ASTCENC_ERR_BAD_QUALITY:
        return ErrorASTC_BAD_QUALITY;
    case ASTCENC_ERR_BAD_SWIZZLE:
        return ErrorASTC_BAD_SWIZZLE;
    case ASTCENC_ERR_BAD_FLAGS:
        return ErrorASTC_BAD_FLAGS;
    case ASTCENC_ERR_BAD_CONTEXT:
        return ErrorASTC_BAD_CONTEXT;
    case ASTCENC_ERR_NOT_IMPLEMENTED:
        return ErrorASTC_NOT_IMPLEMENTED;
    case ASTCENC_ERR_BAD_DECODE_MODE:
        return ErrorASTC_BAD_DECODE_MODE;
    default:
        return ErrorUnknown;
    }
}

// Function to calculate the compressed data size for an ASTC image
size_t dataLenForASTC(
    unsigned int width, unsigned int height, unsigned int depth,
    unsigned int block_width, unsigned int block_height, unsigned int block_depth)
{
    // Calculate the number of blocks required in each dimension
    size_t blocks_x = std::ceil(static_cast<float>(width) / block_width);
    size_t blocks_y = std::ceil(static_cast<float>(height) / block_height);
    size_t blocks_z = std::ceil(static_cast<float>(depth) / block_depth);

    // Calculate the total number of blocks
    size_t total_blocks = blocks_x * blocks_y * blocks_z;

    // Each block generates 16 bytes of compressed data
    return total_blocks * 16;
}

void Log(char *msg)
{
    FreeImage_OutputMessageProc(FIF_UNKNOWN, msg);
}

void AL_Callback(const char *msg)
{
    FreeImage_OutputMessageProc(FIF_UNKNOWN, msg);
}

bool CMP_Log(float progress, CMP_DWORD_PTR user1, CMP_DWORD_PTR user2)
{
    printf("Progress %.2f, u1 %d u2 %d\n", progress, user1, user2);
    return true;
}

KernelOptions NewKernelOptionsFromCustomOptions(
    const CMP_FORMAT cmpFormat,
    const CMP_CustomOptions &compressOptions)
{
    KernelOptions kOpt;
    memset(&kOpt, 0, sizeof(KernelOptions));

    kOpt.encodeWith = compressOptions.encodeWith;
    kOpt.format = cmpFormat;
    kOpt.fquality = compressOptions.fQuality;
    kOpt.threads = compressOptions.dwnumThreads;

    return kOpt;
}

int MipSetFrom(const CMP_DWORD width, const CMP_DWORD height, CMIPS *CMips, CMP_BYTE *data, const CMP_DWORD dataSize, CMP_MipSet *mipSet)
{
    memset(mipSet, 0, sizeof(CMP_MipSet));
    mipSet->dwWidth = width;
    mipSet->dwHeight = height;
    mipSet->m_format = CMP_FORMAT_BGRA_8888;

    // Set the channel formats and mip levels
    mipSet->m_ChannelFormat = CF_8bit;
    mipSet->m_TextureDataType = TDT_ARGB; // TODO check if need normal maps
    mipSet->m_dwFourCC = 0;
    mipSet->m_dwFourCC2 = 0;
    mipSet->m_TextureType = TT_2D;
    mipSet->m_nDepth = 1; // depthsupport

    // Allocate default MipSet header
    CMips->AllocateMipSet(mipSet,
                          mipSet->m_ChannelFormat,
                          mipSet->m_TextureDataType,
                          mipSet->m_TextureType,
                          width,
                          height,
                          mipSet->m_nDepth);

    FreeImage_OutputMessageProc(FIF_UNKNOWN, "Levels");

    // Determine buffer size and set Mip Set Levels we want to use for now
    MipLevel *mipLevel = CMips->GetMipLevel(mipSet, 0);
    mipSet->m_nMipLevels = 1;

    FreeImage_OutputMessageProc(FIF_UNKNOWN, "Allocated data");

    mipLevel->m_nWidth = mipSet->m_nWidth;
    mipLevel->m_nHeight = mipSet->m_nHeight;
    mipLevel->m_dwLinearSize = dataSize;
    mipLevel->m_pbData = data;

    CMP_BYTE *pData = (CMP_BYTE *)(mipLevel->m_pbData);

    if (mipLevel->m_dwLinearSize != dataSize)
    {
        FreeImage_OutputMessageProc(FIF_UNKNOWN, "WRONG data");
        return 1; // fatal case
    }

    FreeImage_OutputMessageProc(FIF_UNKNOWN, "Copy data");

    memcpy(pData, data, dataSize);
    return 0;
}

ImageResult texturesfuse_initialize(InitOptions initOptions, context **contextOutput)
{
    if (!contextOutput)
    {
        return ErrorInvalidPointer;
    }

    context *context = new struct context();
    astcenc_config config;
    astcenc_error status = astcenc_config_init(
        static_cast<astcenc_profile>(initOptions.ASTCProfile),
        initOptions.blockX,
        initOptions.blockY,
        initOptions.blockZ,
        initOptions.quality,
        initOptions.flags,
        &config);

    if (status != ASTCENC_SUCCESS)
    {
        delete context;
        return ErrorASTCOnInit;
    }

    astcenc_context *astcContext;
    status = astcenc_context_alloc(&config, THREADS_PER_CONTEXT, &astcContext);
    if (status != ASTCENC_SUCCESS)
    {
        delete context;
        return ErrorASTCOnAlloc;
    }

    context->config = config;
    context->astcContext = astcContext;

    FreeImage_Initialise();
    FreeImage_SetOutputMessage(initOptions.debugLogFunc);

    if (initOptions.debugLogFunc)
    {
        AL_Init(AL_Callback);
    }

#ifdef _WIN32
    std::string env = "AMDCOMPRESS_PLUGINS=";
    const char *pluginsPath = initOptions.pluginsPath;
    std::string result = env + pluginsPath;

    if (_putenv(result.c_str()) == 0)
    {
        AL_Log("Environment variable AMDCOMPRESS_PLUGINS set successfully.\n");
    }
    else
    {
        AL_Log("Failed to set environment variable");
    }
#endif

    CMP_InitFramework();
    PrintStatusLine = Log;

    FreeImage_OutputMessageProc(FIF_UNKNOWN, "TexturesFuse successfully initialized!");

    *contextOutput = context;
    return Success;
}

ImageResult texturesfuse_dispose(context *context)
{
    if (context->disposed)
    {
        return ErrorDisposeAlreadyDisposed;
    }
    if ((context->handles).empty() == false)
    {
        return ErrorDisposeNotAllTexturesReleased;
    }
    context->disposed = true;
    // TODO dispose handles
    // TODO ASTC context dispose

    FreeImage_DeInitialise();
    return Success;
}

ImageResult texturesfuse_release(context *context, FfiHandle handle)
{
    bool result = context->handles.tryReleaseHandle(handle);
    return result ? Success : ErrorReleaseNoHandleFound;
}

ImageResult texturesfuse_processed_image_from_memory(
    context *context,
    BYTE *bytes,
    int bytesLength,
    int maxSideLength,

    BYTE **outputBytes,
    unsigned int *width,
    unsigned int *height,
    unsigned int *bitsPerPixel,
    int *colorType,
    FfiHandle *releaseHandle)
{
    FIBITMAP *image;
    auto result = BitmapFromMemory(bytes, static_cast<DWORD>(bytesLength), &image);

    if (result != Success)
    {
        return result;
    }

    result = ClampedImage(image, maxSideLength, &image);
    if (result != Success)
    {
        return result;
    }

    // TODO test only, remove
    result = WithAlphaImage(image, &image);
    if (result != Success)
    {
        return result;
    }

    BYTE *bits = FreeImage_GetBits(image);
    if (!bits)
    {
        FreeImage_Unload(image);
        return ErrorCannotGetBits;
    }

    FREE_IMAGE_COLOR_TYPE imageColorType = FreeImage_GetColorType(image);

    *width = FreeImage_GetWidth(image);
    *height = FreeImage_GetHeight(image);
    *bitsPerPixel = FreeImage_GetBPP(image);
    *colorType = imageColorType;
    *releaseHandle = 1; // TODO
    *outputBytes = bits;

    LogImageInfo(image, "Prepared image without ASTC: ");

    // TODO release FIBITMAP
    // FreeImage_Unload(image);

    return Success;
}

ImageResult texturesfuse_astc_image_from_memory(
    context *context,
    astcenc_swizzle swizzle,
    BYTE *bytes,
    int bytesLength,
    int maxSideLength,
    Adjustments adjustments,

    BYTE **outputBytes,
    int *outputLength,
    unsigned int *width,
    unsigned int *height,
    FfiHandle *releaseHandle)
{
    FIBITMAP *image;
    auto result = BitmapFromMemory(bytes, static_cast<DWORD>(bytesLength), &image);

    if (result != Success)
    {
        return result;
    }

    result = ClampedImage(image, maxSideLength, &image);
    if (result != Success)
    {
        return result;
    }

    result = WithAlphaImage(image, &image);
    if (result != Success)
    {
        return result;
    }

    if (adjustments.use)
    {
        BOOL adjustResult = FreeImage_AdjustColors(
            image,
            adjustments.brightness,
            adjustments.contrast,
            adjustments.gamma,
            FALSE);

        if (!adjustResult)
        {
            FreeImage_OutputMessageProc(FIF_UNKNOWN, "Cannot apply adjustments");
        }
    }

    astcenc_type astcType;
    result = ASTCDataTypeFromImageWithAlpha(image, &astcType);
    if (result != Success)
    {
        FreeImage_Unload(image);
        return result;
    }

    LogImageInfo(image, "image to process with ASTC: ");

    BYTE *bits = FreeImage_GetBits(image);
    if (!bits)
    {
        FreeImage_Unload(image);
        return ErrorCannotGetBits;
    }

    *width = FreeImage_GetWidth(image);
    *height = FreeImage_GetHeight(image);

    void **dataBuffer = new void *[1];

    // Create an astcenc_image structure
    astcenc_image astcImage;
    astcImage.dim_x = *width;
    astcImage.dim_y = *height;
    astcImage.dim_z = 1; // depth for 2D is 1
    astcImage.data_type = astcType;
    astcImage.data = dataBuffer; // Only one 2D image layer
    astcImage.data[0] = bits;    // Point to the raw image data

    auto config = context->config;
    auto astcContext = context->astcContext;

    size_t astcBytesLength = dataLenForASTC(
        astcImage.dim_x,
        astcImage.dim_y,
        astcImage.dim_z,
        config.block_x,
        config.block_y,
        1 // compression blocks amount for 2D is 1
    );

    // len shouldn't be too long
    *outputLength = static_cast<int>(astcBytesLength);

    *outputBytes = new BYTE[*outputLength];

    astcenc_error astcError = astcenc_compress_image(
        astcContext,
        &astcImage,
        &swizzle,
        *outputBytes,
        astcBytesLength,
        0 // since THREADS_PER_CONTEXT is 1
    );

    delete[] dataBuffer;
    FreeImage_Unload(image);

    if (astcError != ASTCENC_SUCCESS)
    {
        delete[] *outputBytes;
        return ErrorFromASTC(astcError);
    }
    *releaseHandle = context->handles.registerHandle(*outputBytes);

    return Success;
}

ImageResult texturesfuse_cmp_image_from_memory(
    context *context,
    BYTE *bytes,
    int bytesLength,
    int maxSideLength,
    CMP_FORMAT cmpFormat,
    CMP_CustomOptions compressOptions,

    BYTE **outputBytes,
    int *outputLength,
    unsigned int *width,
    unsigned int *height,
    FfiHandle *releaseHandle)
{
    FIBITMAP *image;
    auto result = BitmapFromMemory(bytes, static_cast<DWORD>(bytesLength), &image);

    if (result != Success)
    {
        return result;
    }

    result = ClampedImage(image, maxSideLength, &image);
    if (result != Success)
    {
        return result;
    }

    if (cmpFormat == CMP_FORMAT_BC5 || cmpFormat == CMP_FORMAT_BC7)
    {
        result = AlignInMultipleOf4(image, &image);
        if (result != Success)
        {
            return result;
        }
    }

    result = WithAlphaImage(image, &image);
    if (result != Success)
    {
        return result;
    }

    LogImageInfo(image, "image to process with CMP: ");

    BYTE *bits = FreeImage_GetBits(image);
    if (!bits)
    {
        FreeImage_Unload(image);
        return ErrorCannotGetBits;
    }

    *width = FreeImage_GetWidth(image);
    *height = FreeImage_GetHeight(image);

    const unsigned int bitsLength = *width * *height * 4; // since 1 byte per channel

    if (cmpFormat == CMP_FORMAT_BC5)
    {
        SwapRGBAtoBGRA(bits, bitsLength);
    }

    FreeImage_OutputMessageProc(FIF_UNKNOWN, "Encoding with compress option is: %d", compressOptions.encodeWith);

    const CMP_FORMAT sourceFormat = CMP_FORMAT_BGRA_8888;

    if (compressOptions.encodeWith == CMP_Compute_type::CMP_CPU)
    {
        CMP_Texture sourceTexture = {0};
        sourceTexture.dwSize = sizeof(CMP_Texture);
        sourceTexture.dwWidth = *width;
        sourceTexture.dwHeight = *height;
        sourceTexture.dwPitch = 0;
        sourceTexture.format = sourceFormat;
        sourceTexture.dwDataSize = bitsLength;
        sourceTexture.pData = bits;

        // Set up destination texture
        CMP_Texture destTexture = {0};
        destTexture.dwSize = sizeof(CMP_Texture);
        destTexture.dwWidth = *width;
        destTexture.dwHeight = *height;
        destTexture.dwPitch = 0;
        destTexture.format = cmpFormat;
        destTexture.dwDataSize = CMP_CalculateBufferSize(&destTexture);
        destTexture.pData = new CMP_BYTE[destTexture.dwDataSize];

        CMP_CompressOptions options = {0};
        options.dwSize = sizeof(CMP_CompressOptions);
        options.fquality = compressOptions.fQuality;
        options.bDisableMultiThreading = compressOptions.disableMultithreading;
        options.dwnumThreads = compressOptions.dwnumThreads;
        options.nEncodeWith = compressOptions.encodeWith;

        auto cmpResult = CMP_ConvertTexture(&sourceTexture, &destTexture, &options, nullptr);
        FreeImage_Unload(image);

        if (cmpResult != CMP_OK)
        {
            FreeImage_OutputMessageProc(FIF_UNKNOWN, "Error during decoding CMP: %d", cmpResult);

            delete[] destTexture.pData;
            return ErrorBC5;
        }

        *outputBytes = destTexture.pData;
        *outputLength = static_cast<int>(destTexture.dwDataSize);

        *releaseHandle = context->handles.registerHandle(*outputBytes);

        return Success;
    }

    KernelOptions kOpt = NewKernelOptionsFromCustomOptions(cmpFormat, compressOptions);

    std::unique_ptr<CMIPS> cmip(new CMIPS);

    CMP_MipSet srcMipSet;

    auto mipResult = MipSetFrom(
        *width,
        *height,
        cmip.get(),
        bits,
        bitsLength,
        &srcMipSet);

    if (mipResult)
    {
        FreeImage_OutputMessageProc(FIF_UNKNOWN, "Fatal error on MipSet source creation");
        return ErrorUnknown;
    }

    CMP_MipSet dstMipSet = {0};

    CMP_ERROR cmpResult = CMP_ProcessTexture(&srcMipSet, &dstMipSet, kOpt, CMP_Log);

    FreeImage_Unload(image);

    if (cmpResult != CMP_OK)
    {
        FreeImage_OutputMessageProc(FIF_UNKNOWN, "Error during decoding CMP: %d", cmpResult);

        if (dstMipSet.pData)
        {
            delete[] dstMipSet.pData;
        }
        return ErrorBC5;
    }

    *outputBytes = dstMipSet.pData;
    *outputLength = dstMipSet.dwDataSize;

    *releaseHandle = context->handles.registerHandle(*outputBytes);

    return Success;
}