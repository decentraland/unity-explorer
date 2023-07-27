using Ipfs;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Scene definition in ECS, indicates the definition is loaded but does tell the state of SceneFacade itself
    /// </summary>
    public struct SceneDefinitionComponent
    {
        internal static readonly IpfsTypes.SceneMetadataScene EMPTY_METADATA = new ()
        {
            allowedMediaHostnames = new List<string>(),
            requiredPermissions = new List<string>(),
        };

        public readonly IpfsTypes.SceneEntityDefinition Definition;

        // This allocation is left on purpose as realm switching will lead to GC so we can keep things simple
        public readonly IReadOnlyList<Vector2Int> Parcels;
        public readonly IReadOnlyList<ParcelMathHelper.ParcelCorners> ParcelsCorners;
        public readonly IpfsTypes.IpfsPath IpfsPath;
        public readonly bool IsEmpty;

        public SceneDefinitionComponent(IpfsTypes.SceneEntityDefinition definition, IReadOnlyList<Vector2Int> parcels, IpfsTypes.IpfsPath ipfsPath)
        {
            Definition = definition;
            Parcels = parcels;
            ParcelsCorners = new List<ParcelMathHelper.ParcelCorners>(parcels.Select(ParcelMathHelper.CalculateCorners));
            IpfsPath = ipfsPath;
            IsEmpty = false;
        }

        /// <summary>
        ///     Create empty scene pointer
        /// </summary>
        public SceneDefinitionComponent(Vector2Int parcel)
        {
            var id = $"empty-parcel-{parcel.x}-{parcel.y}";

            ParcelsCorners = new[] { ParcelMathHelper.CalculateCorners(parcel) };
            Parcels = new[] { parcel };
            IsEmpty = true;
            IpfsPath = new IpfsTypes.IpfsPath(id, string.Empty);

            Definition = new IpfsTypes.SceneEntityDefinition
            {
                id = id,
                metadata = new IpfsTypes.SceneMetadata
                {
                    main = "bin/game.js",
                    scene = EMPTY_METADATA,
                },

                // content will be filled by the loading system
            };
        }
    }
}
