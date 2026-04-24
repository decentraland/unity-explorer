// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


namespace KtxUnity
{
    interface IMetaData
    {
        bool hasAlpha { get; }
    }

    interface ILevelInfo
    {
        bool isPowerOfTwo { get; }
        bool isMultipleOfFour { get; }
        bool isSquare { get; }
    }

    class MetaData : IMetaData
    {
        public bool hasAlpha { get; set; }

        public ImageInfo[] images;

        public void GetSize(out uint width, out uint height, uint imageIndex = 0, uint levelIndex = 0)
        {
            var level = images[imageIndex].levels[levelIndex];
            width = level.width;
            height = level.height;
        }

        public override string ToString()
        {
            return $"BU images:{images.Length} A:{hasAlpha}";
        }
    }

    class ImageInfo
    {
        public LevelInfo[] levels;
        public override string ToString()
        {
            return $"Image levels:{levels.Length}";
        }
    }

    class LevelInfo : ILevelInfo
    {
        public uint width;
        public uint height;

        public static bool IsPowerOfTwo(uint i)
        {
            return (i & (i - 1)) == 0;
        }

        public static bool IsMultipleOfFour(uint i)
        {
            return (i & 0x3) == 0;
        }

        public bool isPowerOfTwo => IsPowerOfTwo(width) && IsPowerOfTwo(height);

        public bool isMultipleOfFour => IsMultipleOfFour(width) && IsMultipleOfFour(height);

        public bool isSquare => width == height;

        public override string ToString()
        {
            return $"Level size {width} x {height}";
        }
    }
}
