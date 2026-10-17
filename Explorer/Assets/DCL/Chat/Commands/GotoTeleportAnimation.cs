using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Coordinates navigation with the ECS departure and arrival presentation.
    /// </summary>
    public sealed class GotoTeleportAnimation
    {
        public bool IsRequested { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public UniTaskCompletionSource? Departure { get; private set; }
        public UniTaskCompletionSource? Arrival { get; private set; }

        public async UniTask<bool> BeginAsync(CancellationToken ct)
        {
            if (IsRequested || ct.IsCancellationRequested)
                return false;

            IsRequested = true;
            CancellationToken = ct;
            Departure = new UniTaskCompletionSource();
            Arrival = null;

            try
            {
                await Departure.Task.AttachExternalCancellation(ct);
                return true;
            }
            catch
            {
                Finish();
                throw;
            }
        }

        public async UniTask ArriveAsync(CancellationToken ct)
        {
            if (!IsRequested) return;
            ct.ThrowIfCancellationRequested();
            Arrival = new UniTaskCompletionSource();
            await Arrival.Task.AttachExternalCancellation(ct);
        }

        public void Finish()
        {
            IsRequested = false;
            Departure?.TrySetCanceled();
            Arrival?.TrySetCanceled();
        }
    }
}
