using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a scene list originated from pointers
    /// </summary>
    public struct GetSceneDefinitionList : ILoadingIntention, IEquatable<GetSceneDefinitionList>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly IReadOnlyList<Vector2Int> Pointers;

        /// <summary>
        ///     Reusable collection the results are placed in
        /// </summary>
        public readonly List<IpfsTypes.SceneEntityDefinition> TargetCollection;

        public GetSceneDefinitionList(List<IpfsTypes.SceneEntityDefinition> targetCollection,
            IReadOnlyList<Vector2Int> pointers,
            CommonLoadingArguments commonArguments)
        {
            TargetCollection = targetCollection;
            CommonArguments = commonArguments;
            Pointers = pointers;
        }

        public bool Equals(GetSceneDefinitionList other) =>

            // fake implementation, not needed, never equal or cached
            false;
    }
}
