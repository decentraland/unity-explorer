using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Utility
{
    public static class ByteUtils
    {
        /// <summary>
        ///     Read an unmanaged type from the span and advances its pointer accordingly
        /// </summary>
        /// <param name="span"></param>
        /// <param name="bytesCounter">An auxiliary argument to increase the bytes counter in accordance with the number of bytes read</param>
        /// <typeparam name="T">Unmanaged type</typeparam>
        /// <returns>The value read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T Read<T>(this ref ReadOnlySpan<byte> span, ref int bytesCounter) where T: unmanaged
        {
            T result = MemoryMarshal.Read<T>(span);
            span = span.Slice(sizeof(T));
            bytesCounter += sizeof(T);
            return result;
        }

        /// <summary>
        ///     Read an unmanaged type from the span and advances its pointer accordingly
        /// </summary>
        /// <param name="span"></param>
        /// <typeparam name="T">Unmanaged type</typeparam>
        /// <returns>The value read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T Read<T>(this ref ReadOnlySpan<byte> span) where T: unmanaged
        {
            T result = MemoryMarshal.Read<T>(span);
            span = span.Slice(sizeof(T));
            return result;
        }

        /// <summary>
        ///     Read an unmanaged type from the memory and advances its pointer accordingly
        /// </summary>
        /// <param name="memory"></param>
        /// <typeparam name="T">Unmanaged type</typeparam>
        /// <returns>The value read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T Read<T>(this ref ReadOnlyMemory<byte> memory) where T: unmanaged
        {
            T result = MemoryMarshal.Read<T>(memory.Span);
            memory = memory.Slice(sizeof(T));
            return result;
        }

        /// <summary>
        ///     Read <see cref="Enum" /> if it is represented by different size in the span and in the memory
        ///     (E.g. in the memory it is <see cref="byte" />, and in the stream it is <see cref="int" />)
        /// </summary>
        /// <param name="memory">Memory stream</param>
        /// <typeparam name="TEnum">Enum representation in the memory</typeparam>
        /// <typeparam name="T">Enum representation in the stream</typeparam>
        /// <returns>Teh value read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe TEnum ReadEnumAs<TEnum, T>(this ref ReadOnlyMemory<byte> memory)
            where T: unmanaged
            where TEnum: unmanaged, Enum
        {
            // Read the required number of bytes from the memory
            T stream = MemoryMarshal.Read<T>(memory.Span);
            memory = memory.Slice(sizeof(T));

            // Then reinterpret them accordingly to the `TEnum` size
            return Unsafe.As<T, TEnum>(ref stream);
        }

        /// <summary>
        ///     Write an unmanaged type to the span and advances its pointer accordingly
        /// </summary>
        /// <param name="span">Span big enough to fit sizeof(T) bytes</param>
        /// <param name="value">The value to write</param>
        /// <typeparam name="T">Unmanaged Type</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Write<T>(this ref Span<byte> span, T value) where T: unmanaged
        {
            MemoryMarshal.Write(span, ref value);
            span = span.Slice(sizeof(T));
        }

        /// <summary>
        ///     Write <see cref="Enum" /> to the span if it should be represented by different size in the span and in the memory
        ///     (E.g. in the memory it is <see cref="byte" />, and in the stream it is <see cref="int" />)
        /// </summary>
        /// <param name="span">Span big enough to fit sizeof(T) bytes</param>
        /// <param name="value">Value to write</param>
        /// <typeparam name="TEnum">Enum representation in the memory</typeparam>
        /// <typeparam name="TTo">Enum representation in the stream</typeparam>
        /// <returns>Teh value read</returns>
        public static unsafe void WriteEnumAs<TEnum, TTo>(this ref Span<byte> span, TEnum value)
            where TTo: unmanaged
            where TEnum: unmanaged, Enum
        {
            TTo result = Unsafe.As<TEnum, TTo>(ref value);
            int sizeDiff = sizeof(TTo) - sizeof(TEnum);

            if (sizeDiff > 0)
            {
                // zero remaining bytes
                // Get a pointer to the start of the value's memory
                var pResult = (byte*)Unsafe.AsPointer(ref result);

                // Zero out the additional bytes
                Unsafe.InitBlockUnaligned(pResult + sizeof(TEnum), 0, (uint)sizeDiff);
            }

            MemoryMarshal.Write(span, ref result);
            span = span[sizeof(TTo)..];
        }

        /// <summary>
        ///     Write source byte span into destination byte span
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public static void Write(this ref Span<byte> destination, in ReadOnlySpan<byte> source)
        {
            source.CopyTo(destination);
            destination = destination[source.Length..];
        }
    }
}
