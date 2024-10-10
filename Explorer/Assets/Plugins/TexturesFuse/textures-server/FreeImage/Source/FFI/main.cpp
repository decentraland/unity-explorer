#include "bitmaps.h"
#include <string>
#include <fstream>
#include <iostream>

#ifndef TEST_TEXTURESFUSE

ImageResult __cdecl texturesfuse_initialize(InitOptions initOptions, context **contextOutput)
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
    context->config = config;

    if (status != ASTCENC_SUCCESS)
    {
        delete context;
        return ErrorASCTOnInit;
    }

    FreeImage_Initialise();

    *contextOutput = context;
    return Success;
}

ImageResult __cdecl texturesfuse_dispose(context *context)
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

    FreeImage_DeInitialise();
    return Success;
}

ImageResult __cdecl texturesfuse_release(context *context, FfiHandle handle)
{
    auto handles = context->handles;

    if (handles.find(handle) == handles.end())
    {
        return ErrorReleaseNoHandleFound;
    }

    handles.erase(handle);
    auto bytePtr = reinterpret_cast<BYTE *>(handle);
    delete[] bytePtr;

    return Success;
}

ImageResult __cdecl texturesfuse_processed_image_from_memory(
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

    // TODO release FIBITMAP
    // FreeImage_Unload(image);

    return Success;
}

ImageResult __cdecl texturesfuse_astc_image_from_memory(
    context *context,
    BYTE *bytes,
    int bytesLength,
    int maxSideLength,

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

    BYTE *bits = FreeImage_GetBits(image);
    if (!bits)
    {
        FreeImage_Unload(image);
        return ErrorCannotGetBits;
    }

    *width = FreeImage_GetWidth(image);
    *height = FreeImage_GetHeight(image);
    *releaseHandle = 1; // TODO
    *outputBytes = bits;
    *outputLength = 0; // TODO

    // TODO release FIBITMAP
    // FreeImage_Unload(image);

    return Success;
}

#else

std::streamsize sizeOf(std::ifstream *stream)
{
    stream->seekg(0, std::ios::end);
    std::streamsize fileSize = stream->tellg();
    stream->seekg(0, std::ios::beg);
    return fileSize;
}

struct BytesResult
{
    BYTE *data;
    size_t size;
};

BytesResult
bytesFromFile(std::string path)
{
    std::ifstream file(path, std::ios::binary);

    if (!file)
    {
        throw std::invalid_argument("Could not open file");
    }

    std::streamsize fileSize = sizeOf(&file);

    BYTE *buffer = new BYTE[fileSize];

    if (!file.read(reinterpret_cast<char *>(buffer), fileSize))
    {
        throw std::invalid_argument("Could not write to buffer");
    }

    file.close(); // TODO RAII

    BytesResult result;
    result.data = buffer;
    result.size = static_cast<size_t>(fileSize);

    return result;
}

int main()
{
    FreeImage_Initialise();

    std::string imagePath = "../image.jpg";
    std::string outputPath = "../output.jpg";

    BytesResult result = bytesFromFile(imagePath);

    FIMEMORY *memory = FreeImage_OpenMemory(result.data, static_cast<DWORD>(result.size));
    if (!memory)
    {
        std::cerr << "Error: Could not create FreeImage memory stream!" << std::endl;
        return 1;
    }

    FREE_IMAGE_FORMAT format = FreeImage_GetFileTypeFromMemory(memory);
    if (format == FIF_UNKNOWN)
    {
        std::cerr << "Error: Unknown image format!" << std::endl;
        return 1;
    }

    std::cout << "format is " << format << '\n';

    // Load the image from the memory stream
    FIBITMAP *image = FreeImage_LoadFromMemory(format, memory);
    if (!image)
    {
        std::cerr << "Error: Could not load image from memory!" << std::endl;
        return 1;
    }

    unsigned bpp = FreeImage_GetBPP(image);

    std::cout << "Bits per pixel: " << bpp << '\n';

    auto colorType = FreeImage_GetColorType(image);

    std::cout << "Color type is: " << colorType << '\n';

    int jpegQuality = 1; // 0 = worst quality, 100 = best quality
    if (FreeImage_Save(format, image, "../output.jpg", jpegQuality))
    {
        std::cout << "Image successfully saved and compressed" << std::endl;
    }
    else
    {
        std::cerr << "Error: Could not save the compressed image!" << std::endl;
    }

    FreeImage_DeInitialise();
}

#endif