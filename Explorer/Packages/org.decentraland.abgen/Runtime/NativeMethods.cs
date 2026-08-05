using System;
using System.Runtime.InteropServices;

namespace Decentraland.Abgen
{
    /// <summary>Must match <c>AbgenKind</c> in abgen.h — the values are ABI.</summary>
    public enum AbgenKind : uint
    {
        /// <summary>UTF-8 JSON progress event, discriminated by its "ev" field.</summary>
        Json = 0,

        /// <summary><c>uint32 name_len | name | uint32 data_len | data</c>.</summary>
        Output = 1,

        /// <summary>UTF-8 error message, fatal to the run.</summary>
        Error = 2,

        /// <summary>UTF-8 JSON job manifest. Once, last, in Convert mode.</summary>
        Manifest = 3,
    }

    /// <summary>Conversion modes, encoded into the request blob.</summary>
    public enum AbgenMode : byte
    {
        /// <summary>Every model, optionally the LOD, plus a manifest.</summary>
        Convert = 0,

        /// <summary>Report plan and dependency edges; convert nothing.</summary>
        Scan = 1,

        /// <summary>One named model, against a supplied content table.</summary>
        ConvertOnly = 2,

        LodOnly = 3,
    }

    /// <summary>Return codes. Must match the <c>ABGEN_*</c> defines in abgen.h.</summary>
    public enum AbgenStatus
    {
        Ok = 0,
        MalformedInput = 1,
        ConvertFailed = 2,
        NullArgument = 3,
        Panic = 4,
        AlreadyConfigured = 5,
    }

    /// <summary>
    /// Raw bindings to the abgen native library. The ABI has nothing
    /// engine-specific in it; only this layer is Unity's.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>Unity applies the platform prefix and suffix.</summary>
        private const string Lib = "abgen";

        /// <summary>The ABI this binding was written against.</summary>
        internal const uint ExpectedAbiVersion = 1;

        /// <summary>
        /// Receives one payload, on the calling thread only. <paramref name="ptr"/>
        /// borrows native memory valid for this call alone, and must not be
        /// read when <paramref name="len"/> is zero.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void EmitCallback(IntPtr userData, uint kind, IntPtr ptr, UIntPtr len);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint abgen_abi_version();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr abgen_version();

        /// <summary>
        /// Caps the worker pool, which otherwise takes every core and competes
        /// with the render thread. Process-wide, effective once.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int abgen_set_max_threads(uint threads);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr abgen_alloc(UIntPtr len);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void abgen_free(IntPtr ptr, UIntPtr len);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int abgen_convert(
            IntPtr request,
            UIntPtr requestLen,
            EmitCallback emit,
            IntPtr userData);
    }
}
