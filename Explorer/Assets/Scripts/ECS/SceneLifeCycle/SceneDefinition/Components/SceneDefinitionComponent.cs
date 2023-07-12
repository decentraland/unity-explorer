using Ipfs;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Scene definition in ECS, indicates the definition is loaded but does tell the state of SceneFacade itself
    /// </summary>
    public struct SceneDefinitionComponent
    {
        public readonly IpfsTypes.SceneEntityDefinition Definition;

        // This allocation is left on purpose as realm switching will lead to GC so we can keep things simple
        public readonly IReadOnlyList<Vector2Int> Parcels;
        public readonly IpfsTypes.IpfsPath IpfsPath;
        public readonly bool IsEmpty;

        public SceneDefinitionComponent(IpfsTypes.SceneEntityDefinition definition, IReadOnlyList<Vector2Int> parcels, IpfsTypes.IpfsPath ipfsPath)
        {
            Definition = definition;
            Parcels = parcels;
            IpfsPath = ipfsPath;
            IsEmpty = false;
        }

        /// <summary>
        ///     Create empty scene pointer
        /// </summary>
        public SceneDefinitionComponent(Vector2Int parcel)
        {
            Parcels = new[] { parcel };
            IsEmpty = true;
            IpfsPath = default(IpfsTypes.IpfsPath);

            Definition = new IpfsTypes.SceneEntityDefinition
            {
                id = $"empty-parcel-{parcel.x}-{parcel.y}",
            };
        }
    }
}
