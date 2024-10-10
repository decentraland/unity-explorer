#run from /Build dir

clear
cmake .. -DCMAKE_BUILD_TYPE=Release 

cd astc-encoder-build
make clean
make -j10;
cd ..

make clean
make -j10;