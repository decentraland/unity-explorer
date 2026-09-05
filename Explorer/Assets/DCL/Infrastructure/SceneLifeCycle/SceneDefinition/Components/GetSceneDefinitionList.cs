using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a scene list originated from pointers
    /// </summary>
    public struct GetSceneDefinitionList : ILoadingIntention, IEquatable<GetSceneDefinitionList>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly IReadOnlyList<int2> Pointers;

        /// <summary>
        ///     Reusable collection the results are placed in
        /// </summary>
        public readonly List<SceneEntityDefinition> TargetCollection;

        public GetSceneDefinitionList(List<SceneEntityDefinition> targetCollection,
            IReadOnlyList<int2> pointers,
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
