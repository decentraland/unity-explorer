using DCL.AvatarRendering.Wearables.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using Runtime.Wearables;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    /// The intention of loading the scene associated with a smart wearable.
    /// </summary>
    public struct GetSmartWearableSceneIntention : ILoadingIntention, IEquatable<GetSmartWearableSceneIntention>
    {
        public IWearable SmartWearable;

        public IPartitionComponent Partition;

        #region ILoadingIntention

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }
#endregion

        public static GetSmartWearableSceneIntention Create(IWearable smartWearable, IPartitionComponent partition) =>
            new()
            {
                SmartWearable = smartWearable,
                Partition = partition,
                CommonArguments = new CommonLoadingArguments(SmartWearableCache.GetCacheId(smartWearable))
            };

        public bool Equals(GetSmartWearableSceneIntention other)
        {
            string id = SmartWearableCache.GetCacheId(SmartWearable);
            string otherId = SmartWearableCache.GetCacheId(other.SmartWearable);
            return string.Equals(id, otherId, StringComparison.Ordinal);
        }

        public struct Result
        {
            public SceneDefinitionComponent SceneDefinition;

            public ISceneFacade SceneFacade;
        }
    }
}
