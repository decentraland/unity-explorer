#define UNICODE

#include <stdio.h>
#include <Windows.h>
#include <psapi.h>
#include "texturesfuse.h"

const LPCWSTR InputFileAddress = L"dcl_fuse_i";
const LPCWSTR OutputFileAddress = L"dcl_fuse_o";
const LPCWSTR PipeName = L"\\\\.\\pipe\\dcl_fuse_p";

const int mb = 1024 * 1024;

const int mmfInputCapacity = mb * 16;
const int mmfOutputCapacity = mb * 4;

const int maxAllowedMemoryMB = 512;

#pragma pack(push, 1)
struct InputArgs
{
    int bytesLength;
    int maxSideLength;
    CMP_FORMAT format;
    float fQuality;
    CMP_Compute_type encodeWith;
};

struct OutputResult
{
    ImageResult code;
    int outputLength;
    unsigned int width;
    unsigned int height;
};

#pragma pack(pop)

bool InBudget(HANDLE process)
{
    PROCESS_MEMORY_COUNTERS memInfo;

    if (GetProcessMemoryInfo(process, &memInfo, sizeof(memInfo)))
    {
        return (memInfo.WorkingSetSize) < maxAllowedMemoryMB * mb;
    }

    return false;
}

void Debug(FREE_IMAGE_FORMAT fif, const char *msg)
{
    printf(msg);
    printf("\n");
}

ImageResult initialize(context **ctx)
{
    // Don't fill init options since CMP is used only
    InitOptions options = {0};
    options.ASTCProfile = 0;
    options.blockX = 4;
    options.blockY = 4;
    options.blockZ = 1;
    options.quality = 60;
    options.flags = 0;
    options.pluginsPath = "./plugins";
    options.debugLogFunc = Debug;

    return texturesfuse_initialize(options, ctx);
}

CMP_CustomOptions CustomOptionsFromArgs(const InputArgs *iArgs)
{
    CMP_CustomOptions cOptions = {0};
    cOptions.disableMultithreading = true;
    cOptions.dwnumThreads = 1;
    cOptions.fQuality = iArgs->fQuality;
    cOptions.encodeWith = iArgs->encodeWith;
    return cOptions;
}

int InputArgsFromPipe(const HANDLE hPipe, InputArgs *iArgs)
{
    DWORD bytesRead;
    BOOL readResult = ReadFile(hPipe, iArgs, sizeof(InputArgs), &bytesRead, nullptr);
    if (!readResult)
    {
        fprintf(stderr, "Error when reading, %d", GetLastError());
        return GetLastError();
    }

    printf(
        "Received message: length - %d, side - %d, format - %d, quality - %f, encode with - %d\n",
        iArgs->bytesLength,
        iArgs->maxSideLength,
        iArgs->format,
        iArgs->fQuality,
        iArgs->encodeWith);

    return 0;
}

int OpenPipe(HANDLE *hPipe)
{
    printf("Opening pipe\n");
    *hPipe = CreateFile(
        PipeName,
        GENERIC_READ | GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        0,
        nullptr);

    if (hPipe == INVALID_HANDLE_VALUE)
    {
        fprintf(stderr, "Failed to connect to named pipe. Error: %d", GetLastError());
        return 1;
    }
    printf("Pipe opened\n");
    return 0;
}

ImageResult NewImage(
    context *ctx,
    BYTE *data,
    const InputArgs *iArgs,
    OutputResult *oResult,
    BYTE **outputBytes,
    FfiHandle *handle)
{
    CMP_CustomOptions cOptions = CustomOptionsFromArgs(iArgs);

    int outputLength;
    unsigned int width;
    unsigned int height;

    ImageResult result = texturesfuse_cmp_image_from_memory(
        ctx,
        data,
        iArgs->bytesLength,
        iArgs->bytesLength,
        iArgs->format,
        cOptions,
        outputBytes,
        &outputLength,
        &width,
        &height,
        handle);

    if (result != Success)
    {
        fprintf(stderr, "Error when compressing, %d", result);

        oResult->code = result;
        oResult->outputLength = 0;
        oResult->width = 0;
        oResult->height = 0;
        return result;
    }
    else
    {
        printf("Compression finished: width %d, height %d, length %d\n", width, height, outputLength);

        oResult->code = result;
        oResult->outputLength = outputLength;
        oResult->width = width;
        oResult->height = height;
        return Success;
    }
}

int main()
{
    printf("Start Node\n");

    HANDLE selfProcess = GetCurrentProcess();

    context *ctx;
    ImageResult result = initialize(&ctx);
    if (result != Success)
    {
        fprintf(stderr, "Error on initializing: %d", result);
        return result;
    }
    printf("Context initialized\n");

    printf("Opening files\n");
    HANDLE hMmfInput = OpenFileMapping(FILE_MAP_READ, FALSE, InputFileAddress);
    if (!hMmfInput)
    {
        fprintf(stderr, "Error on opening input file: %d", GetLastError());
        return 1;
    }

    HANDLE hMmfOutput = OpenFileMapping(FILE_MAP_WRITE, FALSE, OutputFileAddress);
    if (!hMmfInput)
    {
        fprintf(stderr, "Error on opening input file: %d", GetLastError());
        return 1;
    }
    printf("Files are opened successfully\n");

    printf("File mapping\n");
    void *pInputBuffer = MapViewOfFile(hMmfInput, FILE_MAP_READ, 0, 0, mmfInputCapacity);
    if (!pInputBuffer)
    {
        fprintf(stderr, "Error when mapping, %d", GetLastError());
        return 1;
    }

    void *pOutputBuffer = MapViewOfFile(hMmfOutput, FILE_MAP_WRITE, 0, 0, mmfOutputCapacity);
    if (!pOutputBuffer)
    {
        fprintf(stderr, "Error when mapping, %d", GetLastError());
        return 1;
    }

    printf("File is mapped\n");

    HANDLE hPipe;
    if (OpenPipe(&hPipe))
    {
        CloseHandle(hMmfInput);
        CloseHandle(hMmfOutput);
        return 1;
    }

    while (1)
    {
        if (!InBudget(selfProcess))
        {
            fprintf(stderr, "Budget %d MB is overwhelmed", maxAllowedMemoryMB);
            return 1;
        }

        InputArgs iArgs;
        if (InputArgsFromPipe(hPipe, &iArgs))
        {
            fprintf(stderr, "Cannot get input args from pipe, %d", GetLastError());
            return GetLastError();
        }

        printf("Start encoding\n");

        OutputResult oResult;
        BYTE *outputBytes;
        FfiHandle handle;
        if (NewImage(ctx, reinterpret_cast<BYTE *>(pInputBuffer), &iArgs, &oResult, &outputBytes, &handle) == Success)
        {
            // Write to file
            memcpy(pOutputBuffer, outputBytes, oResult.outputLength);
            texturesfuse_release(ctx, handle);
        }

        // Write options to pipe
        DWORD writtenBytes = 0;
        BOOL writeResult = WriteFile(hPipe, &oResult, sizeof(OutputResult), &writtenBytes, nullptr);
        if (!writeResult)
        {
            fprintf(stderr, "Error when writing file, %d", GetLastError());
            return 1;
        }
    }

    return 0;
}