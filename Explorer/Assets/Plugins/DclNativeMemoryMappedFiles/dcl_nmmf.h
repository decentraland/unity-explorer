/*
 * Used on Mac only
 *
 * nmmf - named memory mapped file
*/
#ifdef __APPLE__

#ifndef DCL_NMMF
#define DCL_NMMF

#define EXPORT __attribute__((visibility("default")))

#include <unistd.h>

typedef struct nmmf_t {
    void* memory;
    int fd;
    off_t size;
} nmmf_t;

EXPORT nmmf_t dcl_nmmf_new(const char* name, off_t size);

EXPORT void dcl_nmmf_close(nmmf_t instance);

#endif

#endif
