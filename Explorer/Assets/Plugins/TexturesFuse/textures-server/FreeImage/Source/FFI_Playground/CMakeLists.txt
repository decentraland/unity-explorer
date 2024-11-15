cmake_minimum_required(VERSION 3.10)

# Set the C++ standard
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)
set(LIBRARY_OUTPUT_DIRECTORY "../../../../TexturesServerWrap/Libraries")

# Set additional C++ compiler options
if(WIN32)
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /Os /EHsc /DNDULOCL /DNO_LCMS /D__ANSI__")
else()
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Os -fexceptions -fvisibility=default -Wno-ctor-dtor-privacy -DNO_LCMS -D__ANSI__ -stdlib=libc++ -Wc++11-narrowing")
endif()

if(WIN32)
  add_definitions(-D_CRT_SECURE_NO_WARNINGS)
  add_definitions(-DOPJ_STATIC)
  set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} /ignore:4217")
  set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /ignore:4217")
endif()

if(APPLE)
    #xcrun --show-sdk-path
    set(CMAKE_OSX_SYSROOT "/Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk")
    set(MACOSX_DEPLOYMENT_TARGET 10.11)
    set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64")
    #add default headers
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -isysroot ${CMAKE_OSX_SYSROOT} -mmacosx-version-min=${MACOSX_DEPLOYMENT_TARGET}")
endif()






project(TexturesFusePlayground)

set(USE_PREBUILDED_ASTC OFF)

add_subdirectory("../FFI" library_build)

add_executable(playground main.cpp)

target_link_libraries(playground PRIVATE texturesfuse)

include(${CMAKE_SOURCE_DIR}/../cmake_configs/cmp_config.cmake)

target_include_directories(playground PRIVATE ${COMPRESSATOR_ROOT}/include)
target_include_directories(playground PRIVATE ${CMAKE_SOURCE_DIR}/..)
target_include_directories(playground PRIVATE ${CMAKE_SOURCE_DIR}/../FFI/Include)
target_include_directories(playground PRIVATE ${CMAKE_SOURCE_DIR}/../FFI/MemoryHandles)
target_include_directories(playground PRIVATE ${CMAKE_SOURCE_DIR}/../FFI)


# if(WIN32)
#     target_link_libraries(
#         playground PRIVATE
#         ${CMAKE_CURRENT_BINARY_DIR}/../../../../../TexturesServerWrap/Libraries/Windows/libastcenc-shared.lib
#     )    
# else()
#     target_link_libraries(
#         playground PRIVATE
#         ${CMAKE_CURRENT_BINARY_DIR}/../../../../../TexturesServerWrap/Libraries/Mac/libastcenc-shared.dylib
#     )    
# endif()