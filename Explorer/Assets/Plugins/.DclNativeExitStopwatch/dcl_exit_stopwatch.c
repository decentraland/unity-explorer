// Windows only

#define _CRT_SECURE_NO_WARNINGS

#include <windows.h>
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

static void usage(void)
{
    fprintf(stderr,
        "Usage:\n"
        "  exit-timer.exe --target-pid <pid> -o <file>\n");
}

int main(int argc, char** argv)
{
    DWORD target_pid = 0;
    const char* output_path = NULL;

    for (int i = 1; i < argc; ++i)
    {
        if (strcmp(argv[i], "--target-pid") == 0)
        {
            if (i + 1 >= argc)
            {
                usage();
                return 1;
            }

            target_pid = (DWORD)strtoul(argv[++i], NULL, 10);
        }
        else if (strcmp(argv[i], "-o") == 0)
        {
            if (i + 1 >= argc)
            {
                usage();
                return 1;
            }

            output_path = argv[++i];
        }
    }

    if (target_pid == 0 || output_path == NULL)
    {
        usage();
        return 1;
    }

    HANDLE process =
        OpenProcess(SYNCHRONIZE, FALSE, target_pid);

    if (!process)
    {
        fprintf(stderr,
            "OpenProcess failed. pid=%lu error=%lu\n",
            target_pid,
            GetLastError());

        return 1;
    }

    LARGE_INTEGER freq;
    LARGE_INTEGER start;
    LARGE_INTEGER end;

    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    WaitForSingleObject(process, INFINITE);

    QueryPerformanceCounter(&end);

    double elapsed_ms =
        ((double)(end.QuadPart - start.QuadPart) * 1000.0) /
        (double)freq.QuadPart;

    FILE* f = fopen(output_path, "w");

    if (!f)
    {
        fprintf(stderr,
            "Failed to open output file: %s\n",
            output_path);

        CloseHandle(process);
        return 1;
    }

    fprintf(f, "%.3f\n", elapsed_ms);

    fclose(f);
    CloseHandle(process);

    return 0;
}
