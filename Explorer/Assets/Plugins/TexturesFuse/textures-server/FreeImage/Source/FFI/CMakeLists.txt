# Minimum CMake version required
cmake_minimum_required(VERSION 3.10)

# Project name and version
project(TexturesFuse VERSION 1.0)

# Set the C++ standard
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)
set(LIBRARY_OUTPUT_DIRECTORY "../../../../TexturesServerWrap/Libraries")

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

if(APPLE)
    #xcrun --show-sdk-path
    set(CMAKE_OSX_SYSROOT "/Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk")
    set(MACOSX_DEPLOYMENT_TARGET 10.11)
    set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64")
    #add default headers
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -isysroot ${CMAKE_OSX_SYSROOT} -mmacosx-version-min=${MACOSX_DEPLOYMENT_TARGET}")
endif()

add_definitions(-DFFI_TEXTURESFUSE)

# Enable RGB space instead BGR
add_definitions(-DFREEIMAGE_COLORORDER=1)


# Add ASTC encoder
add_definitions(-DASTCENC_SHAREDLIB)
add_definitions(-DASTCENC_UNIVERSAL_BUILD)
add_definitions(-DASTCENC_DYNAMIC_LIBRARY)
add_subdirectory(
    ../../../astc-encoder
    ${CMAKE_BINARY_DIR}/astc-encoder-build
)


message("Flags are: ${CMAKE_CXX_FLAGS}")

set(JPEG_SOURCES
    ../LibJPEG/jaricom.c
    ../LibJPEG/jcapimin.c
    ../LibJPEG/jcapistd.c
    ../LibJPEG/jcarith.c
    ../LibJPEG/jccoefct.c
    ../LibJPEG/jccolor.c
    ../LibJPEG/jcdctmgr.c
    ../LibJPEG/jchuff.c
    ../LibJPEG/jcinit.c
    ../LibJPEG/jcmainct.c
    ../LibJPEG/jcmarker.c
    ../LibJPEG/jcmaster.c
    ../LibJPEG/jcomapi.c
    ../LibJPEG/jcparam.c
    ../LibJPEG/jcprepct.c
    ../LibJPEG/jcsample.c
    ../LibJPEG/jctrans.c
    ../LibJPEG/jdapimin.c
    ../LibJPEG/jdapistd.c
    ../LibJPEG/jdarith.c
    ../LibJPEG/jdatadst.c
    ../LibJPEG/jdatasrc.c
    ../LibJPEG/jdcoefct.c
    ../LibJPEG/jdcolor.c
    ../LibJPEG/jddctmgr.c
    ../LibJPEG/jdhuff.c
    ../LibJPEG/jdinput.c
    ../LibJPEG/jdmainct.c
    ../LibJPEG/jdmarker.c
    ../LibJPEG/jdmaster.c
    ../LibJPEG/jdmerge.c
    ../LibJPEG/jdpostct.c
    ../LibJPEG/jdsample.c
    ../LibJPEG/jdtrans.c
    ../LibJPEG/jerror.c
    ../LibJPEG/jfdctflt.c
    ../LibJPEG/jfdctfst.c
    ../LibJPEG/jfdctint.c
    ../LibJPEG/jidctflt.c
    ../LibJPEG/jidctfst.c
    ../LibJPEG/jidctint.c
    ../LibJPEG/jmemmgr.c
    ../LibJPEG/jmemnobs.c
    ../LibJPEG/jquant1.c
    ../LibJPEG/jquant2.c
    ../LibJPEG/jutils.c
    ../LibJPEG/transupp.c
)

set(JPEG_INCLUDE
    ../LibJPEG/
)

# End JPEG

# TIFF

