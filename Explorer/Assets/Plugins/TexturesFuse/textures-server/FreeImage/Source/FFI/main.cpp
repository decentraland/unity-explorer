#include "FreeImage.h"
#include "texturesfuse.h"
#include <string>
#include <fstream>
#include <iostream>

#ifndef TEST_TEXTURESFUSE

bool __cdecl texturesfuse_initialize()
{
    FreeImage_Initialise();
    return true;
}

bool __cdecl texturesfuse_dispose()
{
    FreeImage_DeInitialise();
    return true;
}

void __cdecl texturesfuse_release(FfiHandle handle)
{
    // TODO
}

ImageResult __cdecl texturesfuse_processed_image_from_memory(
    BYTE *bytes,
    int length,
    BYTE **outputBytes,
    unsigned int *width,
    unsigned int *height,
    unsigned int *bitsPerPixel,
    int *colorType,
    FfiHandle *releaseHandle)
{
    FIMEMORY *memory = FreeImage_OpenMemory(bytes, static_cast<DWORD>(length));
    if (!memory)
    {
        return ErrorOpenMemoryStream;
    }

    FREE_IMAGE_FORMAT format = FreeImage_GetFileTypeFromMemory(memory);
    if (format == FIF_UNKNOWN)
    {
        FreeImage_CloseMemory(memory);
        return ErrorUnknownImageFormat;
    }

    // Load the image from the memory stream
    FIBITMAP *image = FreeImage_LoadFromMemory(format, memory);
    if (!image)
    {
        FreeImage_CloseMemory(memory);
        return ErrorCannotLoadImage;
    }

    // int jpegQuality = 1; // 0 = worst quality, 100 = best quality

    // FreeImage uses BGR format, it needs to be converted to RGB for Unity
    BYTE *bits = FreeImage_GetBits(image);
    if (!bits)
    {
        FreeImage_CloseMemory(memory);
        FreeImage_Unload(image);
        return ErrorCannotGetBits;
    }

    FREE_IMAGE_COLOR_TYPE imageColorType = FreeImage_GetColorType(image);

    *width = FreeImage_GetWidth(image),
    *height = FreeImage_GetHeight(image),
    *bitsPerPixel = FreeImage_GetBPP(image);
    *colorType = imageColorType;
    *releaseHandle = 1; // TODO
    *outputBytes = bits;

    // TODO release FIBITMAP and FIMEMORY
    // FreeImage_CloseMemory(memory);
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