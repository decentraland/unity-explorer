# Minimum CMake version required
cmake_minimum_required(VERSION 3.10)

# Project name and version
project(ProcessesHub VERSION 1.0)

# Set the C++ standard
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)

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

add_executable(process_example main.cpp)