set(TIFF_SOURCES
    ../LibTIFF4/tif_aux.c
    ../LibTIFF4/tif_close.c
    ../LibTIFF4/tif_codec.c
    ../LibTIFF4/tif_color.c
    ../LibTIFF4/tif_compress.c
    ../LibTIFF4/tif_dir.c
    ../LibTIFF4/tif_dirinfo.c
    ../LibTIFF4/tif_dirread.c
    ../LibTIFF4/tif_dirwrite.c
    ../LibTIFF4/tif_dumpmode.c
    ../LibTIFF4/tif_error.c
    ../LibTIFF4/tif_extension.c
    ../LibTIFF4/tif_fax3.c
    ../LibTIFF4/tif_fax3sm.c
    ../LibTIFF4/tif_flush.c
    ../LibTIFF4/tif_getimage.c
    ../LibTIFF4/tif_jpeg.c
    ../LibTIFF4/tif_luv.c
    ../LibTIFF4/tif_lzma.c
    ../LibTIFF4/tif_lzw.c
    ../LibTIFF4/tif_next.c
    ../LibTIFF4/tif_ojpeg.c
    ../LibTIFF4/tif_open.c
    ../LibTIFF4/tif_packbits.c
    ../LibTIFF4/tif_pixarlog.c
    ../LibTIFF4/tif_predict.c
    ../LibTIFF4/tif_print.c
    ../LibTIFF4/tif_read.c
    ../LibTIFF4/tif_strip.c
    ../LibTIFF4/tif_swab.c
    ../LibTIFF4/tif_thunder.c
    ../LibTIFF4/tif_tile.c
    ../LibTIFF4/tif_version.c
    ../LibTIFF4/tif_warning.c
    ../LibTIFF4/tif_write.c
    ../LibTIFF4/tif_zip.c
)

set(TIFF_INCLUDE
    ../LibTIFF4/
)

#end TIFF

# WebP

