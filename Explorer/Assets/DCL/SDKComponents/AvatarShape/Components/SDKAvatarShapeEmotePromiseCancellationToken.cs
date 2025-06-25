using System;
using System.Threading;

namespace ECS.Unity.AvatarShape.Components
{
    public class SDKAvatarShapeEmotePromiseCancellationToken : IDisposable
    {
        public readonly CancellationTokenSource Cts;

        public SDKAvatarShapeEmotePromiseCancellationToken()
        {
            Cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            Cts.Cancel();
            Cts.Dispose();
        }
    }
}
