using System;

namespace Utility
{
    /// <summary>
    ///     Platform-agnostic abstraction over a JavaScript typed array (e.g. <c>Uint8Array</c>) that lives inside a scene's
    ///     JS engine. Implementations include the ClearScript/V8 desktop path and
    ///     <see cref="SceneRuntime.WebClient.WebClientTypedArrayAdapter" /> for WebGL.
    ///     <para>
    ///         Data can be read/written through managed copies (<see cref="Read" />, <see cref="ReadBytes" />,
    ///         <see cref="WriteBytes" />) or, on desktop only, through a raw pointer via
    ///         <see cref="InvokeWithDirectAccess(System.Action{System.IntPtr})" />.
    ///         Direct pointer access is not supported on WebGL and will throw <see cref="System.NotSupportedException" />.
    ///     </para>
    /// </summary>
    public interface IDCLTypedArray<in T> where T : unmanaged
    {
        ulong Length { get; }
        ulong Size { get; }
        IDCLArrayBuffer ArrayBuffer { get; }

        /// <summary>
        /// Copies elements from the typed array into the specified array.
        /// </summary>
        /// <param name="index">The index within the typed array of the first element to copy.</param>
        /// <param name="length">The maximum number of elements to copy.</param>
        /// <param name="destination">The array into which to copy the elements.</param>
        /// <param name="destinationIndex">The index within <paramref name="destination"/> at which to store the first copied element.</param>
        /// <returns>The number of elements copied.</returns>
        ulong Read(ulong index, ulong length, T[] destination, ulong destinationIndex);

        /// <summary>
        /// Invokes the specified delegate with direct access to the typed array's underlying memory.
        /// </summary>
        /// <param name="action">The delegate to invoke with a pointer to the typed array's memory.</param>
        void InvokeWithDirectAccess(Action<IntPtr> action);

        /// <summary>
        /// Invokes the specified delegate with direct access to the typed array's underlying memory.
        /// </summary>
        /// <param name="func">The delegate to invoke with a pointer to the typed array's memory.</param>
        /// <returns>The return value of the delegate.</returns>
        int InvokeWithDirectAccess(Func<IntPtr, int> func);

        void ReadBytes(ulong @ulong, ulong eventBytesLength, byte[] eventBytes, ulong ulong1);

        void WriteBytes(ReadOnlySpan<byte> source, ulong sourceIndex, ulong count, ulong offset);
    }
}
