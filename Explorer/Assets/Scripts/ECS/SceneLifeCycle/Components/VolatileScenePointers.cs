using DCL.Ipfs;
using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
using System.Collections.Generic;
using ECS.Prioritization.Components;
using Unity.Mathematics;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Scene pointers served in case no fixed list of scenes is provided
    /// </summary>
    public struct VolatileScenePointers
    {
        public readonly List<SceneEntityDefinition> RetrievedReusableList;
        public readonly List<int2> InputReusableList;
        public readonly PartitionComponent ActivePartitionComponent;

        /// <summary>
        ///     Only one bulk request at a time
        /// </summary>
        public AssetPromise<SceneDefinitions, GetSceneDefinitionList>? ActivePromise;

        public VolatileScenePointers(List<SceneEntityDefinition> retrievedReusableList,
            List<int2> inputReusableList, PartitionComponent partitionComponent)
        {
            RetrievedReusableList = retrievedReusableList;
            InputReusableList = inputReusableList;
            ActivePromise = null;
            ActivePartitionComponent = partitionComponent;

            partitionComponent.Bucket = 0;

            // Lets lower the prio against asset bundles on the same bucket
            partitionComponent.IsBehind = true;
        }

        public static VolatileScenePointers Create(PartitionComponent partitionComponent) =>
            new (new List<SceneEntityDefinition>(PoolConstants.SCENES_COUNT),
                new List<int2>(PoolConstants.SCENES_COUNT), partitionComponent);
    }
}
