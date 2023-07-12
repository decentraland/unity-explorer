using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Scene pointers served in case no fixed list of scenes is provided
    /// </summary>
    public struct VolatileScenePointers
    {
        public readonly List<IpfsTypes.SceneEntityDefinition> RetrievedReusableList;
        public readonly List<Vector2Int> InputReusableList;

        /// <summary>
        ///     These parcels were already processed and won't be processed again
        /// </summary>
        public readonly HashSet<Vector2Int> ProcessedParcels;

        /// <summary>
        ///     Only one bulk request at a time
        /// </summary>
        public AssetPromise<SceneDefinitions, GetSceneDefinitionList>? ActivePromise;

        public VolatileScenePointers(List<IpfsTypes.SceneEntityDefinition> retrievedReusableList, HashSet<Vector2Int> processedParcels, List<Vector2Int> inputReusableList)
        {
            RetrievedReusableList = retrievedReusableList;
            ProcessedParcels = processedParcels;
            InputReusableList = inputReusableList;
            ActivePromise = null;
        }
    }
}
