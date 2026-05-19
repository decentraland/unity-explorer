using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common;
using Org.BouncyCastle.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
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
        public bool IsSDK7 { get; }
        public ParcelMathHelper.SceneGeometry SceneGeometry { get; }
        public bool IsPortableExperience { get; }

        public int InternalJobIndex { get; set; }

        /// <summary>
        ///     Initial Scene State descriptor for this scene. Defaults to <see cref="ISSDescriptor.NONE"/>;
        ///     populated by <c>ResolveISSDescriptorSystem</c> once the lazy promise resolves. Read in [Query]
        ///     systems (UpdateSceneLODInfoSystem, ResolveISSLODSystem) that don't have an ISceneData handle.
        /// </summary>
        public ISSDescriptor ISSDescriptor;

        /// <summary>
        ///     In-flight ISS descriptor promise — <c>AssetPromise.NULL</c> while either no promise is active
        ///     or the promise has been consumed and the result stashed into <see cref="ISSDescriptor"/>.
        /// </summary>
        public AssetPromise<ISSDescriptor, GetISSDescriptor> ISSDescriptorPromise;

        /// <summary>
        ///     True once the descriptor promise has resolved (to a real descriptor OR to NONE). Lets
        ///     <c>ResolveISSDescriptorSystem</c> distinguish "not started" from "resolved-to-NONE" — both
        ///     of which leave <see cref="ISSDescriptor"/> as the NONE singleton.
        /// </summary>
        public bool ISSDescriptorResolved;


        public float EstimatedMemoryUsageInMB;
        public float EstimatedMemoryUsageForLODMB;
        public float EstimatedMemoryUsageForQualityReductedLODMB;

        public SceneDefinitionComponent(
            SceneEntityDefinition definition,
            IReadOnlyList<Vector2Int> parcels,
            IReadOnlyList<ParcelMathHelper.ParcelCorners> parcelsCorners,
            ParcelMathHelper.SceneGeometry sceneGeometry,
            IpfsPath ipfsPath, bool isSDK7, bool isPortableExperience)
        {
            Definition = definition;
            Parcels = parcels;
            ParcelsCorners = parcelsCorners;
            IpfsPath = ipfsPath;
            IsSDK7 = isSDK7;
            SceneGeometry = sceneGeometry;
            InternalJobIndex = -1;
            IsPortableExperience = isPortableExperience;
            ISSDescriptor = ISSDescriptor.NONE;
            ISSDescriptorPromise = AssetPromise<ISSDescriptor, GetISSDescriptor>.NULL;
            ISSDescriptorResolved = false;

            EstimatedMemoryUsageInMB = Mathf.Clamp(parcels.Count * 15, 0, SceneLoadingMemoryConstants.MAX_SCENE_SIZE);
            EstimatedMemoryUsageForLODMB = (EstimatedMemoryUsageInMB / SceneLoadingMemoryConstants.LOD_REDUCTION) + (EstimatedMemoryUsageInMB / SceneLoadingMemoryConstants.QUALITY_REDUCTED_LOD_REDUCTION);
            EstimatedMemoryUsageForQualityReductedLODMB = EstimatedMemoryUsageInMB / SceneLoadingMemoryConstants.QUALITY_REDUCTED_LOD_REDUCTION;
        }

        //Used in hot path to avoid additional getters
        public readonly bool Contains(int x, int y) =>
            Definition.Contains(x, y);

        public bool Contains(Vector2Int parcel) =>
            Definition.Contains(parcel);
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
                CreatePortableExperienceSceneDefinitionComponent(definition, ipfsPath) : CreateSceneDefinitionComponent(definition, definition.metadata.scene.DecodedParcels, ipfsPath, isSDK7: definition.metadata.runtimeVersion == "7", isPortableExperience: false);

        private static SceneDefinitionComponent CreatePortableExperienceSceneDefinitionComponent(SceneEntityDefinition definition, IpfsPath ipfsPath) =>
            new (
                definition,
                parcels: definition.metadata.scene.DecodedParcels,
                PORTABLE_EXPERIENCES_PARCEL_CORNERS,
                PORTABLE_EXPERIENCES_SCENE_GEOMETRY,
                ipfsPath,
                isSDK7: definition.metadata.runtimeVersion == "7",
                isPortableExperience: true
            );

        private static SceneDefinitionComponent CreateSceneDefinitionComponent(
            SceneEntityDefinition definition,
            IReadOnlyList<Vector2Int> parcels,
            IpfsPath ipfsPath,
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
                isSDK7,
                isPortableExperience
            );
        }
    }
}
