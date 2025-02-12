#ifdef __APPLE__

#include "dcl_mutex.h"
#include <stdio.h>
#include <stdlib.h>
#include <semaphore.h>
#include <fcntl.h>  // For O_CREAT
#include <errno.h>


EXPORT void* dcl_mutex_new(const char* name, int* error)
{
    sem_t* p = sem_open(name, O_CREAT, 0x1FF, 1);
    if(p == SEM_FAILED){
        *error = errno;
        return NULL;
    }
    *error = 0;
    return p;
}

EXPORT int dcl_mutex_wait(void* mutex)
{
    int result = sem_wait(mutex);
    return result;
}

EXPORT int dcl_mutex_release(void* mutex)
{
    int result = sem_post(mutex);
    return result;
}

EXPORT int dcl_mutex_close_handle(void* mutex)
{
    int result = sem_close(mutex);
    return result;
}

#endif
