namespace DCL.Utilities
{
    public static class UTF8Utility
    {
        /// <summary>
        /// A lookup table that tells you the size of a Unicode code point in UTF-8 given its first
        /// byte. Note that supplementary code points (4 bytes in UTF-8) require two chars in UTF-16.
        /// </summary>
        public static readonly byte[] UTF8_CHAR_SIZE;

        static UTF8Utility()
        {
            UTF8_CHAR_SIZE = new byte[256];

            for (int i = 0; i < 256; i++)
            {
                if ((i & 0b10000000) == 0)
                    UTF8_CHAR_SIZE[i] = 1;
                else if ((i & 0b11100000) == 0b11000000)
                    UTF8_CHAR_SIZE[i] = 2;
                else if ((i & 0b11110000) == 0b11100000)
                    UTF8_CHAR_SIZE[i] = 3;
                else if ((i & 0b11111000) == 0b11110000)
                    UTF8_CHAR_SIZE[i] = 4;
                else
                    UTF8_CHAR_SIZE[i] = 1;
            }
        }
    }
}
