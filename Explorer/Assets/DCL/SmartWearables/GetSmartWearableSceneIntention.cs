using DCL.AvatarRendering.Wearables.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
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

        public static GetSmartWearableSceneIntention Create(IWearable smartWearable, IPartitionComponent partition)
        {
            string id = smartWearable.DTO.Metadata.id;

            return new GetSmartWearableSceneIntention
            {
                SmartWearable = smartWearable,
                Partition = partition,
                CommonArguments = new CommonLoadingArguments(id)
            };
        }

        public bool Equals(GetSmartWearableSceneIntention other)
        {
            string id = SmartWearable.DTO.Metadata.id;
            string otherId = other.SmartWearable.DTO.Metadata.id;
            return string.Equals(id, otherId, StringComparison.Ordinal);
        }

        public struct Result
        {
            public SceneDefinitionComponent SceneDefinition;

            public ISceneFacade SceneFacade;
        }
    }
}
