#include "texture.h"
#include "compressonator.h"
#include "texturesfuse.h"
#include "FreeImage.h"
#include "anylog.h"
#include <string>
#include <fstream>
#include <iostream>
#include <chrono>
// #include "common.h"

void Log(char *msg)
{
    FreeImage_OutputMessageProc(FIF_UNKNOWN, msg);
}

bool CMP_Log(float progress, CMP_DWORD_PTR user1, CMP_DWORD_PTR user2)
{
    printf("Progress %.2f, u1 %d u2 %d\n", progress, user1, user2);
    return true;
}

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

void Debug(FREE_IMAGE_FORMAT fif, const char *msg)
{
    AL_Log(msg);
    AL_Log("\n");
}

void callback(const char *message)
{
    printf(message);
}

int main()
{
    AL_Init(callback);

    InitOptions options;
    options.ASTCProfile = ASTCENC_PRF_LDR_SRGB;
    options.blockX = 6;
    options.blockY = 6;
    options.blockZ = 1;
    options.quality = 10;
    options.flags = 1;
    options.debugLogFunc = Debug;

    const std::string imagePath = "./image.jpg";
    const std::string outputPath = "./output.jpg";
    int maxSideLength = 512;

    context *context;
    auto imageResult = texturesfuse_initialize(options, &context);
    if (imageResult != Success)
    {
        std::cout << "Context init result: " << imageResult << '\n';
        return 0;
    }

    BytesResult result = bytesFromFile(imagePath);

    BYTE *output;
    int outputLength;
    unsigned int width;
    unsigned int height;
    FfiHandle handle;

    CMP_CustomOptions customOptions = {0};
    customOptions.disableMultithreading = true;
    customOptions.dwnumThreads = 1;
    customOptions.encodeWith = CMP_Compute_type::CMP_GPU_DXC;
    customOptions.fQuality = 0.05;

    auto start = std::chrono::high_resolution_clock::now();

    imageResult = texturesfuse_cmp_image_from_memory(
        context,
        result.data,
        result.size,
        maxSideLength,
        CMP_FORMAT_BC7,
        customOptions,
        &output,
        &outputLength,
        &width,
        &height,
        &handle);

    // Record the end time
    auto end = std::chrono::high_resolution_clock::now();

    // Calculate the duration in milliseconds
    std::chrono::duration<double, std::milli> duration = end - start;
    std::cout << "Execution time: " << duration.count() << " ms" << std::endl;

    std::cout << "Image result: " << imageResult << '\n';

    texturesfuse_dispose(context);
}