set(WEBP_SOURCES
    ../LibWebP/src/dec/alpha_dec.c
    ../LibWebP/src/dec/buffer_dec.c
    ../LibWebP/src/dec/frame_dec.c
    ../LibWebP/src/dec/idec_dec.c
    ../LibWebP/src/dec/io_dec.c
    ../LibWebP/src/dec/quant_dec.c
    ../LibWebP/src/dec/tree_dec.c
    ../LibWebP/src/dec/vp8l_dec.c
    ../LibWebP/src/dec/vp8_dec.c
    ../LibWebP/src/dec/webp_dec.c
    ../LibWebP/src/demux/anim_decode.c
    ../LibWebP/src/demux/demux.c
    ../LibWebP/src/dsp/alpha_processing.c
    ../LibWebP/src/dsp/alpha_processing_mips_dsp_r2.c
    ../LibWebP/src/dsp/alpha_processing_neon.c
    ../LibWebP/src/dsp/alpha_processing_sse2.c
    ../LibWebP/src/dsp/alpha_processing_sse41.c
    ../LibWebP/src/dsp/cost.c
    ../LibWebP/src/dsp/cost_mips32.c
    ../LibWebP/src/dsp/cost_mips_dsp_r2.c
    ../LibWebP/src/dsp/cost_sse2.c
    ../LibWebP/src/dsp/cpu.c
    ../LibWebP/src/dsp/dec.c
    ../LibWebP/src/dsp/dec_clip_tables.c
    ../LibWebP/src/dsp/dec_mips32.c
    ../LibWebP/src/dsp/dec_mips_dsp_r2.c
    ../LibWebP/src/dsp/dec_msa.c
    ../LibWebP/src/dsp/dec_neon.c
    ../LibWebP/src/dsp/dec_sse2.c
    ../LibWebP/src/dsp/dec_sse41.c
    ../LibWebP/src/dsp/enc.c
    ../LibWebP/src/dsp/enc_avx2.c
    ../LibWebP/src/dsp/enc_mips32.c
    ../LibWebP/src/dsp/enc_mips_dsp_r2.c
    ../LibWebP/src/dsp/enc_msa.c
    ../LibWebP/src/dsp/enc_neon.c
    ../LibWebP/src/dsp/enc_sse2.c
    ../LibWebP/src/dsp/enc_sse41.c
    ../LibWebP/src/dsp/filters.c
    ../LibWebP/src/dsp/filters_mips_dsp_r2.c
    ../LibWebP/src/dsp/filters_msa.c
    ../LibWebP/src/dsp/filters_neon.c
    ../LibWebP/src/dsp/filters_sse2.c
    ../LibWebP/src/dsp/lossless.c
    ../LibWebP/src/dsp/lossless_enc.c
    ../LibWebP/src/dsp/lossless_enc_mips32.c
    ../LibWebP/src/dsp/lossless_enc_mips_dsp_r2.c
    ../LibWebP/src/dsp/lossless_enc_msa.c
    ../LibWebP/src/dsp/lossless_enc_neon.c
    ../LibWebP/src/dsp/lossless_enc_sse2.c
    ../LibWebP/src/dsp/lossless_enc_sse41.c
    ../LibWebP/src/dsp/lossless_mips_dsp_r2.c
    ../LibWebP/src/dsp/lossless_msa.c
    ../LibWebP/src/dsp/lossless_neon.c
    ../LibWebP/src/dsp/lossless_sse2.c
    ../LibWebP/src/dsp/rescaler.c
    ../LibWebP/src/dsp/rescaler_mips32.c
    ../LibWebP/src/dsp/rescaler_mips_dsp_r2.c
    ../LibWebP/src/dsp/rescaler_msa.c
    ../LibWebP/src/dsp/rescaler_neon.c
    ../LibWebP/src/dsp/rescaler_sse2.c
    ../LibWebP/src/dsp/ssim.c
    ../LibWebP/src/dsp/ssim_sse2.c
    ../LibWebP/src/dsp/upsampling.c
    ../LibWebP/src/dsp/upsampling_mips_dsp_r2.c
    ../LibWebP/src/dsp/upsampling_msa.c
    ../LibWebP/src/dsp/upsampling_neon.c
    ../LibWebP/src/dsp/upsampling_sse2.c
    ../LibWebP/src/dsp/upsampling_sse41.c
    ../LibWebP/src/dsp/yuv.c
    ../LibWebP/src/dsp/yuv_mips32.c
    ../LibWebP/src/dsp/yuv_mips_dsp_r2.c
    ../LibWebP/src/dsp/yuv_neon.c
    ../LibWebP/src/dsp/yuv_sse2.c
    ../LibWebP/src/dsp/yuv_sse41.c
    ../LibWebP/src/enc/alpha_enc.c
    ../LibWebP/src/enc/analysis_enc.c
    ../LibWebP/src/enc/backward_references_cost_enc.c
    ../LibWebP/src/enc/backward_references_enc.c
    ../LibWebP/src/enc/config_enc.c
    ../LibWebP/src/enc/cost_enc.c
    ../LibWebP/src/enc/filter_enc.c
    ../LibWebP/src/enc/frame_enc.c
    ../LibWebP/src/enc/histogram_enc.c
    ../LibWebP/src/enc/iterator_enc.c
    ../LibWebP/src/enc/near_lossless_enc.c
    ../LibWebP/src/enc/picture_csp_enc.c
    ../LibWebP/src/enc/picture_enc.c
    ../LibWebP/src/enc/picture_psnr_enc.c
    ../LibWebP/src/enc/picture_rescale_enc.c
    ../LibWebP/src/enc/picture_tools_enc.c
    ../LibWebP/src/enc/predictor_enc.c
    ../LibWebP/src/enc/quant_enc.c
    ../LibWebP/src/enc/syntax_enc.c
    ../LibWebP/src/enc/token_enc.c
    ../LibWebP/src/enc/tree_enc.c
    ../LibWebP/src/enc/vp8l_enc.c
    ../LibWebP/src/enc/webp_enc.c
    ../LibWebP/src/mux/anim_encode.c
    ../LibWebP/src/mux/muxedit.c
    ../LibWebP/src/mux/muxinternal.c
    ../LibWebP/src/mux/muxread.c
    ../LibWebP/src/utils/bit_reader_utils.c
    ../LibWebP/src/utils/bit_writer_utils.c
    ../LibWebP/src/utils/color_cache_utils.c
    ../LibWebP/src/utils/filters_utils.c
    ../LibWebP/src/utils/huffman_encode_utils.c
    ../LibWebP/src/utils/huffman_utils.c
    ../LibWebP/src/utils/quant_levels_dec_utils.c
    ../LibWebP/src/utils/quant_levels_utils.c
    ../LibWebP/src/utils/random_utils.c
    ../LibWebP/src/utils/rescaler_utils.c
    ../LibWebP/src/utils/thread_utils.c
    ../LibWebP/src/utils/utils.c

)

