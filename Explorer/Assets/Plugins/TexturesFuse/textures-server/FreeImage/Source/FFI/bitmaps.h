#include "FreeImage.h"
#include "texturesfuse.h"

ImageResult BitmapFromMemory(BYTE *bytes, DWORD bytesLength, FIBITMAP **output);

/**
 * @param bitmap takes ownership of bitmap, don't use this reference after
 * @param maxSideSize defines the limit that the bitmap will be clamped to
 * @param output result bitmap of the operation
 * @return result of the operation
 */
ImageResult ClampedImage(FIBITMAP *bitmap, int maxSideSize, FIBITMAP **output);

/**
 * @param bitmap takes ownership of bitmap, don't use this reference after
 * @param output result bitmap of the operation
 * @return result of the operation
 */
ImageResult AlignInMultipleOf4(FIBITMAP *bitmap, FIBITMAP **output);

/**
 * @param bitmap takes ownership of bitmap, don't use this reference after
 * @param output result bitmap of the operation
 * @return result of the operation
 */
ImageResult WithAlphaImage(FIBITMAP *bitmap, FIBITMAP **output);

/**
 * @param bitmap doesn't take ownership of bitmap
 * @return result of the operation
 */
ImageResult ASTCDataTypeFromImageWithAlpha(FIBITMAP *bitmap, astcenc_type *output);

void LogImageInfo(FIBITMAP *bitmap, const char *prefix);

void SwapRGBAtoBGRA(BYTE *imageData, const size_t length);