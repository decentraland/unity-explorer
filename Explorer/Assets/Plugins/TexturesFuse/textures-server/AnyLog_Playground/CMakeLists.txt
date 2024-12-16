cmake_minimum_required(VERSION 3.10)

add_executable(playground main.c)

if (NOT TARGET anylog)
    add_subdirectory("../AnyLog" anylog_build)
endif()
target_link_libraries(playground PRIVATE anylog)
target_include_directories(playground PRIVATE "../AnyLog")