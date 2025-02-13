#ifdef __APPLE__

#include <stdio.h>
#include <stdlib.h>
#include "dcl_nmmf.h"

int main() {
    const char* name = "/dcl_tracking_test";

    nmmf_t nmmf = dcl_nmmf_new(name, 512);

    if (nmmf.memory == NULL) {
        printf("failed new nmmf");
        exit(EXIT_FAILURE);
    }

    dcl_nmmf_close(nmmf);
    return 0;
}

#endif
