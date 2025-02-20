./build.sh
clang test_run.c -L. -ldcl_mutex
export DYLD_LIBRARY_PATH=$(pwd)
