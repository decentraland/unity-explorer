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
        private static readonly SceneMetadataScene EMPTY_METADATA = new ();
        //This is considering a size of -150 to 150 parcels
        private const float PORTABLE_EXPERIENCE_MAX_VALUES = 2400f;
        private const float PORTABLE_EXPERIENCE_MAX_HEIGHT = 300f;

        private static readonly ParcelMathHelper.SceneGeometry PORTABLE_EXPERIENCES_SCENE_GEOMETRY = new ParcelMathHelper.SceneGeometry(Vector3.zero,
            new ParcelMathHelper.SceneCircumscribedPlanes(
                minX: -PORTABLE_EXPERIENCE_MAX_VALUES,
                maxX: PORTABLE_EXPERIENCE_MAX_VALUES,
                minZ: -PORTABLE_EXPERIENCE_MAX_VALUES,
                maxZ: PORTABLE_EXPERIENCE_MAX_VALUES),
            PORTABLE_EXPERIENCE_MAX_HEIGHT);
        //PX don't care about parcel corners as they work on all the map.
        private static readonly IReadOnlyList<ParcelMathHelper.ParcelCorners> PORTABLE_EXPERIENCES_PARCEL_CORNERS = new List<ParcelMathHelper.ParcelCorners>();

        public static SceneDefinitionComponent CreateFromDefinition(SceneEntityDefinition definition, IpfsPath ipfsPath, bool isPortableExperience = false) =>
            isPortableExperience ?
                CreatePortableExperienceSceneDefinitionComponent(definition, ipfsPath) :
                CreateSceneDefinitionComponent(definition, definition.metadata.scene.DecodedParcels, ipfsPath, isEmpty: false, isSDK7: definition.metadata.runtimeVersion == "7", isPortableExperience: false);

        private static SceneDefinitionComponent CreatePortableExperienceSceneDefinitionComponent(SceneEntityDefinition definition, IpfsPath ipfsPath) =>
            new (
                definition,
                parcels: definition.metadata.scene.DecodedParcels,
                PORTABLE_EXPERIENCES_PARCEL_CORNERS,
                PORTABLE_EXPERIENCES_SCENE_GEOMETRY,
                ipfsPath,
                isEmpty: false,
                isSDK7: definition.metadata.runtimeVersion == "7",
                isPortableExperience: true
            );

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
                    scene = EMPTY_METADATA,

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
