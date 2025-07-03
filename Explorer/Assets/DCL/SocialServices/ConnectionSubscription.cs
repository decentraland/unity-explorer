using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Utility;

namespace DCL.SocialService
{
    /// <summary>
    ///     Represents a subscription to connection management events.
    ///     Multiple subscriptions can share the same underlying connection.
    ///     Dispose this object when no longer needed to unsubscribe.
    /// </summary>
    public class ConnectionSubscription : IDisposable
    {
        private readonly IRPCSocialServices parent;
        private readonly CancellationTokenSource cts;
        private bool disposed = false;

        public event Action Connected;
        public event Action Disconnected;
        public event Action ConnectionFailed;

        internal ConnectionSubscription(IRPCSocialServices parent, CancellationToken ct)
        {
            this.parent = parent;
            this.cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }

        /// <summary>
        ///     Waits for the connection to be established.
        ///     Returns when either the connection is ready, timeout is reached, or the cancellation token is triggered.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for connection</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if connection was established, false if timeout or cancellation occurred</returns>
        public async UniTask<bool> WaitForConnectionAsync(TimeSpan timeout, CancellationToken ct)
        {
            var maxIterations = (int)(timeout.TotalMilliseconds / 100);
            var currentIteration = 0;
            
            while (!parent.IsConnected && !cts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                if (currentIteration >= maxIterations)
                {
                    return false; // Timeout reached
                }
                
                await UniTask.Delay(100, cancellationToken: UniTask.WhenAny(cts.Token, ct).Token);
                currentIteration++;
            }
            
            return parent.IsConnected;
        }

        private static readonly TimeSpan DEFAULT_CONNECTION_TIMEOUT = TimeSpan.FromSeconds(30);

        /// <summary>
        ///     Waits for the connection to be established with a default timeout of 30 seconds.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if connection was established, false if timeout or cancellation occurred</returns>
        public async UniTask<bool> WaitForConnectionAsync(CancellationToken ct)
        {
            return await WaitForConnectionAsync(DEFAULT_CONNECTION_TIMEOUT, ct);
        }

        internal void NotifyConnected()
        {
            if (!disposed)
                Connected?.Invoke();
        }

        internal void NotifyDisconnected()
        {
            if (!disposed)
                Disconnected?.Invoke();
        }

        internal void NotifyConnectionFailed()
        {
            if (!disposed)
                ConnectionFailed?.Invoke();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            
            if (parent is RPCSocialServices rpcServices)
            {
                rpcServices.Unsubscribe(this);
            }
            cts.SafeCancelAndDispose();
        }
    }
} 