#ifndef AL_LIB_H
#define AL_LIB_H

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*AL_LogCallback)(const char *message);

void AL_Init(AL_LogCallback callback);

void AL_Log(const char *format, ...);

#ifdef __cplusplus
}
#endif

#endif // AL_LIB_H