using DCL.AvatarRendering.Wearables.Components.Intentions;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    /// <summary>
    ///     Singleton-like structure to store the state of default wearables
    ///     to indicate whether they are loaded
    /// </summary>
    public struct DefaultWearablesComponent
    {
        public enum State
        {
            InProgress,
            Fail,
            Success,
        }

        public AssetPromise<WearablesResolution, GetWearablesByPointersIntention>[] PromisePerBodyShape;

        public State ResolvedState;

        public DefaultWearablesComponent(AssetPromise<WearablesResolution, GetWearablesByPointersIntention>[] promisePerBodyShape)
        {
            PromisePerBodyShape = promisePerBodyShape;
            ResolvedState = State.InProgress;
        }
    }
}
