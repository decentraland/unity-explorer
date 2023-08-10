using System;
using System.Threading;

namespace Utility
{
    public static class CancellationTokenExtensions
    {
        public static void SafeCancelAndDispose(this CancellationTokenSource cancellationToken)
        {
            try
            {
                cancellationToken?.Cancel();
                cancellationToken?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // ignore
            }
        }
    }
}
