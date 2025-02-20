#include "processeshub.h"
#include <Windows.h>
#include <psapi.h>

PROCESS_INFORMATION currentProcess = {0};
const PROCESS_INFORMATION EMPTY_INFO = {0};

PH_Error processeshub_start(char *processExePath)
{
    if (processeshub_is_running())
    {
        return PH_Error::ProcessIsRunning;
    }

    STARTUPINFOA si = {0};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    if (CreateProcessA(
            NULL,
            processExePath,
            NULL,               // Process security attributes
            NULL,               // Thread security attributes
            FALSE,              // Inherit handles
            CREATE_NEW_CONSOLE, // Use shell execution behavior
            NULL,               // Environment (NULL to use parent's environment)
            NULL,               // Current directory (NULL to use parent's directory)
            &si,
            &currentProcess))
    {
        return PH_Error::Ok;
    }
    else
    {
        currentProcess = EMPTY_INFO;
        return PH_Error::CannotStartProcess;
    }
}

bool processeshub_is_running()
{
    if (memcmp(&currentProcess, &EMPTY_INFO, sizeof(EMPTY_INFO)) == 0)
    {
        return false;
    }

    DWORD result = WaitForSingleObject(currentProcess.hProcess, 0);
    return result == WAIT_TIMEOUT;
}

PH_Error processeshub_stop()
{
    if (!processeshub_is_running())
    {
        return PH_Error::ProcessIsNotRunning;
    }

    TerminateProcess(currentProcess.hProcess, 0);
    CloseHandle(currentProcess.hProcess);
    CloseHandle(currentProcess.hThread);
    currentProcess = EMPTY_INFO;
    return PH_Error::Ok;
}

size_t processeshub_used_ram()
{
    if (!processeshub_is_running())
    {
        return 0;
    }

    PROCESS_MEMORY_COUNTERS memInfo;

    if (GetProcessMemoryInfo(currentProcess.hProcess, &memInfo, sizeof(memInfo)))
    {
        return memInfo.WorkingSetSize;
    }

    return 0;
}