#test linking

cmake_minimum_required(VERSION 3.10)

add_executable(TestExe)

target_include_directories(TestExe PRIVATE ${CMAKE_SOURCE_DIR}/include)
target_sources(TestExe PRIVATE main.cpp)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Framework.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core_AVX.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core_AVX512.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core_SSE.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Core.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Common.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_Compressonator.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libCMP_GpuDecode.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libImage_DDS.a)
target_link_libraries(TestExe ${CMAKE_SOURCE_DIR}/lib/libImage_TGA.a)