#include "anylog.h"
#include <stdio.h>

void callback(const char *message)
{
    printf(message);
}

int main()
{
    AL_Init(callback);
    AL_Log("All is good %d", 5);
}