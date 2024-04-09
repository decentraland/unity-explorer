using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;

namespace CrdtEcsBridge.PoolsProviders
{
    public static class InstancePoolsProviderExtensions
    {
        public static int RenewCrdtRawDataPoolFromScriptArray(this IInstancePoolsProvider instancePoolsProvider,
            ITypedArray<byte> scriptArray,
            [CanBeNull] ref byte[] lastInput)
        {
            var intLength = (int)scriptArray.Length;

            if (lastInput == null || lastInput.Length < intLength)
            {
                // Release the old one
                if (lastInput != null)
                    instancePoolsProvider.ReleaseCrdtRawDataPool(lastInput);

                // Rent a new one
                lastInput = instancePoolsProvider.GetCrdtRawDataPool(intLength);
            }

            // V8ScriptItem does not support zero length
            if (scriptArray.Length > 0)

                // otherwise use the existing one
                scriptArray.Read(0, scriptArray.Length, lastInput, 0);

            return intLength;
        }

        public static void ReleaseAndDispose(this IInstancePoolsProvider instancePoolsProvider, ref byte[] lastInput)
        {
            if (lastInput == null) return;

            instancePoolsProvider.ReleaseCrdtRawDataPool(lastInput);
            lastInput = null;
        }
    }
}
