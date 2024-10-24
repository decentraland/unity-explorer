// #include "FreeImage.h"
// #include "texturesfuse.h"
#include "bitmaps.h"
#include <string>

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

    LogImageInfo(image, "From memory: ");
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
        convertedImageFunc = FreeImage_ConvertTo32Bits;
        break;
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
    FreeImage_OutputMessageProc(FIF_UNKNOWN, "Image bpp per channel with alpha is %i", perChannel);
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

const char *NameFromImageType(FREE_IMAGE_TYPE imageType)
{
    switch (imageType)
    {
    case FIT_BITMAP:
        return "FIT_BITMAP";
    case FIT_UINT16:
        return "FIT_UINT16";
    case FIT_INT16:
        return "FIT_INT16";
    case FIT_UINT32:
        return "FIT_UINT32";
    case FIT_INT32:
        return "FIT_INT32";
    case FIT_FLOAT:
        return "FIT_FLOAT";
    case FIT_DOUBLE:
        return "FIT_DOUBLE";
    case FIT_COMPLEX:
        return "FIT_COMPLEX";
    case FIT_RGB16:
        return "FIT_RGB16";
    case FIT_RGBA16:
        return "FIT_RGBA16";
    case FIT_RGBF:
        return "FIT_RGBF";
    case FIT_RGBAF:
        return "FIT_RGBAF";
    case FIT_UNKNOWN:
    default:
        return "FIT_UNKNOWN";
    }
}

const char *NameFromColorType(FREE_IMAGE_COLOR_TYPE colorType)
{
    switch (colorType)
    {
    case FIC_MINISWHITE:
        return "FIC_MINISWHITE";
    case FIC_MINISBLACK:
        return "FIC_MINISBLACK";
    case FIC_RGB:
        return "FIC_RGB";
    case FIC_PALETTE:
        return "FIC_PALETTE";
    case FIC_RGBALPHA:
        return "FIC_RGBALPHA";
    case FIC_CMYK:
        return "FIC_CMYK";
    default:
        return "UNKNOWN_COLOR";
    }
}

const char *ProfileNameFromBitmap(FIBITMAP *bitmap)
{
    FIICCPROFILE *profile = FreeImage_GetICCProfile(bitmap);
    if (profile && profile->data && profile->size > 0)
    {
        //TODO maybe no null terminator
        return (const char *)profile->data;
    }
    return "Unknown Profile";
}

void LogImageInfo(FIBITMAP *bitmap, const char *prefix)
{
    const int width = static_cast<int>(FreeImage_GetWidth(bitmap));
    const int height = static_cast<int>(FreeImage_GetHeight(bitmap));
    const int bpp = static_cast<int>(FreeImage_GetBPP(bitmap));
    const char *imageType = NameFromImageType(FreeImage_GetImageType(bitmap));
    const char *colorType = NameFromColorType(FreeImage_GetColorType(bitmap));
    const char *profileName = "";//ProfileNameFromBitmap(bitmap);

    FreeImage_OutputMessageProc(
        FIF_UNKNOWN,
        "%sImage info: width %i, height %i, bpp %i, image type %s, color type %s, profile type %s",
        prefix,
        width,
        height,
        bpp,
        imageType,
        colorType,
        profileName);
}

void SwapRGBAtoBGRA(BYTE *imageData, const size_t length)
{
    // Not best way, could be optimized with multithreading or with GPU
    // swap RGBA -> BGRA
    for (size_t i = 0; i < length; i += 4)
    {
        BYTE buffer = imageData[i];
        imageData[i] = imageData[i + 2];
        imageData[i + 2] = buffer;
    }
}