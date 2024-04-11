using Microsoft.ClearScript.JavaScript;

namespace CrdtEcsBridge.PoolsProviders
{
    public static class InstancePoolsProviderExtensions
    {
        public static void RenewCrdtRawDataPoolFromScriptArray(this IInstancePoolsProvider instancePoolsProvider,
            ITypedArray<byte> scriptArray,
            ref PoolableByteArray lastInput)
        {
            var intLength = (int)scriptArray.Length;

            if (lastInput.Length < intLength)
            {
                // Release the old one
                lastInput.Dispose();

                // Rent a new one
                lastInput = instancePoolsProvider.GetCrdtRawDataPool(intLength);
            }

            // V8ScriptItem does not support zero length
            if (scriptArray.Length > 0)

                // otherwise use the existing one
                scriptArray.Read(0, scriptArray.Length, lastInput.Array, 0);
        }

        public static void ReleaseAndDispose(this ref PoolableByteArray lastInput)
        {
            lastInput.Dispose();
            lastInput = PoolableByteArray.EMPTY;
        }
    }
}
