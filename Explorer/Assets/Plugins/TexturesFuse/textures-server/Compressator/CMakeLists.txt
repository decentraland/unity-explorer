#test linking

cmake_minimum_required(VERSION 3.10)

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED True)

#Mac Only
#xcrun --show-sdk-path
set(CMAKE_OSX_SYSROOT "/Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk")
set(MACOSX_DEPLOYMENT_TARGET 10.11)
set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64")
#add default headers
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -isysroot ${CMAKE_OSX_SYSROOT} -mmacosx-version-min=${MACOSX_DEPLOYMENT_TARGET}")

add_executable(TestExe)

target_include_directories(TestExe PRIVATE ${CMAKE_SOURCE_DIR}/include)
target_sources(TestExe PRIVATE main.cpp)

target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Framework.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Compressonator.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core_AVX.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core_AVX512.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core_SSE.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core.a)