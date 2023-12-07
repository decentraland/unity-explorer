using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
using System.Collections.Generic;
using Unity.Mathematics;

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
        ///     Only one bulk request at a time
        /// </summary>
        public AssetPromise<SceneDefinitions, GetSceneDefinitionList>? ActivePromise;

        public VolatileScenePointers(List<IpfsTypes.SceneEntityDefinition> retrievedReusableList, List<int2> inputReusableList)
        {
            RetrievedReusableList = retrievedReusableList;
            InputReusableList = inputReusableList;
            ActivePromise = null;
        }

        public static VolatileScenePointers Create() =>
            new (new List<IpfsTypes.SceneEntityDefinition>(PoolConstants.SCENES_COUNT),
                new List<int2>(PoolConstants.SCENES_COUNT));
    }
}
