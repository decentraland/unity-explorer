./build.sh
clang nmmf_test_run.c -L. -lDCL_NMMF
export DYLD_LIBRARY_PATH=$(pwd)
