cmake_minimum_required(VERSION 3.10)

project(AnyLog)

add_library(anylog STATIC ./anylog.c)
target_include_directories(anylog PUBLIC ./)