SET(WEBP_INCLUDE
    ../LibWebP/
    ../LibWebP/src

    ../LibWebP/src/dec/
    ../LibWebP/src/dsp/
    ../LibWebP/src/enc/
    ../LibWebP/src/mux/
    ../LibWebP/src/utils/
    ../LibWebP/src/webp/
)

#end WebP

# ZLib

set(ZLIB_SOURCES
    ../ZLib/adler32.c
    ../ZLib/compress.c
    ../ZLib/crc32.c
    ../ZLib/deflate.c
    ../ZLib/gzclose.c
    ../ZLib/gzlib.c
    ../ZLib/gzread.c
    ../ZLib/gzwrite.c
    ../ZLib/infback.c
    ../ZLib/inffast.c
    ../ZLib/inflate.c
    ../ZLib/inftrees.c
    ../ZLib/trees.c
    ../ZLib/uncompr.c
    ../ZLib/zutil.c
)

SET(ZLIB_INCLUDE
    ../ZLib/
)

#end ZLIB

#LibOpenJPEG

set(OPEN_JPEG_SOURCES
    ../LibOpenJPEG/thix_manager.c
    ../LibOpenJPEG/cidx_manager.c
    ../LibOpenJPEG/tcd.c
    ../LibOpenJPEG/dwt.c
    ../LibOpenJPEG/t1.c
    ../LibOpenJPEG/raw.c
    ../LibOpenJPEG/ppix_manager.c
    ../LibOpenJPEG/jp2.c
    #../LibOpenJPEG/t1_generate_luts.c main() conflict
    ../LibOpenJPEG/opj_clock.c
    ../LibOpenJPEG/mct.c
    ../LibOpenJPEG/image.c
    ../LibOpenJPEG/t2.c
    ../LibOpenJPEG/pi.c
    ../LibOpenJPEG/invert.c
    ../LibOpenJPEG/tgt.c
    ../LibOpenJPEG/event.c
    ../LibOpenJPEG/bio.c
    ../LibOpenJPEG/cio.c
    ../LibOpenJPEG/phix_manager.c
    ../LibOpenJPEG/openjpeg.c
    ../LibOpenJPEG/function_list.c
    ../LibOpenJPEG/mqc.c
    ../LibOpenJPEG/tpix_manager.c
    ../LibOpenJPEG/j2k.c
)

set(OPEN_JPEG_INCLUDE
    ../LibOpenJPEG/
)

add_definitions(-DUSE_JPIP)

# end LibOpenJPEG

# OpenEXR

