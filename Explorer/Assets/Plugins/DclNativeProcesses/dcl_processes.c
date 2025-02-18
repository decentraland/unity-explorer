#include "dcl_processes.h"

#ifdef _WIN32
#include <processthreadsapi.h>
#include <psapi.h>
#endif

#ifdef __APPLE__
#include <sys/sysctl.h>
#include <sys/types.h>
#include <stdlib.h>
#include <string.h>
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
