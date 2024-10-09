#run from /Build dir

clear
cmake .. -DCMAKE_BUILD_TYPE=Release
make clean
make -j10;