set(OPEN_EXR_SOURCES
    ../OpenEXR/IexMath/IexMathFpu.cpp
    #../OpenEXR/IlmImf/b44ExpLogTable.cpp main() conflict
    ../OpenEXR/IlmImf/ImfAcesFile.cpp
    ../OpenEXR/IlmImf/ImfAttribute.cpp
    ../OpenEXR/IlmImf/ImfB44Compressor.cpp
    ../OpenEXR/IlmImf/ImfBoxAttribute.cpp
    ../OpenEXR/IlmImf/ImfChannelList.cpp
    ../OpenEXR/IlmImf/ImfChannelListAttribute.cpp
    ../OpenEXR/IlmImf/ImfChromaticities.cpp
    ../OpenEXR/IlmImf/ImfChromaticitiesAttribute.cpp
    ../OpenEXR/IlmImf/ImfCompositeDeepScanLine.cpp
    ../OpenEXR/IlmImf/ImfCompressionAttribute.cpp
    ../OpenEXR/IlmImf/ImfCompressor.cpp
    ../OpenEXR/IlmImf/ImfConvert.cpp
    ../OpenEXR/IlmImf/ImfCRgbaFile.cpp
    ../OpenEXR/IlmImf/ImfDeepCompositing.cpp
    ../OpenEXR/IlmImf/ImfDeepFrameBuffer.cpp
    ../OpenEXR/IlmImf/ImfDeepImageStateAttribute.cpp
    ../OpenEXR/IlmImf/ImfDeepScanLineInputFile.cpp
    ../OpenEXR/IlmImf/ImfDeepScanLineInputPart.cpp
    ../OpenEXR/IlmImf/ImfDeepScanLineOutputFile.cpp
    ../OpenEXR/IlmImf/ImfDeepScanLineOutputPart.cpp
    ../OpenEXR/IlmImf/ImfDeepTiledInputFile.cpp
    ../OpenEXR/IlmImf/ImfDeepTiledInputPart.cpp
    ../OpenEXR/IlmImf/ImfDeepTiledOutputFile.cpp
    ../OpenEXR/IlmImf/ImfDeepTiledOutputPart.cpp
    ../OpenEXR/IlmImf/ImfDoubleAttribute.cpp
    ../OpenEXR/IlmImf/ImfDwaCompressor.cpp
    ../OpenEXR/IlmImf/ImfEnvmap.cpp
    ../OpenEXR/IlmImf/ImfEnvmapAttribute.cpp
    ../OpenEXR/IlmImf/ImfFastHuf.cpp
    ../OpenEXR/IlmImf/ImfFloatAttribute.cpp
    ../OpenEXR/IlmImf/ImfFloatVectorAttribute.cpp
    ../OpenEXR/IlmImf/ImfFrameBuffer.cpp
    ../OpenEXR/IlmImf/ImfFramesPerSecond.cpp
    ../OpenEXR/IlmImf/ImfGenericInputFile.cpp
    ../OpenEXR/IlmImf/ImfGenericOutputFile.cpp
    ../OpenEXR/IlmImf/ImfHeader.cpp
    ../OpenEXR/IlmImf/ImfHuf.cpp
    ../OpenEXR/IlmImf/ImfInputFile.cpp
    ../OpenEXR/IlmImf/ImfInputPart.cpp
    ../OpenEXR/IlmImf/ImfInputPartData.cpp
    ../OpenEXR/IlmImf/ImfIntAttribute.cpp
    ../OpenEXR/IlmImf/ImfIO.cpp
    ../OpenEXR/IlmImf/ImfKeyCode.cpp
    ../OpenEXR/IlmImf/ImfKeyCodeAttribute.cpp
    ../OpenEXR/IlmImf/ImfLineOrderAttribute.cpp
    ../OpenEXR/IlmImf/ImfLut.cpp
    ../OpenEXR/IlmImf/ImfMatrixAttribute.cpp
    ../OpenEXR/IlmImf/ImfMisc.cpp
    ../OpenEXR/IlmImf/ImfMultiPartInputFile.cpp
    ../OpenEXR/IlmImf/ImfMultiPartOutputFile.cpp
    ../OpenEXR/IlmImf/ImfMultiView.cpp
    ../OpenEXR/IlmImf/ImfOpaqueAttribute.cpp
    ../OpenEXR/IlmImf/ImfOutputFile.cpp
    ../OpenEXR/IlmImf/ImfOutputPart.cpp
    ../OpenEXR/IlmImf/ImfOutputPartData.cpp
    ../OpenEXR/IlmImf/ImfPartType.cpp
    ../OpenEXR/IlmImf/ImfPizCompressor.cpp
    ../OpenEXR/IlmImf/ImfPreviewImage.cpp
    ../OpenEXR/IlmImf/ImfPreviewImageAttribute.cpp
    ../OpenEXR/IlmImf/ImfPxr24Compressor.cpp
    ../OpenEXR/IlmImf/ImfRational.cpp
    ../OpenEXR/IlmImf/ImfRationalAttribute.cpp
    ../OpenEXR/IlmImf/ImfRgbaFile.cpp
    ../OpenEXR/IlmImf/ImfRgbaYca.cpp
    ../OpenEXR/IlmImf/ImfRle.cpp
    ../OpenEXR/IlmImf/ImfRleCompressor.cpp
    ../OpenEXR/IlmImf/ImfScanLineInputFile.cpp
    ../OpenEXR/IlmImf/ImfStandardAttributes.cpp
    ../OpenEXR/IlmImf/ImfStdIO.cpp
    ../OpenEXR/IlmImf/ImfStringAttribute.cpp
    ../OpenEXR/IlmImf/ImfStringVectorAttribute.cpp
    ../OpenEXR/IlmImf/ImfSystemSpecific.cpp
    ../OpenEXR/IlmImf/ImfTestFile.cpp
    ../OpenEXR/IlmImf/ImfThreading.cpp
    ../OpenEXR/IlmImf/ImfTileDescriptionAttribute.cpp
    ../OpenEXR/IlmImf/ImfTiledInputFile.cpp
    ../OpenEXR/IlmImf/ImfTiledInputPart.cpp
    ../OpenEXR/IlmImf/ImfTiledMisc.cpp
    ../OpenEXR/IlmImf/ImfTiledOutputFile.cpp
    ../OpenEXR/IlmImf/ImfTiledOutputPart.cpp
    ../OpenEXR/IlmImf/ImfTiledRgbaFile.cpp
    ../OpenEXR/IlmImf/ImfTileOffsets.cpp
    ../OpenEXR/IlmImf/ImfTimeCode.cpp
    ../OpenEXR/IlmImf/ImfTimeCodeAttribute.cpp
    ../OpenEXR/IlmImf/ImfVecAttribute.cpp
    ../OpenEXR/IlmImf/ImfVersion.cpp
    ../OpenEXR/IlmImf/ImfWav.cpp
    ../OpenEXR/IlmImf/ImfZip.cpp
    ../OpenEXR/IlmImf/ImfZipCompressor.cpp
    ../OpenEXR/Imath/ImathBox.cpp
    ../OpenEXR/Imath/ImathColorAlgo.cpp
    ../OpenEXR/Imath/ImathFun.cpp
    ../OpenEXR/Imath/ImathMatrixAlgo.cpp
    ../OpenEXR/Imath/ImathRandom.cpp
    ../OpenEXR/Imath/ImathShear.cpp
    ../OpenEXR/Imath/ImathVec.cpp
    ../OpenEXR/Iex/IexBaseExc.cpp
    ../OpenEXR/Iex/IexThrowErrnoExc.cpp
    ../OpenEXR/Half/half.cpp
    ../OpenEXR/IlmThread/IlmThread.cpp
    ../OpenEXR/IlmThread/IlmThreadMutex.cpp
    ../OpenEXR/IlmThread/IlmThreadPool.cpp
    ../OpenEXR/IlmThread/IlmThreadSemaphore.cpp
    ../OpenEXR/IexMath/IexMathFloatExc.cpp
)

