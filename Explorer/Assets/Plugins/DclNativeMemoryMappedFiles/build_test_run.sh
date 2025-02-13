./build.sh
clang test_run.c -L. -lDCL_NMMF
export DYLD_LIBRARY_PATH=$(pwd)
