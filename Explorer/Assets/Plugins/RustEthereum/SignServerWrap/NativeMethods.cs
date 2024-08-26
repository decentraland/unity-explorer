using System;
using System.Runtime.InteropServices;

namespace Plugins.RustEthereum.SignServerWrap
{
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "sign-server";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sign_server_initialize")]
        internal extern static unsafe bool SignServerInitialize(byte* privateKey, UIntPtr len);

        /// <param name="message">A message to sign</param>
        /// <param name="signatureOutput">Signatures size is always 65 bytes</param>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sign_server_sign_message")]
        internal extern static unsafe void SignServerSignMessage(string message, byte* signatureOutput);
    }
}
