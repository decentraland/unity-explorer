#ifndef DCL_PROCESSES
#define DCL_PROCESSES

#include <stdint.h>

#ifdef _WIN32
#include<windows.h>
typedef DWORD pid_t;

#define EXPORT __declspec(dllexport)

#endif

#ifdef __APPLE__
#include <sys/types.h>

#define EXPORT __attribute__((visibility("default")))
#endif

EXPORT char* get_process_name(pid_t pid);

EXPORT void free_name(char* name);

EXPORT int start_process(char* filename, char** args, int argc);

#endif
