#ifdef __APPLE__

#include <stdio.h>
#include <stdlib.h>
#include <unistd.h> // For sleep()
#include "dcl_mutex.h"

int main() {
    const char* name = "dcl_tracking";

    int error = 0;
    void* mutex =  dcl_mutex_new(name, &error);

    if (error != 0) {
        printf("sem_open failed, error %d", error);
        exit(EXIT_FAILURE);
    }

    printf("Waiting for mutex...\n");
    int result = dcl_mutex_wait(mutex);

    printf("Result is: %d", result);

    printf("Inside critical section. PID: %d\n", getpid());
    sleep(3);  // Simulate work
    printf("Releasing semaphore...\n");

    dcl_mutex_release   (mutex); 
    dcl_mutex_close_handle(mutex);
    return 0;
}

#endif
