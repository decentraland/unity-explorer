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

ImageResult ClampedImage(FIBITMAP *bitmap, int maxSideSize, FIBITMAP **output)
{
    if (!output)
    {
        FreeImage_Unload(bitmap);
        return ErrorInvalidPointer;
    }

    unsigned imageWidth = FreeImage_GetWidth(bitmap);
    unsigned imageHeight = FreeImage_GetHeight(bitmap);

    if (imageWidth > maxSideSize || imageHeight > maxSideSize)
    {
        FIBITMAP *rescaled = FreeImage_MakeThumbnail(bitmap, maxSideSize, false);
        if (!rescaled)
        {
            // TODO move close upper
            FreeImage_Unload(bitmap);
            return ErrorCannotDownscale;
        }
        FreeImage_Unload(bitmap);
        *output = rescaled;
        return Success;
    }

    *output = bitmap;
    return Success;
}