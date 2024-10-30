# TextureFuse

## Overview

The purpose of this plugin is to provide a feature to decode, resize and load textures in runtime in multi-threading mode. TextureFuse has been created due original Unity API won't support required features.

Features:
1. 5 image formats support (PNG, JPG, WEBP, TIF, GIF)
2. Multithreading decoding on background threads
3. RGBA32, ASTC, BC5 texture formats support
4. Easy access high-level API

### C# API

To use this plugin you need an instance of object with `ITexturesUnzip` interface.
This interface provides method `TextureFromBytesAsync` that allows to asynchronously load texture from any memory chunk.

Base implementation is `TexturesUnzip` class. It doesn't guarantee thread-safety and concurrent execution.
To safely use it in multi-threading environment you need to wrap it `PooledTexturesUnzip` or `SemaphoreTexturesUnzip` that will provide a thread-safe access to `TexturesUnzip` instance.

#### TexturesUnzip

TexturesUnzip handles all native interaction under the hood and provides a high-level API to load textures.
In TexturesUnzip's constructor you can specify options such as `Max side size`, `Decoder mode` and etc.

#### TextureType

Enum `TextureType` represents the type of the texture in which it will be loaded.
In the case of `Albedo` it will load the texture in RGBA32 or ASTC format.
In the case of `Normal` it will load the texture in BC5 format.

### Supported file formats

1. PNG
2. JPG
3. WEBP
4. TIF
5. GIF

### Supported texture formats

#### RGBA32

It's a base format that doesn't provide any compression.
Each color channel is represented by 8 bits and one pixel is 32 bits in total.
Channels are stored in the following order: Red, Green, Blue, Alpha.
It's not recommended to use if you are able to use ASTC or BC5 format on the target machine.

#### ASTC

Format provides ASTC compression.
It is used as a general purpose format for albedo textures.
TexturesFuse provides an access to adjust settings via `InitOptions` and `Swizzle` structs.
To get more specific info visit encoder's git-page: https://github.com/ARM-software/astc-encoder.

#### BC5

Format provides BC5 compression.
It is used as a format for normal maps.
To get more specific info visit compressonator's git-page: https://github.com/GPUOpen-Tools/compressonator.

### Native API

#### TexturesFuseInitialize

Should be called before any other calls to the plugin.
It initializes the plugin and sets up the required resources.

#### TexturesFuseDispose

Should be called to free all allocated resources.
Will return an error code if there is any unreleased texture remains.

#### TexturesFuseRelease

Releases native memory that was allocated for the required texture.
If texture won't be released it's considered as a memory leak.

#### TexturesFuseProcessedImageFromMemory

Decodes and resizes the texture from the provided memory chunk into RGBA32 format.

#### TexturesFuseASTCImageFromMemory

Decodes and resizes the texture from the provided memory chunk into ASTC format.

#### TexturesFuseBC5ImageFromMemory

Decodes and resizes the texture from the provided memory chunk into BC5 format.

### External dependencies

1. ASTC-Encoder (ASTC decoding)
2. Compressonator (BC5 decoding)
3. FreeImage ()

## How to build

To build you need to follow these steps. 
As result you will get .dll/.dylib files respectively for windows and mac.
The files will be located in `Plugins/TexturesFuse/TexturesServerWrap/Libraries`

### Build ASTC 
 1. Ensure you pulled `astc-encoder` with submodules `git submodule update --init --recursive`
 2. Go to directory `astc-encoder`
 3. Run 
    - Mac: - `cmake . -DASTCENC_SHAREDLIB=ON -DASTCENC_UNIVERSAL_BUILD=ON -DASTCENC_DYNAMIC_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release` 
    - Win: - `cmake -G "Visual Studio 17 2022" . -DASTCENC_SHAREDLIB=ON -DASTCENC_UNIVERSAL_BUILD=ON -DASTCENC_DYNAMIC_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release`

### Build Compressonator:
 1. Follow README.md in `./textures-server/СompressonatorWorkaround`

### Build Lib
 1. Run `FreeImage/Source/FFI/build.(sh/ps1)`

## Next steps

 1. Add mipmaps generation and support (Mipmaps are supported in Compressonator)
 2. Add support for GIF playback