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

ImageResult WithAlphaImage(FIBITMAP *bitmap, FIBITMAP **output)
{
    if (!output)
    {
        FreeImage_Unload(bitmap);
        return ErrorInvalidPointer;
    }

    FREE_IMAGE_TYPE imageType = FreeImage_GetImageType(bitmap);

    typedef FIBITMAP *(*ConvertedImageFunc)(FIBITMAP *);

    ConvertedImageFunc convertedImageFunc;

    switch (imageType)
    {
    case FIT_BITMAP:
    {
        unsigned int bpp = FreeImage_GetBPP(bitmap);
        if (bpp != 32)
        {
            convertedImageFunc = FreeImage_ConvertTo32Bits;
            break;
        }
        else
        {
            *output = bitmap;
            return Success;
        }
    }

    case FIT_UINT16:
    case FIT_INT16:
    case FIT_RGB16:

    case FIT_DOUBLE:
    case FIT_COMPLEX:

        convertedImageFunc = FreeImage_ConvertToRGBA16;
        break;
    case FIT_UINT32:
    case FIT_INT32:
    case FIT_FLOAT:
    case FIT_RGBF:
        convertedImageFunc = FreeImage_ConvertToRGBAF;
        break;
    case FIT_RGBA16:
    case FIT_RGBAF:
    {
        *output = bitmap;
        return Success;
    }
    default:
    {
        FreeImage_Unload(bitmap);
        return ErrorConvertImageToAlphaUnsupportedInputFormat;
    }
    }

    FIBITMAP *imageResult = convertedImageFunc(bitmap);
    FreeImage_Unload(bitmap);
    if (!imageResult)
    {
        return ErrorOnConvertImageToAlpha;
    }
    *output = imageResult;
    return Success;
}

ImageResult ASTCDataTypeFromImageWithAlpha(FIBITMAP *bitmap, astcenc_type *output)
{
    unsigned int bpp = FreeImage_GetBPP(bitmap);
    unsigned int perChannel = bpp / 4; // 4 is because 4 channels are considered
    switch (perChannel)
    {
    case 8:
        *output = ASTCENC_TYPE_U8;
        return Success;
    case 16:
        *output = ASTCENC_TYPE_F16;
        return Success;
    case 32:
        *output = ASTCENC_TYPE_F32;
        return Success;
    default:
        return ErrorWrongAlphaImage;
    }
}
