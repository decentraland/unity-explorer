using Microsoft.ClearScript.JavaScript;

namespace CrdtEcsBridge.PoolsProviders
{
    public static class InstancePoolsProviderExtensions
    {
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
