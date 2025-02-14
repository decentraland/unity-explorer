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
    enum PH_Error : int
    {
        Ok = 0,
        ProcessIsRunning = 1,
        CannotStartProcess = 2,
        ProcessIsNotRunning = 3,
    };

    FFI_API PH_Error processeshub_start(char *processExePath);

    FFI_API bool processeshub_is_running();

    FFI_API PH_Error processeshub_stop();
    
    FFI_API size_t processeshub_used_ram();
}