// Test case for Compressonator

#include "texture.h"
#include "compressonator.h"
#include <iostream>

int main()
{
    std::cout << "Hello!\n";

// Define input texture (dummy data here)
    const int width = 512;
    const int height = 512;
    CMP_BYTE* imageData = new CMP_BYTE[width * height * 4];  // RGBA 8-bit format (4 channels)

    // Initialize dummy data (in a real scenario, load actual image data)
    for (int i = 0; i < width * height * 4; i++) {
        imageData[i] = static_cast<CMP_BYTE>(i % 256);
    }

    // Set up the source texture (RGBA 8888 format)
    CMP_Texture sourceTexture;
    sourceTexture.dwSize = sizeof(CMP_Texture);
    sourceTexture.dwWidth = width;
    sourceTexture.dwHeight = height;
    sourceTexture.dwPitch = 0;  // Let Compressonator handle the pitch
    sourceTexture.format = CMP_FORMAT_RGBA_8888;  // Input format is RGBA 8-bit
    sourceTexture.dwDataSize = width * height * 4;
    sourceTexture.pData = imageData;

    // Set up destination texture (BC5 format)
    CMP_Texture destTexture;
    destTexture.dwSize = sizeof(CMP_Texture);
    destTexture.dwWidth = width;
    destTexture.dwHeight = height;
    destTexture.dwPitch = 0;  // Compressonator will compute the pitch for BC5
    destTexture.format = CMP_FORMAT_BC5;  // Target format is BC5
    destTexture.dwDataSize = CMP_CalculateBufferSize(&destTexture);  // Calculate required memory for BC5 compression
    destTexture.pData = new CMP_BYTE[destTexture.dwDataSize];  // Allocate memory for compressed data


    CMP_CompressOptions options;

    // Perform compression to BC5 format
    CMP_ERROR result = CMP_ConvertTexture(&sourceTexture, &destTexture, &options, nullptr);

    if (result == CMP_OK) {
        std::cout << "Successfully compressed to BC5!" << std::endl;
    } else {
        std::cerr << "Error during compression: " << result << std::endl;
    }

    // Decompress back to RGBA 8-bit for verification (if needed)
    CMP_Texture decompressedTexture;
    decompressedTexture.dwSize = sizeof(CMP_Texture);
    decompressedTexture.dwWidth = width;
    decompressedTexture.dwHeight = height;
    decompressedTexture.dwPitch = 0;
    decompressedTexture.format = CMP_FORMAT_RGBA_8888;  // Decompressed to original format
    decompressedTexture.dwDataSize = width * height * 4;  // Allocate space for decompressed data
    decompressedTexture.pData = new CMP_BYTE[decompressedTexture.dwDataSize];

    result = CMP_ConvertTexture(&destTexture, &decompressedTexture, nullptr, nullptr);

    if (result == CMP_OK) {
        std::cout << "Successfully decompressed back to RGBA!" << std::endl;
    } else {
        std::cerr << "Error during decompression: " << result << std::endl;
    }

    // Cleanup memory
    delete[] imageData;
    delete[] destTexture.pData;
    delete[] decompressedTexture.pData;

    return 0;
}