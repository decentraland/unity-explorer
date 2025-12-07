using System;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Custom upload handler that builds JSON directly into a byte buffer
    ///     without intermediate string allocations.
    /// </summary>
    public struct BufferedStringUploadHandler
    {
        // Use a native array instead of pooling a managed one to reduce the memory consumption
        // Native Arrays are not GCed so they don't produce the corresponding pressure
        // Additionally, it's not needed to estimate and maintain the pool to prevent bloating up
        private NativeList<byte> buffer;

        private static readonly byte[] TRUE_BYTES = Encoding.UTF8.GetBytes("true");
        private static readonly byte[] FALSE_BYTES = Encoding.UTF8.GetBytes("false");
        private static readonly byte[] NULL_BYTES = Encoding.UTF8.GetBytes("null");

        public BufferedStringUploadHandler(int initialCapacity = 1024)
        {
            buffer = new NativeList<byte>(initialCapacity, Allocator.Persistent);
        }

        /// <summary>
        ///     Sets the buffer as the upload data.
        ///     Call this after building your JSON. <br/>
        ///     UploadHandler will dispose the created underlying buffer
        /// </summary>
        public UploadHandlerRaw CreateUploadHandler() =>
            new (buffer.AsArray(), true);

        /// <summary>
        ///     Writes a single byte to the buffer.
        /// </summary>
        public void WriteByte(byte value) =>
            buffer.Add(value);

        public void WriteChar(char value) =>
            buffer.Add((byte)value);

        /// <summary>
        ///     Writes a byte array to the buffer.
        /// </summary>
        public unsafe void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* bp = bytes) { buffer.AddRange(bp, bytes.Length); }
        }

        /// <summary>
        ///     Writes a string as UTF-8 bytes to the buffer.
        /// </summary>
        public unsafe void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            int byteCount = Encoding.UTF8.GetByteCount(value);
            int startIndex = buffer.Length;

            // it will not shrink the capacity
            buffer.Resize(buffer.Length + byteCount, NativeArrayOptions.UninitializedMemory);

            var bufPtr = new Span<byte>(buffer.GetUnsafePtr() + startIndex, byteCount);

            Encoding.UTF8.GetBytes(value.AsSpan(), bufPtr);
        }

        /// <summary>
        ///     Writes a JSON-escaped string (with quotes) to the buffer.
        /// </summary>
        public void WriteJsonString(string value)
        {
            WriteChar('"');

            if (!string.IsNullOrEmpty(value))
            {
                // For proper JSON, we should escape special characters
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '"':
                            WriteChar('\\');
                            WriteChar('"');
                            break;
                        case '\\':
                            WriteChar('\\');
                            WriteChar('\\');
                            break;
                        case '\b':
                            WriteChar('\\');
                            WriteChar('b');
                            break;
                        case '\f':
                            WriteChar('\\');
                            WriteChar('f');
                            break;
                        case '\n':
                            WriteChar('\\');
                            WriteChar('n');
                            break;
                        case '\r':
                            WriteChar('\\');
                            WriteChar('r');
                            break;
                        case '\t':
                            WriteChar('\\');
                            WriteChar('t');
                            break;
                        default:

                            if (c < 32)
                            {
                                // Unicode escape for control characters
                                WriteChar('\\');
                                WriteChar('u');
                                int code = c;
                                WriteChar(ToHex((code >> 12) & 0xF));
                                WriteChar(ToHex((code >> 8) & 0xF));
                                WriteChar(ToHex((code >> 4) & 0xF));
                                WriteChar(ToHex(code & 0xF));
                            }
                            else
                            {
                                // Regular character - write as UTF-8 without allocation
                                WriteCharUtf8(c);
                            }

                            break;
                    }
                }
            }

            WriteChar('"');
        }

        /// <summary>
        ///     Writes a single character as UTF-8 bytes without allocations.
        /// </summary>
        private unsafe void WriteCharUtf8(char c)
        {
            Span<byte> tempBuffer = stackalloc byte[4];
            int bytesWritten = Encoding.UTF8.GetBytes(stackalloc char[] { c }, tempBuffer);

            fixed (byte* bp = tempBuffer)
            {
                buffer.AddRange(bp, bytesWritten);
            }
        }

        /// <summary>
        ///     Converts a value (0-15) to its hexadecimal character.
        /// </summary>
        private char ToHex(int value) =>
            (char)(value < 10 ? '0' + value : 'a' + (value - 10));

        /// <summary>
        ///     Writes an integer as ASCII digits to the buffer.
        /// </summary>
        public void WriteInt(int value)
        {
            // Handle special case where -value would overflow
            if (value == int.MinValue)
            {
                WriteString("-2147483648");
                return;
            }

            if (value == 0)
            {
                WriteChar('0');
                return;
            }

            if (value < 0)
            {
                WriteChar('-');
                value = -value;
            }

            // Calculate number of digits
            int temp = value;
            int digitCount = 0;

            while (temp > 0)
            {
                temp /= 10;
                digitCount++;
            }

            buffer.ResizeUninitialized(buffer.Length + digitCount);

            // Write digits in reverse order
            int startPos = buffer.Length - 1;

            while (value > 0)
            {
                buffer[startPos--] = (byte)('0' + (value % 10));
                value /= 10;
            }
        }

        /// <summary>
        ///     Writes a long as ASCII digits to the buffer.
        /// </summary>
        public void WriteLong(long value)
        {
            // Handle special case where -value would overflow
            if (value == long.MinValue)
            {
                WriteString("-9223372036854775808");
                return;
            }

            if (value == 0)
            {
                WriteChar('0');
                return;
            }

            if (value < 0)
            {
                WriteChar('-');
                value = -value;
            }

            long temp = value;
            int digitCount = 0;

            while (temp > 0)
            {
                temp /= 10;
                digitCount++;
            }

            buffer.ResizeUninitialized(buffer.Length + digitCount);

            int startPos = buffer.Length - 1;

            while (value > 0)
            {
                buffer[startPos--] = (byte)('0' + (value % 10));
                value /= 10;
            }
        }

        /// <summary>
        ///     Writes a boolean as "true" or "false" to the buffer.
        /// </summary>
        public void WriteBool(bool value) =>
            WriteBytes(value ? TRUE_BYTES : FALSE_BYTES);

        /// <summary>
        ///     Writes "null" to the buffer.
        /// </summary>
        public void WriteNull() =>
            WriteBytes(NULL_BYTES);

        /// <summary>
        ///     Resets the buffer position to start over.
        /// </summary>
        public void Clear() =>
            buffer.Clear();

        /// <summary>
        ///     Gets the current buffer size in bytes.
        /// </summary>
        public int Length => buffer.Length;

        public override unsafe string ToString() =>
            Encoding.UTF8.GetString(buffer.GetUnsafePtr(), Length);
    }
}
