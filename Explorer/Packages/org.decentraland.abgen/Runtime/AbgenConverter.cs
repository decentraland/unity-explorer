using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;

namespace Decentraland.Abgen
{
    /// <summary>One produced artifact, named <c>&lt;hash&gt;_&lt;deps&gt;_&lt;platform&gt;</c>.</summary>
    public readonly struct AbgenArtifact
    {
        public string Name { get; }

        public byte[] Data { get; }

        public AbgenArtifact(string name, byte[] data)
        {
            Name = name;
            Data = data;
        }
    }

    /// <summary>Everything one conversion produced, in emission order.</summary>
    public sealed class AbgenResult
    {
        public AbgenStatus Status { get; internal set; } = AbgenStatus.Ok;

        public List<AbgenArtifact> Artifacts { get; } = new();

        /// <summary>Raw JSON progress events.</summary>
        public List<string> Events { get; } = new();

        /// <summary>Fatal errors. Empty on a clean run.</summary>
        public List<string> Errors { get; } = new();

        public string? Manifest { get; internal set; }

        /// <summary>
        /// True when the *run* succeeded, which it can be while individual
        /// models failed — those are <c>file-error</c> entries in
        /// <see cref="Events"/> and a non-zero <c>exitCode</c> in
        /// <see cref="Manifest"/>.
        /// </summary>
        public bool Succeeded => Status == AbgenStatus.Ok && Errors.Count == 0;
    }

    /// <summary>
    /// Converts Decentraland content into Unity AssetBundles in-process — no
    /// Editor spawn, no sidecar, no HTTP.
    /// </summary>
    /// <remarks>
    /// <see cref="Convert"/> is <b>blocking and CPU-heavy</b>: call it off the
    /// main thread and bound the native pool with <see cref="SetMaxThreads"/>
    /// first. A hostile asset is reported rather than fatal; native panics
    /// surface as <see cref="AbgenStatus.Panic"/>.
    /// </remarks>
    public static class AbgenConverter
    {
        /// <summary>Held for the domain's lifetime: native keeps this pointer.</summary>
        private static readonly NativeMethods.EmitCallback EmitThunk = OnEmit;

        public static string Version
        {
            get
            {
                IntPtr p = NativeMethods.abgen_version();
                return p == IntPtr.Zero ? "unknown" : Marshal.PtrToStringAnsi(p) ?? "unknown";
            }
        }

        /// <summary>A mismatch means plugin and package came from different builds.</summary>
        public static bool IsAbiCompatible()
        {
            try
            {
                return NativeMethods.abgen_abi_version() == NativeMethods.ExpectedAbiVersion;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Caps the native worker pool. Process-wide, effective once, before
        /// the first <see cref="Convert"/>;
        /// <see cref="AbgenStatus.AlreadyConfigured"/> is informational.
        /// </summary>
        public static AbgenStatus SetMaxThreads(uint threads) =>
            (AbgenStatus)NativeMethods.abgen_set_max_threads(threads);

        public static AbgenResult Convert(AbgenRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            byte[] blob = request.ToBytes();
            var result = new AbgenResult();

            GCHandle handle = GCHandle.Alloc(result);
            GCHandle pinned = GCHandle.Alloc(blob, GCHandleType.Pinned);
            try
            {
                int rc = NativeMethods.abgen_convert(
                    pinned.AddrOfPinnedObject(),
                    (UIntPtr)blob.Length,
                    EmitThunk,
                    GCHandle.ToIntPtr(handle));

                result.Status = (AbgenStatus)rc;
                return result;
            }
            finally
            {
                pinned.Free();
                handle.Free();
                GC.KeepAlive(EmitThunk);
            }
        }

        /// <summary>
        /// Static and attributed so IL2CPP emits a reverse P/Invoke wrapper;
        /// an instance delegate or closure fails to marshal on AOT.
        /// </summary>
        [MonoPInvokeCallback(typeof(NativeMethods.EmitCallback))]
        private static void OnEmit(IntPtr userData, uint kind, IntPtr ptr, UIntPtr len)
        {
            try
            {
                if (userData == IntPtr.Zero) return;
                if (GCHandle.FromIntPtr(userData).Target is not AbgenResult result) return;

                int length = checked((int)len);
                byte[] payload = length == 0 ? Array.Empty<byte>() : new byte[length];
                if (length > 0) Marshal.Copy(ptr, payload, 0, length);

                switch ((AbgenKind)kind)
                {
                    case AbgenKind.Json:
                        result.Events.Add(Encoding.UTF8.GetString(payload));
                        break;

                    case AbgenKind.Output:
                        if (TrySplitOutput(payload, out string name, out byte[] data))
                            result.Artifacts.Add(new AbgenArtifact(name, data));
                        break;

                    case AbgenKind.Error:
                        result.Errors.Add(Encoding.UTF8.GetString(payload));
                        break;

                    case AbgenKind.Manifest:
                        result.Manifest = Encoding.UTF8.GetString(payload);
                        break;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"abgen: emit callback failed: {e}");
            }
        }

        /// <summary><c>uint32 name_len | name | uint32 data_len | data</c>.</summary>
        internal static bool TrySplitOutput(byte[] blob, out string name, out byte[] data)
        {
            name = string.Empty;
            data = Array.Empty<byte>();

            if (blob.Length < 4) return false;
            uint nameLen = BitConverter.ToUInt32(blob, 0);
            if (nameLen > int.MaxValue || blob.Length < 4L + nameLen + 4) return false;

            int dataLenOffset = 4 + (int)nameLen;
            uint dataLen = BitConverter.ToUInt32(blob, dataLenOffset);
            long end = (long)dataLenOffset + 4 + dataLen;
            if (dataLen > int.MaxValue || blob.Length < end) return false;

            name = Encoding.UTF8.GetString(blob, 4, (int)nameLen);
            data = new byte[dataLen];
            Buffer.BlockCopy(blob, dataLenOffset + 4, data, 0, (int)dataLen);
            return true;
        }
    }
}