set(OPEN_EXR_INCLUDE
    ../OpenEXR
    ../OpenEXR/IlmImf
    ../OpenEXR/Imath
    ../OpenEXR/Half
    ../OpenEXR/Iex
    ../OpenEXR/IlmThread
    ../OpenEXR/IexMath
)

# end OpenEXR

# PNG

add_definitions(-DPNG_ARM_NEON_OPT=0)

set(PNG_SOURCES
    ../LibPNG/png.c
    ../LibPNG/pngerror.c
    ../LibPNG/pngget.c
    ../LibPNG/pngmem.c
    ../LibPNG/pngpread.c
    ../LibPNG/pngread.c
    ../LibPNG/pngrio.c
    ../LibPNG/pngrtran.c
    ../LibPNG/pngrutil.c
    ../LibPNG/pngset.c
    ../LibPNG/pngtrans.c
    ../LibPNG/pngwio.c
    ../LibPNG/pngwrite.c
    ../LibPNG/pngwtran.c
    ../LibPNG/pngwutil.c
)

set(PNG_INCLUDE
    ../LibPNG/
)

# end PNG

set(SOURCES
    bitmaps.cpp
    main.cpp
    MemoryHandles/handles.cpp

    ../FreeImage/BitmapAccess.cpp
    ../FreeImage/CacheFile.cpp
    ../FreeImage/ColorLookup.cpp
    ../FreeImage/Conversion.cpp
    ../FreeImage/Conversion16_555.cpp
    ../FreeImage/Conversion16_565.cpp
    ../FreeImage/Conversion24.cpp
    ../FreeImage/Conversion32.cpp
    ../FreeImage/Conversion4.cpp
    ../FreeImage/Conversion8.cpp
    ../FreeImage/ConversionFloat.cpp
    ../FreeImage/ConversionRGB16.cpp
    ../FreeImage/ConversionRGBA16.cpp
    ../FreeImage/ConversionRGBAF.cpp
    ../FreeImage/ConversionRGBF.cpp
    ../FreeImage/ConversionType.cpp
    ../FreeImage/ConversionUINT16.cpp
    ../FreeImage/FreeImage.cpp
    ../FreeImage/FreeImageIO.cpp
    ../FreeImage/GetType.cpp
    ../FreeImage/Halftoning.cpp
    ../FreeImage/J2KHelper.cpp
    ../FreeImage/LFPQuantizer.cpp
    ../FreeImage/MNGHelper.cpp
    ../FreeImage/MemoryIO.cpp
    ../FreeImage/MultiPage.cpp
    ../FreeImage/NNQuantizer.cpp
    ../FreeImage/PSDParser.cpp
    ../FreeImage/PixelAccess.cpp
    ../FreeImage/TIFFLogLuv.cpp
    ../FreeImage/ToneMapping.cpp
    ../FreeImage/WuQuantizer.cpp
    ../FreeImage/ZLibInterface.cpp
    ../FreeImage/tmoColorConvert.cpp
    ../FreeImage/tmoDrago03.cpp
    ../FreeImage/tmoFattal02.cpp
    ../FreeImage/tmoReinhard05.cpp

    ../FreeImage/Plugin.cpp
    ../FreeImage/PluginG3.cpp

    # Plugins Start (also update list in Plugin.cpp)

    # ../FreeImage/PluginBMP.cpp TODO
    # ../FreeImage/PluginCUT.cpp
    # ../FreeImage/PluginDDS.cpp
    # ../FreeImage/PluginEXR.cpp
    # ../FreeImage/PluginG3.cpp
    ../FreeImage/PluginGIF.cpp
    # ../FreeImage/PluginHDR.cpp
    # ../FreeImage/PluginICO.cpp
    # ../FreeImage/PluginIFF.cpp
    # ../FreeImage/PluginJ2K.cpp
    # ../FreeImage/PluginJNG.cpp
    # ../FreeImage/PluginJP2.cpp
    ../FreeImage/PluginJPEG.cpp
    # ../FreeImage/PluginJXR.cpp
    # ../FreeImage/PluginKOALA.cpp
    # ../FreeImage/PluginMNG.cpp
    # ../FreeImage/PluginPCD.cpp
    # ../FreeImage/PluginPCX.cpp
    # ../FreeImage/PluginPFM.cpp
    # ../FreeImage/PluginPICT.cpp
    ../FreeImage/PluginPNG.cpp
    # ../FreeImage/PluginPNM.cpp
    # ../FreeImage/PluginPSD.cpp
    # ../FreeImage/PluginRAS.cpp
    # ../FreeImage/PluginRAW.cpp
    # ../FreeImage/PluginSGI.cpp
    # ../FreeImage/PluginTARGA.cpp
    ../FreeImage/PluginTIFF.cpp
    # ../FreeImage/PluginWBMP.cpp
    ../FreeImage/PluginWebP.cpp
    # ../FreeImage/PluginXBM.cpp
    # ../FreeImage/PluginXPM.cpp
    # ../FreeImage/PSDParser.cpp

    # Plugins End

    ../FreeImageToolkit/BSplineRotate.cpp
    ../FreeImageToolkit/Background.cpp
    ../FreeImageToolkit/Channels.cpp
    ../FreeImageToolkit/ClassicRotate.cpp
    ../FreeImageToolkit/Colors.cpp
    ../FreeImageToolkit/CopyPaste.cpp
    ../FreeImageToolkit/Display.cpp
    ../FreeImageToolkit/Flip.cpp
    ../FreeImageToolkit/JPEGTransform.cpp
    ../FreeImageToolkit/MultigridPoissonSolver.cpp
    ../FreeImageToolkit/Rescale.cpp
    ../FreeImageToolkit/Resize.cpp

    ../Metadata/Exif.cpp
    ../Metadata/FIRational.cpp
    ../Metadata/FreeImageTag.cpp
    ../Metadata/IPTC.cpp
    ../Metadata/TagConversion.cpp
    ../Metadata/TagLib.cpp
    ../Metadata/XTIFF.cpp

    ${WEBP_SOURCES}
    ${PNG_SOURCES}
    ${JPEG_SOURCES}
    ${TIFF_SOURCES}
    ${ZLIB_SOURCES}
    ${OPEN_JPEG_SOURCES}
    ${OPEN_EXR_SOURCES}
)

