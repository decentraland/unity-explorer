using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum Mode
    {
        RGB,
        ASTC_4x4,
        ASTC_5x5,
        ASTC_6x6,
        ASTC_8x8,
        ASTC_10x10,
        ASTC_12x12,
        BC7,
    }

    public static class ModeExtensions
    {
        public static bool IsASTC(this Mode mode) =>
            mode is not Mode.RGB;

        public static TextureFormat AsASTCTextureFormatOrFatalError(this Mode mode) =>
            mode switch
            {
                Mode.RGB => throw new Exception("Mode is not ASTC"),
                Mode.ASTC_4x4 => TextureFormat.ASTC_4x4,
                Mode.ASTC_5x5 => TextureFormat.ASTC_5x5,
                Mode.ASTC_6x6 => TextureFormat.ASTC_6x6,
                Mode.ASTC_8x8 => TextureFormat.ASTC_8x8,
                Mode.ASTC_10x10 => TextureFormat.ASTC_10x10,
                Mode.ASTC_12x12 => TextureFormat.ASTC_12x12,
                Mode.BC7 => TextureFormat.BC7,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, string.Empty)
            };

        public static Result<uint> ASTCChunkSize(this Mode mode) =>
            mode switch
            {
                Mode.BC7 => Result<uint>.ErrorResult("Mode is not ASTC"),
                Mode.RGB => Result<uint>.ErrorResult("Mode is not ASTC"),
                Mode.ASTC_4x4 => Result<uint>.SuccessResult(4),
                Mode.ASTC_5x5 => Result<uint>.SuccessResult(5),
                Mode.ASTC_6x6 => Result<uint>.SuccessResult(6),
                Mode.ASTC_8x8 => Result<uint>.SuccessResult(8),
                Mode.ASTC_10x10 => Result<uint>.SuccessResult(10),
                Mode.ASTC_12x12 => Result<uint>.SuccessResult(12),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, string.Empty),
            };
    }
}
