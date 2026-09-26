using Cysharp.Threading.Tasks;
using System;

namespace DCL.ScenesDebug.ScenesConsistency.Conditions
{
    public class ByTimeNextSceneDelay : INextSceneDelay
    {
        private readonly TimeSpan delay;

        public ByTimeNextSceneDelay(TimeSpan delay)
        {
            this.delay = delay;
        }

        public UniTask WaitAsync() =>
            UniTask.Delay(delay);
    }
}