include_directories(
    /
    ../ 
    Include/
    MemoryHandles/

    ${WEBP_INCLUDE}
    ${PNG_INCLUDE} 
    ${JPEG_INCLUDE} 
    ${TIFF_INCLUDE} 
    ${ZLIB_INCLUDE} 
    ${OPEN_JPEG_INCLUDE}
    ${OPEN_EXR_INCLUDE}
)

# add_definitions(-DTEST_TEXTURESFUSE)
№ TODO rollback
add_executable(texturesfuse ${SOURCES})
# add_library(texturesfuse SHARED)

# Compressor
set(COMPRESSATOR_ROOT ${CMAKE_SOURCE_DIR}/../../../Compressator)

message("COMPRESSATOR_ROOT Is: ${COMPRESSATOR_ROOT}")

target_include_directories(texturesfuse PRIVATE ${COMPRESSATOR_ROOT}/include)

if(WIN32)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Framework.lib)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core_AVX512.lib)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core_SSE.lib)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core_AVX.lib)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core.lib)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Compressonator.lib)
else()
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Framework.a)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core_AVX512.a)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core_SSE.a)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core_AVX.a)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Core.a)
    target_link_libraries(texturesfuse ${COMPRESSATOR_ROOT}/lib/libCMP_Compressonator.a)  
endif()
  

target_sources(texturesfuse PRIVATE ${SOURCES})


if(WIN32)
    target_link_libraries(texturesfuse ${CMAKE_CURRENT_BINARY_DIR}/astc-encoder-build/Source/Release/astcenc-native-shared.lib)
else()
    target_link_libraries(texturesfuse ${CMAKE_CURRENT_BINARY_DIR}/astc-encoder-build/Source/libastcenc-shared.dylib)
endif()
