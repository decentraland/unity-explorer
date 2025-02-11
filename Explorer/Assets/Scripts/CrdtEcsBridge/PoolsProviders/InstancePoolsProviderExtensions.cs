using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8.SplitProxy;

namespace CrdtEcsBridge.PoolsProviders
{
    public static class InstancePoolsProviderExtensions
    {
        public static void RenewCrdtRawDataPoolFromScriptArray(
            this IInstancePoolsProvider instancePoolsProvider, ITypedArray<byte> scriptArray,
            ref PoolableByteArray lastInput)
        {
            EnsureArrayLength(instancePoolsProvider, (int)scriptArray.Length, ref lastInput);

            // V8ScriptItem does not support zero length
            if (scriptArray.Length > 0)
                scriptArray.Read(0, scriptArray.Length, lastInput.Array, 0);
        }

        public static void RenewCrdtRawDataPoolFromScriptArray(
            this IInstancePoolsProvider instancePoolsProvider, Uint8Array scriptArray,
            ref PoolableByteArray lastInput)
        {
            EnsureArrayLength(instancePoolsProvider, scriptArray.Length, ref lastInput);
            scriptArray.CopyTo(lastInput.Array);
        }

        private static void EnsureArrayLength(IInstancePoolsProvider instancePoolsProvider,
            int scriptArrayLength, ref PoolableByteArray lastInput)
        {
            // if the rented array can't keep the desired data, replace it
            if (lastInput.Array.Length < scriptArrayLength)
            {
                // Release the old one
                lastInput.Dispose();

                // Rent a new one
                lastInput = instancePoolsProvider.GetAPIRawDataPool(scriptArrayLength);
            }
            // Otherwise set the desired length to the existing array so it provides a correct span
            else
                lastInput.SetLength(scriptArrayLength);
        }

        /// <summary>
        /// Releases the old array if it can't fit the new size anymore, and rents a new one.
        /// Copies data from the old array to the beginning of the new one
        /// </summary>
        public static PoolableByteArray Expand(this IInstancePoolsProvider instancePoolsProvider, PoolableByteArray lastInput, int newSize)
        {
            if (lastInput.Array.Length >= newSize)
            {
                lastInput.SetLength(newSize);
                return lastInput;
            }

            var newArray = instancePoolsProvider.GetAPIRawDataPool(newSize);

            lastInput.Array.CopyTo(newArray.Array, 0);
            lastInput.Dispose();

            return newArray;
        }

        public static void ReleaseAndDispose(this ref PoolableByteArray lastInput)
        {
            lastInput.Dispose();
            lastInput = PoolableByteArray.EMPTY;
        }
    }
}
