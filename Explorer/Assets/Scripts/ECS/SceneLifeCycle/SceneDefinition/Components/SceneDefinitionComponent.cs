using CommunicationData.URLHelpers;
using DCL.Ipfs;
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
        public static readonly SceneMetadataScene EMPTY_METADATA = new ();

        public readonly SceneEntityDefinition Definition;

        public readonly IReadOnlyList<Vector2Int> Parcels;
        public readonly IReadOnlyList<ParcelMathHelper.ParcelCorners> ParcelsCorners;
        public readonly IpfsPath IpfsPath;
        public readonly bool IsEmpty;
        public readonly bool IsSDK7;
        public readonly ParcelMathHelper.SceneGeometry SceneGeometry;
        public int InternalJobIndex;

        public SceneDefinitionComponent(SceneEntityDefinition definition, IpfsPath ipfsPath)
        {
            Definition = definition;
            ParcelsCorners = new List<ParcelMathHelper.ParcelCorners>(definition.metadata.scene.DecodedParcels.Select(ParcelMathHelper.CalculateCorners));
            IpfsPath = ipfsPath;
            Parcels = definition.metadata.scene.DecodedParcels;
            IsEmpty = false;
            IsSDK7 = definition.metadata?.runtimeVersion == "7";
            SceneGeometry = ParcelMathHelper.CreateSceneGeometry(ParcelsCorners, Definition.metadata.scene.DecodedBase);
            InternalJobIndex = -1;
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
            IpfsPath = new IpfsPath(id, URLDomain.EMPTY);

            Definition = new SceneEntityDefinition(
                id,
                new SceneMetadata
                {
                    main = "bin/game.js",
                    scene = EMPTY_METADATA,
                }
                // content will be filled by the loading system
            );

            //No runtime version in metadata
            IsSDK7 = false;
            SceneGeometry = ParcelMathHelper.CreateSceneGeometry(ParcelsCorners, Definition.metadata.scene.DecodedBase);

            InternalJobIndex = -1;
        }
    }
}
