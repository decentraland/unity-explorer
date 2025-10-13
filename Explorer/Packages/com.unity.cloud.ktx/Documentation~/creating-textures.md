# Creating Textures

You can use the command line tools `toktx` (comes with [KTX-Software][ktxsoftware]) to create KTX&trade; v2.0 files and `basisu` (part of [Basis Universal][basisu]) to create .basis files.

The default texture orientation of both of those tools (right-down) does not match Unity's orientation (right-up). To counter-act, you can provide a parameter to flip textures in the vertical axis (Y). This is recommended, if you use the textures in Unity only. The parameters are:

- `--lower_left_maps_to_s0t0` for `toktx`
- `--y_flip` for `basisu`

Example usage:

```bash
# For KTX files:
# Create regular KTX file from an input image
toktx --bcmp regular.ktx2 input.png
# Create a y-flipped KTX file, fit for Unity out of the box
toktx --lower_left_maps_to_s0t0 --bcmp unity_flipped.ktx2 input.png


# For Basis files:
# Create regular basis file from an input image
basisu -output_file regular.basis input.png
# Create a y-flipped basis file, fit for Unity out of the box
basisu -y_flip -output_file unity_flipped.basis input.png
```

If changing the orientation of your texture files is not an option, you can correct it by applying it flipped at run-time (see [How To](./how-to.md)).

## Trademarks

*Unity* is a registered trademark of [Unity Technologies][unity].

Khronos&reg; and the Khronos Group logo are registered trademarks of the [The Khronos Group Inc][khronos].

KTX&trade; and the KTX logo are trademarks of the [The Khronos Group Inc][khronos].

[basisu]: https://github.com/BinomialLLC/basis_universal
[khronos]: https://www.khronos.org
[ktxsoftware]: https://github.com/KhronosGroup/KTX-Software
[unity]: https://unity.com
