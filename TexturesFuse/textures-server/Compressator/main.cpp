// Test case for Compressonator

#include "texture.h"
#include "compressonator.h"
#include <iostream>
#include <fstream>
#include <string>

struct BytesResult
{
    CMP_BYTE *data;
    size_t size;
};

std::streamsize sizeOf(std::ifstream *stream)
{
    stream->seekg(0, std::ios::end);
    std::streamsize fileSize = stream->tellg();
    stream->seekg(0, std::ios::beg);
    return fileSize;
}

BytesResult bytesFromFile(std::string path)
{
    std::ifstream file(path, std::ios::binary);

    if (!file)
    {
        throw std::invalid_argument("Could not open file");
    }

    std::streamsize fileSize = sizeOf(&file);

    CMP_BYTE *buffer = new CMP_BYTE[fileSize];

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

void BC5Compression(const int width, const int height, const std::string workDirectory)
{
    std::cout << "Start\n";

    std::string path = workDirectory + "brick_wall2-nor-512.bin";
    std::string outputPath = workDirectory + "brick_wall2-nor-512.bc5";
    std::string restoredPath = workDirectory + "brick_wall2-nor-512_restored.bin";

    const CMP_FORMAT sourceFormat = CMP_FORMAT_BGRA_8888;
    const CMP_FORMAT destFormat = CMP_FORMAT_BC5;
    const int bufferSize = width * height * 4;

    BytesResult byteResult = bytesFromFile(path);

    // CMP_BYTE *imageData = byteResult.data;
    CMP_BYTE *imageData = new CMP_BYTE[byteResult.size]; // RGBA 8-bit format (4 channels)

    // swap BGRA -> RGBA
    for (size_t i = 0; i < byteResult.size; i += 4)
    {
        imageData[i] = byteResult.data[i + 2];
        imageData[i + 1] = byteResult.data[i + 1];
        imageData[i + 2] = byteResult.data[i];
        imageData[i + 3] = byteResult.data[i + 3];
    }

    // Set up the source texture (RGBA 8888 format)
    CMP_Texture sourceTexture;
    sourceTexture.dwSize = sizeof(CMP_Texture);
    sourceTexture.dwWidth = width;
    sourceTexture.dwHeight = height;
    sourceTexture.dwPitch = 0;
    sourceTexture.format = sourceFormat;
    sourceTexture.dwDataSize = bufferSize;
    sourceTexture.pData = imageData;

    // Set up destination texture (BC5 format)
    CMP_Texture destTexture;
    destTexture.dwSize = sizeof(CMP_Texture);
    destTexture.dwWidth = width;
    destTexture.dwHeight = height;
    destTexture.dwPitch = 0;                                        // Compressonator will compute the pitch for BC5
    destTexture.format = destFormat;                                // Target format is BC5
    destTexture.dwDataSize = CMP_CalculateBufferSize(&destTexture); // Calculate required memory for BC5 compression
    destTexture.pData = new CMP_BYTE[destTexture.dwDataSize];       // Allocate memory for compressed data

    CMP_CompressOptions options;
    options.bDisableMultiThreading = true;
    options.SourceFormat = sourceFormat;
    options.DestFormat = destFormat;

    // Perform compression to BC5 format
    CMP_ERROR result = CMP_ConvertTexture(&sourceTexture, &destTexture, &options, nullptr);

    if (result == CMP_OK)
    {
        std::cout << "Successfully compressed to BC5!" << std::endl;
    }
    else
    {
        std::cerr << "Error during compression: " << result << std::endl;
    }

    CMP_BYTE *writeData = destTexture.pData;
    CMP_DWORD writeSize = destTexture.dwDataSize;

    std::ofstream output(outputPath, std::ios::binary);

    output.write(reinterpret_cast<const char *>(writeData), writeSize);

    // format swap for the backward convertion
    options.SourceFormat = destFormat;
    options.DestFormat = sourceFormat;

    result = CMP_ConvertTexture(&destTexture, &sourceTexture, &options, nullptr);

    if (result == CMP_OK)
    {
        std::cout << "Successfully restored to RGBA32 !" << std::endl;
    }
    else
    {
        std::cerr << "Error during restoring: RGBA32 " << result << std::endl;
    }

    std::ofstream outputRestored(restoredPath, std::ios::binary);

    writeData = sourceTexture.pData;
    writeSize = sourceTexture.dwDataSize;

    outputRestored.write(reinterpret_cast<const char *>(writeData), writeSize);

    delete[] imageData;
    delete[] destTexture.pData;
}

// MIP MAP Interfaces
// CMP_INT CMP_API   CMP_CalcMaxMipLevel(CMP_INT nHeight, CMP_INT nWidth, CMP_BOOL bForGPU);
// CMP_INT CMP_API   CMP_CalcMinMipSize(CMP_INT nHeight, CMP_INT nWidth, CMP_INT MipsLevel);
// CMP_INT CMP_API   CMP_GenerateMIPLevelsEx(CMP_MipSet* pMipSet, CMP_CFilterParams* pCFilterParams);
// CMP_INT CMP_API   CMP_GenerateMIPLevels(CMP_MipSet* pMipSet, CMP_INT nMinSize);
// CMP_ERROR CMP_API CMP_CreateCompressMipSet(CMP_MipSet* pMipSetCMP, CMP_MipSet* pMipSetSRC);
// CMP_ERROR CMP_API CMP_CreateMipSet(CMP_MipSet* pMipSet, CMP_INT nWidth, CMP_INT nHeight, CMP_INT nDepth, ChannelFormat channelFormat, TextureType textureType);

void MipMaps(const int width, const int height)
{
    CMP_INT mipsLevel = CMP_CalcMaxMipLevel(height, width, true);
    std::cout << "Max mipLevel: " << mipsLevel << '\n';

    CMP_INT minMipSize = CMP_CalcMinMipSize(height, width, mipsLevel);
    std::cout << "Min mipSize: " << minMipSize << '\n';

    CMP_MipSet mipSet = {};
    // mipSet.dwDataSize = sizeof(CMP_MipSet);
    // mipSet.dwHeight = height;
    // mipSet.dwWidth = width;
    // mipSet.m_TextureType = TT_2D;
    // mipSet.m_nMipLevels = mipsLevel;
    // mipSet.

    CMP_ERROR error = CMP_CreateMipSet(&mipSet, width, height, 1, CF_8bit, TT_2D);
    if (error != CMP_OK)
    {
        std::cerr << "Error!: " << error << '\n';
    }

    // CMP_INT generatedCount = CMP_GenerateMIPLevels(&mipSet, minMipSize);
    // std::cout << "Generated mipCount: " << generatedCount << '\n';

    
}

int main()
{
    const int width = 512;
    const int height = 512;
    const std::string workDirectory = "../../TexturesServerWrap/Playground/NormalMap/";

    CMP_InitFramework();

    // BC5Compression(width, height, workDirectory);
    MipMaps(width, height);//TODO
}