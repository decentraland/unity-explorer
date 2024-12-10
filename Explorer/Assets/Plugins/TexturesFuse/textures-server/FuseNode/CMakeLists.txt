#Windows only

cmake_minimum_required(VERSION 3.10)

# Set the C++ standard
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)
set(LIBRARY_OUTPUT_DIRECTORY "../../../../TexturesServerWrap/Libraries")

# Set additional C++ compiler options
if(WIN32)
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /Os /EHsc /DNDULOCL /DNO_LCMS /D__ANSI__")
endif()

if(WIN32)
  add_definitions(-D_CRT_SECURE_NO_WARNINGS)
  add_definitions(-DOPJ_STATIC)
  set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} /ignore:4217")
  set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /ignore:4217")
endif()

project(TexturesFuseNode)

add_executable(node main.cpp)

target_link_libraries(node PRIVATE ${CMAKE_SOURCE_DIR}/libs/astcenc-native-shared.lib)
target_link_libraries(node PRIVATE ${CMAKE_SOURCE_DIR}/libs/texturesfuse.lib)

target_include_directories(node PRIVATE ${CMAKE_SOURCE_DIR}/include)


include(${CMAKE_SOURCE_DIR}/../FreeImage/Source/cmake_configs/cmp_config.cmake)

set(COMPRESSATOR_ROOT ${CMAKE_SOURCE_DIR}/../Compressator)
set(COMPRESSATOR_ORIGIN_ROOT ${CMAKE_SOURCE_DIR}/../CompressonatorWorkaround/compressonator)

message("COMPRESSATOR_ROOT Is: ${COMPRESSATOR_ROOT}")
message("COMPRESSATOR_ORIGIN_ROOT Is: ${COMPRESSATOR_ORIGIN_ROOT}")

target_include_directories(node PRIVATE ${COMPRESSATOR_ROOT}/include)
target_include_directories(node PRIVATE ${CMAKE_SOURCE_DIR}/../FreeImage/Source)
target_include_directories(node PRIVATE ${CMAKE_SOURCE_DIR}/../FreeImage/Source/FFI/Include)
target_include_directories(node PRIVATE ${CMAKE_SOURCE_DIR}/../FreeImage/Source/FFI/MemoryHandles)
target_include_directories(node PRIVATE ${CMAKE_SOURCE_DIR}/../FreeImage/Source/FFI)