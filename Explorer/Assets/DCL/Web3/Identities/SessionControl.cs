using System;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Web3.Identities
{
    /// <summary>Tracks authenticated ownership independently of individual transport connections.</summary>
    public sealed class SessionControl
    {
        public enum Status { Active, Pending, Failed, Superseded, Banned, Unknown, Authenticating }

        private static readonly ConditionalWeakTable<IWeb3IdentityCache, SessionControl> SESSIONS = new ();
        private readonly object sync = new ();
        private readonly IWeb3IdentityCache identityCache;
        private string sessionKey;
        private int generation;
        private int revision;
        private Status status;
        private DateTime pendingDeadline;
        private DateTime transportRetryAt;
        private bool awaitingAssignment;

        public event Action? Changed;

        public static SessionControl For(IWeb3IdentityCache cache) =>
            SESSIONS.GetValue(cache, static key => new SessionControl(key));

        private SessionControl(IWeb3IdentityCache identityCache)
        {
            this.identityCache = identityCache;
            sessionKey = Key();
            identityCache.OnIdentityChanged += IdentityChanged;
        }

        public int Generation { get { lock (sync) return generation; } }
        public int Revision { get { lock (sync) return revision; } }
        public Status Current { get { lock (sync) return status; } }

        public bool CanRecover(int expectedGeneration)
        {
            CheckWatchdog(DateTime.UtcNow);
            lock (sync) return generation == expectedGeneration && status == Status.Active;
        }

        public bool CanCompleteOperation(int expectedGeneration, int expectedRevision)
        {
            lock (sync) return generation == expectedGeneration && revision == expectedRevision && status == Status.Active;
        }

        public bool CanListen(int expectedGeneration)
        {
            CheckWatchdog(DateTime.UtcNow);
            lock (sync) return generation == expectedGeneration && status is Status.Active or Status.Pending;
        }

        public bool CanRecoverTransport(int expectedGeneration)
        {
            CheckWatchdog(DateTime.UtcNow);
            lock (sync) return generation == expectedGeneration && (status == Status.Active || (status == Status.Pending && DateTime.UtcNow >= transportRetryAt));
        }

        public void Pending(int expectedGeneration, uint retryAfterMs, DateTime now)
        {
            lock (sync)
            {
                if (generation != expectedGeneration || status is not (Status.Active or Status.Pending)) return;
                revision++;
                // Refuse unreasonable waits without extending a live watchdog indefinitely.
                if (retryAfterMs > 120000) status = Status.Failed;
                else if (status != Status.Pending)
                {
                    pendingDeadline = now.AddMilliseconds(Math.Max(30000, retryAfterMs + 5000));
                    transportRetryAt = now.AddMilliseconds(retryAfterMs);
                    status = Status.Pending;
                }
                else transportRetryAt = now.AddMilliseconds(retryAfterMs);
            }
            Changed?.Invoke();
        }

        public void CheckWatchdog(DateTime now)
        {
            lock (sync)
            {
                if (!(status == Status.Pending || (status == Status.Active && awaitingAssignment)) || now < pendingDeadline) return;
                status = Status.Failed;
                revision++;
            }
            Changed?.Invoke();
        }

        public void AwaitAssignment(int expectedGeneration, DateTime now)
        {
            lock (sync)
            {
                if (generation != expectedGeneration || status != Status.Active || awaitingAssignment) return;
                awaitingAssignment = true;
                pendingDeadline = now.AddSeconds(30);
            }
        }

        public bool AcceptAssignment(int expectedGeneration)
        {
            lock (sync)
            {
                if (generation != expectedGeneration || status is not (Status.Active or Status.Pending)) return false;
                status = Status.Active;
                awaitingAssignment = false;
                revision++;
            }
            Changed?.Invoke();
            return true;
        }

        public void Stop(int expectedGeneration, Status reason)
        {
            lock (sync)
            {
                if (generation != expectedGeneration || status is Status.Superseded or Status.Banned) return;
                status = reason;
                revision++;
            }
            Changed?.Invoke();
        }

        public bool BeginReauthentication()
        {
            lock (sync)
            {
                if (status is not (Status.Failed or Status.Superseded or Status.Unknown)) return false;
                status = Status.Authenticating;
                awaitingAssignment = false;
                generation++;
                revision++;
            }
            Changed?.Invoke();
            return true;
        }

        public async UniTask ReauthenticateAsync(Func<CancellationToken, UniTask> authenticationFlow, CancellationToken token)
        {
            if (!BeginReauthentication()) return;
            int attempt = Generation;
            identityCache.Clear();
            try
            {
                await authenticationFlow(token);
                if (Generation == attempt || Current == Status.Authenticating)
                    throw new InvalidOperationException("Authentication did not create a fresh session key");
            }
            catch
            {
                Stop(attempt, Status.Failed);
                throw;
            }
        }

        private string Key() =>
            identityCache.Identity?.EphemeralAccount.Address.ToString() ?? string.Empty;

        private void IdentityChanged()
        {
            lock (sync)
            {
                string next = Key();
                if (string.IsNullOrEmpty(next) || string.Equals(next, sessionKey, StringComparison.OrdinalIgnoreCase)) return;
                if (status is not (Status.Active or Status.Authenticating)) return;
                sessionKey = next;
                generation++;
                status = Status.Active;
                awaitingAssignment = false;
                revision++;
            }
            Changed?.Invoke();
        }
    }
}
