using CommunicationData.URLHelpers;
using DCL.Ipfs;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Scene definition in ECS, indicates the definition is loaded but does not tell the state of SceneFacade itself
    /// </summary>
    public struct SceneDefinitionComponent
    {
        private static readonly SceneMetadataScene EMPTY_METADATA = new ();

        public SceneEntityDefinition Definition { get; }
        public IReadOnlyList<Vector2Int> Parcels { get; }
        public IReadOnlyList<ParcelMathHelper.ParcelCorners> ParcelsCorners { get; }
        public IpfsPath IpfsPath { get; }
        public bool IsEmpty { get; }
        public bool IsSDK7 { get; }
        public ParcelMathHelper.SceneGeometry SceneGeometry { get; }
        public bool IsPortableExperience { get; }

        public int InternalJobIndex { get; set; }

        public SceneDefinitionComponent(
            SceneEntityDefinition definition,
            IReadOnlyList<Vector2Int> parcels,
            IReadOnlyList<ParcelMathHelper.ParcelCorners> parcelsCorners,
            ParcelMathHelper.SceneGeometry sceneGeometry,
            IpfsPath ipfsPath, bool isEmpty, bool isSDK7, bool isPortableExperience)
        {
            Definition = definition;
            Parcels = parcels;
            ParcelsCorners = parcelsCorners;
            IpfsPath = ipfsPath;
            IsEmpty = isEmpty;
            IsSDK7 = isSDK7;
            SceneGeometry = sceneGeometry;
            InternalJobIndex = -1;
            IsPortableExperience = isPortableExperience;
        }
    }

    public static class SceneDefinitionComponentFactory
    {
        private static ParcelMathHelper.SceneGeometry cachedPortableExperiencesSceneGeometry;
        private static List<ParcelMathHelper.ParcelCorners> cachedParcelsCorners;

        public static SceneDefinitionComponent CreateFromDefinition(SceneEntityDefinition definition, IpfsPath ipfsPath)
        {
            if (definition.metadata.isPortableExperience) { return CreatePortableExperienceSceneDefinitionComponent(definition, ipfsPath); }

            return CreateSceneDefinitionComponent(definition, definition.metadata.scene.DecodedParcels, ipfsPath, isEmpty: false, isSDK7: definition.metadata.runtimeVersion == "7", isPortableExperience: false);
        }

        public static SceneDefinitionComponent CreatePortableExperienceSceneDefinitionComponent(SceneEntityDefinition definition, IpfsPath ipfsPath)
        {
            if (cachedParcelsCorners == null)
            {
                var portableParcels = new List<Vector2Int>();

                for (int i = -150; i < 150; i++)
                {
                    for (int j = -150; j < 150; j++) { portableParcels.Add(new Vector2Int(i, j)); }
                }

                cachedParcelsCorners = new List<ParcelMathHelper.ParcelCorners>(portableParcels.Select(ParcelMathHelper.CalculateCorners));
                cachedPortableExperiencesSceneGeometry = ParcelMathHelper.CreateSceneGeometry(cachedParcelsCorners, definition.metadata.scene.DecodedBase);
            }

            return new SceneDefinitionComponent(
                definition,
                parcels: definition.metadata.scene.DecodedParcels,
                cachedParcelsCorners,
                cachedPortableExperiencesSceneGeometry,
                ipfsPath,
                isEmpty: false,
                isSDK7: definition.metadata.runtimeVersion == "7",
                isPortableExperience: true
            );
        }

        /// <summary>
        ///     Create empty scene pointer
        /// </summary>
        public static SceneDefinitionComponent CreateEmpty(Vector2Int parcel)
        {
            var id = $"empty-parcel-{parcel.x}-{parcel.y}";

            var definition = new SceneEntityDefinition(
                id,
                new SceneMetadata
                {
                    main = "bin/game.js",
                    scene = new SceneMetadataScene(),

                    // content will be filled by the loading system
                }
            );

            return CreateSceneDefinitionComponent(
                definition,
                new[] { parcel },
                new IpfsPath(id, URLDomain.EMPTY),
                isEmpty: true,
                isSDK7: false,
                isPortableExperience: false
            );
        }

        private static SceneDefinitionComponent CreateSceneDefinitionComponent(
            SceneEntityDefinition definition,
            IReadOnlyList<Vector2Int> parcels,
            IpfsPath ipfsPath,
            bool isEmpty,
            bool isSDK7,
            bool isPortableExperience)
        {
            var parcelCorners = parcels.Select(ParcelMathHelper.CalculateCorners).ToList();
            ParcelMathHelper.SceneGeometry sceneGeometry = ParcelMathHelper.CreateSceneGeometry(parcelCorners, definition.metadata.scene.DecodedBase);

            return new SceneDefinitionComponent(
                definition,
                parcels,
                parcelCorners,
                sceneGeometry,
                ipfsPath,
                isEmpty,
                isSDK7,
                isPortableExperience
            );
        }
    }
}
