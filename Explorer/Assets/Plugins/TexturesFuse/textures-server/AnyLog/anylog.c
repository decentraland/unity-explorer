#include "anylog.h"
#include <stdio.h>
#include <stdarg.h>
#include <stdlib.h>

AL_LogCallback AL_logCallback = NULL;

void AL_Init(AL_LogCallback callback)
{
    AL_logCallback = callback;
    AL_Log("Any log init\n");
}

void AL_Log(const char *format, ...)
{
    if (!AL_logCallback)
    {
        return;
    }

    va_list args;
    va_start(args, format);

    int size = vsnprintf(NULL, 0, format, args) + 1;
    va_end(args);

    char *message = (char *)malloc(size);
    if (!message)
    {
        return;
    }

    va_start(args, format);
    vsnprintf(message, size, format, args);
    va_end(args);

    AL_logCallback(message);
    free(message);
}