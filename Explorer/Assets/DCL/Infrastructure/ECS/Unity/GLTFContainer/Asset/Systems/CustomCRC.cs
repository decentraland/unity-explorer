using UnityEngine;

namespace DCL.Rendering.RenderSystem
{
    public class CustomCRC
    {
        private static uint[] crcTable;

        static CustomCRC()
        {
            // Initialize CRC32 table (same polynomial Unity uses: 0xEDB88320)
            crcTable = new uint[256];
            uint poly = 0xedb88320;

            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ poly;
                    else
                        crc >>= 1;
                }

                crcTable[i] = crc;
            }
        }

        // CRC feed for a single uint32
        public static uint CRCFeed(uint crc, uint value)
        {
            crc = crcTable[(crc ^ (value & 0xFF)) & 0xFF] ^ (crc >> 8);
            crc = crcTable[(crc ^ ((value >> 8) & 0xFF)) & 0xFF] ^ (crc >> 8);
            crc = crcTable[(crc ^ ((value >> 16) & 0xFF)) & 0xFF] ^ (crc >> 8);
            crc = crcTable[(crc ^ ((value >> 24) & 0xFF)) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        // CRC feed for int (converted to uint)
        public static uint CRCFeed(uint crc, int value)
        {
            return CRCFeed(crc, unchecked((uint)value));
        }

        // CRC feed for float
        public static uint CRCFeed(uint crc, float value)
        {
            // Convert float to uint bits representation
            byte[] bytes = System.BitConverter.GetBytes(value);
            uint uintValue = System.BitConverter.ToUInt32(bytes, 0);
            return CRCFeed(crc, uintValue);
        }

        // CRC feed for Vector4
        public static uint CRCFeed(uint crc, Vector4 value)
        {
            crc = CRCFeed(crc, value.x);
            crc = CRCFeed(crc, value.y);
            crc = CRCFeed(crc, value.z);
            crc = CRCFeed(crc, value.w);
            return crc;
        }

        // CRC feed for bool
        public static uint CRCFeed(uint crc, bool value)
        {
            return CRCFeed(crc, value ? 1u : 0u);
        }

        // CRC feed for byte array
        public static uint CRCFeed(uint crc, byte[] data)
        {
            foreach (byte b in data)
            {
                crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return crc;
        }
    }
}
