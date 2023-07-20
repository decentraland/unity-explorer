using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Utility.Pool;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Scene pointers served in case no fixed list of scenes is provided
    /// </summary>
    public struct VolatileScenePointers
    {
        public readonly List<IpfsTypes.SceneEntityDefinition> RetrievedReusableList;
        public readonly List<int2> InputReusableList;

        /// <summary>
        ///     These parcels were already processed and won't be processed again
        /// </summary>
        public NativeHashSet<int2> ProcessedParcels;

        /// <summary>
        ///     Only one bulk request at a time
        /// </summary>
        public AssetPromise<SceneDefinitions, GetSceneDefinitionList>? ActivePromise;

        public VolatileScenePointers(List<IpfsTypes.SceneEntityDefinition> retrievedReusableList, NativeHashSet<int2> processedParcels, List<int2> inputReusableList)
        {
            RetrievedReusableList = retrievedReusableList;
            ProcessedParcels = processedParcels;
            InputReusableList = inputReusableList;
            ActivePromise = null;
        }

        public static VolatileScenePointers Create() =>
            new (new List<IpfsTypes.SceneEntityDefinition>(PoolConstants.SCENES_COUNT),
                new NativeHashSet<int2>(PoolConstants.SCENES_COUNT * 4, AllocatorManager.Persistent),
                new List<int2>(PoolConstants.SCENES_COUNT));
    }
}
