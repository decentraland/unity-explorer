#include "dcl_processes.h"

#ifdef _WIN32
#include <processthreadsapi.h>
#include <psapi.h>
#include <process.h>   // _spawnvp, _P_NOWAIT
#include <errno.h>
#include <stdlib.h>

#define _CRT_NONSTDC_NO_DEPRECATE
#define _CRT_SECURE_NO_WARNINGS

#endif

#ifdef __APPLE__
#include <sys/sysctl.h>
#include <sys/types.h>
#include <stdlib.h>
#include <stddef.h>
#include <string.h>
#include <spawn.h> 
#include <unistd.h>
#include <errno.h>

extern char** environ;

#endif

char* get_process_name(pid_t pid) {

#ifdef _WIN32
    // Open process with query access
    HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);

    if (hProcess) {
        char* buffer = malloc(sizeof(char) * MAX_PATH);
        // Get the process name
        if (GetModuleBaseNameA(hProcess, NULL, buffer, MAX_PATH)) {
            return buffer;
        }
        else {
            free(buffer);
        }

        CloseHandle(hProcess);
    }
#endif

#ifdef __APPLE__
    size_t size;
    int mib[4] = {CTL_KERN, KERN_PROC, KERN_PROC_PID, pid};

    if (sysctl(mib, 4, NULL, &size, NULL, 0) == -1) {
        return NULL;
    }

    struct kinfo_proc *info = malloc(size);
    if (!info) {
        return NULL;
    }

    if (sysctl(mib, 4, info, &size, NULL, 0) == -1) {
        free(info);
        return NULL;
    }

    size_t len = strlen(info->kp_proc.p_comm) + 1;
    char* name = malloc(len);

    if (name == NULL)
        return NULL;

    strcpy(name, info->kp_proc.p_comm);

    free(info);
    return name;
#endif

    return NULL;
}

void free_name(char* name) {
    free(name);
}

static char** make_argv_internal(char* filename, char** args, int argc) {
    if (!filename || argc < 0) {
        errno = EINVAL;
        return NULL;
    }
    // argv = [filename, args..., NULL]
    char** argv = (char**)malloc(sizeof(char*) * (size_t)(argc + 2));
    if (!argv) {
        errno = ENOMEM;
        return NULL;
    }
    argv[0] = filename;
    for (int i = 0; i < argc; ++i) {
        argv[i + 1] = args ? args[i] : NULL;
    }
    argv[argc + 1] = NULL;
    return argv;
}

int start_process(char* filename, char** args, int argc) {
    char** argv = make_argv_internal(filename, args, argc);
    if (!argv) return -1;

#ifdef _WIN32
    // _spawnvp searches PATH (like posix_spawnp). Non-blocking, returns PID.
    intptr_t pid = _spawnvp(_P_NOWAIT, filename, (const char* const*)argv);
    int saved_errno = errno; // preserve before free
    free(argv);

    if (pid == -1) {
        errno = saved_errno;
        return -1;
    }

    // on Windows this is a process handle value cast to intptr_t
    // casting to int is valid
    return (int)pid;
#endif

#ifdef __APPLE__
    pid_t pid = -1;

    // posix_spawnp searches PATH, it inherits current env and file actions/attrs.
    int rc = posix_spawnp(
            &pid, 
            filename, 
            NULL, // file_actions
            NULL, // attrp
            argv, environ
            );
    free(argv);

    if (rc != 0) {
        errno = rc;     // posix_spawn returns an error number directly
        return -1;
    }
    return (int)pid;
#endif   
}
