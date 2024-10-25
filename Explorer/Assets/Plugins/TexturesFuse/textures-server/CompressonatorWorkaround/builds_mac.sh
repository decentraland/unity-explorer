set -e  # Exit immediately if any command exits with a non-zero status

cd compressonator/cmp_compressonatorlib;
cmake . -DOPTION_CMP_QT=OFF -DOPTION_CMP_VULKAN=OFF -DOPTION_CMP_DIRECTX=OFF -DOPTION_CMP_OPENCV=OFF -DOPTION_ENABLE_ALL_APPS=OFF -DOPTION_BUILD_CMP_SDK=ON -DOPTION_BUILD_APPS_CMP_CLI=OFF -DOPTION_BUILD_KTX2=OFF -DSIMDE_X86_AVX_ENABLE_NATIVE_ALIASES=ON -DGLM_ENABLE_EXPERIMENTAL=OFF -DGLM_BUILD_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release; 
make clean;
make -j10;

cp libCMP_Compressonator.a ../../../Compressator/lib/libCMP_Compressonator.a

cd -;

cd compressonator/cmp_core;
cmake . -DOPTION_CMP_QT=OFF -DOPTION_CMP_VULKAN=OFF -DOPTION_CMP_DIRECTX=OFF -DOPTION_CMP_OPENCV=OFF -DOPTION_ENABLE_ALL_APPS=OFF -DOPTION_BUILD_CMP_SDK=ON -DOPTION_BUILD_APPS_CMP_CLI=OFF -DOPTION_BUILD_KTX2=OFF -DSIMDE_X86_AVX_ENABLE_NATIVE_ALIASES=ON -DGLM_ENABLE_EXPERIMENTAL=OFF -DGLM_BUILD_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release; 
make clean;
make -j10;

cp libCMP_Core.a ../../../Compressator/lib/libCMP_Core.a
cp libCMP_Core_SSE.a ../../../Compressator/lib/libCMP_Core_SSE.a
cp libCMP_Core_AVX.a ../../../Compressator/lib/libCMP_Core_AVX.a
cp libCMP_Core_AVX512.a ../../../Compressator/lib/libCMP_Core_AVX512.a