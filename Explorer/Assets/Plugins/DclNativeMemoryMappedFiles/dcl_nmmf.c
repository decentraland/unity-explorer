#ifdef __APPLE__ 

#include <sys/mman.h>
#include <sys/stat.h>        /* For mode constants */
#include <fcntl.h>           /* For O_* constants */
#include <unistd.h>
#include <sys/mman.h>
#include <errno.h>

#include "dcl_nmmf.h"

EXPORT nmmf_t dcl_nmmf_new(const char* name, off_t size) {
    nmmf_t out = {0};
    int fd = shm_open(name, O_CREAT | O_RDWR, 0666);
    if (fd == -1) {
        return out;
    }

    struct stat mapstat;
    if (fstat(fd, &mapstat) == -1) {
        close(fd);
        return out;
    }

    if (mapstat.st_size < size){
        int tr = ftruncate(fd, size);
        if (tr == -1) {
            close(fd);
            return out;
        }
    }

    void* memory = mmap(NULL, size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    if (memory == MAP_FAILED) {
        close(fd);
        return out;
    }

    out.memory = memory;
    out.fd = fd;
    out.size = size;

    return out;
}

EXPORT void dcl_nmmf_close(nmmf_t instance) {
    munmap(instance.memory, instance.size); 
    close(instance.fd);
}
#endif
