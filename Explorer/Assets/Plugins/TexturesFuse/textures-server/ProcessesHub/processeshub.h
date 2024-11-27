#if defined(_WIN32) || defined(_WIN64)
#ifdef BUILD_DLL
#define FFI_API __declspec(dllexport)
#else
#define FFI_API __declspec(dllimport)
#endif
#elif defined(__GNUC__) && __GNUC__ >= 4
#define FFI_API __attribute__((visibility("default"))) // Ensure symbols are visible
#else
#define FFI_API
#endif

extern "C"
{
    FFI_API int processeshub_start(char* processExePath);

    FFI_API int processeshub_is_running();
}