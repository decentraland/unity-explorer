#include "dcl_processes.h"

#ifdef _WIN32
#include <processthreadsapi.h>
#include <psapi.h>
#endif

char* get_process_name(pid_t pid) {

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

    return NULL;
}

void free_name(char* name) {
    free(name);
}
