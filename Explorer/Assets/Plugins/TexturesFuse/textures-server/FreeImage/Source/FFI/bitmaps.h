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