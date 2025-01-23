#ifndef _DCL_PLATFORM_SPECIFICATIONS_
#define _DCL_PLATFORM_SPECIFICATIONS_

// This should be done CPU side really

#if SHADER_API_METAL
    #define GPUI_THREADS 256
    #define GPUI_THREADS_2D 16
#elif SHADER_API_VULKAN
    #define GPUI_THREADS 128
    #define GPUI_THREADS_2D 8
#elif SHADER_API_D3D11
    #define GPUI_THREADS 512
    #define GPUI_THREADS_2D 16
#else
    #define GPUI_THREADS 512
    #define GPUI_THREADS_2D 16
#endif

#endif