using UnityEngine;

namespace TimestampEncodingTests
{
    public class TimestampEncoder
    {
        public const int BITS = 8; // Number of bits for timestamp
        public const float BUFFER = 10f; // 10 seconds buffer
        public const float QUANTUM = BUFFER / (1 << BITS); // Granularity per step: 10 / 256 = 0.0390625 seconds

        public static long Encode(float timestamp, bool isMoving)
        {
            long timestampMs = Mathf.FloorToInt(timestamp % BUFFER / QUANTUM);

            // Ensure timestamp fits into 8 bits
            long encodedTimestamp = timestampMs & 0xFF; // Keep only 8 bits
            long stateBit = isMoving ? 1L : 0L;

            // Shift state bit to the most significant position, then place timestamp
            return (stateBit << 63) | (encodedTimestamp << (63 - BITS));
        }

        public static (float Timestamp, bool IsMoving) Decode(long encodedValue)
        {
            bool isMoving = (encodedValue & (1L << 63)) != 0;
            long timestamp = (encodedValue >> (63 - BITS)) & 0xFF; // Extract 8-bit timestamp

            // Convert timestamp back to seconds
            float timestampInSeconds = timestamp * QUANTUM;
            return (timestampInSeconds, isMoving);
        }
    }

    public class Encoder
{
    private const int StateBitPosition = 63;
    private const int TimestampBits = 8;
    private const int TimestampShift = 55; // Following the state bit
    private const int XExponentBits = 3;
    private const int YExponentBits = 5;
    private const int ZExponentBits = 3;
    private const int MantissaBits = 5; // Uniform across coordinates
    private const int XShift = 47; // After timestamp
    private const int YShift = 39; // After X
    private const int ZShift = 31; // After Y
    private const int BitsPerCoordinate = XExponentBits + MantissaBits; // For X and Z
    private const int YBits = YExponentBits + MantissaBits; // For Y

    public static long Encode(bool isMoving, float timestamp, float x, float y, float z)
    {
        // Initial state and timestamp encoding
        long encoded = (isMoving ? 1L : 0L) << StateBitPosition;
        encoded |= (long)(timestamp % (1 << TimestampBits)) << TimestampShift;

        // Encode each coordinate
        encoded |= EncodeCoordinate(x, XExponentBits, MantissaBits) << XShift;
        encoded |= EncodeCoordinate(y, YExponentBits, MantissaBits) << YShift;
        encoded |= EncodeCoordinate(z, ZExponentBits, MantissaBits) << ZShift;

        return encoded;
    }

    private static long EncodeCoordinate(float coordinate, int exponentBits, int mantissaBits)
    {
        int bias = (1 << (exponentBits - 1)) - 1;
        int exponent = Mathf.FloorToInt(Mathf.Log(Mathf.Abs(coordinate), 2)) + bias;
        float mantissa = (Mathf.Abs(coordinate) / Mathf.Pow(2, exponent - bias)) - 1;
        long encodedExponent = ((long)exponent & ((1 << exponentBits) - 1)) << mantissaBits;
        long encodedMantissa = (long)(mantissa * ((1 << mantissaBits) - 1));

        return encodedExponent | encodedMantissa;
    }

    public static (bool IsMoving, float Timestamp, float X, float Y, float Z) Decode(long encoded)
    {
        bool isMoving = (encoded & (1L << StateBitPosition)) != 0;
        float timestamp = ((encoded >> TimestampShift) & ((1L << TimestampBits) - 1));

        float x = DecodeCoordinate((int)(encoded >> XShift) & ((1 << BitsPerCoordinate) - 1), XExponentBits, MantissaBits);
        float y = DecodeCoordinate((int)(encoded >> YShift) & ((1 << YBits) - 1), YExponentBits, MantissaBits);
        float z = DecodeCoordinate((int)(encoded >> ZShift) & ((1 << BitsPerCoordinate) - 1), ZExponentBits, MantissaBits);

        return (isMoving, timestamp, x, y, z);
    }

    private static float DecodeCoordinate(int encoded, int exponentBits, int mantissaBits)
    {
        int bias = (1 << (exponentBits - 1)) - 1;
        int exponent = ((encoded >> mantissaBits) & ((1 << exponentBits) - 1)) - bias;
        float mantissa = (encoded & ((1 << mantissaBits) - 1)) / (float)(1 << mantissaBits);

        return (1 + mantissa) * Mathf.Pow(2, exponent);
    }
}
}
