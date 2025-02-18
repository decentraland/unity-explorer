#ifdef __APPLE__

#define EXPORT __attribute__((visibility("default")))

EXPORT void* dcl_mutex_new(const char* name, int* error);

EXPORT int dcl_mutex_wait(void* mutex);

EXPORT int dcl_mutex_release(void* mutex);

EXPORT int dcl_mutex_close_handle(void* mutex);

#endif
