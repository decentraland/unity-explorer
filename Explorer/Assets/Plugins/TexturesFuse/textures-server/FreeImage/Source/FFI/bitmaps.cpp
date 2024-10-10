// #include "FreeImage.h"
// #include "texturesfuse.h"
#include "bitmaps.h"

ImageResult BitmapFromMemory(BYTE *bytes, DWORD bytesLength, FIBITMAP **output)
{
    FIMEMORY *memory = FreeImage_OpenMemory(bytes, bytesLength);
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

    *output = image;
    FreeImage_CloseMemory(memory);
    return Success;
}