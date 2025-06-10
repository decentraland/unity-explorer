# KTX for Unity

[![KTX Logo](Images/ktx-logo.png)][KTX]

Unity package that allows users to load [KTX&trade; 2.0][ktx] or [Basis Universal][basisu] texture files.

> [!NOTE]
> This package can be used in combination with the [com.unity.cloud.gltfast package][glTFast]. By doing so, glTF&trade; files containing [KTX 2.0][KTXSpec] texture files will be loaded. Only installing the package is required, nothing else.

## Features

Supported features include:

- Run-time loading.
- Editor (design-time) importing.
- [KTX 2.0][KTXSpec] (.ktx2) and [Basis Universal][basisu] (.basis) files.
- Basis Universal super compression modes
  - ETC1s (low quality mode)
  - UASTC (high quality mode)
- ZStd compression.
- Texture orientation.

## Trademarks

*Unity* is a registered trademark of [Unity Technologies][unity].

Khronos&reg; and the Khronos Group logo are registered trademarks of the [The Khronos Group Inc][khronos].

KTX&trade; and the KTX logo are trademarks of the [The Khronos Group Inc][khronos].

[basisu]: https://github.com/BinomialLLC/basis_universal
[glTFast]: https://github.com/Unity-Technologies/glTFast
[khronos]: https://www.khronos.org
[KTX]: https://www.khronos.org/ktx/
[KTXSpec]: https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html
[unity]: https://unity.com
