using CommunicationData.URLHelpers;
using DCL.Ipfs;
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

        public readonly Vector2Int [] Parcels;
        public readonly ParcelMathHelper.ParcelCorners [] ParcelsCorners;
        public readonly IpfsPath IpfsPath;
        public readonly bool IsEmpty;
        public readonly bool IsSDK7;
        public readonly ParcelMathHelper.SceneGeometry SceneGeometry;
        public int InternalJobIndex;

        public SceneDefinitionComponent(SceneEntityDefinition definition, IpfsPath ipfsPath)
        {
            var decodedParcels = definition.metadata.scene.DecodedParcels;
            Definition = definition;
            Parcels = new Vector2Int[decodedParcels.Count];
            ParcelsCorners = new ParcelMathHelper.ParcelCorners[Parcels.Length];
            for (int i = 0; i < Parcels.Length; i++)
            {
                Parcels[i] = decodedParcels[i];
                ParcelsCorners[i] = ParcelMathHelper.CalculateCorners(Parcels[i]);
            }
            IpfsPath = ipfsPath;

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
